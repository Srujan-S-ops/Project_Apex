using System;
using UnityEngine;

namespace Apex.Vehicle
{
    /// <summary>
    /// Rigidbody + WheelCollider GT3 simulation: RWD drivetrain, torque curve, 6-speed automatic with
    /// reverse, rev limiter, brake bias, ABS, traction control, Ackermann steering, aerodynamic
    /// downforce/drag, anti-roll bars. Driven exclusively through ICarInput (player or AI).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class CarController : MonoBehaviour
    {
        public const int FL = 0, FR = 1, RL = 2, RR = 3;
        private const float AirDensity = 1.225f;
        private float SuspensionDistance = 0.14f;

        public CarSpec Spec { get; private set; }
        public ICarInput Input { get; set; }
        public Rigidbody Body { get; private set; }
        public WheelCollider[] Wheels { get; private set; }

        // driver aids
        public bool AbsEnabled = true;
        public bool TcsEnabled = true;
        public float SteerAssist;            // 0..1 (counter-steer + yaw damping)
        public bool InputLocked;             // grid / countdown: engine revs, car held

        // telemetry (read by HUD, audio, AI, effects)
        public float SpeedMs { get; private set; }
        public float ForwardSpeed { get; private set; }
        public float SpeedKmh => SpeedMs * 3.6f;
        public float EngineRpm { get; private set; }
        public float RpmNormalized => Spec == null ? 0f : Mathf.InverseLerp(Spec.IdleRpm, Spec.MaxRpm, EngineRpm);
        public int Gear { get; private set; } = 1;   // -1 reverse, 1..6
        public float ThrottleInput { get; private set; }
        public float BrakeInput { get; private set; }
        public float SteerInput { get; private set; }
        public float SteerAngle { get; private set; }
        public bool AbsActive { get; private set; }
        public bool TcsActive { get; private set; }
        public bool RevLimiterHit { get; private set; }
        public float ShiftCut => _shiftTimer > 0f ? 1f : 0f;
        public float[] ForwardSlip { get; } = new float[4];
        public float[] SideSlip { get; } = new float[4];
        public bool[] Grounded { get; } = new bool[4];
        public float[] BrakeHeat { get; } = new float[4];   // 0..1 used for disc glow
        public float LongitudinalG { get; private set; }
        public float LateralG { get; private set; }
        public bool AnyWheelGrounded { get; private set; }
        public float SlipAngleDeg { get; private set; }

        public event Action<int, int> GearChanged;            // (from, to)
        public event Action<float, Vector3> Collided;         // (impact speed, point)
        public int CarContacts { get; private set; }          // impacts > 4 m/s with another car
        public int WallContacts { get; private set; }         // impacts > 4 m/s with scenery
        public event Action Backfire;

        private CarInputState _input;
        private float _shiftTimer;
        private float _shiftCooldown;
        private float _reverseTimer;
        private float _tcsFactor = 1f;
        private float _absPhase;
        private float _limiterTimer;
        private Vector3 _lastVelocity;
        private float _liftOffTimer;
        private readonly float[] _wheelTorque = new float[4];

        public void Setup(CarSpec spec)
        {
            Spec = spec;
            SuspensionDistance = spec.SuspensionDistance;
            Body = GetComponent<Rigidbody>();
            Body.mass = spec.MassKg;
            Body.linearDamping = 0f;
            Body.angularDamping = 0.08f;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            Body.maxAngularVelocity = 12f;

            Wheels = new WheelCollider[4];
            var root = new GameObject("WheelColliders").transform;
            root.SetParent(transform, false);
            float frontZ = spec.Wheelbase * 0.51f;
            float rearZ = -spec.Wheelbase * 0.49f;
            float halfTrack = spec.TrackWidth * 0.5f;
            Vector3[] pos =
            {
                new Vector3(-halfTrack, spec.WheelRadius + SuspensionDistance * 0.5f, frontZ),
                new Vector3(halfTrack, spec.WheelRadius + SuspensionDistance * 0.5f, frontZ),
                new Vector3(-halfTrack - 0.02f, spec.WheelRadius + SuspensionDistance * 0.5f, rearZ),
                new Vector3(halfTrack + 0.02f, spec.WheelRadius + SuspensionDistance * 0.5f, rearZ)
            };
            string[] names = { "FL", "FR", "RL", "RR" };
            for (int i = 0; i < 4; i++)
            {
                var go = new GameObject("Wheel_" + names[i]);
                go.transform.SetParent(root, false);
                go.transform.localPosition = pos[i];
                var wc = go.AddComponent<WheelCollider>();
                ConfigureWheel(wc, i < 2);
                Wheels[i] = wc;
            }
            Wheels[0].ConfigureVehicleSubsteps(5f, 16, 22);
            Body.centerOfMass = new Vector3(0f, spec.CenterOfMassHeight, 0.04f);
            Body.ResetInertiaTensor();
            Body.inertiaTensor *= 1.1f;
            EngineRpm = spec.IdleRpm;
        }

        private void ConfigureWheel(WheelCollider wc, bool front)
        {
            wc.mass = Spec.WheelMass;
            wc.radius = Spec.WheelRadius;
            wc.wheelDampingRate = 0.3f;
            wc.suspensionDistance = SuspensionDistance;
            wc.forceAppPointDistance = 0.12f;
            var spring = new JointSpring
            {
                spring = front ? Spec.SpringFront : Spec.SpringRear,
                damper = front ? Spec.DamperFront : Spec.DamperRear,
                targetPosition = 0.45f
            };
            wc.suspensionSpring = spring;
            float g = Spec.GripMultiplier;
            wc.forwardFriction = new WheelFrictionCurve
            {
                extremumSlip = 0.32f, extremumValue = 1f,
                asymptoteSlip = 0.9f, asymptoteValue = 0.72f,
                stiffness = 1.75f * g
            };
            wc.sidewaysFriction = new WheelFrictionCurve
            {
                extremumSlip = 0.22f, extremumValue = 1f,
                asymptoteSlip = 0.62f, asymptoteValue = 0.8f,
                stiffness = (front ? 1.85f : 1.95f) * g
            };
        }

        /// <summary>Teleports the car (grid placement, recovery).</summary>
        public void PlaceAt(Vector3 position, Quaternion rotation)
        {
            Body.isKinematic = true;
            transform.SetPositionAndRotation(position, rotation);
            Body.position = position;
            Body.rotation = rotation;
            Body.isKinematic = false;
            Body.linearVelocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
            foreach (var w in Wheels) { w.motorTorque = 0f; w.brakeTorque = 1e5f; }
            Gear = 1;
            EngineRpm = Spec.IdleRpm;
            _tcsFactor = 1f;
        }

        public void SetFrozen(bool frozen)
        {
            Body.isKinematic = frozen;
            if (frozen) { Body.linearVelocity = Vector3.zero; Body.angularVelocity = Vector3.zero; }
        }

        private void FixedUpdate()
        {
            if (Spec == null || Body.isKinematic) return;
            float dt = Time.fixedDeltaTime;

            _input = Input != null ? Input.ReadInput(this, dt) : CarInputState.None;
            _input.Throttle = Mathf.Clamp01(_input.Throttle);
            _input.Brake = Mathf.Clamp01(_input.Brake);
            _input.Steer = Mathf.Clamp(_input.Steer, -1f, 1f);

            Vector3 vel = Body.linearVelocity;
            SpeedMs = vel.magnitude;
            Vector3 localVel = transform.InverseTransformDirection(vel);
            ForwardSpeed = localVel.z;
            SlipAngleDeg = SpeedMs > 3f ? Mathf.Atan2(localVel.x, Mathf.Abs(localVel.z)) * Mathf.Rad2Deg : 0f;
            Vector3 accel = (vel - _lastVelocity) / dt;
            _lastVelocity = vel;
            Vector3 localAccel = transform.InverseTransformDirection(accel);
            LongitudinalG = Mathf.Lerp(LongitudinalG, localAccel.z / 9.81f, dt * 8f);
            LateralG = Mathf.Lerp(LateralG, localAccel.x / 9.81f, dt * 8f);

            ReadWheels();
            UpdateGearbox(dt);
            UpdateEngine(dt);
            ApplySteering(dt);
            ApplyDriveAndBrakes(dt);
            ApplyAero();
            ApplyAntiRoll();
            ApplyAssists();
        }

        private void ReadWheels()
        {
            AnyWheelGrounded = false;
            for (int i = 0; i < 4; i++)
            {
                if (Wheels[i].GetGroundHit(out WheelHit hit))
                {
                    Grounded[i] = true;
                    ForwardSlip[i] = hit.forwardSlip;
                    SideSlip[i] = hit.sidewaysSlip;
                    AnyWheelGrounded = true;
                }
                else
                {
                    Grounded[i] = false;
                    ForwardSlip[i] = 0f;
                    SideSlip[i] = 0f;
                }
            }
        }

        // ---------------------------------------------------------------- gearbox
        private void UpdateGearbox(float dt)
        {
            _shiftTimer -= dt;
            _shiftCooldown -= dt;
            float throttle = _input.Throttle, brake = _input.Brake;

            if (InputLocked) { Gear = 1; return; }

            if (Gear >= 1)
            {
                // engage reverse: nearly stopped and holding brake without throttle
                if (ForwardSpeed < 0.8f && brake > 0.5f && throttle < 0.1f)
                {
                    _reverseTimer += dt;
                    if (_reverseTimer > 0.45f) { ChangeGear(-1); _reverseTimer = 0f; return; }
                }
                else _reverseTimer = 0f;

                if (_shiftCooldown > 0f) return;
                int idx = Gear - 1;
                float drivenRpm = DrivenRpm(Gear);
                if (Gear < Spec.GearRatios.Length && drivenRpm > Spec.MaxRpm * 0.95f && throttle > 0.2f)
                {
                    ChangeGear(Gear + 1);
                }
                else if (Gear > 1)
                {
                    float lowerRpm = drivenRpm * Spec.GearRatios[idx - 1] / Spec.GearRatios[idx];
                    float downThreshold = Mathf.Lerp(0.42f, 0.62f, Mathf.Max(throttle, brake * 0.9f)) * Spec.MaxRpm;
                    if (drivenRpm < downThreshold && lowerRpm < Spec.MaxRpm * 0.9f) ChangeGear(Gear - 1);
                }
            }
            else // reverse
            {
                if (throttle > 0.1f && ForwardSpeed > -1.5f) ChangeGear(1);
            }
        }

        private void ChangeGear(int to)
        {
            int from = Gear;
            if (from == to) return;
            Gear = to;
            _shiftTimer = to > from && from >= 1 ? 0.09f : 0.05f;
            _shiftCooldown = 0.45f;
            GearChanged?.Invoke(from, to);
        }

        private float GearRatio(int gear) => gear < 0 ? Spec.ReverseRatio : Spec.GearRatios[Mathf.Clamp(gear - 1, 0, Spec.GearRatios.Length - 1)];

        private float DrivenRpm(int gear)
        {
            float wheelRpm = (Mathf.Abs(Wheels[RL].rpm) + Mathf.Abs(Wheels[RR].rpm)) * 0.5f;
            // guard against spinning wheels in the air dominating
            float groundRpm = Mathf.Abs(ForwardSpeed) / (2f * Mathf.PI * Spec.WheelRadius) * 60f;
            float rpmW = Mathf.Min(wheelRpm, groundRpm * 1.6f + 50f);
            return rpmW * GearRatio(gear) * Spec.FinalDrive;
        }

        // ---------------------------------------------------------------- engine
        private void UpdateEngine(float dt)
        {
            float throttle = Gear < 0 ? _input.Brake : _input.Throttle;
            float target;
            if (InputLocked || !AnyWheelGrounded)
            {
                target = Spec.IdleRpm + throttle * (Spec.MaxRpm - Spec.IdleRpm) * 0.92f;
            }
            else
            {
                float driven = DrivenRpm(Gear);
                target = driven;
                // clutch slip for launches
                float slipRpm = Spec.IdleRpm + throttle * Spec.MaxRpm * 0.42f;
                if ((Gear == 1 || Gear < 0) && driven < slipRpm) target = Mathf.Lerp(driven, slipRpm, 0.85f);
            }
            target = Mathf.Clamp(target, Spec.IdleRpm, Spec.MaxRpm + 150f);
            float rate = InputLocked ? (target > EngineRpm ? 6f : 3f) : 18f;
            EngineRpm = Mathf.Lerp(EngineRpm, target, dt * rate);

            _limiterTimer -= dt;
            RevLimiterHit = false;
            if (EngineRpm >= Spec.MaxRpm - 20f)
            {
                _limiterTimer = 0.06f;
                RevLimiterHit = true;
            }

            // lift-off backfire
            if (_input.Throttle < 0.05f && EngineRpm > Spec.MaxRpm * 0.6f && SpeedMs > 15f)
            {
                _liftOffTimer += dt;
                if (_liftOffTimer < dt * 1.5f || UnityEngine.Random.value < dt * 2.5f) Backfire?.Invoke();
            }
            else _liftOffTimer = 0f;
        }

        // ---------------------------------------------------------------- steering
        private void ApplySteering(float dt)
        {
            SteerInput = _input.Steer;
            float speedFactor = Mathf.Clamp01(SpeedMs / 70f);
            float maxAngle = Mathf.Lerp(Spec.MaxSteerDeg, 8.5f, Mathf.Pow(speedFactor, 0.75f));
            float angle = _input.Steer * maxAngle;

            if (SteerAssist > 0f && SpeedMs > 6f && Gear > 0)
            {
                // counter-steer toward the velocity vector when the rear steps out
                angle += Mathf.Clamp(SlipAngleDeg * 0.55f, -12f, 12f) * SteerAssist;
            }
            angle = Mathf.Clamp(angle, -Spec.MaxSteerDeg, Spec.MaxSteerDeg);
            SteerAngle = angle;

            // Ackermann geometry
            if (Mathf.Abs(angle) > 0.01f)
            {
                float L = Spec.Wheelbase, T = Spec.TrackWidth;
                float r = L / Mathf.Tan(Mathf.Abs(angle) * Mathf.Deg2Rad);
                float inner = Mathf.Atan(L / (r - T * 0.5f)) * Mathf.Rad2Deg;
                float outer = Mathf.Atan(L / (r + T * 0.5f)) * Mathf.Rad2Deg;
                float s = Mathf.Sign(angle);
                // turning right (positive): right wheel is inner
                Wheels[FR].steerAngle = s > 0 ? inner : -outer;
                Wheels[FL].steerAngle = s > 0 ? outer : -inner;
            }
            else
            {
                Wheels[FL].steerAngle = 0f;
                Wheels[FR].steerAngle = 0f;
            }
        }

        // ---------------------------------------------------------------- drive & brakes
        private void ApplyDriveAndBrakes(float dt)
        {
            float throttle = _input.Throttle, brake = _input.Brake;
            if (Gear < 0) { float t = throttle; throttle = brake; brake = t; }
            ThrottleInput = _input.Throttle;
            BrakeInput = _input.Brake;

            if (InputLocked)
            {
                for (int i = 0; i < 4; i++) { Wheels[i].motorTorque = 0f; Wheels[i].brakeTorque = Spec.BrakeTorqueNm; }
                AbsActive = TcsActive = false;
                return;
            }

            // engine torque → rear axle
            float engineTorque = Spec.MaxTorqueNm * Spec.TorqueCurve(EngineRpm) * throttle;
            if (_shiftTimer > 0f || _limiterTimer > 0f) engineTorque = 0f;
            float axleTorque = engineTorque * GearRatio(Gear) * Spec.FinalDrive * Spec.DrivetrainEfficiency;
            if (Gear < 0) axleTorque = -axleTorque;

            // engine braking
            if (throttle < 0.05f && Gear > 0 && ForwardSpeed > 3f)
                axleTorque -= 55f * GearRatio(Gear) * Spec.FinalDrive * RpmNormalized;

            // traction control
            TcsActive = false;
            if (TcsEnabled && throttle > 0.1f && Gear > 0)
            {
                float rearSlip = Mathf.Max(ForwardSlip[RL], ForwardSlip[RR]);
                if (rearSlip > 0.28f) { _tcsFactor = Mathf.Max(0.15f, _tcsFactor - dt * 7f); TcsActive = true; }
                else _tcsFactor = Mathf.Min(1f, _tcsFactor + dt * 3f);
            }
            else _tcsFactor = Mathf.Min(1f, _tcsFactor + dt * 4f);
            axleTorque *= _tcsFactor;

            Wheels[FL].motorTorque = 0f;
            Wheels[FR].motorTorque = 0f;
            Wheels[RL].motorTorque = axleTorque * 0.5f;
            Wheels[RR].motorTorque = axleTorque * 0.5f;

            // brakes with ABS
            _absPhase += dt;
            bool absOpen = (_absPhase % 0.07f) < 0.035f;
            AbsActive = false;
            float total = Spec.BrakeTorqueNm * brake;
            for (int i = 0; i < 4; i++)
            {
                bool front = i < 2;
                float torque = total * (front ? Spec.BrakeBias : 1f - Spec.BrakeBias) * 0.5f;
                if (AbsEnabled && brake > 0.1f && SpeedMs > 3f && Grounded[i] && ForwardSlip[i] < -0.24f)
                {
                    torque *= absOpen ? 0.25f : 0.7f;
                    AbsActive = true;
                }
                if (!front && _input.Handbrake) torque = Mathf.Max(torque, 4500f);
                // tiny holding brake when stationary and idle so the car does not creep
                if (throttle < 0.02f && brake < 0.02f && SpeedMs < 0.4f) torque = Mathf.Max(torque, 60f);
                Wheels[i].brakeTorque = torque;
                _wheelTorque[i] = torque;

                float heatIn = torque / Spec.BrakeTorqueNm * SpeedMs * 0.012f;
                BrakeHeat[i] = Mathf.Clamp01(BrakeHeat[i] + heatIn * dt * 6f - dt * 0.12f * (0.3f + BrakeHeat[i]));
            }

            // handbrake reduces rear lateral grip for slides
            float rearSide = _input.Handbrake ? 1.1f : 1.95f;
            for (int i = RL; i <= RR; i++)
            {
                var f = Wheels[i].sidewaysFriction;
                float target = rearSide * Spec.GripMultiplier;
                if (Mathf.Abs(f.stiffness - target) > 0.01f)
                {
                    f.stiffness = Mathf.MoveTowards(f.stiffness, target, dt * 4f);
                    Wheels[i].sidewaysFriction = f;
                }
            }
        }

        // ---------------------------------------------------------------- aero
        private void ApplyAero()
        {
            float v2 = SpeedMs * SpeedMs;
            if (v2 < 0.01f) return;
            float downforce = 0.5f * AirDensity * Spec.DownforceClA * v2;
            float frontZ = Spec.Wheelbase * 0.51f, rearZ = -Spec.Wheelbase * 0.49f;
            Vector3 down = -transform.up;
            if (AnyWheelGrounded)
            {
                Body.AddForceAtPosition(down * downforce * Spec.AeroBalanceFront, transform.TransformPoint(0f, 0.3f, frontZ));
                Body.AddForceAtPosition(down * downforce * (1f - Spec.AeroBalanceFront), transform.TransformPoint(0f, 0.3f, rearZ));
            }
            float drag = 0.5f * AirDensity * Spec.DragCdA * v2;
            Vector3 v = Body.linearVelocity;
            Body.AddForce(-v.normalized * drag);
            if (AnyWheelGrounded && SpeedMs > 0.5f)
                Body.AddForce(-v.normalized * 0.014f * Spec.MassKg * 9.81f);
        }

        private void ApplyAntiRoll()
        {
            AntiRoll(Wheels[FL], Wheels[FR], Spec.AntiRollFront);
            AntiRoll(Wheels[RL], Wheels[RR], Spec.AntiRollRear);
        }

        private void AntiRoll(WheelCollider left, WheelCollider right, float stiffness)
        {
            float travelL = 1f, travelR = 1f;
            bool gl = left.GetGroundHit(out WheelHit hl);
            bool gr = right.GetGroundHit(out WheelHit hr);
            if (gl) travelL = (-left.transform.InverseTransformPoint(hl.point).y - left.radius) / left.suspensionDistance;
            if (gr) travelR = (-right.transform.InverseTransformPoint(hr.point).y - right.radius) / right.suspensionDistance;
            float force = (travelL - travelR) * stiffness;
            if (gl) Body.AddForceAtPosition(left.transform.up * -force, left.transform.position);
            if (gr) Body.AddForceAtPosition(right.transform.up * force, right.transform.position);
        }

        private void ApplyAssists()
        {
            if (SteerAssist <= 0f || !AnyWheelGrounded) return;
            // yaw damping when the driver is not asking for rotation
            float yawRate = Vector3.Dot(Body.angularVelocity, transform.up);
            float k = (1f - Mathf.Abs(_input.Steer)) * 0.9f * SteerAssist;
            Body.AddTorque(transform.up * (-yawRate * k), ForceMode.Acceleration);
        }

        private void OnCollisionEnter(Collision c)
        {
            if (c.contactCount == 0) return;
            // ContinuousSpeculative CCD reports contacts that are still apart (margin grows with speed, ~0.8 m at
            // 80 m/s). Those are not impacts: ignore them for effects and contact statistics.
            // A contact is real when its gap closes within one physics step at the closing speed along its normal.
            int best = -1;
            float bestMargin = float.MaxValue;
            for (int i = 0; i < c.contactCount; i++)
            {
                var cp = c.GetContact(i);
                float closing = Mathf.Abs(Vector3.Dot(c.relativeVelocity, cp.normal));
                float margin = cp.separation - closing * Time.fixedDeltaTime - 0.02f;
                if (margin < bestMargin) { bestMargin = margin; best = i; }
            }
            if (best < 0 || bestMargin > 0f) return;
            var contact = c.GetContact(best);
            float impact = c.relativeVelocity.magnitude;
            if (impact > 4f)
            {
                if (c.rigidbody != null) CarContacts++;
                else if (Mathf.Abs(contact.normal.y) < 0.5f) WallContacts++;
            }
            if (impact > 1.5f) Collided?.Invoke(impact, contact.point);
        }

        /// <summary>True when the car is upside down or on its side.</summary>
        public bool IsFlipped => Vector3.Dot(transform.up, Vector3.up) < 0.3f;
    }
}

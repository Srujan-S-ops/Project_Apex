using System.Collections.Generic;
using Apex.Race;
using Apex.Track;
using Apex.Vehicle;
using UnityEngine;

namespace Apex.AI
{
    /// <summary>
    /// Physics-driven AI: produces throttle/brake/steer for the same CarController the player uses.
    /// Layers: (1) racing line + speed profile tracking, (2) tactical command (Java or local),
    /// (3) reactive collision avoidance / overtaking / side-by-side, (4) mistake recovery.
    /// </summary>
    public sealed class AIDriver : ICarInput
    {
        public AIProfile Profile { get; }
        public AICommand Command { get; private set; } = AICommand.Default;
        public bool RaceActive;
        public string DebugState = "";
        public float TargetSpeed { get; private set; }

        private readonly AITrackModel _model;
        private readonly RaceParticipant _self;
        private readonly IReadOnlyList<RaceParticipant> _field;
        private readonly int _seed;
        private float _avoidOffset;
        private float _tacticalBlend;
        private float _reverseTimer;
        private float _stuckTimer;
        private float _noiseT;
        private float _prevSteer;

        public System.Action<RaceParticipant> RequestRespawn;

        public AIDriver(AITrackModel model, RaceParticipant self, IReadOnlyList<RaceParticipant> field, AIProfile profile, int seed)
        {
            _model = model; _self = self; _field = field; Profile = profile; _seed = seed;
            _noiseT = seed * 13.7f;
        }

        public void SetCommand(AICommand c)
        {
            c.SpeedScale = Mathf.Clamp(c.SpeedScale, 0.85f, 1.06f);
            Command = c;
        }

        public CarInputState ReadInput(CarController car, float dt)
        {
            var spline = _model.Spline;
            float speed = car.SpeedMs;
            float d = _self.CentreDistance;
            int idx = _self.TrackIndex < 0 ? 0 : _self.TrackIndex;
            float halfWidth = spline.Width[idx] * 0.5f;

            // ---------- recovery: stuck against something
            if (_reverseTimer > 0f)
            {
                _reverseTimer -= dt;
                DebugState = "REVERSE";
                float back = -Mathf.Sign(_self.Lateral) * 0.8f;
                return new CarInputState { Throttle = 0f, Brake = 1f, Steer = car.Gear < 0 ? back : 0f };
            }
            if (RaceActive && speed < 1.2f && !car.InputLocked)
            {
                _stuckTimer += dt;
                if (_stuckTimer > 2.2f) { _reverseTimer = 1.6f; _stuckTimer = -1.5f; }
                if (_stuckTimer > 6.5f) { RequestRespawn?.Invoke(_self); _stuckTimer = 0f; }
            }
            else if (_stuckTimer > 0f) _stuckTimer = Mathf.Max(0f, _stuckTimer - dt * 2f);

            // ---------- tactical layer
            var cmd = Command;
            if (Time.realtimeSinceStartup - cmd.ReceivedAt > 1.5f) cmd = LocalTactics.Decide(_self, _field, Profile, spline);
            bool tactical = cmd.Mode == TacticalMode.Overtake || cmd.Mode == TacticalMode.Defend;
            _tacticalBlend = Mathf.MoveTowards(_tacticalBlend, tactical ? 1f : 0f, dt * 0.8f);

            // ---------- reactive layer: traffic
            float follow = float.MaxValue;
            float avoidTarget = 0f;
            float myLine = _model.OffsetAt(d);
            float baseLateral = Mathf.Lerp(myLine, cmd.LateralTarget, _tacticalBlend * 0.8f);
            foreach (var o in _field)
            {
                if (o == _self || o.Car == null || o.GhostTimer > 0f || _self.GhostTimer > 0f) continue;
                float along = spline.Delta(d, o.CentreDistance);
                float dLat = o.Lateral - _self.Lateral;
                if (along > -6f && along < 5.5f && Mathf.Abs(dLat) < 2.5f)
                {
                    // alongside: keep a car's width
                    avoidTarget += -Mathf.Sign(dLat == 0 ? 1f : dLat) * (2.6f - Mathf.Abs(dLat)) * 1.2f;
                }
                else if (along >= 5.5f && along < 45f + speed * 0.4f)
                {
                    float closing = speed - o.Car.SpeedMs;
                    float timeGap = along / Mathf.Max(speed, 1f);
                    bool blocking = Mathf.Abs(o.Lateral - (baseLateral + _avoidOffset)) < 2.4f;
                    if (!blocking) continue;
                    if (closing > 0.5f || timeGap < 0.7f)
                    {
                        // choose a side with room, preferring the inside of the next corner
                        float roomLeft = (o.Lateral - 2.9f) - (-halfWidth + 1.2f);
                        float roomRight = (halfWidth - 1.2f) - (o.Lateral + 2.9f);
                        float next = spline.Curvature[spline.IndexAt(o.CentreDistance + 40f)];
                        float prefer = next > 0.004f ? 1f : (next < -0.004f ? -1f : Mathf.Sign(_self.Lateral - o.Lateral + 0.01f));
                        int side = 0;
                        if (prefer > 0 && roomRight > 0f) side = 1;
                        else if (prefer < 0 && roomLeft > 0f) side = -1;
                        else if (roomRight > roomLeft && roomRight > 0f) side = 1;
                        else if (roomLeft > 0f) side = -1;

                        bool attempt = side != 0 && (Profile.Eagerness > 0.5f || cmd.Mode == TacticalMode.Overtake || closing > 4f);
                        if (attempt)
                            avoidTarget = (o.Lateral + side * 3.0f) - baseLateral;
                        else
                            follow = Mathf.Min(follow, o.Car.SpeedMs + (along - Profile.FollowGap) * 0.45f);
                    }
                }
            }
            float avoidRate = 3.2f + Profile.Eagerness * 2f;
            _avoidOffset = Mathf.MoveTowards(_avoidOffset, avoidTarget, avoidRate * dt);
            if (Mathf.Abs(avoidTarget) < 0.01f) _avoidOffset = Mathf.MoveTowards(_avoidOffset, 0f, 1.5f * dt);

            // ---------- path following (pure pursuit)
            float look = Mathf.Clamp(7f + speed * 0.42f, 9f, 40f);
            float lateralAhead = Mathf.Lerp(_model.OffsetAt(d + look), cmd.LateralTarget, _tacticalBlend * 0.8f) + _avoidOffset;
            float limit = spline.Width[spline.IndexAt(d + look)] * 0.5f - 1.15f;
            lateralAhead = Mathf.Clamp(lateralAhead, -limit, limit);
            Vector3 target = spline.PointAt(d + look, lateralAhead);
            Vector3 local = car.transform.InverseTransformPoint(target);
            float alpha = Mathf.Atan2(local.x, Mathf.Max(local.z, 0.5f));
            float steerAngle = Mathf.Atan(2f * car.Spec.Wheelbase * Mathf.Sin(alpha) / look) * Mathf.Rad2Deg;
            float maxAngle = Mathf.Lerp(car.Spec.MaxSteerDeg, 8.5f, Mathf.Pow(Mathf.Clamp01(speed / 70f), 0.75f));
            float steer = steerAngle / maxAngle;
            // counter-steer when sliding
            steer += Mathf.Clamp(car.SlipAngleDeg / maxAngle * 0.6f, -0.5f, 0.5f);
            _noiseT += dt;
            steer += (Mathf.PerlinNoise(_noiseT * 0.7f, _seed) - 0.5f) * Profile.SteerNoise * 2f;
            steer = Mathf.Clamp(steer, -1f, 1f);
            steer = Mathf.Lerp(_prevSteer, steer, Mathf.Clamp01(dt * 18f));
            _prevSteer = steer;

            // ---------- speed control
            float pace = Profile.Pace * cmd.SpeedScale * (1f + (Mathf.PerlinNoise(_seed, _noiseT * 0.13f) - 0.5f) * Profile.Consistency * 2f);
            float decel = 9.5f + Profile.CornerCommitment * 1.5f;
            float anticipation = speed * (0.3f + Profile.BrakingMargin);
            float limitSpeed = _model.SpeedLimitAhead(d + anticipation, 160f, decel);
            bool inCorner = Mathf.Abs(spline.Curvature[idx]) > 1f / 200f;
            float tgt = limitSpeed * pace * (inCorner ? Profile.CornerCommitment : 1f);
            // off the racing line → lower grip margin
            float offLine = Mathf.Abs(lateralAhead - _model.OffsetAt(d + look));
            tgt *= Mathf.Lerp(1f, 0.93f, Mathf.Clamp01(offLine / 4f) * (inCorner ? 1f : 0.2f));
            tgt = Mathf.Min(tgt, follow);
            if (_self.OffTrack) tgt = Mathf.Min(tgt, 22f);
            TargetSpeed = tgt;

            float err = tgt - speed;
            float throttle, brake;
            if (err > 0.3f) { throttle = Mathf.Clamp01(0.45f + err * 0.22f); brake = 0f; }
            else if (err < -0.8f) { throttle = 0f; brake = Mathf.Clamp01(-err * 0.16f + 0.15f); }
            else { throttle = 0.3f; brake = 0f; }
            if (Mathf.Abs(car.SlipAngleDeg) > 9f) throttle *= 0.55f;
            if (!RaceActive) { throttle = car.InputLocked ? 0.35f + 0.25f * Mathf.PerlinNoise(_noiseT * 2f, _seed) : 0f; brake = car.InputLocked ? 0f : 1f; }

            DebugState = $"{cmd.Mode} tgt {tgt * 3.6f:0} avoid {_avoidOffset:0.0}";
            return new CarInputState { Throttle = throttle, Brake = brake, Steer = steer };
        }
    }
}

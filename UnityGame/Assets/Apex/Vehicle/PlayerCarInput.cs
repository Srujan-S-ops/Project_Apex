using Apex.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Apex.Vehicle
{
    /// <summary>Keyboard / gamepad driver input with digital-input smoothing.</summary>
    public sealed class PlayerCarInput : ICarInput
    {
        private float _steer, _throttle, _brake;
        private readonly GameSettings _settings;

        public PlayerCarInput(GameSettings settings)
        {
            _settings = settings;
            ApexControls.Initialise();
        }

        private static bool UsingGamepad => Gamepad.current != null &&
            (Mathf.Abs(Gamepad.current.leftStick.x.ReadValue()) > 0.05f || Gamepad.current.rightTrigger.ReadValue() > 0.05f || Gamepad.current.leftTrigger.ReadValue() > 0.05f);

        public CarInputState ReadInput(CarController car, float dt)
        {
            float rawSteer = ApexControls.Steer.ReadValue<float>();
            float rawThrottle = ApexControls.Throttle.ReadValue<float>();
            float rawBrake = ApexControls.Brake.ReadValue<float>();

            if (UsingGamepad)
            {
                float s = Mathf.Sign(rawSteer) * Mathf.Pow(Mathf.Abs(rawSteer), _settings.steeringLinearity);
                _steer = Mathf.MoveTowards(_steer, s * _settings.steeringSensitivity, dt * 8f);
                _throttle = rawThrottle;
                _brake = rawBrake;
            }
            else
            {
                // digital keys: ramp in, faster return to centre, slower at speed
                float speedK = Mathf.Lerp(1f, 0.55f, Mathf.Clamp01(car.SpeedMs / 60f));
                float target = rawSteer * _settings.steeringSensitivity;
                bool returning = Mathf.Abs(target) < Mathf.Abs(_steer) || Mathf.Sign(target) != Mathf.Sign(_steer);
                float rate = (returning ? 5.5f : 2.8f) * speedK;
                _steer = Mathf.MoveTowards(_steer, target, rate * dt);
                _throttle = Mathf.MoveTowards(_throttle, rawThrottle, dt * 6f);
                _brake = Mathf.MoveTowards(_brake, rawBrake, dt * 8f);
            }

            return new CarInputState
            {
                Steer = Mathf.Clamp(_steer, -1f, 1f),
                Throttle = _throttle,
                Brake = _brake,
                Handbrake = ApexControls.Handbrake.IsPressed()
            };
        }
    }
}

using UnityEngine.InputSystem;

namespace Apex.Core
{
    /// <summary>Input System actions defined in code (keyboard + gamepad).</summary>
    public static class ApexControls
    {
        public static readonly InputAction Throttle = new InputAction("Throttle", InputActionType.Value);
        public static readonly InputAction Brake = new InputAction("Brake", InputActionType.Value);
        public static readonly InputAction Steer = new InputAction("Steer", InputActionType.Value);
        public static readonly InputAction Handbrake = new InputAction("Handbrake", InputActionType.Button);
        public static readonly InputAction CameraNext = new InputAction("CameraNext", InputActionType.Button);
        public static readonly InputAction LookBack = new InputAction("LookBack", InputActionType.Button);
        public static readonly InputAction ResetCar = new InputAction("ResetCar", InputActionType.Button);
        public static readonly InputAction Pause = new InputAction("Pause", InputActionType.Button);
        public static readonly InputAction OrbitDelta = new InputAction("OrbitDelta", InputActionType.Value);
        public static readonly InputAction OrbitHold = new InputAction("OrbitHold", InputActionType.Button);

        private static bool _initialised;

        public static void Initialise()
        {
            if (_initialised) return;
            _initialised = true;

            Throttle.AddBinding("<Keyboard>/w");
            Throttle.AddBinding("<Keyboard>/upArrow");
            Throttle.AddBinding("<Gamepad>/rightTrigger");

            Brake.AddBinding("<Keyboard>/s");
            Brake.AddBinding("<Keyboard>/downArrow");
            Brake.AddBinding("<Gamepad>/leftTrigger");

            Steer.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/a").With("Positive", "<Keyboard>/d");
            Steer.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/leftArrow").With("Positive", "<Keyboard>/rightArrow");
            Steer.AddBinding("<Gamepad>/leftStick/x");

            Handbrake.AddBinding("<Keyboard>/space");
            Handbrake.AddBinding("<Gamepad>/buttonSouth");

            CameraNext.AddBinding("<Keyboard>/c");
            CameraNext.AddBinding("<Gamepad>/buttonNorth");

            LookBack.AddBinding("<Keyboard>/b");
            LookBack.AddBinding("<Gamepad>/rightStickPress");

            ResetCar.AddBinding("<Keyboard>/r");
            ResetCar.AddBinding("<Gamepad>/select");

            Pause.AddBinding("<Keyboard>/escape");
            Pause.AddBinding("<Keyboard>/p");
            Pause.AddBinding("<Gamepad>/start");

            OrbitDelta.AddBinding("<Mouse>/delta");
            OrbitDelta.AddBinding("<Gamepad>/rightStick").WithProcessor("scaleVector2(x=12,y=12)");
            OrbitHold.AddBinding("<Mouse>/leftButton");

            foreach (var a in All) a.Enable();
        }

        public static InputAction[] All => new[] { Throttle, Brake, Steer, Handbrake, CameraNext, LookBack, ResetCar, Pause, OrbitDelta, OrbitHold };

        public static readonly (string action, string keys, string pad)[] BindingTable =
        {
            ("THROTTLE", "W / UP", "RT"),
            ("BRAKE / REVERSE", "S / DOWN", "LT"),
            ("STEER", "A D / LEFT RIGHT", "LEFT STICK"),
            ("HANDBRAKE", "SPACE", "A"),
            ("CHANGE CAMERA", "C", "Y"),
            ("LOOK BACK", "B", "R3"),
            ("RESET CAR", "R", "VIEW"),
            ("PAUSE", "ESC / P", "MENU"),
        };
    }
}

namespace Apex.Vehicle
{
    /// <summary>Driver commands consumed by CarController. Player and AI produce the same struct.</summary>
    public struct CarInputState
    {
        public float Throttle;   // 0..1
        public float Brake;      // 0..1
        public float Steer;      // -1..1
        public bool Handbrake;

        public static readonly CarInputState None = default;
    }

    public interface ICarInput
    {
        CarInputState ReadInput(CarController car, float dt);
    }
}

using System;
using Apex.Vehicle;

namespace Apex.Core
{
    public enum GameMode { QuickRace, TimeTrial }
    public enum Difficulty { Easy, Medium, Hard }
    public enum Personality { Aggressive, Defensive, Balanced }

    /// <summary>Configuration of the next race; written by Race Setup, read by RaceManager.</summary>
    [Serializable]
    public sealed class RaceConfig
    {
        public GameMode mode = GameMode.QuickRace;
        public Difficulty difficulty = Difficulty.Medium;
        public int opponents = 4;      // 0..4
        public int laps = 3;           // 1..10 (quick race)
        public CarClass carClass = CarClass.GT3;
        public int carIndex;           // index into CarSpec.RosterFor(carClass)
        public string trackId = TrackIds.HarbourGP;

        public static RaceConfig Current = new RaceConfig();

        public RaceConfig Clone() => (RaceConfig)MemberwiseClone();

        /// <summary>The selected car's spec (class roster, clamped index).</summary>
        public CarSpec CarSpec
        {
            get
            {
                var r = Vehicle.CarSpec.RosterFor(carClass);
                return r[UnityEngine.Mathf.Clamp(carIndex, 0, r.Length - 1)];
            }
        }

        /// <summary>Local records key: GT3 keeps the original key, Formula laps are stored separately.</summary>
        public string RecordKey => mode.ToString() + (carClass == CarClass.Formula ? "_F1" : "");
    }

    public static class TrackIds
    {
        public const string HarbourGP = "harbour_gp";
        public const string HarbourGPName = "PORTO VELA STREET CIRCUIT";
    }
}

using System;

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
        public int carIndex;           // index into CarSpec.Roster
        public string trackId = TrackIds.HarbourGP;

        public static RaceConfig Current = new RaceConfig();

        public RaceConfig Clone() => (RaceConfig)MemberwiseClone();
    }

    public static class TrackIds
    {
        public const string HarbourGP = "harbour_gp";
        public const string HarbourGPName = "PORTO VELA STREET CIRCUIT";
    }
}

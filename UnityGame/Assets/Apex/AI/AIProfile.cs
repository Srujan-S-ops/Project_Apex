using Apex.Core;
using UnityEngine;

namespace Apex.AI
{
    public enum TacticalMode { Race, Push, Overtake, Defend, Conserve, Recover }

    /// <summary>High-level instruction for one AI driver (from Java, or from LocalTactics).</summary>
    public struct AICommand
    {
        public TacticalMode Mode;
        public float SpeedScale;      // multiplier on the speed profile (≈0.9..1.05)
        public float LateralTarget;   // preferred lateral offset (m, + right) when overtaking / defending
        public float Aggression;      // 0..1
        public float ReceivedAt;      // realtime of reception

        public static AICommand Default => new AICommand { Mode = TacticalMode.Race, SpeedScale = 1f, LateralTarget = 0f, Aggression = 0.5f };
    }

    /// <summary>Driver behaviour parameters derived from personality and difficulty.</summary>
    public sealed class AIProfile
    {
        public Personality Personality;
        public Difficulty Difficulty;
        public float Pace;              // global speed-profile multiplier
        public float CornerCommitment;  // extra corner-speed multiplier
        public float BrakingMargin;     // seconds of extra braking anticipation
        public float Eagerness;         // willingness to attempt passes (0..1)
        public float DefendSkill;       // willingness to defend (0..1)
        public float FollowGap;         // metres kept behind a car it cannot pass
        public float Consistency;       // amplitude of pace noise
        public float SteerNoise;        // steering wobble

        public static AIProfile Create(Personality p, Difficulty d, int seed)
        {
            var prof = new AIProfile { Personality = p, Difficulty = d };
            switch (d)
            {
                case Difficulty.Easy: prof.Pace = 0.86f; prof.Consistency = 0.035f; prof.SteerNoise = 0.02f; prof.BrakingMargin = 0.18f; break;
                case Difficulty.Medium: prof.Pace = 0.935f; prof.Consistency = 0.018f; prof.SteerNoise = 0.008f; prof.BrakingMargin = 0.1f; break;
                default: prof.Pace = 0.985f; prof.Consistency = 0.008f; prof.SteerNoise = 0.0f; prof.BrakingMargin = 0.04f; break;
            }
            switch (p)
            {
                case Personality.Aggressive:
                    prof.CornerCommitment = 1.02f; prof.Eagerness = 0.95f; prof.DefendSkill = 0.45f; prof.FollowGap = 6f; prof.BrakingMargin *= 0.7f; break;
                case Personality.Defensive:
                    prof.CornerCommitment = 0.985f; prof.Eagerness = 0.45f; prof.DefendSkill = 1f; prof.FollowGap = 11f; break;
                default:
                    prof.CornerCommitment = 1f; prof.Eagerness = 0.7f; prof.DefendSkill = 0.7f; prof.FollowGap = 8f; break;
            }
            // small per-driver variation so cars do not run in lock-step
            var r = new System.Random(seed);
            prof.Pace *= 1f + ((float)r.NextDouble() - 0.5f) * 0.012f;
            return prof;
        }
    }
}

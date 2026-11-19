using System.Collections.Generic;
using Apex.Race;
using Apex.Track;
using UnityEngine;

namespace Apex.AI
{
    /// <summary>
    /// Offline fallback for the Java AIDecisionEngine (same decision rules, simplified):
    /// overtake when closing on a car ahead, defend the inside when attacked, conserve when clear.
    /// </summary>
    public static class LocalTactics
    {
        public static AICommand Decide(RaceParticipant self, IReadOnlyList<RaceParticipant> field, AIProfile p, TrackSpline s)
        {
            RaceParticipant ahead = null, behind = null;
            float gapAhead = float.MaxValue, gapBehind = float.MaxValue;
            foreach (var o in field)
            {
                if (o == self) continue;
                float dd = o.TotalDistance - self.TotalDistance;
                if (dd > 0 && dd < gapAhead) { gapAhead = dd; ahead = o; }
                if (dd < 0 && -dd < gapBehind) { gapBehind = -dd; behind = o; }
            }
            float v = Mathf.Max(10f, self.Car != null ? self.Car.SpeedMs : 10f);
            var cmd = AICommand.Default;
            cmd.Aggression = p.Eagerness;
            cmd.ReceivedAt = Time.realtimeSinceStartup;
            float nextCurv = s.Curvature[s.IndexAt(self.CentreDistance + 60f)];
            float inside = nextCurv > 0f ? 1f : -1f;
            float halfW = s.Width[s.IndexAt(self.CentreDistance)] * 0.5f;

            if (ahead != null && gapAhead / v < 1.0f * (0.6f + p.Eagerness))
            {
                cmd.Mode = TacticalMode.Overtake;
                cmd.SpeedScale = 1.0f + 0.025f * p.Eagerness;
                cmd.LateralTarget = Mathf.Clamp(ahead.Lateral + inside * 3f, -halfW + 1.3f, halfW - 1.3f);
            }
            else if (behind != null && gapBehind / v < 0.7f && p.DefendSkill > 0.5f)
            {
                cmd.Mode = TacticalMode.Defend;
                cmd.SpeedScale = 1.0f;
                cmd.LateralTarget = inside * (halfW - 2f) * 0.6f;
            }
            else if (self.Position == 1 && behind != null && gapBehind / v > 4f)
            {
                cmd.Mode = TacticalMode.Conserve;
                cmd.SpeedScale = 0.975f;
            }
            else if (ahead != null && gapAhead / v < 3f)
            {
                cmd.Mode = TacticalMode.Push;
                cmd.SpeedScale = 1.01f;
            }
            return cmd;
        }
    }
}

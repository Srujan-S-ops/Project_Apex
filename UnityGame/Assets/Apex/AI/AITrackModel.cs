using Apex.Track;
using UnityEngine;

namespace Apex.AI
{
    /// <summary>
    /// Racing line + speed profile consumed by every AI driver. Starts with the local analysis and is
    /// upgraded in place when the Java backend's TrackAnalyzer result arrives (validated first).
    /// </summary>
    public sealed class AITrackModel
    {
        public TrackSpline Spline { get; }
        public float[] LineOffset { get; private set; }
        public float[] Speed { get; private set; }
        public string Source { get; private set; } = "LOCAL";

        public AITrackModel(TrackSpline spline, TrackAnalysis local)
        {
            Spline = spline;
            LineOffset = local.LineOffset;
            Speed = local.Speed;
        }

        public bool TryApplyBackend(float[] offsets, float[] speeds)
        {
            if (!TrackAnalysis.IsValid(offsets, speeds, Spline))
            {
                Debug.LogWarning("[AI] Backend track analysis rejected (failed validation); keeping local model.");
                return false;
            }
            LineOffset = offsets;
            Speed = speeds;
            Source = "JAVA ENGINE";
            Debug.Log("[AI] Using backend racing line and speed profile.");
            return true;
        }

        public float OffsetAt(float distance)
        {
            float f = Spline.WrapDistance(distance) / Spline.Length * Spline.Count;
            int i = (int)f % Spline.Count;
            return Mathf.Lerp(LineOffset[i], LineOffset[(i + 1) % Spline.Count], f - Mathf.Floor(f));
        }

        public float SpeedAt(float distance)
        {
            float f = Spline.WrapDistance(distance) / Spline.Length * Spline.Count;
            int i = (int)f % Spline.Count;
            return Mathf.Lerp(Speed[i], Speed[(i + 1) % Spline.Count], f - Mathf.Floor(f));
        }

        /// <summary>Lowest profile speed within the next `horizon` metres, adjusted for braking distance.</summary>
        public float SpeedLimitAhead(float distance, float horizon, float decel)
        {
            float best = float.MaxValue;
            for (float s = 0f; s <= horizon; s += 4f)
            {
                float v = SpeedAt(distance + s);
                float allowed = Mathf.Sqrt(v * v + 2f * decel * s);
                if (allowed < best) best = allowed;
            }
            return best;
        }
    }
}

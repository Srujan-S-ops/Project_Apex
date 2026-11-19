using System.Collections.Generic;
using UnityEngine;

namespace Apex.Track
{
    /// <summary>Vehicle performance envelope used for corner-speed / braking calculations.</summary>
    public struct VehicleEnvelope
    {
        public float Mu;            // effective tyre friction coefficient
        public float MassKg;
        public float DownforceCoef; // 0.5 * rho * ClA  (N per (m/s)^2)
        public float MaxAccel;      // m/s^2 at low speed
        public float MaxBrake;      // m/s^2 mechanical (before downforce)
        public float TopSpeed;      // m/s

        public static VehicleEnvelope Gt3 => new VehicleEnvelope
        {
            Mu = 1.35f, MassKg = 1300f, DownforceCoef = 0.5f * 1.225f * 3.1f, MaxAccel = 7.5f, MaxBrake = 13.5f, TopSpeed = 78f
        };
    }

    public struct CornerInfo
    {
        public int Start, Apex, End;
        public float MinSpeed;
        public int Direction;     // +1 right, -1 left
    }

    /// <summary>
    /// Racing line + speed profile + corner extraction. The Java backend (TrackAnalyzer) performs the
    /// same analysis as the primary source; this class is the offline fallback and is also used to
    /// validate the backend's response.
    /// </summary>
    public sealed class TrackAnalysis
    {
        public float[] LineOffset;   // lateral offset of the racing line per sample (+ right)
        public float[] Speed;        // target speed per sample (m/s)
        public float[] LineCurvature;
        public List<CornerInfo> Corners = new List<CornerInfo>();
        public List<int> BrakingZones = new List<int>();
        public bool FromBackend;

        public static TrackAnalysis Compute(TrackSpline s, VehicleEnvelope env)
        {
            var a = new TrackAnalysis();
            a.LineOffset = ComputeRacingLine(s, 1.9f, 400);
            a.LineCurvature = ComputeLineCurvature(s, a.LineOffset);
            a.Speed = ComputeSpeedProfile(s.Length / s.Count, a.LineCurvature, env);
            a.ExtractCorners(s);
            return a;
        }

        /// <summary>Iterative Laplacian smoothing of the line inside the track limits (≈ minimum curvature).</summary>
        public static float[] ComputeRacingLine(TrackSpline s, float margin, int iterations)
        {
            int n = s.Count;
            var off = new float[n];
            var pts = new Vector3[n];
            var next = new float[n];
            const int k = 3;
            for (int it = 0; it < iterations; it++)
            {
                for (int i = 0; i < n; i++) pts[i] = s.Pos[i] + s.Right[i] * off[i];
                for (int i = 0; i < n; i++)
                {
                    Vector3 target = (pts[(i - k + n) % n] + pts[(i + k) % n]) * 0.5f;
                    float o = Vector3.Dot(target - s.Pos[i], s.Right[i]);
                    float lim = s.Width[i] * 0.5f - margin;
                    next[i] = Mathf.Clamp(Mathf.Lerp(off[i], o, 0.6f), -lim, lim);
                }
                System.Array.Copy(next, off, n);
            }
            return off;
        }

        public static float[] ComputeLineCurvature(TrackSpline s, float[] offsets)
        {
            int n = s.Count;
            var pts = new Vector3[n];
            for (int i = 0; i < n; i++) pts[i] = s.Pos[i] + s.Right[i] * offsets[i];
            var c = new float[n];
            for (int i = 0; i < n; i++) c[i] = TrackSpline.SignedCurvature(pts, i, 5);
            var tmp = new float[n];
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < n; i++) tmp[i] = (c[(i - 1 + n) % n] + 2f * c[i] + c[(i + 1) % n]) * 0.25f;
                System.Array.Copy(tmp, c, n);
            }
            return c;
        }

        /// <summary>
        /// v_max from lateral grip including downforce (v² κ = μ(g + c v²/m)), then backward (braking)
        /// and forward (acceleration) passes around the closed loop.
        /// </summary>
        public static float[] ComputeSpeedProfile(float ds, float[] curvature, VehicleEnvelope e)
        {
            int n = curvature.Length;
            var v = new float[n];
            const float g = 9.81f;
            float cm = e.DownforceCoef / e.MassKg;
            for (int i = 0; i < n; i++)
            {
                float k = Mathf.Abs(curvature[i]);
                float denom = k - e.Mu * cm;
                v[i] = denom <= 1e-5f ? e.TopSpeed : Mathf.Min(e.TopSpeed, Mathf.Sqrt(e.Mu * g / denom));
            }
            for (int pass = 0; pass < 2; pass++)
            {
                // backward: braking capability grows with downforce
                for (int j = 0; j < n; j++)
                {
                    int i = (n - 1 - j + n) % n;
                    int nx = (i + 1) % n;
                    float vn = v[nx];
                    float brake = Mathf.Min(e.MaxBrake + e.Mu * cm * vn * vn, 32f);
                    float lim = Mathf.Sqrt(vn * vn + 2f * brake * ds);
                    if (v[i] > lim) v[i] = lim;
                }
                // forward: traction/power limited acceleration
                for (int i = 0; i < n; i++)
                {
                    int nx = (i + 1) % n;
                    float vi = v[i];
                    float acc = Mathf.Max(0.4f, e.MaxAccel * (1f - vi / (e.TopSpeed * 1.05f)));
                    float lim = Mathf.Sqrt(vi * vi + 2f * acc * ds);
                    if (v[nx] > lim) v[nx] = lim;
                }
            }
            return v;
        }

        private void ExtractCorners(TrackSpline s)
        {
            Corners.Clear();
            BrakingZones.Clear();
            int n = s.Count;
            const float threshold = 1f / 160f;
            // start scanning from a straight-ish point so corners do not wrap
            int start = 0;
            for (int i = 0; i < n; i++) if (Mathf.Abs(s.Curvature[i]) < threshold * 0.5f) { start = i; break; }
            bool inCorner = false;
            var c = new CornerInfo();
            for (int j = 0; j <= n; j++)
            {
                int i = (start + j) % n;
                float k = Mathf.Abs(s.Curvature[i]);
                if (!inCorner && k > threshold)
                {
                    inCorner = true;
                    c = new CornerInfo { Start = i, Apex = i, MinSpeed = Speed[i], Direction = s.Curvature[i] > 0 ? 1 : -1 };
                }
                else if (inCorner)
                {
                    if (k > Mathf.Abs(s.Curvature[c.Apex])) c.Apex = i;
                    c.MinSpeed = Mathf.Min(c.MinSpeed, Speed[i]);
                    if (k < threshold * 0.7f)
                    {
                        c.End = i;
                        inCorner = false;
                        int len = (c.End - c.Start + n) % n;
                        if (len >= 3) Corners.Add(c);
                    }
                }
            }
            for (int i = 0; i < n; i++)
            {
                if (Speed[(i + 1) % n] < Speed[i] - 0.05f && Speed[(i - 1 + n) % n] <= Speed[i] + 0.05f && Speed[i] > Speed[(i + 30) % n] + 8f)
                    BrakingZones.Add(i);
            }
        }

        /// <summary>Validates arrays received from the backend before they are used.</summary>
        public static bool IsValid(float[] offsets, float[] speeds, TrackSpline s)
        {
            if (offsets == null || speeds == null || offsets.Length != s.Count || speeds.Length != s.Count) return false;
            for (int i = 0; i < s.Count; i++)
            {
                if (float.IsNaN(offsets[i]) || float.IsNaN(speeds[i])) return false;
                if (Mathf.Abs(offsets[i]) > s.Width[i] * 0.5f + 0.5f) return false;
                if (speeds[i] < 3f || speeds[i] > 120f) return false;
            }
            return true;
        }
    }
}

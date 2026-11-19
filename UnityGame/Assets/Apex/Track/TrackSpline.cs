using System.Collections.Generic;
using UnityEngine;

namespace Apex.Track
{
    /// <summary>Result of projecting a world position onto the centre line.</summary>
    public struct TrackProjection
    {
        public int Index;          // sample index
        public float Distance;     // arc length along the centre line [0, Length)
        public float Lateral;      // signed offset (+ right)
        public float Height;       // world y minus road height
        public float DistanceToCentre;
    }

    /// <summary>
    /// Closed centripetal Catmull-Rom centre line resampled at uniform arc length.
    /// </summary>
    public sealed class TrackSpline
    {
        public const float Spacing = 2f;

        public Vector3[] Pos { get; private set; }
        public Vector3[] Fwd { get; private set; }
        public Vector3[] Right { get; private set; }
        public float[] Width { get; private set; }
        public float[] Curvature { get; private set; }   // signed 1/m, + = right-hand turn
        public float Length { get; private set; }
        public int Count => Pos.Length;

        public static TrackSpline FromLayout(Vector4[] control, float targetLength)
        {
            // dense raw evaluation
            var raw = new List<Vector3>();
            var rawW = new List<float>();
            int n = control.Length;
            const int sub = 60;
            for (int i = 0; i < n; i++)
            {
                Vector4 p0 = control[(i - 1 + n) % n], p1 = control[i], p2 = control[(i + 1) % n], p3 = control[(i + 2) % n];
                for (int k = 0; k < sub; k++)
                {
                    float t = k / (float)sub;
                    Vector3 xz = CatmullRom(V(p0), V(p1), V(p2), V(p3), t);
                    float y = Smooth(p1.z, p2.z, t, p0.z, p3.z);
                    raw.Add(new Vector3(xz.x, y, xz.z));
                    rawW.Add(Mathf.Lerp(p1.w, p2.w, t * t * (3f - 2f * t)));
                }
            }
            // measure & scale horizontally
            float len = 0f;
            for (int i = 0; i < raw.Count; i++) len += Flat(raw[(i + 1) % raw.Count] - raw[i]).magnitude;
            float scale = targetLength / len;
            for (int i = 0; i < raw.Count; i++) raw[i] = new Vector3(raw[i].x * scale, raw[i].y, raw[i].z * scale);

            var s = new TrackSpline();
            s.Resample(raw, rawW);
            return s;
        }

        private static Vector3 V(Vector4 p) => new Vector3(p.x, 0f, p.y);
        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        private static float Smooth(float a, float b, float t, float prev, float next)
        {
            // Catmull-Rom on scalar elevation
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * ((2f * a) + (-prev + b) * t + (2f * prev - 5f * a + 4f * b - next) * t2 + (-prev + 3f * a - 3f * b + next) * t3);
        }

        private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            // centripetal parameterisation (alpha = 0.5) avoids cusps with uneven spacing
            float t0 = 0f;
            float t1 = t0 + Mathf.Pow(Vector3.Distance(p0, p1), 0.5f);
            float t2 = t1 + Mathf.Pow(Vector3.Distance(p1, p2), 0.5f);
            float t3 = t2 + Mathf.Pow(Vector3.Distance(p2, p3), 0.5f);
            float tt = Mathf.Lerp(t1, t2, t);
            Vector3 a1 = (t1 - tt) / (t1 - t0) * p0 + (tt - t0) / (t1 - t0) * p1;
            Vector3 a2 = (t2 - tt) / (t2 - t1) * p1 + (tt - t1) / (t2 - t1) * p2;
            Vector3 a3 = (t3 - tt) / (t3 - t2) * p2 + (tt - t2) / (t3 - t2) * p3;
            Vector3 b1 = (t2 - tt) / (t2 - t0) * a1 + (tt - t0) / (t2 - t0) * a2;
            Vector3 b2 = (t3 - tt) / (t3 - t1) * a2 + (tt - t1) / (t3 - t1) * a3;
            return (t2 - tt) / (t2 - t1) * b1 + (tt - t1) / (t2 - t1) * b2;
        }

        private void Resample(List<Vector3> raw, List<float> rawW)
        {
            int rn = raw.Count;
            var cum = new float[rn + 1];
            for (int i = 0; i < rn; i++) cum[i + 1] = cum[i] + Vector3.Distance(raw[i], raw[(i + 1) % rn]);
            float total = cum[rn];
            int count = Mathf.RoundToInt(total / Spacing);
            float step = total / count;
            Pos = new Vector3[count];
            Width = new float[count];
            int j = 0;
            for (int i = 0; i < count; i++)
            {
                float d = i * step;
                while (j < rn - 1 && cum[j + 1] < d) j++;
                float t = (d - cum[j]) / Mathf.Max(1e-5f, cum[j + 1] - cum[j]);
                Pos[i] = Vector3.Lerp(raw[j], raw[(j + 1) % rn], t);
                Width[i] = Mathf.Lerp(rawW[j], rawW[(j + 1) % rn], t);
            }
            Length = total;
            Fwd = new Vector3[count];
            Right = new Vector3[count];
            Curvature = new float[count];
            for (int i = 0; i < count; i++)
            {
                Vector3 f = Pos[(i + 1) % count] - Pos[(i - 1 + count) % count];
                Fwd[i] = f.normalized;
                Vector3 flat = Flat(f).normalized;
                Right[i] = new Vector3(flat.z, 0f, -flat.x);
            }
            for (int i = 0; i < count; i++) Curvature[i] = SignedCurvature(Pos, i, 4);
            // smooth curvature
            var tmp = new float[count];
            for (int pass = 0; pass < 3; pass++)
            {
                for (int i = 0; i < count; i++)
                    tmp[i] = (Curvature[(i - 1 + count) % count] + Curvature[i] * 2f + Curvature[(i + 1) % count]) * 0.25f;
                System.Array.Copy(tmp, Curvature, count);
            }
        }

        /// <summary>Signed horizontal curvature from three points i-k, i, i+k (+ = turning right).</summary>
        public static float SignedCurvature(Vector3[] pts, int i, int k)
        {
            int n = pts.Length;
            Vector3 a = Flat(pts[(i - k + n) % n]), b = Flat(pts[i]), c = Flat(pts[(i + k) % n]);
            float ab = Vector3.Distance(a, b), bc = Vector3.Distance(b, c), ca = Vector3.Distance(c, a);
            float cross = (b.x - a.x) * (c.z - a.z) - (b.z - a.z) * (c.x - a.x);
            float denom = ab * bc * ca;
            if (denom < 1e-6f) return 0f;
            // cross > 0 → counter-clockwise (left turn) in x/z with z up the page
            return -2f * cross / denom;
        }

        public int Wrap(int i) => ((i % Count) + Count) % Count;
        public float WrapDistance(float d) => ((d % Length) + Length) % Length;

        public void Sample(float distance, out Vector3 pos, out Vector3 fwd, out Vector3 right, out float width)
        {
            float d = WrapDistance(distance);
            float f = d / Length * Count;
            int i = Mathf.FloorToInt(f) % Count;
            int j = (i + 1) % Count;
            float t = f - Mathf.Floor(f);
            pos = Vector3.Lerp(Pos[i], Pos[j], t);
            fwd = Vector3.Slerp(Fwd[i], Fwd[j], t).normalized;
            right = Vector3.Slerp(Right[i], Right[j], t).normalized;
            width = Mathf.Lerp(Width[i], Width[j], t);
        }

        public Vector3 PointAt(float distance, float lateral)
        {
            Sample(distance, out var p, out _, out var r, out _);
            return p + r * lateral;
        }

        public float DistanceOf(int index) => index * Length / Count;

        public int IndexAt(float distance) => Wrap(Mathf.FloorToInt(WrapDistance(distance) / Length * Count));

        /// <summary>Projects a position on the centre line. hint = previous index (-1 for global search).</summary>
        public TrackProjection Project(Vector3 p, int hint)
        {
            int best = -1;
            float bestD = float.MaxValue;
            if (hint >= 0)
            {
                for (int o = -30; o <= 30; o++)
                {
                    int i = Wrap(hint + o);
                    float d = (Pos[i] - p).sqrMagnitude;
                    if (d < bestD) { bestD = d; best = i; }
                }
                if (bestD > 40f * 40f) best = -1;
            }
            if (best < 0)
            {
                bestD = float.MaxValue;
                for (int i = 0; i < Count; i++)
                {
                    float d = (Pos[i] - p).sqrMagnitude;
                    if (d < bestD) { bestD = d; best = i; }
                }
            }
            // refine onto the segment best..next or prev..best
            int nxt = Wrap(best + 1), prv = Wrap(best - 1);
            Vector3 seg = Pos[nxt] - Pos[best];
            float t = Vector3.Dot(p - Pos[best], seg) / seg.sqrMagnitude;
            float baseDist = DistanceOf(best);
            if (t < 0f)
            {
                seg = Pos[best] - Pos[prv];
                t = Vector3.Dot(p - Pos[prv], seg) / seg.sqrMagnitude;
                baseDist = DistanceOf(prv);
                t = Mathf.Clamp01(t);
                best = prv;
            }
            else t = Mathf.Clamp01(t);
            float step = Length / Count;
            Vector3 onLine = Pos[best] + (Pos[Wrap(best + 1)] - Pos[best]) * t;
            Vector3 delta = p - onLine;
            return new TrackProjection
            {
                Index = best,
                Distance = WrapDistance(baseDist + t * step),
                Lateral = Vector3.Dot(delta, Right[best]),
                Height = delta.y,
                DistanceToCentre = new Vector3(delta.x, 0f, delta.z).magnitude
            };
        }

        /// <summary>Signed forward distance from a to b along the track, in (-Length/2, Length/2].</summary>
        public float Delta(float a, float b)
        {
            float d = b - a;
            if (d > Length * 0.5f) d -= Length;
            if (d <= -Length * 0.5f) d += Length;
            return d;
        }
    }
}

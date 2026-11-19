using System.Collections.Generic;
using Apex.Rendering;
using UnityEngine;

namespace Apex.Track
{
    /// <summary>
    /// Generates the drivable circuit: road (with camber and colliders), kerbs, run-off, concrete
    /// barriers with advertising, catch fencing, markings, start gantry and lights, grid, terrain.
    /// Environment dressing is delegated to EnvironmentBuilder.
    /// </summary>
    public sealed class TrackBuilder
    {
        private const int Chunk = 90;
        private const float KerbWidth = 1.25f;
        private const float WallHeight = 1.05f;

        private readonly TrackSpline _s;
        private readonly TrackAnalysis _a;
        private readonly Transform _root;
        private readonly int _quality;
        private TrackWorld _world;
        private float[] _kerbL, _kerbR;   // 0/1 per sample
        private PhysicsMaterial _roadPm, _kerbPm, _wallPm, _grassPm;

        public TrackBuilder(TrackSpline spline, TrackAnalysis analysis, Transform root, int quality)
        {
            _s = spline; _a = analysis; _root = root; _quality = quality;
        }

        public TrackWorld World => _world;

        /// <summary>Builds in steps (coroutine friendly). Each call to the enumerator performs one stage.</summary>
        public IEnumerable<float> Build()
        {
            _world = _root.gameObject.AddComponent<TrackWorld>();
            _world.Spline = _s;
            _world.Analysis = _a;
            _world.StartLine = TrackLayout.StartLineDistance;
            float l = _s.Length;
            _world.SectorStarts = new[] { 0f, l / 3f, 2f * l / 3f };
            var cps = new List<float>();
            for (float d = 100f; d < l - 60f; d += 100f) cps.Add(d);
            _world.Checkpoints = cps.ToArray();

            _roadPm = new PhysicsMaterial("Road") { dynamicFriction = 1f, staticFriction = 1f };
            _kerbPm = new PhysicsMaterial("Kerb") { dynamicFriction = 0.9f, staticFriction = 0.9f };
            _wallPm = new PhysicsMaterial("Wall") { dynamicFriction = 0.08f, staticFriction = 0.1f, frictionCombine = PhysicsMaterialCombine.Minimum, bounciness = 0.05f };
            _grassPm = new PhysicsMaterial("Grass") { dynamicFriction = 0.6f, staticFriction = 0.6f };

            ComputeKerbsAndWalls();
            yield return 0.1f;
            BuildRoad();
            yield return 0.25f;
            BuildKerbs();
            BuildRunoff();
            yield return 0.35f;
            BuildWalls();
            yield return 0.45f;
            if (_quality >= 1) BuildFence();
            BuildMarkings();
            BuildStartGantry();
            BuildGrid();
            yield return 0.55f;
            BuildTerrain();
            yield return 0.7f;
            ComputeBounds();
        }

        // ------------------------------------------------------------------ layout data

        private void ComputeKerbsAndWalls()
        {
            int n = _s.Count;
            _kerbL = new float[n];
            _kerbR = new float[n];
            for (int i = 0; i < n; i++)
            {
                float k = _s.Curvature[i];
                if (Mathf.Abs(k) > 1f / 115f)
                {
                    // inside kerb plus a little lead-in / exit
                    for (int o = -8; o <= 8; o++)
                    {
                        int j = _s.Wrap(i + o);
                        if (k > 0) _kerbR[j] = 1f; else _kerbL[j] = 1f;
                    }
                    // exit kerb on the outside
                    for (int o = 6; o <= 22; o++)
                    {
                        int j = _s.Wrap(i + o);
                        if (Mathf.Abs(_s.Curvature[j]) < 1f / 115f)
                        {
                            if (k > 0) _kerbL[j] = 1f; else _kerbR[j] = 1f;
                        }
                    }
                }
            }
            var left = new float[n];
            var right = new float[n];
            for (int i = 0; i < n; i++)
            {
                float half = _s.Width[i] * 0.5f;
                left[i] = half + 2.2f + _kerbL[i] * KerbWidth;
                right[i] = half + 2.2f + _kerbR[i] * KerbWidth;
            }
            // escape roads on the outside of heavy-braking corners
            foreach (var c in _a.Corners)
            {
                if (c.MinSpeed > 34f) continue;
                float extra = Mathf.Lerp(12f, 6f, Mathf.InverseLerp(14f, 34f, c.MinSpeed));
                int len = (c.End - c.Start + n) % n;
                for (int o = -25; o <= len + 10; o++)
                {
                    int j = _s.Wrap(c.Start + o);
                    float w = o < 0 ? (o + 25) / 25f : (o > len ? 1f - (o - len) / 10f : 1f);
                    if (c.Direction > 0) left[j] = Mathf.Max(left[j], _s.Width[j] * 0.5f + 2.2f + extra * w);
                    else right[j] = Mathf.Max(right[j], _s.Width[j] * 0.5f + 2.2f + extra * w);
                }
            }
            // pit straight: wider pit-wall side
            for (int i = 0; i < n; i++)
            {
                float d = _s.DistanceOf(i);
                if (d > 10f && d < 560f) left[i] = Mathf.Max(left[i], _s.Width[i] * 0.5f + 3.5f);
            }
            _world.LeftWall = SmoothArray(left, 12);
            _world.RightWall = SmoothArray(right, 12);
        }

        private static float[] SmoothArray(float[] src, int radius)
        {
            int n = src.Length;
            var dst = new float[n];
            for (int i = 0; i < n; i++)
            {
                float m = 0f, sum = 0f;
                for (int o = -radius; o <= radius; o++)
                {
                    float w = 1f - Mathf.Abs(o) / (float)(radius + 1);
                    sum += src[((i + o) % n + n) % n] * w;
                    m += w;
                }
                dst[i] = Mathf.Max(src[i] * 0.85f, sum / m);
            }
            return dst;
        }

        private Vector3 P(int i, float lateral, float height = 0f)
        {
            int j = _s.Wrap(i);
            return _s.Pos[j] + _s.Right[j] * lateral + Vector3.up * height;
        }

        private float V(int i) => i * _s.Length / _s.Count;

        /// <summary>Builds an extruded strip over a sample range. profile(i) returns lateral/height pairs right→left order not required.</summary>
        private void Strip(MeshBuilder mb, int from, int to, System.Func<int, Vector2[]> profile, float vScale, Vector3 desiredNormalAtFirst, bool closed = false)
        {
            var rings = new List<Vector3[]>();
            var vs = new List<float>();
            for (int i = from; i <= to; i++)
            {
                var prof = profile(i);
                var ring = new Vector3[prof.Length];
                for (int k = 0; k < prof.Length; k++) ring[k] = P(i, prof[k].x, prof[k].y);
                rings.Add(ring);
                vs.Add(V(i) * vScale);
            }
            // decide orientation from the first quad
            Vector3 around = rings[0][Mathf.Min(1, rings[0].Length - 1)] - rings[0][0];
            Vector3 along = rings[1][0] - rings[0][0];
            bool flip = Vector3.Dot(Vector3.Cross(around, along), desiredNormalAtFirst) < 0f;
            mb.AddLoft(rings, closed, 1f, vs, flip);
        }

        private void AddCollider(GameObject go, PhysicsMaterial pm)
        {
            var mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
            mc.sharedMaterial = pm;
        }

        // ------------------------------------------------------------------ road

        private void BuildRoad()
        {
            var asphalt = MaterialLibrary.LitNormal(new Color(0.78f, 0.78f, 0.8f), 0f, 0.42f, MaterialLibrary.Tex("asphalt"), MaterialLibrary.Tex("asphalt_n"), 0.6f);
            var parent = new GameObject("Road").transform;
            parent.SetParent(_root, false);
            int n = _s.Count;
            for (int c = 0; c < n; c += Chunk)
            {
                int to = Mathf.Min(c + Chunk, n);
                var mb = new MeshBuilder();
                Strip(mb, c, to, i =>
                {
                    float h = _s.Width[_s.Wrap(i)] * 0.5f;
                    return new[]
                    {
                        new Vector2(h, 0f), new Vector2(h * 0.5f, 0.035f), new Vector2(0f, 0.05f), new Vector2(-h * 0.5f, 0.035f), new Vector2(-h, 0f)
                    };
                }, 1f / 14f, Vector3.up);
                // u across the road maps to the full texture width
                var go = mb.Build("RoadChunk" + c, parent, asphalt);
                go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                AddCollider(go, _roadPm);
            }
        }

        private void BuildKerbs()
        {
            var kerbMat = MaterialLibrary.Lit(Color.white, 0f, 0.55f, MaterialLibrary.Tex("kerb"));
            var mb = new MeshBuilder();
            int n = _s.Count;
            for (int side = -1; side <= 1; side += 2)
            {
                float[] mask = side > 0 ? _kerbR : _kerbL;
                int i = 0;
                while (i < n)
                {
                    if (mask[i] < 0.5f) { i++; continue; }
                    int start = i;
                    while (i < n && mask[i] > 0.5f) i++;
                    int end = Mathf.Min(i, n - 1);
                    if (end - start < 2) continue;
                    int sd = side;
                    Strip(mb, start, end, j =>
                    {
                        float h = _s.Width[_s.Wrap(j)] * 0.5f;
                        float s = sd;
                        return new[]
                        {
                            new Vector2(s * (h - 0.25f), 0.0f), new Vector2(s * (h + 0.2f), 0.06f),
                            new Vector2(s * (h + KerbWidth - 0.15f), 0.075f), new Vector2(s * (h + KerbWidth), 0.0f)
                        };
                    }, 1f / 4f, Vector3.up);
                }
            }
            var go = mb.Build("Kerbs", _root, kerbMat, false);
            AddCollider(go, _kerbPm);
        }

        private void BuildRunoff()
        {
            var mat = MaterialLibrary.LitNormal(new Color(0.55f, 0.55f, 0.57f), 0f, 0.3f, MaterialLibrary.Tex("asphalt"), MaterialLibrary.Tex("asphalt_n"), 0.4f, new Vector2(2f, 1f));
            var parent = new GameObject("Runoff").transform;
            parent.SetParent(_root, false);
            int n = _s.Count;
            for (int c = 0; c < n; c += Chunk)
            {
                int to = Mathf.Min(c + Chunk, n);
                for (int side = -1; side <= 1; side += 2)
                {
                    var mb = new MeshBuilder();
                    int sd = side;
                    Strip(mb, c, to, i =>
                    {
                        int j = _s.Wrap(i);
                        float h = _s.Width[j] * 0.5f;
                        float wall = sd > 0 ? _world.RightWall[j] : _world.LeftWall[j];
                        return new[] { new Vector2(sd * (h - 0.05f), -0.01f), new Vector2(sd * (wall + 0.2f), -0.02f) };
                    }, 1f / 10f, Vector3.up);
                    var go = mb.Build("Runoff" + c + (side > 0 ? "R" : "L"), parent, mat, false);
                    AddCollider(go, _roadPm);
                }
            }
        }

        private void BuildWalls()
        {
            var concrete = MaterialLibrary.LitNormal(new Color(0.85f, 0.85f, 0.86f), 0f, 0.25f, MaterialLibrary.Tex("concrete"), MaterialLibrary.Tex("concrete_n"), 0.5f);
            var ads = MaterialLibrary.Lit(Color.white, 0f, 0.5f, MaterialLibrary.Tex("adboard"));
            var parent = new GameObject("Barriers").transform;
            parent.SetParent(_root, false);
            int n = _s.Count;
            for (int c = 0; c < n; c += Chunk)
            {
                int to = Mathf.Min(c + Chunk, n);
                for (int side = -1; side <= 1; side += 2)
                {
                    int sd = side;
                    float[] off = side > 0 ? _world.RightWall : _world.LeftWall;
                    var mb = new MeshBuilder();
                    Strip(mb, c, to, i =>
                    {
                        float w = off[_s.Wrap(i)];
                        return new[]
                        {
                            new Vector2(sd * w, -0.35f), new Vector2(sd * (w + 0.06f), WallHeight),
                            new Vector2(sd * (w + 0.36f), WallHeight), new Vector2(sd * (w + 0.62f), -0.35f)
                        };
                    }, 1f / 4f, -_s.Right[_s.Wrap(c)] * sd);
                    var go = mb.Build("Wall" + c + (side > 0 ? "R" : "L"), parent, concrete);
                    AddCollider(go, _wallPm);

                    // advertising strip on the track-facing side
                    var ab = new MeshBuilder();
                    Strip(ab, c, to, i =>
                    {
                        float w = off[_s.Wrap(i)] - 0.012f;
                        return new[] { new Vector2(sd * (w + 0.01f), 0.12f), new Vector2(sd * (w + 0.05f), 0.95f) };
                    }, 1f / 13.5f, -_s.Right[_s.Wrap(c)] * sd);
                    // texture u runs along the track (mirrored on the right so logos read correctly)
                    for (int k = 0; k < ab.Uvs.Count; k++) ab.Uvs[k] = new Vector2(ab.Uvs[k].y * -sd, ab.Uvs[k].x);
                    ab.Build("Ads" + c + (side > 0 ? "R" : "L"), parent, ads, false);
                }
            }
        }

        private void BuildFence()
        {
            var fenceMat = MaterialLibrary.Cutout(MaterialLibrary.Tex("fence"), new Color(0.75f, 0.77f, 0.8f), 0.4f, null, 0.5f);
            var poleMat = MaterialLibrary.Lit(new Color(0.35f, 0.36f, 0.38f), 0.7f, 0.5f);
            var fence = new MeshBuilder();
            var poles = new MeshBuilder();
            int n = _s.Count;
            int stepPole = 3;
            for (int side = -1; side <= 1; side += 2)
            {
                float[] off = side > 0 ? _world.RightWall : _world.LeftWall;
                int sd = side;
                for (int c = 0; c < n; c += Chunk)
                {
                    int to = Mathf.Min(c + Chunk, n);
                    Strip(fence, c, to, i =>
                    {
                        float w = off[_s.Wrap(i)] + 0.3f;
                        return new[] { new Vector2(sd * w, WallHeight), new Vector2(sd * (w - 0.6f), WallHeight + 3.4f) };
                    }, 1f / 2f, -_s.Right[_s.Wrap(c)] * sd);
                }
                for (int i = 0; i < n; i += stepPole)
                {
                    float w = off[i] + 0.3f;
                    Vector3 a = P(i, sd * w, WallHeight - 0.05f);
                    Vector3 b = P(i, sd * (w - 0.65f), WallHeight + 3.5f);
                    poles.AddBox((a + b) * 0.5f, new Vector3(0.08f, (b - a).magnitude, 0.08f), Quaternion.FromToRotation(Vector3.up, (b - a).normalized));
                }
            }
            // UV of the fence: stretch v over length, u over height — texture tiles both ways
            for (int i = 0; i < fence.Uvs.Count; i++) fence.Uvs[i] = new Vector2(fence.Uvs[i].y, fence.Uvs[i].x * 3.4f);
            fence.Build("CatchFence", _root, fenceMat, false);
            poles.Build("FencePoles", _root, poleMat, false);
        }

        private void BuildMarkings()
        {
            var white = MaterialLibrary.Lit(new Color(0.92f, 0.92f, 0.9f), 0f, 0.35f);
            var mb = new MeshBuilder();
            int n = _s.Count;
            // edge lines
            for (int c = 0; c < n; c += Chunk)
            {
                int to = Mathf.Min(c + Chunk, n);
                for (int side = -1; side <= 1; side += 2)
                {
                    int sd = side;
                    Strip(mb, c, to, i =>
                    {
                        float h = _s.Width[_s.Wrap(i)] * 0.5f - 0.3f;
                        return new[] { new Vector2(sd * (h + 0.22f), 0.012f), new Vector2(sd * h, 0.018f) };
                    }, 0.1f, Vector3.up);
                }
            }
            mb.Build("EdgeLines", _root, white, false);

            // start/finish chequered line
            var chequer = MaterialLibrary.Lit(Color.white, 0f, 0.4f, MaterialLibrary.Tex("chequer"));
            var sl = new MeshBuilder();
            int si = _s.IndexAt(_world.StartLine);
            _s.Sample(_world.StartLine, out var p, out var f, out var r, out var w);
            float half = w * 0.5f;
            Vector3 up = Vector3.up * 0.06f;
            sl.AddQuad(p - r * half - f * 0.9f + up, p - r * half + f * 0.9f + up, p + r * half + f * 0.9f + up, p + r * half - f * 0.9f + up,
                new Vector2(0, 0), new Vector2(0, 1), new Vector2(w / 1.8f, 1), new Vector2(w / 1.8f, 0));
            FixUp(sl);
            sl.Build("StartLine", _root, chequer, false);
        }

        private static void FixUp(MeshBuilder mb)
        {
            for (int q = 0; q < mb.Normals.Count; q += 4)
            {
                if (mb.Normals[q].y >= 0f) continue;
                for (int i = q; i < q + 4; i++) mb.Normals[i] = -mb.Normals[i];
                int ti = q / 4 * 6;
                for (int i = ti; i < ti + 6; i += 3)
                {
                    int t = mb.Triangles[i + 1]; mb.Triangles[i + 1] = mb.Triangles[i + 2]; mb.Triangles[i + 2] = t;
                }
            }
        }

        private void BuildGrid()
        {
            var white = MaterialLibrary.Lit(new Color(0.95f, 0.95f, 0.93f), 0f, 0.35f);
            var mb = new MeshBuilder();
            for (int k = 0; k < 8; k++)
            {
                float d = _world.StartLine - 10f - k * 9f;
                float lat = (k % 2 == 0) ? 3.2f : -3.2f;     // pole on the right (racing line into T1)
                _s.Sample(d, out var p, out var f, out var r, out _);
                Vector3 pos = p + r * lat + Vector3.up * 0.25f;
                _world.GridSlots.Add(new Pose(pos, Quaternion.LookRotation(f, Vector3.up)));
                // painted grid box: front bar + two side ticks
                Vector3 c = p + r * lat + Vector3.up * 0.065f + f * 2.6f;
                AddFlatQuad(mb, c, f, r, 2.6f, 0.18f);
                AddFlatQuad(mb, c - r * 1.2f - f * 0.6f, f, r, 0.15f, 1.2f);
                AddFlatQuad(mb, c + r * 1.2f - f * 0.6f, f, r, 0.15f, 1.2f);
            }
            FixUp(mb);
            mb.Build("GridMarks", _root, white, false);
            // time trial: rolling start on the run to the final corner
            _s.Sample(_world.StartLine - 330f, out var tp, out var tf, out var tr, out _);
            _world.TimeTrialStart = new Pose(tp + Vector3.up * 0.3f, Quaternion.LookRotation(tf, Vector3.up));
        }

        private static void AddFlatQuad(MeshBuilder mb, Vector3 c, Vector3 f, Vector3 r, float width, float length)
        {
            Vector3 hr = r * width * 0.5f, hf = f * length * 0.5f;
            mb.AddQuad(c - hr - hf, c - hr + hf, c + hr + hf, c + hr - hf);
        }

        private void BuildStartGantry()
        {
            _s.Sample(_world.StartLine + 6f, out var p, out var f, out var r, out _);
            int idx = _s.IndexAt(_world.StartLine + 6f);
            float lw = _world.LeftWall[idx] + 0.4f, rw = _world.RightWall[idx] + 0.4f;
            var steel = MaterialLibrary.Lit(new Color(0.12f, 0.12f, 0.13f), 0.6f, 0.5f);
            var g = new MeshBuilder();
            Quaternion rot = Quaternion.LookRotation(f, Vector3.up);
            Vector3 left = p - r * lw, right = p + r * rw;
            g.AddBox(left + Vector3.up * 4f, new Vector3(0.6f, 8f, 0.6f), rot);
            g.AddBox(right + Vector3.up * 4f, new Vector3(0.6f, 8f, 0.6f), rot);
            g.AddBox((left + right) * 0.5f + Vector3.up * 7.6f, new Vector3(lw + rw + 0.6f, 1.6f, 0.7f), rot);
            g.AddBox(p + Vector3.up * 6.1f - f * 0.2f, new Vector3(4.2f, 1.2f, 0.5f), rot);   // light pod
            g.Build("StartGantry", _root, steel);

            var banner = MaterialLibrary.Emissive(Color.white, Color.white, 0.6f, MaterialLibrary.Tex("banner_start"));
            var bb = new MeshBuilder();
            Vector3 bc = (left + right) * 0.5f + Vector3.up * 7.6f - f * 0.37f;
            float bw = (lw + rw) * 0.5f;
            bb.AddQuad(bc - r * bw - Vector3.up * 0.7f, bc - r * bw + Vector3.up * 0.7f, bc + r * bw + Vector3.up * 0.7f, bc + r * bw - Vector3.up * 0.7f,
                new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0));
            FixFacing(bb, -f);
            bb.Build("GantryBanner", _root, banner, false);

            for (int i = 0; i < 5; i++)
            {
                var m = MaterialLibrary.Emissive(new Color(0.08f, 0.02f, 0.02f), Color.red, 0f);
                _world.StartLights[i] = m;
                var lb = new MeshBuilder();
                Vector3 lc = p + Vector3.up * 6.1f - f * 0.5f + r * ((i - 2) * 0.8f);
                lb.AddSphere(lc, new Vector3(0.22f, 0.22f, 0.12f), 14);
                lb.Build("StartLight" + i, _root, m, false);
                var lgo = new GameObject("StartLamp" + i);
                lgo.transform.SetParent(_root, false);
                lgo.transform.position = lc - f * 0.6f;
                var lamp = lgo.AddComponent<Light>();
                lamp.type = LightType.Point;
                lamp.range = 9f;
                lamp.intensity = 3f;
                lamp.color = Color.red;
                lamp.enabled = false;
                lamp.shadows = LightShadows.None;
                _world.StartLightLamps[i] = lamp;
            }
        }

        private static void FixFacing(MeshBuilder mb, Vector3 desired)
        {
            for (int q = 0; q < mb.Normals.Count; q += 4)
            {
                if (Vector3.Dot(mb.Normals[q], desired) >= 0f) continue;
                for (int i = q; i < q + 4; i++) mb.Normals[i] = -mb.Normals[i];
                int ti = q / 4 * 6;
                for (int i = ti; i < ti + 6; i += 3)
                {
                    int t = mb.Triangles[i + 1]; mb.Triangles[i + 1] = mb.Triangles[i + 2]; mb.Triangles[i + 2] = t;
                }
            }
        }

        // ------------------------------------------------------------------ terrain

        public const float HarbourZ = -585f;

        public static bool InHarbour(float x, float z) => z < HarbourZ && x > -470f && x < 460f;

        private void BuildTerrain()
        {
            Bounds b = SplineBounds();
            b.Expand(new Vector3(700f, 0f, 700f));
            const float step = 8f;
            int nx = Mathf.CeilToInt(b.size.x / step), nz = Mathf.CeilToInt(b.size.z / step);
            var heights = new float[(nx + 1) * (nz + 1)];
            var hash = new SampleGrid(_s, 24f);
            // coarse samples for IDW
            var coarse = new List<Vector3>();
            for (int i = 0; i < _s.Count; i += 8) coarse.Add(_s.Pos[i]);

            for (int z = 0; z <= nz; z++)
            {
                for (int x = 0; x <= nx; x++)
                {
                    float wx = b.min.x + x * step, wz = b.min.z + z * step;
                    float h;
                    int near = hash.Nearest(new Vector3(wx, 0f, wz), 60f, out float dist);
                    if (near >= 0 && dist < Mathf.Max(_world.LeftWall[near], _world.RightWall[near]) + 14f)
                    {
                        h = _s.Pos[near].y - 0.3f;
                    }
                    else
                    {
                        float sw = 0f, sh = 0f;
                        foreach (var c in coarse)
                        {
                            float dx = c.x - wx, dz = c.z - wz;
                            float d2 = dx * dx + dz * dz + 400f;
                            float w = 1f / (d2 * d2);
                            sw += w; sh += c.y * w;
                        }
                        h = sh / sw - 0.3f;
                    }
                    if (InHarbour(wx, wz)) h = Mathf.Min(h, -13f);
                    heights[z * (nx + 1) + x] = h;
                }
            }

            var ground = MaterialLibrary.LitNormal(new Color(0.8f, 0.8f, 0.8f), 0f, 0.2f, MaterialLibrary.Tex("paving"), MaterialLibrary.Tex("paving_n"), 0.5f);
            var parent = new GameObject("Terrain").transform;
            parent.SetParent(_root, false);
            const int chunkCells = 50;
            for (int cz = 0; cz < nz; cz += chunkCells)
            {
                for (int cx = 0; cx < nx; cx += chunkCells)
                {
                    var mb = new MeshBuilder();
                    int x1 = Mathf.Min(cx + chunkCells, nx), z1 = Mathf.Min(cz + chunkCells, nz);
                    int w = x1 - cx + 1;
                    for (int z = cz; z <= z1; z++)
                        for (int x = cx; x <= x1; x++)
                        {
                            float wx = b.min.x + x * step, wz = b.min.z + z * step;
                            mb.AddVertex(new Vector3(wx, heights[z * (nx + 1) + x], wz), Vector3.up, new Vector2(wx / 6f, wz / 6f));
                        }
                    for (int z = 0; z < z1 - cz; z++)
                        for (int x = 0; x < x1 - cx; x++)
                        {
                            int i0 = z * w + x;
                            mb.AddTriangle(i0, i0 + w, i0 + w + 1);
                            mb.AddTriangle(i0, i0 + w + 1, i0 + 1);
                        }
                    var mesh = mb.ToMesh("Terrain");
                    mesh.RecalculateNormals();
                    mesh.RecalculateTangents();
                    var go = new GameObject("TerrainChunk");
                    go.transform.SetParent(parent, false);
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var mr = go.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = ground;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    var mc = go.AddComponent<MeshCollider>();
                    mc.sharedMesh = mesh;
                    mc.sharedMaterial = _grassPm;
                }
            }

            // harbour water
            var water = MaterialLibrary.Lit(new Color(0.05f, 0.16f, 0.22f), 0.1f, 0.96f, MaterialLibrary.Tex("water"), new Vector2(40f, 20f));
            var wb = new MeshBuilder();
            float wy = -9.6f;
            wb.AddQuad(new Vector3(-900f, wy, -1600f), new Vector3(-900f, wy, HarbourZ + 4f), new Vector3(900f, wy, HarbourZ + 4f), new Vector3(900f, wy, -1600f),
                new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0));
            FixUp(wb);
            var wgo = wb.Build("Harbour", _root, water, false);
            wgo.AddComponent<WaterRipple>();
        }

        private Bounds SplineBounds()
        {
            var b = new Bounds(_s.Pos[0], Vector3.zero);
            foreach (var p in _s.Pos) b.Encapsulate(p);
            return b;
        }

        private void ComputeBounds()
        {
            var b = SplineBounds();
            b.Expand(new Vector3(60f, 20f, 60f));
            _world.Bounds = b;
        }
    }

    /// <summary>Uniform grid over track samples for fast proximity queries.</summary>
    public sealed class SampleGrid
    {
        private readonly Dictionary<long, List<int>> _cells = new Dictionary<long, List<int>>();
        private readonly float _cell;
        private readonly TrackSpline _s;

        public SampleGrid(TrackSpline s, float cell)
        {
            _s = s; _cell = cell;
            for (int i = 0; i < s.Count; i++)
            {
                long k = Key(s.Pos[i].x, s.Pos[i].z);
                if (!_cells.TryGetValue(k, out var l)) _cells[k] = l = new List<int>();
                l.Add(i);
            }
        }

        private long Key(float x, float z)
        {
            long cx = Mathf.FloorToInt(x / _cell), cz = Mathf.FloorToInt(z / _cell);
            return (cx << 32) ^ (cz & 0xffffffff);
        }

        public int Nearest(Vector3 p, float radius, out float dist)
        {
            int r = Mathf.CeilToInt(radius / _cell);
            int cx = Mathf.FloorToInt(p.x / _cell), cz = Mathf.FloorToInt(p.z / _cell);
            int best = -1;
            float bd = radius * radius;
            for (int dz = -r; dz <= r; dz++)
                for (int dx = -r; dx <= r; dx++)
                {
                    long k = ((long)(cx + dx) << 32) ^ ((cz + dz) & 0xffffffff);
                    if (!_cells.TryGetValue(k, out var l)) continue;
                    foreach (int i in l)
                    {
                        float ddx = _s.Pos[i].x - p.x, ddz = _s.Pos[i].z - p.z;
                        float d = ddx * ddx + ddz * ddz;
                        if (d < bd) { bd = d; best = i; }
                    }
                }
            dist = best >= 0 ? Mathf.Sqrt(bd) : float.MaxValue;
            return best;
        }
    }

    /// <summary>Scrolls the water normal map for gentle ripples.</summary>
    public sealed class WaterRipple : MonoBehaviour
    {
        private Material _m;
        private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        private void Start() => _m = GetComponent<MeshRenderer>().sharedMaterial;
        private void Update()
        {
            if (_m == null) return;
            float t = Time.time * 0.004f;
            _m.SetTextureOffset(BaseMap, new Vector2(t, t * 0.6f));
        }
    }
}

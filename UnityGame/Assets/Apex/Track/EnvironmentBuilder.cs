using System.Collections.Generic;
using Apex.Rendering;
using UnityEngine;

namespace Apex.Track
{
    /// <summary>
    /// Trackside environment: city, grandstands, pit complex, paddock, floodlights, braking boards,
    /// footbridges, harbour. Geometry is batched per material to keep draw calls low.
    /// </summary>
    public sealed class EnvironmentBuilder
    {
        private readonly TrackWorld _w;
        private readonly TrackSpline _s;
        private readonly Transform _root;
        private readonly int _quality;
        private readonly System.Random _rng = new System.Random(20261120);
        private readonly SampleGrid _grid;
        private readonly List<Rect> _blocked = new List<Rect>();   // axis-aligned exclusion zones (x/z)

        private Material _concrete, _roof, _steel, _glassEmit, _crowd, _seatRed, _adsEmit, _white, _dark;
        private Material[] _facades;
        private MeshBuilder[] _facadeMb;
        private MeshBuilder _roofMb, _concreteMb, _steelMb, _darkMb, _whiteMb;

        public EnvironmentBuilder(TrackWorld world, Transform root, int quality)
        {
            _w = world; _s = world.Spline; _root = root; _quality = quality;
            _grid = new SampleGrid(_s, 24f);
        }

        private float R(float a, float b) => a + (float)_rng.NextDouble() * (b - a);

        private Vector3 P(int i, float lateral, float h = 0f)
        {
            int j = _s.Wrap(i);
            return _s.Pos[j] + _s.Right[j] * lateral + Vector3.up * h;
        }

        private float Wall(int i, int side) => side > 0 ? _w.RightWall[_s.Wrap(i)] : _w.LeftWall[_s.Wrap(i)];

        public IEnumerable<float> Build()
        {
            _concrete = MaterialLibrary.LitNormal(new Color(0.8f, 0.8f, 0.8f), 0f, 0.25f, MaterialLibrary.Tex("concrete"), MaterialLibrary.Tex("concrete_n"), 0.5f, new Vector2(0.25f, 0.25f));
            _roof = MaterialLibrary.Lit(new Color(0.22f, 0.22f, 0.24f), 0.1f, 0.3f);
            _steel = MaterialLibrary.Lit(new Color(0.3f, 0.31f, 0.33f), 0.75f, 0.55f);
            _glassEmit = MaterialLibrary.Emissive(new Color(0.15f, 0.18f, 0.22f), new Color(1f, 0.85f, 0.65f), 1.1f, MaterialLibrary.Tex("glass_band"));
            _crowd = MaterialLibrary.Cutout(MaterialLibrary.Tex("crowd"), Color.white, 0.45f, null, 0.2f);
            _seatRed = MaterialLibrary.Lit(new Color(0.55f, 0.04f, 0.05f), 0.1f, 0.5f);
            _adsEmit = MaterialLibrary.Emissive(Color.white, Color.white, 0.9f, MaterialLibrary.Tex("adboard"));
            _white = MaterialLibrary.Lit(new Color(0.92f, 0.92f, 0.92f), 0f, 0.5f);
            _dark = MaterialLibrary.Lit(new Color(0.04f, 0.04f, 0.05f), 0f, 0.3f);
            _facades = new Material[3];
            _facadeMb = new MeshBuilder[3];
            for (int i = 0; i < 3; i++)
            {
                _facades[i] = MaterialLibrary.Emissive(Color.white, new Color(1f, 0.82f, 0.6f), 1.15f, MaterialLibrary.Tex("facade_" + i));
                _facades[i].SetTexture("_EmissionMap", MaterialLibrary.Tex("facade_" + i + "_e"));
                _facadeMb[i] = new MeshBuilder();
            }
            _roofMb = new MeshBuilder();
            _concreteMb = new MeshBuilder();
            _steelMb = new MeshBuilder();
            _darkMb = new MeshBuilder();
            _whiteMb = new MeshBuilder();

            BuildPitComplex();
            yield return 0.75f;
            BuildGrandstands();
            BuildFootbridges();
            yield return 0.8f;
            BuildFloodlights();
            BuildBrakingBoards();
            yield return 0.85f;
            BuildHarbour();
            BuildCity();
            yield return 0.92f;
            Flush();
            BuildTvPosts();
            yield return 0.95f;
        }

        private void Flush()
        {
            for (int i = 0; i < 3; i++) if (_facadeMb[i].VertexCount > 0) BuildSplit(_facadeMb[i], "City" + i, _facades[i]);
            BuildSplit(_roofMb, "Roofs", _roof);
            BuildSplit(_concreteMb, "Concrete", _concrete);
            BuildSplit(_steelMb, "Steel", _steel);
            BuildSplit(_darkMb, "DarkTrim", _dark);
            BuildSplit(_whiteMb, "WhiteTrim", _white);
        }

        private void BuildSplit(MeshBuilder mb, string name, Material m)
        {
            if (mb.VertexCount == 0) return;
            var go = mb.Build(name, _root, m);
            if (mb.VertexCount > 65000) go.GetComponent<MeshFilter>().sharedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        }

        // ------------------------------------------------------------------ helpers

        private static Quaternion Yaw(Vector3 fwd) => Quaternion.LookRotation(new Vector3(fwd.x, 0f, fwd.z).normalized, Vector3.up);

        /// <summary>Building box with façade UVs (1 u = 4 m bay, 1 v = 3.4 m floor).</summary>
        private void Building(Vector3 baseCenter, Quaternion rot, float width, float depth, float height, int facade)
        {
            var mb = _facadeMb[facade];
            float sink = 18f;
            Vector3 c = baseCenter + Vector3.up * (height - sink) * 0.5f;
            float h = height + sink;
            Vector3 hx = rot * Vector3.right * width * 0.5f, hz = rot * Vector3.forward * depth * 0.5f, hy = Vector3.up * h * 0.5f;
            float v0 = -sink / 13.6f, v1 = height / 13.6f;   // texture tile = 2 bays (8 m) x 4 floors (13.6 m)
            void Face(Vector3 a, Vector3 b, float len)   // a→b horizontal bottom edge, outward is to the right of a→b looking from outside
            {
                Vector3 up = Vector3.up * h;
                Vector3 bottomA = a - hy, bottomB = b - hy;
                mb.AddQuad(bottomA, bottomA + up, bottomB + up, bottomB,
                    new Vector2(0, v0), new Vector2(0, v1), new Vector2(len / 8f, v1), new Vector2(len / 8f, v0));
            }
            Vector3 p0 = c - hx - hz, p1 = c + hx - hz, p2 = c + hx + hz, p3 = c - hx + hz;
            Face(p0, p1, width);   // front (-z) seen from outside: left→right is -x → +x
            Face(p1, p2, depth);
            Face(p2, p3, width);
            Face(p3, p0, depth);
            // roof + parapet
            Vector3 top = c + hy;
            _roofMb.AddQuad(top - hx - hz, top - hx + hz, top + hx + hz, top + hx - hz);
            _concreteMb.AddBox(top + Vector3.up * 0.5f + hz * 0.98f, new Vector3(width, 1f, 0.4f), rot);
            _concreteMb.AddBox(top + Vector3.up * 0.5f - hz * 0.98f, new Vector3(width, 1f, 0.4f), rot);
            if (_rng.NextDouble() < 0.4)
                _steelMb.AddBox(top + Vector3.up * 1.4f + hx * R(-0.5f, 0.5f), new Vector3(R(3f, 6f), 2.8f, R(3f, 6f)), rot);
        }

        private bool Clear(Vector3 center, Quaternion rot, float width, float depth, float minTrackGap)
        {
            Vector3 hx = rot * Vector3.right * width * 0.5f, hz = rot * Vector3.forward * depth * 0.5f;
            Vector3[] pts = { center, center + hx + hz, center + hx - hz, center - hx + hz, center - hx - hz, center + hx, center - hx, center + hz, center - hz };
            foreach (var p in pts)
            {
                if (TrackBuilder.InHarbour(p.x, p.z + 12f)) return false;
                int i = _grid.Nearest(p, 90f, out float d);
                if (i >= 0 && d < Mathf.Max(_w.LeftWall[i], _w.RightWall[i]) + minTrackGap) return false;
                foreach (var r in _blocked) if (r.Contains(new Vector2(p.x, p.z))) return false;
            }
            return true;
        }

        private void Block(Vector3 center, float radius)
        {
            _blocked.Add(new Rect(center.x - radius, center.z - radius, radius * 2f, radius * 2f));
        }

        // ------------------------------------------------------------------ pit complex

        private void BuildPitComplex()
        {
            const float d0 = 110f, d1 = 450f;
            int i0 = _s.IndexAt(d0), i1 = _s.IndexAt(d1);
            int iMid = _s.IndexAt((d0 + d1) * 0.5f);
            float lat = -(_w.LeftWall[iMid] + 1.0f);
            var pitRoad = new MeshBuilder();
            var lines = new MeshBuilder();
            // pit lane surface between pit wall and garages
            for (int i = _s.IndexAt(40f); i < _s.IndexAt(540f); i++)
            {
                Vector3 a0 = P(i, lat - 0.6f, -0.02f), a1 = P(i, lat - 15f, -0.02f);
                Vector3 b0 = P(i + 1, lat - 0.6f, -0.02f), b1 = P(i + 1, lat - 15f, -0.02f);
                pitRoad.AddQuad(a0, b0, b1, a1, new Vector2(0, V(i)), new Vector2(0, V(i + 1)), new Vector2(1, V(i + 1)), new Vector2(1, V(i)));
                Vector3 l0 = P(i, lat - 7.4f, 0f), l1 = P(i, lat - 7.6f, 0f), m0 = P(i + 1, lat - 7.4f, 0f), m1 = P(i + 1, lat - 7.6f, 0f);
                if ((i / 2) % 2 == 0) lines.AddQuad(l0, m0, m1, l1);
            }
            FixUp(pitRoad); FixUp(lines);
            var asphalt = MaterialLibrary.LitNormal(new Color(0.6f, 0.6f, 0.62f), 0f, 0.35f, MaterialLibrary.Tex("asphalt"), MaterialLibrary.Tex("asphalt_n"), 0.4f);
            pitRoad.Build("PitLane", _root, asphalt, false);
            lines.Build("PitLaneLines", _root, _white, false);

            // pit building: 2 storeys, garages facing the pit lane
            var front = new MeshBuilder();
            var garageInterior = MaterialLibrary.Emissive(new Color(0.25f, 0.26f, 0.28f), new Color(0.9f, 0.95f, 1f), 0.8f);
            var interior = new MeshBuilder();
            var teamBoards = new MeshBuilder();
            var boardMat = MaterialLibrary.Emissive(Color.white, Color.white, 1.2f, MaterialLibrary.Tex("sponsors"));
            int garage = 0;
            for (int i = i0; i < i1; i += 6)
            {
                int j = Mathf.Min(i + 6, i1);
                Vector3 fwd = (_s.Pos[j] - _s.Pos[i]).normalized;
                Vector3 mid = (P(i, lat - 16f) + P(j, lat - 16f)) * 0.5f;
                float len = Vector3.Distance(P(i, lat - 16f), P(j, lat - 16f));
                Quaternion rot = Yaw(fwd);
                Vector3 outward = rot * Vector3.right;      // track direction is +right (south)
                // building block behind the facade
                _concreteMb.AddBox(mid - outward * 11f + Vector3.up * 2.2f, new Vector3(22f, 4.4f, len + 0.05f), rot);
                // upper glass floor
                Vector3 upper = mid - outward * 9.5f + Vector3.up * 7.2f;
                                front.AddBox(upper, new Vector3(19f, 5.2f, len + 0.02f), rot);
                _roofMb.AddBox(mid - outward * 10f + Vector3.up * 10.1f, new Vector3(24f, 0.6f, len + 0.05f), rot);
                // garage opening (dark recess with lit interior) and pillars
                interior.AddBox(mid - outward * 6.5f + Vector3.up * 2.1f, new Vector3(0.2f, 4.0f, len - 1.4f), rot);
                _darkMb.AddBox(mid - outward * 6.2f + Vector3.up * 0.02f, new Vector3(6f, 0.04f, len - 1.4f), rot);
                _concreteMb.AddBox(mid + fwd * (len * 0.5f) - outward * 1.0f + Vector3.up * 2.2f, new Vector3(2.4f, 4.4f, 0.7f), rot);
                // team board above the garage
                int cell = garage % 8;
                Vector3 bc = mid + outward * 0.05f + Vector3.up * 4.9f;
                Vector3 r = fwd * (len * 0.45f), u = Vector3.up * 0.55f;
                teamBoards.AddQuad(bc + r - u, bc + r + u, bc - r + u, bc - r - u, Cell(cell, 0, 0), Cell(cell, 0, 1), Cell(cell, 1, 1), Cell(cell, 1, 0));
                garage++;
            }
            FixFacingAll(teamBoards, P(iMid, 1f) - P(iMid, -1f));
            front.Build("PitUpperFloor", _root, _glassEmit);
            interior.Build("GarageInteriors", _root, garageInterior, false);
            teamBoards.Build("TeamBoards", _root, boardMat, false);

            // roof sign + timing tower
            var logoMat = MaterialLibrary.Emissive(Color.white, Color.white, 1.6f, MaterialLibrary.Tex("logo_sign"));
            var sign = new MeshBuilder();
            Vector3 sc = P(iMid, lat - 12f, 15.5f);
            Vector3 sf = _s.Fwd[iMid];
            sign.AddQuad(sc + sf * 30f - Vector3.up * 4f, sc + sf * 30f + Vector3.up * 4f, sc - sf * 30f + Vector3.up * 4f, sc - sf * 30f - Vector3.up * 4f,
                new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0));
            FixFacingAll(sign, _s.Right[iMid]);
            sign.Build("PitRoofSign", _root, logoMat, false);
            _steelMb.AddBox(sc - _s.Right[iMid] * 0.4f + Vector3.down * 2.5f, new Vector3(50f, 5f, 1f), Yaw(_s.Right[iMid]));

            int ti = _s.IndexAt(_w.StartLine + 30f);
            Vector3 towerBase = P(ti, _w.RightWall[ti] + 6f);
            Quaternion trot = Yaw(-_s.Right[ti]);
            _steelMb.AddBox(towerBase + Vector3.up * 14f, new Vector3(3.2f, 28f, 1.6f), trot);
            var towerMat = MaterialLibrary.Emissive(Color.white, Color.white, 1.4f, MaterialLibrary.Tex("timing_tower"));
            var tower = new MeshBuilder();
            Vector3 tf = towerBase + Vector3.up * 15f - _s.Right[ti] * 0.82f;
            Vector3 tr = _s.Fwd[ti] * 1.5f, tu = Vector3.up * 12f;
            tower.AddQuad(tf + tr - tu, tf + tr + tu, tf - tr + tu, tf - tr - tu, new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0));
            FixFacingAll(tower, -_s.Right[ti]);
            tower.Build("TimingTower", _root, towerMat, false);

            // paddock: trucks + hospitality behind the building
            var truckMats = new[] { new Color(0.78f, 0.02f, 0.04f), new Color(0.05f, 0.18f, 0.55f), new Color(0.02f, 0.4f, 0.22f), new Color(0.95f, 0.45f, 0.02f), new Color(0.45f, 0.05f, 0.55f), new Color(0.06f, 0.06f, 0.07f) };
            var truckMb = new MeshBuilder[truckMats.Length];
            for (int k = 0; k < truckMb.Length; k++) truckMb[k] = new MeshBuilder();
            int t = 0;
            for (int i = i0 + 5; i < i1 - 5; i += 9)
            {
                Vector3 f = _s.Fwd[i];
                Quaternion rot = Yaw(-_s.Right[i]);
                Vector3 pos = P(i, lat - 48f);
                truckMb[t % truckMb.Length].AddBox(pos + Vector3.up * 2.1f, new Vector3(2.6f, 3.8f, 15f), rot);
                _darkMb.AddBox(pos + Vector3.up * 0.6f, new Vector3(2.5f, 1.0f, 15.2f), rot);
                if (t % 2 == 0) _whiteMb.AddBox(P(i, lat - 64f) + Vector3.up * 2f, new Vector3(10f, 4f, 10f), Yaw(f));
                t++;
            }
            for (int k = 0; k < truckMb.Length; k++) truckMb[k].Build("Trucks" + k, _root, MaterialLibrary.Lit(truckMats[k], 0.4f, 0.75f));

            _w.PitAmbiencePoint = P(iMid, lat - 12f, 2f);
            // exclusion zone for city generation
            for (int i = _s.IndexAt(0f); i < _s.IndexAt(600f); i += 10) Block(P(i, lat - 45f), 55f);
        }

        private float V(int i) => i * _s.Length / _s.Count / 8f;

        private static Vector2 Cell(int cell, int u, int v)
        {
            int cx = cell % 4, cy = 1 - cell / 4;
            return new Vector2((cx + u) / 4f, (cy + v) / 2f);
        }

        // ------------------------------------------------------------------ grandstands

        private void BuildGrandstands()
        {
            float l = _s.Length;
            // (distance from spline start, side, length, rows)
            var stands = new List<(float d, int side, float len, int rows)>
            {
                (_w.StartLine + 110f, 1, 160f, 14),
                (_w.StartLine - 120f, 1, 140f, 12),
            };
            // stands on the outside of slow corners
            foreach (var c in _w.Analysis.Corners)
            {
                if (c.MinSpeed > 30f) continue;
                float d = _s.DistanceOf(c.Apex);
                stands.Add((d, -c.Direction, 70f, 10));
            }
            // harbour straight, inland side
            stands.Add((_s.DistanceOf(_s.IndexAt(l * 0.36f)), 1, 120f, 10));

            var crowd = new MeshBuilder();
            var seats = new MeshBuilder();
            var roofs = new MeshBuilder();
            var adsFront = new MeshBuilder();
            foreach (var st in stands)
            {
                int i = _s.IndexAt(st.d);
                float wall = Wall(i, st.side) + 3.5f;
                Vector3 baseC = P(i, st.side * wall);
                Vector3 fwd = new Vector3(_s.Fwd[i].x, 0f, _s.Fwd[i].z).normalized;
                Vector3 outward = _s.Right[i] * st.side;      // away from track
                // check that the stand does not overlap other parts of the track
                Quaternion rot = Quaternion.LookRotation(outward, Vector3.up);
                float depth = st.rows * 0.85f + 2f;
                if (!Clear(baseC + outward * depth * 0.5f, rot, st.len, depth, 2.5f)) continue;
                for (int r = 0; r < st.rows; r++)
                {
                    float y = 0.9f + r * 0.55f;
                    Vector3 rowC = baseC + outward * (1f + r * 0.85f) + Vector3.up * y;
                    // tread + riser
                    _concreteMb.AddBox(rowC - Vector3.up * 0.3f, new Vector3(st.len, 0.6f + r * 0.0f, 0.85f), rot * Quaternion.identity);
                    seats.AddBox(rowC + Vector3.up * 0.12f + outward * 0.15f, new Vector3(st.len - 0.4f, 0.25f, 0.4f), rot);
                    // crowd strip facing the track
                    Vector3 cc = rowC + Vector3.up * 0.65f - outward * 0.05f;
                    Vector3 hr = fwd * (st.len * 0.5f - 0.3f), hu = Vector3.up * 0.55f;
                    crowd.AddQuad(cc - hr - hu, cc - hr + hu, cc + hr + hu, cc + hr - hu,
                        new Vector2(0, 0), new Vector2(0, 1), new Vector2(st.len / 24f, 1), new Vector2(st.len / 24f, 0));
                }
                float topY = 0.9f + st.rows * 0.55f;
                // back wall, side walls, columns, roof
                _concreteMb.AddBox(baseC + outward * (depth - 0.5f) + Vector3.up * (topY + 1.5f) * 0.5f, new Vector3(st.len, topY + 1.5f, 0.4f), rot);
                for (int s = -1; s <= 1; s += 2)
                    _concreteMb.AddBox(baseC + fwd * (s * st.len * 0.5f) + outward * depth * 0.5f + Vector3.up * topY * 0.5f, new Vector3(0.4f, topY, depth), rot);
                for (float x = -st.len * 0.5f; x <= st.len * 0.5f + 0.1f; x += 20f)
                    _steelMb.AddBox(baseC + fwd * x + outward * (depth - 0.6f) + Vector3.up * (topY + 4.5f) * 0.5f, new Vector3(0.4f, topY + 4.5f, 0.4f), rot);
                roofs.AddBox(baseC + outward * (depth * 0.55f) + Vector3.up * (topY + 4.4f), new Vector3(st.len + 2f, 0.3f, depth + 2f), rot * Quaternion.Euler(-6f, 0f, 0f));
                // advertising fascia under the roof
                Vector3 ac = baseC + outward * 0.2f + Vector3.up * (topY + 3.9f);
                Vector3 ar = fwd * (st.len * 0.5f), au = Vector3.up * 0.7f;
                adsFront.AddQuad(ac - ar - au, ac - ar + au, ac + ar + au, ac + ar - au,
                    new Vector2(0, 0), new Vector2(0, 1), new Vector2(st.len / 22.4f, 1), new Vector2(st.len / 22.4f, 0));
                _w.CrowdPoints.Add(baseC + outward * depth * 0.5f + Vector3.up * 5f);
                Block(baseC + outward * depth * 0.5f, Mathf.Max(st.len, depth) * 0.55f);
            }
            FixFacingTowardTrack(crowd);
            FixFacingTowardTrack(adsFront);
            crowd.Build("Crowd", _root, _crowd, false);
            seats.Build("Seats", _root, _seatRed);
            roofs.Build("StandRoofs", _root, MaterialLibrary.Lit(new Color(0.85f, 0.86f, 0.88f), 0.6f, 0.6f));
            adsFront.Build("StandAds", _root, _adsEmit, false);
        }

        /// <summary>Flip quads so they face the closest point on the track and text reads correctly.</summary>
        private void FixFacingTowardTrack(MeshBuilder mb)
        {
            for (int q = 0; q < mb.Normals.Count; q += 4)
            {
                Vector3 c = (mb.Vertices[q] + mb.Vertices[q + 2]) * 0.5f;
                int i = _grid.Nearest(c, 200f, out _);
                if (i < 0) continue;
                Vector3 toTrack = _s.Pos[i] - c;
                toTrack.y = 0f;
                if (Vector3.Dot(mb.Normals[q], toTrack) >= 0f) continue;
                // mirror: swap left/right vertices so the quad faces the other way and UVs stay readable
                for (int k = q; k < q + 4; k++) mb.Normals[k] = -mb.Normals[k];
                Vector3 v0 = mb.Vertices[q], v1 = mb.Vertices[q + 1];
                mb.Vertices[q] = mb.Vertices[q + 3]; mb.Vertices[q + 1] = mb.Vertices[q + 2];
                mb.Vertices[q + 3] = v0; mb.Vertices[q + 2] = v1;
            }
        }

        private static void FixFacingAll(MeshBuilder mb, Vector3 desired)
        {
            for (int q = 0; q < mb.Normals.Count; q += 4)
            {
                if (Vector3.Dot(mb.Normals[q], desired) >= 0f) continue;
                for (int k = q; k < q + 4; k++) mb.Normals[k] = -mb.Normals[k];
                Vector3 v0 = mb.Vertices[q], v1 = mb.Vertices[q + 1];
                mb.Vertices[q] = mb.Vertices[q + 3]; mb.Vertices[q + 1] = mb.Vertices[q + 2];
                mb.Vertices[q + 3] = v0; mb.Vertices[q + 2] = v1;
            }
        }

        private static void FixUp(MeshBuilder mb) => FixFacingAll(mb, Vector3.up);

        // ------------------------------------------------------------------ bridges, lights, boards

        private void BuildFootbridges()
        {
            float l = _s.Length;
            float[] ds = { _w.StartLine + 820f, l * 0.42f, l * 0.74f };
            var banners = new MeshBuilder();
            foreach (float d in ds)
            {
                int i = _s.IndexAt(d);
                float lw = _w.LeftWall[i] + 1.5f, rw = _w.RightWall[i] + 1.5f;
                Vector3 a = P(i, -lw), b = P(i, rw);
                Vector3 f = _s.Fwd[i];
                Quaternion rot = Yaw(f);
                Vector3 mid = (a + b) * 0.5f;
                float span = lw + rw;
                _steelMb.AddBox(mid + Vector3.up * 7.2f, new Vector3(span + 2f, 0.5f, 3f), rot);
                _steelMb.AddBox(mid + Vector3.up * 8.4f + f * 1.45f, new Vector3(span + 2f, 0.12f, 0.12f), rot);
                _steelMb.AddBox(mid + Vector3.up * 8.4f - f * 1.45f, new Vector3(span + 2f, 0.12f, 0.12f), rot);
                for (int s = -1; s <= 1; s += 2)
                {
                    Vector3 tower = s < 0 ? a : b;
                    _concreteMb.AddBox(tower + Vector3.up * 4f, new Vector3(3f, 8f, 3.2f), rot);
                }
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 c = mid + Vector3.up * 6.3f + f * (side * 1.52f);
                    Vector3 r = _s.Right[i] * (span * 0.5f), u = Vector3.up * 0.8f;
                    var q = new MeshBuilder();
                    banners.AddQuad(c - r - u, c - r + u, c + r + u, c + r - u, new Vector2(0, 0), new Vector2(0, 1), new Vector2(span / 25.6f, 1), new Vector2(span / 25.6f, 0));
                    FixFacingAllLast(banners, f * side);
                }
            }
            banners.Build("BridgeBanners", _root, _adsEmit, false);
        }

        private static void FixFacingAllLast(MeshBuilder mb, Vector3 desired)
        {
            int q = mb.Normals.Count - 4;
            if (Vector3.Dot(mb.Normals[q], desired) >= 0f) return;
            for (int k = q; k < q + 4; k++) mb.Normals[k] = -mb.Normals[k];
            Vector3 v0 = mb.Vertices[q], v1 = mb.Vertices[q + 1];
            mb.Vertices[q] = mb.Vertices[q + 3]; mb.Vertices[q + 1] = mb.Vertices[q + 2];
            mb.Vertices[q + 3] = v0; mb.Vertices[q + 2] = v1;
        }

        private void BuildFloodlights()
        {
            var headMat = MaterialLibrary.Emissive(new Color(0.9f, 0.9f, 0.85f), new Color(1f, 0.95f, 0.85f), 5f);
            var heads = new MeshBuilder();
            int step = Mathf.RoundToInt(70f / TrackSpline.Spacing);
            int k = 0;
            for (int i = 0; i < _s.Count; i += step, k++)
            {
                int side = k % 2 == 0 ? 1 : -1;
                float lat = side * (Wall(i, side) + 2.4f);
                Vector3 b = P(i, lat);
                _steelMb.AddCylinder(b - Vector3.up * 1f, b + Vector3.up * 19f, 0.22f, 8, false);
                Vector3 headC = b + Vector3.up * 19.5f - _s.Right[_s.Wrap(i)] * side * 0.8f;
                Quaternion rot = Quaternion.LookRotation(-_s.Right[_s.Wrap(i)] * side + Vector3.down * 0.9f, Vector3.up);
                _steelMb.AddBox(headC, new Vector3(3.2f, 1.6f, 0.4f), rot);
                heads.AddBox(headC + rot * Vector3.forward * 0.22f, new Vector3(3f, 1.4f, 0.05f), rot);
                if (_quality >= 2 && k % 2 == 0)
                {
                    var go = new GameObject("Floodlight");
                    go.transform.SetParent(_root, false);
                    go.transform.position = headC;
                    go.transform.rotation = Quaternion.LookRotation(-_s.Right[_s.Wrap(i)] * side * 0.6f + Vector3.down, Vector3.up);
                    var l = go.AddComponent<Light>();
                    l.type = LightType.Spot;
                    l.spotAngle = 95f;
                    l.innerSpotAngle = 50f;
                    l.range = 55f;
                    l.intensity = 9f;
                    l.color = new Color(1f, 0.93f, 0.82f);
                    l.shadows = LightShadows.None;
                }
            }
            heads.Build("FloodlightHeads", _root, headMat, false);
        }

        private void BuildBrakingBoards()
        {
            var boardMat = MaterialLibrary.Lit(Color.white, 0f, 0.4f, MaterialLibrary.Tex("brakeboards"));
            var boards = new MeshBuilder();
            int[] dist = { 150, 100, 50 };
            foreach (var c in _w.Analysis.Corners)
            {
                float entry = _s.DistanceOf(c.Start);
                int iBefore = _s.IndexAt(entry - 20f);
                float speedBefore = _w.Analysis.Speed[iBefore];
                float brakeSpeed = _w.Analysis.Speed[_s.IndexAt(entry - 160f)];
                if (brakeSpeed - c.MinSpeed < 14f) continue;
                int side = -c.Direction;   // outside of the corner
                for (int b = 0; b < 3; b++)
                {
                    int i = _s.IndexAt(entry - dist[b]);
                    float lat = side * (Wall(i, side) - 0.3f);
                    Vector3 basePos = P(i, lat, WallHeight());
                    Vector3 f = _s.Fwd[i];
                    Vector3 r = _s.Right[i] * 0.7f, u = Vector3.up * 0.7f;
                    Vector3 c0 = basePos + Vector3.up * 1.0f;
                    boards.AddQuad(c0 - r - u, c0 - r + u, c0 + r + u, c0 + r - u, new Vector2(b / 3f, 0), new Vector2(b / 3f, 1), new Vector2((b + 1) / 3f, 1), new Vector2((b + 1) / 3f, 0));
                    FixFacingAllLast(boards, -f);
                    _steelMb.AddBox(basePos + Vector3.up * 0.15f, new Vector3(0.1f, 0.3f, 0.1f), Yaw(f));
                }
                _ = speedBefore;
            }
            boards.Build("BrakingBoards", _root, boardMat, false);
        }

        private static float WallHeight() => 1.05f;

        // ------------------------------------------------------------------ harbour

        private void BuildHarbour()
        {
            // quay wall
            var stone = MaterialLibrary.LitNormal(new Color(0.75f, 0.72f, 0.66f), 0f, 0.2f, MaterialLibrary.Tex("concrete"), MaterialLibrary.Tex("concrete_n"), 0.6f, new Vector2(40f, 1f));
            var quay = new MeshBuilder();
            quay.AddBox(new Vector3(-5f, -10.5f, TrackBuilder.HarbourZ + 1.5f), new Vector3(930f, 5.6f, 3f));
            quay.Build("QuayWall", _root, stone);

            // promenade palms between the barrier and the quay
            var trunkMat = MaterialLibrary.Lit(new Color(0.45f, 0.36f, 0.27f), 0f, 0.2f);
            var frondMat = MaterialLibrary.Cutout(MaterialLibrary.Tex("palm"), Color.white, 0.4f, null, 0.25f);
            var trunks = new MeshBuilder();
            var fronds = new MeshBuilder();
            for (int i = 0; i < _s.Count; i += 9)
            {
                Vector3 p = _s.Pos[i];
                if (p.z > -500f || p.x < -440f || p.x > 420f) continue;
                Vector3 basePos = P(i, -(_w.LeftWall[i] + 9f));
                if (basePos.z < TrackBuilder.HarbourZ + 4f) continue;
                Palm(trunks, fronds, basePos, R(8f, 12f));
            }
            trunks.Build("PalmTrunks", _root, trunkMat);
            fronds.Build("PalmFronds", _root, frondMat);

            // yachts
            var hull = new MeshBuilder();
            var cabin = new MeshBuilder();
            for (int k = 0; k < 26; k++)
            {
                float x = R(-420f, 420f), z = R(TrackBuilder.HarbourZ - 40f, TrackBuilder.HarbourZ - 260f);
                float len = R(14f, 38f);
                Quaternion rot = Quaternion.Euler(0f, R(-20f, 20f) + (_rng.NextDouble() < 0.5 ? 0f : 180f), 0f);
                Yacht(hull, cabin, new Vector3(x, -9.6f, z), rot, len);
            }
            hull.Build("YachtHulls", _root, MaterialLibrary.Lit(new Color(0.95f, 0.95f, 0.96f), 0.1f, 0.85f));
            cabin.Build("YachtCabins", _root, MaterialLibrary.Lit(new Color(0.08f, 0.1f, 0.13f), 0.3f, 0.95f));
        }

        private void Palm(MeshBuilder trunks, MeshBuilder fronds, Vector3 basePos, float height)
        {
            Vector3 lean = new Vector3(R(-1f, 1f), 0f, R(-1f, 1f)).normalized * R(0.6f, 1.6f);
            Vector3 prev = basePos - Vector3.up;
            int segs = 5;
            for (int s = 1; s <= segs; s++)
            {
                float t = s / (float)segs;
                Vector3 p = basePos + Vector3.up * height * t + lean * t * t;
                trunks.AddCylinder(prev, p, Mathf.Lerp(0.28f, 0.16f, t), 7, false);
                prev = p;
            }
            for (int f = 0; f < 8; f++)
            {
                float a = f * 45f + R(-10f, 10f);
                Quaternion q = Quaternion.Euler(R(15f, 35f), a, 0f);
                Vector3 dir = q * Vector3.forward;
                Vector3 side = q * Vector3.right * 0.7f;
                Vector3 tip = prev + dir * 4.2f + Vector3.down * 1.4f;
                fronds.AddQuad(prev - side, prev + side, tip + side, tip - side, new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1));
                fronds.AddQuad(prev + side, prev - side, tip - side, tip + side, new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1));
            }
        }

        private void Yacht(MeshBuilder hull, MeshBuilder cabin, Vector3 pos, Quaternion rot, float len)
        {
            float beam = len * 0.26f, h = len * 0.12f;
            var rings = new List<Vector3[]>();
            int st = 10;
            for (int i = 0; i <= st; i++)
            {
                float t = i / (float)st;
                float w = beam * 0.5f * Mathf.Sqrt(Mathf.Clamp01(1f - Mathf.Pow(t * 1.05f, 3f)) * (0.55f + 0.45f * Mathf.Sin(Mathf.Min(1f, t + 0.6f) * Mathf.PI)));
                w = Mathf.Max(w, 0.05f);
                float z = (t - 0.5f) * len;
                var ring = new Vector3[6];
                ring[0] = new Vector3(0f, -h * 0.6f, z);
                ring[1] = new Vector3(w * 0.7f, -h * 0.35f, z);
                ring[2] = new Vector3(w, h * 0.5f, z);
                ring[3] = new Vector3(-w, h * 0.5f, z);
                ring[4] = new Vector3(-w * 0.7f, -h * 0.35f, z);
                ring[5] = new Vector3(0f, -h * 0.6f, z);
                for (int k = 0; k < 6; k++) ring[k] = pos + rot * ring[k];
                rings.Add(ring);
            }
            // reorder ring so it's a closed loop without the duplicated keel point
            var closed = new List<Vector3[]>();
            foreach (var r in rings) closed.Add(new[] { r[0], r[1], r[2], r[3], r[4] });
            hull.AddLoft(closed, true);
            hull.AddCap(closed[closed.Count - 1], rot * Vector3.forward, false);
            hull.AddCap(closed[0], rot * Vector3.back, true);
            cabin.AddBox(pos + rot * new Vector3(0f, h * 0.5f + len * 0.04f, -len * 0.08f), new Vector3(beam * 0.7f, len * 0.08f, len * 0.4f), rot);
            if (len > 25f) cabin.AddBox(pos + rot * new Vector3(0f, h * 0.5f + len * 0.11f, -len * 0.12f), new Vector3(beam * 0.55f, len * 0.06f, len * 0.25f), rot);
        }

        // ------------------------------------------------------------------ city

        private void BuildCity()
        {
            int count = 0;
            float density = _quality >= 2 ? 1f : 0.6f;
            // trackside blocks
            for (int side = -1; side <= 1; side += 2)
            {
                float d = 0f;
                while (d < _s.Length)
                {
                    d += R(16f, 26f) / density;
                    int i = _s.IndexAt(d);
                    float width = R(14f, 26f), depth = R(14f, 28f);
                    bool oldTown = _s.Pos[i].x < -460f && _s.Pos[i].z < -250f;
                    float height = oldTown ? R(9f, 18f) : R(14f, 46f);
                    float lat = side * (Wall(i, side) + 4f + depth * 0.5f);
                    Vector3 c = P(i, lat);
                    Quaternion rot = Yaw(_s.Fwd[i]);
                    if (!Clear(c, rot, width, depth, 3f)) continue;
                    Building(c, rot, width, depth, height, oldTown ? 2 : _rng.Next(0, 2));
                    count++;
                }
            }
            // outer city fill (skyline)
            Bounds b = _w.Bounds;
            b.Expand(new Vector3(500f, 0f, 500f));
            int attempts = (int)(1600 * density);
            for (int a = 0; a < attempts; a++)
            {
                float x = R(b.min.x, b.max.x), z = R(b.min.z, b.max.z);
                var probe = new Vector3(x, 0f, z);
                int near = _grid.Nearest(probe, 400f, out float dist);
                if (near < 0 || dist < 45f) continue;
                if (TrackBuilder.InHarbour(x, z + 30f)) continue;
                float width = R(18f, 40f), depth = R(18f, 40f);
                float height = Mathf.Lerp(18f, 90f, Mathf.Pow((float)_rng.NextDouble(), 2f)) * (dist > 250f ? 1.3f : 1f);
                float y = _s.Pos[near].y - 0.2f;
                Quaternion rot = Quaternion.Euler(0f, Mathf.Round(R(0f, 4f)) * 90f + R(-8f, 8f), 0f);
                Vector3 c = new Vector3(x, y, z);
                if (!Clear(c, rot, width, depth, 12f)) continue;
                Block(c, Mathf.Max(width, depth) * 0.6f);
                Building(c, rot, width, depth, height, _rng.Next(0, 3));
                count++;
            }
            Debug.Log($"[Environment] {count} buildings placed");
        }

        private void BuildTvPosts()
        {
            int step = Mathf.RoundToInt(220f / TrackSpline.Spacing);
            int k = 0;
            for (int i = step / 2; i < _s.Count; i += step, k++)
            {
                int side = (k % 2 == 0) ? 1 : -1;
                if (Mathf.Abs(_s.Curvature[i]) > 1f / 200f) side = _s.Curvature[i] > 0 ? -1 : 1;   // outside of corners
                _w.TvCameraPosts.Add(P(i, side * (Wall(i, side) + 4f), 7f));
            }
        }
    }
}

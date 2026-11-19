using System.Collections.Generic;
using Apex.Rendering;
using UnityEngine;

namespace Apex.Track
{
    /// <summary>
    /// Visual-overhaul city layer: modular PBR buildings (shopfront base, façade body, cornice, setback
    /// towers, rooftop plant) and the streetscape behind the barriers (kerbstones, sidewalks, street lamps,
    /// trees, benches, bins, bus shelters, traffic signs). All geometry is batched per material.
    /// </summary>
    public sealed partial class EnvironmentBuilder
    {
        private sealed class FacadeStyle
        {
            public string Set;
            public float TileW, TileH;     // metres covered by one texture tile
            public float Smooth;
            public Material Mat;
            public MeshBuilder Mb;
        }

        private readonly Dictionary<string, FacadeStyle> _styles = new Dictionary<string, FacadeStyle>();
        private FacadeStyle[] _modern;
        private FacadeStyle _brickStyle, _oldTownStyle;
        private Material _storefrontMat, _signMat, _ledgeMat, _gravelMat, _paveMat, _kerbStoneMat, _poleMat, _lampMat, _barkMat, _foliageMat, _woodMat, _signsMat, _shelterGlass;
        private Material[] _awningMats;
        private MeshBuilder _storefrontMb, _signMb, _ledgeMb, _gravelMb, _paveMb, _kerbStoneMb, _poleMb, _lampMb, _barkMb, _foliageMb, _woodMb, _signsMb, _shelterMb;
        private MeshBuilder[] _awningMb;

        private void InitCityMaterials()
        {
            FacadeStyle Style(string set, float tw, float th, float smooth)
            {
                var st = new FacadeStyle { Set = set, TileW = tw, TileH = th, Smooth = smooth, Mb = new MeshBuilder() };
                st.Mat = MaterialLibrary.PBR(set, Vector2.one, Color.white, smooth, 1f);
                _styles[set] = st;
                return st;
            }
            _modern = new[]
            {
                Style("facade_office", 24f, 26f, 1f),
                Style("facade_glass", 12f, 12f, 1.05f),
                Style("facade_tower", 20f, 24f, 1f),
            };
            _brickStyle = Style("facade_brick", 18f, 19.5f, 1f);
            _oldTownStyle = Style("bricks", 4f, 2f, 0.9f);

            _storefrontMat = MaterialLibrary.PBR("facade_glass", Vector2.one, new Color(0.55f, 0.6f, 0.65f), 1.1f, 1f);
            _signMat = MaterialLibrary.Emissive(Color.white, Color.white, 0.35f, MaterialLibrary.Tex("sponsors"));
            _ledgeMat = MaterialLibrary.PBR("concrete", new Vector2(0.5f, 0.5f), new Color(0.86f, 0.84f, 0.8f), 0.9f, 1f);
            _gravelMat = MaterialLibrary.PBR("concrete", new Vector2(0.15f, 0.15f), new Color(0.38f, 0.38f, 0.4f), 0.5f, 1.5f);
            _paveMat = MaterialLibrary.PBR("concrete", Vector2.one, new Color(0.8f, 0.78f, 0.75f), 0.85f, 1f);
            _kerbStoneMat = MaterialLibrary.PBR("concrete", Vector2.one, new Color(0.62f, 0.62f, 0.64f), 0.9f, 1f);
            _poleMat = MaterialLibrary.Lit(new Color(0.16f, 0.17f, 0.18f), 0.8f, 0.55f);
            _lampMat = MaterialLibrary.Emissive(new Color(0.85f, 0.85f, 0.8f), new Color(1f, 0.92f, 0.78f), 0.6f);
            _barkMat = MaterialLibrary.Lit(new Color(0.3f, 0.24f, 0.18f), 0f, 0.2f);
            _foliageMat = MaterialLibrary.Cutout(MaterialLibrary.Tex("pbr/foliage"), new Color(0.85f, 0.95f, 0.8f), 0.45f, null, 0.15f);
            _woodMat = MaterialLibrary.Lit(new Color(0.45f, 0.3f, 0.18f), 0f, 0.35f);
            _signsMat = MaterialLibrary.Cutout(MaterialLibrary.Tex("pbr/signs"), Color.white, 0.4f, null, 0.5f);
            _shelterGlass = MaterialLibrary.Transparent(new Color(0.7f, 0.8f, 0.85f, 0.25f), 0.95f, 0f);
            Color[] awning = { new Color(0.6f, 0.06f, 0.08f), new Color(0.08f, 0.22f, 0.15f), new Color(0.12f, 0.14f, 0.2f), new Color(0.75f, 0.6f, 0.4f) };
            _awningMats = new Material[awning.Length];
            _awningMb = new MeshBuilder[awning.Length];
            for (int i = 0; i < awning.Length; i++) { _awningMats[i] = MaterialLibrary.Lit(awning[i], 0f, 0.3f); _awningMb[i] = new MeshBuilder(); }
            _storefrontMb = new MeshBuilder(); _signMb = new MeshBuilder(); _ledgeMb = new MeshBuilder(); _gravelMb = new MeshBuilder();
            _paveMb = new MeshBuilder(); _kerbStoneMb = new MeshBuilder(); _poleMb = new MeshBuilder(); _lampMb = new MeshBuilder();
            _barkMb = new MeshBuilder(); _foliageMb = new MeshBuilder(); _woodMb = new MeshBuilder(); _signsMb = new MeshBuilder(); _shelterMb = new MeshBuilder();
        }

        private void FlushCity()
        {
            foreach (var st in _styles.Values) BuildSplit(st.Mb, "City_" + st.Set, st.Mat);
            BuildSplit(_storefrontMb, "Shopfronts", _storefrontMat);
            BuildSplit(_signMb, "ShopSigns", _signMat);
            BuildSplit(_ledgeMb, "Cornices", _ledgeMat);
            BuildSplit(_gravelMb, "RoofGravel", _gravelMat);
            BuildSplit(_paveMb, "Sidewalks", _paveMat);
            BuildSplit(_kerbStoneMb, "Kerbstones", _kerbStoneMat);
            BuildSplit(_poleMb, "StreetPoles", _poleMat);
            BuildSplit(_lampMb, "StreetLamps", _lampMat);
            BuildSplit(_barkMb, "TreeTrunks", _barkMat);
            BuildSplit(_foliageMb, "TreeFoliage", _foliageMat);
            BuildSplit(_woodMb, "Benches", _woodMat);
            BuildSplit(_signsMb, "TrafficSigns", _signsMat);
            BuildSplit(_shelterMb, "BusShelters", _shelterGlass);
            for (int i = 0; i < _awningMb.Length; i++) BuildSplit(_awningMb[i], "Awnings" + i, _awningMats[i]);
        }

        // ------------------------------------------------------------------ modular building

        /// <summary>Vertical face quad from bottom edge a→b (outward to the right seen from outside), y-range relative to base.</summary>
        private static void Face(MeshBuilder mb, Vector3 a, Vector3 b, float y0, float y1, float u0, float u1, float v0, float v1)
        {
            Vector3 lo = Vector3.up * y0, hi = Vector3.up * y1;
            mb.AddQuad(a + lo, a + hi, b + hi, b + lo, new Vector2(u0, v0), new Vector2(u0, v1), new Vector2(u1, v1), new Vector2(u1, v0));
        }

        private static void Ring(MeshBuilder mb, Vector3 c, Quaternion rot, float w, float d, float y0, float y1, float tileW, float tileH, float uOffset)
        {
            Vector3 hx = rot * Vector3.right * w * 0.5f, hz = rot * Vector3.forward * d * 0.5f;
            Vector3 p0 = c - hx - hz, p1 = c + hx - hz, p2 = c + hx + hz, p3 = c - hx + hz;
            float v0 = y0 / tileH, v1 = y1 / tileH;
            float u = uOffset;
            Face(mb, p0, p1, y0, y1, u, u + w / tileW, v0, v1); u += w / tileW;
            Face(mb, p1, p2, y0, y1, u, u + d / tileW, v0, v1); u += d / tileW;
            Face(mb, p2, p3, y0, y1, u, u + w / tileW, v0, v1); u += w / tileW;
            Face(mb, p3, p0, y0, y1, u, u + d / tileW, v0, v1);
        }

        private static void Lid(MeshBuilder mb, Vector3 c, Quaternion rot, float w, float d, float y, float tile)
        {
            Vector3 hx = rot * Vector3.right * w * 0.5f, hz = rot * Vector3.forward * d * 0.5f, top = c + Vector3.up * y;
            mb.AddQuad(top - hx - hz, top - hx + hz, top + hx + hz, top + hx - hz,
                new Vector2(0, 0), new Vector2(0, d / tile), new Vector2(w / tile, d / tile), new Vector2(w / tile, 0));
        }

        /// <summary>
        /// Modular building: shopfront base with sign band and awning, PBR façade body at real-world scale,
        /// cornice, optional setback tower, gravel roof with plant. facade == 2 selects the old-town style.
        /// </summary>
        private void Building(Vector3 baseCenter, Quaternion rot, float width, float depth, float height, int facade)
        {
            const float sink = 18f;
            bool oldTown = facade == 2;
            var style = oldTown ? (_rng.NextDouble() < 0.7 ? _brickStyle : _oldTownStyle) : _modern[_rng.Next(_modern.Length)];
            float uOff = (float)_rng.NextDouble();
            float baseH = oldTown ? 0f : 4.4f;
            Vector3 c = baseCenter;
            Vector3 fwd = rot * Vector3.forward, right = rot * Vector3.right;

            // shopfront base (glazed) + sign band + awning on the street side
            if (!oldTown)
            {
                Ring(_storefrontMb, c, rot, width, depth, -sink, baseH - 0.9f, 6f, 6f, uOff);
                Ring(_ledgeMb, c, rot, width + 0.05f, depth + 0.05f, baseH - 0.9f, baseH, 4f, 4f, 0f);
                // sign panels on the two long faces (atlas cells)
                int cell = _rng.Next(0, 8);
                for (int s = -1; s <= 1; s += 2)
                {
                    Vector3 fc = c + fwd * (s * (depth * 0.5f + 0.06f)) + Vector3.up * (baseH - 0.45f);
                    Vector3 r = right * Mathf.Min(width * 0.35f, 5f), u = Vector3.up * 0.38f;
                    var a0 = fc - r - u; var a1 = fc - r + u; var a2 = fc + r + u; var a3 = fc + r - u;
                    if (s < 0) { var t0 = a0; var t1 = a1; a0 = a3; a1 = a2; a3 = t0; a2 = t1; }
                    _signMb.AddQuad(a0, a1, a2, a3, Cell(cell, 0, 0), Cell(cell, 0, 1), Cell(cell, 1, 1), Cell(cell, 1, 0));
                }
                var awn = _awningMb[_rng.Next(_awningMb.Length)];
                for (int s = -1; s <= 1; s += 2)
                    awn.AddBox(c + fwd * (s * (depth * 0.5f + 0.8f)) + Vector3.up * (baseH - 1.25f), new Vector3(width * 0.85f, 0.12f, 1.6f), rot * Quaternion.Euler(s * 12f, 0f, 0f));
            }
            else
            {
                Ring(_oldTownStyle.Mb, c, rot, width, depth, -sink, 3.6f, _oldTownStyle.TileW, _oldTownStyle.TileH, uOff);
                baseH = 3.6f;
                Ring(_ledgeMb, c, rot, width + 0.3f, depth + 0.3f, baseH, baseH + 0.35f, 4f, 4f, 0f);
            }

            // façade body
            float bodyTop = Mathf.Max(baseH + 6f, height);
            Ring(style.Mb, c, rot, width, depth, baseH, bodyTop, style.TileW, style.TileH, uOff);
            // mid-height string course on classic buildings
            if (oldTown || style == _brickStyle)
                for (float y = baseH + 10f; y < bodyTop - 4f; y += 10f)
                    Ring(_ledgeMb, c, rot, width + 0.25f, depth + 0.25f, y, y + 0.28f, 4f, 4f, 0f);
            // cornice
            Ring(_ledgeMb, c, rot, width + 0.7f, depth + 0.7f, bodyTop, bodyTop + 0.8f, 4f, 4f, 0f);
            Lid(_ledgeMb, c, rot, width + 0.7f, depth + 0.7f, bodyTop + 0.8f, 6f);
            float roofY = bodyTop + 0.81f;
            Lid(_gravelMb, c, rot, width - 0.2f, depth - 0.2f, roofY, 8f);

            // setback tower
            if (!oldTown && height > 42f && _rng.NextDouble() < 0.55)
            {
                float tw = width * R(0.55f, 0.75f), td = depth * R(0.55f, 0.75f), th = R(8f, 26f);
                Ring(style.Mb, c, rot, tw, td, roofY, roofY + th, style.TileW, style.TileH, uOff + 0.37f);
                Ring(_ledgeMb, c, rot, tw + 0.5f, td + 0.5f, roofY + th, roofY + th + 0.6f, 4f, 4f, 0f);
                Lid(_gravelMb, c, rot, tw, td, roofY + th + 0.6f, 8f);
                if (_rng.NextDouble() < 0.6)
                    _poleMb.AddCylinder(c + Vector3.up * (roofY + th + 0.6f), c + Vector3.up * (roofY + th + R(6f, 14f)), 0.12f, 6, false);
            }
            // rooftop plant: AC units and a water tank
            int units = _rng.Next(1, 4);
            for (int k = 0; k < units; k++)
            {
                Vector3 p = c + right * R(-width * 0.3f, width * 0.3f) + fwd * R(-depth * 0.3f, depth * 0.3f) + Vector3.up * (roofY + 0.7f);
                _steelMb.AddBox(p, new Vector3(R(1.6f, 3f), 1.4f, R(1.6f, 2.6f)), rot);
            }
            if (_rng.NextDouble() < 0.3)
            {
                Vector3 p = c + right * R(-width * 0.25f, width * 0.25f) + Vector3.up * roofY;
                _darkMb.AddCylinder(p, p + Vector3.up * 3.2f, 1.3f, 12, true);
            }
        }

        // ------------------------------------------------------------------ streetscape

        private void BuildStreetscape()
        {
            int n = _s.Count;
            for (int side = -1; side <= 1; side += 2)
            {
                bool prevOk = false;
                for (int i = 0; i < n; i++)
                {
                    int j = (i + 1) % n;
                    bool ok = SidewalkOk(i, side) && SidewalkOk(j, side);
                    if (ok)
                    {
                        float wi = Wall(i, side) + 0.62f, wj = Wall(j, side) + 0.62f;
                        float y = 0.12f;
                        Vector3 a0 = P(i, side * wi, y), a1 = P(i, side * (wi + 5.6f), y);
                        Vector3 b0 = P(j, side * wj, y), b1 = P(j, side * (wj + 5.6f), y);
                        float v0 = i * TrackSpline.Spacing / 2.5f, v1 = v0 + TrackSpline.Spacing / 2.5f;
                        _paveMb.AddQuad(a0, b0, b1, a1, new Vector2(0, v0), new Vector2(0, v1), new Vector2(2.2f, v1), new Vector2(2.2f, v0));
                        FixLastUp(_paveMb);
                        // kerbstone along the inner edge
                        Vector3 k0 = P(i, side * (wi + 0.15f), 0.0f), k1 = P(j, side * (wj + 0.15f), 0.0f);
                        Vector3 mid = (k0 + k1) * 0.5f + Vector3.up * 0.09f;
                        _kerbStoneMb.AddBox(mid, new Vector3(0.3f, 0.2f, Vector3.Distance(k0, k1) + 0.02f), Yaw(k1 - k0));
                    }
                    prevOk = ok;
                }
                _ = prevOk;
            }

            // street furniture
            int lampStep = Mathf.RoundToInt(32f / TrackSpline.Spacing);
            int k2 = 0;
            for (int i = 0; i < n; i += lampStep / 2, k2++)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    if (!SidewalkOk(i, side)) continue;
                    float lat = side * (Wall(i, side) + 0.62f + 1.1f);
                    Vector3 basePos = P(i, lat, 0.12f);
                    Vector3 toRoad = -_s.Right[_s.Wrap(i)] * side;
                    Vector3 along = _s.Fwd[_s.Wrap(i)];
                    int kind = (k2 + (side > 0 ? 0 : 1)) % 4;
                    if (kind == 0) StreetLamp(basePos, toRoad);
                    else if (kind == 1) Tree(P(i, side * (Wall(i, side) + 4.2f), 0.12f));
                    else if (kind == 2 && _rng.NextDouble() < 0.6) Bench(P(i, side * (Wall(i, side) + 4.6f), 0.12f), along, toRoad);
                    else if (kind == 3)
                    {
                        if (_rng.NextDouble() < 0.18) Shelter(P(i, side * (Wall(i, side) + 4.4f), 0.12f), along, toRoad);
                        else if (_rng.NextDouble() < 0.5) Sign(basePos + along * 1.5f, toRoad, _rng.Next(0, 4));
                    }
                }
            }
        }

        private bool SidewalkOk(int i, int side)
        {
            int j = _s.Wrap(i);
            float d = _s.DistanceOf(j);
            if (side < 0 && d > 0f && d < 620f) return false;          // pit lane & paddock
            Vector3 p = P(j, side * (Wall(j, side) + 3.5f));
            if (TrackBuilder.InHarbour(p.x, p.z + 6f)) return false;
            foreach (var r in _blocked) if (r.Contains(new Vector2(p.x, p.z))) return false;
            // keep clear of other parts of the circuit
            int near = _grid.Nearest(p, 40f, out float dist);
            if (near >= 0 && Mathf.Abs(_s.Delta(_s.DistanceOf(near), d)) > 60f && dist < Mathf.Max(_w.LeftWall[near], _w.RightWall[near]) + 6f) return false;
            return true;
        }

        private static void FixLastUp(MeshBuilder mb)
        {
            int q = mb.Normals.Count - 4;
            if (mb.Normals[q].y >= 0f) return;
            for (int k = q; k < q + 4; k++) mb.Normals[k] = -mb.Normals[k];
            int t = mb.Triangles.Count;
            for (int i = t - 6; i < t; i += 3) { int tmp = mb.Triangles[i + 1]; mb.Triangles[i + 1] = mb.Triangles[i + 2]; mb.Triangles[i + 2] = tmp; }
        }

        private void StreetLamp(Vector3 p, Vector3 toRoad)
        {
            Vector3 top = p + Vector3.up * 8.2f;
            _poleMb.AddCylinder(p, top, 0.09f, 8, false);
            _poleMb.AddCylinder(p, p + Vector3.up * 0.6f, 0.16f, 8, false);
            Vector3 armEnd = top + toRoad * 1.9f + Vector3.up * 0.15f;
            _poleMb.AddCylinder(top, armEnd, 0.05f, 6, false);
            Quaternion r = Quaternion.LookRotation(toRoad, Vector3.up);
            _poleMb.AddBox(armEnd + Vector3.down * 0.06f, new Vector3(0.38f, 0.12f, 0.75f), r);
            _lampMb.AddBox(armEnd + Vector3.down * 0.13f, new Vector3(0.3f, 0.02f, 0.62f), r);
        }

        private void Tree(Vector3 p)
        {
            float h = R(5.5f, 8f);
            _barkMb.AddCylinder(p, p + Vector3.up * h * 0.55f, 0.13f, 7, false);
            _kerbStoneMb.AddBox(p + Vector3.up * 0.06f, new Vector3(1.3f, 0.12f, 1.3f));   // tree pit surround
            float size = R(3.4f, 4.6f);
            Vector3 c = p + Vector3.up * (h * 0.55f + size * 0.35f);
            float rot0 = R(0f, 180f);
            for (int k = 0; k < 3; k++)
            {
                Quaternion q = Quaternion.Euler(0f, rot0 + k * 60f, 0f);
                Vector3 r = q * Vector3.right * size * 0.5f, u = Vector3.up * size * 0.5f;
                _foliageMb.AddQuad(c - r - u, c - r + u, c + r + u, c + r - u, new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0));
            }
            // horizontal layer for a fuller canopy from above
            Vector3 hr = Vector3.right * size * 0.42f, hf = Vector3.forward * size * 0.42f, hc = c + Vector3.up * 0.2f;
            _foliageMb.AddQuad(hc - hr - hf, hc - hr + hf, hc + hr + hf, hc + hr - hf, new Vector2(0.1f, 0.2f), new Vector2(0.1f, 0.8f), new Vector2(0.9f, 0.8f), new Vector2(0.9f, 0.2f));
        }

        private void Bench(Vector3 p, Vector3 along, Vector3 toRoad)
        {
            Quaternion r = Quaternion.LookRotation(toRoad, Vector3.up);
            _woodMb.AddBox(p + Vector3.up * 0.45f, new Vector3(1.8f, 0.06f, 0.45f), r);
            _woodMb.AddBox(p + Vector3.up * 0.75f - toRoad * 0.22f, new Vector3(1.8f, 0.35f, 0.05f), r);
            for (int s = -1; s <= 1; s += 2) _poleMb.AddBox(p + along * (s * 0.8f) + Vector3.up * 0.22f, new Vector3(0.06f, 0.44f, 0.45f), r);
            Vector3 bin = p + along * 1.6f;
            _poleMb.AddCylinder(bin, bin + Vector3.up * 0.9f, 0.25f, 10, true);
        }

        private void Shelter(Vector3 p, Vector3 along, Vector3 toRoad)
        {
            Quaternion r = Quaternion.LookRotation(toRoad, Vector3.up);
            for (int s = -1; s <= 1; s += 2) _poleMb.AddBox(p + along * (s * 1.9f) + Vector3.up * 1.25f, new Vector3(0.08f, 2.5f, 0.08f), r);
            _poleMb.AddBox(p + Vector3.up * 2.55f + toRoad * 0.2f, new Vector3(4.1f, 0.1f, 1.7f), r);
            _shelterMb.AddBox(p + Vector3.up * 1.3f - toRoad * 0.55f, new Vector3(3.8f, 2.2f, 0.03f), r);
            _woodMb.AddBox(p + Vector3.up * 0.45f - toRoad * 0.3f, new Vector3(2.5f, 0.06f, 0.35f), r);
        }

        private void Sign(Vector3 p, Vector3 toRoad, int cell)
        {
            _poleMb.AddCylinder(p, p + Vector3.up * 2.6f, 0.035f, 6, false);
            Vector3 c = p + Vector3.up * 2.75f + toRoad * 0.05f;
            Vector3 r = Vector3.Cross(toRoad, Vector3.up).normalized * 0.32f, u = Vector3.up * 0.32f;   // u runs left→right as seen from the road
            Vector2 uv0 = new Vector2(cell / 4f, 0f), uv1 = new Vector2((cell + 1) / 4f, 1f);
            _signsMb.AddQuad(c - r - u, c - r + u, c + r + u, c + r - u, new Vector2(uv0.x, uv0.y), new Vector2(uv0.x, uv1.y), new Vector2(uv1.x, uv1.y), new Vector2(uv1.x, uv0.y));
        }
    }
}

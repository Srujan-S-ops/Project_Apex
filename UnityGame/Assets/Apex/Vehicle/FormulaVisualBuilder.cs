using System.Collections.Generic;
using Apex.Rendering;
using UnityEngine;

namespace Apex.Vehicle
{
    /// <summary>
    /// Procedural open-wheel single-seater (original design). Produces the same CarVisual hierarchy as the
    /// GT3 builder, so CarController, CarEffects and CarAudio work unchanged.
    /// Car space: +z forward, y up, ground at y = 0.
    /// </summary>
    public static class FormulaVisualBuilder
    {
        private const float ZTail = -2.27f, ZNose = 2.95f;
        private const int Ring = 40;

        // monocoque / nose / engine cover keys (z, value)
        private static readonly float[] WKeys = { -2.27f, 0.26f, -1.9f, 0.42f, -1.2f, 0.56f, -0.55f, 0.64f, 0.0f, 0.8f, 0.6f, 0.66f, 1.2f, 0.42f, 1.9f, 0.3f, 2.6f, 0.2f, 2.95f, 0.14f };
        private static readonly float[] BKeys = { -2.27f, 0.24f, -1.5f, 0.12f, 1.0f, 0.12f, 1.8f, 0.16f, 2.5f, 0.2f, 2.95f, 0.22f };
        private static readonly float[] TKeys = { -2.27f, 0.45f, -1.7f, 0.6f, -1.0f, 0.74f, -0.6f, 0.96f, -0.35f, 0.95f, -0.15f, 0.66f, 0.4f, 0.62f, 0.9f, 0.58f, 1.5f, 0.5f, 2.2f, 0.4f, 2.95f, 0.32f };
        // sidepods
        private static readonly float[] SpOuter = { -1.55f, 0.36f, -1.2f, 0.5f, -0.6f, 0.68f, 0.0f, 0.75f, 0.35f, 0.72f };
        private static readonly float[] SpTop = { -1.55f, 0.3f, -1.2f, 0.38f, -0.6f, 0.5f, 0.0f, 0.58f, 0.35f, 0.55f };

        public static CarVisual Build(Transform carRoot, CarSpec spec, CarVisualBuilder.Livery livery, bool highDetail)
        {
            var root = new GameObject("Visual").transform;
            root.SetParent(carRoot, false);
            var visual = root.gameObject.AddComponent<CarVisual>();

            Texture2D liveryTex = LiveryPainter.Paint(livery);
            var paint = MaterialLibrary.CarPaint(liveryTex, 0.5f, 0.62f);
            var carbon = MaterialLibrary.CarPaint(MaterialLibrary.Tex("carbon"), 0.3f, 0.5f);
            carbon.SetTextureScale("_BaseMap", new Vector2(8f, 8f));
            carbon.SetFloat("_BumpScale", 0.05f);
            var dark = MaterialLibrary.Lit(new Color(0.012f, 0.012f, 0.014f), 0.1f, 0.35f);
            var titanium = MaterialLibrary.Lit(new Color(0.22f, 0.22f, 0.24f), 0.85f, 0.7f);
            var chrome = MaterialLibrary.Lit(new Color(0.86f, 0.86f, 0.88f), 1f, 0.95f);
            var rubber = MaterialLibrary.Lit(new Color(0.06f, 0.06f, 0.065f), 0f, 0.3f, MaterialLibrary.Tex("tyre_sidewall"));
            var rim = MaterialLibrary.PBR("brushed", new Vector2(2f, 2f), new Color(0.12f, 0.12f, 0.13f), 1f, 0.6f);
            var caliperMat = MaterialLibrary.Lit(new Color(0.08f, 0.08f, 0.09f), 0.4f, 0.6f);
            var nutMat = MaterialLibrary.Lit(livery.Accent, 0.8f, 0.8f);
            var helmetMat = MaterialLibrary.Lit(livery.Helmet, 0.4f, 0.88f);
            var rainMat = MaterialLibrary.Emissive(new Color(0.5f, 0.02f, 0.02f), new Color(1f, 0.05f, 0.03f), 1.2f);
            var intakeMat = MaterialLibrary.Lit(new Color(0.02f, 0.02f, 0.02f), 0f, 0.25f, MaterialLibrary.Tex("mesh_grille"), new Vector2(4, 2));
            var decalMat = MaterialLibrary.Cutout(MaterialLibrary.Tex("sponsors"), Color.white, 0.4f, null, 0.7f);
            var digitsMat = MaterialLibrary.Cutout(MaterialLibrary.Tex("digits"), new Color(0.97f, 0.97f, 0.97f), 0.4f, null, 0.6f);
            visual.TailLightMaterial = rainMat;

            // ---------- monocoque, nose and engine cover
            var body = new MeshBuilder();
            var rings = new List<Vector3[]>();
            var vs = new List<float>();
            int st = highDetail ? 110 : 60;
            for (int i = 0; i <= st; i++)
            {
                float t = i / (float)st;
                float z = Mathf.Lerp(ZTail, ZNose, t);
                rings.Add(Section(z, Key(WKeys, z), Key(BKeys, z), Key(TKeys, z), 0f, 2.6f));
                vs.Add(t);
            }
            body.AddLoft(rings, true, 1f, vs);
            body.AddCap(rings[rings.Count - 1], Vector3.forward, false, new Vector2(0.22f, 0.98f));
            body.AddCap(rings[0], Vector3.back, true, new Vector2(0.22f, 0.05f));

            // ---------- sidepods (both sides)
            for (int s = -1; s <= 1; s += 2)
            {
                var sp = new List<Vector3[]>();
                var spv = new List<float>();
                for (int i = 0; i <= 30; i++)
                {
                    float t = i / 30f;
                    float z = Mathf.Lerp(-1.55f, 0.35f, t);
                    float outer = Key(SpOuter, z), inner = 0.28f;
                    float cx = s * (inner + outer) * 0.5f;
                    sp.Add(Section(z, outer - inner, 0.13f, Key(SpTop, z), cx, 3.2f));
                    spv.Add(Mathf.Lerp(0.25f, 0.62f, t));
                }
                body.AddLoft(sp, true, 1f, spv);
                body.AddCap(sp[0], Vector3.back, true, new Vector2(0.22f, 0.3f));
                // intake mouth
                var mouth = new MeshBuilder();
                Vector3 c = new Vector3(s * 0.52f, 0.36f, 0.355f);
                mouth.AddQuad(c + new Vector3(-0.2f, -0.13f, 0f), c + new Vector3(-0.2f, 0.13f, 0f), c + new Vector3(0.2f, 0.13f, 0f), c + new Vector3(0.2f, -0.13f, 0f));
                FixFacing(mouth, Vector3.forward);
                mouth.Build("SidepodIntake", root, intakeMat, false);
            }
            // shark fin
            var fin = new List<Vector3[]>();
            for (int i = 0; i <= 12; i++)
            {
                float z = Mathf.Lerp(-0.7f, -2.0f, i / 12f);
                float top = Mathf.Lerp(0.99f, 0.62f, i / 12f);
                fin.Add(new[] { new Vector3(0.006f, Key(TKeys, z) - 0.02f, z), new Vector3(0.006f, top, z), new Vector3(-0.006f, top, z), new Vector3(-0.006f, Key(TKeys, z) - 0.02f, z) });
            }
            body.AddLoft(fin, true);
            body.Build("Body", root, paint);

            // ---------- floor, diffuser, wings (carbon)
            var cb = new MeshBuilder();
            cb.AddBox(new Vector3(0f, 0.07f, -0.05f), new Vector3(1.56f, 0.025f, 2.6f));
            cb.AddBox(new Vector3(0f, 0.07f, -1.7f), new Vector3(1.1f, 0.025f, 0.7f));
            for (int s = -1; s <= 1; s += 2)
            {
                cb.AddBox(new Vector3(s * 0.78f, 0.12f, -0.05f), new Vector3(0.015f, 0.1f, 2.6f));                   // floor edge fence
                cb.AddBox(new Vector3(s * 0.45f, 0.36f, 1.05f), new Vector3(0.012f, 0.3f, 0.5f), Quaternion.Euler(0f, s * 6f, 0f)); // bargeboard
            }
            Quaternion diff = Quaternion.Euler(-28f, 0f, 0f);
            cb.AddBox(new Vector3(0f, 0.18f, -2.08f), new Vector3(1.0f, 0.02f, 0.5f), diff);
            for (int f = -2; f <= 2; f++) cb.AddBox(new Vector3(f * 0.24f, 0.16f, -2.1f), new Vector3(0.012f, 0.16f, 0.45f), diff);
            // front wing: main plane, two flaps, end plates, nose pylons
            cb.AddBox(new Vector3(0f, 0.1f, 2.82f), new Vector3(1.9f, 0.02f, 0.3f), Quaternion.Euler(-4f, 0f, 0f));
            for (int s = -1; s <= 1; s += 2)
            {
                cb.AddBox(new Vector3(s * 0.55f, 0.16f, 2.68f), new Vector3(0.78f, 0.016f, 0.2f), Quaternion.Euler(-20f, 0f, 0f));
                cb.AddBox(new Vector3(s * 0.6f, 0.22f, 2.58f), new Vector3(0.68f, 0.014f, 0.14f), Quaternion.Euler(-34f, 0f, 0f));
                cb.AddBox(new Vector3(s * 0.12f, 0.17f, 2.62f), new Vector3(0.02f, 0.12f, 0.22f));
            }
            // rear wing: beam wing, main plane, flap, pylon
            cb.AddBox(new Vector3(0f, 0.38f, -2.3f), new Vector3(0.9f, 0.02f, 0.15f), Quaternion.Euler(10f, 0f, 0f));
            cb.AddBox(new Vector3(0f, 0.82f, -2.42f), new Vector3(1.0f, 0.025f, 0.32f), Quaternion.Euler(8f, 0f, 0f));
            cb.AddBox(new Vector3(0f, 0.93f, -2.53f), new Vector3(1.0f, 0.02f, 0.17f), Quaternion.Euler(24f, 0f, 0f));
            cb.AddBox(new Vector3(0f, 0.6f, -2.32f), new Vector3(0.02f, 0.46f, 0.14f));
            // suspension wishbones + push rods
            float fz = spec.Wheelbase * 0.51f, rz = -spec.Wheelbase * 0.49f, half = spec.TrackWidth * 0.5f;
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 upF = new Vector3(s * (half - 0.14f), 0f, fz);
                cb.AddCylinder(new Vector3(s * 0.22f, 0.33f, fz - 0.25f), upF + Vector3.up * 0.26f, 0.017f, 6, false);
                cb.AddCylinder(new Vector3(s * 0.22f, 0.33f, fz + 0.2f), upF + Vector3.up * 0.26f, 0.017f, 6, false);
                cb.AddCylinder(new Vector3(s * 0.2f, 0.48f, fz - 0.2f), upF + Vector3.up * 0.5f, 0.015f, 6, false);
                cb.AddCylinder(new Vector3(s * 0.2f, 0.48f, fz + 0.15f), upF + Vector3.up * 0.5f, 0.015f, 6, false);
                cb.AddCylinder(new Vector3(s * 0.18f, 0.55f, fz - 0.05f), upF + Vector3.up * 0.24f, 0.014f, 6, false);
                Vector3 upR = new Vector3(s * (half - 0.18f), 0f, rz);
                cb.AddCylinder(new Vector3(s * 0.22f, 0.3f, rz + 0.3f), upR + Vector3.up * 0.24f, 0.017f, 6, false);
                cb.AddCylinder(new Vector3(s * 0.2f, 0.3f, rz - 0.2f), upR + Vector3.up * 0.24f, 0.017f, 6, false);
                cb.AddCylinder(new Vector3(s * 0.2f, 0.46f, rz + 0.25f), upR + Vector3.up * 0.5f, 0.015f, 6, false);
                cb.AddCylinder(new Vector3(s * 0.2f, 0.46f, rz - 0.15f), upR + Vector3.up * 0.5f, 0.015f, 6, false);
                // mirror stalk
                cb.AddCylinder(new Vector3(s * 0.6f, 0.56f, 0.28f), new Vector3(s * 0.66f, 0.7f, 0.32f), 0.012f, 6, false);
            }
            cb.Build("Carbon", root, carbon);

            // painted wing end plates
            var pe = new MeshBuilder();
            for (int s = -1; s <= 1; s += 2)
            {
                pe.AddBox(new Vector3(s * 0.96f, 0.17f, 2.74f), new Vector3(0.015f, 0.24f, 0.48f));
                pe.AddBox(new Vector3(s * 0.505f, 0.74f, -2.44f), new Vector3(0.015f, 0.56f, 0.6f));
                pe.AddBox(new Vector3(s * 0.68f, 0.72f, 0.33f), new Vector3(0.15f, 0.065f, 0.05f));   // mirror housing
            }
            pe.Build("EndPlates", root, paint);
            var mg = new MeshBuilder();
            for (int s = -1; s <= 1; s += 2) mg.AddBox(new Vector3(s * 0.68f, 0.72f, 0.304f), new Vector3(0.14f, 0.055f, 0.004f));
            mg.Build("MirrorGlass", root, chrome, false);

            // ---------- halo
            var halo = new MeshBuilder();
            halo.AddCylinder(new Vector3(0f, 0.6f, 0.62f), new Vector3(0f, 0.9f, 0.4f), 0.026f, 8, false);
            Vector3 prev = HaloPoint(-110f);
            for (int k = 1; k <= 22; k++)
            {
                Vector3 p = HaloPoint(Mathf.Lerp(-110f, 110f, k / 22f));
                halo.AddCylinder(prev, p, 0.026f, 8, false);
                prev = p;
            }
            halo.Build("Halo", root, titanium);

            // ---------- cockpit, driver, airbox, roll hoop
            var ck = new MeshBuilder();
            ck.AddSphere(new Vector3(0f, 0.6f, 0.2f), new Vector3(0.27f, 0.06f, 0.4f), 18);           // cockpit opening (dark)
            ck.AddBox(new Vector3(0f, 0.86f, -0.33f), new Vector3(0.2f, 0.14f, 0.02f));                // airbox inlet
            ck.AddBox(new Vector3(0f, 0.71f, -0.22f), new Vector3(0.36f, 0.14f, 0.1f));                // headrest
            ck.Build("Cockpit", root, dark, false);
            var helmet = new MeshBuilder();
            helmet.AddSphere(new Vector3(0f, 0.8f, -0.05f), new Vector3(0.13f, 0.14f, 0.15f), highDetail ? 18 : 12);
            helmet.Build("Helmet", root, helmetMat, false);
            var visor = new MeshBuilder();
            visor.AddSphere(new Vector3(0f, 0.82f, 0.02f), new Vector3(0.11f, 0.045f, 0.1f), 12);
            visor.Build("Visor", root, chrome, false);

            // rain light + exhaust
            var rl = new MeshBuilder();
            rl.AddBox(new Vector3(0f, 0.36f, -2.28f), new Vector3(0.09f, 0.11f, 0.02f));
            rl.Build("RainLight", root, rainMat, false);
            var ex = new MeshBuilder();
            ex.AddCylinder(new Vector3(0f, 0.5f, -2.0f), new Vector3(0f, 0.5f, -2.3f), 0.05f, 12, false);
            ex.Build("Exhaust", root, chrome);
            visual.ExhaustPoints.Add(new Vector3(0f, 0.5f, -2.32f));

            // ---------- decals: sidepod sponsors, nose number
            var dm = new MeshBuilder();
            int cell = Mathf.Abs(livery.Team.GetHashCode()) % 4;
            for (int s = -1; s <= 1; s += 2)
            {
                float x = s * 0.756f, y = 0.37f, z = -0.25f, w = 0.6f, h = 0.18f;
                float za = s > 0 ? z - w * 0.5f : z + w * 0.5f, zb = s > 0 ? z + w * 0.5f : z - w * 0.5f;
                dm.AddQuad(new Vector3(x, y - h * 0.5f, za), new Vector3(x, y + h * 0.5f, za), new Vector3(x, y + h * 0.5f, zb), new Vector3(x, y - h * 0.5f, zb),
                    CarVisualBuilder.Cell(cell, 0, 0), CarVisualBuilder.Cell(cell, 0, 1), CarVisualBuilder.Cell(cell, 1, 1), CarVisualBuilder.Cell(cell, 1, 0));
                FixLast(dm, new Vector3(s, 0f, 0f));
                // rear wing end plate logo
                float ex2 = s * 0.514f;
                dm.AddQuad(new Vector3(ex2, 0.7f, s > 0 ? -2.62f : -2.26f), new Vector3(ex2, 0.86f, s > 0 ? -2.62f : -2.26f), new Vector3(ex2, 0.86f, s > 0 ? -2.26f : -2.62f), new Vector3(ex2, 0.7f, s > 0 ? -2.26f : -2.62f),
                    CarVisualBuilder.Cell(7, 0, 0), CarVisualBuilder.Cell(7, 0, 1), CarVisualBuilder.Cell(7, 1, 1), CarVisualBuilder.Cell(7, 1, 0));
                FixLast(dm, new Vector3(s, 0f, 0f));
            }
            dm.Build("Sponsors", root, decalMat, false);
            var nm = new MeshBuilder();
            string num = livery.Number.ToString();
            float nz = 2.05f, ny = Key(TKeys, nz) + 0.004f, dw = 0.1f;
            for (int d = 0; d < num.Length; d++)
            {
                int dv = num[d] - '0';
                float x0 = (d - (num.Length - 1) * 0.5f) * dw;
                // lying on the nose, readable from the cockpit side looking forward
                nm.AddQuad(new Vector3(x0 - dw * 0.45f, ny, nz - 0.1f), new Vector3(x0 - dw * 0.45f, ny, nz + 0.1f), new Vector3(x0 + dw * 0.45f, ny, nz + 0.1f), new Vector3(x0 + dw * 0.45f, ny, nz - 0.1f),
                    new Vector2(dv / 10f, 0f), new Vector2(dv / 10f, 1f), new Vector2((dv + 1) / 10f, 1f), new Vector2((dv + 1) / 10f, 0f));
            }
            FixFacing(nm, Vector3.up);
            nm.Build("NoseNumber", root, digitsMat, false);

            // contact shadow
            var cs = new MeshBuilder();
            cs.AddQuad(new Vector3(-1.05f, 0.022f, -2.6f), new Vector3(-1.05f, 0.022f, 3.0f), new Vector3(1.05f, 0.022f, 3.0f), new Vector3(1.05f, 0.022f, -2.6f),
                new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0));
            FixFacing(cs, Vector3.up);
            cs.Build("ContactShadow", root, MaterialLibrary.UnlitTransparent(MaterialLibrary.Tex("pbr/car_shadow"), new Color(1f, 1f, 1f, 0.85f)), false);

            // ---------- wheels (exposed, wide slicks)
            Vector3[] wheelPos =
            {
                new Vector3(-half, spec.WheelRadius, fz), new Vector3(half, spec.WheelRadius, fz),
                new Vector3(-half - 0.02f, spec.WheelRadius, rz), new Vector3(half + 0.02f, spec.WheelRadius, rz)
            };
            for (int i = 0; i < 4; i++)
            {
                var discMat = MaterialLibrary.Emissive(new Color(0.2f, 0.2f, 0.21f), new Color(1f, 0.35f, 0.05f), 0f, MaterialLibrary.Tex("brake_disc"));
                visual.DiscMaterials[i] = discMat;
                float hw = i < 2 ? spec.FrontTyreHalfWidth : spec.RearTyreHalfWidth;
                var (knuckle, spinner) = CarVisualBuilder.BuildWheel(root, i, wheelPos[i], spec.WheelRadius, rubber, rim, discMat, caliperMat, nutMat, highDetail, hw);
                visual.Knuckles[i] = knuckle;
                visual.Spinners[i] = spinner;
            }
            return visual;
        }

        private static Vector3 HaloPoint(float deg)
        {
            float a = deg * Mathf.Deg2Rad;
            float y = Mathf.Lerp(0.92f, 0.66f, Mathf.Pow(Mathf.Abs(deg) / 110f, 2.2f));
            return new Vector3(0.3f * Mathf.Sin(a), y, 0.05f + 0.35f * Mathf.Cos(a));
        }

        private static float Key(float[] keys, float z)
        {
            int n = keys.Length / 2;
            if (z <= keys[0]) return keys[1];
            for (int i = 0; i < n - 1; i++)
            {
                float z0 = keys[i * 2], z1 = keys[i * 2 + 2];
                if (z <= z1)
                {
                    float t = (z - z0) / (z1 - z0);
                    t = t * t * (3f - 2f * t);
                    return Mathf.Lerp(keys[i * 2 + 1], keys[i * 2 + 3], t);
                }
            }
            return keys[keys.Length - 1];
        }

        /// <summary>Superellipse cross-section ring (counter-clockwise seen from the front, starting at the bottom).</summary>
        private static Vector3[] Section(float z, float width, float bottom, float top, float cx, float n)
        {
            var ring = new Vector3[Ring];
            float yc = (top + bottom) * 0.5f, hh = Mathf.Max(0.005f, (top - bottom) * 0.5f);
            float e = 2f / n;
            for (int k = 0; k < Ring; k++)
            {
                float th = -Mathf.PI * 0.5f + 2f * Mathf.PI * k / Ring;
                float c = Mathf.Cos(th), s = Mathf.Sin(th);
                float x = width * 0.5f * Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), e);
                float y = yc + hh * Mathf.Sign(s) * Mathf.Pow(Mathf.Abs(s), e);
                ring[k] = new Vector3(cx + x, y, z);
            }
            return ring;
        }

        private static void FixFacing(MeshBuilder mb, Vector3 desired)
        {
            for (int q = 0; q < mb.Normals.Count; q += 4)
            {
                if (Vector3.Dot(mb.Normals[q], desired) >= 0f) continue;
                for (int i = q; i < q + 4; i++) mb.Normals[i] = -mb.Normals[i];
                int ti = q / 4 * 6;
                for (int i = ti; i < ti + 6; i += 3) { int t = mb.Triangles[i + 1]; mb.Triangles[i + 1] = mb.Triangles[i + 2]; mb.Triangles[i + 2] = t; }
            }
        }

        private static void FixLast(MeshBuilder mb, Vector3 desired)
        {
            int n = mb.Normals.Count;
            if (Vector3.Dot(mb.Normals[n - 1], desired) >= 0f) return;
            for (int i = n - 4; i < n; i++) mb.Normals[i] = -mb.Normals[i];
            int t = mb.Triangles.Count;
            for (int i = t - 6; i < t; i += 3) { int tmp = mb.Triangles[i + 1]; mb.Triangles[i + 1] = mb.Triangles[i + 2]; mb.Triangles[i + 2] = tmp; }
        }
    }
}

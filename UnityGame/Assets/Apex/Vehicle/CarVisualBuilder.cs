using System.Collections.Generic;
using Apex.Rendering;
using UnityEngine;

namespace Apex.Vehicle
{
    /// <summary>
    /// Builds the GT3 car's visual model procedurally. Completely decoupled from physics: the result
    /// is a CarVisual hierarchy that can be replaced by an imported model without touching
    /// CarController.
    /// </summary>
    public static class CarVisualBuilder
    {
        // body extents (car space: +z forward, y up, ground at 0)
        private const float ZRear = -2.36f, ZFront = 2.27f;
        private const float FrontAxle = 1.356f, RearAxle = -1.303f;
        private const float ArchRadius = 0.44f, ArchTop = 0.745f;
        private const int Ring = 48;

        private static readonly float[] WidthKeys = { -2.36f, 1.84f, -2.2f, 1.97f, -1.75f, 2.04f, -1.3f, 2.07f, -0.85f, 2.0f, -0.4f, 1.9f, 0.3f, 1.9f, 0.9f, 1.98f, 1.36f, 2.03f, 1.85f, 1.96f, 2.12f, 1.8f, 2.27f, 1.56f };
        private static readonly float[] TopKeys = { -2.36f, 0.93f, -2.05f, 0.99f, -1.3f, 0.985f, -0.95f, 0.965f, 0.0f, 0.93f, 0.62f, 0.9f, 1.0f, 0.855f, 1.36f, 0.865f, 1.9f, 0.73f, 2.15f, 0.6f, 2.27f, 0.47f };
        private static readonly float[] HoodDipKeys = { -2.36f, 0.0f, 0.6f, 0.0f, 0.9f, 0.05f, 1.4f, 0.075f, 2.0f, 0.05f, 2.27f, 0.0f };

        public struct Livery
        {
            public Color Body, Accent, Trim, Helmet;
            public int Number;
            public string Team;
        }

        public static CarVisual Build(Transform carRoot, CarSpec spec, Livery livery, bool highDetail)
        {
            var root = new GameObject("Visual").transform;
            root.SetParent(carRoot, false);
            var visual = root.gameObject.AddComponent<CarVisual>();

            // ---------- materials
            Texture2D liveryTex = LiveryPainter.Paint(livery);
            // clear-coated metallic paint with flake normals (URP Complex Lit)
            var paint = MaterialLibrary.CarPaint(liveryTex, 0.55f, 0.6f);
            // clear-coated carbon weave
            var carbon = MaterialLibrary.CarPaint(MaterialLibrary.Tex("carbon"), 0.3f, 0.5f);
            carbon.SetTextureScale("_BaseMap", new Vector2(6f, 6f));
            carbon.SetFloat("_BumpScale", 0.05f);
            var glass = MaterialLibrary.Transparent(new Color(0.015f, 0.02f, 0.03f, 0.74f), 0.985f, 0.35f);
            var lensMat = MaterialLibrary.Transparent(new Color(0.85f, 0.9f, 1f, 0.16f), 1f, 0f);
            var tailGlass = MaterialLibrary.Transparent(new Color(0.45f, 0.01f, 0.02f, 0.6f), 0.98f, 0.1f);
            var seal = MaterialLibrary.Lit(new Color(0.015f, 0.015f, 0.017f), 0f, 0.35f);
            var trim = MaterialLibrary.Lit(livery.Trim, 0.1f, 0.45f);
            var rubber = MaterialLibrary.Lit(new Color(0.06f, 0.06f, 0.065f), 0f, 0.28f, MaterialLibrary.Tex("tyre_sidewall"));
            var rim = MaterialLibrary.PBR("brushed", new Vector2(2f, 2f), new Color(0.2f, 0.2f, 0.22f), 1f, 0.6f);
            var chrome = MaterialLibrary.Lit(new Color(0.86f, 0.86f, 0.88f), 1f, 0.95f);
            var caliperMat = MaterialLibrary.Lit(new Color(0.85f, 0.05f, 0.05f), 0.3f, 0.7f);
            var nutMat = MaterialLibrary.Lit(livery.Accent, 0.8f, 0.8f);
            var headMat = MaterialLibrary.Emissive(new Color(0.9f, 0.95f, 1f), new Color(0.85f, 0.92f, 1f), 4.5f);
            var drlMat = MaterialLibrary.Emissive(Color.white, new Color(0.9f, 0.95f, 1f), 7f);
            var tailMat = MaterialLibrary.Emissive(new Color(0.5f, 0.02f, 0.02f), new Color(1f, 0.05f, 0.03f), 1.2f);
            var intakeMat = MaterialLibrary.Lit(new Color(0.02f, 0.02f, 0.02f), 0f, 0.25f, MaterialLibrary.Tex("mesh_grille"), new Vector2(8, 3));
            var decalMat = MaterialLibrary.Cutout(MaterialLibrary.Tex("sponsors"), Color.white, 0.4f, null, 0.7f);
            var digitsMat = MaterialLibrary.Cutout(MaterialLibrary.Tex("digits"), new Color(0.05f, 0.05f, 0.05f), 0.4f, null, 0.6f);
            var helmetMat = MaterialLibrary.Lit(livery.Helmet, 0.4f, 0.85f);
            visual.TailLightMaterial = tailMat;
            visual.HeadLightMaterial = headMat;

            // ---------- body shell
            var body = new MeshBuilder();
            var rings = new List<Vector3[]>();
            var vCoords = new List<float>();
            int stations = highDetail ? 110 : 56;
            for (int i = 0; i <= stations; i++)
            {
                float t = i / (float)stations;
                float z = Mathf.Lerp(ZRear, ZFront, t);
                rings.Add(SectionRing(z));
                vCoords.Add(t);
            }
            body.AddLoft(rings, true, 1f, vCoords);
            body.AddCap(rings[rings.Count - 1], Vector3.forward, false, new Vector2(0.22f, 0.98f));
            body.AddCap(rings[0], Vector3.back, true, new Vector2(0.22f, 0.05f));
            body.Build("Body", root, paint);

            // ---------- greenhouse (glass) + roof
            BuildCabin(root, glass, paint, highDetail);
            BuildWindowSeals(root, seal);

            // ---------- aero, trim & details
            var cb = new MeshBuilder();   // carbon
            var tb = new MeshBuilder();   // dark trim
            // splitter + end plates
            cb.AddBox(new Vector3(0f, 0.085f, 2.27f), new Vector3(1.96f, 0.025f, 0.34f));
            cb.AddBox(new Vector3(-0.97f, 0.14f, 2.18f), new Vector3(0.02f, 0.12f, 0.3f));
            cb.AddBox(new Vector3(0.97f, 0.14f, 2.18f), new Vector3(0.02f, 0.12f, 0.3f));
            // canards
            for (int s = -1; s <= 1; s += 2)
            {
                cb.AddBox(new Vector3(s * 0.93f, 0.36f, 2.05f), new Vector3(0.16f, 0.012f, 0.18f), Quaternion.Euler(-8f, 0f, s * 12f));
                cb.AddBox(new Vector3(s * 0.95f, 0.45f, 1.98f), new Vector3(0.12f, 0.012f, 0.14f), Quaternion.Euler(-10f, 0f, s * 14f));
            }
            // side skirts
            for (int s = -1; s <= 1; s += 2)
                cb.AddBox(new Vector3(s * 0.96f, 0.16f, 0.03f), new Vector3(0.1f, 0.09f, 1.75f));
            // diffuser (angled plate + fins)
            Quaternion diffRot = Quaternion.Euler(-14f, 0f, 0f);
            cb.AddBox(new Vector3(0f, 0.2f, -2.1f), new Vector3(1.62f, 0.02f, 0.62f), diffRot);
            for (int f = -2; f <= 2; f++)
                cb.AddBox(new Vector3(f * 0.36f, 0.19f, -2.18f), new Vector3(0.015f, 0.17f, 0.52f), diffRot);
            // underfloor tub (fills the space between the wheels)
            tb.AddBox(new Vector3(0f, 0.3f, -0.02f), new Vector3(1.55f, 0.36f, 3.6f));
            tb.AddBox(new Vector3(0f, 0.44f, FrontAxle), new Vector3(1.0f, 0.5f, 0.9f));
            tb.AddBox(new Vector3(0f, 0.46f, RearAxle), new Vector3(1.05f, 0.55f, 1.0f));
            // rear wing: swan-neck supports, element, end plates, gurney
            for (int s = -1; s <= 1; s += 2)
            {
                cb.AddBox(new Vector3(s * 0.42f, 1.08f, -2.08f), new Vector3(0.025f, 0.28f, 0.12f), Quaternion.Euler(-28f, 0f, 0f));
                cb.AddBox(new Vector3(s * 0.42f, 1.24f, -2.18f), new Vector3(0.025f, 0.12f, 0.14f), Quaternion.Euler(30f, 0f, 0f));
                cb.AddBox(new Vector3(s * 0.99f, 1.24f, -2.26f), new Vector3(0.018f, 0.3f, 0.5f));
            }
            AddAirfoil(cb, new Vector3(0f, 1.27f, -2.25f), 1.96f, 0.34f, 9f);
            cb.AddBox(new Vector3(0f, 1.305f, -2.42f), new Vector3(1.96f, 0.035f, 0.01f));
            // mirrors
            for (int s = -1; s <= 1; s += 2)
            {
                tb.AddBox(new Vector3(s * 0.93f, 0.96f, 0.48f), new Vector3(0.12f, 0.025f, 0.05f), Quaternion.Euler(0, 0, s * -15f));
                var mb = new MeshBuilder();
                mb.AddSphere(new Vector3(s * 1.02f, 1.0f, 0.46f), new Vector3(0.1f, 0.055f, 0.075f), 14);
                mb.Build("Mirror", root, paint);
                var mg = new MeshBuilder();
                mg.AddBox(new Vector3(s * 1.02f, 1.0f, 0.392f), new Vector3(0.15f, 0.07f, 0.004f));
                mg.Build("MirrorGlass", root, chrome, false);
            }
            // roof scoop + antenna
            cb.AddBox(new Vector3(0f, CabinTop(-0.15f) + 0.035f, -0.15f), new Vector3(0.36f, 0.07f, 0.42f), Quaternion.Euler(3f, 0f, 0f));
            tb.AddBox(new Vector3(0f, CabinTop(-0.15f) + 0.055f, -0.36f), new Vector3(0.3f, 0.05f, 0.012f));
            tb.AddCylinder(new Vector3(0f, CabinTop(-0.62f), -0.62f), new Vector3(0f, CabinTop(-0.62f) + 0.22f, -0.7f), 0.005f, 5, false);
            // rear valance, reverse / fog light housing and number plate
            tb.AddBox(new Vector3(0f, 0.36f, -2.36f), new Vector3(1.55f, 0.12f, 0.06f));
            var plate = new MeshBuilder();
            plate.AddBox(new Vector3(0f, 0.5f, -2.385f), new Vector3(0.42f, 0.11f, 0.01f));
            plate.Build("NumberPlate", root, MaterialLibrary.Lit(new Color(0.95f, 0.95f, 0.93f), 0f, 0.5f), false);
            var plateText = new MeshBuilder();
            // seen from behind, +x is the viewer's right: u runs -x → +x and the quad faces -z
            plateText.AddQuad(new Vector3(-0.19f, 0.455f, -2.392f), new Vector3(-0.19f, 0.545f, -2.392f), new Vector3(0.19f, 0.545f, -2.392f), new Vector3(0.19f, 0.455f, -2.392f),
                Cell(7, 0, 0), Cell(7, 0, 1), Cell(7, 1, 1), Cell(7, 1, 0));
            plateText.Build("PlateBadge", root, decalMat, false);
            // front air curtains (dark vertical intakes at the bumper corners)
            for (int s = -1; s <= 1; s += 2)
                tb.AddBox(new Vector3(s * 0.84f, 0.3f, 2.17f), new Vector3(0.12f, 0.22f, 0.03f), Quaternion.Euler(0f, s * 32f, 0f));
            // hood vents, louvres, door intakes
            for (int s = -1; s <= 1; s += 2)
            {
                for (int k = 0; k < 4; k++)
                    tb.AddBox(new Vector3(s * 0.55f, SurfaceTop(1.42f - k * 0.08f, s * 0.55f) - 0.004f, 1.42f - k * 0.08f), new Vector3(0.38f, 0.02f, 0.03f), Quaternion.Euler(-8, 0, 0));
                float sx = SideX(-0.75f, 0.5f) - 0.01f;
                tb.AddBox(new Vector3(s * sx, 0.5f, -0.75f), new Vector3(0.03f, 0.18f, 0.42f));
            }
            // exhausts
            var exb = new MeshBuilder();
            for (int s = -1; s <= 1; s += 2)
            {
                exb.AddCylinder(new Vector3(s * 0.52f, 0.27f, -2.2f), new Vector3(s * 0.52f, 0.27f, -2.43f), 0.055f, 12, false);
                visual.ExhaustPoints.Add(new Vector3(s * 0.52f, 0.27f, -2.45f));
            }
            exb.Build("Exhaust", root, chrome);
            // tow hooks
            var hook = new MeshBuilder();
            hook.AddBox(new Vector3(0.45f, 0.22f, 2.4f), new Vector3(0.05f, 0.04f, 0.1f));
            hook.AddBox(new Vector3(0.3f, 0.35f, -2.38f), new Vector3(0.05f, 0.04f, 0.1f));
            hook.Build("TowHooks", root, caliperMat, false);
            cb.Build("Carbon", root, carbon);
            tb.Build("Trim", root, trim);

            // grille / intakes
            var ib = new MeshBuilder();
            ib.AddQuad(new Vector3(0.62f, 0.17f, 2.255f), new Vector3(0.62f, 0.37f, 2.235f), new Vector3(-0.62f, 0.37f, 2.235f), new Vector3(-0.62f, 0.17f, 2.255f),
                new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0));
            for (int s = -1; s <= 1; s += 2)
                ib.AddQuad(new Vector3(s * 0.92f, 0.17f, 2.12f), new Vector3(s * 0.92f, 0.33f, 2.12f), new Vector3(s * 0.7f, 0.33f, 2.2f), new Vector3(s * 0.7f, 0.17f, 2.2f),
                    new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0));
            FixWindingTowards(ib, Vector3.forward);
            ib.Build("Intakes", root, intakeMat, false);

            // lights
            // headlights: chrome reflector housing, projector lenses, LED DRL strip, clear lens cover
            var housing = new MeshBuilder();
            var hb = new MeshBuilder();
            var drl = new MeshBuilder();
            var lens = new MeshBuilder();
            for (int s = -1; s <= 1; s += 2)
            {
                float hy = SurfaceTop(1.95f, s * 0.66f) - 0.035f;
                Vector3 hc = new Vector3(s * 0.66f, hy, 1.95f);
                housing.AddSphere(hc, new Vector3(0.25f, 0.065f, 0.165f), 20);
                hb.AddSphere(hc + new Vector3(s * -0.06f, 0.035f, 0.07f), new Vector3(0.045f, 0.03f, 0.03f), 12);
                hb.AddSphere(hc + new Vector3(s * 0.06f, 0.03f, 0.05f), new Vector3(0.04f, 0.028f, 0.028f), 12);
                for (int k = 0; k < 7; k++)
                {
                    float t = k / 6f;
                    Vector3 dp = hc + new Vector3(s * Mathf.Lerp(-0.2f, 0.17f, t), 0.008f + Mathf.Sin(t * Mathf.PI) * 0.01f, Mathf.Lerp(0.1f, 0.02f, t * t) - 0.05f);
                    drl.AddBox(dp, new Vector3(0.055f, 0.012f, 0.02f), Quaternion.Euler(0f, s * Mathf.Lerp(-12f, 20f, t), 0f));
                }
                lens.AddSphere(hc + Vector3.up * 0.006f, new Vector3(0.262f, 0.074f, 0.175f), 20);
                visual.HeadlightPoints.Add(new Vector3(s * 0.66f, 0.68f, 2.1f));
            }
            housing.Build("HeadlightHousings", root, chrome, false);
            hb.Build("Headlights", root, headMat, false);
            drl.Build("DaytimeRunningLights", root, drlMat, false);
            lens.Build("HeadlightLenses", root, lensMat, false);

            // tail lights: segmented LED bar behind red glass
            var lb = new MeshBuilder();
            for (int k = 0; k < 22; k++)
                lb.AddBox(new Vector3(-0.735f + k * 0.07f, 0.82f, -2.352f), new Vector3(0.055f, 0.026f, 0.015f));
            for (int s = -1; s <= 1; s += 2)
                for (int k = 0; k < 4; k++)
                    lb.AddBox(new Vector3(s * (0.66f + k * 0.05f), 0.78f - k * 0.008f, -2.348f), new Vector3(0.04f, 0.07f, 0.02f));
            lb.AddBox(new Vector3(0f, 1.21f, -1.15f), new Vector3(0.25f, 0.025f, 0.02f)); // high-level brake light
            lb.AddBox(new Vector3(0f, 0.36f, -2.395f), new Vector3(0.1f, 0.05f, 0.01f));    // rear fog light
            lb.Build("TailLights", root, tailMat, false);
            var tg = new MeshBuilder();
            tg.AddBox(new Vector3(0f, 0.815f, -2.358f), new Vector3(1.6f, 0.05f, 0.025f));
            for (int s = -1; s <= 1; s += 2) tg.AddBox(new Vector3(s * 0.74f, 0.78f, -2.355f), new Vector3(0.24f, 0.095f, 0.03f));
            tg.Build("TailLightGlass", root, tailGlass, false);

            // soft contact shadow grounding the car
            var cs = new MeshBuilder();
            cs.AddQuad(new Vector3(-1.25f, 0.025f, -2.65f), new Vector3(-1.25f, 0.025f, 2.6f), new Vector3(1.25f, 0.025f, 2.6f), new Vector3(1.25f, 0.025f, -2.65f),
                new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0));
            FixWindingTowards(cs, Vector3.up);
            cs.Build("ContactShadow", root, MaterialLibrary.UnlitTransparent(MaterialLibrary.Tex("pbr/car_shadow"), new Color(1f, 1f, 1f, 0.9f)), false);

            // decals: door numbers, sponsors
            BuildDecals(root, decalMat, digitsMat, livery);

            // interior + driver
            BuildInterior(root, trim, helmetMat, carbon, highDetail);

            // ---------- wheels
            float frontZ = spec.Wheelbase * 0.51f, rearZ = -spec.Wheelbase * 0.49f;
            float half = spec.TrackWidth * 0.5f;
            Vector3[] wheelPos =
            {
                new Vector3(-half, spec.WheelRadius, frontZ), new Vector3(half, spec.WheelRadius, frontZ),
                new Vector3(-half - 0.02f, spec.WheelRadius, rearZ), new Vector3(half + 0.02f, spec.WheelRadius, rearZ)
            };
            for (int i = 0; i < 4; i++)
            {
                var discMat = MaterialLibrary.Emissive(new Color(0.42f, 0.42f, 0.44f), new Color(1f, 0.35f, 0.05f), 0f, MaterialLibrary.Tex("brake_disc"));
                visual.DiscMaterials[i] = discMat;
                var (knuckle, spinner) = BuildWheel(root, i, wheelPos[i], spec.WheelRadius, rubber, rim, discMat, caliperMat, nutMat, highDetail);
                visual.Knuckles[i] = knuckle;
                visual.Spinners[i] = spinner;
            }

            return visual;
        }

        // ------------------------------------------------------------------ body section maths

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

        private static float Width(float z) => Key(WidthKeys, z);
        private static float Top(float z) => Key(TopKeys, z);

        private static float Bottom(float z)
        {
            float b = 0.13f;
            if (z > 2.0f) b = Mathf.Lerp(0.13f, 0.16f, (z - 2.0f) / 0.27f);
            if (z < -1.85f) b = Mathf.Lerp(0.13f, 0.3f, (-1.85f - z) / 0.51f);
            float arch = 0f;
            foreach (float axle in new[] { FrontAxle, RearAxle })
            {
                float d = Mathf.Abs(z - axle) / ArchRadius;
                if (d < 1f) arch = Mathf.Max(arch, Mathf.Sqrt(1f - d * d));
            }
            return Mathf.Lerp(b, ArchTop, arch);
        }

        private const float NxExp = 2f / 4.5f, NyUp = 2f / 2.3f, NyLow = 2f / 7f;

        private static Vector3[] SectionRing(float z)
        {
            var ring = new Vector3[Ring];
            float w = Width(z), top = Top(z), bot = Bottom(z);
            float yc = (top + bot) * 0.5f, hh = (top - bot) * 0.5f;
            float dip = Key(HoodDipKeys, z);
            for (int k = 0; k < Ring; k++)
            {
                float th = -Mathf.PI * 0.5f + 2f * Mathf.PI * k / Ring;
                float c = Mathf.Cos(th), s = Mathf.Sin(th);
                float x = w * 0.5f * Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), NxExp);
                float y;
                if (s < 0f) y = yc - hh * Mathf.Pow(-s, NyLow);
                else
                {
                    y = yc + hh * Mathf.Pow(s, NyUp);
                    x *= 1f - 0.07f * s;                       // tumblehome
                    float xn = Mathf.Clamp01(Mathf.Abs(2f * x / w));
                    float bell = (1f - xn * xn);
                    y -= dip * bell * bell * Mathf.Pow(s, 4f);   // hood valley between fenders
                }
                ring[k] = new Vector3(x, y, z);
            }
            return ring;
        }

        /// <summary>Approximate body surface x at height y (side panels).</summary>
        private static float SideX(float z, float y)
        {
            float w = Width(z), top = Top(z), bot = Bottom(z);
            float yc = (top + bot) * 0.5f, hh = (top - bot) * 0.5f;
            float r = Mathf.Clamp((y - yc) / hh, -0.999f, 0.999f);
            float s = r >= 0 ? Mathf.Pow(r, 1f / NyUp) : -Mathf.Pow(-r, 1f / NyLow);
            float c = Mathf.Sqrt(Mathf.Max(0f, 1f - s * s));
            float x = w * 0.5f * Mathf.Pow(c, NxExp);
            if (s > 0) x *= 1f - 0.07f * s;
            return x;
        }

        /// <summary>Approximate body surface y on the top at lateral position x.</summary>
        private static float SurfaceTop(float z, float x)
        {
            float best = 0f, bestD = float.MaxValue;
            var ring = SectionRing(z);
            for (int k = Ring / 4; k <= Ring * 3 / 4; k++)
            {
                float d = Mathf.Abs(ring[k].x - x);
                if (d < bestD) { bestD = d; best = ring[k].y; }
            }
            return best;
        }

        // ------------------------------------------------------------------ parts

        private static void BuildCabin(Transform root, Material glass, Material paint, bool highDetail)
        {
            const float z0 = -1.18f, z1 = 0.66f;
            int stations = highDetail ? 36 : 22;
            int ringN = 20;
            var rings = new List<Vector3[]>();
            var roofRings = new List<Vector3[]>();
            var pillarRings = new List<Vector3[]>();
            for (int i = 0; i <= stations; i++)
            {
                float z = Mathf.Lerp(z0, z1, i / (float)stations);
                var r = CabinRing(z, ringN, 0f, out _);
                rings.Add(r);
            }
            var gb = new MeshBuilder();
            gb.AddLoft(rings, true);
            gb.Build("Glass", root, glass, false);

            // roof panel and B-pillar band in body paint
            var rb = new MeshBuilder();
            for (int i = 0; i <= 14; i++)
            {
                float z = Mathf.Lerp(-0.78f, 0.12f, i / 14f);
                roofRings.Add(CabinArc(z, 0.28f, 0.72f, 12, 0.006f));
            }
            rb.AddLoft(roofRings, false);
            for (int i = 0; i <= 3; i++)
            {
                float z = Mathf.Lerp(-0.52f, -0.42f, i / 3f);
                pillarRings.Add(CabinArc(z, 0.02f, 0.98f, 16, 0.008f));
            }
            rb.AddLoft(pillarRings, false);
            // A-pillars
            for (int s = 0; s < 2; s++)
            {
                var a = new List<Vector3[]>();
                float u0 = s == 0 ? 0.12f : 0.82f;
                for (int i = 0; i <= 10; i++)
                {
                    float z = Mathf.Lerp(0.0f, 0.62f, i / 10f);
                    a.Add(CabinArc(z, u0, u0 + 0.06f, 2, 0.008f));
                }
                rb.AddLoft(a, false);
            }
            rb.Build("Roof", root, paint);
        }

        /// <summary>Black rubber window seals along the base of the greenhouse.</summary>
        private static void BuildWindowSeals(Transform root, Material seal)
        {
            var mb = new MeshBuilder();
            for (int side = -1; side <= 1; side += 2)
            {
                var rings = new List<Vector3[]>();
                for (int i = 0; i <= 24; i++)
                {
                    float z = Mathf.Lerp(-1.1f, 0.6f, i / 24f);
                    float baseY = Top(z) - 0.03f;
                    float wBase = Width(z) * 0.86f * 0.93f;
                    float x = side * (wBase * 0.5f + 0.004f);
                    rings.Add(new[] { new Vector3(x, baseY - 0.025f, z), new Vector3(x * 0.995f, baseY + 0.035f, z) });
                }
                mb.AddLoft(rings, false);
                mb.AddLoft(rings, false, 1f, null, true);   // both facings: thin strip visible from any side
            }
            mb.Build("WindowSeals", root, seal, false);
        }

        private static float CabinTop(float z)
        {
            // windscreen rises from z=0.66 to z=-0.02, roof to -0.48, fastback rear window to -1.18
            if (z > -0.02f) return Mathf.Lerp(1.2f, 0.89f, Mathf.SmoothStep(0f, 1f, (z + 0.02f) / 0.68f));
            if (z > -0.5f) return 1.2f + 0.01f * Mathf.Sin((z + 0.02f) / 0.48f * Mathf.PI);
            return Mathf.Lerp(1.2f, 0.965f, Mathf.SmoothStep(0f, 1f, (-0.5f - z) / 0.68f));
        }

        private static Vector3[] CabinRing(float z, int n, float inflate, out float topY)
        {
            float baseY = Top(z) - 0.03f;
            topY = Mathf.Max(CabinTop(z), baseY + 0.001f);
            float wBase = Width(z) * 0.86f * (1f - 0.07f);
            float wTop = wBase * 0.68f;
            var ring = new Vector3[n];
            for (int k = 0; k < n; k++)
            {
                float th = -Mathf.PI * 0.5f + 2f * Mathf.PI * k / n;
                float c = Mathf.Cos(th), s = Mathf.Sin(th);
                float y01 = s < 0 ? 0f : Mathf.Pow(s, 0.8f);
                float w = Mathf.Lerp(wBase, wTop, y01);
                float x = w * 0.5f * Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), 0.55f);
                float y = Mathf.Lerp(baseY - 0.02f, topY, y01);
                ring[k] = new Vector3(x * (1f + inflate), y + inflate, z);
            }
            return ring;
        }

        /// <summary>Upper arc of the cabin between parameters u0..u1 (0 = right base, 1 = left base).</summary>
        private static Vector3[] CabinArc(float z, float u0, float u1, int n, float lift)
        {
            float baseY = Top(z) - 0.03f;
            float topY = Mathf.Max(CabinTop(z), baseY + 0.001f);
            float wBase = Width(z) * 0.86f * 0.93f;
            float wTop = wBase * 0.68f;
            var arc = new Vector3[n + 1];
            for (int k = 0; k <= n; k++)
            {
                float u = Mathf.Lerp(u0, u1, k / (float)n);
                float th = u * Mathf.PI;     // 0 → right base, π → left base
                float c = Mathf.Cos(th), s = Mathf.Sin(th);
                float y01 = Mathf.Pow(s, 0.8f);
                float w = Mathf.Lerp(wBase, wTop, y01);
                float x = w * 0.5f * Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), 0.55f);
                float y = Mathf.Lerp(baseY - 0.02f, topY, y01);
                Vector3 outward = new Vector3(x, (y - baseY) * 0.6f, 0f).normalized;
                arc[k] = new Vector3(x, y, z) + outward * lift;
            }
            return arc;
        }

        private static void AddAirfoil(MeshBuilder mb, Vector3 center, float span, float chord, float aoaDeg)
        {
            // NACA-like profile in (z,y), extruded along x
            int n = 14;
            var top = new List<Vector2>();
            for (int i = 0; i <= n; i++)
            {
                float x = i / (float)n;
                float t = 0.12f * 5f * (0.2969f * Mathf.Sqrt(x) - 0.126f * x - 0.3516f * x * x + 0.2843f * x * x * x - 0.1015f * x * x * x * x);
                float camber = 0.06f * (1f - (2f * x - 1f) * (2f * x - 1f));
                top.Add(new Vector2(x, camber + t));
            }
            var ring = new List<Vector2>();
            for (int i = n; i >= 0; i--) ring.Add(new Vector2(top[i].x, top[i].y));
            for (int i = 1; i < n; i++)
            {
                float x = i / (float)n;
                float t = top[i].y - (0.06f * (1f - (2f * x - 1f) * (2f * x - 1f)));
                ring.Add(new Vector2(x, 0.06f * (1f - (2f * x - 1f) * (2f * x - 1f)) - t));
            }
            Quaternion aoa = Quaternion.Euler(aoaDeg, 0f, 0f);
            var r0 = new Vector3[ring.Count];
            var r1 = new Vector3[ring.Count];
            for (int i = 0; i < ring.Count; i++)
            {
                // chord runs from leading edge (front, +z) to trailing edge (rear, -z)
                Vector3 p = new Vector3(0f, ring[i].y * chord, (0.5f - ring[i].x) * chord);
                p = aoa * p;
                r0[i] = center + new Vector3(span * 0.5f, 0f, 0f) + p;
                r1[i] = center + new Vector3(-span * 0.5f, 0f, 0f) + p;
            }
            mb.AddLoft(new List<Vector3[]> { r1, r0 }, true);
            mb.AddCap(r0, Vector3.right, false);
            mb.AddCap(r1, Vector3.left, true);
        }

        private static void BuildDecals(Transform root, Material sponsors, Material digits, Livery livery)
        {
            var db = new MeshBuilder();
            var nb = new MeshBuilder();
            var bg = new MeshBuilder();
            for (int s = -1; s <= 1; s += 2)
            {
                // number roundel on the door
                float z = -0.12f, y = 0.6f;
                float x = SideX(z, y) + 0.004f;
                SideQuad(bg, s, x, z, y, 0.44f, 0.36f, new Vector2(0, 0), new Vector2(1, 1));
                string num = livery.Number.ToString();
                float digitW = 0.15f;
                float start = -(num.Length - 1) * digitW * 0.5f;
                for (int d = 0; d < num.Length; d++)
                {
                    int dv = num[d] - '0';
                    SideQuad(nb, s, x + 0.002f, z + (start + d * digitW) * (s > 0 ? 1f : -1f), y, digitW, 0.28f,
                        new Vector2(dv / 10f, 0f), new Vector2((dv + 1) / 10f, 1f));
                }
                // sponsor decals (atlas 4x2)
                int a = Mathf.Abs(livery.Team.GetHashCode()) % 4;
                SideQuadAtlas(db, s, -1.05f, 0.62f, 0.62f, 0.2f, a);
                SideQuadAtlas(db, s, 0.95f, 0.5f, 0.5f, 0.16f, 4 + (a + 1) % 4);
                SideQuadAtlas(db, s, 0.45f, 0.38f, 0.55f, 0.13f, (a + 2) % 4);
            }
            // windscreen banner (atlas cell 7 = APEX)
            var wq = new MeshBuilder();
            Vector3 c = new Vector3(0f, CabinTop(0.08f) - 0.03f, 0.08f);
            float halfW = 0.45f;
            wq.AddQuad(new Vector3(halfW, c.y - 0.05f, c.z + 0.12f), new Vector3(halfW, c.y + 0.035f, c.z - 0.02f),
                new Vector3(-halfW, c.y + 0.035f, c.z - 0.02f), new Vector3(-halfW, c.y - 0.05f, c.z + 0.12f),
                Cell(7, 0, 0), Cell(7, 0, 1), Cell(7, 1, 1), Cell(7, 1, 0));
            FixWindingTowards(wq, new Vector3(0, 1, 1));
            wq.Build("WindscreenBanner", root, sponsors, false);
            // rear wing end-plate logos
            for (int s = -1; s <= 1; s += 2)
                SideQuadAtlas(db, s, -2.26f, 1.24f, 0.42f, 0.14f, 7, 0.995f + 0.012f);
            bg.Build("NumberPanels", root, MaterialLibrary.Lit(new Color(0.95f, 0.95f, 0.95f), 0f, 0.6f), false);
            nb.Build("Numbers", root, digits, false);
            db.Build("Sponsors", root, sponsors, false);
        }

        private static Vector2 Cell(int cell, int u, int v)
        {
            int cx = cell % 4, cy = 1 - cell / 4;
            return new Vector2((cx + u) / 4f, (cy + v) / 2f);
        }

        private static void SideQuadAtlas(MeshBuilder mb, int side, float z, float y, float w, float h, int cell, float fixedX = -1f)
        {
            float x = fixedX > 0 ? fixedX : SideX(z, y) + 0.005f;
            SideQuad(mb, side, x, z, y, w, h, Cell(cell, 0, 0), Cell(cell, 1, 1));
        }

        /// <summary>Vertical quad on the car side (side = +1 right, -1 left), readable from outside.</summary>
        private static void SideQuad(MeshBuilder mb, int side, float x, float z, float y, float w, float h, Vector2 uv0, Vector2 uv1)
        {
            float hx = side * x;
            // text reads front-to-back on the left side and back-to-front on the right side when viewed from outside
            float zA = side > 0 ? z - w * 0.5f : z + w * 0.5f;
            float zB = side > 0 ? z + w * 0.5f : z - w * 0.5f;
            var a = new Vector3(hx, y - h * 0.5f, zA);
            var b = new Vector3(hx, y + h * 0.5f, zA);
            var c = new Vector3(hx, y + h * 0.5f, zB);
            var d = new Vector3(hx, y - h * 0.5f, zB);
            mb.AddQuad(a, b, c, d, new Vector2(uv0.x, uv0.y), new Vector2(uv0.x, uv1.y), new Vector2(uv1.x, uv1.y), new Vector2(uv1.x, uv0.y));
            FixLastQuad(mb, new Vector3(side, 0f, 0f));
        }

        private static void FixLastQuad(MeshBuilder mb, Vector3 desired)
        {
            int n = mb.Normals.Count;
            if (Vector3.Dot(mb.Normals[n - 1], desired) >= 0f) return;
            for (int i = n - 4; i < n; i++) mb.Normals[i] = -mb.Normals[i];
            int t = mb.Triangles.Count;
            for (int i = t - 6; i < t; i += 3)
            {
                int tmp = mb.Triangles[i + 1];
                mb.Triangles[i + 1] = mb.Triangles[i + 2];
                mb.Triangles[i + 2] = tmp;
            }
        }

        private static void FixWindingTowards(MeshBuilder mb, Vector3 desired)
        {
            for (int q = 0; q < mb.Normals.Count; q += 4)
            {
                if (Vector3.Dot(mb.Normals[q], desired) >= 0f) continue;
                for (int i = q; i < q + 4; i++) mb.Normals[i] = -mb.Normals[i];
                int ti = q / 4 * 6;
                for (int i = ti; i < ti + 6; i += 3)
                {
                    int tmp = mb.Triangles[i + 1];
                    mb.Triangles[i + 1] = mb.Triangles[i + 2];
                    mb.Triangles[i + 2] = tmp;
                }
            }
        }

        private static void BuildInterior(Transform root, Material trim, Material helmet, Material carbon, bool highDetail)
        {
            var ib = new MeshBuilder();
            float dx = -0.33f;  // left-hand drive
            ib.AddBox(new Vector3(dx, 0.5f, -0.55f), new Vector3(0.5f, 0.12f, 0.5f));                     // seat base
            ib.AddBox(new Vector3(dx, 0.78f, -0.8f), new Vector3(0.52f, 0.6f, 0.1f), Quaternion.Euler(-12, 0, 0)); // seat back
            ib.AddBox(new Vector3(0f, 0.82f, 0.38f), new Vector3(1.5f, 0.12f, 0.35f), Quaternion.Euler(10, 0, 0)); // dashboard
            ib.AddBox(new Vector3(0f, 0.55f, -0.1f), new Vector3(0.22f, 0.25f, 0.9f));                     // tunnel
            ib.Build("Interior", root, trim, false);

            var cage = new MeshBuilder();
            float r = 0.022f;
            for (int s = -1; s <= 1; s += 2)
            {
                cage.AddCylinder(new Vector3(s * 0.58f, 0.6f, -0.9f), new Vector3(s * 0.5f, 1.13f, -0.48f), r, 6);
                cage.AddCylinder(new Vector3(s * 0.5f, 1.13f, -0.48f), new Vector3(s * 0.5f, 1.12f, 0.0f), r, 6);
                cage.AddCylinder(new Vector3(s * 0.5f, 1.12f, 0.0f), new Vector3(s * 0.62f, 0.86f, 0.55f), r, 6);
            }
            cage.AddCylinder(new Vector3(-0.5f, 1.13f, -0.48f), new Vector3(0.5f, 1.13f, -0.48f), r, 6);
            cage.AddCylinder(new Vector3(-0.58f, 0.6f, -0.9f), new Vector3(0.5f, 1.13f, -0.48f), r, 6);
            cage.Build("RollCage", root, MaterialLibrary.Lit(new Color(0.75f, 0.75f, 0.78f), 0.8f, 0.6f), false);

            // steering wheel (torus) + driver
            var sw = new MeshBuilder();
            var tor = new List<Vector2>();
            for (int i = 0; i <= 8; i++)
            {
                float a = i / 8f * Mathf.PI * 2f;
                tor.Add(new Vector2(Mathf.Cos(a) * 0.016f, 0.15f + Mathf.Sin(a) * 0.016f));
            }
            sw.AddLathe(new Vector3(dx, 0.86f, 0.12f), Quaternion.Euler(0f, 90f, 0f) * Quaternion.Euler(0f, 0f, 22f), tor, 18);
            sw.AddBox(new Vector3(dx, 0.86f, 0.12f), new Vector3(0.22f, 0.08f, 0.04f), Quaternion.Euler(-22, 0, 0));
            sw.Build("SteeringWheel", root, carbon, false);

            var drv = new MeshBuilder();
            drv.AddSphere(new Vector3(dx, 1.0f, -0.42f), new Vector3(0.14f, 0.15f, 0.16f), highDetail ? 18 : 12);
            drv.Build("Helmet", root, helmet, false);
            var suit = new MeshBuilder();
            suit.AddBox(new Vector3(dx, 0.74f, -0.52f), new Vector3(0.42f, 0.34f, 0.24f), Quaternion.Euler(-14, 0, 0));
            suit.AddBox(new Vector3(dx, 0.92f, -0.37f), new Vector3(0.2f, 0.065f, 0.05f));
            suit.Build("Driver", root, trim, false);
        }

        private static Material _chromeShared;
        private static Material ChromeShared => _chromeShared != null ? _chromeShared : (_chromeShared = MaterialLibrary.Lit(new Color(0.88f, 0.88f, 0.9f), 1f, 0.95f));

        private static (Transform knuckle, Transform spinner) BuildWheel(Transform root, int index, Vector3 pos, float radius,
            Material rubber, Material rimMat, Material discMat, Material caliperMat, Material nutMat, bool highDetail)
        {
            bool left = index == 0 || index == 2;
            var knuckle = new GameObject("Knuckle_" + index).transform;
            knuckle.SetParent(root, false);
            knuckle.localPosition = pos;
            var orient = new GameObject("Orient").transform;
            orient.SetParent(knuckle, false);
            orient.localRotation = left ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;

            // caliper: does not spin (front calipers sit at the rear of the disc)
            var cal = new MeshBuilder();
            float calAngle = index < 2 ? 125f : 55f;   // degrees around axle, measured from +y toward -z
            Quaternion calRot = Quaternion.Euler(calAngle * (left ? -1f : 1f) * -1f, 0f, 0f);
            cal.AddBox(calRot * new Vector3(-0.03f, 0.165f, 0f), new Vector3(0.07f, 0.075f, 0.17f), calRot);
            cal.Build("Caliper", orient, caliperMat, false);

            var spinner = new GameObject("Spinner").transform;
            spinner.SetParent(orient, false);
            int seg = highDetail ? 36 : 24;

            float w = 0.155f, rimR = 0.235f;
            var tyre = new MeshBuilder();
            tyre.AddLathe(Vector3.zero, Quaternion.identity, new List<Vector2>
            {
                new Vector2(-w, rimR), new Vector2(-w, radius - 0.045f), new Vector2(-w + 0.006f, radius - 0.02f),
                new Vector2(-w + 0.025f, radius - 0.004f), new Vector2(-w + 0.045f, radius),
                new Vector2(w - 0.045f, radius), new Vector2(w - 0.025f, radius - 0.004f),
                new Vector2(w - 0.006f, radius - 0.02f), new Vector2(w, radius - 0.045f), new Vector2(w, rimR)
            }, seg);
            tyre.Build("Tyre", spinner, rubber);

            var rim = new MeshBuilder();
            // barrel (inner surface visible) and outer lip
            rim.AddLathe(Vector3.zero, Quaternion.identity, new List<Vector2>
            {
                new Vector2(0.135f, rimR + 0.006f), new Vector2(0.125f, rimR - 0.012f), new Vector2(0.125f, rimR - 0.012f),
                new Vector2(-0.13f, rimR - 0.012f)
            }, seg);
            // spokes
            int spokes = 10;
            for (int s = 0; s < spokes; s++)
            {
                float a = s * 360f / spokes;
                Quaternion q = Quaternion.Euler(a, 0f, 0f);
                rim.AddBox(q * new Vector3(0.1f, 0.135f, 0f), new Vector3(0.025f, 0.19f, 0.03f), q * Quaternion.Euler(0f, 0f, -8f));
                rim.AddBox(q * Quaternion.Euler(14f, 0, 0) * new Vector3(0.095f, 0.16f, 0f), new Vector3(0.02f, 0.14f, 0.018f), q * Quaternion.Euler(14f, 0, 0) * Quaternion.Euler(0f, 0f, -8f));
            }
            rim.AddLathe(new Vector3(0.08f, 0f, 0f), Quaternion.identity, new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(0f, 0.06f), new Vector2(0f, 0.06f), new Vector2(0.035f, 0.055f), new Vector2(0.035f, 0.055f), new Vector2(0.035f, 0f)
            }, 16);
            rim.Build("Rim", spinner, rimMat);
            var lip = new MeshBuilder();
            lip.AddLathe(Vector3.zero, Quaternion.identity, new List<Vector2>
            {
                new Vector2(0.128f, rimR - 0.004f), new Vector2(0.138f, rimR + 0.004f), new Vector2(0.138f, rimR + 0.004f), new Vector2(0.141f, rimR + 0.012f)
            }, seg);
            lip.Build("RimLip", spinner, ChromeShared, false);

            var nut = new MeshBuilder();
            nut.AddLathe(new Vector3(0.115f, 0f, 0f), Quaternion.identity, new List<Vector2>
            {
                new Vector2(0f, 0.032f), new Vector2(0.03f, 0.032f), new Vector2(0.03f, 0.032f), new Vector2(0.035f, 0.02f), new Vector2(0.035f, 0f)
            }, 6);
            nut.Build("CentreLock", spinner, nutMat, false);

            var disc = new MeshBuilder();
            disc.AddLathe(new Vector3(-0.03f, 0f, 0f), Quaternion.identity, new List<Vector2>
            {
                new Vector2(-0.016f, 0.09f), new Vector2(-0.016f, 0.185f), new Vector2(-0.016f, 0.185f),
                new Vector2(0.016f, 0.185f), new Vector2(0.016f, 0.185f), new Vector2(0.016f, 0.09f), new Vector2(0.016f, 0.09f), new Vector2(0.04f, 0.06f), new Vector2(0.04f, 0f)
            }, seg);
            disc.Build("Disc", spinner, discMat, false);

            return (knuckle, spinner);
        }
    }
}

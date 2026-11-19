using UnityEngine;

namespace Apex.Vehicle
{
    /// <summary>
    /// Paints the body livery texture. Body UVs: u wraps around the cross-section
    /// (0 = bottom centre, 0.25 = right side, 0.5 = roof centre, 0.75 = left side), v runs rear→front.
    /// </summary>
    public static class LiveryPainter
    {
        public static Texture2D Paint(CarVisualBuilder.Livery livery)
        {
            const int W = 1024, H = 512;
            var px = new Color32[W * H];
            Color body = livery.Body, accent = livery.Accent;
            Color dark = new Color(0.06f, 0.06f, 0.07f);
            Color carbonLow = Color.Lerp(body, dark, 0.85f);
            for (int y = 0; y < H; y++)
            {
                float v = y / (float)(H - 1);          // rear → front
                for (int x = 0; x < W; x++)
                {
                    float u = x / (float)(W - 1);
                    Color c = body;
                    // distance from roof centre line (0 at top, 0.5 at bottom)
                    float du = Mathf.Abs(u - 0.5f);
                    // lower sill band in dark
                    if (du > 0.36f) c = carbonLow;
                    // twin centre stripes over the roof/hood/deck
                    if (du < 0.012f || (du > 0.02f && du < 0.028f)) c = accent;
                    // side swoosh (both sides; side param s: 0 at side centre line)
                    float side = Mathf.Min(Mathf.Abs(u - 0.25f), Mathf.Abs(u - 0.75f));
                    float sweep = (v - 0.25f) * 0.22f;            // rises toward the front
                    float band = Mathf.Abs(side - (0.06f - sweep));
                    if (v > 0.15f && v < 0.92f && band < 0.022f) c = accent;
                    if (v > 0.15f && v < 0.92f && band >= 0.022f && band < 0.027f) c = Color.Lerp(accent, dark, 0.6f);
                    // front nose tip accent
                    if (v > 0.965f && du > 0.18f) c = accent;
                    // panel shut lines (doors / bonnet)
                    if (Mathf.Abs(v - 0.43f) < 0.0016f || Mathf.Abs(v - 0.585f) < 0.0016f || Mathf.Abs(v - 0.69f) < 0.0016f)
                        c = Color.Lerp(c, Color.black, 0.65f);
                    px[y * W + x] = c;
                }
            }
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, true) { name = "Livery", wrapModeU = TextureWrapMode.Repeat, wrapModeV = TextureWrapMode.Clamp, anisoLevel = 4 };
            tex.SetPixels32(px);
            tex.Apply(true, true);
            return tex;
        }
    }
}

using TMPro;
using UnityEngine;

namespace Apex.UI
{
    /// <summary>PROJECT APEX visual identity: palette, typography and procedurally generated UI sprites.</summary>
    public static class UITheme
    {
        public static readonly Color Black = new Color(0.02f, 0.02f, 0.025f, 1f);
        public static readonly Color Charcoal = new Color(0.075f, 0.078f, 0.088f, 1f);
        public static readonly Color Graphite = new Color(0.13f, 0.135f, 0.15f, 1f);
        public static readonly Color Steel = new Color(0.42f, 0.44f, 0.48f, 1f);
        public static readonly Color Metal = new Color(0.72f, 0.74f, 0.78f, 1f);
        public static readonly Color White = new Color(0.97f, 0.97f, 0.98f, 1f);
        public static readonly Color Red = new Color(0.89f, 0.05f, 0.11f, 1f);
        public static readonly Color RedDeep = new Color(0.55f, 0.02f, 0.06f, 1f);
        public static readonly Color Purple = new Color(0.68f, 0.32f, 1f, 1f);
        public static readonly Color Green = new Color(0.2f, 0.92f, 0.42f, 1f);
        public static readonly Color Yellow = new Color(1f, 0.82f, 0.1f, 1f);
        public static readonly Color Blue = new Color(0.25f, 0.6f, 1f, 1f);
        public static readonly Color PanelBg = new Color(0.035f, 0.037f, 0.043f, 0.86f);
        public static readonly Color PanelBgSolid = new Color(0.05f, 0.052f, 0.06f, 0.97f);

        public static TMP_FontAsset Title { get; private set; }
        public static TMP_FontAsset Body { get; private set; }
        public static TMP_FontAsset Mono { get; private set; }

        public static Sprite Rounded, Chamfer, Circle, Ring, Glow, GradientV, GradientH, Skew, Solid, Stripes;

        private static bool _ready;

        public static void Initialise()
        {
            if (_ready) return;
            _ready = true;
            // TextMeshPro dynamic font assets built at runtime from installed system fonts (Bahnschrift ships
            // with Windows 10/11; the font file is never copied into the project).
            var symbols = SystemFont(("Segoe UI Symbol", "Regular"), ("Segoe UI", "Regular"));
            Title = SystemFont(("Bahnschrift SemiBold Condensed", "Regular"), ("Bahnschrift SemiBold", "Regular"), ("Bahnschrift", "Regular"), ("Segoe UI", "Bold"), ("Arial", "Bold"));
            Body = SystemFont(("Bahnschrift", "Regular"), ("Segoe UI", "Regular"), ("Arial", "Regular"));
            Mono = SystemFont(("Bahnschrift SemiBold", "Regular"), ("Bahnschrift", "Regular"), ("Consolas", "Regular"));
            var fallbackDefault = TMP_Settings.defaultFontAsset;
            foreach (var f in new[] { Title, Body, Mono })
            {
                if (f == null) continue;
                if (f.fallbackFontAssetTable == null) f.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset>();
                if (symbols != null && symbols != f) f.fallbackFontAssetTable.Add(symbols);
                if (fallbackDefault != null && fallbackDefault != f) f.fallbackFontAssetTable.Add(fallbackDefault);
            }
            if (Title == null) Title = fallbackDefault;
            if (Body == null) Body = Title;
            if (Mono == null) Mono = Body;

            Solid = MakeSprite(Tex(8, 8, (x, y) => 1f), 0);
            Rounded = MakeSprite(Tex(64, 64, (x, y) => RoundRect(x, y, 64, 64, 12f)), 20);
            Chamfer = MakeSprite(Tex(64, 64, (x, y) => ChamferShape(x, y, 64, 64, 18f)), 22);
            Circle = MakeSprite(Tex(128, 128, (x, y) => Mathf.Clamp01((63.5f - Vector2.Distance(new Vector2(x, y), new Vector2(63.5f, 63.5f))) * 1.2f)), 0);
            Ring = MakeSprite(Tex(512, 512, (x, y) =>
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(255.5f, 255.5f));
                return Mathf.Clamp01((255f - d) * 0.8f) * Mathf.Clamp01((d - 222f) * 0.8f);
            }), 0);
            Glow = MakeSprite(Tex(128, 128, (x, y) =>
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(63.5f, 63.5f)) / 63.5f;
                return Mathf.Pow(Mathf.Clamp01(1f - d), 2.2f);
            }), 0);
            GradientV = MakeSprite(Tex(4, 128, (x, y) => y / 127f), 0);
            GradientH = MakeSprite(Tex(128, 4, (x, y) => x / 127f), 0);
            Skew = MakeSprite(Tex(64, 64, (x, y) =>
            {
                float edge = 12f * (y / 63f);
                return Mathf.Clamp01(x - edge + 0.5f) * Mathf.Clamp01(63f - 12f + edge - x + 0.5f);
            }), 14);
            Stripes = MakeSprite(Tex(32, 32, (x, y) => ((x + y) / 8) % 2 == 0 ? 1f : 0.35f), 0, TextureWrapMode.Repeat);
        }

        private static TMP_FontAsset SystemFont(params (string family, string style)[] candidates)
        {
            foreach (var (family, style) in candidates)
            {
                try
                {
                    var fa = TMP_FontAsset.CreateFontAsset(family, style, 90);
                    if (fa != null)
                    {
                        fa.name = family + " " + style;
                        return fa;
                    }
                }
                catch (System.Exception e) { Debug.LogWarning($"[UITheme] font {family} {style} unavailable: {e.Message}"); }
            }
            return null;
        }

        private static float RoundRect(int x, int y, int w, int h, float r)
        {
            float cx = Mathf.Clamp(x + 0.5f, r, w - r), cy = Mathf.Clamp(y + 0.5f, r, h - r);
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
            return Mathf.Clamp01(r - d + 0.5f);
        }

        private static float ChamferShape(int x, int y, int w, int h, float c)
        {
            // cut top-right and bottom-left corners
            float a = (w - x - 0.5f) + (h - y - 0.5f) - c;
            float b = (x + 0.5f) + (y + 0.5f) - c;
            return Mathf.Clamp01(a + 0.5f) * Mathf.Clamp01(b + 0.5f);
        }

        private static Texture2D Tex(int w, int h, System.Func<int, int, float> alpha)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alpha(x, y)) * 255f));
            t.SetPixels32(px);
            t.Apply(false, true);
            return t;
        }

        private static Sprite MakeSprite(Texture2D t, int border, TextureWrapMode wrap = TextureWrapMode.Clamp)
        {
            t.wrapMode = wrap;
            return Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        }

        public static Sprite Icon(string name)
        {
            var tex = Resources.Load<Texture2D>("UI/icon_" + name);
            if (tex == null) return Circle;
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }

        public static string FormatTime(float seconds, bool showPlaceholder = true)
        {
            if (seconds <= 0f || float.IsNaN(seconds) || float.IsInfinity(seconds)) return showPlaceholder ? "--:--.---" : "";
            int m = (int)(seconds / 60f);
            float s = seconds - m * 60f;
            return $"{m}:{s:00.000}";
        }

        public static string FormatDelta(float d) => (d >= 0f ? "+" : "-") + Mathf.Abs(d).ToString("0.000");

        public static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);
    }
}

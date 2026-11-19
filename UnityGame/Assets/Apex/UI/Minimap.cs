using System.Collections.Generic;
using Apex.Race;
using Apex.Track;
using UnityEngine;
using UnityEngine.UI;

namespace Apex.UI
{
    /// <summary>Track map rendered from the live spline with real-time car markers.</summary>
    public sealed class Minimap : MonoBehaviour
    {
        public struct Mapping
        {
            public Vector2 Min;
            public float Scale;
            public Vector2 Offset;
            public Vector2 Size;
            public Vector2 ToPixel(Vector3 world) => new Vector2((world.x - Min.x) * Scale + Offset.x, (world.z - Min.y) * Scale + Offset.y);
        }

        private RaceManager _race;
        private Mapping _map;
        private RectTransform _rt;
        private readonly List<Image> _dots = new List<Image>();

        public static Texture2D RenderTrack(TrackSpline s, int w, int h, float radius, out Mapping map, float startLine = TrackLayout.StartLineDistance)
        {
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            foreach (var p in s.Pos)
            {
                min = Vector2.Min(min, new Vector2(p.x, p.z));
                max = Vector2.Max(max, new Vector2(p.x, p.z));
            }
            float pad = 14f;
            float scale = Mathf.Min((w - pad * 2) / (max.x - min.x), (h - pad * 2) / (max.y - min.y));
            var offset = new Vector2((w - (max.x - min.x) * scale) * 0.5f, (h - (max.y - min.y) * scale) * 0.5f);
            map = new Mapping { Min = min, Scale = scale, Offset = offset, Size = new Vector2(w, h) };

            var alpha = new float[w * h];
            var core = new float[w * h];
            var m = map;
            void Stamp(float[] buf, Vector2 c, float r)
            {
                int x0 = Mathf.Max(0, (int)(c.x - r - 2)), x1 = Mathf.Min(w - 1, (int)(c.x + r + 2));
                int y0 = Mathf.Max(0, (int)(c.y - r - 2)), y1 = Mathf.Min(h - 1, (int)(c.y + r + 2));
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c);
                        float a = Mathf.Clamp01(r - d + 0.5f);
                        int i = y * w + x;
                        if (a > buf[i]) buf[i] = a;
                    }
            }
            for (int i = 0; i < s.Count; i++)
            {
                Vector2 a = m.ToPixel(s.Pos[i]), b = m.ToPixel(s.Pos[(i + 1) % s.Count]);
                for (float t = 0f; t < 1f; t += 0.34f)
                {
                    Vector2 c = Vector2.Lerp(a, b, t);
                    Stamp(alpha, c, radius + 2f);
                    Stamp(core, c, radius);
                }
            }
            var px = new Color32[w * h];
            for (int i = 0; i < px.Length; i++)
            {
                float o = alpha[i], c = core[i];
                Color col = Color.Lerp(new Color(0.05f, 0.05f, 0.06f, o * 0.9f), new Color(0.92f, 0.93f, 0.95f, 1f), c);
                px[i] = col;
            }
            // start/finish marker
            Vector2 sl = m.ToPixel(s.PointAt(startLine, 0f));
            for (int y = -6; y <= 6; y++)
                for (int x = -2; x <= 2; x++)
                {
                    int xi = (int)sl.x + x, yi = (int)sl.y + y;
                    if (xi >= 0 && xi < w && yi >= 0 && yi < h) px[yi * w + xi] = new Color32(230, 13, 28, 255);
                }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }

        public static Minimap Create(Transform parent, RaceManager race, Vector2 anchor, Vector2 pivot, Vector2 pos, float size)
        {
            var panel = UIFactory.Panel(parent, "Minimap", anchor, anchor, pivot, pos, new Vector2(size + 30f, size + 30f), false);
            var rt = UIFactory.Rect("Map", panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
            var raw = rt.gameObject.AddComponent<RawImage>();
            raw.raycastTarget = false;
            var mm = rt.gameObject.AddComponent<Minimap>();
            mm._race = race;
            mm._rt = rt;
            raw.texture = RenderTrack(race.Track.Spline, (int)size * 2, (int)size * 2, 4f, out mm._map, race.Track.StartLine);
            foreach (var p in race.Field)
            {
                var dot = UIFactory.Image(rt, "Dot" + p.Id, UITheme.Circle, p.IsPlayer ? UITheme.Red : p.TeamColor, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero,
                    p.IsPlayer ? new Vector2(20f, 20f) : new Vector2(14f, 14f));
                if (p.IsPlayer)
                {
                    var ring = UIFactory.Image(dot.transform, "Ring", UITheme.Circle, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(26f, 26f));
                    ring.transform.SetAsFirstSibling();
                }
                mm._dots.Add(dot);
            }
            // keep player on top
            for (int i = 0; i < race.Field.Count; i++) if (race.Field[i].IsPlayer) mm._dots[i].transform.SetAsLastSibling();
            return mm;
        }

        private void LateUpdate()
        {
            if (_race == null) return;
            float k = _rt.rect.width / _map.Size.x;
            for (int i = 0; i < _race.Field.Count && i < _dots.Count; i++)
            {
                Vector2 px = _map.ToPixel(_race.Field[i].Car.transform.position) * k;
                _dots[i].rectTransform.anchoredPosition = px;
            }
        }
    }
}

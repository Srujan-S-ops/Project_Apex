using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Apex.UI
{
    /// <summary>Builds PROJECT APEX styled uGUI elements in code (no default Unity styling).</summary>
    public static class UIFactory
    {
        public static Canvas CreateCanvas(string name, int order, Transform parent = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            if (parent != null) go.transform.SetParent(parent, false);
            var c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = order;
            c.pixelPerfect = false;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();
            return c;
        }

        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            var module = es.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
            UnityEngine.Object.DontDestroyOnLoad(es);
        }

        public static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Stretch(string name, Transform parent, float inset = 0f)
        {
            var rt = Rect(name, parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-inset * 2f, -inset * 2f));
            return rt;
        }

        public static Image Image(Transform parent, string name, Sprite sprite, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size, bool raycast = false)
        {
            var rt = Rect(name, parent, anchorMin, anchorMax, pivot, pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = raycast;
            if (sprite != null && sprite.border != Vector4.zero) img.type = UnityEngine.UI.Image.Type.Sliced;
            return img;
        }

        public static Image FullImage(Transform parent, string name, Sprite sprite, Color color)
        {
            return Image(parent, name, sprite, color, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        }

        public static TMP_Text Text(Transform parent, string name, string text, int size, Color color, TMP_FontAsset font, TextAnchor align,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 sizeDelta, FontStyle style = FontStyle.Normal)
        {
            var rt = Rect(name, parent, anchorMin, anchorMax, pivot, pos, sizeDelta);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = Align(align);
            t.fontStyle = style == FontStyle.Bold ? FontStyles.Bold : (style == FontStyle.Italic ? FontStyles.Italic : (style == FontStyle.BoldAndItalic ? FontStyles.Bold | FontStyles.Italic : FontStyles.Normal));
            // titles: if only the Regular weight of Bahnschrift was found, use TMP's synthetic bold
            if (font != null && font == UITheme.Title && font.name == "Bahnschrift Regular") t.fontStyle |= FontStyles.Bold;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.richText = true;
            t.extraPadding = true;
            return t;
        }

        /// <summary>Text anchored at a point (pivot / anchor at the same spot).</summary>
        public static TMP_Text Label(Transform parent, string text, int size, Color color, TMP_FontAsset font, TextAnchor align, Vector2 anchor, Vector2 pos, float width = 600f)
        {
            Vector2 pivot = AlignPivot(align);
            return Text(parent, "Text", text, size, color, font, align, anchor, anchor, pivot, pos, new Vector2(width, size * 1.4f));
        }

        public static TextAlignmentOptions Align(TextAnchor a)
        {
            switch (a)
            {
                case TextAnchor.UpperLeft: return TextAlignmentOptions.TopLeft;
                case TextAnchor.UpperCenter: return TextAlignmentOptions.Top;
                case TextAnchor.UpperRight: return TextAlignmentOptions.TopRight;
                case TextAnchor.MiddleLeft: return TextAlignmentOptions.Left;
                case TextAnchor.MiddleCenter: return TextAlignmentOptions.Center;
                case TextAnchor.MiddleRight: return TextAlignmentOptions.Right;
                case TextAnchor.LowerLeft: return TextAlignmentOptions.BottomLeft;
                case TextAnchor.LowerCenter: return TextAlignmentOptions.Bottom;
                default: return TextAlignmentOptions.BottomRight;
            }
        }

        public static Vector2 AlignPivot(TextAnchor a)
        {
            switch (a)
            {
                case TextAnchor.UpperLeft: return new Vector2(0, 1);
                case TextAnchor.UpperCenter: return new Vector2(0.5f, 1);
                case TextAnchor.UpperRight: return new Vector2(1, 1);
                case TextAnchor.MiddleLeft: return new Vector2(0, 0.5f);
                case TextAnchor.MiddleCenter: return new Vector2(0.5f, 0.5f);
                case TextAnchor.MiddleRight: return new Vector2(1, 0.5f);
                case TextAnchor.LowerLeft: return new Vector2(0, 0);
                case TextAnchor.LowerCenter: return new Vector2(0.5f, 0);
                default: return new Vector2(1, 0);
            }
        }

        /// <summary>Dark glass panel with chamfered corners and a red top accent line.</summary>
        public static RectTransform Panel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size, bool accent = true, Color? bg = null)
        {
            var img = Image(parent, name, UITheme.Chamfer, bg ?? UITheme.PanelBg, anchorMin, anchorMax, pivot, pos, size, true);
            var rt = img.rectTransform;
            if (accent)
            {
                Image(rt, "Accent", UITheme.Solid, UITheme.Red, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(20f, -1f), new Vector2(90f, 3f));
                Image(rt, "Hairline", UITheme.Solid, new Color(1f, 1f, 1f, 0.08f), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0f, -1f), new Vector2(-40f, 1f));
            }
            return rt;
        }

        /// <summary>Main-menu style item: index, large label, subtitle, sliding red accent.</summary>
        public static Button MenuItem(Transform parent, string index, string label, string subtitle, Action onClick, float width = 620f)
        {
            var rt = Rect("Item_" + label, parent, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(width, 92f));
            var bg = rt.gameObject.AddComponent<Image>();
            bg.sprite = UITheme.GradientH;
            bg.color = new Color(1f, 1f, 1f, 0f);
            bg.raycastTarget = true;
            var btn = rt.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => onClick?.Invoke());

            var accent = Image(rt, "Accent", UITheme.Solid, UITheme.Red, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), Vector2.zero, new Vector2(6f, -14f));
            var content = Rect("Content", rt, Vector2.zero, Vector2.one, new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);
            Text(content, "Index", index, 22, UITheme.Steel, UITheme.Mono, TextAnchor.MiddleLeft, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(28f, 14f), new Vector2(60f, 30f));
            var l = Text(content, "Label", label, 46, UITheme.Metal, UITheme.Title, TextAnchor.MiddleLeft, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(78f, 10f), new Vector2(width - 90f, 56f));
            var s = Text(content, "Sub", subtitle, 19, UITheme.Metal, UITheme.Body, TextAnchor.MiddleLeft, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(80f, -26f), new Vector2(width - 90f, 26f));

            var fx = rt.gameObject.AddComponent<ApexButtonFx>();
            fx.Background = bg;
            fx.Accent = accent;
            fx.Label = l;
            fx.Sub = s;
            fx.Content = content;
            fx.IdleBg = new Color(1f, 1f, 1f, 0f);
            fx.ActiveBg = new Color(0.9f, 0.1f, 0.15f, 0.16f);
            return btn;
        }

        /// <summary>Compact call-to-action button.</summary>
        public static Button ActionButton(Transform parent, string label, Action onClick, bool primary, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var img = Image(parent, "Btn_" + label, UITheme.Skew, primary ? UITheme.Red : new Color(1f, 1f, 1f, 0.08f), anchor, anchor, pivot, pos, size, true);
            var btn = img.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => onClick?.Invoke());
            var content = Rect("Content", img.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var t = Text(content, "Label", label, 26, UITheme.White, UITheme.Title, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var accent = Image(img.transform, "Underline", UITheme.Solid, UITheme.White, new Vector2(0.15f, 0f), new Vector2(0.85f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 6f), new Vector2(0f, 2f));
            var fx = img.gameObject.AddComponent<ApexButtonFx>();
            fx.Background = img;
            fx.Label = t;
            fx.Accent = accent;
            fx.Content = content;
            fx.SlideX = 0f;
            fx.IdleText = primary ? UITheme.White : UITheme.Metal;
            fx.IdleBg = primary ? UITheme.RedDeep : new Color(1f, 1f, 1f, 0.08f);
            fx.ActiveBg = primary ? UITheme.Red : new Color(1f, 1f, 1f, 0.2f);
            return btn;
        }

        /// <summary>Settings / setup row: label on the left, "‹ value ›" selector on the right.</summary>
        public static ApexStepper Stepper(Transform parent, string label, string[] options, int index, Action<int> changed, float width = 820f, string hint = null)
        {
            var rt = Rect("Row_" + label, parent, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(width, 64f));
            var bg = rt.gameObject.AddComponent<Image>();
            bg.sprite = UITheme.Solid;
            bg.color = new Color(1f, 1f, 1f, 0.03f);
            var step = rt.gameObject.AddComponent<ApexStepper>();
            step.transition = Selectable.Transition.None;
            step.Options = options;
            step.Index = Mathf.Clamp(index, 0, options.Length - 1);
            step.Changed = changed;
            var accent = Image(rt, "Accent", UITheme.Solid, UITheme.Red, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), Vector2.zero, new Vector2(4f, -12f));
            var content = Rect("Content", rt, Vector2.zero, Vector2.one, new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);
            var l = Text(content, "Label", label, 26, UITheme.Metal, UITheme.Title, TextAnchor.MiddleLeft, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(26f, 0f), new Vector2(width * 0.5f, 40f));
            if (!string.IsNullOrEmpty(hint))
            {
                l.rectTransform.anchoredPosition = new Vector2(26f, 9f);
                Text(content, "Hint", hint, 16, UITheme.Steel, UITheme.Body, TextAnchor.MiddleLeft, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(27f, -16f), new Vector2(width * 0.5f, 24f));
            }
            var value = Text(rt, "Value", options.Length > 0 ? options[step.Index] : "", 26, UITheme.White, UITheme.Title, TextAnchor.MiddleCenter,
                new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-70f, 4f), new Vector2(240f, 40f));
            step.ValueText = value;
            var left = ArrowButton(rt, "‹", new Vector2(-322f, 0f), () => step.Step(-1));
            var right = ArrowButton(rt, "›", new Vector2(-28f, 0f), () => step.Step(1));
            // option pips
            int pipCount = Mathf.Min(options.Length, 10);
            step.Pips = new Image[pipCount];
            float pw = 220f / Mathf.Max(1, pipCount);
            for (int i = 0; i < pipCount; i++)
                step.Pips[i] = Image(rt, "Pip" + i, UITheme.Solid, Color.white, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(0, 0.5f),
                    new Vector2(-300f + i * pw, -21f), new Vector2(pw - 4f, 3f));
            var fx = rt.gameObject.AddComponent<ApexButtonFx>();
            fx.Background = bg;
            fx.Accent = accent;
            fx.Label = l;
            fx.Content = content;
            fx.SlideX = 6f;
            fx.IdleBg = new Color(1f, 1f, 1f, 0.03f);
            fx.ActiveBg = new Color(1f, 1f, 1f, 0.09f);
            step.Refresh();
            _ = left; _ = right;
            return step;
        }

        private static Button ArrowButton(Transform parent, string glyph, Vector2 pos, Action onClick)
        {
            var t = Text(parent, "Arrow" + glyph, glyph, 44, UITheme.Red, UITheme.Title, TextAnchor.MiddleCenter, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), pos, new Vector2(40f, 56f));
            t.raycastTarget = true;
            var b = t.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.ColorTint;
            var nav = b.navigation; nav.mode = Navigation.Mode.None; b.navigation = nav;
            b.targetGraphic = t;
            var colors = b.colors; colors.highlightedColor = new Color(1.4f, 1.4f, 1.4f); colors.pressedColor = Color.white; b.colors = colors;
            b.onClick.AddListener(() => onClick());
            return b;
        }

        /// <summary>Volume / sensitivity slider row.</summary>
        public static Slider SliderRow(Transform parent, string label, float value, float min, float max, Action<float> changed, Func<float, string> format, float width = 820f)
        {
            var rt = Rect("Slider_" + label, parent, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(width, 64f));
            var bg = rt.gameObject.AddComponent<Image>();
            bg.sprite = UITheme.Solid;
            bg.color = new Color(1f, 1f, 1f, 0.03f);
            var accent = Image(rt, "Accent", UITheme.Solid, UITheme.Red, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), Vector2.zero, new Vector2(4f, -12f));
            var content = Rect("Content", rt, Vector2.zero, Vector2.one, new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);
            var l = Text(content, "Label", label, 26, UITheme.Metal, UITheme.Title, TextAnchor.MiddleLeft, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(26f, 0f), new Vector2(width * 0.5f, 40f));
            var valueText = Text(rt, "Value", format(value), 24, UITheme.White, UITheme.Mono, TextAnchor.MiddleRight, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-24f, 0f), new Vector2(90f, 40f));

            var track = Image(rt, "Track", UITheme.Solid, new Color(1f, 1f, 1f, 0.12f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-130f, 0f), new Vector2(260f, 4f));
            var fillArea = Rect("FillArea", track.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var fill = Image(fillArea, "Fill", UITheme.Solid, UITheme.Red, Vector2.zero, new Vector2(0, 1), new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);
            var handleArea = Rect("HandleArea", track.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var handle = Image(handleArea, "Handle", UITheme.Skew, UITheme.White, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(16f, 26f));

            var slider = rt.gameObject.AddComponent<Slider>();
            slider.transition = Selectable.Transition.None;
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = value;
            slider.onValueChanged.AddListener(v => { valueText.text = format(v); changed(v); });
            var fx = rt.gameObject.AddComponent<ApexButtonFx>();
            fx.Background = bg;
            fx.Accent = accent;
            fx.Label = l;
            fx.Content = content;
            fx.SlideX = 6f;
            fx.IdleBg = new Color(1f, 1f, 1f, 0.03f);
            fx.ActiveBg = new Color(1f, 1f, 1f, 0.09f);
            return slider;
        }

        /// <summary>Vertical list container with spacing.</summary>
        public static RectTransform VerticalList(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, float spacing)
        {
            var rt = Rect(name, parent, anchor, anchor, pivot, pos, size);
            var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.childAlignment = TextAnchor.UpperLeft;
            v.childControlWidth = false;
            v.childControlHeight = false;
            v.childForceExpandHeight = false;
            v.childForceExpandWidth = false;
            return rt;
        }

        /// <summary>Section heading: small red tag + large title.</summary>
        public static void Heading(Transform parent, string tag, string title, Vector2 anchor, Vector2 pos)
        {
            var t = Label(parent, tag, 20, UITheme.Red, UITheme.Mono, TextAnchor.UpperLeft, anchor, pos, 800f);
            t.text = tag;
            Label(parent, title, 64, UITheme.White, UITheme.Title, TextAnchor.UpperLeft, anchor, pos + new Vector2(0f, -26f), 1200f);
            Image(parent, "HeadingRule", UITheme.Solid, UITheme.Red, anchor, anchor, new Vector2(0, 1), pos + new Vector2(0f, -110f), new Vector2(64f, 4f));
        }
    }
}

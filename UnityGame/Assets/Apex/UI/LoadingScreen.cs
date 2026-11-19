using System.Collections;
using Apex.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Apex.UI
{
    /// <summary>Persistent loading overlay with artwork, progress and racing tips.</summary>
    public sealed class LoadingScreen : MonoBehaviour
    {
        private CanvasGroup _group;
        private Image _fill;
        private Text _percent, _tip, _title, _subtitle;
        private RawImage _art;
        private float _progress, _shown;
        private float _tipTimer;
        private int _tipIndex;

        private static readonly string[] Tips =
        {
            "Brake in a straight line, then release the pedal progressively as you turn in.",
            "Traction control saves the rear tyres out of slow corners — try disabling it for faster exits.",
            "Aggressive drivers dive down the inside. Defensive drivers cover the inside line before braking zones.",
            "Kerbs are part of the track. All four wheels beyond the white line invalidates your lap.",
            "Purple sector = fastest of the session. Green = personal best. Yellow = slower.",
            "Press C to cycle cameras, B to look back and R to recover the car.",
            "The harbour straight is over 900 metres — get a clean exit from Porto for the overtake into Dogana.",
            "The Java engine analyses the circuit and directs AI tactics in real time.",
            "In Time Trial the delta compares your live lap against your personal best reference lap."
        };

        public static LoadingScreen Create(Transform parent)
        {
            var canvas = UIFactory.CreateCanvas("LoadingCanvas", 1000, parent);
            var ls = canvas.gameObject.AddComponent<LoadingScreen>();
            ls.Build(canvas.transform);
            return ls;
        }

        private void Build(Transform root)
        {
            _group = gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            UIFactory.FullImage(root, "Black", UITheme.Solid, UITheme.Black);
            var artRt = UIFactory.Stretch("Art", root);
            _art = artRt.gameObject.AddComponent<RawImage>();
            _art.texture = Resources.Load<Texture2D>("UI/loading_bg");
            _art.color = new Color(1f, 1f, 1f, 0.9f);
            _art.raycastTarget = false;
            var g = UIFactory.FullImage(root, "Shade", UITheme.GradientV, new Color(0f, 0f, 0f, 0.92f));
            g.rectTransform.localScale = new Vector3(1f, -1f, 1f);
            UIFactory.Image(root, "SideShade", UITheme.GradientH, new Color(0f, 0f, 0f, 0.85f), new Vector2(0, 0), new Vector2(0.6f, 1), new Vector2(0, 0.5f), Vector2.zero, Vector2.zero)
                .rectTransform.localScale = new Vector3(-1f, 1f, 1f);

            var logoTex = Resources.Load<Texture2D>("UI/logo");
            if (logoTex != null)
            {
                var lrt = UIFactory.Rect("Logo", root, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(90f, -70f), new Vector2(420f, 420f * logoTex.height / logoTex.width));
                var raw = lrt.gameObject.AddComponent<RawImage>();
                raw.texture = logoTex;
                raw.raycastTarget = false;
            }
            _subtitle = UIFactory.Label(root, "LOADING", 22, UITheme.Red, UITheme.Mono, TextAnchor.LowerLeft, new Vector2(0, 0), new Vector2(92f, 250f), 900f);
            _title = UIFactory.Label(root, TrackIds.HarbourGPName, 66, UITheme.White, UITheme.Title, TextAnchor.LowerLeft, new Vector2(0, 0), new Vector2(90f, 180f), 1400f);
            _tip = UIFactory.Label(root, "", 22, UITheme.Metal, UITheme.Body, TextAnchor.LowerLeft, new Vector2(0, 0), new Vector2(92f, 130f), 1300f);

            UIFactory.Image(root, "BarBg", UITheme.Solid, new Color(1f, 1f, 1f, 0.1f), new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0f, 80f), new Vector2(-180f, 4f));
            _fill = UIFactory.Image(root, "BarFill", UITheme.Solid, UITheme.Red, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(90f, 80f), new Vector2(0f, 4f));
            _percent = UIFactory.Label(root, "0%", 24, UITheme.White, UITheme.Mono, TextAnchor.LowerRight, new Vector2(1, 0), new Vector2(-90f, 92f), 200f);
            gameObject.SetActive(false);
        }

        public IEnumerator Show(bool race)
        {
            gameObject.SetActive(true);
            _group.blocksRaycasts = true;
            _progress = 0f; _shown = 0f;
            var cfg = RaceConfig.Current;
            _subtitle.text = race ? (cfg.mode == GameMode.QuickRace ? $"QUICK RACE  ·  {cfg.laps} LAPS  ·  {cfg.difficulty.ToString().ToUpper()}" : "TIME TRIAL") : "RETURNING TO PADDOCK";
            _title.text = race ? TrackIds.HarbourGPName : "PROJECT APEX";
            _tipIndex = Random.Range(0, Tips.Length);
            _tip.text = Tips[_tipIndex];
            yield return UITween.Fade(_group, 1f, 0.3f);
        }

        public void SetProgress(float p) => _progress = Mathf.Max(_progress, Mathf.Clamp01(p));

        public IEnumerator Hide()
        {
            _group.blocksRaycasts = false;
            yield return UITween.Fade(_group, 0f, 0.45f);
            gameObject.SetActive(false);
        }

        private void Update()
        {
            _shown = Mathf.MoveTowards(_shown, _progress, Time.unscaledDeltaTime * 1.5f);
            float w = (Screen.width > 0 ? 1920f * Screen.width / Screen.height / (1920f / 1080f) : 1920f) - 180f;
            _fill.rectTransform.sizeDelta = new Vector2(Mathf.Max(0f, w) * _shown, 4f);
            _percent.text = Mathf.RoundToInt(_shown * 100f) + "%";
            _tipTimer += Time.unscaledDeltaTime;
            if (_tipTimer > 4.5f)
            {
                _tipTimer = 0f;
                _tipIndex = (_tipIndex + 1) % Tips.Length;
                _tip.text = Tips[_tipIndex];
            }
            if (_art != null) _art.uvRect = new Rect(Time.unscaledTime * 0.004f % 0.05f, 0f, 0.95f, 1f);
        }
    }
}

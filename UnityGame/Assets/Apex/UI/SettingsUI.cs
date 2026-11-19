using System;
using System.Collections.Generic;
using System.Linq;
using Apex.Core;
using Apex.Rendering;
using UnityEngine;
using UnityEngine.UI;

namespace Apex.UI
{
    /// <summary>Tabbed settings (graphics, audio, controls, camera, gameplay) bound to GameSettings.</summary>
    public sealed class SettingsUI : UIScreen
    {
        private static readonly string[] Tabs = { "GRAPHICS", "AUDIO", "CONTROLS", "CAMERA", "GAMEPLAY" };
        private readonly List<RectTransform> _pages = new List<RectTransform>();
        private readonly List<Text> _tabLabels = new List<Text>();
        private readonly List<Image> _tabBars = new List<Image>();
        private int _tab;
        private GameSettings _s;
        private Resolution[] _resolutions;
        private readonly List<GameObject> _firstOfPage = new List<GameObject>();

        public static SettingsUI Create(Transform parent)
        {
            var rt = UIFactory.Stretch("Settings", parent);
            var ui = rt.gameObject.AddComponent<SettingsUI>();
            ui.Build();
            return ui;
        }

        private void Build()
        {
            _s = GameServices.Instance.Settings;
            UIFactory.FullImage(transform, "Dim", UITheme.Solid, new Color(0f, 0f, 0f, 0.72f)).raycastTarget = true;
            var panel = UIFactory.Panel(transform, "Panel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1180f, 820f), true, UITheme.PanelBgSolid);
            UIFactory.Heading(panel, "SETUP · PREFERENCES", "SETTINGS", new Vector2(0, 1), new Vector2(60f, -40f));

            // tabs
            for (int i = 0; i < Tabs.Length; i++)
            {
                int idx = i;
                var t = UIFactory.Text(panel, "Tab" + i, Tabs[i], 24, UITheme.Steel, UITheme.Title, TextAnchor.MiddleCenter,
                    new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(60f + i * 200f, -170f), new Vector2(190f, 44f));
                t.raycastTarget = true;
                var b = t.gameObject.AddComponent<Button>();
                b.transition = Selectable.Transition.None;
                var nav = b.navigation; nav.mode = Navigation.Mode.None; b.navigation = nav;
                b.onClick.AddListener(() => { SelectTab(idx); UISound.Tick(); });
                _tabLabels.Add(t);
                _tabBars.Add(UIFactory.Image(t.transform, "Bar", UITheme.Solid, UITheme.Red, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(0f, 3f)));
            }
            UIFactory.Label(panel, "Q / E  or  LB / RB  to switch tabs   ·   ESC  to apply", 16, UITheme.Steel, UITheme.Body, TextAnchor.LowerLeft, new Vector2(0, 0), new Vector2(60f, 58f), 600f);

            for (int i = 0; i < Tabs.Length; i++)
            {
                var page = UIFactory.VerticalList(panel, "Page" + i, new Vector2(0, 1), new Vector2(0, 1), new Vector2(60f, -240f), new Vector2(1060f, 480f), 8f);
                _pages.Add(page);
            }
            BuildGraphics(_pages[0]);
            BuildAudio(_pages[1]);
            BuildControls(_pages[2]);
            BuildCamera(_pages[3]);
            BuildGameplay(_pages[4]);

            UIFactory.ActionButton(panel, "APPLY & CLOSE", () => BackRequested?.Invoke(), true, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-60f, 40f), new Vector2(300f, 60f));
            InitScreen();
            BackRequested = () => Close();
            SelectTab(0);
        }

        public Action Closed;

        private void Close()
        {
            GameServices.Instance.ApplySettings();
            Hide();
            Closed?.Invoke();
        }

        private void SelectTab(int i)
        {
            _tab = i;
            for (int k = 0; k < _pages.Count; k++)
            {
                _pages[k].gameObject.SetActive(k == i);
                _tabLabels[k].color = k == i ? UITheme.White : UITheme.Steel;
                _tabBars[k].enabled = k == i;
            }
            FirstSelected = _firstOfPage.Count > i ? _firstOfPage[i] : null;
            if (Visible && FirstSelected != null && UnityEngine.EventSystems.EventSystem.current != null)
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(FirstSelected);
        }

        protected override void Update()
        {
            base.Update();
            if (!Visible) return;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            var gp = UnityEngine.InputSystem.Gamepad.current;
            if ((kb != null && kb.qKey.wasPressedThisFrame) || (gp != null && gp.leftShoulder.wasPressedThisFrame)) { SelectTab((_tab + Tabs.Length - 1) % Tabs.Length); UISound.Tick(); }
            if ((kb != null && kb.eKey.wasPressedThisFrame) || (gp != null && gp.rightShoulder.wasPressedThisFrame)) { SelectTab((_tab + 1) % Tabs.Length); UISound.Tick(); }
        }

        private static readonly string[] OnOff = { "OFF", "ON" };

        private void BuildGraphics(Transform p)
        {
            var q = UIFactory.Stepper(p, "QUALITY PRESET", GraphicsQuality.PresetNames, _s.qualityPreset, i => { _s.qualityPreset = i; GraphicsQuality.Apply(_s); }, 1060f, "Shadows, anti-aliasing, crowd and city density");
            _firstOfPage.Add(q.gameObject);
            _resolutions = Screen.resolutions.GroupBy(r => (r.width, r.height)).Select(g => g.Last()).Where(r => r.width >= 1024).ToArray();
            if (_resolutions.Length == 0) _resolutions = new[] { Screen.currentResolution };
            var names = _resolutions.Select(r => $"{r.width} × {r.height}").ToArray();
            int cur = Array.FindIndex(_resolutions, r => r.width == (_s.resolutionWidth > 0 ? _s.resolutionWidth : Screen.width) && r.height == (_s.resolutionHeight > 0 ? _s.resolutionHeight : Screen.height));
            if (cur < 0) cur = _resolutions.Length - 1;
            UIFactory.Stepper(p, "RESOLUTION", names, cur, i => { _s.resolutionWidth = _resolutions[i].width; _s.resolutionHeight = _resolutions[i].height; }, 1060f);
            UIFactory.Stepper(p, "DISPLAY MODE", new[] { "WINDOWED", "FULLSCREEN" }, _s.fullscreen ? 1 : 0, i => _s.fullscreen = i == 1, 1060f);
            UIFactory.Stepper(p, "V-SYNC", OnOff, _s.vsync ? 1 : 0, i => _s.vsync = i == 1, 1060f);
            UIFactory.Stepper(p, "POST PROCESSING", OnOff, _s.postProcessing ? 1 : 0, i =>
            {
                _s.postProcessing = i == 1;
                foreach (var v in FindObjectsByType<UnityEngine.Rendering.Volume>(FindObjectsSortMode.None)) v.enabled = _s.postProcessing;
            }, 1060f, "Bloom, tone mapping, colour grading, depth of field");
        }

        private void BuildAudio(Transform p)
        {
            Func<float, string> pct = v => Mathf.RoundToInt(v * 100f) + "%";
            var m = UIFactory.SliderRow(p, "MASTER", _s.masterVolume, 0f, 1f, v => { _s.masterVolume = v; AudioListener.volume = v; }, pct, 1060f);
            _firstOfPage.Add(m.gameObject);
            UIFactory.SliderRow(p, "ENGINE", _s.engineVolume, 0f, 1f, v => _s.engineVolume = v, pct, 1060f);
            UIFactory.SliderRow(p, "EFFECTS", _s.effectsVolume, 0f, 1f, v => _s.effectsVolume = v, pct, 1060f);
            UIFactory.SliderRow(p, "AMBIENCE", _s.ambienceVolume, 0f, 1f, v => _s.ambienceVolume = v, pct, 1060f);
            UIFactory.SliderRow(p, "MUSIC", _s.musicVolume, 0f, 1f, v => { _s.musicVolume = v; MenuMusic.SetVolume(v); }, pct, 1060f);
        }

        private void BuildControls(Transform p)
        {
            var s = UIFactory.SliderRow(p, "STEERING SENSITIVITY", _s.steeringSensitivity, 0.5f, 1.5f, v => _s.steeringSensitivity = v, v => v.ToString("0.00"), 1060f);
            _firstOfPage.Add(s.gameObject);
            UIFactory.SliderRow(p, "STEERING LINEARITY", _s.steeringLinearity, 1f, 2f, v => _s.steeringLinearity = v, v => v.ToString("0.00"), 1060f);
            UIFactory.Stepper(p, "STEERING ASSIST", OnOff, _s.steeringAssist ? 1 : 0, i => _s.steeringAssist = i == 1, 1060f, "Counter-steer help and yaw stability");
            var table = UIFactory.Rect("Bindings", p, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(1060f, 220f));
            UIFactory.Label(table, "ACTION", 16, UITheme.Red, UITheme.Mono, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(26f, -10f), 300f);
            UIFactory.Label(table, "KEYBOARD", 16, UITheme.Red, UITheme.Mono, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(420f, -10f), 300f);
            UIFactory.Label(table, "GAMEPAD", 16, UITheme.Red, UITheme.Mono, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(760f, -10f), 300f);
            for (int i = 0; i < ApexControls.BindingTable.Length; i++)
            {
                var b = ApexControls.BindingTable[i];
                float y = -36f - i * 22f;
                int col = i % 2;
                UIFactory.Label(table, b.action, 18, UITheme.Metal, UITheme.Body, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(26f, y), 380f);
                UIFactory.Label(table, b.keys, 18, UITheme.White, UITheme.Mono, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(420f, y), 320f);
                UIFactory.Label(table, b.pad, 18, UITheme.White, UITheme.Mono, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(760f, y), 280f);
                _ = col;
            }
        }

        private void BuildCamera(Transform p)
        {
            var c = UIFactory.Stepper(p, "DEFAULT CAMERA", new[] { "CHASE FAR", "CHASE NEAR", "HOOD", "BUMPER", "TV" }, (int)_s.defaultCamera, i => _s.defaultCamera = (CameraView)i, 1060f);
            _firstOfPage.Add(c.gameObject);
            UIFactory.SliderRow(p, "FIELD OF VIEW", _s.fieldOfView, 50f, 80f, v => _s.fieldOfView = v, v => Mathf.RoundToInt(v) + "°", 1060f);
            UIFactory.Stepper(p, "CAMERA SHAKE", OnOff, _s.cameraShake ? 1 : 0, i => _s.cameraShake = i == 1, 1060f);
        }

        private void BuildGameplay(Transform p)
        {
            var a = UIFactory.Stepper(p, "ABS", OnOff, _s.abs ? 1 : 0, i => _s.abs = i == 1, 1060f, "Anti-lock brakes");
            _firstOfPage.Add(a.gameObject);
            UIFactory.Stepper(p, "TRACTION CONTROL", OnOff, _s.tcs ? 1 : 0, i => _s.tcs = i == 1, 1060f, "Limits rear wheelspin");
            UIFactory.Stepper(p, "SPEED UNITS", new[] { "KM/H", "MPH" }, (int)_s.units, i => _s.units = (SpeedUnit)i, 1060f);
            UIFactory.Stepper(p, "MINIMAP", OnOff, _s.showMinimap ? 1 : 0, i => _s.showMinimap = i == 1, 1060f);
            UIFactory.Stepper(p, "LIVE LEADERBOARD", OnOff, _s.showLeaderboard ? 1 : 0, i => _s.showLeaderboard = i == 1, 1060f);
        }
    }
}

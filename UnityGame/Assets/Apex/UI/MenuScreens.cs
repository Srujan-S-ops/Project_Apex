using System;
using Apex.Core;
using Apex.Net;
using Apex.Track;
using Apex.Vehicle;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Apex.UI
{
    /// <summary>Main menu: branding, primary navigation, driver profile and engine link status.</summary>
    public sealed class MainMenuUI : UIScreen
    {
        public Action QuickRace, TimeTrial, Garage, Settings, Exit;
        private TMP_Text _status, _statusDot, _profile, _carLine;

        public static MainMenuUI Create(Transform parent)
        {
            var rt = UIFactory.Stretch("MainMenu", parent);
            var ui = rt.gameObject.AddComponent<MainMenuUI>();
            ui.Build();
            return ui;
        }

        private void Build()
        {
            var shade = UIFactory.Image(transform, "LeftShade", UITheme.GradientH, new Color(0f, 0f, 0f, 0.92f), new Vector2(0, 0), new Vector2(0.55f, 1), new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);
            shade.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
            shade.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            UIFactory.Image(transform, "BottomShade", UITheme.GradientV, new Color(0f, 0f, 0f, 0.7f), new Vector2(0, 0), new Vector2(1, 0.25f), new Vector2(0.5f, 0), Vector2.zero, Vector2.zero)
                .rectTransform.localScale = new Vector3(1f, -1f, 1f);

            var logoTex = Resources.Load<Texture2D>("UI/logo");
            var lrt = UIFactory.Rect("Logo", transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(96f, -70f), new Vector2(520f, logoTex != null ? 520f * logoTex.height / logoTex.width : 160f));
            var raw = lrt.gameObject.AddComponent<RawImage>();
            raw.texture = logoTex;
            raw.raycastTarget = false;
            UIFactory.Label(transform, "GT3 RACING SIMULATOR", 20, UITheme.Metal, UITheme.Mono, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(100f, -70f - lrt.sizeDelta.y - 6f), 600f);

            var list = UIFactory.VerticalList(transform, "Menu", new Vector2(0, 1f), new Vector2(0, 1f), new Vector2(70f, -340f), new Vector2(640f, 520f), 6f);
            var q = UIFactory.MenuItem(list, "01", "QUICK RACE", "Five-car sprint against AI rivals with a standing start", () => QuickRace?.Invoke());
            UIFactory.MenuItem(list, "02", "TIME TRIAL", "Hot laps against the clock and your reference lap", () => TimeTrial?.Invoke());
            UIFactory.MenuItem(list, "03", "GARAGE", "Inspect and select your GT3 machine", () => Garage?.Invoke());
            UIFactory.MenuItem(list, "04", "SETTINGS", "Graphics, audio, controls, camera and assists", () => Settings?.Invoke());
            UIFactory.MenuItem(list, "05", "EXIT", "Leave the paddock", () => Exit?.Invoke());
            FirstSelected = q.gameObject;

            // profile card
            var card = UIFactory.Panel(transform, "Profile", new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-70f, 110f), new Vector2(520f, 250f));
            UIFactory.Label(card, "DRIVER PROFILE", 18, UITheme.Red, UITheme.Mono, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(28f, -22f), 400f);
            _profile = UIFactory.Label(card, "", 22, UITheme.White, UITheme.Body, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(28f, -56f), 470f);
            _profile.lineSpacing = 25f;
            _carLine = UIFactory.Label(transform, "", 22, UITheme.Metal, UITheme.Mono, TextAnchor.UpperRight, new Vector2(1, 1), new Vector2(-70f, -70f), 900f);

            _statusDot = UIFactory.Label(transform, "●", 22, UITheme.Steel, UITheme.Body, TextAnchor.LowerLeft, new Vector2(0, 0), new Vector2(96f, 40f), 40f);
            _status = UIFactory.Label(transform, "", 18, UITheme.Metal, UITheme.Mono, TextAnchor.LowerLeft, new Vector2(0, 0), new Vector2(124f, 42f), 900f);
            UIFactory.Label(transform, "↑ ↓  SELECT     ENTER  CONFIRM     ESC  BACK", 16, UITheme.Steel, UITheme.Mono, TextAnchor.LowerRight, new Vector2(1, 0), new Vector2(-70f, 42f), 900f);
            InitScreen();
        }

        protected override void Update()
        {
            base.Update();
            if (!Visible) return;
            var svc = GameServices.Instance;
            var be = svc.Backend;
            switch (be.State)
            {
                case LinkState.Online:
                    _statusDot.color = UITheme.Green;
                    string ver = string.IsNullOrEmpty(be.ServerVersion) ? "" : $"  ·  v{be.ServerVersion}";
                    string lat = be.LatencyMs > 0f ? $"  ·  {be.LatencyMs:0} ms" : "";
                    _status.text = $"JAVA ENGINE ONLINE{ver}{lat}  ·  AI + PERSISTENCE ACTIVE";
                    break;
                case LinkState.Connecting:
                    _statusDot.color = UITheme.Yellow;
                    _status.text = $"CONNECTING TO JAVA ENGINE ({svc.Launcher.Status})";
                    break;
                default:
                    _statusDot.color = UITheme.Red;
                    _status.text = $"OFFLINE MODE  ·  LOCAL AI + LOCAL RECORDS  ({svc.Launcher.Status})";
                    break;
            }
            var spec = RaceConfig.Current.CarSpec;
            _carLine.text = $"{spec.Name}   ·   {TrackIds.HarbourGPName}";
            _profile.text = ProfileCache.Describe();
        }
    }

    /// <summary>Race configuration: track, mode, difficulty, opponents, length, car.</summary>
    public sealed class RaceSetupUI : UIScreen
    {
        public Action StartRace;
        public Action<CarClass> ClassChanged;
        private ApexStepper _mode, _diff, _opp, _laps, _class, _car;
        private TMP_Text _info, _pb;
        private RawImage _map;

        public static RaceSetupUI Create(Transform parent)
        {
            var rt = UIFactory.Stretch("RaceSetup", parent);
            var ui = rt.gameObject.AddComponent<RaceSetupUI>();
            ui.Build();
            return ui;
        }

        private void Build()
        {
            UIFactory.FullImage(transform, "Dim", UITheme.GradientH, new Color(0f, 0f, 0f, 0.9f)).rectTransform.localScale = new Vector3(-1f, 1f, 1f);
            UIFactory.Heading(transform, "EVENT · CONFIGURATION", "RACE SETUP", new Vector2(0, 1), new Vector2(96f, -70f));
            var cfg = RaceConfig.Current;
            var list = UIFactory.VerticalList(transform, "Options", new Vector2(0, 1), new Vector2(0, 1), new Vector2(96f, -230f), new Vector2(900f, 700f), 8f);
            UIFactory.Stepper(list, "TRACK", new[] { "PORTO VELA" }, 0, _ => { }, 900f, "Street circuit · 3.50 km · 9 named corners");
            _mode = UIFactory.Stepper(list, "GAME MODE", new[] { "QUICK RACE", "TIME TRIAL" }, (int)cfg.mode, i => { cfg.mode = (GameMode)i; RefreshInteractable(); RefreshInfo(); }, 900f);
            _diff = UIFactory.Stepper(list, "AI DIFFICULTY", new[] { "EASY", "MEDIUM", "HARD" }, (int)cfg.difficulty, i => { cfg.difficulty = (Difficulty)i; RefreshInfo(); }, 900f, "Pace, consistency and racecraft of the field");
            _opp = UIFactory.Stepper(list, "OPPONENTS", new[] { "1", "2", "3", "4" }, Mathf.Clamp(cfg.opponents, 1, 4) - 1, i => { cfg.opponents = i + 1; RefreshInfo(); }, 900f, "Aggressive · Defensive · Balanced personalities");
            _laps = UIFactory.Stepper(list, "RACE LENGTH", new[] { "1 LAP", "2 LAPS", "3 LAPS", "4 LAPS", "5 LAPS", "6 LAPS", "7 LAPS", "8 LAPS", "9 LAPS", "10 LAPS" }, Mathf.Clamp(cfg.laps, 1, 10) - 1, i => { cfg.laps = i + 1; RefreshInfo(); }, 900f);
            _class = UIFactory.Stepper(list, "CAR CLASS", ClassNames, (int)cfg.carClass, i =>
            {
                cfg.carClass = (CarClass)i;
                cfg.carIndex = Mathf.Clamp(cfg.carIndex, 0, CarSpec.RosterFor(cfg.carClass).Length - 1);
                RefreshCarNames(_car, cfg.carClass, cfg.carIndex);
                RefreshInfo();
                ClassChanged?.Invoke(cfg.carClass);
            }, 900f, "GT3 sports cars · Formula open-wheel single-seaters");
            var names = Array.ConvertAll(CarSpec.RosterFor(cfg.carClass), c => c.Name);
            _car = UIFactory.Stepper(list, "CAR", names, cfg.carIndex, i => { cfg.carIndex = i; RefreshInfo(); }, 900f);
            FirstSelected = _mode.gameObject;

            var card = UIFactory.Panel(transform, "TrackCard", new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-80f, -230f), new Vector2(720f, 620f));
            UIFactory.Label(card, "CIRCUIT", 18, UITheme.Red, UITheme.Mono, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(30f, -24f), 400f);
            UIFactory.Label(card, TrackIds.HarbourGPName, 34, UITheme.White, UITheme.Title, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(30f, -50f), 680f);
            var mapRt = UIFactory.Rect("Map", card, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0f, -110f), new Vector2(640f, 330f));
            _map = mapRt.gameObject.AddComponent<RawImage>();
            _map.raycastTarget = false;
            var spline = TrackSpline.FromLayout(TrackLayout.ControlPoints, TrackLayout.TargetLength);
            _map.texture = Minimap.RenderTrack(spline, 640, 330, 3f, out _);
            _info = UIFactory.Label(card, "", 20, UITheme.Metal, UITheme.Body, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(30f, -460f), 660f);
            _info.lineSpacing = 20f;
            _pb = UIFactory.Label(card, "", 22, UITheme.White, UITheme.Mono, TextAnchor.UpperRight, new Vector2(1, 1), new Vector2(-30f, -460f), 360f);

            UIFactory.ActionButton(transform, "START RACE", () => StartRace?.Invoke(), true, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-80f, 70f), new Vector2(360f, 72f));
            UIFactory.Label(transform, "← →  CHANGE     ENTER ON START  ·  ESC  BACK", 16, UITheme.Steel, UITheme.Mono, TextAnchor.LowerLeft, new Vector2(0, 0), new Vector2(96f, 42f), 900f);
            InitScreen();
        }

        protected override void OnShown()
        {
            var cfg = RaceConfig.Current;
            _mode.SetIndex((int)cfg.mode, false);
            _class.SetIndex((int)cfg.carClass, false);
            RefreshCarNames(_car, cfg.carClass, cfg.carIndex);
            RefreshInteractable();
            RefreshInfo();
        }

        private static string[] ClassNames => ClassUi.Names;
        private static void RefreshCarNames(ApexStepper s, CarClass c, int i) => ClassUi.SetCarNames(s, c, i);

        private void RefreshInteractable()
        {
            bool race = RaceConfig.Current.mode == GameMode.QuickRace;
            foreach (var s in new[] { _diff, _opp, _laps })
            {
                s.interactable = race;
                var cg = s.GetComponent<CanvasGroup>();
                if (cg == null) cg = s.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = race ? 1f : 0.35f;
            }
        }

        private void RefreshInfo()
        {
            var cfg = RaceConfig.Current;
            var spec = cfg.CarSpec;
            string mode = cfg.mode == GameMode.QuickRace
                ? $"Quick Race · {cfg.laps} lap{(cfg.laps > 1 ? "s" : "")} · {cfg.opponents} opponent{(cfg.opponents > 1 ? "s" : "")} · {cfg.difficulty}"
                : "Time Trial · unlimited laps · rolling start";
            _info.text = $"LENGTH  3.50 KM\nELEVATION  16 M\nCORNERS  {TrackLayout.Corners.Length} NAMED\n{mode}\n{spec.Name} · {spec.PowerHp:0} HP";
            float pb = GameServices.Instance.Records.BestLap(cfg.trackId, cfg.RecordKey, out _, out _);
            _pb.text = $"PERSONAL BEST\n<size=34>{UITheme.FormatTime(pb)}</size>";
        }
    }

    internal static class ClassUi
    {
        public static readonly string[] Names = { "GT3", "FORMULA" };

        public static void SetCarNames(ApexStepper stepper, CarClass c, int index)
        {
            stepper.Options = Array.ConvertAll(CarSpec.RosterFor(c), s => s.Name);
            stepper.SetIndex(index, false);
        }
    }

    /// <summary>Garage: showroom car, specifications and car selection.</summary>
    public sealed class GarageUI : UIScreen
    {
        public Action<int> CarChanged;
        public Action Confirmed;
        private ApexStepper _class, _car;
        private CarClass _carClass;
        private TMP_Text _maker, _name, _engine;
        private Image[] _bars;
        private TMP_Text[] _values;
        private int _index;

        public static GarageUI Create(Transform parent)
        {
            var rt = UIFactory.Stretch("Garage", parent);
            var ui = rt.gameObject.AddComponent<GarageUI>();
            ui.Build();
            return ui;
        }

        private void Build()
        {
            UIFactory.Image(transform, "Shade", UITheme.GradientH, new Color(0f, 0f, 0f, 0.85f), new Vector2(0, 0), new Vector2(0.42f, 1), new Vector2(0, 0.5f), Vector2.zero, Vector2.zero)
                .rectTransform.localScale = new Vector3(-1f, 1f, 1f);
            UIFactory.Heading(transform, "GARAGE · SHOWROOM", "YOUR MACHINE", new Vector2(0, 1), new Vector2(96f, -70f));
            _maker = UIFactory.Label(transform, "", 22, UITheme.Red, UITheme.Mono, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(96f, -230f), 700f);
            _name = UIFactory.Label(transform, "", 60, UITheme.White, UITheme.Title, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(94f, -258f), 900f);
            _engine = UIFactory.Label(transform, "", 22, UITheme.Metal, UITheme.Body, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(96f, -330f), 700f);

            string[] labels = { "POWER", "WEIGHT", "DOWNFORCE", "TOP SPEED", "POWER / WEIGHT" };
            _bars = new Image[labels.Length];
            _values = new TMP_Text[labels.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                float y = -400f - i * 66f;
                UIFactory.Label(transform, labels[i], 18, UITheme.Metal, UITheme.Mono, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(96f, y), 300f);
                _values[i] = UIFactory.Label(transform, "", 22, UITheme.White, UITheme.Mono, TextAnchor.UpperRight, new Vector2(0, 1), new Vector2(616f, y - 2f), 300f);
                UIFactory.Image(transform, "BarBg" + i, UITheme.Solid, new Color(1f, 1f, 1f, 0.1f), new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(96f, y - 32f), new Vector2(520f, 6f));
                _bars[i] = UIFactory.Image(transform, "Bar" + i, UITheme.Solid, UITheme.Red, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(96f, y - 32f), new Vector2(0f, 6f));
            }
            var list = UIFactory.VerticalList(transform, "Select", new Vector2(0, 0), new Vector2(0, 0), new Vector2(96f, 200f), new Vector2(640f, 70f), 0f);
            var classList = UIFactory.VerticalList(transform, "Class", new Vector2(0, 0), new Vector2(0, 0), new Vector2(96f, 280f), new Vector2(640f, 70f), 0f);
            _carClass = RaceConfig.Current.carClass;
            _class = UIFactory.Stepper(classList, "CLASS", ClassUi.Names, (int)_carClass, i =>
            {
                _carClass = (CarClass)i;
                _index = 0;
                ClassUi.SetCarNames(_car, _carClass, 0);
                // the showroom spawns from the configured class; selection is committed with SELECT CAR or restored on back
                RaceConfig.Current.carClass = _carClass;
                RefreshSpecs();
                CarChanged?.Invoke(0);
            }, 620f);
            var names = Array.ConvertAll(CarSpec.RosterFor(_carClass), c => c.Name);
            _car = UIFactory.Stepper(list, "CAR", names, RaceConfig.Current.carIndex, i => { _index = i; RefreshSpecs(); CarChanged?.Invoke(i); }, 620f);
            FirstSelected = _car.gameObject;
            UIFactory.ActionButton(transform, "SELECT CAR", () => { RaceConfig.Current.carClass = _carClass; RaceConfig.Current.carIndex = _index; _committedClass = _carClass; Confirmed?.Invoke(); }, true, new Vector2(0, 0), new Vector2(0, 0), new Vector2(96f, 100f), new Vector2(320f, 66f));
            UIFactory.Label(transform, "DRAG / RIGHT STICK  ORBIT     ← →  CHANGE CAR     ESC  BACK", 16, UITheme.Steel, UITheme.Mono, TextAnchor.LowerRight, new Vector2(1, 0), new Vector2(-70f, 42f), 1000f);
            InitScreen();
        }

        protected override void OnShown()
        {
            _index = RaceConfig.Current.carIndex;
            _carClass = _committedClass = RaceConfig.Current.carClass;
            _class.SetIndex((int)_carClass, false);
            ClassUi.SetCarNames(_car, _carClass, _index);
            RefreshSpecs();
        }

        private CarClass _committedClass;

        /// <summary>Called when leaving without SELECT CAR: restores the committed class.</summary>
        public void RevertClass() => RaceConfig.Current.carClass = _committedClass;

        private void RefreshSpecs()
        {
            var roster = CarSpec.RosterFor(_carClass);
            var s = roster[Mathf.Clamp(_index, 0, roster.Length - 1)];
            _maker.text = s.Maker;
            _name.text = s.Name;
            _engine.text = s.Engine + $"  ·  {s.GearRatios.Length}-SPEED SEQUENTIAL (AUTO)  ·  RWD";
            float hp = s.PowerHp, kg = s.MassKg, df = s.DownforceClA, top = s.TopSpeedKmh;
            // shared scale covering both classes so GT3 and Formula compare honestly
            float[] norm = { Mathf.InverseLerp(400f, 1100f, hp), Mathf.InverseLerp(1450f, 750f, kg), Mathf.InverseLerp(2f, 5.2f, df), Mathf.InverseLerp(220f, 360f, top), Mathf.InverseLerp(0.3f, 1.35f, hp / kg) };
            string[] vals = { $"{hp:0} HP", $"{kg:0} KG", $"{df * 0.5f * 1.225f * 69.4f * 69.4f / 9.81f:0} KG @250", $"{top:0} KM/H", $"{hp / kg * 1000f:0} HP/T" };
            for (int i = 0; i < _bars.Length; i++)
            {
                _bars[i].rectTransform.sizeDelta = new Vector2(520f * Mathf.Clamp(norm[i], 0.08f, 1f), 6f);
                _values[i].text = vals[i];
            }
        }
    }
}

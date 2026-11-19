using System.Collections.Generic;
using Apex.Core;
using Apex.Net;
using Apex.Race;
using Apex.Vehicle;
using UnityEngine;
using UnityEngine.UI;

namespace Apex.UI
{
    /// <summary>In-race HUD. Every element reads live data from RaceManager / CarController.</summary>
    public sealed class RaceHUD : MonoBehaviour
    {
        private RaceManager _race;
        private GameSettings _s;
        private CanvasGroup _group;

        // cluster
        private Image _rpmFill, _shiftFlash;
        private Text _gear, _speed, _unit, _rpmText;
        private Image[] _shiftLights;
        private Image _throttle, _brake;
        private Text _abs, _tcs;
        private Image _absBg, _tcsBg;

        // race info
        private Text _pos, _posOf, _lap, _raceTime, _posLabel;
        private Text _current, _last, _best, _pb;
        private Image[] _sectorBg;
        private Text[] _sectorText;
        private Text _delta, _deltaLabel;
        private RectTransform _tower;
        private readonly List<(Image bg, Image stripe, Text pos, Text code, Text gap)> _rows = new List<(Image, Image, Text, Text, Text)>();
        private Text _towerHeader;

        // centre
        private Image[] _lights;
        private RectTransform _lightsRoot;
        private Text _bigMessage;
        private CanvasGroup _bigGroup;
        private float _bigTimer;
        private Image _flag;
        private Text _flagText;
        private Text _warning;
        private Text _engineLink;
        private Minimap _minimap;

        private readonly SectorStatus[] _sectorStatus = new SectorStatus[3];
        private readonly float[] _sectorTimes = new float[3];
        private float _sectorFlash;

        public static RaceHUD Create(Transform canvas, RaceManager race)
        {
            var rt = UIFactory.Stretch("HUD", canvas);
            var hud = rt.gameObject.AddComponent<RaceHUD>();
            hud._race = race;
            hud._s = GameServices.Instance.Settings;
            hud.Build();
            return hud;
        }

        private void Build()
        {
            _group = gameObject.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            BuildCluster();
            BuildRaceInfo();
            BuildTiming();
            BuildTower();
            BuildCentre();
            if (_s.showMinimap) _minimap = Minimap.Create(transform, _race, new Vector2(0, 0), new Vector2(0, 0), new Vector2(40f, 40f), 300f);
            _engineLink = UIFactory.Label(transform, "", 14, UITheme.Steel, UITheme.Mono, TextAnchor.LowerLeft, new Vector2(0, 0), new Vector2(44f, 14f), 700f);

            _race.CountdownLight += n => { _lightsRoot.gameObject.SetActive(true); };
            _race.LightsOut += () => { ShowBig("GO!", UITheme.Green, 1.4f); };
            _race.Message += (m, c) => ShowBig(m, c, 2.6f);
            _race.SectorCompleted += OnSector;
            _race.LapCompleted += OnLap;
        }

        // ------------------------------------------------------------------ builders

        private void BuildCluster()
        {
            Vector2 a = new Vector2(1, 0);
            var root = UIFactory.Rect("Cluster", transform, a, a, new Vector2(1, 0), new Vector2(-50f, 40f), new Vector2(420f, 420f));
            UIFactory.Image(root, "Glow", UITheme.Glow, new Color(0f, 0f, 0f, 0.75f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520f, 520f));
            Image Arc(string n, Color c, float fill)
            {
                var img = UIFactory.Image(root, n, UITheme.Ring, c, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(360f, 360f));
                img.type = Image.Type.Filled;
                img.fillMethod = Image.FillMethod.Radial360;
                img.fillOrigin = (int)Image.Origin360.Bottom;
                img.fillClockwise = true;
                img.fillAmount = fill;
                img.rectTransform.localEulerAngles = new Vector3(0f, 0f, -45f);
                return img;
            }
            Arc("ArcBg", new Color(1f, 1f, 1f, 0.12f), 0.75f);
            var red = Arc("Redline", new Color(0.9f, 0.05f, 0.1f, 0.55f), 0.75f);
            red.fillAmount = 0.75f;
            // redline zone: show only the last 12% of the arc
            red.fillClockwise = false;
            red.fillOrigin = (int)Image.Origin360.Bottom;
            red.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
            red.fillAmount = 0.09f;
            _rpmFill = Arc("RpmFill", UITheme.White, 0f);
            // tick marks
            for (int i = 0; i <= 10; i++)
            {
                float ang = -135f + i * 27f;
                var tick = UIFactory.Image(root, "Tick" + i, UITheme.Solid, new Color(1f, 1f, 1f, 0.35f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(2f, i % 2 == 0 ? 16f : 9f));
                tick.rectTransform.localEulerAngles = new Vector3(0f, 0f, -ang);
                tick.rectTransform.anchoredPosition = Quaternion.Euler(0, 0, -ang) * new Vector3(0f, 150f, 0f);
            }
            _gear = UIFactory.Text(root, "Gear", "N", 150, UITheme.White, UITheme.Title, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 28f), new Vector2(200f, 160f));
            _speed = UIFactory.Text(root, "Speed", "0", 58, UITheme.White, UITheme.Mono, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -78f), new Vector2(220f, 64f));
            _unit = UIFactory.Text(root, "Unit", "KM/H", 18, UITheme.Metal, UITheme.Mono, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -116f), new Vector2(200f, 24f));
            _rpmText = UIFactory.Text(root, "Rpm", "", 16, UITheme.Steel, UITheme.Mono, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(200f, 24f));

            // shift lights
            _shiftLights = new Image[10];
            for (int i = 0; i < 10; i++)
            {
                _shiftLights[i] = UIFactory.Image(root, "Shift" + i, UITheme.Circle, new Color(1f, 1f, 1f, 0.1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                    new Vector2(-135f + i * 30f, 18f), new Vector2(20f, 20f));
            }
            _shiftFlash = UIFactory.Image(root, "ShiftFlash", UITheme.Glow, new Color(0.3f, 0.6f, 1f, 0f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, 18f), new Vector2(380f, 70f));

            // pedals
            UIFactory.Image(root, "ThrBg", UITheme.Solid, new Color(1f, 1f, 1f, 0.1f), new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(-38f, 70f), new Vector2(10f, 180f));
            UIFactory.Image(root, "BrkBg", UITheme.Solid, new Color(1f, 1f, 1f, 0.1f), new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(-56f, 70f), new Vector2(10f, 180f));
            _throttle = UIFactory.Image(root, "Thr", UITheme.Solid, UITheme.Green, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(-38f, 70f), new Vector2(10f, 0f));
            _brake = UIFactory.Image(root, "Brk", UITheme.Solid, UITheme.Red, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(-56f, 70f), new Vector2(10f, 0f));
            UIFactory.Text(root, "ThrL", "T", 14, UITheme.Steel, UITheme.Mono, TextAnchor.MiddleCenter, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0.5f, 1f), new Vector2(-33f, 66f), new Vector2(20f, 20f));
            UIFactory.Text(root, "BrkL", "B", 14, UITheme.Steel, UITheme.Mono, TextAnchor.MiddleCenter, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0.5f, 1f), new Vector2(-51f, 66f), new Vector2(20f, 20f));

            // assist pills
            _absBg = UIFactory.Image(root, "AbsBg", UITheme.Rounded, new Color(1f, 1f, 1f, 0.08f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-48f, 6f), new Vector2(80f, 30f));
            _abs = UIFactory.Text(_absBg.transform, "Abs", "ABS", 18, UITheme.Steel, UITheme.Mono, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            _tcsBg = UIFactory.Image(root, "TcsBg", UITheme.Rounded, new Color(1f, 1f, 1f, 0.08f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(48f, 6f), new Vector2(80f, 30f));
            _tcs = UIFactory.Text(_tcsBg.transform, "Tcs", "TCS", 18, UITheme.Steel, UITheme.Mono, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        }

        private void BuildRaceInfo()
        {
            var a = new Vector2(0, 1);
            var panel = UIFactory.Panel(transform, "RaceInfo", a, a, new Vector2(0, 1), new Vector2(40f, -36f), new Vector2(330f, 150f));
            _posLabel = UIFactory.Label(panel, "POS", 16, UITheme.Red, UITheme.Mono, TextAnchor.UpperLeft, a, new Vector2(24f, -16f), 100f);
            _pos = UIFactory.Label(panel, "P1", 84, UITheme.White, UITheme.Title, TextAnchor.UpperLeft, a, new Vector2(18f, -30f), 200f);
            _posOf = UIFactory.Label(panel, "/5", 30, UITheme.Steel, UITheme.Title, TextAnchor.UpperLeft, a, new Vector2(130f, -76f), 100f);
            UIFactory.Label(panel, "LAP", 16, UITheme.Red, UITheme.Mono, TextAnchor.UpperLeft, a, new Vector2(196f, -16f), 100f);
            _lap = UIFactory.Label(panel, "1/3", 40, UITheme.White, UITheme.Title, TextAnchor.UpperLeft, a, new Vector2(194f, -34f), 140f);
            UIFactory.Label(panel, "TIME", 16, UITheme.Red, UITheme.Mono, TextAnchor.UpperLeft, a, new Vector2(196f, -88f), 100f);
            _raceTime = UIFactory.Label(panel, "0:00.000", 24, UITheme.White, UITheme.Mono, TextAnchor.UpperLeft, a, new Vector2(196f, -108f), 160f);
        }

        private void BuildTiming()
        {
            var a = new Vector2(1, 1);
            var panel = UIFactory.Panel(transform, "Timing", a, a, new Vector2(1, 1), new Vector2(-40f, -36f), new Vector2(380f, 236f));
            UIFactory.Label(panel, "CURRENT LAP", 16, UITheme.Red, UITheme.Mono, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(24f, -16f), 200f);
            _current = UIFactory.Label(panel, "--:--.---", 46, UITheme.White, UITheme.Mono, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(22f, -34f), 340f);
            UIFactory.Label(panel, "LAST", 16, UITheme.Metal, UITheme.Mono, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(24f, -98f), 100f);
            _last = UIFactory.Label(panel, "--:--.---", 24, UITheme.White, UITheme.Mono, TextAnchor.UpperRight, new Vector2(1, 1), new Vector2(-24f, -94f), 200f);
            UIFactory.Label(panel, "BEST", 16, UITheme.Metal, UITheme.Mono, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(24f, -128f), 100f);
            _best = UIFactory.Label(panel, "--:--.---", 24, UITheme.White, UITheme.Mono, TextAnchor.UpperRight, new Vector2(1, 1), new Vector2(-24f, -124f), 200f);
            UIFactory.Label(panel, "PERSONAL BEST", 16, UITheme.Metal, UITheme.Mono, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(24f, -158f), 200f);
            _pb = UIFactory.Label(panel, "--:--.---", 24, UITheme.Purple, UITheme.Mono, TextAnchor.UpperRight, new Vector2(1, 1), new Vector2(-24f, -154f), 200f);
            _sectorBg = new Image[3];
            _sectorText = new Text[3];
            for (int i = 0; i < 3; i++)
            {
                _sectorBg[i] = UIFactory.Image(panel, "S" + i, UITheme.Solid, new Color(1f, 1f, 1f, 0.08f), new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(24f + i * 112f, 14f), new Vector2(106f, 32f));
                _sectorText[i] = UIFactory.Text(_sectorBg[i].transform, "T", $"S{i + 1}", 17, UITheme.White, UITheme.Mono, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            }
            // delta (top centre)
            var c = new Vector2(0.5f, 1);
            _deltaLabel = UIFactory.Label(transform, "DELTA TO PB", 15, UITheme.Metal, UITheme.Mono, TextAnchor.UpperCenter, c, new Vector2(0f, -36f), 300f);
            _delta = UIFactory.Label(transform, "", 40, UITheme.Green, UITheme.Mono, TextAnchor.UpperCenter, c, new Vector2(0f, -54f), 300f);
        }

        private void BuildTower()
        {
            var a = new Vector2(0, 1);
            _tower = UIFactory.Rect("Tower", transform, a, a, new Vector2(0, 1), new Vector2(40f, -200f), new Vector2(330f, 300f));
            _towerHeader = UIFactory.Label(_tower, "LIVE TIMING", 15, UITheme.Red, UITheme.Mono, TextAnchor.UpperLeft, a, new Vector2(4f, 0f), 320f);
            for (int i = 0; i < _race.Field.Count; i++)
            {
                float y = -24f - i * 40f;
                var bg = UIFactory.Image(_tower, "Row" + i, UITheme.Solid, UITheme.PanelBg, a, a, new Vector2(0, 1), new Vector2(0f, y), new Vector2(330f, 37f));
                var posBox = UIFactory.Image(bg.transform, "PosBox", UITheme.Solid, new Color(1f, 1f, 1f, 0.92f), new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), Vector2.zero, new Vector2(40f, 0f));
                var pos = UIFactory.Text(posBox.transform, "Pos", (i + 1).ToString(), 22, UITheme.Black, UITheme.Title, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                var stripe = UIFactory.Image(bg.transform, "Stripe", UITheme.Solid, Color.white, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(44f, 0f), new Vector2(5f, -10f));
                var code = UIFactory.Text(bg.transform, "Code", "", 22, UITheme.White, UITheme.Title, TextAnchor.MiddleLeft, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(58f, 0f), new Vector2(150f, 34f));
                var gap = UIFactory.Text(bg.transform, "Gap", "", 20, UITheme.Metal, UITheme.Mono, TextAnchor.MiddleRight, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-12f, 0f), new Vector2(140f, 34f));
                _rows.Add((bg, stripe, pos, code, gap));
            }
        }

        private void BuildCentre()
        {
            var c = new Vector2(0.5f, 1);
            _lightsRoot = UIFactory.Rect("Lights", transform, c, c, new Vector2(0.5f, 1), new Vector2(0f, -150f), new Vector2(520f, 120f));
            UIFactory.Image(_lightsRoot, "Housing", UITheme.Rounded, new Color(0.02f, 0.02f, 0.02f, 0.9f), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            _lights = new Image[5];
            for (int i = 0; i < 5; i++)
            {
                UIFactory.Image(_lightsRoot, "LBase" + i, UITheme.Circle, new Color(0.15f, 0.02f, 0.02f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-200f + i * 100f, 0f), new Vector2(76f, 76f));
                _lights[i] = UIFactory.Image(_lightsRoot, "L" + i, UITheme.Circle, UITheme.Red, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-200f + i * 100f, 0f), new Vector2(76f, 76f));
                UIFactory.Image(_lights[i].transform, "Glow", UITheme.Glow, new Color(1f, 0.1f, 0.05f, 0.6f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(150f, 150f));
            }
            _lightsRoot.gameObject.SetActive(false);

            var bigRt = UIFactory.Rect("BigMessage", transform, c, c, new Vector2(0.5f, 1), new Vector2(0f, -300f), new Vector2(1400f, 90f));
            _bigGroup = bigRt.gameObject.AddComponent<CanvasGroup>();
            _bigGroup.alpha = 0f;
            UIFactory.Image(bigRt, "Band", UITheme.GradientH, new Color(0f, 0f, 0f, 0.0f), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            _bigMessage = UIFactory.Text(bigRt, "Text", "", 54, UITheme.White, UITheme.Title, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var shadow = _bigMessage.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
            shadow.effectDistance = new Vector2(3f, -3f);

            _flag = UIFactory.Image(transform, "Flag", UITheme.Skew, UITheme.Green, c, c, new Vector2(0.5f, 1), new Vector2(0f, -110f), new Vector2(260f, 34f));
            _flagText = UIFactory.Text(_flag.transform, "T", "", 20, UITheme.Black, UITheme.Title, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            _warning = UIFactory.Label(transform, "", 34, UITheme.Red, UITheme.Title, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, 150f), 900f);
        }

        // ------------------------------------------------------------------ events

        private void ShowBig(string text, Color c, float seconds)
        {
            _bigMessage.text = text;
            _bigMessage.color = c;
            _bigTimer = seconds;
        }

        private void OnSector(RaceParticipant p, int sector, float time, SectorStatus status)
        {
            if (!p.IsPlayer) return;
            _sectorTimes[sector] = time;
            _sectorStatus[sector] = status;
            if (sector == 0) { _sectorTimes[1] = _sectorTimes[2] = 0f; }
            _sectorFlash = 3f;
        }

        private void OnLap(RaceParticipant p, LapData lap, bool personalBest, bool overallBest)
        {
            if (overallBest && !p.IsPlayer && _race.Config.mode == GameMode.QuickRace)
                ShowBig($"FASTEST LAP  {p.Name}  {UITheme.FormatTime(lap.time)}", UITheme.Purple, 2.5f);
            if (!p.IsPlayer) return;
            if (personalBest) ShowBig($"PERSONAL BEST  {UITheme.FormatTime(lap.time)}", UITheme.Green, 3f);
            else if (overallBest) ShowBig($"FASTEST LAP  {UITheme.FormatTime(lap.time)}", UITheme.Purple, 3f);
            else if (!lap.valid) ShowBig($"LAP {lap.lap}  {UITheme.FormatTime(lap.time)}  (INVALID)", new Color(1f, 0.6f, 0.2f), 2.5f);
            else ShowBig($"LAP {lap.lap}  {UITheme.FormatTime(lap.time)}", UITheme.White, 2.5f);
        }

        // ------------------------------------------------------------------ per-frame update

        private void Update()
        {
            var player = _race.Player;
            if (player == null) return;
            var car = player.Car;

            // cluster
            float rpmN = car.RpmNormalized;
            _rpmFill.fillAmount = rpmN * 0.75f;
            _rpmFill.color = rpmN > 0.9f ? UITheme.Red : Color.Lerp(UITheme.White, new Color(1f, 0.75f, 0.3f), Mathf.InverseLerp(0.7f, 0.9f, rpmN));
            _gear.text = car.Gear < 0 ? "R" : car.Gear.ToString();
            _gear.color = car.RevLimiterHit && Mathf.Repeat(Time.time * 12f, 1f) > 0.5f ? UITheme.Red : UITheme.White;
            _speed.text = Mathf.RoundToInt(car.SpeedMs * _s.SpeedFactor).ToString();
            _unit.text = _s.SpeedLabel;
            _rpmText.text = $"{car.EngineRpm:0} RPM";
            float shiftStart = 0.72f;
            int lit = Mathf.Clamp(Mathf.FloorToInt((rpmN - shiftStart) / (0.96f - shiftStart) * 10f), 0, 10);
            bool flashAll = rpmN > 0.965f;
            for (int i = 0; i < 10; i++)
            {
                Color on = i < 4 ? UITheme.Green : (i < 7 ? UITheme.Red : UITheme.Blue);
                bool isOn = flashAll ? Mathf.Repeat(Time.time * 10f, 1f) > 0.4f : i < lit;
                _shiftLights[i].color = isOn ? (flashAll ? UITheme.Blue : on) : new Color(1f, 1f, 1f, 0.1f);
            }
            _shiftFlash.color = new Color(0.3f, 0.6f, 1f, flashAll ? 0.35f : 0f);
            _throttle.rectTransform.sizeDelta = new Vector2(10f, 180f * car.ThrottleInput);
            _brake.rectTransform.sizeDelta = new Vector2(10f, 180f * car.BrakeInput);
            SetPill(_absBg, _abs, car.AbsEnabled, car.AbsActive, UITheme.Yellow);
            SetPill(_tcsBg, _tcs, car.TcsEnabled, car.TcsActive, UITheme.Yellow);

            // race info
            var cfg = _race.Config;
            bool race = cfg.mode == GameMode.QuickRace;
            _pos.text = race ? "P" + player.Position : "TT";
            _posOf.text = race ? "/" + _race.Field.Count : "";
            _posLabel.text = race ? "POS" : "MODE";
            _lap.text = race ? $"{Mathf.Min(player.Lap, cfg.laps)}/{cfg.laps}" : player.Lap.ToString();
            _raceTime.text = UITheme.FormatTime(_race.RaceTime, false) ?? "";
            if (_race.Phase == RacePhase.Grid || _race.Phase == RacePhase.Countdown) _raceTime.text = "0:00.000";

            bool timing = !float.IsNaN(player.LapStartTime) && (_race.Phase == RacePhase.Racing || _race.Phase == RacePhase.Finished);
            _current.text = timing && !player.Finished ? UITheme.FormatTime(player.CurrentLapTime(_race.RaceTime)) : "--:--.---";
            _current.color = player.LapValid ? UITheme.White : new Color(1f, 0.55f, 0.2f);
            _last.text = UITheme.FormatTime(player.LastLap);
            _best.text = UITheme.FormatTime(player.BestLap);
            _best.color = player.BestLap > 0f && Mathf.Approximately(player.BestLap, _race.OverallBestLap) ? UITheme.Purple : UITheme.White;
            _pb.text = UITheme.FormatTime(_race.PersonalBest);

            _sectorFlash -= Time.deltaTime;
            for (int i = 0; i < 3; i++)
            {
                bool done = i < player.CurrentSector || (_sectorFlash > 0f && _sectorTimes[i] > 0f);
                if (done && _sectorTimes[i] > 0f)
                {
                    _sectorBg[i].color = _sectorStatus[i] == SectorStatus.OverallBest ? UITheme.Purple : (_sectorStatus[i] == SectorStatus.PersonalBest ? UITheme.Green : UITheme.Yellow);
                    _sectorText[i].text = _sectorTimes[i].ToString("0.000");
                    _sectorText[i].color = UITheme.Black;
                }
                else
                {
                    _sectorBg[i].color = i == player.CurrentSector && timing ? new Color(1f, 1f, 1f, 0.18f) : new Color(1f, 1f, 1f, 0.08f);
                    _sectorText[i].text = $"S{i + 1}";
                    _sectorText[i].color = UITheme.White;
                }
            }

            bool showDelta = _race.HasDelta && timing && !player.Finished;
            _delta.text = showDelta ? UITheme.FormatDelta(_race.Delta) : "";
            _delta.color = _race.Delta <= 0f ? UITheme.Green : UITheme.Red;
            _deltaLabel.text = showDelta ? "DELTA TO PERSONAL BEST" : "";

            // tower
            _tower.gameObject.SetActive(_s.showLeaderboard && race);
            if (_tower.gameObject.activeSelf)
            {
                var st = _race.Standings;
                bool java = _race.StandingsSourceAge < 1.5f;
                _towerHeader.text = java ? "LIVE TIMING  ·  JAVA ENGINE" : "LIVE TIMING";
                for (int i = 0; i < _rows.Count; i++)
                {
                    if (i >= st.Count) { _rows[i].bg.gameObject.SetActive(false); continue; }
                    var p = st[i];
                    var r = _rows[i];
                    r.bg.gameObject.SetActive(true);
                    r.pos.text = (i + 1).ToString();
                    r.code.text = p.Code;
                    r.stripe.color = p.IsPlayer ? UITheme.Red : p.TeamColor;
                    r.bg.color = p.IsPlayer ? new Color(0.55f, 0.02f, 0.06f, 0.88f) : UITheme.PanelBg;
                    string gap;
                    if (p.Finished) gap = i == 0 ? "FINISH" : "+" + p.GapToLeader.ToString("0.000");
                    else if (i == 0) gap = "LEADER";
                    else
                    {
                        int down = _race.LapsDown(p);
                        gap = down > 0 ? $"+{down} LAP" : "+" + p.Interval.ToString("0.000");
                    }
                    if (_race.Phase == RacePhase.Grid || _race.Phase == RacePhase.Countdown) gap = "";
                    r.gap.text = gap;
                }
            }

            // countdown lights
            if (_race.Phase == RacePhase.Countdown)
            {
                _lightsRoot.gameObject.SetActive(true);
                for (int i = 0; i < 5; i++) _lights[i].enabled = i < _race.LightsOn;
            }
            else if (_lightsRoot.gameObject.activeSelf) _lightsRoot.gameObject.SetActive(false);

            // big messages
            _bigTimer -= Time.unscaledDeltaTime;
            _bigGroup.alpha = Mathf.Clamp01(_bigTimer * 3f);

            // flags
            var flag = _race.PlayerFlag;
            _flag.gameObject.SetActive(flag != RaceFlag.None);
            switch (flag)
            {
                case RaceFlag.Green: _flag.color = UITheme.Green; _flagText.text = "GREEN FLAG"; _flagText.color = UITheme.Black; break;
                case RaceFlag.Yellow: _flag.color = UITheme.Yellow; _flagText.text = "YELLOW · HAZARD AHEAD"; _flagText.color = UITheme.Black; break;
                case RaceFlag.Blue: _flag.color = UITheme.Blue; _flagText.text = "BLUE · LET LEADER PASS"; _flagText.color = UITheme.White; break;
                case RaceFlag.White: _flag.color = UITheme.White; _flagText.text = "FINAL LAP"; _flagText.color = UITheme.Black; break;
                case RaceFlag.Chequered: _flag.color = UITheme.White; _flagText.text = "▚ CHEQUERED ▞"; _flagText.color = UITheme.Black; break;
                case RaceFlag.TrackLimits: _flag.color = new Color(1f, 0.55f, 0.15f); _flagText.text = "TRACK LIMITS · INVALID LAP"; _flagText.color = UITheme.Black; break;
            }

            _warning.text = player.WrongWay ? "WRONG WAY" : (car.IsFlipped ? "PRESS R TO RECOVER" : "");

            var be = GameServices.Instance.Backend;
            _engineLink.text = be.IsOnline ? $"JAVA ENGINE ● ONLINE · AI LINE: {_race.AiSource} · {be.LatencyMs:0} MS" : $"ENGINE OFFLINE · LOCAL AI · AI LINE: {_race.AiSource}";
            if (_minimap != null) _minimap.transform.parent.gameObject.SetActive(_s.showMinimap);
        }

        private static void SetPill(Image bg, Text t, bool enabled, bool active, Color activeColor)
        {
            if (!enabled) { bg.color = new Color(1f, 1f, 1f, 0.04f); t.color = new Color(1f, 1f, 1f, 0.2f); return; }
            bool blink = active && Mathf.Repeat(Time.time * 8f, 1f) > 0.35f;
            bg.color = blink ? activeColor : new Color(1f, 1f, 1f, 0.1f);
            t.color = blink ? UITheme.Black : UITheme.Metal;
        }

        public void SetVisible(bool v) => _group.alpha = v ? 1f : 0f;
    }
}

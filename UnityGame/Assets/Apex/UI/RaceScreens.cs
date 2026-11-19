using System;
using System.Collections.Generic;
using System.Linq;
using Apex.Core;
using Apex.Race;
using UnityEngine;
using UnityEngine.UI;

namespace Apex.UI
{
    /// <summary>Pause menu: resume, restart, settings, (end session), main menu.</summary>
    public sealed class PauseUI : UIScreen
    {
        public Action Resume, Restart, OpenSettings, MainMenu, EndSession;
        private Text _summary;
        private RaceManager _race;

        public static PauseUI Create(Transform parent, RaceManager race)
        {
            var rt = UIFactory.Stretch("Pause", parent);
            var ui = rt.gameObject.AddComponent<PauseUI>();
            ui._race = race;
            ui.Build();
            return ui;
        }

        private void Build()
        {
            UIFactory.FullImage(transform, "Dim", UITheme.Solid, new Color(0f, 0f, 0f, 0.6f)).raycastTarget = true;
            UIFactory.Image(transform, "Side", UITheme.GradientH, new Color(0f, 0f, 0f, 0.95f), new Vector2(0, 0), new Vector2(0.45f, 1), new Vector2(0, 0.5f), Vector2.zero, Vector2.zero)
                .rectTransform.localScale = new Vector3(-1f, 1f, 1f);
            UIFactory.Heading(transform, "SESSION · PAUSED", "PAUSED", new Vector2(0, 1), new Vector2(96f, -110f));
            var list = UIFactory.VerticalList(transform, "Menu", new Vector2(0, 1f), new Vector2(0, 1f), new Vector2(70f, -290f), new Vector2(640f, 520f), 6f);
            var first = UIFactory.MenuItem(list, "01", "RESUME", "Back to the action", () => Resume?.Invoke());
            UIFactory.MenuItem(list, "02", "RESTART", "Restart this session from the start", () => Restart?.Invoke());
            if (_race.Config.mode == GameMode.TimeTrial)
                UIFactory.MenuItem(list, "03", "END SESSION", "Finish and view your statistics", () => EndSession?.Invoke());
            UIFactory.MenuItem(list, _race.Config.mode == GameMode.TimeTrial ? "04" : "03", "SETTINGS", "Adjust graphics, audio and assists", () => OpenSettings?.Invoke());
            UIFactory.MenuItem(list, _race.Config.mode == GameMode.TimeTrial ? "05" : "04", "MAIN MENU", "Abandon the session", () => MainMenu?.Invoke());
            FirstSelected = first.gameObject;
            _summary = UIFactory.Label(transform, "", 22, UITheme.Metal, UITheme.Body, TextAnchor.LowerLeft, new Vector2(0, 0), new Vector2(100f, 70f), 900f);
            _summary.lineSpacing = 1.3f;
            InitScreen();
            BackRequested = () => Resume?.Invoke();
        }

        protected override void OnShown()
        {
            var p = _race.Player;
            var cfg = _race.Config;
            _summary.text = cfg.mode == GameMode.QuickRace
                ? $"<color=#E30D1C>QUICK RACE</color>   LAP {Mathf.Min(p.Lap, cfg.laps)}/{cfg.laps}   ·   P{p.Position}/{_race.Field.Count}   ·   {cfg.difficulty.ToString().ToUpper()}\nBEST LAP {UITheme.FormatTime(p.BestLap)}   ·   RACE TIME {UITheme.FormatTime(_race.RaceTime)}"
                : $"<color=#E30D1C>TIME TRIAL</color>   LAPS {p.LapsCompleted}   ·   BEST {UITheme.FormatTime(p.BestLap)}   ·   PB {UITheme.FormatTime(_race.PersonalBest)}";
        }
    }

    /// <summary>Final classification and player performance breakdown.</summary>
    public sealed class ResultsUI : UIScreen
    {
        public Action Restart, MainMenu;
        private RaceManager _race;

        public static ResultsUI Create(Transform parent, RaceManager race)
        {
            var rt = UIFactory.Stretch("Results", parent);
            var ui = rt.gameObject.AddComponent<ResultsUI>();
            ui._race = race;
            ui.InitBase();
            return ui;
        }

        private RectTransform _content;

        private void InitBase()
        {
            UIFactory.FullImage(transform, "Dim", UITheme.Solid, new Color(0.01f, 0.01f, 0.015f, 0.82f)).raycastTarget = true;
            _content = UIFactory.Stretch("Content", transform);
            InitScreen();
            BackRequested = () => MainMenu?.Invoke();
        }

        protected override void OnShown()
        {
            for (int i = _content.childCount - 1; i >= 0; i--) Destroy(_content.GetChild(i).gameObject);
            BuildContent();
        }

        private void BuildContent()
        {
            var cfg = _race.Config;
            var results = _race.BuildResults();
            var player = _race.Player;
            bool race = cfg.mode == GameMode.QuickRace;
            var me = results.FirstOrDefault(r => r.isPlayer);
            string title = race ? (me != null && me.position == 1 ? "VICTORY" : $"FINISHED P{me?.position}") : "SESSION COMPLETE";
            UIFactory.Heading(_content, race ? $"QUICK RACE · {cfg.laps} LAPS · {cfg.difficulty.ToString().ToUpper()}" : "TIME TRIAL · RESULTS", title, new Vector2(0, 1), new Vector2(96f, -60f));

            // classification table
            var table = UIFactory.Panel(_content, "Classification", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(96f, -210f), new Vector2(1060f, 120f + results.Count * 62f));
            string[] heads = { "POS", "DRIVER", "CAR", "LAPS", race ? "TIME / GAP" : "TOTAL", "BEST LAP" };
            float[] xs = { 28f, 110f, 430f, 680f, 770f, 930f };
            for (int i = 0; i < heads.Length; i++)
                UIFactory.Label(table, heads[i], 16, UITheme.Red, UITheme.Mono, i >= 3 ? TextAnchor.UpperLeft : TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(xs[i], -28f), 260f);
            float best = results.Where(r => r.bestLap > 0f).Select(r => r.bestLap).DefaultIfEmpty(0f).Min();
            for (int i = 0; i < results.Count; i++)
            {
                var r = results[i];
                float y = -64f - i * 62f;
                var row = UIFactory.Image(table, "Row" + i, UITheme.Solid, r.isPlayer ? new Color(0.55f, 0.02f, 0.06f, 0.75f) : new Color(1f, 1f, 1f, i % 2 == 0 ? 0.04f : 0.02f),
                    new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0f, y), new Vector2(-40f, 56f));
                Color posCol = r.position == 1 ? new Color(1f, 0.82f, 0.25f) : (r.position == 2 ? UITheme.Metal : (r.position == 3 ? new Color(0.8f, 0.5f, 0.3f) : UITheme.White));
                UIFactory.Label(table, r.position.ToString(), 34, posCol, UITheme.Title, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(xs[0] + 6f, y - 28f), 80f);
                UIFactory.Label(table, r.name + (r.isPlayer ? "  (YOU)" : ""), 26, UITheme.White, UITheme.Title, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(xs[1], y - 28f), 320f);
                UIFactory.Label(table, r.car, 18, UITheme.Metal, UITheme.Body, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(xs[2], y - 28f), 250f);
                UIFactory.Label(table, r.laps.ToString(), 22, UITheme.White, UITheme.Mono, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(xs[3], y - 28f), 80f);
                string time;
                if (!race) time = UITheme.FormatTime(r.totalTime);
                else if (r.dnf) time = "DNF";
                else if (r.position == 1) time = UITheme.FormatTime(r.totalTime);
                else if (r.lapsDown > 0) time = $"+{r.lapsDown} LAP{(r.lapsDown > 1 ? "S" : "")}";
                else time = "+" + r.gap.ToString("0.000");
                UIFactory.Label(table, time, 22, UITheme.White, UITheme.Mono, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(xs[4], y - 28f), 160f);
                UIFactory.Label(table, UITheme.FormatTime(r.bestLap), 22, r.bestLap > 0f && Mathf.Approximately(r.bestLap, best) ? UITheme.Purple : UITheme.White, UITheme.Mono, TextAnchor.MiddleLeft,
                    new Vector2(0, 1), new Vector2(xs[5], y - 28f), 160f);
            }

            // player performance
            var perf = UIFactory.Panel(_content, "Performance", new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-96f, -210f), new Vector2(600f, 640f));
            UIFactory.Label(perf, "PLAYER PERFORMANCE", 18, UITheme.Red, UITheme.Mono, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(30f, -26f), 500f);
            var stats = new List<(string, string, Color)>();
            if (race && me != null)
            {
                int gained = player.StartPosition - me.position;
                stats.Add(("GRID → FINISH", $"P{player.StartPosition} → P{me.position}  ({(gained >= 0 ? "+" : "")}{gained})", gained >= 0 ? UITheme.Green : UITheme.Red));
                stats.Add(("TOTAL RACE TIME", UITheme.FormatTime(me.totalTime), UITheme.White));
            }
            else stats.Add(("LAPS COMPLETED", player.LapsCompleted.ToString(), UITheme.White));
            stats.Add(("BEST LAP", UITheme.FormatTime(player.BestLap), player.BestLap > 0f && Mathf.Approximately(player.BestLap, best) ? UITheme.Purple : UITheme.White));
            stats.Add(("AVERAGE LAP", UITheme.FormatTime(player.AverageLap), UITheme.White));
            stats.Add(("CONSISTENCY (σ)", player.Laps.Count > 1 ? $"±{player.LapStdDev:0.000} s" : "—", UITheme.White));
            float theo = player.BestSectors.All(s => s > 0f) ? player.BestSectors.Sum() : 0f;
            stats.Add(("BEST SECTORS", player.BestSectors.All(s => s > 0f) ? string.Join("  ", player.BestSectors.Select(s => s.ToString("0.000"))) : "—", UITheme.White));
            stats.Add(("THEORETICAL BEST", UITheme.FormatTime(theo), UITheme.Green));
            stats.Add(("TOP SPEED", $"{player.TopSpeed * GameServices.Instance.Settings.SpeedFactor:0} {GameServices.Instance.Settings.SpeedLabel}", UITheme.White));
            stats.Add(("TRACK LIMIT WARNINGS", player.TrackLimitWarnings.ToString(), player.TrackLimitWarnings > 0 ? UITheme.Yellow : UITheme.White));
            stats.Add(("PERSONAL BEST", UITheme.FormatTime(_race.PersonalBest), UITheme.Purple));
            for (int i = 0; i < stats.Count; i++)
            {
                float y = -70f - i * 50f;
                UIFactory.Label(perf, stats[i].Item1, 18, UITheme.Metal, UITheme.Mono, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(30f, y), 300f);
                UIFactory.Label(perf, stats[i].Item2, 24, stats[i].Item3, UITheme.Mono, TextAnchor.UpperRight, new Vector2(1, 1), new Vector2(-30f, y - 2f), 330f);
                UIFactory.Image(perf, "Rule" + i, UITheme.Solid, new Color(1f, 1f, 1f, 0.05f), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0f, y - 38f), new Vector2(-60f, 1f));
            }

            // lap history
            var laps = UIFactory.Panel(_content, "Laps", new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(96f, 150f), new Vector2(1060f, 150f));
            UIFactory.Label(laps, "LAP TIMES", 18, UITheme.Red, UITheme.Mono, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(30f, -22f), 300f);
            int shown = Mathf.Min(player.Laps.Count, 10);
            for (int i = 0; i < shown; i++)
            {
                var l = player.Laps[player.Laps.Count - shown + i];
                float x = 30f + i * 102f;
                Color c = !l.valid ? new Color(1f, 0.55f, 0.2f) : (Mathf.Approximately(l.time, player.BestLap) ? UITheme.Green : UITheme.White);
                UIFactory.Label(laps, "L" + l.lap, 16, UITheme.Steel, UITheme.Mono, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(x, -56f), 100f);
                UIFactory.Label(laps, UITheme.FormatTime(l.time), 20, c, UITheme.Mono, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(x, -80f), 110f);
            }
            if (shown == 0) UIFactory.Label(laps, "No completed laps", 20, UITheme.Steel, UITheme.Body, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(30f, -60f), 400f);

            var be = GameServices.Instance.Backend;
            UIFactory.Label(_content, be.IsOnline ? "RESULTS SAVED TO JAVA ENGINE (H2) AND LOCAL RECORDS" : "RESULTS SAVED TO LOCAL RECORDS · WILL SYNC WHEN ENGINE CONNECTS",
                16, UITheme.Steel, UITheme.Mono, TextAnchor.LowerLeft, new Vector2(0, 0), new Vector2(98f, 60f), 1200f);
            var restart = UIFactory.ActionButton(_content, race ? "RACE AGAIN" : "NEW SESSION", () => Restart?.Invoke(), false, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-470f, 60f), new Vector2(340f, 70f));
            var menu = UIFactory.ActionButton(_content, "MAIN MENU", () => MainMenu?.Invoke(), true, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-96f, 60f), new Vector2(340f, 70f));
            FirstSelected = menu.gameObject;
            _ = restart;
            if (UnityEngine.EventSystems.EventSystem.current != null) UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(menu.gameObject);
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Apex.Race;
using UnityEngine;

namespace Apex.Core
{
    /// <summary>
    /// Autotest safety net: records every error/exception to exceptions.log and terminates the run if
    /// it exceeds its wall-clock budget, so an unattended test can never hang.
    /// </summary>
    public sealed class AutoTestWatchdog : MonoBehaviour
    {
        private float _limit;
        private string _log;
        public static int ErrorCount;

        public void Begin(AutoTestOptions o)
        {
            _limit = 300f + o.Laps * 300f;
            Directory.CreateDirectory(o.OutputDir);
            _log = Path.Combine(o.OutputDir, "exceptions.log");
            File.WriteAllText(_log, "");
            Application.logMessageReceived += OnLog;
        }

        private void OnLog(string message, string stack, LogType type)
        {
            if (type != LogType.Exception && type != LogType.Error && type != LogType.Assert) return;
            ErrorCount++;
            try { File.AppendAllText(_log, $"[{Time.realtimeSinceStartup:0.0}s] {type}: {message}\n{stack}\n"); } catch { /* ignore */ }
        }

        private void Update()
        {
            if (Time.realtimeSinceStartup < _limit) return;
            Debug.LogError("[AutoTest] watchdog: wall-clock limit reached, aborting");
            enabled = false;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(3);
#else
            Application.Quit(3);
#endif
        }
    }

    /// <summary>Screenshot capture for automated test runs.</summary>
    public static class AutoTestCapture
    {
        public static void Shot(string name)
        {
            var o = GameServices.Instance.AutoTest;
            Directory.CreateDirectory(o.OutputDir);
            string path = Path.Combine(o.OutputDir, name + ".png");
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log("[AutoTest] screenshot " + path);
        }
    }

    /// <summary>
    /// Records objective measurements during an automated session (frame times, timing events,
    /// AI behaviour, backend link) and writes a JSON-like report used by the test phase.
    /// </summary>
    public sealed class AutoTestRecorder
    {
        private readonly RaceManager _race;
        private readonly AutoTestOptions _o;
        private readonly List<float> _frameTimes = new List<float>(20000);
        private readonly List<string> _events = new List<string>();
        private int _sectorEvents, _lapEvents, _collisions;
        private string _failure;
        private float _onlineTime, _totalTime;
        private readonly float _startRealtime;

        public AutoTestRecorder(RaceManager race, AutoTestOptions o)
        {
            _race = race;
            _o = o;
            _startRealtime = Time.realtimeSinceStartup;
            race.SectorCompleted += (p, s, t, st) => { _sectorEvents++; };
            race.LapCompleted += (p, lap, pb, ob) => { _lapEvents++; _events.Add($"{_race.RaceTime:0.00}s LAP {p.Code} L{lap.lap} {lap.time:0.000} valid={lap.valid} s1={lap.s1:0.000} s2={lap.s2:0.000} s3={lap.s3:0.000}"); };
            race.ParticipantFinished += p => _events.Add($"{_race.RaceTime:0.00}s FINISH {p.Code} order={p.FinishOrder} time={p.FinishTime:0.000}");
            race.LightsOut += () => _events.Add("LIGHTS OUT");
            race.CountdownLight += n => _events.Add("LIGHT " + n);
            race.Message += (m, c) => _events.Add($"{_race.RaceTime:0.00}s MSG {m}");
            foreach (var p in race.Field) p.Car.Collided += (impact, pt) => { if (impact > 4f) _collisions++; };
        }

        public void Tick(float dt)
        {
            if (_race.Phase != RacePhase.Racing) return;
            _frameTimes.Add(dt);
            _totalTime += dt;
            if (GameServices.Instance.Backend.IsOnline) _onlineTime += dt;
        }

        public void Fail(string reason) => _failure = reason;

        public void WriteReport()
        {
            var sb = new StringBuilder();
            var be = GameServices.Instance.Backend;
            var sorted = _frameTimes.OrderBy(f => f).ToList();
            float avg = sorted.Count > 0 ? sorted.Average() : 0f;
            float p99 = sorted.Count > 0 ? sorted[Mathf.Min(sorted.Count - 1, (int)(sorted.Count * 0.99f))] : 0f;
            sb.AppendLine("{");
            sb.AppendLine($"  \"mode\": \"{_race.Config.mode}\", \"laps\": {_race.Config.laps}, \"difficulty\": \"{_race.Config.difficulty}\",");
            sb.AppendLine($"  \"failure\": \"{_failure ?? ""}\", \"loggedErrors\": {AutoTestWatchdog.ErrorCount},");
            sb.AppendLine($"  \"raceTime\": {_race.RaceTime:0.000}, \"wallClock\": {Time.realtimeSinceStartup - _startRealtime:0.0},");
            sb.AppendLine($"  \"fpsAvg\": {(avg > 0 ? 1f / avg : 0):0.0}, \"fps1pctLow\": {(p99 > 0 ? 1f / p99 : 0):0.0}, \"frames\": {sorted.Count},");
            sb.AppendLine($"  \"backendOnlineShare\": {(_totalTime > 0 ? _onlineTime / _totalTime : 0):0.00}, \"backendReconnects\": {be.ReconnectCount}, \"msgIn\": {be.MessagesIn}, \"msgOut\": {be.MessagesOut}, \"aiLineSource\": \"{_race.AiSource}\",");
            sb.AppendLine($"  \"sectorEvents\": {_sectorEvents}, \"lapEvents\": {_lapEvents}, \"collisions\": {_collisions}, \"overallBestLap\": {_race.OverallBestLap:0.000},");
            sb.AppendLine("  \"results\": [");
            var results = _race.BuildResults();
            for (int i = 0; i < results.Count; i++)
            {
                var r = results[i];
                var p = _race.Field[r.driverId];
                sb.AppendLine($"    {{\"pos\": {r.position}, \"code\": \"{r.code}\", \"player\": {r.isPlayer.ToString().ToLower()}, \"personality\": \"{p.Personality}\", \"laps\": {r.laps}, \"total\": {r.totalTime:0.000}, \"best\": {r.bestLap:0.000}, \"finished\": {p.Finished.ToString().ToLower()}, \"recoveries\": {p.Recoveries}, \"trackLimits\": {p.TrackLimitWarnings}, \"topSpeedKmh\": {p.TopSpeed * 3.6f:0.0}, \"carHits\": {p.Car.CarContacts}, \"wallHits\": {p.Car.WallContacts}, \"gridPos\": {p.StartPosition}}}{(i < results.Count - 1 ? "," : "")}");
            }
            sb.AppendLine("  ],");
            sb.AppendLine("  \"events\": [");
            for (int i = 0; i < _events.Count; i++) sb.AppendLine($"    \"{_events[i].Replace("\"", "'")}\"{(i < _events.Count - 1 ? "," : "")}");
            sb.AppendLine("  ]");
            sb.AppendLine("}");
            Directory.CreateDirectory(_o.OutputDir);
            string path = Path.Combine(_o.OutputDir, $"report_{_race.Config.mode}.json");
            File.WriteAllText(path, sb.ToString());
            Debug.Log("[AutoTest] report written: " + path);
        }
    }
}

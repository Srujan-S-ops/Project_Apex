using System;
using System.Collections.Generic;
using System.Linq;
using Apex.AI;
using Apex.Core;
using Apex.Net;
using Apex.Track;
using Apex.Vehicle;
using UnityEngine;

namespace Apex.Race
{
    public enum RacePhase { Grid, Countdown, Racing, Finished, Results }
    public enum RaceFlag { None, Green, Yellow, Blue, White, Chequered, TrackLimits }
    public enum SectorStatus { Slower, PersonalBest, OverallBest }

    /// <summary>
    /// Authoritative race logic: countdown, timing (checkpoints, sectors, laps, interpolated line
    /// crossings), validity, positions, gaps, flags, finish/classification, recovery, records and the
    /// Java session protocol (telemetry out, AI commands / standings in).
    /// </summary>
    public sealed class RaceManager : MonoBehaviour
    {
        public RaceConfig Config { get; private set; }
        public TrackWorld Track { get; private set; }
        public AITrackModel AiModel { get; private set; }
        public RacePhase Phase { get; private set; } = RacePhase.Grid;
        public float RaceTime { get; private set; }
        public int LightsOn { get; private set; }
        public readonly List<RaceParticipant> Field = new List<RaceParticipant>();
        public List<RaceParticipant> Standings { get; private set; } = new List<RaceParticipant>();
        public RaceParticipant Player { get; private set; }
        public float OverallBestLap { get; private set; }
        public RaceParticipant OverallBestHolder { get; private set; }
        public readonly float[] OverallBestSectors = new float[3];
        public RaceFlag PlayerFlag { get; private set; } = RaceFlag.None;
        public string SessionId { get; private set; }
        public float PersonalBest { get; private set; }
        public float[] PersonalBestSectors { get; private set; }
        public float Delta { get; private set; }
        public bool HasDelta { get; private set; }
        public string AiSource => AiModel?.Source ?? "LOCAL";
        public float StandingsSourceAge => Time.realtimeSinceStartup - _lastBackendStandings;

        public event Action<int> CountdownLight;
        public event Action LightsOut;
        public event Action<RaceParticipant, LapData, bool, bool> LapCompleted;      // p, lap, personal best, overall best
        public event Action<RaceParticipant, int, float, SectorStatus> SectorCompleted;
        public event Action<RaceParticipant> ParticipantFinished;
        public event Action<string, Color> Message;
        public event Action RaceComplete;

        private float[] _reference;           // personal-best reference lap (time every 10 m)
        private int _finishOrder;
        private float _leaderFinishTime = -1f;
        private float _telemetryTimer;
        private float _flagTimer;
        private float _greenUntil;
        private float _lastBackendStandings = -100f;
        private readonly Dictionary<int, StandingDto> _backendStandings = new Dictionary<int, StandingDto>();
        private BackendClient _backend;
        private bool _resultsSent;
        private float _countdownTimer;
        private float _holdTime;
        private bool _finalLapAnnounced;

        public void Initialise(RaceConfig config, TrackWorld track, AITrackModel aiModel)
        {
            Config = config;
            Track = track;
            AiModel = aiModel;
            SessionId = Guid.NewGuid().ToString("N").Substring(0, 12);
            PersonalBest = GameServices.Instance.Records.BestLap(config.trackId, config.RecordKey, out var sectors, out _reference);
            PersonalBestSectors = sectors;
            _backend = GameServices.Instance.Backend;
            _backend.MessageReceived += OnBackendMessage;
            _backend.Connected += OnBackendConnected;
        }

        public void AddParticipant(RaceParticipant p)
        {
            p.Id = Field.Count;
            Field.Add(p);
            if (p.IsPlayer) Player = p;
            InitTracking(p);
        }

        private void InitTracking(RaceParticipant p)
        {
            var proj = Track.Spline.Project(p.Car.transform.position, -1);
            p.TrackIndex = proj.Index;
            p.CentreDistance = proj.Distance;
            p.DistFromLine = Track.TrackDistanceFromLine(proj.Distance);
            p.TotalDistance = Track.Spline.Delta(Track.StartLine, proj.Distance);
            p.Lateral = proj.Lateral;
        }

        // ------------------------------------------------------------------ lifecycle

        public void BeginGrid()
        {
            Phase = RacePhase.Grid;
            foreach (var p in Field) p.Car.InputLocked = Config.mode == GameMode.QuickRace;
            UpdateStandings();
            for (int i = 0; i < Standings.Count; i++) Standings[i].StartPosition = i + 1;
            SendTrackData();
            SendSessionStart();
            if (Config.mode == GameMode.TimeTrial)
            {
                // rolling start: timing begins at the first line crossing
                Phase = RacePhase.Racing;
                foreach (var p in Field) { p.Car.InputLocked = false; if (p.AI != null) p.AI.RaceActive = true; }
                Player.LapStartTime = float.NaN;
                Message?.Invoke("OUT LAP — CROSS THE LINE TO START TIMING", Color.white);
            }
        }

        public void StartCountdown()
        {
            if (Config.mode != GameMode.QuickRace) return;
            Phase = RacePhase.Countdown;
            LightsOn = 0;
            _countdownTimer = 1.0f;
            _holdTime = UnityEngine.Random.Range(0.4f, 1.6f);
        }

        private void Update()
        {
            if (Phase == RacePhase.Countdown)
            {
                _countdownTimer -= Time.deltaTime;
                if (_countdownTimer <= 0f)
                {
                    if (LightsOn < 5)
                    {
                        LightsOn++;
                        Track.SetStartLights(LightsOn);
                        CountdownLight?.Invoke(LightsOn);
                        _countdownTimer = LightsOn < 5 ? 1.0f : _holdTime;
                    }
                    else
                    {
                        GoGreen();
                    }
                }
            }
            if (Phase == RacePhase.Racing || Phase == RacePhase.Finished) UpdateFlags();
        }

        private void GoGreen()
        {
            Phase = RacePhase.Racing;
            LightsOn = 0;
            Track.SetStartLights(0);
            RaceTime = 0f;
            foreach (var p in Field)
            {
                p.Car.InputLocked = false;
                p.LapStartTime = 0f;
                p.SectorStartTime = 0f;
                if (p.AI != null) p.AI.RaceActive = true;
            }
            _greenUntil = Time.time + 4f;
            LightsOut?.Invoke();
        }

        private void FixedUpdate()
        {
            if (Track == null) return;
            float dt = Time.fixedDeltaTime;
            bool running = Phase == RacePhase.Racing || Phase == RacePhase.Finished;
            if (running) RaceTime += dt;

            foreach (var p in Field) Track1(p, dt, running);
            UpdateStandings();

            if (running)
            {
                _telemetryTimer -= dt;
                if (_telemetryTimer <= 0f) { _telemetryTimer = 0.1f; SendTelemetry(); }
                CheckRaceEnd();
            }
        }

        // ------------------------------------------------------------------ per-car tracking & timing

        private void Track1(RaceParticipant p, float dt, bool running)
        {
            var s = Track.Spline;
            var car = p.Car;
            var proj = s.Project(car.transform.position, p.TrackIndex);
            float prevCentre = p.CentreDistance;
            float prevLine = p.DistFromLine;
            p.TrackIndex = proj.Index;
            p.CentreDistance = proj.Distance;
            p.Lateral = proj.Lateral;
            float step = s.Delta(prevCentre, proj.Distance);
            if (Mathf.Abs(step) > 30f) step = 0f;     // teleport (recovery) → no distance credit
            p.TotalDistance += step;
            p.DistFromLine = Track.TrackDistanceFromLine(proj.Distance);
            p.TopSpeed = Mathf.Max(p.TopSpeed, car.SpeedMs);
            if (p.GhostTimer > 0f) { p.GhostTimer -= dt; if (p.GhostTimer <= 0f) SetGhost(p, false); }

            // wrong way & track limits
            float align = Vector3.Dot(car.transform.forward, s.Fwd[proj.Index]);
            if (align < -0.4f && car.SpeedMs > 4f) p.WrongWayTimer += dt; else p.WrongWayTimer = 0f;
            p.WrongWay = p.WrongWayTimer > 1.5f;
            float limit = s.Width[proj.Index] * 0.5f + 1.9f;
            bool off = Mathf.Abs(proj.Lateral) > limit;
            if (off && !p.OffTrack && running && !p.Finished)
            {
                if (p.LapValid && !float.IsNaN(p.LapStartTime))
                {
                    p.LapValid = false;
                    p.TrackLimitWarnings++;
                    Debug.Log($"[TrackLimits] {p.Code} line={p.DistFromLine:0} centre={proj.Distance:0} lateral={proj.Lateral:0.00} halfWidth={s.Width[proj.Index] * 0.5f:0.00} curvature={s.Curvature[proj.Index]:0.0000} speed={car.SpeedMs * 3.6f:0}");
                    if (p.IsPlayer) Message?.Invoke("TRACK LIMITS — LAP INVALIDATED", new Color(1f, 0.8f, 0.1f));
                }
            }
            p.OffTrack = off;

            // automatic recovery
            if (car.IsFlipped && car.SpeedMs < 3f) p.FlipTimer += dt; else p.FlipTimer = 0f;
            if (p.FlipTimer > 2.5f) Respawn(p);
            if (proj.DistanceToCentre > s.Width[proj.Index] * 0.5f + 30f || proj.Height < -6f) Respawn(p);

            if (!running || p.Finished) return;

            // reference samples for delta timing (time trial / player)
            if (p.IsPlayer && !float.IsNaN(p.LapStartTime))
            {
                int sample = Mathf.FloorToInt(p.DistFromLine / 10f);
                while (p.ReferenceSamples.Count <= sample && p.ReferenceSamples.Count < 2000)
                    p.ReferenceSamples.Add(RaceTime - p.LapStartTime);
                if (_reference != null && sample < _reference.Length && sample >= 0 && p.LapValid)
                {
                    float frac = (p.DistFromLine - sample * 10f) / 10f;
                    float refT = sample + 1 < _reference.Length ? Mathf.Lerp(_reference[sample], _reference[sample + 1], frac) : _reference[sample];
                    Delta = (RaceTime - p.LapStartTime) - refT;
                    HasDelta = true;
                }
                else HasDelta = false;
            }

            // marker times for gap calculation
            int marker = Mathf.FloorToInt(Mathf.Max(0f, p.TotalDistance) / 20f);
            while (p.MarkerTimes.Count <= marker) p.MarkerTimes.Add(RaceTime);

            float L = s.Length;
            float cur = p.DistFromLine;
            bool forward = step > 0f;
            // checkpoints in order
            if (forward && p.NextCheckpoint < Track.Checkpoints.Length)
            {
                float cp = Track.Checkpoints[p.NextCheckpoint];
                if (prevLine < cp && cur >= cp && cur - prevLine < 40f) p.NextCheckpoint++;
            }
            // sector boundaries
            if (forward && p.CurrentSector < 2)
            {
                float boundary = Track.SectorStarts[p.CurrentSector + 1];
                if (prevLine < boundary && cur >= boundary && cur - prevLine < 40f && !float.IsNaN(p.LapStartTime))
                {
                    float frac = (boundary - prevLine) / Mathf.Max(1e-4f, cur - prevLine);
                    float t = RaceTime - dt + frac * dt;
                    CompleteSector(p, p.CurrentSector, t - p.SectorStartTime);
                    p.SectorStartTime = t;
                    p.CurrentSector++;
                }
            }
            // line crossing
            if (forward && prevLine > L - 40f && cur < 40f)
            {
                float before = L - prevLine;
                float frac = before / Mathf.Max(1e-4f, before + cur);
                float t = RaceTime - dt + frac * dt;
                OnLineCrossed(p, t);
            }
        }

        private void OnLineCrossed(RaceParticipant p, float t)
        {
            if (float.IsNaN(p.LapStartTime))
            {
                // time trial: start of the first timed lap
                StartLap(p, t);
                Message?.Invoke("TIMED LAP", Color.white);
                return;
            }
            bool allCheckpoints = p.NextCheckpoint >= Track.Checkpoints.Length;
            if (!allCheckpoints)
            {
                // e.g. the run from the grid to the line, or a shortcut
                if (p.LapsCompleted > 0 && p.IsPlayer) Message?.Invoke("LAP NOT COUNTED — MISSED CHECKPOINT", new Color(1f, 0.4f, 0.2f));
                return;
            }
            float lapTime = t - p.LapStartTime;
            float s3 = t - p.SectorStartTime;
            CompleteSector(p, 2, s3);
            var lap = new LapData { lap = p.Lap, time = lapTime, s1 = p.CurrentSectors[0], s2 = p.CurrentSectors[1], s3 = p.CurrentSectors[2], valid = p.LapValid };
            p.Laps.Add(lap);
            p.LastLap = lapTime;
            p.LapsCompleted++;
            bool pb = false, ob = false;
            if (lap.valid && (p.BestLap <= 0f || lapTime < p.BestLap)) p.BestLap = lapTime;
            if (lap.valid && (OverallBestLap <= 0f || lapTime < OverallBestLap))
            {
                OverallBestLap = lapTime; OverallBestHolder = p; ob = true;
            }
            if (p.IsPlayer && lap.valid)
            {
                var reference = p.ReferenceSamples.ToArray();
                pb = GameServices.Instance.Records.SubmitLap(Config.trackId, Config.RecordKey, p.CarName, lap, reference);
                if (pb)
                {
                    PersonalBest = lapTime;
                    PersonalBestSectors = new[] { lap.s1, lap.s2, lap.s3 };
                    _reference = reference;
                }
            }
            _backend.Send(MsgType.LapCompleted, new LapCompletedMsg
            {
                sessionId = SessionId, driverId = p.Id, lap = lap.lap, lapTime = lap.time, s1 = lap.s1, s2 = lap.s2, s3 = lap.s3, valid = lap.valid
            }, true);
            LapCompleted?.Invoke(p, lap, pb, ob);

            // finishing
            if (Config.mode == GameMode.QuickRace && (p.LapsCompleted >= Config.laps || _leaderFinishTime >= 0f))
            {
                Finish(p, t);
                return;
            }
            if (Config.mode == GameMode.QuickRace && p.IsPlayer && p.Lap + 1 == Config.laps && !_finalLapAnnounced)
            {
                _finalLapAnnounced = true;
                Message?.Invoke("FINAL LAP", Color.white);
            }
            p.Lap++;
            StartLap(p, t);
        }

        private void StartLap(RaceParticipant p, float t)
        {
            p.LapStartTime = t;
            p.SectorStartTime = t;
            p.CurrentSector = 0;
            p.NextCheckpoint = 0;
            p.LapValid = true;
            p.ReferenceSamples.Clear();
            for (int i = 0; i < 3; i++) p.CurrentSectors[i] = 0f;
        }

        private void CompleteSector(RaceParticipant p, int sector, float time)
        {
            p.CurrentSectors[sector] = time;
            var status = SectorStatus.Slower;
            if (p.LapValid)
            {
                if (OverallBestSectors[sector] <= 0f || time < OverallBestSectors[sector])
                {
                    OverallBestSectors[sector] = time;
                    status = SectorStatus.OverallBest;
                }
                if (p.BestSectors[sector] <= 0f || time < p.BestSectors[sector])
                {
                    p.BestSectors[sector] = time;
                    if (status != SectorStatus.OverallBest) status = SectorStatus.PersonalBest;
                }
            }
            SectorCompleted?.Invoke(p, sector, time, status);
        }

        private void Finish(RaceParticipant p, float t)
        {
            if (p.Finished) return;
            p.Finished = true;
            p.FinishTime = t;
            p.FinishOrder = ++_finishOrder;
            if (_leaderFinishTime < 0f) _leaderFinishTime = t;   // everyone else finishes at their next line crossing
            if (p.IsPlayer)
            {
                Phase = RacePhase.Finished;
                Message?.Invoke(p.FinishOrder == 1 ? "CHEQUERED FLAG — VICTORY!" : $"CHEQUERED FLAG — P{CurrentPosition(p)}", Color.white);
            }
            ParticipantFinished?.Invoke(p);
        }

        private int CurrentPosition(RaceParticipant p) => Standings.IndexOf(p) + 1;

        private void CheckRaceEnd()
        {
            if (Config.mode != GameMode.QuickRace || Phase == RacePhase.Results) return;
            bool all = Field.All(f => f.Finished);
            bool timeout = _leaderFinishTime >= 0f && RaceTime - _leaderFinishTime > 60f && (Player == null || Player.Finished);
            if (Player != null && Player.Finished && (all || timeout || RaceTime - Player.FinishTime > 40f))
                CompleteRace();
        }

        /// <summary>Ends the session and publishes results (quick race end, or time trial "end session").</summary>
        public void CompleteRace()
        {
            if (Phase == RacePhase.Results) return;
            Phase = RacePhase.Results;
            UpdateStandings();
            foreach (var p in Field) if (!p.Finished && Config.mode == GameMode.QuickRace) p.Dnf = false;
            SendResults();
            RaceComplete?.Invoke();
        }

        // ------------------------------------------------------------------ standings

        private void UpdateStandings()
        {
            Standings = Field.OrderByDescending(p => p.Finished ? 1 : 0)
                .ThenBy(p => p.Finished ? p.FinishOrder : 0)
                .ThenByDescending(p => p.TotalDistance)
                .ToList();
            var leader = Standings.Count > 0 ? Standings[0] : null;
            for (int i = 0; i < Standings.Count; i++)
            {
                var p = Standings[i];
                p.Position = i + 1;
                if (_backendStandings.TryGetValue(p.Id, out var bs) && StandingsSourceAge < 1.5f)
                {
                    p.GapToLeader = bs.gapToLeader;
                    p.Interval = bs.interval;
                    continue;
                }
                if (i == 0 || leader == null) { p.GapToLeader = 0f; p.Interval = 0f; continue; }
                p.GapToLeader = TimeGap(leader, p);
                p.Interval = TimeGap(Standings[i - 1], p);
            }
        }

        private float TimeGap(RaceParticipant ahead, RaceParticipant behind)
        {
            if (ahead.Finished && behind.Finished) return behind.FinishTime - ahead.FinishTime;
            int m = Mathf.FloorToInt(Mathf.Max(0f, behind.TotalDistance) / 20f);
            if (m < ahead.MarkerTimes.Count && m < behind.MarkerTimes.Count)
                return Mathf.Max(0f, behind.MarkerTimes[m] - ahead.MarkerTimes[m]);
            return 0f;
        }

        public int LapsDown(RaceParticipant p)
        {
            if (Standings.Count == 0) return 0;
            var leader = Standings[0];
            return Mathf.Max(0, Mathf.FloorToInt((leader.TotalDistance - p.TotalDistance) / Track.Spline.Length));
        }

        // ------------------------------------------------------------------ flags

        private void UpdateFlags()
        {
            if (Player == null) return;
            _flagTimer -= Time.deltaTime;
            if (_flagTimer > 0f) return;
            _flagTimer = 0.25f;
            var s = Track.Spline;
            RaceFlag flag = RaceFlag.None;
            if (Player.Finished) flag = RaceFlag.Chequered;
            else if (Time.time < _greenUntil) flag = RaceFlag.Green;
            else
            {
                foreach (var o in Field)
                {
                    if (o == Player) continue;
                    float ahead = s.Delta(Player.CentreDistance, o.CentreDistance);
                    if (ahead > 0f && ahead < 250f && o.Car.SpeedMs < 5f && RaceTime > 8f && !o.Finished) { flag = RaceFlag.Yellow; break; }
                }
                if (flag == RaceFlag.None)
                {
                    foreach (var o in Field)
                    {
                        if (o == Player) continue;
                        float behind = s.Delta(o.CentreDistance, Player.CentreDistance);
                        if (behind > 0f && behind < 80f && o.TotalDistance - Player.TotalDistance > s.Length * 0.5f) { flag = RaceFlag.Blue; break; }
                    }
                }
                if (flag == RaceFlag.None && !Player.LapValid && Config.mode == GameMode.TimeTrial) flag = RaceFlag.TrackLimits;
                if (flag == RaceFlag.None && Config.mode == GameMode.QuickRace && Standings.Count > 0 && Standings[0].Lap == Config.laps && Standings[0].LapsCompleted < Config.laps)
                    flag = RaceFlag.White;
            }
            PlayerFlag = flag;
        }

        // ------------------------------------------------------------------ recovery

        public void Respawn(RaceParticipant p)
        {
            var s = Track.Spline;
            float d = p.CentreDistance - 4f;
            float lateral = AiModel.OffsetAt(d) * 0.3f;
            s.Sample(d, out var pos, out var fwd, out var right, out _);
            Vector3 place = pos + right * lateral + Vector3.up * 0.5f;
            p.Car.PlaceAt(place, Quaternion.LookRotation(fwd, Vector3.up));
            p.FlipTimer = 0f;
            p.Recoveries++;
            SetGhost(p, true);
            p.GhostTimer = 3f;
            if (p.IsPlayer) Message?.Invoke("CAR RECOVERED", new Color(0.8f, 0.8f, 0.8f));
        }

        private void SetGhost(RaceParticipant p, bool ghost)
        {
            var mine = p.Car.GetComponents<Collider>();
            foreach (var o in Field)
            {
                if (o == p) continue;
                foreach (var a in mine)
                foreach (var b in o.Car.GetComponents<Collider>())
                    Physics.IgnoreCollision(a, b, ghost || o.GhostTimer > 0f);
            }
        }

        // ------------------------------------------------------------------ backend protocol

        private void OnBackendConnected()
        {
            // re-establish session state after a reconnection
            SendTrackData();
            SendSessionStart();
        }

        private void SendTrackData()
        {
            if (!_backend.IsOnline) return;
            var s = Track.Spline;
            var msg = new TrackDataMsg
            {
                trackId = Config.trackId,
                x = new float[s.Count], y = new float[s.Count], z = new float[s.Count], width = new float[s.Count]
            };
            for (int i = 0; i < s.Count; i++)
            {
                msg.x[i] = s.Pos[i].x; msg.y[i] = s.Pos[i].y; msg.z[i] = s.Pos[i].z; msg.width[i] = s.Width[i];
            }
            var env = VehicleEnvelope.For(Config.carClass);
            msg.mu = env.Mu; msg.massKg = env.MassKg; msg.downforceCoef = env.DownforceCoef;
            msg.maxAccel = env.MaxAccel; msg.maxBrake = env.MaxBrake; msg.topSpeed = env.TopSpeed;
            _backend.Send(MsgType.TrackData, msg);
        }

        private void SendSessionStart()
        {
            if (!_backend.IsOnline || Field.Count == 0) return;
            _backend.Send(MsgType.SessionStart, new SessionStartMsg
            {
                sessionId = SessionId,
                mode = Config.mode.ToString(),
                difficulty = Config.difficulty.ToString(),
                trackId = Config.trackId,
                laps = Config.mode == GameMode.QuickRace ? Config.laps : 0,
                trackLength = Track.Spline.Length,
                drivers = Field.Select(p => new DriverDto { id = p.Id, name = p.Name, personality = p.Personality.ToString(), isPlayer = p.IsPlayer, car = p.CarName }).ToArray()
            });
        }

        private void SendTelemetry()
        {
            if (!_backend.IsOnline) return;
            _backend.Send(MsgType.Telemetry, new TelemetryMsg
            {
                sessionId = SessionId,
                raceTime = RaceTime,
                cars = Field.Select(p => new CarTelemetryDto
                {
                    id = p.Id, distance = p.TotalDistance, lapDistance = p.CentreDistance, lateral = p.Lateral,
                    speed = p.Car.SpeedMs, lap = p.Lap, position = p.Position
                }).ToArray()
            });
        }

        private void SendResults()
        {
            if (_resultsSent) return;
            _resultsSent = true;
            var results = BuildResults();
            var msg = new RaceFinishedMsg
            {
                sessionId = SessionId, mode = Config.mode.ToString(), trackId = Config.trackId, difficulty = Config.difficulty.ToString(),
                car = Player?.CarName, laps = Config.mode == GameMode.QuickRace ? Config.laps : (Player?.LapsCompleted ?? 0),
                results = results.Select(r => new ResultDto
                {
                    driverId = r.driverId, name = r.name, isPlayer = r.isPlayer, position = r.position,
                    totalTime = r.totalTime, bestLap = r.bestLap, laps = r.laps, dnf = r.dnf
                }).ToArray()
            };
            _backend.Send(MsgType.RaceFinished, msg, true);

            var pr = results.FirstOrDefault(r => r.isPlayer);
            GameServices.Instance.Records.AddRace(new RaceRecord
            {
                date = DateTime.Now.ToString("yyyy-MM-dd HH:mm"), mode = Config.mode.ToString(), trackId = Config.trackId,
                difficulty = Config.difficulty.ToString(), laps = msg.laps, playerPosition = pr?.position ?? 0, fieldSize = Field.Count,
                playerBestLap = pr?.bestLap ?? 0f, playerTotal = pr?.totalTime ?? 0f
            }, Player?.LapsCompleted ?? 0);
        }

        public List<ResultEntry> BuildResults()
        {
            UpdateStandings();
            var list = new List<ResultEntry>();
            var winner = Standings.FirstOrDefault();
            foreach (var p in Standings)
            {
                int lapsDown = p.Finished ? Mathf.Max(0, (winner?.LapsCompleted ?? 0) - p.LapsCompleted) : LapsDown(p);
                list.Add(new ResultEntry
                {
                    driverId = p.Id, name = p.Name, code = p.Code, car = p.CarName, isPlayer = p.IsPlayer, position = p.Position,
                    totalTime = p.Finished ? p.FinishTime : RaceTime, bestLap = p.BestLap, laps = p.LapsCompleted,
                    dnf = Config.mode == GameMode.QuickRace && !p.Finished && p.LapsCompleted == 0,
                    gap = p.Finished && winner != null && winner.Finished ? p.FinishTime - winner.FinishTime : p.GapToLeader,
                    lapsDown = lapsDown
                });
            }
            return list;
        }

        private void OnBackendMessage(string type, string payload)
        {
            try
            {
                switch (type)
                {
                    case MsgType.TrackAnalysis:
                        var ta = JsonUtility.FromJson<TrackAnalysisMsg>(payload);
                        if (ta != null && ta.trackId == Config.trackId) AiModel.TryApplyBackend(ta.lineOffset, ta.speed);
                        break;
                    case MsgType.AiCommands:
                        var ac = JsonUtility.FromJson<AICommandsMsg>(payload);
                        if (ac == null || ac.sessionId != SessionId || ac.commands == null) break;
                        foreach (var c in ac.commands)
                        {
                            if (c.id < 0 || c.id >= Field.Count) continue;
                            var p = Field[c.id];
                            if (p.AI == null) continue;
                            if (!Enum.TryParse(c.mode, true, out TacticalMode mode)) mode = TacticalMode.Race;
                            p.AI.SetCommand(new AICommand
                            {
                                Mode = mode, SpeedScale = c.speedScale, LateralTarget = c.lateralTarget,
                                Aggression = c.aggression, ReceivedAt = Time.realtimeSinceStartup
                            });
                        }
                        break;
                    case MsgType.Standings:
                        var st = JsonUtility.FromJson<StandingsMsg>(payload);
                        if (st == null || st.sessionId != SessionId || st.entries == null) break;
                        _backendStandings.Clear();
                        foreach (var e in st.entries) _backendStandings[e.id] = e;
                        _lastBackendStandings = Time.realtimeSinceStartup;
                        break;
                    case MsgType.RaceSaved:
                        var rs = JsonUtility.FromJson<RaceSavedMsg>(payload);
                        if (rs != null) Debug.Log($"[Race] Backend saved race #{rs.raceId} (PB {rs.personalBest:0.000})");
                        break;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Race] Ignored malformed '{type}' message: {e.Message}");
            }
        }

        private void OnDestroy()
        {
            if (_backend != null)
            {
                _backend.MessageReceived -= OnBackendMessage;
                _backend.Connected -= OnBackendConnected;
            }
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using Apex.AI;
using Apex.Audio;
using Apex.Core;
using Apex.Rendering;
using Apex.Track;
using Apex.UI;
using Apex.Vehicle;
using UnityEngine;

namespace Apex.Race
{
    /// <summary>
    /// Race scene orchestrator: generates the circuit and environment, spawns the field, wires the
    /// HUD / pause / results screens and player input, and runs automated test sessions.
    /// </summary>
    public sealed class RaceDirector : MonoBehaviour
    {
        private static readonly (string name, string code, Personality personality)[] Rivals =
        {
            ("LUCA VARGA", "VAR", Personality.Aggressive),
            ("ELENA MOREAU", "MOR", Personality.Defensive),
            ("KENJI ASAHARA", "ASA", Personality.Balanced),
            ("SOFIA LINDQVIST", "LIN", Personality.Balanced),
        };

        private RaceConfig _cfg;
        private GameSettings _settings;
        private Transform _root;
        private TrackWorld _world;
        private RaceManager _race;
        private CameraRig _rig;
        private RaceHUD _hud;
        private PauseUI _pause;
        private ResultsUI _results;
        private SettingsUI _settingsUI;
        private AmbienceController _ambience;
        private bool _paused;
        private bool _started;
        private AutoTestOptions _auto;
        private AutoTestRecorder _recorder;

        private IEnumerator Start()
        {
            SceneReady.Progress = 0f;
            _cfg = RaceConfig.Current.Clone();
            _settings = GameServices.Instance.Settings;
            _auto = GameServices.Instance.AutoTest;
            ApexControls.Initialise();
            _root = new GameObject("RaceWorld").transform;
            int quality = _settings.qualityPreset;

            // ---------------- circuit
            var spline = TrackSpline.FromLayout(TrackLayout.ControlPoints, TrackLayout.TargetLength);
            var analysis = TrackAnalysis.Compute(spline, VehicleEnvelope.Gt3);
            SceneReady.Progress = 0.05f;
            yield return null;
            var builder = new TrackBuilder(spline, analysis, _root, quality);
            foreach (float p in builder.Build()) { SceneReady.Progress = p; yield return null; }
            _world = builder.World;
            var env = new EnvironmentBuilder(_world, _root, quality);
            foreach (float p in env.Build()) { SceneReady.Progress = p; yield return null; }
            SceneLighting.SetupRace(_root);

            // ---------------- race + field
            _race = gameObject.AddComponent<RaceManager>();
            var aiModel = new AITrackModel(spline, analysis);
            _race.Initialise(_cfg, _world, aiModel);
            SpawnField(aiModel);
            SceneReady.Progress = 0.97f;
            yield return null;

            Vector3 probePos = _world.GridSlots[0].position + Vector3.up * 12f;
            SceneLighting.AddReflectionProbe(_root, probePos);
            SceneLighting.AddTrackProbes(_root, _world);

            // ---------------- camera, audio, UI
            var cam = SceneLighting.CreateCamera(_root, "RaceCamera", _settings.fieldOfView);
            _rig = cam.gameObject.AddComponent<CameraRig>();
            _rig.Setup(cam, _world, _settings);
            _rig.SetTarget(_race.Player.Car, true);
            _ambience = _root.gameObject.AddComponent<AmbienceController>();
            _ambience.Setup(_world, _settings);
            BuildUI();
            _race.LightsOut += () => _ambience.Cheer(0.8f);
            _race.ParticipantFinished += OnFinished;
            _race.RaceComplete += () => StartCoroutine(ShowResults());
            if (_auto.Enabled) _recorder = new AutoTestRecorder(_race, _auto);
            SceneReady.Progress = 1f;

            // wait for the loading screen to clear
            while (GameServices.Instance.Loader.IsLoading) yield return null;
            _race.BeginGrid();
            _started = true;
            if (_cfg.mode == GameMode.QuickRace)
            {
                _rig.PlayIntro();
                if (_auto.Screenshots) { yield return new WaitForSeconds(1.2f); AutoTestCapture.Shot("04_grid"); }
                yield return new WaitForSeconds(3.2f);
                _race.StartCountdown();
                if (_auto.Screenshots) { yield return new WaitForSeconds(3.6f); AutoTestCapture.Shot("05_start_lights"); }
            }
            if (_auto.Enabled) StartCoroutine(AutoTestRoutine());
        }

        private void SpawnField(AITrackModel aiModel)
        {
            int opponents = _cfg.mode == GameMode.QuickRace ? Mathf.Clamp(_cfg.opponents, 1, 4) : 0;
            var raceRoot = new GameObject("Cars").transform;
            raceRoot.SetParent(_root, false);
            var playerSpec = CarSpec.Roster[Mathf.Clamp(_cfg.carIndex, 0, CarSpec.Roster.Length - 1)];

            for (int i = 0; i < opponents; i++)
            {
                var rival = Rivals[i];
                var liv = CarSpec.AiLiveries[i];
                var spec = CarSpec.Roster[(i + 1) % CarSpec.Roster.Length].WithColors(liv.body, liv.accent);
                var livery = new CarVisualBuilder.Livery { Body = liv.body, Accent = liv.accent, Trim = spec.TrimColor, Helmet = liv.accent, Number = liv.number, Team = liv.team };
                var car = CarFactory.Create(raceRoot, spec, livery, false, true, "AI_" + rival.code);
                var pose = _world.GridSlots[i];
                car.PlaceAt(pose.position, pose.rotation);
                car.AbsEnabled = true; car.TcsEnabled = true; car.SteerAssist = 0f;
                var p = new RaceParticipant
                {
                    Name = rival.name, Code = rival.code, Team = liv.team, CarName = spec.Name, Number = liv.number,
                    Personality = rival.personality, TeamColor = liv.body, Car = car
                };
                _race.AddParticipant(p);
                var profile = AIProfile.Create(rival.personality, _cfg.difficulty, 1000 + i * 77);
                p.AI = new AIDriver(aiModel, p, _race.Field, profile, i * 31 + 7) { RequestRespawn = _race.Respawn };
                car.Input = p.AI;
            }

            // player
            var pcar = CarFactory.Create(raceRoot, playerSpec, CarFactory.PlayerLivery(playerSpec), true, true, "Player");
            Pose ppose = _cfg.mode == GameMode.QuickRace ? _world.GridSlots[opponents] : _world.TimeTrialStart;
            pcar.PlaceAt(ppose.position, ppose.rotation);
            ApplyAssists(pcar);
            var player = new RaceParticipant
            {
                Name = "PLAYER", Code = "YOU", Team = "APEX WORKS", CarName = playerSpec.Name, Number = 7, IsPlayer = true,
                Personality = Personality.Balanced, TeamColor = UITheme.Red, Car = pcar
            };
            _race.AddParticipant(player);
            player.HumanInput = new PlayerCarInput(_settings);
            // autopilot used for the cool-down lap and automated tests
            var autoProfile = AIProfile.Create(Personality.Balanced, Difficulty.Hard, 4242);
            player.AI = null;
            var autopilot = new AIDriver(aiModel, player, _race.Field, autoProfile, 99) { RequestRespawn = _race.Respawn };
            _autopilot = autopilot;
            pcar.Input = _auto.Enabled ? (ICarInput)autopilot : player.HumanInput;
            if (_auto.Enabled) { autopilot.RaceActive = false; }
            if (_cfg.mode == GameMode.TimeTrial) { pcar.Body.linearVelocity = ppose.rotation * Vector3.forward * 20f; autopilot.RaceActive = true; }
        }

        private AIDriver _autopilot;

        private void ApplyAssists(CarController car)
        {
            car.AbsEnabled = _settings.abs;
            car.TcsEnabled = _settings.tcs;
            car.SteerAssist = _settings.steeringAssist ? 0.6f : 0f;
        }

        private void BuildUI()
        {
            var canvas = UIFactory.CreateCanvas("RaceCanvas", 20);
            _hud = RaceHUD.Create(canvas.transform, _race);
            _pause = PauseUI.Create(canvas.transform, _race);
            _results = ResultsUI.Create(canvas.transform, _race);
            _settingsUI = SettingsUI.Create(canvas.transform);
            _pause.Resume = () => SetPaused(false);
            _pause.Restart = Restart;
            _pause.MainMenu = ToMenu;
            _pause.OpenSettings = () => { _pause.Hide(); _settingsUI.Show(); };
            _pause.EndSession = () => { SetPaused(false); _race.CompleteRace(); };
            _settingsUI.Closed = () =>
            {
                ApplyAssists(_race.Player.Car);
                _ambience.ApplyVolume();
                _pause.Show();
            };
            _results.Restart = Restart;
            _results.MainMenu = ToMenu;
        }

        private void Restart()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            GameServices.Instance.Loader.Load(SceneNames.Race);
        }

        private void ToMenu()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            GameServices.Instance.Loader.Load(SceneNames.MainMenu);
        }

        private void SetPaused(bool paused)
        {
            if (_race.Phase == RacePhase.Results) return;
            _paused = paused;
            Time.timeScale = paused ? 0f : 1f;
            AudioListener.pause = paused;
            _hud.SetVisible(!paused);
            if (paused) _pause.Show();
            else { _pause.Hide(); _settingsUI.Hide(); }
        }

        private void Update()
        {
            if (!_started || _race == null) return;
            if (_race.Phase != RacePhase.Results && !_auto.Enabled)
            {
                if (ApexControls.Pause.WasPressedThisFrame() && !_settingsUI.Visible)
                {
                    if (!_paused) SetPaused(true);
                }
            }
            if (_paused) return;
            if (ApexControls.CameraNext.WasPressedThisFrame()) _rig.NextView();
            _rig.SetLookBack(ApexControls.LookBack.IsPressed());
            var player = _race.Player;
            if (ApexControls.ResetCar.WasPressedThisFrame() && (_race.Phase == RacePhase.Racing) && !player.Finished) _race.Respawn(player);
            if (_autopilot != null && player.Car.Input == _autopilot) _autopilot.RaceActive = _race.Phase == RacePhase.Racing || _race.Phase == RacePhase.Finished;
            _recorder?.Tick(Time.unscaledDeltaTime);
        }

        private void OnFinished(RaceParticipant p)
        {
            if (p.IsPlayer)
            {
                p.Car.Input = _autopilot;          // cool-down lap on autopilot
                _autopilot.RaceActive = true;
                StartCoroutine(FinishCamera());
                _ambience.Cheer(1f);
            }
        }

        private IEnumerator FinishCamera()
        {
            yield return new WaitForSeconds(2.5f);
            _rig.SetView(CameraView.TV);
        }

        private IEnumerator ShowResults()
        {
            yield return new WaitForSeconds(_cfg.mode == GameMode.QuickRace ? 2.5f : 0.3f);
            _hud.SetVisible(false);
            _results.Show();
            if (_auto.Enabled)
            {
                yield return new WaitForSecondsRealtime(1.5f);
                if (_auto.Screenshots) AutoTestCapture.Shot("09_results");
                _recorder.WriteReport();
                yield return new WaitForSecondsRealtime(1.5f);
#if UNITY_EDITOR
                UnityEditor.EditorApplication.Exit(0);
#else
                Application.Quit();
#endif
            }
        }

        private IEnumerator AutoTestRoutine()
        {
            // pause / resume round-trip
            while (_race.RaceTime < 12f && _race.Phase != RacePhase.Results) yield return null;
            if (_race.Phase != RacePhase.Results)
            {
                float before = _race.RaceTime;
                SetPaused(true);
                yield return new WaitForSecondsRealtime(1.5f);
                if (_auto.Screenshots) AutoTestCapture.Shot("05b_pause");
                yield return new WaitForSecondsRealtime(0.5f);
                bool frozen = Mathf.Abs(_race.RaceTime - before) < 0.05f;
                Debug.Log($"[AutoTest] pause froze race clock: {frozen}");
                if (!frozen) _recorder.Fail("pause did not freeze race time");
                SetPaused(false);
            }
            var shots = new[] { (CameraView.ChaseNear, "06_racing_chase"), (CameraView.TV, "07_racing_tv"), (CameraView.Hood, "08_racing_hood") };
            float[] at = { 22f, 40f, 55f };
            for (int i = 0; i < shots.Length; i++)
            {
                while (_race.RaceTime < at[i] && _race.Phase != RacePhase.Results) yield return null;
                if (_race.Phase == RacePhase.Results) break;
                if (_auto.Screenshots)
                {
                    _rig.SetView(shots[i].Item1);
                    yield return new WaitForSeconds(1.2f);
                    AutoTestCapture.Shot(shots[i].Item2);
                    _rig.SetView(CameraView.ChaseNear);
                }
            }
            // close-up of the photoscanned street props (verifies scale / orientation / materials)
            if (_auto.Screenshots && _race.Phase != RacePhase.Results)
            {
                var lamp = GameObject.Find(Apex.Rendering.PropLibrary.Lamp);
                Debug.Log($"[AutoTest] props spawned: {Apex.Rendering.PropLibrary.Spawned}");
                if (lamp != null)
                {
                    _rig.enabled = false;
                    var camT = _rig.transform;
                    Vector3 lp = lamp.transform.position;
                    camT.position = lp - lamp.transform.forward * 7f + lamp.transform.right * 3f + Vector3.up * 1.8f;
                    camT.rotation = Quaternion.LookRotation(lp + Vector3.up * 2.2f - camT.position, Vector3.up);
                    yield return new WaitForSeconds(0.8f);
                    AutoTestCapture.Shot("08b_street_props");
                    yield return new WaitForSeconds(0.3f);
                    _rig.enabled = true;
                }
            }
            if (_cfg.mode == GameMode.TimeTrial)
            {
                while (_race.Player.LapsCompleted < _auto.Laps) yield return null;
                yield return new WaitForSeconds(1f);
                _race.CompleteRace();
            }
            // hard timeout so a stuck run cannot hang the test harness
            float limit = 240f * _auto.Laps + 120f;
            while (_race.Phase != RacePhase.Results && _race.RaceTime < limit) yield return null;
            if (_race.Phase != RacePhase.Results)
            {
                Debug.LogError("[AutoTest] Session timed out");
                _recorder.Fail("timeout");
                _race.CompleteRace();
            }
        }
    }
}

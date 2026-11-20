using System;
using Apex.Audio;
using Apex.Net;
using Apex.Race;
using Apex.Rendering;
using Apex.UI;
using UnityEngine;

namespace Apex.Core
{
    /// <summary>
    /// Persistent service locator (lives across scene loads). Created on demand by GameBootstrap.
    /// </summary>
    public sealed class GameServices : MonoBehaviour
    {
        public static GameServices Instance { get; private set; }

        public GameSettings Settings { get; private set; }
        public BackendClient Backend { get; private set; }
        public BackendLauncher Launcher { get; private set; }
        public RecordsStore Records { get; private set; }
        public SceneLoader Loader { get; private set; }
        public AudioLibrary AudioLib { get; private set; }
        public AutoTestOptions AutoTest { get; private set; }

        public event Action SettingsChanged;

        public static GameServices Ensure()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("ApexServices");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<GameServices>();
            Instance.Initialise();
            return Instance;
        }

        private void Initialise()
        {
            Application.targetFrameRate = 144;
            Time.fixedDeltaTime = 0.01f;           // 100 Hz physics for stable WheelColliders
            Physics.defaultSolverIterations = 10;
            Physics.defaultSolverVelocityIterations = 4;

            AutoTest = AutoTestOptions.FromCommandLine();
            if (AutoTest.Enabled) gameObject.AddComponent<AutoTestWatchdog>().Begin(AutoTest);
            Settings = GameSettings.Load();
            if (AutoTest.Enabled && AutoTest.Quality >= 0) Settings.qualityPreset = AutoTest.Quality;
            Records = new RecordsStore();
            AudioLib = new AudioLibrary();

            UITheme.Initialise();
            GraphicsQuality.Apply(Settings);

            Loader = gameObject.AddComponent<SceneLoader>();
            Backend = gameObject.AddComponent<BackendClient>();
            Launcher = gameObject.AddComponent<BackendLauncher>();
            Launcher.Begin(Backend);
        }

        public void ApplySettings()
        {
            Settings.Save();
            GraphicsQuality.Apply(Settings);
            AudioListener.volume = Settings.masterVolume;
            SettingsChanged?.Invoke();
        }

        private void OnApplicationQuit()
        {
            Settings?.Save();
        }
    }

    /// <summary>Command-line driven automated test run (used in Phase 9).</summary>
    public sealed class AutoTestOptions
    {
        public bool Enabled;
        public bool Screenshots;
        public string OutputDir;
        public GameMode Mode = GameMode.QuickRace;
        public int Laps = 1;
        public Difficulty Difficulty = Difficulty.Hard;
        public int Quality = -1;
        public Apex.Vehicle.CarClass CarClass = Apex.Vehicle.CarClass.GT3;

        public static AutoTestOptions FromCommandLine()
        {
            var o = new AutoTestOptions();
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                if (a == "-apexAutotest") o.Enabled = true;
                else if (a == "-apexScreens") o.Screenshots = true;
                else if (a == "-apexOut" && i + 1 < args.Length) o.OutputDir = args[++i];
                else if (a == "-apexLaps" && i + 1 < args.Length && int.TryParse(args[++i], out int l)) o.Laps = Mathf.Clamp(l, 1, 10);
                else if (a == "-apexTimeTrial") o.Mode = GameMode.TimeTrial;
                else if (a == "-apexClass" && i + 1 < args.Length && Enum.TryParse(args[++i], true, out Apex.Vehicle.CarClass cc)) o.CarClass = cc;
                else if (a == "-apexQuality" && i + 1 < args.Length && int.TryParse(args[++i], out int q)) o.Quality = Mathf.Clamp(q, 0, 3);
                else if (a == "-apexDifficulty" && i + 1 < args.Length && Enum.TryParse(args[++i], true, out Difficulty d)) o.Difficulty = d;
            }
            if (string.IsNullOrEmpty(o.OutputDir)) o.OutputDir = System.IO.Path.Combine(Application.persistentDataPath, "autotest");
            return o;
        }
    }
}

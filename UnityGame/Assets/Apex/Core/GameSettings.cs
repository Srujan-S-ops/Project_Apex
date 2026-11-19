using System;
using UnityEngine;

namespace Apex.Core
{
    public enum CameraView { ChaseFar, ChaseNear, Hood, Bumper, TV }
    public enum SpeedUnit { Kmh, Mph }

    /// <summary>Persistent player settings (PlayerPrefs JSON).</summary>
    [Serializable]
    public sealed class GameSettings
    {
        private const string Key = "apex.settings.v1";

        // Graphics
        public int qualityPreset = 2;       // 0 Low, 1 Medium, 2 High, 3 Ultra
        public int resolutionWidth;
        public int resolutionHeight;
        public bool fullscreen = true;
        public bool vsync = true;
        public bool postProcessing = true;

        // Audio (0..1)
        public float masterVolume = 0.9f;
        public float engineVolume = 0.85f;
        public float effectsVolume = 0.8f;
        public float ambienceVolume = 0.6f;
        public float musicVolume = 0.5f;

        // Controls
        public float steeringSensitivity = 1f;  // 0.5..1.5
        public float steeringLinearity = 1.3f;  // 1..2
        public bool steeringAssist = true;

        // Camera
        public CameraView defaultCamera = CameraView.ChaseNear;
        public float fieldOfView = 62f;
        public bool cameraShake = true;

        // Gameplay
        public bool abs = true;
        public bool tcs = true;
        public SpeedUnit units = SpeedUnit.Kmh;
        public bool showMinimap = true;
        public bool showLeaderboard = true;

        public static GameSettings Load()
        {
            try
            {
                string json = PlayerPrefs.GetString(Key, string.Empty);
                if (!string.IsNullOrEmpty(json)) return JsonUtility.FromJson<GameSettings>(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Settings] Failed to load settings, using defaults: {e.Message}");
            }
            return new GameSettings();
        }

        public void Save()
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(this));
            PlayerPrefs.Save();
        }

        public float SpeedFactor => units == SpeedUnit.Kmh ? 3.6f : 2.23694f;
        public string SpeedLabel => units == SpeedUnit.Kmh ? "KM/H" : "MPH";
    }
}

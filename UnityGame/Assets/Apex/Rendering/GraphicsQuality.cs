using Apex.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Apex.Rendering
{
    /// <summary>Applies the settings' graphics preset to the active URP asset and the screen.</summary>
    public static class GraphicsQuality
    {
        public static readonly string[] PresetNames = { "LOW", "MEDIUM", "HIGH", "ULTRA" };

        public static void Apply(GameSettings s)
        {
            var asset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (asset != null)
            {
                switch (s.qualityPreset)
                {
                    case 0:
                        asset.renderScale = 0.8f; asset.shadowDistance = 90f; asset.msaaSampleCount = 1; asset.shadowCascadeCount = 1;
                        break;
                    case 1:
                        asset.renderScale = 1f; asset.shadowDistance = 140f; asset.msaaSampleCount = 2; asset.shadowCascadeCount = 2;
                        break;
                    case 2:
                        asset.renderScale = 1f; asset.shadowDistance = 220f; asset.msaaSampleCount = 4; asset.shadowCascadeCount = 4;
                        break;
                    default:
                        asset.renderScale = 1f; asset.shadowDistance = 320f; asset.msaaSampleCount = 8; asset.shadowCascadeCount = 4;
                        break;
                }
            }
            QualitySettings.vSyncCount = s.vsync ? 1 : 0;
            QualitySettings.lodBias = 0.8f + s.qualityPreset * 0.4f;

            if (!Application.isEditor)
            {
                int w = s.resolutionWidth > 0 ? s.resolutionWidth : Screen.currentResolution.width;
                int h = s.resolutionHeight > 0 ? s.resolutionHeight : Screen.currentResolution.height;
                var mode = s.fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
                if (Screen.width != w || Screen.height != h || Screen.fullScreenMode != mode)
                    Screen.SetResolution(w, h, mode);
            }
        }

        /// <summary>Particle / environment density multiplier for the preset.</summary>
        public static float Detail(GameSettings s) => 0.5f + s.qualityPreset * 0.25f;
    }
}

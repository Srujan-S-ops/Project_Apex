using System.Reflection;
using Apex.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Apex.Rendering
{
    /// <summary>
    /// Graphics presets. LOW: reduced shadows, half-res textures, minimal post. MEDIUM: better shadows,
    /// moderate post. HIGH: full-res textures, 4 cascades, SSAO, track reflection probes, full post incl.
    /// speed motion blur. ULTRA: 8x MSAA, longest shadows, more/higher-res probes, HQ post.
    /// </summary>
    public static class GraphicsQuality
    {
        public static readonly string[] PresetNames = { "LOW", "MEDIUM", "HIGH", "ULTRA" };

        public static int Current { get; private set; } = 2;
        public static int ProbeResolution => Current >= 3 ? 256 : (Current >= 1 ? 128 : 64);
        public static int TrackProbeCount => Current >= 3 ? 14 : (Current == 2 ? 10 : (Current == 1 ? 5 : 0));
        public static bool SsaoEnabled => Current >= 2;

        public static void Apply(GameSettings s)
        {
            Current = Mathf.Clamp(s.qualityPreset, 0, 3);
            var asset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (asset != null)
            {
                switch (Current)
                {
                    case 0:
                        asset.renderScale = 0.85f; asset.shadowDistance = 90f; asset.msaaSampleCount = 1; asset.shadowCascadeCount = 1;
                        break;
                    case 1:
                        asset.renderScale = 1f; asset.shadowDistance = 150f; asset.msaaSampleCount = 2; asset.shadowCascadeCount = 2;
                        break;
                    case 2:
                        asset.renderScale = 1f; asset.shadowDistance = 230f; asset.msaaSampleCount = 4; asset.shadowCascadeCount = 4;
                        break;
                    default:
                        asset.renderScale = 1f; asset.shadowDistance = 340f; asset.msaaSampleCount = 8; asset.shadowCascadeCount = 4;
                        break;
                }
                SetSsao(asset, SsaoEnabled);
            }
            QualitySettings.globalTextureMipmapLimit = Current == 0 ? 1 : 0;
            QualitySettings.anisotropicFiltering = Current >= 2 ? AnisotropicFiltering.ForceEnable : AnisotropicFiltering.Enable;
            QualitySettings.vSyncCount = s.vsync ? 1 : 0;
            QualitySettings.lodBias = 0.8f + Current * 0.4f;
            QualitySettings.softParticles = Current >= 2;

            if (!Application.isEditor)
            {
                int w = s.resolutionWidth > 0 ? s.resolutionWidth : Screen.currentResolution.width;
                int h = s.resolutionHeight > 0 ? s.resolutionHeight : Screen.currentResolution.height;
                var mode = s.fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
                if (Screen.width != w || Screen.height != h || Screen.fullScreenMode != mode)
                    Screen.SetResolution(w, h, mode);
            }
        }

        /// <summary>Enables/disables URP's SSAO renderer feature on the active renderer(s).</summary>
        private static void SetSsao(UniversalRenderPipelineAsset asset, bool on)
        {
            var field = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList", BindingFlags.NonPublic | BindingFlags.Instance);
            if (!(field?.GetValue(asset) is ScriptableRendererData[] list)) return;
            foreach (var data in list)
            {
                if (data == null) continue;
                foreach (var f in data.rendererFeatures)
                    if (f != null && f.GetType().Name == "ScreenSpaceAmbientOcclusion") f.SetActive(on);
            }
        }

        /// <summary>Particle / environment density multiplier for the preset.</summary>
        public static float Detail(GameSettings s) => 0.5f + s.qualityPreset * 0.25f;
    }
}

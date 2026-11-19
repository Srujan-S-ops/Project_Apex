using Apex.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Apex.Rendering
{
    /// <summary>Golden-hour race lighting, showroom lighting, sky, fog, reflections and post-processing.</summary>
    public static class SceneLighting
    {
        public static Light SetupRace(Transform root)
        {
            var sky = Resources.Load<Material>("Materials/M_Skybox");
            if (sky != null)
            {
                sky = new Material(sky);
                sky.SetFloat("_SunSize", 0.035f);
                sky.SetFloat("_SunSizeConvergence", 6f);
                sky.SetFloat("_AtmosphereThickness", 1.15f);
                sky.SetColor("_SkyTint", new Color(0.52f, 0.58f, 0.72f));
                sky.SetColor("_GroundColor", new Color(0.36f, 0.33f, 0.31f));
                sky.SetFloat("_Exposure", 1.25f);
                RenderSettings.skybox = sky;
            }
            var sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(root, false);
            sunGo.transform.rotation = Quaternion.Euler(16f, -62f, 0f);
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.83f, 0.64f);
            sun.intensity = 2.1f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;
            sun.shadowBias = 0.04f;
            sun.shadowNormalBias = 0.35f;
            RenderSettings.sun = sun;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.5f, 0.56f, 0.7f);
            RenderSettings.ambientEquatorColor = new Color(0.62f, 0.52f, 0.46f);
            RenderSettings.ambientGroundColor = new Color(0.2f, 0.18f, 0.17f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.78f, 0.66f, 0.58f);
            RenderSettings.fogStartDistance = 280f;
            RenderSettings.fogEndDistance = 2200f;
            RenderSettings.reflectionIntensity = 1f;

            AddVolume(root, false);
            return sun;
        }

        public static ReflectionProbe AddReflectionProbe(Transform root, Vector3 position)
        {
            var go = new GameObject("ReflectionProbe");
            go.transform.SetParent(root, false);
            go.transform.position = position;
            var probe = go.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            probe.size = new Vector3(6000f, 800f, 6000f);
            probe.resolution = 256;
            probe.hdr = true;
            probe.boxProjection = false;
            probe.importance = 1;
            probe.nearClipPlane = 1f;
            probe.farClipPlane = 2500f;
            probe.RenderProbe();
            return probe;
        }

        public static void SetupShowroom(Transform root)
        {
            var sky = Resources.Load<Material>("Materials/M_Skybox");
            if (sky != null)
            {
                sky = new Material(sky);
                sky.SetFloat("_Exposure", 0.12f);
                sky.SetColor("_SkyTint", new Color(0.2f, 0.2f, 0.24f));
                sky.SetFloat("_SunSize", 0f);
                RenderSettings.skybox = sky;
            }
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.07f, 0.07f, 0.085f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.018f;
            RenderSettings.fogColor = new Color(0.02f, 0.02f, 0.025f);
            RenderSettings.sun = null;

            // key, rim and fill spots plus a red accent wash
            Spot(root, "Key", new Vector3(4.5f, 7f, 4f), new Color(1f, 0.97f, 0.92f), 75f, 38f, true);
            Spot(root, "Rim", new Vector3(-5f, 5f, -5.5f), new Color(0.75f, 0.85f, 1f), 55f, 30f, false);
            Spot(root, "Fill", new Vector3(-5.5f, 4f, 4.5f), new Color(0.95f, 0.92f, 0.9f), 22f, 18f, false);
            Spot(root, "Top", new Vector3(0f, 9f, 0f), new Color(1f, 1f, 1f), 40f, 18f, false);
            var accent = Spot(root, "Accent", new Vector3(0f, 3f, -9f), new Color(1f, 0.08f, 0.08f), 60f, 18f, false);
            accent.spotAngle = 90f;
            AddVolume(root, true);
        }

        private static Light Spot(Transform root, string name, Vector3 pos, Color c, float intensity, float range, bool shadows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.position = pos;
            go.transform.LookAt(new Vector3(0f, 0.6f, 0f));
            var l = go.AddComponent<Light>();
            l.type = LightType.Spot;
            l.color = c;
            l.intensity = intensity;
            l.range = range;
            l.spotAngle = 55f;
            l.innerSpotAngle = 30f;
            l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            return l;
        }

        public static Volume AddVolume(Transform root, bool showroom)
        {
            var go = new GameObject("PostFX");
            go.transform.SetParent(root, false);
            var vol = go.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 10f;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(showroom ? 0.9f : 1.05f);
            bloom.intensity.Override(showroom ? 0.9f : 0.55f);
            bloom.scatter.Override(0.68f);
            var ca = profile.Add<ColorAdjustments>(true);
            ca.postExposure.Override(showroom ? 0.35f : 0.25f);
            ca.contrast.Override(showroom ? 18f : 12f);
            ca.saturation.Override(showroom ? 4f : 8f);
            var vig = profile.Add<Vignette>(true);
            vig.intensity.Override(showroom ? 0.38f : 0.22f);
            vig.smoothness.Override(0.45f);
            var smh = profile.Add<ShadowsMidtonesHighlights>(true);
            smh.shadows.Override(new Vector4(0.95f, 0.98f, 1.06f, 0f));
            smh.highlights.Override(new Vector4(1.04f, 1.0f, 0.95f, 0f));
            if (showroom)
            {
                var dof = profile.Add<DepthOfField>(true);
                dof.mode.Override(DepthOfFieldMode.Bokeh);
                dof.focusDistance.Override(6.5f);
                dof.focalLength.Override(55f);
                dof.aperture.Override(4f);
            }
            else
            {
                var mb = profile.Add<MotionBlur>(true);
                mb.intensity.Override(0.18f);
                mb.quality.Override(MotionBlurQuality.Low);
            }
            vol.sharedProfile = profile;
            vol.enabled = GameServices.Instance == null || GameServices.Instance.Settings.postProcessing;
            return vol;
        }

        public static Camera CreateCamera(Transform root, string name, float fov)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.tag = "MainCamera";
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.08f;
            cam.farClipPlane = 3000f;
            var data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            go.AddComponent<AudioListener>();
            return cam;
        }
    }
}

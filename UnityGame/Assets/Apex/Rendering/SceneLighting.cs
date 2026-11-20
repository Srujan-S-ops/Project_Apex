using System.Collections.Generic;
using Apex.Core;
using Apex.Track;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Apex.Rendering
{
    /// <summary>
    /// Cinematic daytime race lighting, showroom lighting, sky, atmospheric fog, reflection probes and the
    /// post-processing stack (tuned per graphics preset).
    /// </summary>
    public static class SceneLighting
    {
        /// <summary>Race motion blur override (driven by camera speed in CameraRig).</summary>
        public static MotionBlur RaceMotionBlur { get; private set; }

        public static Light SetupRace(Transform root)
        {
            var sky = Resources.Load<Material>("Materials/M_Skybox");
            if (sky != null)
            {
                sky = new Material(sky);
                sky.SetFloat("_SunSize", 0.03f);
                sky.SetFloat("_SunSizeConvergence", 7f);
                sky.SetFloat("_AtmosphereThickness", 1.1f);
                sky.SetColor("_SkyTint", new Color(0.46f, 0.52f, 0.64f));
                sky.SetColor("_GroundColor", new Color(0.42f, 0.41f, 0.40f));
                sky.SetFloat("_Exposure", 1.35f);
                RenderSettings.skybox = sky;
            }
            var sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(root, false);
            // late-afternoon sun: high enough for punchy contrast, low enough for long shadows across the streets
            sunGo.transform.rotation = Quaternion.Euler(31f, -58f, 0f);
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.9f, 0.78f);
            sun.intensity = 2.6f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.92f;
            sun.shadowBias = 0.03f;
            sun.shadowNormalBias = 0.3f;
            RenderSettings.sun = sun;

            // sky-driven ambient lighting (spherical harmonics from the procedural sky)
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1.05f;
            DynamicGI.UpdateEnvironment();

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.66f, 0.71f, 0.78f);
            RenderSettings.fogStartDistance = 220f;
            RenderSettings.fogEndDistance = 1900f;
            RenderSettings.reflectionIntensity = 1f;

            AddVolume(root, false);
            return sun;
        }

        public static ReflectionProbe AddReflectionProbe(Transform root, Vector3 position)
        {
            var probe = MakeProbe(root, "ReflectionProbe_Global", position, new Vector3(6000f, 800f, 6000f), GraphicsQuality.ProbeResolution, false);
            probe.importance = 0;
            probe.RenderProbe();
            return probe;
        }

        /// <summary>
        /// Box-projected local probes along the circuit so cars and glass reflect the street they are in
        /// (blended by URP Forward+). Rendered once at load.
        /// </summary>
        public static List<ReflectionProbe> AddTrackProbes(Transform root, TrackWorld world)
        {
            var probes = new List<ReflectionProbe>();
            int count = GraphicsQuality.TrackProbeCount;
            if (count <= 0) return probes;
            var s = world.Spline;
            float step = s.Length / count;
            for (int k = 0; k < count; k++)
            {
                s.Sample(k * step, out var p, out var f, out _, out _);
                var probe = MakeProbe(root, "ReflectionProbe_" + k, p + Vector3.up * 3.5f, new Vector3(140f, 60f, 140f), GraphicsQuality.ProbeResolution, true);
                probe.importance = 1;
                probe.blendDistance = 25f;
                probe.RenderProbe();
                probes.Add(probe);
            }
            return probes;
        }

        private static ReflectionProbe MakeProbe(Transform root, string name, Vector3 position, Vector3 size, int resolution, bool boxProjection)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.position = position;
            var probe = go.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            probe.size = size;
            probe.resolution = resolution;
            probe.hdr = true;
            probe.boxProjection = boxProjection;
            probe.nearClipPlane = 0.5f;
            probe.farClipPlane = 2500f;
            probe.shadowDistance = 120f;
            return probe;
        }

        // ------------------------------------------------------------------ showroom

        public static void SetupShowroom(Transform root)
        {
            var sky = Resources.Load<Material>("Materials/M_Skybox");
            if (sky != null)
            {
                sky = new Material(sky);
                sky.SetFloat("_Exposure", 0.06f);
                sky.SetColor("_SkyTint", new Color(0.2f, 0.2f, 0.24f));
                sky.SetFloat("_SunSize", 0f);
                RenderSettings.skybox = sky;
            }
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.045f, 0.048f, 0.058f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.022f;
            RenderSettings.fogColor = new Color(0.012f, 0.013f, 0.016f);
            RenderSettings.sun = null;

            // commercial three-point setup with a long overhead softbox and coloured rim accents
            Spot(root, "Key", new Vector3(5.5f, 7.5f, 4.5f), new Color(1f, 0.96f, 0.9f), 95f, 40f, true, 50f);
            Spot(root, "KeyFill", new Vector3(-6f, 6.5f, 5f), new Color(0.85f, 0.9f, 1f), 30f, 30f, false, 60f);
            Spot(root, "RimCool", new Vector3(-6.5f, 3.5f, -6.5f), new Color(0.55f, 0.75f, 1f), 70f, 26f, false, 45f);
            Spot(root, "RimRed", new Vector3(6.5f, 2.5f, -7f), new Color(1f, 0.1f, 0.12f), 85f, 24f, false, 50f);
            Spot(root, "Top", new Vector3(0f, 9.5f, 0.2f), Color.white, 55f, 18f, false, 70f);
            Spot(root, "Ground", new Vector3(0f, 0.6f, 8.5f), new Color(0.9f, 0.92f, 1f), 14f, 14f, false, 70f);
            AddVolume(root, true);
        }

        private static Light Spot(Transform root, string name, Vector3 pos, Color c, float intensity, float range, bool shadows, float angle)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.position = pos;
            go.transform.LookAt(new Vector3(0f, 0.55f, 0f));
            var l = go.AddComponent<Light>();
            l.type = LightType.Spot;
            l.color = c;
            l.intensity = intensity;
            l.range = range;
            l.spotAngle = angle;
            l.innerSpotAngle = angle * 0.45f;
            l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            if (shadows) l.shadowResolution = LightShadowResolution.VeryHigh;
            return l;
        }

        /// <summary>Attaches the planar reflection (mirror camera + additive reflection layer) to the showroom floor.</summary>
        public static void AddShowroomReflection(Transform root, Camera source, float radius)
        {
            var mat = new Material(MaterialLibrary.Template("M_PlanarReflection"));
            mat.SetFloat("_Strength", 0.75f);
            mat.SetFloat("_FadeRadius", radius);
            var mb = new MeshBuilder();
            float r = radius;
            // just above the (flush) turntable so the reflection shows directly beneath the car
            mb.AddQuad(new Vector3(-r, 0.009f, -r), new Vector3(-r, 0.009f, r), new Vector3(r, 0.009f, r), new Vector3(r, 0.009f, -r));
            var quad = mb.Build("ReflectionLayer", root, mat, false);
            quad.layer = 2;
            var pr = quad.AddComponent<PlanarReflection>();
            pr.Source = source;
            pr.PlaneHeight = 0f;
            pr.Resolution = GraphicsQuality.Current >= 2 ? 1280 : 768;
            pr.Target = mat;
        }

        // ------------------------------------------------------------------ post-processing

        public static Volume AddVolume(Transform root, bool showroom)
        {
            int q = GraphicsQuality.Current;
            var go = new GameObject("PostFX");
            go.transform.SetParent(root, false);
            var vol = go.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 10f;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();

            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(showroom ? 1.0f : 1.15f);
            bloom.intensity.Override(showroom ? 0.55f : 0.32f);
            bloom.scatter.Override(0.65f);
            bloom.highQualityFiltering.Override(q >= 3);
            var ca = profile.Add<ColorAdjustments>(true);
            ca.postExposure.Override(showroom ? 0.45f : 0.12f);
            ca.contrast.Override(showroom ? 20f : 16f);
            ca.saturation.Override(showroom ? 2f : -6f);

            if (q >= 1)
            {
                // NFS-style grade: cool, slightly teal shadows and warm highlights, never oversaturated
                var split = profile.Add<SplitToning>(true);
                split.shadows.Override(new Color(0.36f, 0.5f, 0.58f));
                split.highlights.Override(new Color(0.9f, 0.76f, 0.58f));
                split.balance.Override(-18f);
                var wb = profile.Add<WhiteBalance>(true);
                wb.temperature.Override(showroom ? -4f : 4f);
                var smh = profile.Add<ShadowsMidtonesHighlights>(true);
                smh.shadows.Override(new Vector4(0.96f, 0.99f, 1.05f, -0.02f));
                smh.highlights.Override(new Vector4(1.03f, 1.0f, 0.96f, 0f));
                var vig = profile.Add<Vignette>(true);
                vig.intensity.Override(showroom ? 0.36f : 0.2f);
                vig.smoothness.Override(0.5f);
            }
            if (q >= 2)
            {
                var grain = profile.Add<FilmGrain>(true);
                grain.type.Override(FilmGrainLookup.Thin1);
                grain.intensity.Override(0.12f);
                grain.response.Override(0.85f);
                var chroma = profile.Add<ChromaticAberration>(true);
                chroma.intensity.Override(showroom ? 0.06f : 0.05f);
            }
            if (showroom)
            {
                var dof = profile.Add<DepthOfField>(true);
                dof.mode.Override(q >= 2 ? DepthOfFieldMode.Bokeh : DepthOfFieldMode.Gaussian);
                dof.focusDistance.Override(7.2f);
                dof.focalLength.Override(58f);
                dof.aperture.Override(3.2f);
                dof.gaussianStart.Override(9f);
                dof.gaussianEnd.Override(22f);
                RaceMotionBlur = null;
            }
            else if (q >= 2)
            {
                var mb = profile.Add<MotionBlur>(true);
                mb.mode.Override(MotionBlurMode.CameraOnly);
                mb.quality.Override(q >= 3 ? MotionBlurQuality.High : MotionBlurQuality.Medium);
                mb.intensity.Override(0f);
                mb.clamp.Override(0.04f);
                RaceMotionBlur = mb;
            }
            else RaceMotionBlur = null;

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
            data.antialiasingQuality = GraphicsQuality.Current >= 2 ? AntialiasingQuality.High : AntialiasingQuality.Medium;
            data.renderShadows = true;
            data.requiresDepthTexture = true;
            go.AddComponent<AudioListener>();
            return cam;
        }
    }
}

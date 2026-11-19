using System;
using System.IO;
using System.Linq;
using Apex.Core;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Apex.EditorTools
{
    /// <summary>
    /// One-click project configuration and Windows build. Usable from the menu or in batch mode:
    ///   Unity -batchmode -quit -projectPath UnityGame -executeMethod Apex.EditorTools.ApexBuildPipeline.SetupProject
    ///   Unity -batchmode -quit -projectPath UnityGame -executeMethod Apex.EditorTools.ApexBuildPipeline.BuildWindows
    /// </summary>
    public static class ApexBuildPipeline
    {
        private const string ResMaterials = "Assets/Resources/Materials";
        private const string ResTextures = "Assets/Resources/Textures";
        private const string ResUI = "Assets/Resources/UI";
        private const string Scenes = "Assets/Scenes";

        [MenuItem("Apex/Setup Project")]
        public static void SetupProject()
        {
            try
            {
                EnsureFolder(ResMaterials);
                EnsureFolder(ResTextures);
                EnsureFolder(ResUI);
                EnsureFolder(Scenes);
                AssetDatabase.Refresh();
                ConfigureTextures();
                CreateTemplateMaterials();
                ConfigureRenderPipeline();
                ConfigurePlayer();
                ConfigureAudioAndTime();
                CreateScenes();
                AssetDatabase.SaveAssets();
                Debug.Log("[ApexBuild] Project setup complete.");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        [MenuItem("Apex/Build Windows")]
        public static void BuildWindows()
        {
            SetupProject();
            string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Build", "Windows"));
            Directory.CreateDirectory(outDir);
            var options = new BuildPlayerOptions
            {
                scenes = new[] { $"{Scenes}/MainMenu.unity", $"{Scenes}/Race.unity" },
                locationPathName = Path.Combine(outDir, "ProjectApex.exe"),
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            var s = report.summary;
            Debug.Log($"[ApexBuild] Build {s.result}: {s.totalSize / (1024 * 1024)} MB, {s.totalErrors} errors, {s.totalWarnings} warnings, {s.totalTime}");
            if (s.result != BuildResult.Succeeded && Application.isBatchMode) EditorApplication.Exit(1);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        // ------------------------------------------------------------------ textures

        private static void ConfigureTextures()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ResTextures, ResUI }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var imp = (TextureImporter)AssetImporter.GetAtPath(path);
                if (imp == null) continue;
                bool changed = false;
                string name = Path.GetFileNameWithoutExtension(path);
                bool isNormal = name.EndsWith("_n");
                bool isUi = path.StartsWith(ResUI);
                var type = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                if (imp.textureType != type) { imp.textureType = type; changed = true; }
                bool mips = !isUi;
                if (imp.mipmapEnabled != mips) { imp.mipmapEnabled = mips; changed = true; }
                int aniso = isUi ? 1 : 8;
                if (imp.anisoLevel != aniso) { imp.anisoLevel = aniso; changed = true; }
                bool alpha = isUi || name is "fence" or "crowd" or "palm" or "sponsors" or "digits" or "smoke" or "spark" or "flame" or "skid";
                if (imp.alphaIsTransparency != alpha) { imp.alphaIsTransparency = alpha; changed = true; }
                var wrap = isUi || name is "sponsors" or "digits" or "brakeboards" or "logo_sign" or "timing_tower" or "banner_start" ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
                if (imp.wrapMode != wrap) { imp.wrapMode = wrap; changed = true; }
                int max = isUi ? 2048 : 2048;
                if (imp.maxTextureSize != max) { imp.maxTextureSize = max; changed = true; }
                if (isUi && imp.textureCompression != TextureImporterCompression.Uncompressed) { imp.textureCompression = TextureImporterCompression.Uncompressed; changed = true; }
                if (changed) imp.SaveAndReimport();
            }
        }

        // ------------------------------------------------------------------ materials

        private static Material MakeMaterial(string name, string shaderName, Action<Material> configure)
        {
            string path = $"{ResMaterials}/{name}.mat";
            var shader = Shader.Find(shaderName);
            if (shader == null) throw new Exception($"Shader not found: {shaderName}");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            m.shaderKeywords = Array.Empty<string>();
            configure(m);
            EditorUtility.SetDirty(m);
            return m;
        }

        private static void CreateTemplateMaterials()
        {
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ResTextures}/asphalt_n.png");
            var white = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ResTextures}/concrete.png");
            MakeMaterial("M_Lit", "Universal Render Pipeline/Lit", m =>
            {
                m.SetFloat("_Surface", 0f);
                m.SetTexture("_BaseMap", white);
            });
            MakeMaterial("M_LitNormal", "Universal Render Pipeline/Lit", m =>
            {
                m.SetTexture("_BaseMap", white);
                m.SetTexture("_BumpMap", normal);
                m.EnableKeyword("_NORMALMAP");
            });
            MakeMaterial("M_LitEmissive", "Universal Render Pipeline/Lit", m =>
            {
                m.SetTexture("_BaseMap", white);
                m.SetColor("_EmissionColor", Color.white);
                m.SetTexture("_EmissionMap", white);
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            });
            MakeMaterial("M_LitCutout", "Universal Render Pipeline/Lit", m =>
            {
                m.SetTexture("_BaseMap", white);
                m.SetFloat("_AlphaClip", 1f);
                m.SetFloat("_Cutoff", 0.5f);
                m.SetFloat("_Cull", 0f);
                m.EnableKeyword("_ALPHATEST_ON");
                m.renderQueue = (int)RenderQueue.AlphaTest;
                m.SetOverrideTag("RenderType", "TransparentCutout");
            });
            MakeMaterial("M_LitTransparent", "Universal Render Pipeline/Lit", m =>
            {
                SetTransparent(m, false);
            });
            MakeMaterial("M_Unlit", "Universal Render Pipeline/Unlit", m =>
            {
                m.SetTexture("_BaseMap", white);
            });
            MakeMaterial("M_ParticleAlpha", "Universal Render Pipeline/Particles/Unlit", m =>
            {
                SetTransparent(m, false);
                m.SetFloat("_Cull", 0f);
                m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{ResTextures}/smoke.png"));
            });
            MakeMaterial("M_ParticleAdditive", "Universal Render Pipeline/Particles/Unlit", m =>
            {
                SetTransparent(m, true);
                m.SetFloat("_Cull", 0f);
                m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{ResTextures}/spark.png"));
            });
            MakeMaterial("M_Skybox", "Skybox/Procedural", m =>
            {
                m.SetFloat("_SunDisk", 2f);
                m.SetFloat("_SunSize", 0.035f);
                m.SetFloat("_AtmosphereThickness", 1.1f);
                m.SetFloat("_Exposure", 1.2f);
            });
        }

        private static void SetTransparent(Material m, bool additive)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", additive ? 2f : 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            if (!additive) m.EnableKeyword("_ALPHAPREMULTIPLY_OFF");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetOverrideTag("RenderType", "Transparent");
        }

        // ------------------------------------------------------------------ pipeline / quality

        private static void ConfigureRenderPipeline()
        {
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
            if (asset == null) throw new Exception("PC_RPAsset not found");
            asset.supportsHDR = true;
            asset.shadowDistance = 220f;
            asset.shadowCascadeCount = 4;
            asset.msaaSampleCount = 4;
            asset.renderScale = 1f;
            var so = new SerializedObject(asset);
            SetProp(so, "m_SoftShadowsSupported", true);
            SetProp(so, "m_MainLightShadowmapResolution", 4096);
            SetProp(so, "m_AdditionalLightsRenderingMode", 1);
            SetProp(so, "m_RequireDepthTexture", true);
            SetProp(so, "m_RequireOpaqueTexture", false);
            so.ApplyModifiedPropertiesWithoutUndo();

            // Forward+ renderer: many floodlights/spotlights without per-object light limits
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset");
            if (renderer != null)
            {
                renderer.renderingMode = RenderingMode.ForwardPlus;
                EditorUtility.SetDirty(renderer);
            }

            GraphicsSettings.defaultRenderPipeline = asset;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = asset;
            }
            QualitySettings.SetQualityLevel(Mathf.Clamp(current, 0, QualitySettings.names.Length - 1), false);
            EditorUtility.SetDirty(asset);
        }

        private static void SetProp(SerializedObject so, string name, object value)
        {
            var p = so.FindProperty(name);
            if (p == null) { Debug.LogWarning("[ApexBuild] property not found: " + name); return; }
            switch (value)
            {
                case bool b: p.boolValue = b; break;
                case int i: if (p.propertyType == SerializedPropertyType.Enum) p.enumValueIndex = i; else p.intValue = i; break;
                case float f: p.floatValue = f; break;
            }
        }

        private static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "Project Apex Studio";
            PlayerSettings.productName = "PROJECT APEX";
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.visibleInBackground = true;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetApiCompatibilityLevel(UnityEditor.Build.NamedBuildTarget.Standalone, ApiCompatibilityLevel.NET_Standard);
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ResUI}/icon_app.png");
            if (icon != null) PlayerSettings.SetIcons(UnityEditor.Build.NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
        }

        private static void ConfigureAudioAndTime()
        {
            var audio = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/AudioManager.asset").FirstOrDefault();
            if (audio != null)
            {
                var so = new SerializedObject(audio);
                var rv = so.FindProperty("m_RealVoiceCount");
                if (rv != null) rv.intValue = 64;
                var vv = so.FindProperty("m_VirtualVoiceCount");
                if (vv != null) vv.intValue = 512;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            var time = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TimeManager.asset").FirstOrDefault();
            if (time != null)
            {
                var so = new SerializedObject(time);
                var fixedStep = so.FindProperty("Fixed Timestep");
                if (fixedStep != null)
                {
                    if (fixedStep.propertyType == SerializedPropertyType.Float) fixedStep.floatValue = 0.01f;
                    else if (fixedStep.FindPropertyRelative("m_Count") != null) { /* rational timestep in newer versions: runtime sets it */ }
                }
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // ------------------------------------------------------------------ scenes

        private static void CreateScenes()
        {
            CreateScene("MainMenu", SceneRole.MainMenu);
            CreateScene("Race", SceneRole.Race);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene($"{Scenes}/MainMenu.unity", true),
                new EditorBuildSettingsScene($"{Scenes}/Race.unity", true)
            };
        }

        private static void CreateScene(string name, SceneRole role)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var go = new GameObject("Bootstrap");
            go.AddComponent<GameBootstrap>().role = role;
            EditorSceneManager.SaveScene(scene, $"{Scenes}/{name}.unity");
        }
    }
}

using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace PoingMort.EditorTools
{
    /// <summary>
    /// One-time project configuration (idempotent): linear colour space, URP asset, layers,
    /// Input System backend, product name. URP types are reached by reflection so this script
    /// compiles even if the package is missing; failures are reported with the manual steps.
    /// </summary>
    public static class ProjectSetup
    {
        public const string SettingsFolder = "Assets/PoingMort/Settings";
        public const string Version = "0.1.0-p01";
        public static readonly string[] Layers = { "Environment", "Vehicle", "Character" };

        public static void Run()
        {
            PlayerSettings.companyName = "Poing Mort";
            PlayerSettings.productName = "Poing Mort";
            PlayerSettings.bundleVersion = Version;
            if (PlayerSettings.colorSpace != ColorSpace.Linear)
                PlayerSettings.colorSpace = ColorSpace.Linear;

            EnsureLayers();
            ConfigurePhysics();
            bool urp = EnsureUrp();
            bool input = CheckInputBackend();

            AssetDatabase.SaveAssets();
            string report = "Configuration terminée.\n\n" +
                            $"• Espace colorimétrique : Linéaire\n" +
                            $"• URP : {(urp ? "actif" : "NON configuré (voir la console)")}\n" +
                            $"• Couches : {string.Join(", ", Layers)}\n" +
                            $"• Input System : {(input ? "actif" : "à activer (redémarrage demandé)")}";
            Debug.Log("[PoingMort] " + report.Replace("\n", " "));
            if (!Application.isBatchMode)
                EditorUtility.DisplayDialog("Poing Mort", report, "OK");
        }

        // ------------------------------------------------------------------ layers

        public static void EnsureLayers()
        {
            var tagManager = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset").FirstOrDefault();
            if (tagManager == null) return;
            var so = new SerializedObject(tagManager);
            var layers = so.FindProperty("layers");
            foreach (var name in Layers)
            {
                bool exists = false;
                for (int i = 0; i < layers.arraySize; i++)
                    if (layers.GetArrayElementAtIndex(i).stringValue == name) exists = true;
                if (exists) continue;
                for (int i = 8; i < layers.arraySize; i++)
                {
                    var p = layers.GetArrayElementAtIndex(i);
                    if (string.IsNullOrEmpty(p.stringValue))
                    {
                        p.stringValue = name;
                        break;
                    }
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void ConfigurePhysics()
        {
            int vehicle = LayerMask.NameToLayer("Vehicle");
            if (vehicle >= 0)
                Physics.IgnoreLayerCollision(vehicle, vehicle, true); // cars are kinematic path followers
        }

        // ------------------------------------------------------------------ URP

        static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName, false);
                if (t != null) return t;
            }
            return null;
        }

        /// <summary>Creates and assigns a URP asset if the project has none.</summary>
        public static bool EnsureUrp()
        {
            if (GraphicsSettings.defaultRenderPipeline != null)
            {
                TuneUrpAsset(GraphicsSettings.defaultRenderPipeline);
                return true;
            }
            try
            {
                var assetType = FindType("UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset");
                var rendererType = FindType("UnityEngine.Rendering.Universal.UniversalRendererData");
                var baseRendererType = FindType("UnityEngine.Rendering.Universal.ScriptableRendererData");
                if (assetType == null || rendererType == null || baseRendererType == null)
                    throw new Exception("Paquet URP introuvable.");

                EditorUtil.EnsureFolder(SettingsFolder);
                string rendererPath = SettingsFolder + "/PM_URP_Renderer.asset";
                string assetPath = SettingsFolder + "/PM_URP.asset";
                var renderer = AssetDatabase.LoadAssetAtPath(rendererPath, rendererType) as ScriptableObject;
                if (renderer == null)
                {
                    renderer = ScriptableObject.CreateInstance(rendererType);
                    var ppType = FindType("UnityEngine.Rendering.Universal.PostProcessData");
                    var field = rendererType.GetField("postProcessData", BindingFlags.Public | BindingFlags.Instance);
                    if (ppType != null && field != null)
                    {
                        var pp = AssetDatabase.LoadAssetAtPath("Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset", ppType);
                        if (pp != null) field.SetValue(renderer, pp);
                    }
                    // Forward+: no per-object limit on the number of street lamps lighting a surface.
                    SceneBuilder.SetMember(renderer, "renderingMode", "ForwardPlus");
                    AssetDatabase.CreateAsset(renderer, rendererPath);
                }
                var asset = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(assetPath);
                if (asset == null)
                {
                    var create = assetType.GetMethod("Create", BindingFlags.Public | BindingFlags.Static, null, new[] { baseRendererType }, null);
                    if (create == null) throw new Exception("UniversalRenderPipelineAsset.Create introuvable.");
                    asset = (RenderPipelineAsset)create.Invoke(null, new object[] { renderer });
                    AssetDatabase.CreateAsset(asset, assetPath);
                }
                GraphicsSettings.defaultRenderPipeline = asset;
                int current = QualitySettings.GetQualityLevel();
                for (int i = 0; i < QualitySettings.names.Length; i++)
                {
                    QualitySettings.SetQualityLevel(i, false);
                    QualitySettings.renderPipeline = null; // use the default pipeline on every level
                }
                QualitySettings.SetQualityLevel(current, false);
                TuneUrpAsset(asset);
                AssetDatabase.SaveAssets();
                return GraphicsSettings.defaultRenderPipeline != null;
            }
            catch (Exception e)
            {
                Debug.LogError("[PoingMort] Impossible de créer l'asset URP automatiquement : " + e.Message +
                               "\nÉtapes manuelles : Assets > Create > Rendering > URP Asset (with Universal Renderer), " +
                               "puis Edit > Project Settings > Graphics > Default Render Pipeline = cet asset.");
                return false;
            }
        }

        static void TuneUrpAsset(RenderPipelineAsset asset)
        {
            if (asset == null) return;
            SetProperty(asset, "shadowDistance", 70f);
            SetProperty(asset, "shadowCascadeCount", 2);
            SetProperty(asset, "supportsHDR", true);
            SetProperty(asset, "msaaSampleCount", 4);
            SetProperty(asset, "maxAdditionalLightsCount", 8);
            EditorUtility.SetDirty(asset);
        }

        static void SetProperty(object target, string name, object value)
        {
            var prop = target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (prop == null || !prop.CanWrite) return;
            try { prop.SetValue(target, Convert.ChangeType(value, prop.PropertyType)); }
            catch (Exception) { /* property type differs between URP versions: keep the default */ }
        }

        // ------------------------------------------------------------------ input

        /// <summary>True when the Input System backend is enabled; otherwise switches to "Both" and asks for a restart.</summary>
        public static bool CheckInputBackend()
        {
#if ENABLE_INPUT_SYSTEM
            return true;
#else
            var settings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset").FirstOrDefault();
            if (settings != null)
            {
                var so = new SerializedObject(settings);
                var prop = so.FindProperty("activeInputHandler");
                if (prop != null)
                {
                    prop.intValue = 2; // Both
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            if (!Application.isBatchMode &&
                EditorUtility.DisplayDialog("Poing Mort", "Le nouvel Input System doit être activé. Redémarrer l'éditeur maintenant ?", "Redémarrer", "Plus tard"))
                EditorApplication.OpenProject(System.IO.Directory.GetCurrentDirectory());
            return false;
#endif
        }
    }
}

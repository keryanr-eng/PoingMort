using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace PoingMort.EditorTools
{
    /// <summary>
    /// Creates or updates the project's materials in Assets/PoingMort/Generated/Materials.
    /// Uses URP Lit when URP is active, the built-in Standard shader otherwise, so nothing renders pink.
    /// Updating an existing material keeps its GUID (scene references stay valid).
    /// </summary>
    public static class MaterialLibrary
    {
        public const string Folder = "Assets/PoingMort/Generated/Materials";
        public const string TextureFolder = "Assets/PoingMort/Art/Textures/";

        public static bool UsesUrp => GraphicsSettings.defaultRenderPipeline != null;

        public static Shader LitShader
        {
            get
            {
                Shader s = UsesUrp ? Shader.Find("Universal Render Pipeline/Lit") : null;
                return s != null ? s : Shader.Find("Standard");
            }
        }

        public sealed class Spec
        {
            public string name;
            public Color color = Color.white;
            public string texture;          // file name in Art/Textures (optional)
            public string normal;           // normal map file name (optional)
            public float smoothness = 0.15f;
            public float metallic;
            public Color emission = Color.black;
            public float emissionIntensity;
            public bool emissionFromTexture;
            public Vector2 tiling = Vector2.one;
        }

        public static Material GetOrCreate(Spec spec)
        {
            EditorUtil.EnsureFolder(Folder);
            string path = $"{Folder}/{spec.name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = LitShader;
            if (mat == null)
            {
                mat = new Material(shader) { name = spec.name };
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.shader != shader)
            {
                mat.shader = shader;
            }
            Apply(mat, spec);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Texture2D LoadTexture(string file)
        {
            if (string.IsNullOrEmpty(file)) return null;
            return AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder + file);
        }

        static void Apply(Material mat, Spec spec)
        {
            var albedo = LoadTexture(spec.texture);
            var normal = LoadTexture(spec.normal);
            bool urp = mat.shader != null && mat.shader.name.StartsWith("Universal Render Pipeline");
            string colorProp = urp ? "_BaseColor" : "_Color";
            string mapProp = urp ? "_BaseMap" : "_MainTex";
            mat.SetColor(colorProp, spec.color);
            if (mat.HasProperty(mapProp))
            {
                mat.SetTexture(mapProp, albedo);
                mat.SetTextureScale(mapProp, spec.tiling);
            }
            if (urp)
            {
                mat.SetFloat("_Smoothness", spec.smoothness);
                mat.SetFloat("_Metallic", spec.metallic);
            }
            else
            {
                mat.SetFloat("_Glossiness", spec.smoothness);
                mat.SetFloat("_Metallic", spec.metallic);
            }
            if (mat.HasProperty("_BumpMap"))
            {
                mat.SetTexture("_BumpMap", normal);
                if (normal != null) mat.EnableKeyword("_NORMALMAP"); else mat.DisableKeyword("_NORMALMAP");
                if (mat.HasProperty("_BumpScale")) mat.SetFloat("_BumpScale", 0.6f);
            }
            bool emissive = spec.emissionIntensity > 0f;
            if (mat.HasProperty("_EmissionColor"))
            {
                Color e = spec.emission * Mathf.Max(0f, spec.emissionIntensity);
                mat.SetColor("_EmissionColor", emissive ? e : Color.black);
                if (mat.HasProperty("_EmissionMap")) mat.SetTexture("_EmissionMap", emissive && spec.emissionFromTexture ? albedo : null);
                if (emissive) mat.EnableKeyword("_EMISSION"); else mat.DisableKeyword("_EMISSION");
                mat.globalIlluminationFlags = emissive ? MaterialGlobalIlluminationFlags.RealtimeEmissive : MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }
            // Matte surfaces: no environment reflections on rough materials.
            if (urp && mat.HasProperty("_EnvironmentReflections"))
                mat.SetFloat("_EnvironmentReflections", spec.smoothness > 0.5f ? 1f : 0f);
        }

        public static Color Hex(string hex)
        {
            return ColorUtility.TryParseHtmlString(hex.StartsWith("#") ? hex : "#" + hex, out var c) ? c : Color.magenta;
        }

        /// <summary>Converts a linear colour (as written by the Blender export manifest) to sRGB for the material colour.</summary>
        public static Color FromLinear(float r, float g, float b) => new Color(r, g, b).gamma;

        public static Material Skybox()
        {
            EditorUtil.EnsureFolder(Folder);
            string path = $"{Folder}/M_Sky_Sunset.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("Skybox/Procedural");
            if (mat == null)
            {
                mat = new Material(shader) { name = "M_Sky_Sunset" };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            if (mat.HasProperty("_SunDisk")) mat.SetFloat("_SunDisk", 2f);
            mat.SetFloat("_SunSize", 0.035f);
            if (mat.HasProperty("_SunSizeConvergence")) mat.SetFloat("_SunSizeConvergence", 4f);
            mat.SetFloat("_AtmosphereThickness", 1.35f);
            mat.SetColor("_SkyTint", new Color(0.62f, 0.52f, 0.58f));
            mat.SetColor("_GroundColor", new Color(0.36f, 0.32f, 0.31f));
            mat.SetFloat("_Exposure", 1.15f);
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }

    public static class EditorUtil
    {
        public static void EnsureFolder(string path)
        {
            path = path.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        public static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            EnsureFolder(Path.GetDirectoryName(path));
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        public static int Layer(string name)
        {
            int layer = LayerMask.NameToLayer(name);
            return layer < 0 ? 0 : layer;
        }

        public static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform) SetLayerRecursively(child.gameObject, layer);
        }
    }
}

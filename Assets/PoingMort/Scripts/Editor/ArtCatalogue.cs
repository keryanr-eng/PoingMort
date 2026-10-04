using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PoingMort.EditorTools
{
    /// <summary>
    /// URP materials of the environment and the car, keyed by the material names written by the Blender
    /// generators (tools/blender). Colours are the same sRGB values as in those scripts; the city UVs are
    /// already in metres / tile, so tiling stays 1.
    /// </summary>
    public static class ArtCatalogue
    {
        public const string CityFolder = "Assets/PoingMort/Art/Models/City/";
        public const string CarFbx = "Assets/PoingMort/Art/Models/Vehicles/PM_Car_Sedan.fbx";

        public static readonly string[] CarPaints = { "#8f8a82", "#a99a83", "#2f3b52", "#7a2d27", "#d8d3c8" };

        static MaterialLibrary.Spec Tex(string name, string texture, bool normal, float smoothness, float metallic = 0f)
        {
            return new MaterialLibrary.Spec
            {
                name = name,
                texture = texture + ".png",
                normal = normal ? texture + "_N.png" : null,
                smoothness = smoothness,
                metallic = metallic,
            };
        }

        static MaterialLibrary.Spec Col(string name, string hex, float smoothness, float metallic = 0f)
        {
            return new MaterialLibrary.Spec { name = name, color = MaterialLibrary.Hex(hex), smoothness = smoothness, metallic = metallic };
        }

        static MaterialLibrary.Spec Glow(MaterialLibrary.Spec s, string emissionHex, float intensity, bool fromTexture = false)
        {
            s.emission = emissionHex != null ? MaterialLibrary.Hex(emissionHex) : Color.white;
            s.emissionIntensity = intensity;
            s.emissionFromTexture = fromTexture;
            return s;
        }

        /// <summary>Material name in the FBX → library material description.</summary>
        public static Dictionary<string, MaterialLibrary.Spec> Specs()
        {
            var list = new List<MaterialLibrary.Spec>
            {
                // Walls and ground of the buildings
                Tex("PM_BrickRed", "T_Brick_Red", true, 0.08f),
                Tex("PM_BrickDark", "T_Brick_Dark", true, 0.08f),
                Tex("PM_PlasterWarm", "T_Plaster_Warm", true, 0.06f),
                Tex("PM_PlasterGrey", "T_Plaster_Grey", true, 0.06f),
                Tex("PM_Concrete", "T_Concrete", true, 0.08f),
                Tex("PM_RollerDoor", "T_RollerDoor", true, 0.3f, 0.35f),
                Tex("PM_Foliage", "T_Foliage", false, 0.1f),
                Tex("PM_Bark", "T_Bark", false, 0.05f),
                Col("PM_Roof", "#3c3a39", 0.04f),
                Col("PM_WindowGlass", "#2a3138", 0.86f),
                Col("PM_WindowFrame", "#ddd8cf", 0.3f),
                Col("PM_FrameDark", "#2a2b2d", 0.35f),
                Col("PM_Wood", "#5a3f2c", 0.2f),
                Col("PM_MetalDark", "#2d3330", 0.4f, 0.4f),
                Col("PM_MetalGreen", "#2f4136", 0.4f, 0.3f),
                Col("PM_AwningRed", "#8f2a24", 0.1f),
                Col("PM_AwningGreen", "#2f5a3d", 0.1f),
                Col("PM_RedPaint", "#9c2b22", 0.35f),
                Glow(Col("PM_LampLight", "#ffe2b0", 0.5f), "#ffd59a", 3.2f),
                // Signs and lit interiors (textures created for the project)
                Glow(Tex("PM_Sign_Epicerie", "T_Sign_Epicerie", false, 0.3f), null, 0.3f, true),
                Glow(Tex("PM_Sign_Laverie", "T_Sign_Laverie", false, 0.3f), null, 0.3f, true),
                Glow(Tex("PM_Sign_Cafe", "T_Sign_Cafe", false, 0.3f), null, 0.3f, true),
                Glow(Tex("PM_Sign_Atelier", "T_Sign_Atelier", false, 0.3f), null, 0.25f, true),
                Tex("PM_Sign_SensUnique", "T_Sign_SensUnique", false, 0.35f),
                Glow(Tex("PM_Shop_Interior_Warm", "T_Shop_Interior_Warm", false, 0.2f), null, 1.0f, true),
                Glow(Tex("PM_Shop_Interior_Cool", "T_Shop_Interior_Cool", false, 0.2f), null, 1.0f, true),
                Glow(Tex("PM_Windows_Lit", "T_Windows_Lit", false, 0.4f), null, 1.1f, true),
                // Car
                Col("PM_CarPaint", CarPaints[0], 0.62f),
                Col("PM_CarChrome", "#c9c9c6", 0.78f, 0.9f),
                Col("PM_CarGlass", "#1d232a", 0.92f),
                Col("PM_CarTrim", "#262628", 0.25f),
                Glow(Col("PM_CarLight", "#f3eedc", 0.6f), "#fff2d0", 1.6f),
                Glow(Col("PM_CarTail", "#8e1410", 0.5f), "#ff2a1a", 1.2f),
                Col("PM_CarInterior", "#2c2a29", 0.1f),
                Col("PM_CarPlate", "#d8d4c8", 0.35f),
                Col("PM_Tire", "#18181a", 0.08f),
                Col("PM_Rim", "#a7a7a4", 0.5f, 0.7f),
            };
            foreach (var s in list) s.name = "M_" + s.name.Substring(3);
            return list.ToDictionary(s => "PM_" + s.name.Substring(2), s => s);
        }

        /// <summary>Extra materials used by the generated ground.</summary>
        public static class Ground
        {
            public static Material Asphalt() => MaterialLibrary.GetOrCreate(new MaterialLibrary.Spec { name = "M_Asphalt", texture = "T_Asphalt.png", normal = "T_Asphalt_N.png", smoothness = 0.14f });
            public static Material Sidewalk() => MaterialLibrary.GetOrCreate(new MaterialLibrary.Spec { name = "M_Sidewalk", texture = "T_Sidewalk.png", normal = "T_Sidewalk_N.png", smoothness = 0.08f });
            public static Material Curb() => MaterialLibrary.GetOrCreate(new MaterialLibrary.Spec { name = "M_Curb", texture = "T_Curb.png", normal = "T_Curb_N.png", smoothness = 0.1f });
            public static Material Gravel() => MaterialLibrary.GetOrCreate(new MaterialLibrary.Spec { name = "M_Gravel", texture = "T_Gravel.png", normal = "T_Gravel_N.png", smoothness = 0.05f });
            public static Material Paint() => MaterialLibrary.GetOrCreate(new MaterialLibrary.Spec { name = "M_RoadPaint", color = MaterialLibrary.Hex("#e4e0d6"), smoothness = 0.25f });
            public static Material Concrete() => MaterialLibrary.GetOrCreate(Specs()["PM_Concrete"]);
        }

        /// <summary>Paint variant of the car (same settings as M_CarPaint, other colour).</summary>
        public static Material CarPaint(int index)
        {
            var spec = Specs()["PM_CarPaint"];
            index = Mathf.Abs(index) % CarPaints.Length;
            if (index > 0)
            {
                spec.name = "M_CarPaint_" + index;
                spec.color = MaterialLibrary.Hex(CarPaints[index]);
            }
            return MaterialLibrary.GetOrCreate(spec);
        }

        /// <summary>
        /// Remaps every material of a model to its library material (by name) and reimports it.
        /// Unknown names are reported: they would otherwise keep the importer's default material.
        /// </summary>
        public static void RemapModel(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
            {
                Debug.LogWarning("[PoingMort] Modèle introuvable : " + path);
                return;
            }
            var specs = Specs();
            var names = new HashSet<string>();
            foreach (var m in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>()) names.Add(m.name);
            foreach (var pair in importer.GetExternalObjectMap())
                if (pair.Key.type == typeof(Material)) names.Add(pair.Key.name);

            bool changed = false;
            var existing = importer.GetExternalObjectMap();
            foreach (var name in names)
            {
                if (!specs.TryGetValue(name, out var spec))
                {
                    Debug.LogWarning($"[PoingMort] Matériau « {name} » de {path} absent du catalogue : matériau importé conservé.");
                    continue;
                }
                var mat = MaterialLibrary.GetOrCreate(spec);
                var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), name);
                if (existing.TryGetValue(id, out var current) && current == mat) continue;
                importer.AddRemap(id, mat);
                changed = true;
            }
            if (changed) importer.SaveAndReimport();
        }
    }
}

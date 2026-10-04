using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PoingMort.EditorTools
{
    /// <summary>
    /// Import settings applied automatically to the project's art:
    /// characters as Humanoid with named, looping or root-locked clips; props, buildings and the car
    /// keep their material slots so the setup tools can remap them by name to URP materials;
    /// tiling textures and normal maps; UI images as sprites.
    /// </summary>
    public sealed class AssetImportRules : AssetPostprocessor
    {
        public const string ArtRoot = "Assets/PoingMort/Art/";
        public const string CharactersFolder = ArtRoot + "Models/Characters/";

        static readonly string[] LoopingClips =
        {
            "Idle", "Walk", "Run", "Sprint", "FightIdle", "FightStepFwd", "FightStepBack", "FightStepLeft", "FightStepRight", "Seated",
        };

        static bool IsOurs(string path) => path.StartsWith(ArtRoot);

        void OnPreprocessTexture()
        {
            if (!IsOurs(assetPath)) return;
            var importer = (TextureImporter)assetImporter;
            if (assetPath.Contains("/Art/UI/"))
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.alphaIsTransparency = true;
                return;
            }
            bool normal = assetPath.EndsWith("_N.png");
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !normal;
            importer.mipmapEnabled = true;
            importer.anisoLevel = 4;
            bool sign = assetPath.Contains("T_Sign_") || assetPath.Contains("_Skin");
            importer.wrapMode = sign ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            importer.maxTextureSize = assetPath.Contains("_Skin") ? 2048 : 1024;
        }

        void OnPreprocessModel()
        {
            if (!IsOurs(assetPath)) return;
            var importer = (ModelImporter)assetImporter;
            // Materials stay embedded in the model; the setup tools remap each one by name
            // (AddRemap) to a URP material from the library, so nothing renders pink.
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            importer.materialName = ModelImporterMaterialName.BasedOnMaterialName;
            importer.materialSearch = ModelImporterMaterialSearch.Local;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importBlendShapes = false;
            importer.isReadable = false;
            importer.globalScale = 1f;
            importer.useFileScale = true;
            if (assetPath.StartsWith(CharactersFolder))
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.importAnimation = true;
                importer.optimizeGameObjects = false;
                importer.importNormals = ModelImporterNormals.Import;
            }
            else
            {
                importer.animationType = ModelImporterAnimationType.None;
                importer.importAnimation = false;
                importer.importNormals = ModelImporterNormals.Calculate;
                importer.normalSmoothingAngle = 40f;
                importer.addCollider = false;
            }
        }

        void OnPreprocessAnimation()
        {
            if (!assetPath.StartsWith(CharactersFolder)) return;
            var importer = (ModelImporter)assetImporter;
            var defaults = importer.defaultClipAnimations;
            if (defaults == null || defaults.Length == 0) return;
            var clips = defaults.Select(c =>
            {
                string shortName = ShortClipName(c.takeName);
                c.name = shortName;
                bool loop = LoopingClips.Contains(shortName);
                c.loopTime = loop;
                c.loopPose = loop;
                // The game moves the character: animation stays in place, body orientation/height kept.
                c.lockRootRotation = true;
                c.keepOriginalOrientation = true;
                c.lockRootHeightY = true;
                c.keepOriginalPositionY = true;
                c.heightFromFeet = false;
                c.lockRootPositionXZ = true;
                c.keepOriginalPositionXZ = true;
                return c;
            }).ToArray();
            importer.clipAnimations = clips;
        }

        /// <summary>"Armature|Jab" → "Jab".</summary>
        public static string ShortClipName(string take)
        {
            if (string.IsNullOrEmpty(take)) return take;
            int bar = take.LastIndexOf('|');
            return bar >= 0 ? take.Substring(bar + 1) : take;
        }
    }
}

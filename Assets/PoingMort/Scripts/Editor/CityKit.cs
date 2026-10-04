using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PoingMort.EditorTools
{
    /// <summary>
    /// Prefabs of the buildings and street furniture (variants of the FBX models): URP materials,
    /// simple colliders matching the visible volumes, static flags, lamp lights.
    /// Model axes in Unity: buildings have their facade at z = 0 facing +Z and extend toward -Z;
    /// lamps, benches and bus stop face +Z (the road side).
    /// </summary>
    public static class CityKit
    {
        public const string PrefabFolder = CharacterSetup.PrefabFolder + "/Ville";

#pragma warning disable 0649 // filled by JsonUtility
        [System.Serializable]
        class BuildingInfo { public float width, depth, height; }
#pragma warning restore 0649

        static readonly Dictionary<string, BuildingInfo> s_Buildings = new Dictionary<string, BuildingInfo>();

        public static GameObject Get(string name)
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/PF_{name}.prefab");
        }

        public static BuildingInfoView Info(string name)
        {
            LoadManifest();
            return s_Buildings.TryGetValue(name, out var b) ? new BuildingInfoView(b.width, b.depth, b.height) : new BuildingInfoView(10f, 10f, 10f);
        }

        public readonly struct BuildingInfoView
        {
            public readonly float width, depth, height;
            public BuildingInfoView(float w, float d, float h) { width = w; depth = d; height = h; }
        }

        static void LoadManifest()
        {
            if (s_Buildings.Count > 0) return;
            string path = ArtCatalogue.CityFolder + "city_assets.json";
            if (!File.Exists(path)) return;
            // JsonUtility cannot read dictionaries: parse the "buildings" block by hand (flat numbers only).
            string json = File.ReadAllText(path);
            int start = json.IndexOf("\"buildings\"");
            int end = json.IndexOf("\"props\"");
            if (start < 0 || end < 0) return;
            string block = json.Substring(start, end - start);
            int i = 0;
            while (true)
            {
                int q = block.IndexOf("\"Bld_", i);
                if (q < 0) break;
                int q2 = block.IndexOf('"', q + 1);
                string name = block.Substring(q + 1, q2 - q - 1);
                int open = block.IndexOf('{', q2);
                int close = block.IndexOf('}', open);
                string body = block.Substring(open, close - open + 1);
                var info = JsonUtility.FromJson<BuildingInfo>(body);
                s_Buildings[name] = info;
                i = close;
            }
        }

        public static void BuildAll()
        {
            EditorUtil.EnsureFolder(PrefabFolder);
            LoadManifest();
            var guids = AssetDatabase.FindAssets("t:Model", new[] { ArtCatalogue.CityFolder.TrimEnd('/') });
            int count = 0;
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string name = Path.GetFileNameWithoutExtension(path);
                ArtCatalogue.RemapModel(path);
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model == null) continue;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
                go.name = name;
                Configure(go, name);
                EditorUtil.SetLayerRecursively(go, EditorUtil.Layer("Environment"));
                var flags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic;
                if (name.StartsWith("Bld_")) flags |= StaticEditorFlags.OccluderStatic;
                foreach (var t in go.GetComponentsInChildren<Transform>(true))
                    if (t.GetComponent<Light>() == null) GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags);
                PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabFolder}/PF_{name}.prefab");
                Object.DestroyImmediate(go);
                count++;
            }
            Debug.Log($"[PoingMort] {count} prefabs de décor construits dans {PrefabFolder}.");
        }

        static void Configure(GameObject go, string name)
        {
            if (name.StartsWith("Bld_"))
            {
                var info = Info(name);
                Box(go, new Vector3(0f, info.height / 2f, -info.depth / 2f), new Vector3(info.width, info.height, info.depth));
                return;
            }
            switch (name)
            {
                case "Prop_StreetLamp":
                    Capsule(go, new Vector3(0f, 2.75f, 0f), 0.13f, 5.5f);
                    AddLamp(go, new Vector3(0f, 5.12f, 1.35f), LightType.Spot, 4.5f, 13f, 112f);
                    break;
                case "Prop_Floodlight":
                    Capsule(go, new Vector3(0f, 2.45f, 0f), 0.1f, 4.9f);
                    AddLamp(go, new Vector3(0f, 4.62f, 0.4f), LightType.Spot, 5.5f, 18f, 95f, 35f);
                    break;
                case "Prop_Bench": Box(go, new Vector3(0f, 0.425f, 0f), new Vector3(1.9f, 0.85f, 0.5f)); break;
                case "Prop_TrashBin": Capsule(go, new Vector3(0f, 0.455f, 0f), 0.31f, 0.91f); break;
                case "Prop_Tree":
                    Box(go, new Vector3(0f, 0.175f, 0f), new Vector3(1.4f, 0.35f, 1.4f));
                    Capsule(go, new Vector3(0f, 1.6f, 0f), 0.18f, 2.6f);
                    break;
                case "Prop_Hydrant": Capsule(go, new Vector3(0f, 0.35f, 0f), 0.16f, 0.7f); break;
                case "Prop_Bollard": Capsule(go, new Vector3(0f, 0.5f, 0f), 0.1f, 1.0f); break;
                case "Prop_Barrier": Box(go, new Vector3(0f, 0.525f, 0f), new Vector3(2.06f, 1.05f, 0.08f)); break;
                case "Prop_Fence": Box(go, new Vector3(0f, 1.1f, 0f), new Vector3(3.08f, 2.2f, 0.08f)); break;
                case "Prop_Crate": Box(go, new Vector3(0f, 0.4f, 0f), new Vector3(0.94f, 0.8f, 0.94f)); break;
                case "Prop_Dumpster": Box(go, new Vector3(0f, 0.64f, 0f), new Vector3(2.0f, 1.28f, 1.2f)); break;
                case "Prop_SignOneWay": Capsule(go, new Vector3(0f, 1.3f, 0f), 0.06f, 2.6f); break;
                case "Prop_Wall": Box(go, new Vector3(0f, 1.775f, 0f), new Vector3(4.0f, 3.55f, 0.3f)); break;
                case "Prop_BusStop":
                    Box(go, new Vector3(0f, 1.25f, -0.46f), new Vector3(3.9f, 2.5f, 0.12f));
                    Box(go, new Vector3(0f, 0.25f, -0.175f), new Vector3(2.4f, 0.5f, 0.35f));
                    Box(go, new Vector3(0f, 2.55f, -0.15f), new Vector3(4.2f, 0.1f, 1.1f));
                    break;
                default:
                    var r = go.GetComponentInChildren<Renderer>();
                    if (r != null) Box(go, r.bounds.center - go.transform.position, r.bounds.size);
                    break;
            }
        }

        static void Box(GameObject go, Vector3 centre, Vector3 size)
        {
            var c = go.AddComponent<BoxCollider>();
            c.center = centre;
            c.size = size;
        }

        static void Capsule(GameObject go, Vector3 centre, float radius, float height)
        {
            var c = go.AddComponent<CapsuleCollider>();
            c.center = centre;
            c.radius = radius;
            c.height = height;
            c.direction = 1;
        }

        static void AddLamp(GameObject go, Vector3 localPosition, LightType type, float intensity, float range, float spotAngle, float tiltDeg = 0f)
        {
            var lightGo = new GameObject("Lumière");
            lightGo.transform.SetParent(go.transform, false);
            lightGo.transform.localPosition = localPosition;
            lightGo.transform.localRotation = Quaternion.Euler(90f - tiltDeg, 0f, 0f); // pointing down (tilted toward +Z)
            var light = lightGo.AddComponent<Light>();
            light.type = type;
            light.color = new Color(1f, 0.82f, 0.6f);
            light.intensity = intensity;
            light.range = range;
            light.spotAngle = spotAngle;
            light.innerSpotAngle = spotAngle * 0.55f;
            light.shadows = LightShadows.None;
            light.renderMode = LightRenderMode.Auto;
        }
    }
}

using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace PoingMort.EditorTools
{
    /// <summary>
    /// Writes the ground pieces of <see cref="GroundGeometry"/> as mesh assets (Generated/Meshes) and
    /// adds them to the scene with their URP material and a collider on the Environment layer.
    /// </summary>
    public static class GroundBuilder
    {
        public const string MeshFolder = "Assets/PoingMort/Generated/Meshes";

        public static GameObject Build(Transform parent, LoopPath path)
        {
            EditorUtil.EnsureFolder(MeshFolder);
            var root = new GameObject("Sol");
            root.transform.SetParent(parent, false);
            foreach (var piece in GroundGeometry.Pieces(path))
                Emit(root.transform, piece);
            return root;
        }

        static Material MaterialFor(string key)
        {
            switch (key)
            {
                case "asphalt": return ArtCatalogue.Ground.Asphalt();
                case "sidewalk": return ArtCatalogue.Ground.Sidewalk();
                case "curb": return ArtCatalogue.Ground.Curb();
                case "gravel": return ArtCatalogue.Ground.Gravel();
                case "paint": return ArtCatalogue.Ground.Paint();
                default: return ArtCatalogue.Ground.Concrete();
            }
        }

        static void Emit(Transform parent, GroundGeometry.Piece piece)
        {
            var data = piece.mesh;
            if (data.Empty) return;
            string path = $"{MeshFolder}/MSH_{piece.assetId}.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool create = mesh == null;
            if (create) mesh = new Mesh();
            mesh.Clear();
            mesh.name = piece.name;
            mesh.indexFormat = data.v.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(data.v);
            mesh.SetUVs(0, data.uv);
            mesh.SetTriangles(data.t, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            if (create) AssetDatabase.CreateAsset(mesh, path);
            else EditorUtility.SetDirty(mesh);

            var go = new GameObject(piece.name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = MaterialFor(piece.material);
            if (!piece.collider) r.shadowCastingMode = ShadowCastingMode.Off;
            else go.AddComponent<MeshCollider>().sharedMesh = mesh;
            go.layer = EditorUtil.Layer("Environment");
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ReflectionProbeStatic | StaticEditorFlags.OccludeeStatic);
        }
    }
}

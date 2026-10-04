using PoingMort.Vehicles;
using UnityEditor;
using UnityEngine;

namespace PoingMort.EditorTools
{
    /// <summary>
    /// Builds the car prefab from PM_Car_Sedan.fbx: doors on hinge pivots, wheel pivots, gameplay markers
    /// (seat, door entries, exits) with the car's orientation, colliders, engine and horn sounds.
    /// Car model axes in Unity: forward +Z, up +Y, driver side (left-hand drive) at -X.
    /// </summary>
    public static class VehicleSetup
    {
        public const string PrefabPath = CharacterSetup.PrefabFolder + "/PF_Car_Sedan.prefab";
        const string AudioFolder = "Assets/PoingMort/Art/Audio/";

        // Fallback marker positions (car space) if the FBX empties are missing. Same values as build_car.py.
        static readonly Vector3 SeatFallback = new Vector3(-0.37f, 0.13f, -0.12f);
        static readonly Vector3 EntryLeftFallback = new Vector3(-1.32f, 0f, 0.2f);
        static readonly Vector3 ExitLeftFallback = new Vector3(-1.52f, 0f, -0.05f);

        public static bool HasModel => AssetDatabase.LoadAssetAtPath<GameObject>(ArtCatalogue.CarFbx) != null;

        public static GameObject Build()
        {
            if (!HasModel)
            {
                Debug.LogError("[PoingMort] " + ArtCatalogue.CarFbx + " introuvable : impossible de construire la voiture.");
                return null;
            }
            ArtCatalogue.RemapModel(ArtCatalogue.CarFbx);
            EditorUtil.EnsureFolder(CharacterSetup.PrefabFolder);

            var root = new GameObject("Voiture");
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ArtCatalogue.CarFbx));
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            model.name = "Modele";
            model.transform.SetParent(root.transform, false);

            var vehicle = root.AddComponent<TrafficVehicle>();

            // Doors: a pivot on the hinge with the car's orientation, so the door opens around the vertical axis.
            vehicle.doorLeft = MakeDoor(root.transform, CharacterSetup.FindDeep(model.transform, "Door_FL"), "Charnière portière G", 62f);
            vehicle.doorRight = MakeDoor(root.transform, CharacterSetup.FindDeep(model.transform, "Door_FR"), "Charnière portière D", -62f);

            // Wheels: pivots at the hubs, axle along the car's X axis (what TrafficVehicle rotates).
            var fl = Pivot(root.transform, CharacterSetup.FindDeep(model.transform, "Wheel_FL"), "Roue AV G");
            var fr = Pivot(root.transform, CharacterSetup.FindDeep(model.transform, "Wheel_FR"), "Roue AV D");
            var rl = Pivot(root.transform, CharacterSetup.FindDeep(model.transform, "Wheel_RL"), "Roue AR G");
            var rr = Pivot(root.transform, CharacterSetup.FindDeep(model.transform, "Wheel_RR"), "Roue AR D");
            vehicle.steeringWheels = Compact(fl, fr);
            vehicle.wheels = Compact(rl, rr);
            vehicle.wheelRadius = 0.33f;

            // Markers (always aligned with the car: the seated character takes the seat's orientation).
            var markers = new GameObject("Repères").transform;
            markers.SetParent(root.transform, false);
            vehicle.seatAnchor = Marker(markers, model.transform, "Seat_Driver", "Siège conducteur", SeatFallback);
            vehicle.doorEntryLeft = Marker(markers, model.transform, "Door_Entry_L", "Accès portière G", EntryLeftFallback);
            vehicle.doorEntryRight = Marker(markers, model.transform, "Door_Entry_R", "Accès portière D", Mirror(EntryLeftFallback));
            vehicle.exitPointLeft = Marker(markers, model.transform, "Exit_L", "Sortie G", ExitLeftFallback);
            vehicle.exitPointRight = Marker(markers, model.transform, "Exit_R", "Sortie D", Mirror(ExitLeftFallback));

            vehicle.length = 4.86f;
            vehicle.width = 1.8f;

            // Physics: kinematic body (moved by the traffic system), two boxes for the body and the cabin.
            var rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.None;
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
            var lower = root.AddComponent<BoxCollider>();
            lower.center = new Vector3(0f, 0.56f, 0f);
            lower.size = new Vector3(1.8f, 0.72f, 4.84f);
            var cabin = root.AddComponent<BoxCollider>();
            cabin.center = new Vector3(0f, 1.12f, -0.2f);
            cabin.size = new Vector3(1.62f, 0.5f, 2.5f);

            // Sound
            var engine = root.AddComponent<AudioSource>();
            engine.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioFolder + "SFX_Engine_Loop.wav");
            engine.loop = true;
            engine.playOnAwake = false;
            engine.spatialBlend = 1f;
            engine.volume = 0.38f;
            engine.minDistance = 3f;
            engine.maxDistance = 38f;
            engine.rolloffMode = AudioRolloffMode.Linear;
            engine.dopplerLevel = 0.5f;
            vehicle.engineAudio = engine;
            var hornGo = new GameObject("Klaxon");
            hornGo.transform.SetParent(root.transform, false);
            hornGo.transform.localPosition = new Vector3(0f, 0.7f, 2.2f);
            var horn = hornGo.AddComponent<AudioSource>();
            horn.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioFolder + "SFX_Horn.wav");
            horn.playOnAwake = false;
            horn.spatialBlend = 1f;
            horn.volume = 0.7f;
            horn.minDistance = 5f;
            horn.maxDistance = 70f;
            horn.rolloffMode = AudioRolloffMode.Linear;
            vehicle.hornAudio = horn;

            foreach (var r in root.GetComponentsInChildren<MeshRenderer>())
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

            EditorUtil.SetLayerRecursively(root, EditorUtil.Layer("Vehicle"));
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        static Vector3 Mirror(Vector3 v) => new Vector3(-v.x, v.y, v.z);

        static Transform[] Compact(params Transform[] items)
        {
            var list = new System.Collections.Generic.List<Transform>();
            foreach (var t in items) if (t != null) list.Add(t);
            return list.ToArray();
        }

        /// <summary>Wraps a model part in a pivot placed at its origin, aligned with the car.</summary>
        static Transform Pivot(Transform carRoot, Transform part, string name)
        {
            if (part == null)
            {
                Debug.LogWarning($"[PoingMort] Pièce « {name} » absente du modèle de voiture.");
                return null;
            }
            var pivot = new GameObject(name).transform;
            pivot.SetParent(part.parent, false);
            pivot.position = part.position;
            pivot.rotation = carRoot.rotation;
            part.SetParent(pivot, true);
            return pivot;
        }

        static VehicleDoor MakeDoor(Transform carRoot, Transform door, string name, float angle)
        {
            var pivot = Pivot(carRoot, door, name);
            if (pivot == null) return null;
            var d = pivot.gameObject.AddComponent<VehicleDoor>();
            d.openAngle = angle;
            d.hingeAxis = Vector3.up;
            return d;
        }

        static Transform Marker(Transform parent, Transform model, string sourceName, string name, Vector3 fallback)
        {
            var source = CharacterSetup.FindDeep(model, sourceName);
            var marker = new GameObject(name).transform;
            marker.SetParent(parent, false);
            if (source != null)
            {
                marker.position = source.position;
                Object.DestroyImmediate(source.gameObject);
            }
            else
            {
                Debug.LogWarning($"[PoingMort] Repère « {sourceName} » absent du modèle : position par défaut utilisée.");
                marker.localPosition = fallback;
            }
            marker.localRotation = Quaternion.identity;
            return marker;
        }

        /// <summary>Applies a paint variant to a car instance (body and doors share the paint slot).</summary>
        public static void Paint(GameObject car, int variant)
        {
            var paint = ArtCatalogue.CarPaint(variant);
            var basePaint = ArtCatalogue.CarPaint(0);
            foreach (var r in car.GetComponentsInChildren<MeshRenderer>())
            {
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] != null && (mats[i] == basePaint || mats[i].name.StartsWith("M_CarPaint")))
                    {
                        mats[i] = paint;
                        changed = true;
                    }
                }
                if (changed) r.sharedMaterials = mats;
            }
        }
    }
}

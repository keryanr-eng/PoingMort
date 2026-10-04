using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PoingMort.Characters;
using PoingMort.Combat;
using PoingMort.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace PoingMort.EditorTools
{
    /// <summary>
    /// Turns the character FBX files into playable prefabs: URP materials remapped by name,
    /// a two-layer Animator Controller (full body + upper body for punches) and the gameplay components.
    /// Falls back to a clearly labelled technical placeholder when a character FBX is missing.
    /// </summary>
    public static class CharacterSetup
    {
        public const string ModelsFolder = "Assets/PoingMort/Art/Models/Characters/";
        public const string AnimFolder = "Assets/PoingMort/Generated/Animation";
        public const string PrefabFolder = "Assets/PoingMort/Generated/Prefabs";

#pragma warning disable 0649 // filled by JsonUtility
        [Serializable]
        class MaterialEntry
        {
            public string name;
            public float[] color_linear;
            public float roughness = 0.8f;
            public float metallic;
            public string texture;
        }

        [Serializable]
        class Manifest
        {
            public string character;
            public string fbx;
            public MaterialEntry[] materials;
            public float height = 1.78f;
        }
#pragma warning restore 0649

        public static string FbxPath(string who) => ModelsFolder + $"PM_{Capitalise(who)}.fbx";
        static string ManifestPath(string who) => ModelsFolder + $"PM_{Capitalise(who)}.materials.json";
        static string Capitalise(string s) => char.ToUpperInvariant(s[0]) + s.Substring(1);

        static Manifest LoadManifest(string who)
        {
            string path = ManifestPath(who);
            if (!File.Exists(path)) return null;
            return JsonUtility.FromJson<Manifest>(File.ReadAllText(path));
        }

        /// <summary>Creates URP materials for the character and remaps the FBX materials to them.</summary>
        public static void RemapMaterials(string who)
        {
            var manifest = LoadManifest(who);
            var importer = AssetImporter.GetAtPath(FbxPath(who)) as ModelImporter;
            if (manifest == null || importer == null || manifest.materials == null) return;
            bool changed = false;
            foreach (var m in manifest.materials)
            {
                var c = m.color_linear != null && m.color_linear.Length >= 3 ? MaterialLibrary.FromLinear(m.color_linear[0], m.color_linear[1], m.color_linear[2]) : Color.white;
                var spec = new MaterialLibrary.Spec
                {
                    name = $"M_{Capitalise(who)}_{m.name.Replace("PM_", "")}",
                    color = string.IsNullOrEmpty(m.texture) ? c : Color.white,
                    texture = m.texture,
                    smoothness = Mathf.Clamp01(1f - m.roughness) * 0.8f,
                    metallic = m.metallic,
                };
                if (m.name == "PM_Eyes") spec.smoothness = 0.75f;
                var mat = MaterialLibrary.GetOrCreate(spec);
                var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), m.name);
                var existing = importer.GetExternalObjectMap();
                if (!existing.TryGetValue(id, out var current) || current != mat)
                {
                    importer.AddRemap(id, mat);
                    changed = true;
                }
            }
            if (changed) importer.SaveAndReimport();
        }

        public static Dictionary<string, AnimationClip> LoadClips(string fbxPath)
        {
            var dict = new Dictionary<string, AnimationClip>();
            foreach (var clip in AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<AnimationClip>())
            {
                if (clip.name.StartsWith("__preview__")) continue;
                dict[AssetImportRules.ShortClipName(clip.name)] = clip;
            }
            return dict;
        }

        public static AvatarMask UpperBodyMask()
        {
            EditorUtil.EnsureFolder(AnimFolder);
            string path = AnimFolder + "/AM_UpperBody.mask";
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(path);
            if (mask == null)
            {
                mask = new AvatarMask();
                AssetDatabase.CreateAsset(mask, path);
            }
            foreach (AvatarMaskBodyPart part in Enum.GetValues(typeof(AvatarMaskBodyPart)))
            {
                if (part == AvatarMaskBodyPart.LastBodyPart) continue;
                bool on = part == AvatarMaskBodyPart.Body || part == AvatarMaskBodyPart.Head ||
                          part == AvatarMaskBodyPart.LeftArm || part == AvatarMaskBodyPart.RightArm ||
                          part == AvatarMaskBodyPart.LeftFingers || part == AvatarMaskBodyPart.RightFingers ||
                          part == AvatarMaskBodyPart.LeftHandIK || part == AvatarMaskBodyPart.RightHandIK;
                mask.SetHumanoidBodyPartActive(part, on);
            }
            EditorUtility.SetDirty(mask);
            return mask;
        }

        /// <summary>(Re)creates the Animator Controller for a character. Missing clips are reported, not faked.</summary>
        public static AnimatorController BuildController(string who, Dictionary<string, AnimationClip> clips)
        {
            EditorUtil.EnsureFolder(AnimFolder);
            string path = AnimFolder + $"/AC_{Capitalise(who)}.controller";
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(path) != null) AssetDatabase.DeleteAsset(path);
            var ac = AnimatorController.CreateAnimatorControllerAtPath(path);

            var missing = CharacterAnimStates.RequiredClips.Where(c => !clips.ContainsKey(c)).ToList();
            if (missing.Count > 0)
                Debug.LogWarning($"[PoingMort] Clips manquants pour {who} : {string.Join(", ", missing)}");
            AnimationClip C(string n) => clips.TryGetValue(n, out var c) ? c : null;

            ac.AddParameter(CharacterAnimStates.Speed, AnimatorControllerParameterType.Float);
            ac.AddParameter(CharacterAnimStates.MoveX, AnimatorControllerParameterType.Float);
            ac.AddParameter(CharacterAnimStates.MoveY, AnimatorControllerParameterType.Float);
            ac.AddParameter(CharacterAnimStates.Combat, AnimatorControllerParameterType.Bool);
            ac.AddParameter(CharacterAnimStates.Guard, AnimatorControllerParameterType.Bool);
            ac.AddParameter(CharacterAnimStates.Grounded, AnimatorControllerParameterType.Bool);
            ac.AddParameter(CharacterAnimStates.Seated, AnimatorControllerParameterType.Bool);
            ac.AddParameter(CharacterAnimStates.KnockedOut, AnimatorControllerParameterType.Bool);
            ac.AddParameter(CharacterAnimStates.Fallen, AnimatorControllerParameterType.Bool);

            // ---------------- Base layer (full body)
            var root = ac.layers[0].stateMachine;
            var loco = ac.CreateBlendTreeInController(CharacterAnimStates.Locomotion, out var locoTree, 0);
            locoTree.blendType = BlendTreeType.Simple1D;
            locoTree.blendParameter = CharacterAnimStates.Speed;
            locoTree.useAutomaticThresholds = false;
            AddChild(locoTree, C("Idle"), 0f);
            AddChild(locoTree, C("Walk"), 1.45f);
            AddChild(locoTree, C("Run"), 3.6f);
            AddChild(locoTree, C("Sprint"), 6.4f);
            root.defaultState = loco;

            var fight = ac.CreateBlendTreeInController(CharacterAnimStates.FightLocomotion, out var fightTree, 0);
            fightTree.blendType = BlendTreeType.FreeformDirectional2D;
            fightTree.blendParameter = CharacterAnimStates.MoveX;
            fightTree.blendParameterY = CharacterAnimStates.MoveY;
            AddChild2D(fightTree, C("FightIdle"), Vector2.zero);
            AddChild2D(fightTree, C("FightStepFwd"), new Vector2(0f, 1f));
            AddChild2D(fightTree, C("FightStepBack"), new Vector2(0f, -1f));
            AddChild2D(fightTree, C("FightStepLeft"), new Vector2(-1f, 0f));
            AddChild2D(fightTree, C("FightStepRight"), new Vector2(1f, 0f));

            Transition(loco, fight, 0.2f).AddCondition(AnimatorConditionMode.If, 0, CharacterAnimStates.Combat);
            Transition(fight, loco, 0.25f).AddCondition(AnimatorConditionMode.IfNot, 0, CharacterAnimStates.Combat);

            // One-shot states return to the right locomotion when they end.
            foreach (var name in new[] { CharacterAnimStates.Dodge, CharacterAnimStates.HitLight, CharacterAnimStates.HitHeavy })
            {
                var s = root.AddState(name);
                s.motion = C(name);
                ExitTo(s, fight, loco, 0.85f);
            }
            var ko = root.AddState(CharacterAnimStates.KO);
            ko.motion = C("KO");
            var fall = root.AddState(CharacterAnimStates.Fall);
            fall.motion = C("Fall");
            var getUp = root.AddState(CharacterAnimStates.GetUp);
            getUp.motion = C("GetUp");
            ExitTo(getUp, fight, loco, 0.9f);
            var seated = root.AddState(CharacterAnimStates.SeatedState);
            seated.motion = C("Seated");
            var enter = root.AddState(CharacterAnimStates.CarEnter);
            enter.motion = C("CarEnter");
            var toSeat = Transition(enter, seated, 0.15f);
            toSeat.hasExitTime = true;
            toSeat.exitTime = 0.95f;
            var exit = root.AddState(CharacterAnimStates.CarExit);
            exit.motion = C("CarExit");
            var exitDone = Transition(exit, loco, 0.15f);
            exitDone.hasExitTime = true;
            exitDone.exitTime = 0.9f;

            // ---------------- Upper body layer (punches and guard over the legs of the base layer)
            ac.AddLayer("UpperBody");
            var layers = ac.layers;
            layers[1].defaultWeight = 0f; // weight driven by MecanimCharacterAnimator (0 while the layer is in "Empty")
            layers[1].avatarMask = UpperBodyMask();
            layers[1].blendingMode = AnimatorLayerBlendingMode.Override;
            ac.layers = layers;
            var upper = ac.layers[1].stateMachine;
            var empty = upper.AddState(CharacterAnimStates.UpperEmpty);
            upper.defaultState = empty;
            var guard = upper.AddState(CharacterAnimStates.UpperGuard);
            guard.motion = C("FightIdle");
            Transition(empty, guard, 0.12f).AddCondition(AnimatorConditionMode.If, 0, CharacterAnimStates.Guard);
            Transition(guard, empty, 0.15f).AddCondition(AnimatorConditionMode.IfNot, 0, CharacterAnimStates.Guard);
            foreach (var name in new[] { CharacterAnimStates.Jab, CharacterAnimStates.Cross, CharacterAnimStates.Hook, CharacterAnimStates.BlockHit })
            {
                var s = upper.AddState(name);
                s.motion = C(name);
                var toGuard = Transition(s, guard, 0.1f);
                toGuard.hasExitTime = true;
                toGuard.exitTime = 0.92f;
                toGuard.AddCondition(AnimatorConditionMode.If, 0, CharacterAnimStates.Guard);
                var toEmpty = Transition(s, empty, 0.12f);
                toEmpty.hasExitTime = true;
                toEmpty.exitTime = 0.92f;
                toEmpty.AddCondition(AnimatorConditionMode.IfNot, 0, CharacterAnimStates.Guard);
            }
            EditorUtility.SetDirty(ac);
            AssetDatabase.SaveAssets();
            return ac;
        }

        static void AddChild(BlendTree tree, AnimationClip clip, float threshold)
        {
            if (clip != null) tree.AddChild(clip, threshold);
        }

        static void AddChild2D(BlendTree tree, AnimationClip clip, Vector2 pos)
        {
            if (clip != null) tree.AddChild(clip, pos);
        }

        static AnimatorStateTransition Transition(AnimatorState from, AnimatorState to, float duration)
        {
            var t = from.AddTransition(to);
            t.hasExitTime = false;
            t.duration = duration;
            t.hasFixedDuration = true;
            return t;
        }

        static void ExitTo(AnimatorState s, AnimatorState fight, AnimatorState loco, float exitTime)
        {
            var a = Transition(s, fight, 0.12f);
            a.hasExitTime = true;
            a.exitTime = exitTime;
            a.AddCondition(AnimatorConditionMode.If, 0, CharacterAnimStates.Combat);
            var b = Transition(s, loco, 0.15f);
            b.hasExitTime = true;
            b.exitTime = exitTime;
            b.AddCondition(AnimatorConditionMode.IfNot, 0, CharacterAnimStates.Combat);
        }

        // ------------------------------------------------------------------ prefabs

        public static bool HasModel(string who) => AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath(who)) != null;

        /// <summary>Builds the character root with its model. Returns the prefab asset.</summary>
        public static GameObject BuildPrefab(string who, bool isPlayer)
        {
            EditorUtil.EnsureFolder(PrefabFolder);
            var root = new GameObject(isPlayer ? "Joueur" : "Adversaire");
            int layer = EditorUtil.Layer("Character");
            var manifest = LoadManifest(who);
            float height = manifest != null && manifest.height > 1f ? manifest.height : 1.78f;

            var cc = root.AddComponent<CharacterController>();
            cc.height = height - 0.02f;
            cc.radius = isPlayer ? 0.3f : 0.32f;
            cc.center = new Vector3(0f, cc.height / 2f + 0.01f, 0f);
            cc.slopeLimit = 45f;
            cc.stepOffset = 0.32f;
            cc.skinWidth = 0.03f;
            cc.minMoveDistance = 0f;
            root.AddComponent<CharacterMotor>();
            var fighter = root.AddComponent<Fighter>();
            fighter.team = isPlayer ? FighterTeam.Player : FighterTeam.Opponent;
            fighter.displayName = isPlayer ? "Toi" : "« Le Mur »";
            fighter.maxHealth = isPlayer ? 100f : 90f;
            fighter.bodyRadius = cc.radius;
            fighter.chestHeight = height * 0.74f;
            fighter.obstacleMask = LayerMask.GetMask("Default", "Environment", "Vehicle");

            CharacterAnimatorBase anim;
            if (HasModel(who))
            {
                RemapMaterials(who);
                var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath(who));
                var model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
                model.name = "Modele";
                model.transform.SetParent(root.transform, false);
                var animator = model.GetComponent<Animator>();
                if (animator == null) animator = model.AddComponent<Animator>();
                var avatar = AssetDatabase.LoadAllAssetsAtPath(FbxPath(who)).OfType<Avatar>().FirstOrDefault();
                if (avatar != null) animator.avatar = avatar;
                if (avatar == null || !avatar.isHuman)
                    Debug.LogError($"[PoingMort] L'avatar Humanoid de {who} n'est pas valide : vérifier l'import de {FbxPath(who)} (Rig > Humanoid).");
                animator.runtimeAnimatorController = BuildController(who, LoadClips(FbxPath(who)));
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                anim = model.AddComponent<MecanimCharacterAnimator>();
                foreach (var r in model.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    r.updateWhenOffscreen = false;
                    r.skinnedMotionVectors = false;
                }
            }
            else
            {
                Debug.LogWarning($"[PoingMort] {FbxPath(who)} absent : mannequin technique PROVISOIRE utilisé (pas la direction artistique).");
                anim = BuildPlaceholder(root.transform, isPlayer);
            }
            fighter.animator = anim;

            if (isPlayer)
            {
                var pc = root.AddComponent<PlayerController>();
                pc.exitBlockMask = LayerMask.GetMask("Default", "Environment", "Vehicle", "Character");
                pc.groundMask = LayerMask.GetMask("Default", "Environment");
            }
            else
            {
                var brain = root.AddComponent<OpponentBrain>();
                var lightGo = new GameObject("Annonce du coup");
                var hand = FindDeep(root.transform, "mixamorig:RightHand");
                lightGo.transform.SetParent(hand != null ? hand : root.transform, false);
                if (hand == null) lightGo.transform.localPosition = new Vector3(0.2f, 1.45f, 0.35f);
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, 0.25f, 0.18f);
                light.range = 1.6f;
                light.intensity = 3.5f;
                light.shadows = LightShadows.None;
                light.enabled = false;
                brain.telegraphLight = light;
            }
            EditorUtil.SetLayerRecursively(root, layer);
            string path = PrefabFolder + $"/PF_{(isPlayer ? "Player" : "Opponent")}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        public static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t)
            {
                var r = FindDeep(c, name);
                if (r != null) return r;
            }
            return null;
        }

        /// <summary>Technical stand-in, only used when the real character asset is missing.</summary>
        static CharacterAnimatorBase BuildPlaceholder(Transform root, bool isPlayer)
        {
            var body = new GameObject("Mannequin PROVISOIRE");
            body.transform.SetParent(root, false);
            var mat = MaterialLibrary.GetOrCreate(new MaterialLibrary.Spec { name = "M_Placeholder", color = isPlayer ? new Color(0.2f, 0.2f, 0.22f) : new Color(0.75f, 0.73f, 0.7f) });
            var torso = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            UnityEngine.Object.DestroyImmediate(torso.GetComponent<Collider>());
            torso.transform.SetParent(body.transform, false);
            torso.transform.localPosition = new Vector3(0f, 0.95f, 0f);
            torso.transform.localScale = new Vector3(0.5f, 0.9f, 0.32f);
            torso.GetComponent<Renderer>().sharedMaterial = mat;
            Transform Fist(string n, float x)
            {
                var f = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                UnityEngine.Object.DestroyImmediate(f.GetComponent<Collider>());
                f.name = n;
                f.transform.SetParent(body.transform, false);
                f.transform.localPosition = new Vector3(x, 1.25f, 0.2f);
                f.transform.localScale = Vector3.one * 0.12f;
                f.GetComponent<Renderer>().sharedMaterial = mat;
                return f.transform;
            }
            var anim = body.AddComponent<PlaceholderCharacterAnimator>();
            anim.body = torso.transform;
            anim.leftFist = Fist("Poing G", -0.22f);
            anim.rightFist = Fist("Poing D", 0.22f);
            return anim;
        }
    }
}

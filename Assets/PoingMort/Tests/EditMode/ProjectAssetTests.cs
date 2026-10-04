using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PoingMort.Characters;
using PoingMort.Combat;
using PoingMort.Core;
using PoingMort.EditorTools;
using PoingMort.Player;
using PoingMort.Traffic;
using PoingMort.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PoingMort.Tests
{
    /// <summary>
    /// Checks that need the Unity editor: model import (Humanoid avatars, clips, no T-pose), materials
    /// (nothing pink), textures, and the content of the generated scenes.
    /// </summary>
    public class ProjectAssetTests
    {
        static readonly string[] Characters = { "player", "opponent" };

        static IEnumerable<string> ModelPaths()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets/PoingMort/Art/Models" }))
                yield return AssetDatabase.GUIDToAssetPath(guid);
        }

        [Test]
        public void CharacterModelsAreValidHumanoids([ValueSource(nameof(Characters))] string who)
        {
            string path = CharacterSetup.FbxPath(who);
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            Assert.That(importer, Is.Not.Null, path + " introuvable");
            Assert.That(importer.animationType, Is.EqualTo(ModelImporterAnimationType.Human));
            var avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
            Assert.That(avatar, Is.Not.Null, "pas d'avatar");
            Assert.That(avatar.isValid && avatar.isHuman, Is.True, "avatar Humanoid invalide : vérifier Rig > Configure");
        }

        [Test]
        public void CharacterModelsContainEveryClip([ValueSource(nameof(Characters))] string who)
        {
            var clips = CharacterSetup.LoadClips(CharacterSetup.FbxPath(who));
            var missing = CharacterAnimStates.RequiredClips.Where(c => !clips.ContainsKey(c)).ToList();
            Assert.That(missing, Is.Empty, "clips manquants : " + string.Join(", ", missing));
            var importer = (ModelImporter)AssetImporter.GetAtPath(CharacterSetup.FbxPath(who));
            foreach (var c in importer.clipAnimations.Where(c => c.name == "Idle" || c.name == "Walk" || c.name == "Run" || c.name == "FightIdle"))
                Assert.That(c.loopTime, Is.True, c.name + " doit boucler");
        }

        /// <summary>Samples every clip: the arms must never be stretched out sideways at shoulder height (T-pose).</summary>
        [Test]
        public void NoClipShowsATPose([ValueSource(nameof(Characters))] string who)
        {
            string path = CharacterSetup.FbxPath(who);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var go = Object.Instantiate(model);
            try
            {
                var animator = go.GetComponent<Animator>();
                Assert.That(animator != null && animator.isHuman, Is.True);
                var clips = CharacterSetup.LoadClips(path);
                var problems = new List<string>();
                AnimationMode.StartAnimationMode();
                try
                {
                    // Clips where the body lies on the ground are not checked (arms may rest spread out).
                    foreach (var pair in clips.Where(c => c.Key != "KO" && c.Key != "Fall" && c.Key != "GetUp"))
                        for (int k = 0; k <= 4; k++)
                        {
                            float t = pair.Value.length * k / 4f;
                            AnimationMode.BeginSampling();
                            AnimationMode.SampleAnimationClip(go, pair.Value, t);
                            AnimationMode.EndSampling();
                            bool ArmOut(HumanBodyBones shoulder, HumanBodyBones hand)
                            {
                                Vector3 s = animator.GetBoneTransform(shoulder).position;
                                Vector3 h = animator.GetBoneTransform(hand).position;
                                return Mathf.Abs(Vector3.Dot(h - s, go.transform.right)) > 0.45f && Mathf.Abs(h.y - s.y) < 0.12f;
                            }
                            // A T-pose has both arms out at once (a hook only opens one).
                            if (ArmOut(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftHand) && ArmOut(HumanBodyBones.RightUpperArm, HumanBodyBones.RightHand))
                                problems.Add($"{pair.Key} à {t:0.00} s");
                        }
                }
                finally
                {
                    AnimationMode.StopAnimationMode();
                }
                Assert.That(problems, Is.Empty, "bras en T : " + string.Join(", ", problems));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void EveryModelMaterialHasALibraryMaterial()
        {
            var specs = ArtCatalogue.Specs();
            var unknown = new List<string>();
            foreach (var path in ModelPaths().Where(p => !p.Contains("/Characters/")))
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                var names = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>().Select(m => m.name)
                    .Concat(importer.GetExternalObjectMap().Keys.Where(k => k.type == typeof(Material)).Select(k => k.name));
                unknown.AddRange(names.Where(n => !specs.ContainsKey(n)).Select(n => $"{Path.GetFileName(path)} : {n}"));
            }
            Assert.That(unknown.Distinct(), Is.Empty, string.Join("\n", unknown.Distinct()));
        }

        [Test]
        public void LibraryTexturesExist()
        {
            var missing = new List<string>();
            foreach (var spec in ArtCatalogue.Specs().Values)
                foreach (var file in new[] { spec.texture, spec.normal })
                    if (!string.IsNullOrEmpty(file) && AssetDatabase.LoadAssetAtPath<Texture2D>(MaterialLibrary.TextureFolder + file) == null)
                        missing.Add(file);
            Assert.That(missing, Is.Empty, string.Join(", ", missing));
        }

        // ------------------------------------------------------------------ generated scenes

        static Scene OpenAdditive(string path)
        {
            if (!File.Exists(path)) Assert.Ignore($"{path} absent : lancer PoingMort > 2. Construire les scènes.");
            return EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        }

        static List<T> All<T>(Scene scene) where T : Component =>
            scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<T>(true)).ToList();

        static void AssertSceneIsClean(Scene scene)
        {
            var missingScripts = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true))
                .Where(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) > 0).Select(t => t.name).ToList();
            Assert.That(missingScripts, Is.Empty, "scripts manquants : " + string.Join(", ", missingScripts));
            var badMaterials = new List<string>();
            foreach (var r in All<Renderer>(scene))
                foreach (var m in r.sharedMaterials)
                    if (m == null || m.shader == null || !m.shader.isSupported || m.shader.name == "Hidden/InternalErrorShader")
                        badMaterials.Add($"{r.name} ({(m != null ? m.name : "aucun")})");
            Assert.That(badMaterials.Distinct(), Is.Empty, "matériaux manquants ou roses : " + string.Join(", ", badMaterials.Distinct()));
        }

        [Test]
        public void GameSceneHasEverySystem()
        {
            var scene = OpenAdditive(SceneBuilder.GameScenePath);
            try
            {
                AssertSceneIsClean(scene);
                Assert.That(All<PlayerController>(scene).Count, Is.EqualTo(1));
                Assert.That(All<GameSession>(scene).Count, Is.EqualTo(1));
                Assert.That(All<CameraRig>(scene).Count, Is.EqualTo(1));
                Assert.That(All<Hud>(scene).Count, Is.EqualTo(1));
                Assert.That(All<PauseMenu>(scene).Count, Is.EqualTo(1));
                var traffic = All<TrafficSystem>(scene).Single();
                Assert.That(traffic.lanes.Count(l => l != null && l.points.Count >= 3), Is.EqualTo(2));
                Assert.That(traffic.vehicles.Count(v => v != null), Is.GreaterThanOrEqualTo(4));
                foreach (var v in traffic.vehicles)
                {
                    Assert.That(v.seatAnchor && v.doorEntryLeft && v.doorEntryRight && v.exitPointLeft && v.exitPointRight, Is.True, v.name + " : repères manquants");
                    Assert.That(v.doorLeft != null && v.doorRight != null, Is.True, v.name + " : portières manquantes");
                    Assert.That(v.wheels.Length + v.steeringWheels.Length, Is.EqualTo(4), v.name + " : roues");
                }
                var arena = All<FightArena>(scene).Single();
                Assert.That(arena.opponent, Is.Not.Null);
                Assert.That(arena.playerStart != null && arena.opponentStart != null, Is.True);
                var player = All<PlayerController>(scene).Single();
                Assert.That(player.GetComponentInChildren<MecanimCharacterAnimator>(), Is.Not.Null, "le joueur doit utiliser le vrai personnage animé");
                Assert.That(arena.opponent.GetComponentInChildren<MecanimCharacterAnimator>(), Is.Not.Null, "l'adversaire doit utiliser le vrai personnage animé");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void MenuSceneHasTheHomeScreen()
        {
            var scene = OpenAdditive(SceneBuilder.MenuScenePath);
            try
            {
                AssertSceneIsClean(scene);
                var menu = All<MainMenuController>(scene).Single();
                Assert.That(menu.theme, Is.Not.Null);
                Assert.That(menu.theme.titleSprite, Is.Not.Null, "logo");
                Assert.That(All<Camera>(scene).Count, Is.EqualTo(1));
                Assert.That(All<Animator>(scene).Any(a => a.runtimeAnimatorController != null), Is.True, "personnage animé de l'accueil");
                Assert.That(EditorBuildSettings.scenes.Select(s => s.path), Does.Contain(SceneBuilder.MenuScenePath).And.Contain(SceneBuilder.GameScenePath));
                Assert.That(EditorBuildSettings.scenes[0].path, Is.EqualTo(SceneBuilder.MenuScenePath), "l'accueil doit être la première scène");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}

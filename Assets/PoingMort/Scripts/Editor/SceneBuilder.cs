using System;
using System.Collections.Generic;
using System.Reflection;
using PoingMort.Combat;
using PoingMort.Core;
using PoingMort.Player;
using PoingMort.Traffic;
using PoingMort.UI;
using PoingMort.Vehicles;
using PoingMort.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace PoingMort.EditorTools
{
    /// <summary>
    /// Rebuilds the two P01 scenes from the project's assets, reproducibly:
    /// MainMenu (home screen over the living street) and Quartier (traffic loop, boarding, courtyard fight).
    /// Re-running it overwrites both scenes; manual edits belong in prefabs or in this builder.
    /// </summary>
    public static class SceneBuilder
    {
        public const string SceneFolder = "Assets/PoingMort/Scenes";
        public const string SettingsFolder = ProjectSetup.SettingsFolder;
        const string AudioFolder = "Assets/PoingMort/Art/Audio/";

        public static string GameScenePath => $"{SceneFolder}/{SceneFlow.GameScene}.unity";
        public static string MenuScenePath => $"{SceneFolder}/{SceneFlow.MainMenuScene}.unity";

        // ------------------------------------------------------------------ entry points

        /// <summary>Builds every generated asset then both scenes. Returns false if something essential is missing.</summary>
        public static bool BuildEverything()
        {
            try
            {
                EditorUtility.DisplayProgressBar("Poing Mort", "Prefabs du décor…", 0.1f);
                CityKit.BuildAll();
                EditorUtility.DisplayProgressBar("Poing Mort", "Voiture…", 0.25f);
                if (VehicleSetup.Build() == null) return false;
                EditorUtility.DisplayProgressBar("Poing Mort", "Personnages…", 0.4f);
                CharacterSetup.BuildPrefab("player", true);
                CharacterSetup.BuildPrefab("opponent", false);
                EditorUtility.DisplayProgressBar("Poing Mort", "Thème d'interface…", 0.5f);
                var theme = BuildTheme();
                var credits = BuildCredits();
                EditorUtility.DisplayProgressBar("Poing Mort", "Scène Quartier…", 0.6f);
                BuildGameScene(theme);
                EditorUtility.DisplayProgressBar("Poing Mort", "Scène d'accueil…", 0.85f);
                BuildMenuScene(theme, credits);
                EditorBuildSettings.scenes = new[]
                {
                    new EditorBuildSettingsScene(MenuScenePath, true),
                    new EditorBuildSettingsScene(GameScenePath, true),
                };
                AssetDatabase.SaveAssets();
                Debug.Log("[PoingMort] Scènes construites : " + MenuScenePath + ", " + GameScenePath);
                return true;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        // ------------------------------------------------------------------ shared assets

        public static UITheme BuildTheme()
        {
            var theme = EditorUtil.LoadOrCreate<UITheme>(SettingsFolder + "/UITheme.asset");
            theme.bodyFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/PoingMort/Art/Fonts/Inter-Regular.otf");
            theme.boldFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/PoingMort/Art/Fonts/Inter-SemiBold.otf");
            theme.headingFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/PoingMort/Art/Fonts/BebasNeue-Bold.otf");
            theme.titleSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/PoingMort/Art/UI/T_Title_PoingMort.png");
            if (theme.bodyFont == null || theme.headingFont == null)
                Debug.LogWarning("[PoingMort] Polices du thème introuvables : la police intégrée d'Unity sera utilisée.");
            if (theme.titleSprite == null)
                Debug.LogWarning("[PoingMort] Logo T_Title_PoingMort introuvable (vérifier son import en Sprite).");
            EditorUtility.SetDirty(theme);
            return theme;
        }

        static TextAsset BuildCredits()
        {
            string path = SettingsFolder + "/Credits.txt";
            EditorUtil.EnsureFolder(SettingsFolder);
            System.IO.File.WriteAllText(path, MenuScreens.DefaultCredits);
            AssetDatabase.ImportAsset(path);
            return AssetDatabase.LoadAssetAtPath<TextAsset>(path);
        }

        static AudioClip Clip(string name) => AssetDatabase.LoadAssetAtPath<AudioClip>(AudioFolder + name + ".wav");

        static AudioClip[] Clips(params string[] names)
        {
            var list = new List<AudioClip>();
            foreach (var n in names)
            {
                var c = Clip(n);
                if (c != null) list.Add(c);
            }
            return list.ToArray();
        }

        // ------------------------------------------------------------------ Quartier

        public static void BuildGameScene(UITheme theme)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var layout = QuartierLayout.Create();
            foreach (var w in layout.warnings) Debug.LogWarning("[PoingMort] " + w);

            var session = new GameObject("Session de jeu");
            session.AddComponent<GameSession>();
            AddAudio(session, true);

            var sun = SetupLighting(scene);
            var world = BuildEnvironment(layout);
            var traffic = BuildTraffic(layout, world.transform, layout.cars.Count);

            // Player
            var spawn = new GameObject("Point de départ joueur").transform;
            spawn.SetPositionAndRotation(layout.playerSpawn, Quaternion.Euler(0f, layout.playerYaw, 0f));
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterSetup.PrefabFolder + "/PF_Player.prefab");
            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            player.transform.SetPositionAndRotation(spawn.position, spawn.rotation);
            var pc = player.GetComponent<PlayerController>();
            pc.respawnPoint = spawn;

            // Fight courtyard
            var arenaGo = new GameObject("Cour — combat de rue");
            arenaGo.transform.position = layout.arenaCentre;
            var arena = arenaGo.AddComponent<FightArena>();
            var pStart = new GameObject("Départ joueur").transform;
            pStart.SetParent(arenaGo.transform, false);
            pStart.SetPositionAndRotation(layout.playerStart, Quaternion.LookRotation(layout.opponentStart - layout.playerStart));
            var oStart = new GameObject("Départ adversaire").transform;
            oStart.SetParent(arenaGo.transform, false);
            oStart.SetPositionAndRotation(layout.opponentStart, Quaternion.LookRotation(layout.playerStart - layout.opponentStart));
            var oppPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterSetup.PrefabFolder + "/PF_Opponent.prefab");
            var opponent = (GameObject)PrefabUtility.InstantiatePrefab(oppPrefab);
            opponent.transform.SetPositionAndRotation(oStart.position, oStart.rotation);
            arena.opponent = opponent.GetComponent<OpponentBrain>();
            arena.playerStart = pStart;
            arena.opponentStart = oStart;
            arena.arenaRadius = 12f;

            // Camera
            var rigGo = new GameObject("Caméra joueur");
            Vector3 behind = -QuartierLayout.DirectionFromYaw(layout.playerYaw) * 3.4f;
            rigGo.transform.SetPositionAndRotation(layout.playerSpawn + behind + Vector3.up * 1.6f, Quaternion.Euler(10f, layout.playerYaw, 0f));
            var cam = CreateCamera(rigGo.transform, 58f);
            var rig = rigGo.AddComponent<CameraRig>();
            rig.collisionMask = LayerMask.GetMask("Default", "Environment");

            // Combat feedback (sounds and impact particles)
            var fx = new GameObject("Retours de combat");
            var feedback = fx.AddComponent<CombatFeedback>();
            feedback.fighters = new[] { player.GetComponent<Fighter>(), opponent.GetComponent<Fighter>() };
            feedback.swings = Clips("SFX_Swing_1", "SFX_Swing_2", "SFX_Swing_3");
            feedback.hits = Clips("SFX_Punch_Hit_1", "SFX_Punch_Hit_2", "SFX_Punch_Hit_3");
            feedback.heavyHits = Clips("SFX_Punch_Heavy_1", "SFX_Punch_Heavy_2");
            feedback.blocks = Clips("SFX_Block_1", "SFX_Block_2");
            feedback.knockOut = Clip("SFX_KO_BodyFall");
            feedback.impactParticles = ImpactParticles(fx.transform);

            // Interface
            var ui = new GameObject("Interface");
            ui.AddComponent<Hud>().theme = theme;
            ui.AddComponent<PauseMenu>().theme = theme;

            traffic.characterMask = LayerMask.GetMask("Character");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorUtil.EnsureFolder(SceneFolder);
            EditorSceneManager.SaveScene(scene, GameScenePath);
        }

        // ------------------------------------------------------------------ MainMenu

        public static void BuildMenuScene(UITheme theme, TextAsset credits)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var layout = QuartierLayout.Create();
            var audio = new GameObject("Sons");
            AddAudio(audio, true);
            SetupLighting(scene);
            var world = BuildEnvironment(layout);
            BuildTraffic(layout, world.transform, 4);

            // The player character, idle on the sidewalk (model + animator only: no gameplay script).
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterSetup.FbxPath("player"));
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(CharacterSetup.AnimFolder + "/AC_Player.controller");
            if (fbx != null)
            {
                var hero = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
                hero.name = "Personnage (accueil)";
                hero.transform.SetPositionAndRotation(layout.menuCharacter, Quaternion.Euler(0f, layout.menuCharacterYaw, 0f));
                var animator = hero.GetComponent<Animator>();
                if (animator == null) animator = hero.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                EditorUtil.SetLayerRecursively(hero, EditorUtil.Layer("Character"));
            }
            else Debug.LogWarning("[PoingMort] Personnage absent de l'écran d'accueil (PM_Player.fbx introuvable).");

            var camGo = new GameObject("Caméra accueil");
            camGo.transform.SetPositionAndRotation(layout.menuCamera, Quaternion.LookRotation(layout.menuLookAt - layout.menuCamera));
            CreateCamera(camGo.transform, layout.menuFov, ownObject: true);
            var drift = camGo.AddComponent<CameraDrift>();
            drift.amplitude = 0.08f;
            drift.rotationAmplitude = 0.5f;

            var menu = new GameObject("Accueil");
            var controllerUi = menu.AddComponent<MainMenuController>();
            controllerUi.theme = theme;
            controllerUi.credits = credits;
            controllerUi.versionLabel = "Prototype P01 — version " + PlayerSettings.bundleVersion;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorUtil.EnsureFolder(SceneFolder);
            EditorSceneManager.SaveScene(scene, MenuScenePath);
        }

        // ------------------------------------------------------------------ pieces

        static void AddAudio(GameObject go, bool ambience)
        {
            var a = go.AddComponent<GameAudio>();
            a.uiClick = Clip("SFX_UI_Click");
            a.uiHover = Clip("SFX_UI_Hover");
            a.whistle = Clip("SFX_Whistle");
            a.doorOpen = Clip("SFX_Door_Open");
            a.doorClose = Clip("SFX_Door_Close");
            if (ambience) a.ambience = Clip("AMB_City_Loop");
        }

        static Camera CreateCamera(Transform parent, float fov, bool ownObject = false)
        {
            GameObject go;
            if (ownObject) go = parent.gameObject;
            else
            {
                go = new GameObject("Caméra");
                go.transform.SetParent(parent, false);
            }
            go.tag = "MainCamera";
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.08f;
            cam.farClipPlane = 450f;
            cam.clearFlags = CameraClearFlags.Skybox;
            go.AddComponent<AudioListener>();
            var dataType = FindType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData");
            if (dataType != null)
            {
                var data = go.GetComponent(dataType);
                if (data == null) data = go.AddComponent(dataType);
                SetMember(data, "renderPostProcessing", true);
                SetMember(data, "antialiasing", "SubpixelMorphologicalAntiAliasing");
                SetMember(data, "renderShadows", true);
            }
            return cam;
        }

        static ParticleSystem ImpactParticles(Transform parent)
        {
            var go = new GameObject("Impacts");
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.5f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.38f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 3.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.06f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.95f, 0.92f, 0.85f, 0.9f), new Color(0.75f, 0.7f, 0.65f, 0.6f));
            main.gravityModifier = 0.6f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 200;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.05f;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
            if (shader != null)
            {
                string path = MaterialLibrary.Folder + "/M_ImpactDust.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null)
                {
                    mat = new Material(shader) { name = "M_ImpactDust" };
                    AssetDatabase.CreateAsset(mat, path);
                }
                mat.shader = shader;
                renderer.sharedMaterial = mat;
            }
            return ps;
        }

        static readonly Dictionary<string, Transform> s_Groups = new Dictionary<string, Transform>();

        static Transform Group(Transform root, string name)
        {
            if (s_Groups.TryGetValue(name, out var t) && t != null) return t;
            t = new GameObject(name).transform;
            t.SetParent(root, false);
            s_Groups[name] = t;
            return t;
        }

        /// <summary>Ground, buildings, street furniture and invisible limits of the neighbourhood.</summary>
        static GameObject BuildEnvironment(QuartierLayout layout)
        {
            s_Groups.Clear();
            var root = new GameObject("Quartier");
            GroundBuilder.Build(root.transform, layout.path);
            var missing = new HashSet<string>();
            foreach (var p in layout.placements)
            {
                var prefab = CityKit.Get(p.asset);
                if (prefab == null)
                {
                    missing.Add(p.asset);
                    continue;
                }
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, Group(root.transform, p.group));
                go.transform.SetPositionAndRotation(p.position, Quaternion.Euler(0f, p.yaw, 0f));
                go.transform.localScale = p.scale;
                if (!string.IsNullOrEmpty(p.label)) go.name = p.label;
            }
            foreach (var m in missing)
                Debug.LogWarning($"[PoingMort] Prefab de décor PF_{m} absent : élément ignoré.");

            var limits = Group(root.transform, "Limites (invisibles)");
            foreach (var (centre, size) in layout.boundaries)
            {
                var b = new GameObject("Limite");
                b.transform.SetParent(limits, false);
                b.transform.position = centre;
                b.AddComponent<BoxCollider>().size = size;
            }
            return root;
        }

        static TrafficSystem BuildTraffic(QuartierLayout layout, Transform parent, int carCount)
        {
            var go = new GameObject("Trafic");
            go.transform.SetParent(parent, false);
            var traffic = go.AddComponent<TrafficSystem>();

            var inner = new GameObject("Voie de droite (trottoir côté îlot)").AddComponent<TrafficLane>();
            inner.transform.SetParent(go.transform, false);
            inner.points = new List<Vector3>(layout.innerLane);
            inner.kerbOnRight = true;
            var outer = new GameObject("Voie de gauche").AddComponent<TrafficLane>();
            outer.transform.SetParent(go.transform, false);
            outer.points = new List<Vector3>(layout.outerLane);
            outer.kerbOnRight = false;
            outer.rightNeighbour = inner;
            inner.leftNeighbour = outer;
            traffic.lanes = new List<TrafficLane> { inner, outer };

            var carPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(VehicleSetup.PrefabPath);
            traffic.vehicles = new List<TrafficVehicle>();
            if (carPrefab == null)
            {
                Debug.LogError("[PoingMort] Prefab de voiture absent : trafic vide.");
                return traffic;
            }
            for (int i = 0; i < Mathf.Min(carCount, layout.cars.Count); i++)
            {
                var spawn = layout.cars[i];
                var car = (GameObject)PrefabUtility.InstantiatePrefab(carPrefab, go.transform);
                car.name = "Voiture " + (i + 1);
                car.transform.SetPositionAndRotation(spawn.position, Quaternion.Euler(0f, spawn.yaw, 0f));
                VehicleSetup.Paint(car, spawn.paint);
                traffic.vehicles.Add(car.GetComponent<TrafficVehicle>());
            }
            traffic.characterMask = LayerMask.GetMask("Character");
            return traffic;
        }

        // ------------------------------------------------------------------ lighting

        /// <summary>Late-afternoon light: low warm sun, cool sky fill, light haze, realtime only (nothing to bake).</summary>
        static Light SetupLighting(UnityEngine.SceneManagement.Scene scene)
        {
            var sunGo = new GameObject("Soleil");
            sunGo.transform.rotation = Quaternion.Euler(17.5f, 141f, 0f);
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.79f, 0.58f);
            sun.intensity = 1.55f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.82f;
            sun.shadowBias = 0.04f;
            sun.shadowNormalBias = 0.35f;

            RenderSettings.sun = sun;
            RenderSettings.skybox = MaterialLibrary.Skybox();
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.44f, 0.47f, 0.56f);
            RenderSettings.ambientEquatorColor = new Color(0.4f, 0.36f, 0.34f);
            RenderSettings.ambientGroundColor = new Color(0.17f, 0.15f, 0.14f);
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0062f;
            RenderSettings.fogColor = new Color(0.66f, 0.58f, 0.55f);
            RenderSettings.reflectionIntensity = 0.8f;

            // Realtime-only lighting settings asset (no lightmaps to bake, nothing stale in git).
            EditorUtil.EnsureFolder(SettingsFolder);
            string lsPath = SettingsFolder + "/PM_Lighting.lighting";
            var ls = AssetDatabase.LoadAssetAtPath<LightingSettings>(lsPath);
            if (ls == null)
            {
                ls = new LightingSettings { name = "PM_Lighting" };
                AssetDatabase.CreateAsset(ls, lsPath);
            }
            ls.bakedGI = false;
            ls.realtimeGI = false;
            EditorUtility.SetDirty(ls);
            Lightmapping.SetLightingSettingsForScene(scene, ls);

            // Reflections of the actual street on glass and car paint, captured once at load.
            var probeGo = new GameObject("Reflets (sonde temps réel)");
            probeGo.transform.position = new Vector3(0f, 3f, -22f);
            var probe = probeGo.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
            probe.resolution = 128;
            probe.size = new Vector3(170f, 50f, 110f);
            probe.boxProjection = false;
            probe.importance = 1;

            SetupPostProcessing();
            return sun;
        }

        /// <summary>URP global volume (tonemapping, bloom, grading, vignette), reached by reflection.</summary>
        static void SetupPostProcessing()
        {
            var volumeType = FindType("UnityEngine.Rendering.Volume");
            var profileType = FindType("UnityEngine.Rendering.VolumeProfile");
            if (volumeType == null || profileType == null)
            {
                Debug.LogWarning("[PoingMort] Post-traitement URP indisponible (paquet absent) : scène sans étalonnage.");
                return;
            }
            string path = SettingsFolder + "/PM_PostProcess.asset";
            var profile = AssetDatabase.LoadAssetAtPath(path, profileType) as ScriptableObject;
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance(profileType);
                AssetDatabase.CreateAsset(profile, path);
                Override(profile, "UnityEngine.Rendering.Universal.Tonemapping", ("mode", "ACES"));
                Override(profile, "UnityEngine.Rendering.Universal.Bloom", ("threshold", 1.05f), ("intensity", 0.55f), ("scatter", 0.6f));
                Override(profile, "UnityEngine.Rendering.Universal.ColorAdjustments", ("postExposure", 0.25f), ("contrast", 10f), ("saturation", -10f));
                Override(profile, "UnityEngine.Rendering.Universal.WhiteBalance", ("temperature", 9f), ("tint", 2f));
                Override(profile, "UnityEngine.Rendering.Universal.Vignette", ("intensity", 0.24f), ("smoothness", 0.45f));
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssets();
            }
            var go = new GameObject("Post-traitement");
            var volume = go.AddComponent(volumeType);
            SetMember(volume, "isGlobal", true);
            SetMember(volume, "sharedProfile", profile);
            SetMember(volume, "priority", 0f);
        }

        static void Override(ScriptableObject profile, string componentType, params (string field, object value)[] values)
        {
            var type = FindType(componentType);
            if (type == null) return;
            var add = profile.GetType().GetMethod("Add", new[] { typeof(Type), typeof(bool) });
            if (add == null) return;
            var component = add.Invoke(profile, new object[] { type, false }) as ScriptableObject;
            if (component == null) return;
            component.name = type.Name;
            component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(component, profile);
            SetMember(component, "active", true);
            foreach (var (field, value) in values)
            {
                var f = type.GetField(field, BindingFlags.Public | BindingFlags.Instance);
                var parameter = f?.GetValue(component);
                if (parameter == null) continue;
                var valueProp = parameter.GetType().GetProperty("value", BindingFlags.Public | BindingFlags.Instance);
                if (valueProp == null) continue;
                try
                {
                    object v = value;
                    if (valueProp.PropertyType.IsEnum && value is string s) v = Enum.Parse(valueProp.PropertyType, s);
                    else if (!valueProp.PropertyType.IsInstanceOfType(value)) v = Convert.ChangeType(value, valueProp.PropertyType);
                    valueProp.SetValue(parameter, v);
                    SetMember(parameter, "overrideState", true);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[PoingMort] Réglage {type.Name}.{field} ignoré : {e.Message}");
                }
            }
            EditorUtility.SetDirty(component);
        }

        // ------------------------------------------------------------------ reflection helpers

        internal static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName, false);
                if (t != null) return t;
            }
            return null;
        }

        internal static void SetMember(object target, string name, object value)
        {
            if (target == null) return;
            var type = target.GetType();
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
            var prop = type.GetProperty(name, flags);
            var field = prop == null ? type.GetField(name, flags) : null;
            Type valueType = prop != null ? prop.PropertyType : field?.FieldType;
            if (valueType == null || (prop != null && !prop.CanWrite)) return;
            try
            {
                object v = value;
                if (valueType.IsEnum && value is string s) v = Enum.Parse(valueType, s);
                else if (value != null && !valueType.IsInstanceOfType(value)) v = Convert.ChangeType(value, valueType);
                if (prop != null) prop.SetValue(target, v);
                else field.SetValue(target, v);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PoingMort] {type.Name}.{name} non réglé : {e.Message}");
            }
        }
    }
}

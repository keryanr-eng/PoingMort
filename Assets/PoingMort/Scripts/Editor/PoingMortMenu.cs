using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PoingMort.EditorTools
{
    /// <summary>
    /// "PoingMort" menu of the editor: one-time project setup, reproducible scene build, play from the home screen.
    /// On the first opening of the project, offers to run the two steps.
    /// </summary>
    [InitializeOnLoad]
    public static class PoingMortMenu
    {
        const string AskedKey = "PoingMort.SetupOffered";

        static PoingMortMenu()
        {
            EditorApplication.delayCall += OfferSetup;
        }

        static void OfferSetup()
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorPrefs.GetBool(AskedKey + "." + Application.dataPath, false)) return;
            if (System.IO.File.Exists(SceneBuilder.GameScenePath) && System.IO.File.Exists(SceneBuilder.MenuScenePath)) return;
            EditorPrefs.SetBool(AskedKey + "." + Application.dataPath, true);
            if (EditorUtility.DisplayDialog("Poing Mort",
                    "Première ouverture du projet : configurer le projet (URP, couches, Input System) puis construire les scènes ?\n\n" +
                    "Ces commandes restent disponibles dans le menu PoingMort.", "Configurer et construire", "Plus tard"))
            {
                SetupAndBuild();
            }
        }

        [MenuItem("PoingMort/1. Configurer le projet", priority = 1)]
        public static void Configure() => ProjectSetup.Run();

        [MenuItem("PoingMort/2. Construire les scènes", priority = 2)]
        public static void Build()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Poing Mort", "Quitte le mode Play avant de reconstruire les scènes.", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            bool ok = SceneBuilder.BuildEverything();
            if (ok) EditorSceneManager.OpenScene(SceneBuilder.MenuScenePath);
            if (!Application.isBatchMode)
                EditorUtility.DisplayDialog("Poing Mort", ok
                    ? "Scènes construites : MainMenu (accueil) et Quartier (jeu).\nLance PoingMort > 3. Jouer depuis l'accueil."
                    : "La construction a échoué : voir la console.", "OK");
        }

        [MenuItem("PoingMort/3. Jouer depuis l'accueil", priority = 3)]
        public static void Play()
        {
            if (!System.IO.File.Exists(SceneBuilder.MenuScenePath))
            {
                EditorUtility.DisplayDialog("Poing Mort", "Scènes absentes : lance d'abord PoingMort > 2. Construire les scènes.", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(SceneBuilder.MenuScenePath);
            EditorApplication.isPlaying = true;
        }

        [MenuItem("PoingMort/Configurer et construire (tout)", priority = 20)]
        public static void SetupAndBuild()
        {
            ProjectSetup.Run();
            Build();
        }

        /// <summary>Command line: Unity -batchmode -projectPath . -executeMethod PoingMort.EditorTools.PoingMortMenu.BatchBuild -quit</summary>
        public static void BatchBuild()
        {
            ProjectSetup.Run();
            bool ok = SceneBuilder.BuildEverything();
            if (!ok) EditorApplication.Exit(1);
        }

        [MenuItem("PoingMort/Ouvrir la scène Quartier", priority = 40)]
        public static void OpenGame()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(SceneBuilder.GameScenePath);
        }
    }
}

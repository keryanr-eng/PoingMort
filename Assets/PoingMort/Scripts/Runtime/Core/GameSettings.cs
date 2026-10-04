using System;
using UnityEngine;

namespace PoingMort.Core
{
    /// <summary>
    /// Player options persisted with PlayerPrefs (local, per machine).
    /// Every option exposed in the menus is implemented here; nothing is simulated.
    /// </summary>
    public static class GameSettings
    {
        const string Prefix = "poingmort.settings.";

        public static float MasterVolume { get; private set; } = 0.8f;
        public static float EffectsVolume { get; private set; } = 1f;
        public static float MouseSensitivity { get; private set; } = 1f;
        public static bool InvertY { get; private set; }
        public static float CameraShake { get; private set; } = 1f;
        public static int QualityLevel { get; private set; } = -1;
        public static bool Fullscreen { get; private set; } = true;

        static bool s_Loaded;

        public static event Action Changed;

        public static void EnsureLoaded()
        {
            if (!s_Loaded) Load();
        }

        public static void Load()
        {
            s_Loaded = true;
            MasterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix + "masterVolume", 0.8f));
            EffectsVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix + "effectsVolume", 1f));
            MouseSensitivity = Mathf.Clamp(PlayerPrefs.GetFloat(Prefix + "mouseSensitivity", 1f), 0.2f, 3f);
            InvertY = PlayerPrefs.GetInt(Prefix + "invertY", 0) == 1;
            CameraShake = Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix + "cameraShake", 1f));
            QualityLevel = PlayerPrefs.GetInt(Prefix + "quality", -1);
            Fullscreen = PlayerPrefs.GetInt(Prefix + "fullscreen", 1) == 1;
            Apply();
        }

        public static void Save()
        {
            PlayerPrefs.SetFloat(Prefix + "masterVolume", MasterVolume);
            PlayerPrefs.SetFloat(Prefix + "effectsVolume", EffectsVolume);
            PlayerPrefs.SetFloat(Prefix + "mouseSensitivity", MouseSensitivity);
            PlayerPrefs.SetInt(Prefix + "invertY", InvertY ? 1 : 0);
            PlayerPrefs.SetFloat(Prefix + "cameraShake", CameraShake);
            PlayerPrefs.SetInt(Prefix + "quality", QualityLevel);
            PlayerPrefs.SetInt(Prefix + "fullscreen", Fullscreen ? 1 : 0);
            PlayerPrefs.Save();
        }

        public static void Apply()
        {
            AudioListener.volume = MasterVolume;
            int levels = QualitySettings.names.Length;
            if (QualityLevel >= 0 && QualityLevel < levels && QualitySettings.GetQualityLevel() != QualityLevel)
                QualitySettings.SetQualityLevel(QualityLevel, true);
            if (!Application.isEditor && Screen.fullScreen != Fullscreen)
                Screen.fullScreen = Fullscreen;
        }

        static void Commit()
        {
            Apply();
            Save();
            Changed?.Invoke();
        }

        public static void SetMasterVolume(float v) { MasterVolume = Mathf.Clamp01(v); Commit(); }
        public static void SetEffectsVolume(float v) { EffectsVolume = Mathf.Clamp01(v); Commit(); }
        public static void SetMouseSensitivity(float v) { MouseSensitivity = Mathf.Clamp(v, 0.2f, 3f); Commit(); }
        public static void SetInvertY(bool v) { InvertY = v; Commit(); }
        public static void SetCameraShake(float v) { CameraShake = Mathf.Clamp01(v); Commit(); }
        public static void SetFullscreen(bool v) { Fullscreen = v; Commit(); }

        public static void SetQualityLevel(int level)
        {
            QualityLevel = Mathf.Clamp(level, 0, Mathf.Max(0, QualitySettings.names.Length - 1));
            Commit();
        }

        public static int CurrentQualityIndex => QualityLevel >= 0 ? QualityLevel : QualitySettings.GetQualityLevel();

        /// <summary>Used by tests and the "reset" button.</summary>
        public static void ResetToDefaults()
        {
            MasterVolume = 0.8f;
            EffectsVolume = 1f;
            MouseSensitivity = 1f;
            InvertY = false;
            CameraShake = 1f;
            QualityLevel = -1;
            Fullscreen = true;
            Commit();
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace PoingMort.Core
{
    /// <summary>
    /// Sounds shared by a scene: interface clicks, short world sounds (doors, whistle) and the city
    /// ambience. The master volume goes through the AudioListener; everything here also follows the
    /// "effects" option. Interface sounds keep playing while the game is paused.
    /// </summary>
    [DefaultExecutionOrder(-950)]
    public sealed class GameAudio : MonoBehaviour
    {
        public static GameAudio Current { get; private set; }

        [Header("Interface")]
        public AudioClip uiClick;
        public AudioClip uiHover;
        public float uiVolume = 0.55f;

        [Header("Monde")]
        public AudioClip whistle;
        public AudioClip doorOpen;
        public AudioClip doorClose;
        public float worldVolume = 0.9f;

        [Header("Ambiance")]
        public AudioClip ambience;
        public float ambienceVolume = 0.32f;

        AudioSource m_Ui;
        AudioSource m_Ambience;
        readonly List<AudioSource> m_World = new List<AudioSource>();
        int m_Next;
        float m_LastHover = -1f;

        void Awake()
        {
            Current = this;
            m_Ui = gameObject.AddComponent<AudioSource>();
            m_Ui.playOnAwake = false;
            m_Ui.spatialBlend = 0f;
            m_Ui.ignoreListenerPause = true;
            for (int i = 0; i < 4; i++)
            {
                var go = new GameObject("Son " + i);
                go.transform.SetParent(transform, false);
                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 1f;
                src.minDistance = 2f;
                src.maxDistance = 35f;
                src.rolloffMode = AudioRolloffMode.Linear;
                m_World.Add(src);
            }
            if (ambience != null)
            {
                m_Ambience = gameObject.AddComponent<AudioSource>();
                m_Ambience.clip = ambience;
                m_Ambience.loop = true;
                m_Ambience.spatialBlend = 0f;
                m_Ambience.playOnAwake = false;
                ApplyVolumes();
                m_Ambience.Play();
            }
        }

        void OnEnable() => GameSettings.Changed += ApplyVolumes;
        void OnDisable() => GameSettings.Changed -= ApplyVolumes;

        void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        void ApplyVolumes()
        {
            if (m_Ambience != null) m_Ambience.volume = ambienceVolume * GameSettings.EffectsVolume;
        }

        /// <summary>Button validated (click = true) or selection moved (hover).</summary>
        public static void Ui(bool click)
        {
            var a = Current;
            if (a == null || a.m_Ui == null) return;
            var clip = click ? a.uiClick : a.uiHover;
            if (clip == null) return;
            if (!click)
            {
                // No hover tick while a screen is opening (first selection) or for very fast navigation.
                if (Time.timeSinceLevelLoad < 0.5f || Time.unscaledTime - a.m_LastHover < 0.06f) return;
                a.m_LastHover = Time.unscaledTime;
            }
            a.m_Ui.PlayOneShot(clip, a.uiVolume * GameSettings.EffectsVolume * (click ? 1f : 0.6f));
        }

        public static void PlayAt(AudioClip clip, Vector3 position, float volume = 1f)
        {
            var a = Current;
            if (a == null || clip == null || a.m_World.Count == 0) return;
            var src = a.m_World[a.m_Next];
            a.m_Next = (a.m_Next + 1) % a.m_World.Count;
            src.transform.position = position;
            src.pitch = 1f + Random.Range(-0.04f, 0.04f);
            src.volume = volume * a.worldVolume * GameSettings.EffectsVolume;
            src.clip = clip;
            src.Play();
        }

        public static void Whistle(Vector3 position) { if (Current != null) PlayAt(Current.whistle, position, 0.8f); }
        public static void DoorOpened(Vector3 position) { if (Current != null) PlayAt(Current.doorOpen, position, 0.7f); }
        public static void DoorClosed(Vector3 position) { if (Current != null) PlayAt(Current.doorClose, position, 0.8f); }
    }
}

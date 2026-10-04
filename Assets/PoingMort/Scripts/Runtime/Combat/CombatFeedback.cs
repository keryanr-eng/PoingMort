using System.Collections.Generic;
using PoingMort.Core;
using PoingMort.Player;
using UnityEngine;

namespace PoingMort.Combat
{
    /// <summary>
    /// Sounds, impact particles and camera impulses for punches. Purely presentational:
    /// it never changes timing or gameplay, and never freezes anything globally.
    /// </summary>
    public sealed class CombatFeedback : MonoBehaviour
    {
        public Fighter[] fighters = new Fighter[0];

        [Header("Sons")]
        public AudioClip[] swings = new AudioClip[0];
        public AudioClip[] hits = new AudioClip[0];
        public AudioClip[] heavyHits = new AudioClip[0];
        public AudioClip[] blocks = new AudioClip[0];
        public AudioClip knockOut;
        public float volume = 0.9f;

        [Header("Effets")]
        public ParticleSystem impactParticles;
        public int lightImpactCount = 10;
        public int heavyImpactCount = 22;

        readonly List<AudioSource> m_Pool = new List<AudioSource>();
        int m_Next;

        void Awake()
        {
            for (int i = 0; i < 6; i++)
            {
                var go = new GameObject("Son combat " + i);
                go.transform.SetParent(transform, false);
                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0.85f;
                src.minDistance = 2f;
                src.maxDistance = 40f;
                src.rolloffMode = AudioRolloffMode.Linear;
                m_Pool.Add(src);
            }
        }

        void OnEnable()
        {
            foreach (var f in fighters)
            {
                if (f == null) continue;
                f.AttackStarted += OnAttackStarted;
                f.AttackLanded += OnAttackLanded;
                f.KnockedOut += OnKnockedOut;
            }
        }

        void OnDisable()
        {
            foreach (var f in fighters)
            {
                if (f == null) continue;
                f.AttackStarted -= OnAttackStarted;
                f.AttackLanded -= OnAttackLanded;
                f.KnockedOut -= OnKnockedOut;
            }
        }

        void Play(AudioClip[] clips, Vector3 position, float vol, float pitchJitter = 0.08f)
        {
            if (clips == null || clips.Length == 0) return;
            Play(clips[Random.Range(0, clips.Length)], position, vol, pitchJitter);
        }

        void Play(AudioClip clip, Vector3 position, float vol, float pitchJitter = 0.06f)
        {
            if (clip == null || m_Pool.Count == 0) return;
            var src = m_Pool[m_Next];
            m_Next = (m_Next + 1) % m_Pool.Count;
            src.transform.position = position;
            src.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            src.volume = vol * volume * GameSettings.EffectsVolume;
            src.clip = clip;
            src.Play();
        }

        void OnAttackStarted(Fighter attacker, AttackDefinition attack)
        {
            Play(swings, attacker.Chest, attack.heavy ? 0.75f : 0.5f, 0.12f);
        }

        void OnAttackLanded(Fighter attacker, AttackDefinition attack, HitResult result, Vector3 point)
        {
            // The prototype only has fights involving the player, so every landed punch gets a (optional) camera impulse.
            var cam = CameraRig.Current;
            switch (result)
            {
                case HitResult.Hit:
                case HitResult.KnockOut:
                    Play(attack.heavy ? (heavyHits.Length > 0 ? heavyHits : hits) : hits, point, attack.heavy ? 1f : 0.85f);
                    Emit(point, attack.heavy ? heavyImpactCount : lightImpactCount);
                    if (cam != null) cam.Shake(attack.cameraShake);
                    break;
                case HitResult.Blocked:
                    Play(blocks, point, 0.7f);
                    Emit(point, lightImpactCount / 2);
                    if (cam != null) cam.Shake(attack.cameraShake * 0.4f);
                    break;
            }
        }

        void OnKnockedOut(Fighter fighter)
        {
            Play(knockOut, fighter.transform.position + Vector3.up * 0.3f, 1f, 0.03f);
            if (CameraRig.Current != null) CameraRig.Current.Shake(0.6f);
        }

        void Emit(Vector3 point, int count)
        {
            if (impactParticles == null || count <= 0) return;
            impactParticles.transform.position = point;
            impactParticles.Emit(count);
        }
    }
}

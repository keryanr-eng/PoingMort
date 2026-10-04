using System;
using System.Collections.Generic;
using PoingMort.Characters;
using UnityEngine;

namespace PoingMort.Combat
{
    public enum FighterTeam { Player, Opponent }

    /// <summary>
    /// Fist fighting for one character: health, punches with anticipation/contact/recovery windows,
    /// guard, dodge with invulnerability, stagger and KO. Damage only happens during the active window,
    /// within range, in front of the attacker and with a clear line of sight (no hit through walls).
    /// </summary>
    public sealed class Fighter : MonoBehaviour
    {
        static readonly List<Fighter> s_All = new List<Fighter>();
        public static IReadOnlyList<Fighter> All => s_All;

        public FighterTeam team = FighterTeam.Opponent;
        public string displayName = "Adversaire";
        public float maxHealth = 100f;

        [Header("Coups")]
        public AttackDefinition jab = AttackDefinition.Jab();
        public AttackDefinition cross = AttackDefinition.Cross();
        public AttackDefinition hook = AttackDefinition.Hook();
        [Tooltip("Temps maximal pendant lequel une demande de coup est mémorisée (s).")]
        public float inputBuffer = 0.25f;
        [Tooltip("Délai sans frapper après lequel l'enchaînement repart du premier coup (s).")]
        public float comboReset = 0.7f;

        [Header("Corps")]
        public float bodyRadius = 0.32f;
        public float chestHeight = 1.3f;
        [Tooltip("Couches qui bloquent les coups (murs, voitures...).")]
        public LayerMask obstacleMask = ~0;

        [Header("Défense")]
        public float dodgeDuration = 0.42f;
        public float dodgeInvulnerableFrom = 0.04f;
        public float dodgeInvulnerableTo = 0.32f;
        public float dodgeSpeed = 5.2f;
        public float dodgeCooldown = 0.25f;
        public float blockStun = 0.16f;

        [Header("Références")]
        public CharacterAnimatorBase animator;
        public CharacterMotor motor;

        readonly AttackTimeline m_Timeline = new AttackTimeline();
        HealthPool m_Health;
        int m_ComboIndex;
        float m_LastAttackEnd = -10f;
        AttackDefinition m_Buffered;
        float m_BufferedTime;
        float m_StaggerTimer;
        float m_DodgeTimer;
        float m_DodgeCooldownTimer;
        float m_HitStopTimer;
        bool m_Guarding;
        bool m_Locked;

        public HealthPool Health => m_Health ??= new HealthPool(maxHealth);
        public bool IsKnockedOut => Health.IsDepleted;
        public bool IsGuarding => m_Guarding && !IsBusy && !IsKnockedOut;
        public bool IsAttacking => m_Timeline.IsRunning;
        public bool IsStaggered => m_StaggerTimer > 0f;
        public bool IsDodging => m_DodgeTimer > 0f;
        public bool IsInvulnerable => IsDodging && (dodgeDuration - m_DodgeTimer) >= dodgeInvulnerableFrom && (dodgeDuration - m_DodgeTimer) <= dodgeInvulnerableTo;
        public bool IsBusy => IsAttacking || IsStaggered || IsDodging;
        public AttackPhase CurrentPhase => m_Timeline.Phase;
        public AttackDefinition CurrentAttack => m_Timeline.Attack;
        public float AttackTime => m_Timeline.Time;
        public float LocalTimeScale => m_HitStopTimer > 0f ? 0.06f : 1f;
        /// <summary>Gameplay can lock a fighter (e.g. not in a fight, seated in a car).</summary>
        public bool Locked { get => m_Locked; set { m_Locked = value; if (value) { m_Guarding = false; m_Timeline.Cancel(); } } }

        public Vector3 Chest => transform.position + Vector3.up * chestHeight;

        public event Action<Fighter, AttackDefinition> AttackStarted;
        public event Action<Fighter, AttackDefinition, HitResult, Vector3> AttackLanded; // attacker side
        public event Action<Fighter, AttackDefinition, HitResult, float> Damaged;       // defender side
        public event Action<Fighter> KnockedOut;
        public event Action<Fighter> Revived;

        void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<CharacterAnimatorBase>();
            if (motor == null) motor = GetComponent<CharacterMotor>();
            m_Health ??= new HealthPool(maxHealth);
        }

        void OnEnable() => s_All.Add(this);
        void OnDisable() => s_All.Remove(this);

        public void SetGuard(bool guard)
        {
            if (Locked || IsKnockedOut) guard = false;
            m_Guarding = guard;
            if (animator != null) animator.SetGuard(IsGuarding);
        }

        public bool TryLightAttack() => Request(NextLight());
        public bool TryHeavyAttack() => Request(hook);

        AttackDefinition NextLight()
        {
            if (Time.time - m_LastAttackEnd > comboReset && !IsAttacking) m_ComboIndex = 0;
            return m_ComboIndex % 2 == 0 ? jab : cross;
        }

        bool Request(AttackDefinition attack)
        {
            if (attack == null || Locked || IsKnockedOut) return false;
            if (IsStaggered || IsDodging || (IsAttacking && !m_Timeline.CanChain))
            {
                m_Buffered = attack;
                m_BufferedTime = Time.time;
                return false;
            }
            StartAttack(attack);
            return true;
        }

        void StartAttack(AttackDefinition attack)
        {
            m_Buffered = null;
            m_Guarding = false;
            m_Timeline.Start(attack);
            if (attack == jab || attack == cross) m_ComboIndex++;
            else m_ComboIndex = 0;
            if (animator != null)
            {
                animator.SetGuard(false);
                animator.PlayAttack(attack);
            }
            AttackStarted?.Invoke(this, attack);
        }

        public bool TryDodge(Vector3 worldDirection)
        {
            if (Locked || IsKnockedOut || IsStaggered || IsDodging || m_DodgeCooldownTimer > 0f) return false;
            if (IsAttacking && m_Timeline.Phase != AttackPhase.Recovery) return false;
            m_Timeline.Cancel();
            m_DodgeTimer = dodgeDuration;
            worldDirection.y = 0f;
            if (worldDirection.sqrMagnitude < 0.01f) worldDirection = -transform.forward;
            worldDirection.Normalize();
            if (motor != null) motor.AddImpulse(worldDirection * dodgeSpeed);
            if (animator != null)
            {
                Vector3 local = transform.InverseTransformDirection(worldDirection);
                animator.PlayDodge(new Vector2(local.x, local.z));
            }
            return true;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (m_HitStopTimer > 0f)
            {
                m_HitStopTimer -= dt;
                if (m_HitStopTimer <= 0f && animator != null) animator.SetLocalTimeScale(1f);
            }
            float localDt = dt * LocalTimeScale;

            m_StaggerTimer = Mathf.Max(0f, m_StaggerTimer - localDt);
            if (m_DodgeTimer > 0f)
            {
                m_DodgeTimer = Mathf.Max(0f, m_DodgeTimer - localDt);
                if (m_DodgeTimer <= 0f) m_DodgeCooldownTimer = dodgeCooldown;
            }
            m_DodgeCooldownTimer = Mathf.Max(0f, m_DodgeCooldownTimer - dt);

            if (m_Timeline.IsRunning)
            {
                var attack = m_Timeline.Attack;
                // Small forward lunge during anticipation and contact.
                if (motor != null && m_Timeline.Phase != AttackPhase.Recovery && attack.lunge > 0f)
                {
                    float lungeTime = Mathf.Max(0.05f, attack.startup + attack.active);
                    motor.AddImpulse(transform.forward * (attack.lunge / lungeTime) * localDt * 6f);
                }
                // Never skip the contact window, even at a low frame rate.
                float t0 = m_Timeline.Time;
                if (t0 < attack.startup && t0 + localDt >= attack.startup)
                {
                    float toActive = attack.startup - t0 + 0.0001f;
                    m_Timeline.Advance(toActive);
                    SweepHits(attack);
                    if (m_Timeline.IsRunning) m_Timeline.Advance(Mathf.Max(0f, localDt - toActive));
                }
                else
                {
                    if (m_Timeline.IsActive) SweepHits(attack);
                    m_Timeline.Advance(localDt);
                }
                if (!m_Timeline.IsRunning)
                {
                    m_LastAttackEnd = Time.time;
                    if (animator != null) animator.SetGuard(IsGuarding);
                }
            }

            // Execute a buffered request as soon as possible.
            if (m_Buffered != null)
            {
                if (Time.time - m_BufferedTime > inputBuffer) m_Buffered = null;
                else if (!IsStaggered && !IsDodging && (!m_Timeline.IsRunning || m_Timeline.CanChain)) StartAttack(m_Buffered);
            }
        }

        static readonly List<Fighter> s_SweepBuffer = new List<Fighter>();

        void SweepHits(AttackDefinition attack)
        {
            // Copy: a KO handler may disable a fighter (which edits the registry) during the loop.
            s_SweepBuffer.Clear();
            s_SweepBuffer.AddRange(s_All);
            foreach (var target in s_SweepBuffer)
            {
                if (target == this || target.team == team || target.IsKnockedOut) continue;
                if (!HitGeometry.InStrikeZone(transform.position, transform.forward, target.transform.position, attack.reach, target.bodyRadius, attack.maxAngle))
                    continue;
                if (!HasLineOfSight(target)) continue;
                if (!m_Timeline.TryRegisterHit(target)) continue;

                HitResult result = target.ReceiveHit(this, attack);
                Vector3 impact = animator != null ? animator.GetFistPosition(attack.rightHand) : Vector3.Lerp(Chest, target.Chest, 0.8f);
                if (result == HitResult.Hit || result == HitResult.KnockOut || result == HitResult.Blocked)
                    ApplyHitStop(attack.hitStop * (result == HitResult.Blocked ? 0.6f : 1f));
                AttackLanded?.Invoke(this, attack, result, impact);
            }
        }

        /// <summary>No hit through walls, cars or other obstacles between the two chests.</summary>
        public bool HasLineOfSight(Fighter target)
        {
            Vector3 from = Chest;
            Vector3 to = target.Chest;
            var hits = Physics.RaycastAll(from, (to - from).normalized, Vector3.Distance(from, to), obstacleMask, QueryTriggerInteraction.Ignore);
            foreach (var h in hits)
            {
                if (h.collider.transform.IsChildOf(transform) || h.collider.transform.IsChildOf(target.transform)) continue;
                return false;
            }
            return true;
        }

        public HitResult ReceiveHit(Fighter attacker, AttackDefinition attack)
        {
            if (IsKnockedOut || Locked) return HitResult.Ignored;
            if (IsInvulnerable) return HitResult.Dodged;

            bool frontal = HitGeometry.IsFrontal(transform.position, transform.forward, attacker.transform.position);
            bool guarding = IsGuarding;
            float damage = HitGeometry.ResolveDamage(attack, guarding, frontal);
            Vector3 push = HitGeometry.Flat(transform.position - attacker.transform.position).normalized;

            HitResult result;
            if (guarding && frontal)
            {
                result = HitResult.Blocked;
                m_StaggerTimer = Mathf.Max(m_StaggerTimer, blockStun);
                if (motor != null) motor.AddImpulse(push * attack.knockback * 0.45f);
                if (animator != null) animator.PlayHit(attack.heavy, true);
            }
            else
            {
                result = HitResult.Hit;
                m_Timeline.Cancel();
                m_Buffered = null;
                m_StaggerTimer = Mathf.Max(m_StaggerTimer, attack.hitStun);
                if (motor != null) motor.AddImpulse(push * attack.knockback);
                if (animator != null) animator.PlayHit(attack.heavy, false);
            }

            Health.Damage(damage);
            ApplyHitStop(attack.hitStop * (result == HitResult.Blocked ? 0.6f : 1f));
            if (Health.IsDepleted)
            {
                result = HitResult.KnockOut;
                m_Guarding = false;
                m_Timeline.Cancel();
                if (animator != null) animator.SetKnockedOut(true);
                Damaged?.Invoke(this, attack, result, damage);
                KnockedOut?.Invoke(this);
                return result;
            }
            Damaged?.Invoke(this, attack, result, damage);
            return result;
        }

        void ApplyHitStop(float duration)
        {
            if (duration <= 0f) return;
            m_HitStopTimer = Mathf.Max(m_HitStopTimer, duration);
            if (animator != null) animator.SetLocalTimeScale(LocalTimeScale);
        }

        /// <summary>Restores health and clears every combat state (used for "Recommencer le combat").</summary>
        public void Revive()
        {
            Health.Reset(maxHealth);
            m_Timeline.Cancel();
            m_Buffered = null;
            m_StaggerTimer = 0f;
            m_DodgeTimer = 0f;
            m_HitStopTimer = 0f;
            m_Guarding = false;
            m_ComboIndex = 0;
            if (animator != null)
            {
                animator.SetLocalTimeScale(1f);
                animator.SetKnockedOut(false);
                animator.SetGuard(false);
            }
            Revived?.Invoke(this);
        }

        public Fighter FindNearestEnemy(float maxDistance)
        {
            Fighter best = null;
            float bestDist = maxDistance;
            foreach (var f in s_All)
            {
                if (f == this || f.team == team || f.IsKnockedOut || !f.isActiveAndEnabled) continue;
                float d = Vector3.Distance(f.transform.position, transform.position);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = f;
                }
            }
            return best;
        }
    }
}

using PoingMort.Characters;
using UnityEngine;

namespace PoingMort.Combat
{
    /// <summary>
    /// Simple street fighter: approaches, keeps its distance, circles, guards, announces its punches
    /// (a short visible wind-up before the real anticipation) and reacts to hits.
    /// </summary>
    [RequireComponent(typeof(CharacterMotor))]
    [RequireComponent(typeof(Fighter))]
    public sealed class OpponentBrain : MonoBehaviour
    {
        public enum Mode { Idle, Approach, Circle, WindUp, Attack, Retreat, Guarding, Stagger, Down }

        [Header("Distances (m)")]
        public float spacing = 2.2f;
        public float attackDistance = 1.15f;
        [Header("Vitesses (m/s)")]
        public float approachSpeed = 2.3f;
        public float circleSpeed = 1.3f;
        public float retreatSpeed = 1.9f;
        [Header("Comportement")]
        [Tooltip("Temps de réaction avant de lever la garde (s).")]
        public float reactionTime = 0.22f;
        [Range(0f, 1f)] public float guardChance = 0.5f;
        [Range(0f, 1f)] public float dodgeChance = 0.12f;
        [Range(0f, 1f)] public float heavyChance = 0.3f;
        public Vector2 circleDurationRange = new Vector2(1.1f, 2.4f);
        [Tooltip("Durée de l'annonce visible d'un coup, avant son anticipation (s).")]
        public float windUpDuration = 0.32f;
        public Vector2 attackCooldownRange = new Vector2(0.9f, 1.8f);

        [Header("Annonce")]
        public Light telegraphLight;

        public Fighter Target { get; set; }
        public Mode CurrentMode { get; private set; } = Mode.Idle;
        public bool IsTelegraphing => CurrentMode == Mode.WindUp;
        /// <summary>True while the fight is running (set by the arena).</summary>
        public bool Active { get; private set; }

        CharacterMotor m_Motor;
        Fighter m_Fighter;
        CharacterAnimatorBase m_Anim;
        float m_ModeTimer;
        float m_ModeDuration;
        float m_CircleDir = 1f;
        float m_Cooldown;
        float m_ReactTimer = -1f;
        bool m_PlannedHeavy;
        AttackPhase m_LastTargetPhase;

        void Awake()
        {
            m_Motor = GetComponent<CharacterMotor>();
            m_Fighter = GetComponent<Fighter>();
            m_Fighter.team = FighterTeam.Opponent;
            m_Anim = GetComponentInChildren<CharacterAnimatorBase>();
            if (m_Fighter.animator == null) m_Fighter.animator = m_Anim;
            m_Fighter.Damaged += OnDamaged;
            if (telegraphLight != null) telegraphLight.enabled = false;
        }

        void OnDestroy()
        {
            if (m_Fighter != null) m_Fighter.Damaged -= OnDamaged;
        }

        public void SetActive(bool active)
        {
            Active = active;
            m_Fighter.Locked = !active;
            SetMode(active ? Mode.Approach : Mode.Idle, 0f);
            m_Cooldown = 0.8f;
        }

        void SetMode(Mode mode, float duration)
        {
            CurrentMode = mode;
            m_ModeTimer = 0f;
            m_ModeDuration = duration;
            if (telegraphLight != null) telegraphLight.enabled = mode == Mode.WindUp;
        }

        void OnDamaged(Fighter self, AttackDefinition attack, HitResult result, float amount)
        {
            if (result == HitResult.KnockOut) { SetMode(Mode.Down, 0f); return; }
            if (result == HitResult.Hit)
            {
                SetMode(Mode.Stagger, attack.hitStun);
                // Being hit makes it more defensive for a moment.
                if (Random.value < guardChance) m_ReactTimer = reactionTime;
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            m_ModeTimer += dt;
            m_Cooldown = Mathf.Max(0f, m_Cooldown - dt);

            Vector3 desired = Vector3.zero;
            if (!Active || Target == null)
            {
                m_Fighter.SetGuard(false);
                m_Motor.Move(Vector3.zero, dt);
                Animate(false);
                return;
            }

            if (m_Fighter.IsKnockedOut) CurrentMode = Mode.Down;
            Vector3 toTarget = Target.transform.position - transform.position;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;
            Vector3 dir = distance > 0.01f ? toTarget / distance : transform.forward;
            Vector3 side = Vector3.Cross(Vector3.up, dir) * m_CircleDir;

            ReactToTarget(distance);

            switch (CurrentMode)
            {
                case Mode.Down:
                    m_Fighter.SetGuard(false);
                    m_Motor.Move(Vector3.zero, dt);
                    Animate(true);
                    return;

                case Mode.Stagger:
                    if (m_ModeTimer >= m_ModeDuration) SetMode(Mode.Retreat, 0.5f);
                    break;

                case Mode.Approach:
                    desired = dir * approachSpeed;
                    if (distance <= spacing) SetMode(Mode.Circle, Random.Range(circleDurationRange.x, circleDurationRange.y));
                    break;

                case Mode.Circle:
                    desired = side * circleSpeed + dir * (distance - spacing) * 1.5f;
                    if (m_ModeTimer >= m_ModeDuration)
                    {
                        if (Random.value < 0.35f) m_CircleDir = -m_CircleDir;
                        if (m_Cooldown <= 0f && !Target.IsKnockedOut) SetMode(Mode.WindUp, windUpDuration + Random.Range(0f, 0.1f));
                        else SetMode(Mode.Circle, Random.Range(circleDurationRange.x, circleDurationRange.y));
                    }
                    break;

                case Mode.WindUp:
                    // Announced attack: steps in, fist raised (light cue), then commits.
                    desired = dir * (distance > attackDistance ? approachSpeed * 1.2f : 0f);
                    if (m_ModeTimer >= m_ModeDuration && distance <= attackDistance + 0.35f)
                    {
                        m_PlannedHeavy = Random.value < heavyChance;
                        bool started = m_PlannedHeavy ? m_Fighter.TryHeavyAttack() : m_Fighter.TryLightAttack();
                        SetMode(started ? Mode.Attack : Mode.Circle, started ? 0f : 0.6f);
                    }
                    else if (m_ModeTimer > m_ModeDuration + 1.2f) SetMode(Mode.Circle, 0.8f);
                    break;

                case Mode.Attack:
                    if (!m_Fighter.IsAttacking)
                    {
                        // Sometimes chains a second light punch.
                        if (!m_PlannedHeavy && Random.value < 0.4f && distance <= attackDistance + 0.2f && m_Fighter.TryLightAttack())
                        {
                            m_PlannedHeavy = true; // only one follow-up
                            break;
                        }
                        m_Cooldown = Random.Range(attackCooldownRange.x, attackCooldownRange.y);
                        SetMode(Mode.Retreat, 0.55f);
                    }
                    break;

                case Mode.Retreat:
                    desired = -dir * retreatSpeed;
                    if (m_ModeTimer >= m_ModeDuration || distance >= spacing) SetMode(Mode.Circle, Random.Range(circleDurationRange.x, circleDurationRange.y));
                    break;

                case Mode.Guarding:
                    desired = -dir * 0.4f;
                    if (m_ModeTimer >= m_ModeDuration) SetMode(Mode.Circle, Random.Range(0.6f, 1.2f));
                    break;

                case Mode.Idle:
                    SetMode(Mode.Approach, 0f);
                    break;
            }

            m_Fighter.SetGuard(CurrentMode == Mode.Guarding);
            if (m_Fighter.IsStaggered || m_Fighter.IsAttacking) desired *= 0.15f;
            m_Motor.Move(desired, dt);
            m_Motor.Face(dir, dt, 0.75f);
            Animate(true);
        }

        /// <summary>Raises the guard (or dodges) when the player starts a punch nearby, after a reaction delay.</summary>
        void ReactToTarget(float distance)
        {
            AttackPhase phase = Target.CurrentPhase;
            bool justStarted = phase == AttackPhase.Startup && m_LastTargetPhase != AttackPhase.Startup;
            m_LastTargetPhase = phase;
            if (justStarted && distance < 2.6f && (CurrentMode == Mode.Circle || CurrentMode == Mode.Approach || CurrentMode == Mode.Retreat))
            {
                float roll = Random.value;
                if (roll < dodgeChance)
                {
                    Vector3 away = transform.position - Target.transform.position;
                    if (m_Fighter.TryDodge(Vector3.Cross(Vector3.up, away.normalized) * (Random.value < 0.5f ? 1f : -1f) - away.normalized * 0.4f))
                        SetMode(Mode.Retreat, 0.45f);
                }
                else if (roll < dodgeChance + guardChance) m_ReactTimer = reactionTime;
            }
            if (m_ReactTimer >= 0f)
            {
                m_ReactTimer -= Time.deltaTime;
                if (m_ReactTimer < 0f && !m_Fighter.IsBusy) SetMode(Mode.Guarding, Random.Range(0.5f, 0.9f));
            }
        }

        void Animate(bool combat)
        {
            if (m_Anim == null) return;
            Vector3 local = transform.InverseTransformDirection(m_Motor.PlanarVelocity);
            Vector2 move = Vector2.ClampMagnitude(new Vector2(local.x, local.z) / Mathf.Max(0.1f, approachSpeed), 1f);
            m_Anim.SetLocomotion(new Vector2(local.x, local.z).magnitude, move, m_Motor.IsGrounded);
            m_Anim.SetCombatStance(combat && CurrentMode != Mode.Down);
        }

        /// <summary>Puts the opponent back at its start point, full health, waiting.</summary>
        public void ResetTo(Vector3 position, Quaternion rotation)
        {
            m_Motor.Teleport(position, rotation);
            m_Fighter.Revive();
            if (m_Anim != null) m_Anim.PlayGetUp();
            SetActive(false);
        }
    }
}

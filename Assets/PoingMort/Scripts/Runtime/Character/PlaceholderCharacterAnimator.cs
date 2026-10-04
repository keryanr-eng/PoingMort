using PoingMort.Combat;
using UnityEngine;

namespace PoingMort.Characters
{
    /// <summary>
    /// Minimal motion for the technical placeholder used only when the real character asset is missing.
    /// It is NOT the art direction: it exists so that gameplay can still be tested.
    /// </summary>
    public sealed class PlaceholderCharacterAnimator : CharacterAnimatorBase
    {
        public Transform body;
        public Transform leftFist;
        public Transform rightFist;

        float m_Speed;
        float m_Phase;
        float m_AttackTimer;
        bool m_AttackRight;
        float m_AttackDuration = 0.4f;
        float m_HitTimer;
        bool m_Guard;
        bool m_Down;
        bool m_Seated;
        float m_TimeScale = 1f;
        Vector3 m_LeftRest, m_RightRest, m_BodyRest;

        void Awake()
        {
            if (body != null) m_BodyRest = body.localPosition;
            if (leftFist != null) m_LeftRest = leftFist.localPosition;
            if (rightFist != null) m_RightRest = rightFist.localPosition;
        }

        public override void SetLocomotion(float speed, Vector2 localDirection, bool grounded) => m_Speed = speed;
        public override void SetCombatStance(bool combat) { }
        public override void SetGuard(bool guard) => m_Guard = guard;

        public override void PlayAttack(AttackDefinition attack)
        {
            if (attack == null) return;
            m_AttackRight = attack.rightHand;
            m_AttackDuration = Mathf.Max(0.1f, attack.TotalDuration);
            m_AttackTimer = m_AttackDuration;
        }

        public override void PlayDodge(Vector2 localDirection) => m_HitTimer = 0.25f;
        public override void PlayHit(bool heavy, bool blocked) => m_HitTimer = heavy ? 0.45f : 0.25f;
        public override void SetKnockedOut(bool knockedOut) => m_Down = knockedOut;
        public override void PlayGetUp() => m_Down = false;
        public override void SetSeated(bool seated) => m_Seated = seated;
        public override void PlayVehicleTransition(bool entering) { }
        public override void SetFallen(bool fallen) => m_Down = fallen;
        public override void SetLocalTimeScale(float scale) => m_TimeScale = scale;

        void Update()
        {
            float dt = Time.deltaTime * m_TimeScale;
            m_Phase += dt * Mathf.Lerp(2f, 9f, Mathf.Clamp01(m_Speed / 6f));
            m_AttackTimer = Mathf.Max(0f, m_AttackTimer - dt);
            m_HitTimer = Mathf.Max(0f, m_HitTimer - dt);

            if (body != null)
            {
                float bob = Mathf.Abs(Mathf.Sin(m_Phase)) * 0.05f * Mathf.Clamp01(m_Speed / 2f);
                Vector3 pos = m_BodyRest + Vector3.up * bob;
                if (m_Seated) pos += Vector3.down * 0.45f;
                body.localPosition = pos;
                Quaternion tilt = Quaternion.Euler(m_Down ? -80f : (m_HitTimer > 0f ? -12f : m_Speed * 1.5f), 0f, 0f);
                body.localRotation = Quaternion.Slerp(body.localRotation, tilt, 1f - Mathf.Exp(-14f * dt));
            }
            UpdateFist(leftFist, m_LeftRest, !m_AttackRight);
            UpdateFist(rightFist, m_RightRest, m_AttackRight);
        }

        void UpdateFist(Transform fist, Vector3 rest, bool attacking)
        {
            if (fist == null) return;
            Vector3 target = rest + (m_Guard ? new Vector3(0f, 0.25f, 0.05f) : Vector3.zero);
            if (attacking && m_AttackTimer > 0f)
            {
                float t = 1f - m_AttackTimer / m_AttackDuration;
                target += Vector3.forward * (Mathf.Sin(Mathf.Clamp01(t * 1.6f) * Mathf.PI) * 0.5f) + Vector3.up * 0.2f;
            }
            fist.localPosition = Vector3.Lerp(fist.localPosition, target, 1f - Mathf.Exp(-25f * Time.deltaTime));
        }
    }
}

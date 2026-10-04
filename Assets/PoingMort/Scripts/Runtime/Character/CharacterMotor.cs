using UnityEngine;

namespace PoingMort.Characters
{
    /// <summary>
    /// CharacterController-based movement shared by the player and the opponent.
    /// When another system drives the character (boarding, seated in a car), call <see cref="SetDriven"/>
    /// so that only one system moves the transform at a time.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class CharacterMotor : MonoBehaviour
    {
        public float acceleration = 18f;
        public float deceleration = 22f;
        public float turnSpeed = 720f;
        public float gravity = -22f;
        [Tooltip("Freinage des impulsions externes (recul, esquive, élan de sortie de voiture).")]
        public float impulseDamping = 6.5f;

        CharacterController m_Controller;
        Vector3 m_PlanarVelocity;
        Vector3 m_Impulse;
        float m_VerticalSpeed;
        Vector3 m_LastPosition;
        bool m_Driven;

        public CharacterController Controller => m_Controller;
        public Vector3 Velocity { get; private set; }
        public Vector3 PlanarVelocity => m_PlanarVelocity + new Vector3(m_Impulse.x, 0f, m_Impulse.z);
        public bool IsGrounded { get; private set; } = true;
        public bool IsDriven => m_Driven;
        public float ImpulseMagnitude => new Vector3(m_Impulse.x, 0f, m_Impulse.z).magnitude;

        void Awake()
        {
            m_Controller = GetComponent<CharacterController>();
            m_LastPosition = transform.position;
        }

        /// <summary>Moves toward the desired planar velocity (m/s), applying gravity and pending impulses.</summary>
        public void Move(Vector3 desiredPlanarVelocity, float dt)
        {
            if (m_Driven || dt <= 0f || !m_Controller.enabled) return;
            desiredPlanarVelocity.y = 0f;
            float rate = desiredPlanarVelocity.sqrMagnitude > m_PlanarVelocity.sqrMagnitude ? acceleration : deceleration;
            m_PlanarVelocity = Vector3.MoveTowards(m_PlanarVelocity, desiredPlanarVelocity, rate * dt);

            if (IsGrounded && m_VerticalSpeed < 0f) m_VerticalSpeed = -2f;
            m_VerticalSpeed += gravity * dt;

            m_Impulse = Vector3.Lerp(m_Impulse, Vector3.zero, 1f - Mathf.Exp(-impulseDamping * dt));
            if (m_Impulse.sqrMagnitude < 0.0004f) m_Impulse = Vector3.zero;

            Vector3 motion = (m_PlanarVelocity + m_Impulse + Vector3.up * m_VerticalSpeed) * dt;
            CollisionFlags flags = m_Controller.Move(motion);
            IsGrounded = (flags & CollisionFlags.Below) != 0 || m_Controller.isGrounded;

            Vector3 pos = transform.position;
            Velocity = (pos - m_LastPosition) / dt;
            m_LastPosition = pos;
        }

        public void Face(Vector3 direction, float dt, float speedMultiplier = 1f)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-4f) return;
            Quaternion target = Quaternion.LookRotation(direction.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * speedMultiplier * dt);
        }

        public void FaceImmediate(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-4f) return;
            transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        /// <summary>Adds an external horizontal push (knockback, dodge, inherited vehicle speed).</summary>
        public void AddImpulse(Vector3 velocity)
        {
            velocity.y = 0f;
            m_Impulse += velocity;
        }

        public void ClearMotion()
        {
            m_PlanarVelocity = Vector3.zero;
            m_Impulse = Vector3.zero;
            m_VerticalSpeed = 0f;
            Velocity = Vector3.zero;
        }

        /// <summary>
        /// Gives control of the transform to another system (true) or takes it back (false).
        /// The CharacterController is disabled while driven so it never fights the other system.
        /// </summary>
        public void SetDriven(bool driven)
        {
            m_Driven = driven;
            m_Controller.enabled = !driven;
            if (!driven)
            {
                m_LastPosition = transform.position;
                m_VerticalSpeed = 0f;
            }
        }

        /// <summary>Places the character without the controller interfering.</summary>
        public void Teleport(Vector3 position, Quaternion rotation)
        {
            bool wasEnabled = m_Controller.enabled;
            m_Controller.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            m_Controller.enabled = wasEnabled && !m_Driven;
            m_LastPosition = position;
            ClearMotion();
        }

        /// <summary>Is a capsule of the controller's size free at this position (ignoring the character itself)?</summary>
        public bool IsSpaceFree(Vector3 feetPosition, LayerMask mask, float padding = 0.02f)
        {
            float radius = m_Controller.radius + padding;
            float height = Mathf.Max(m_Controller.height, radius * 2f);
            Vector3 bottom = feetPosition + Vector3.up * (radius + 0.05f);
            Vector3 top = feetPosition + Vector3.up * (height - radius);
            var hits = Physics.OverlapCapsule(bottom, top, radius, mask, QueryTriggerInteraction.Ignore);
            foreach (var h in hits)
            {
                if (h.transform.IsChildOf(transform)) continue;
                return false;
            }
            return true;
        }
    }
}

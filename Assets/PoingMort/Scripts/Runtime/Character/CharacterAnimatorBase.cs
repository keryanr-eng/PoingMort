using PoingMort.Combat;
using UnityEngine;

namespace PoingMort.Characters
{
    /// <summary>
    /// What gameplay asks from a character's animation, independent of how it is animated
    /// (Mecanim humanoid clips for the real character, minimal motion for the technical placeholder).
    /// </summary>
    public abstract class CharacterAnimatorBase : MonoBehaviour
    {
        /// <summary>Planar speed in m/s and direction in the character's local space (x = right, y = forward).</summary>
        public abstract void SetLocomotion(float speed, Vector2 localDirection, bool grounded);
        public abstract void SetCombatStance(bool combat);
        public abstract void SetGuard(bool guard);
        public abstract void PlayAttack(AttackDefinition attack);
        public abstract void PlayDodge(Vector2 localDirection);
        public abstract void PlayHit(bool heavy, bool blocked);
        public abstract void SetKnockedOut(bool knockedOut);
        public abstract void PlayGetUp();
        public abstract void SetSeated(bool seated);
        public abstract void PlayVehicleTransition(bool entering);
        public abstract void SetFallen(bool fallen);

        /// <summary>Local hit-stop: slows only this character's animation (never the world).</summary>
        public abstract void SetLocalTimeScale(float scale);

        /// <summary>World position of a fist, used for impact effects. Falls back to an estimate.</summary>
        public virtual Vector3 GetFistPosition(bool rightHand)
        {
            Transform t = transform;
            return t.position + t.up * 1.45f + t.forward * 0.55f + t.right * (rightHand ? 0.18f : -0.18f);
        }

        /// <summary>World position of the head, used for impact effects and look-at.</summary>
        public virtual Vector3 GetHeadPosition()
        {
            return transform.position + Vector3.up * 1.62f;
        }
    }
}

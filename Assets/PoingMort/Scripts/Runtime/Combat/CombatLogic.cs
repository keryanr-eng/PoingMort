using System.Collections.Generic;
using UnityEngine;

namespace PoingMort.Combat
{
    public enum HitResult { Ignored, Hit, Blocked, Dodged, KnockOut }

    /// <summary>Runs the timeline of one attack and guarantees a single hit per target.</summary>
    public sealed class AttackTimeline
    {
        readonly HashSet<object> m_HitTargets = new HashSet<object>();

        public AttackDefinition Attack { get; private set; }
        public float Time { get; private set; }

        public bool IsRunning => Attack != null && Time < Attack.TotalDuration;
        public AttackPhase Phase => Attack == null ? AttackPhase.None : Attack.PhaseAt(Time);
        public bool IsActive => Phase == AttackPhase.Active;

        /// <summary>True once the attack reached the part of its recovery where a follow-up may start.</summary>
        public bool CanChain
        {
            get
            {
                if (Attack == null) return true;
                if (!IsRunning) return true;
                if (Phase != AttackPhase.Recovery) return false;
                float intoRecovery = Time - Attack.startup - Attack.active;
                return intoRecovery >= Attack.chainFrom * Attack.recovery;
            }
        }

        public void Start(AttackDefinition attack)
        {
            Attack = attack;
            Time = 0f;
            m_HitTargets.Clear();
        }

        public void Cancel()
        {
            Attack = null;
            Time = 0f;
            m_HitTargets.Clear();
        }

        public void Advance(float dt)
        {
            if (Attack == null) return;
            Time += Mathf.Max(0f, dt);
            if (Time >= Attack.TotalDuration)
            {
                Attack = null;
                m_HitTargets.Clear();
            }
        }

        /// <summary>Returns true the first time a target is registered for the running attack.</summary>
        public bool TryRegisterHit(object target)
        {
            if (Attack == null || !IsActive || target == null) return false;
            return m_HitTargets.Add(target);
        }

        public int HitCount => m_HitTargets.Count;
    }

    /// <summary>Geometry helpers for melee hits, on the horizontal plane.</summary>
    public static class HitGeometry
    {
        public static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }

        /// <summary>
        /// Is the target inside the strike cone of the attacker? <paramref name="reach"/> is measured between centres,
        /// <paramref name="targetRadius"/> lets wider bodies be hit slightly further away.
        /// </summary>
        public static bool InStrikeZone(Vector3 attackerPos, Vector3 attackerForward, Vector3 targetPos, float reach, float targetRadius, float maxAngleDeg)
        {
            Vector3 to = Flat(targetPos - attackerPos);
            float dist = to.magnitude;
            if (dist > reach + targetRadius) return false;
            if (dist < 0.05f) return true;
            Vector3 fwd = Flat(attackerForward);
            if (fwd.sqrMagnitude < 1e-6f) return false;
            return Vector3.Angle(fwd, to) <= maxAngleDeg;
        }

        /// <summary>Does the attack come from the front of the defender (guard can block it)?</summary>
        public static bool IsFrontal(Vector3 defenderPos, Vector3 defenderForward, Vector3 attackerPos, float maxAngleDeg = 75f)
        {
            Vector3 to = Flat(attackerPos - defenderPos);
            Vector3 fwd = Flat(defenderForward);
            if (to.sqrMagnitude < 1e-6f || fwd.sqrMagnitude < 1e-6f) return true;
            return Vector3.Angle(fwd, to) <= maxAngleDeg;
        }

        public static float ResolveDamage(AttackDefinition attack, bool guarding, bool frontal)
        {
            if (attack == null) return 0f;
            return guarding && frontal ? attack.damage * attack.guardDamageFactor : attack.damage;
        }
    }

    /// <summary>Health with clamping and KO detection.</summary>
    public sealed class HealthPool
    {
        public float Max { get; private set; }
        public float Current { get; private set; }
        public bool IsDepleted => Current <= 0f;
        public float Normalized => Max > 0f ? Current / Max : 0f;

        public HealthPool(float max)
        {
            Max = Mathf.Max(1f, max);
            Current = Max;
        }

        /// <summary>Applies damage, returns the amount actually removed.</summary>
        public float Damage(float amount)
        {
            if (amount <= 0f || IsDepleted) return 0f;
            float before = Current;
            Current = Mathf.Max(0f, Current - amount);
            return before - Current;
        }

        public void Heal(float amount)
        {
            if (amount <= 0f) return;
            Current = Mathf.Min(Max, Current + amount);
        }

        public void Reset(float max)
        {
            Max = Mathf.Max(1f, max);
            Current = Max;
        }

        public void Reset() => Current = Max;
    }
}

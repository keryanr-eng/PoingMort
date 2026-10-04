using System;
using UnityEngine;

namespace PoingMort.Combat
{
    public enum AttackPhase { None, Startup, Active, Recovery }

    /// <summary>
    /// Data of one punch. Timings are in seconds, distances in metres.
    /// Plain serializable class (not a ScriptableObject) so it can be tested outside Unity.
    /// </summary>
    [Serializable]
    public class AttackDefinition
    {
        public string id = "jab";
        [Tooltip("Nom affiché (français).")]
        public string displayName = "Direct";
        [Tooltip("Nom de l'état dans l'Animator (personnage Mecanim) ou de la pose procédurale.")]
        public string animState = "Jab";
        public bool rightHand;
        public bool heavy;

        [Header("Dégâts")]
        public float damage = 8f;
        [Tooltip("Part des dégâts qui passe à travers une garde de face.")]
        [Range(0f, 1f)] public float guardDamageFactor = 0.15f;

        [Header("Timing (anticipation, contact, récupération)")]
        public float startup = 0.14f;
        public float active = 0.09f;
        public float recovery = 0.26f;
        [Tooltip("À partir de quelle fraction de la récupération le coup suivant peut s'enchaîner.")]
        [Range(0f, 1f)] public float chainFrom = 0.25f;

        [Header("Portée")]
        [Tooltip("Distance horizontale maximale entre les centres des deux combattants, rayon de la cible compris.")]
        public float reach = 1.05f;
        [Tooltip("Demi-angle du cône de frappe devant l'attaquant (degrés).")]
        public float maxAngle = 50f;

        [Header("Effets")]
        public float hitStun = 0.32f;
        public float knockback = 1.4f;
        [Tooltip("Arrêt d'impact local aux deux combattants (jamais global).")]
        public float hitStop = 0.05f;
        public float cameraShake = 0.2f;
        [Tooltip("Avancée de l'attaquant pendant le coup (m).")]
        public float lunge = 0.25f;

        public float TotalDuration => startup + active + recovery;

        public AttackPhase PhaseAt(float t)
        {
            if (t < 0f) return AttackPhase.None;
            if (t < startup) return AttackPhase.Startup;
            if (t < startup + active) return AttackPhase.Active;
            if (t < TotalDuration) return AttackPhase.Recovery;
            return AttackPhase.None;
        }

        public AttackDefinition Clone()
        {
            return (AttackDefinition)MemberwiseClone();
        }

        // ----- Presets used by the prototype (tunable in the Inspector afterwards) -----

        public static AttackDefinition Jab() => new AttackDefinition
        {
            id = "jab", displayName = "Direct du gauche", animState = "Jab", rightHand = false,
            damage = 7f, startup = 0.12f, active = 0.08f, recovery = 0.22f, reach = 1.05f, maxAngle = 50f,
            hitStun = 0.28f, knockback = 1.1f, hitStop = 0.045f, cameraShake = 0.15f, lunge = 0.2f, chainFrom = 0.15f,
        };

        public static AttackDefinition Cross() => new AttackDefinition
        {
            id = "cross", displayName = "Direct du droit", animState = "Cross", rightHand = true,
            damage = 11f, startup = 0.16f, active = 0.09f, recovery = 0.3f, reach = 1.12f, maxAngle = 45f,
            hitStun = 0.36f, knockback = 1.7f, hitStop = 0.06f, cameraShake = 0.25f, lunge = 0.3f, chainFrom = 0.25f,
        };

        public static AttackDefinition Hook() => new AttackDefinition
        {
            id = "hook", displayName = "Crochet", animState = "Hook", rightHand = false, heavy = true,
            damage = 18f, startup = 0.3f, active = 0.1f, recovery = 0.42f, reach = 0.98f, maxAngle = 65f,
            hitStun = 0.55f, knockback = 2.6f, hitStop = 0.085f, cameraShake = 0.45f, lunge = 0.35f, chainFrom = 0.4f,
            guardDamageFactor = 0.3f,
        };
    }
}

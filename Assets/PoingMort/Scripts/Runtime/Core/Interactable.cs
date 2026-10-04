using System.Collections.Generic;
using UnityEngine;

namespace PoingMort.Core
{
    /// <summary>
    /// Something the player can use with the interaction key (challenge a fighter, later shops...).
    /// Vehicles are handled separately by the player controller because they move.
    /// </summary>
    public abstract class Interactable : MonoBehaviour
    {
        static readonly List<Interactable> s_All = new List<Interactable>();
        public static IReadOnlyList<Interactable> All => s_All;

        [Tooltip("Distance maximale d'utilisation (m).")]
        public float range = 2.5f;
        [Tooltip("Priorité quand plusieurs interactions sont possibles.")]
        public int priority;

        protected virtual void OnEnable() => s_All.Add(this);
        protected virtual void OnDisable() => s_All.Remove(this);

        /// <summary>Returns the action label (e.g. "Défier") or null when unavailable.</summary>
        public abstract string GetPrompt(GameObject user);

        public abstract void Interact(GameObject user);

        public virtual Vector3 InteractionPoint => transform.position;

        public static Interactable FindBest(Vector3 position, GameObject user)
        {
            Interactable best = null;
            float bestScore = float.MaxValue;
            foreach (var i in s_All)
            {
                if (i == null || !i.isActiveAndEnabled) continue;
                float d = Vector3.Distance(position, i.InteractionPoint);
                if (d > i.range) continue;
                if (string.IsNullOrEmpty(i.GetPrompt(user))) continue;
                float score = d - i.priority * 10f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = i;
                }
            }
            return best;
        }
    }
}

using PoingMort.Combat;
using UnityEngine;

namespace PoingMort.Characters
{
    /// <summary>Names shared by the runtime and the editor tool that generates the Animator Controller.</summary>
    public static class CharacterAnimStates
    {
        public const int BaseLayer = 0;
        public const int UpperLayer = 1;

        // Parameters
        public const string Speed = "Speed";
        public const string MoveX = "MoveX";
        public const string MoveY = "MoveY";
        public const string Combat = "Combat";
        public const string Guard = "Guard";
        public const string Grounded = "Grounded";
        public const string Seated = "Seated";
        public const string KnockedOut = "KO";
        public const string Fallen = "Fallen";

        // Base layer states
        public const string Locomotion = "Locomotion";
        public const string FightLocomotion = "FightLocomotion";
        public const string Dodge = "Dodge";
        public const string HitLight = "HitLight";
        public const string HitHeavy = "HitHeavy";
        public const string KO = "KO";
        public const string GetUp = "GetUp";
        public const string CarEnter = "CarEnter";
        public const string SeatedState = "Seated";
        public const string CarExit = "CarExit";
        public const string Fall = "Fall";

        // Upper body layer states
        public const string UpperEmpty = "Empty";
        public const string UpperGuard = "GuardUp";
        public const string BlockHit = "BlockHit";
        public const string Jab = "Jab";
        public const string Cross = "Cross";
        public const string Hook = "Hook";

        /// <summary>Clip names expected in the character FBX (Blender actions), see ASSETS.md.</summary>
        public static readonly string[] RequiredClips =
        {
            "Idle", "Walk", "Run", "Sprint",
            "FightIdle", "FightStepFwd", "FightStepBack", "FightStepLeft", "FightStepRight",
            "Jab", "Cross", "Hook", "Dodge", "BlockHit", "HitLight", "HitHeavy",
            "KO", "GetUp", "CarEnter", "Seated", "CarExit", "Fall",
        };
    }

    /// <summary>Drives a humanoid Animator built by PoingMort &gt; Personnage tools.</summary>
    [RequireComponent(typeof(Animator))]
    public sealed class MecanimCharacterAnimator : CharacterAnimatorBase
    {
        [Tooltip("Lissage des paramètres de locomotion (s).")]
        public float locomotionDamping = 0.08f;

        Animator m_Animator;
        static readonly int s_Speed = Animator.StringToHash(CharacterAnimStates.Speed);
        static readonly int s_MoveX = Animator.StringToHash(CharacterAnimStates.MoveX);
        static readonly int s_MoveY = Animator.StringToHash(CharacterAnimStates.MoveY);
        static readonly int s_Combat = Animator.StringToHash(CharacterAnimStates.Combat);
        static readonly int s_Guard = Animator.StringToHash(CharacterAnimStates.Guard);
        static readonly int s_Grounded = Animator.StringToHash(CharacterAnimStates.Grounded);
        static readonly int s_Seated = Animator.StringToHash(CharacterAnimStates.Seated);
        static readonly int s_KO = Animator.StringToHash(CharacterAnimStates.KnockedOut);
        static readonly int s_Fallen = Animator.StringToHash(CharacterAnimStates.Fallen);

        static readonly int s_UpperEmpty = Animator.StringToHash(CharacterAnimStates.UpperEmpty);

        [Tooltip("Durées de fondu du calque haut du corps (s) : entrée d'un coup ou de la garde, retour au corps entier.")]
        public float upperFadeIn = 0.06f;
        public float upperFadeOut = 0.12f;

        float m_TimeScale = 1f;
        bool m_HasUpperLayer;
        float m_UpperWeight;

        public Animator Animator => m_Animator;

        void Awake()
        {
            m_Animator = GetComponent<Animator>();
            m_Animator.applyRootMotion = false;
            m_Animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            m_HasUpperLayer = m_Animator.layerCount > CharacterAnimStates.UpperLayer;
        }

        bool Ready => m_Animator != null && m_Animator.runtimeAnimatorController != null && m_Animator.isActiveAndEnabled;

        /// <summary>
        /// The upper-body layer only weighs in while a punch, a blocked hit or the guard plays on it:
        /// its idle "Empty" state never touches the arms of the full-body animation.
        /// </summary>
        void Update()
        {
            if (!m_HasUpperLayer || !Ready) return;
            int layer = CharacterAnimStates.UpperLayer;
            bool active = m_Animator.IsInTransition(layer)
                ? m_Animator.GetNextAnimatorStateInfo(layer).shortNameHash != s_UpperEmpty
                : m_Animator.GetCurrentAnimatorStateInfo(layer).shortNameHash != s_UpperEmpty;
            float duration = active ? upperFadeIn : upperFadeOut;
            m_UpperWeight = Mathf.MoveTowards(m_UpperWeight, active ? 1f : 0f, Time.deltaTime / Mathf.Max(0.01f, duration));
            m_Animator.SetLayerWeight(layer, m_UpperWeight);
        }

        public override void SetLocomotion(float speed, Vector2 localDirection, bool grounded)
        {
            if (!Ready) return;
            float dt = Time.deltaTime;
            m_Animator.SetFloat(s_Speed, speed, locomotionDamping, dt);
            m_Animator.SetFloat(s_MoveX, localDirection.x, locomotionDamping, dt);
            m_Animator.SetFloat(s_MoveY, localDirection.y, locomotionDamping, dt);
            m_Animator.SetBool(s_Grounded, grounded);
        }

        public override void SetCombatStance(bool combat)
        {
            if (!Ready) return;
            m_Animator.SetBool(s_Combat, combat);
        }

        public override void SetGuard(bool guard)
        {
            if (!Ready) return;
            m_Animator.SetBool(s_Guard, guard);
        }

        void CrossFade(string state, int layer, float duration)
        {
            if (!Ready) return;
            if (layer >= m_Animator.layerCount) layer = CharacterAnimStates.BaseLayer;
            int hash = Animator.StringToHash(state);
            if (!m_Animator.HasState(layer, hash))
            {
                Debug.LogWarning($"[PoingMort] État d'animation manquant : {state} (couche {layer}).", this);
                return;
            }
            m_Animator.CrossFadeInFixedTime(hash, duration, layer, 0f);
        }

        public override void PlayAttack(AttackDefinition attack)
        {
            if (attack == null) return;
            CrossFade(attack.animState, m_HasUpperLayer ? CharacterAnimStates.UpperLayer : CharacterAnimStates.BaseLayer, 0.05f);
        }

        public override void PlayDodge(Vector2 localDirection) => CrossFade(CharacterAnimStates.Dodge, CharacterAnimStates.BaseLayer, 0.06f);

        public override void PlayHit(bool heavy, bool blocked)
        {
            if (blocked)
                CrossFade(CharacterAnimStates.BlockHit, m_HasUpperLayer ? CharacterAnimStates.UpperLayer : CharacterAnimStates.BaseLayer, 0.04f);
            else
                CrossFade(heavy ? CharacterAnimStates.HitHeavy : CharacterAnimStates.HitLight, CharacterAnimStates.BaseLayer, 0.04f);
        }

        public override void SetKnockedOut(bool knockedOut)
        {
            if (!Ready) return;
            m_Animator.SetBool(s_KO, knockedOut);
            if (knockedOut) CrossFade(CharacterAnimStates.KO, CharacterAnimStates.BaseLayer, 0.08f);
        }

        public override void PlayGetUp()
        {
            if (!Ready) return;
            m_Animator.SetBool(s_KO, false);
            m_Animator.SetBool(s_Fallen, false);
            CrossFade(CharacterAnimStates.GetUp, CharacterAnimStates.BaseLayer, 0.1f);
        }

        public override void SetSeated(bool seated)
        {
            if (!Ready) return;
            m_Animator.SetBool(s_Seated, seated);
            if (seated) CrossFade(CharacterAnimStates.SeatedState, CharacterAnimStates.BaseLayer, 0.15f);
        }

        public override void PlayVehicleTransition(bool entering)
        {
            CrossFade(entering ? CharacterAnimStates.CarEnter : CharacterAnimStates.CarExit, CharacterAnimStates.BaseLayer, 0.08f);
        }

        public override void SetFallen(bool fallen)
        {
            if (!Ready) return;
            m_Animator.SetBool(s_Fallen, fallen);
            if (fallen) CrossFade(CharacterAnimStates.Fall, CharacterAnimStates.BaseLayer, 0.06f);
        }

        public override void SetLocalTimeScale(float scale)
        {
            m_TimeScale = Mathf.Clamp(scale, 0f, 2f);
            if (m_Animator != null) m_Animator.speed = m_TimeScale;
        }

        public override Vector3 GetFistPosition(bool rightHand)
        {
            if (m_Animator != null && m_Animator.isHuman)
            {
                var bone = m_Animator.GetBoneTransform(rightHand ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
                if (bone != null) return bone.position;
            }
            return base.GetFistPosition(rightHand);
        }

        public override Vector3 GetHeadPosition()
        {
            if (m_Animator != null && m_Animator.isHuman)
            {
                var bone = m_Animator.GetBoneTransform(HumanBodyBones.Head);
                if (bone != null) return bone.position;
            }
            return base.GetHeadPosition();
        }
    }
}

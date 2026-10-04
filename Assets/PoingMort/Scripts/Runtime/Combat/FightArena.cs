using System;
using PoingMort.Controls;
using PoingMort.Core;
using PoingMort.Player;
using UnityEngine;

namespace PoingMort.Combat
{
    /// <summary>
    /// The courtyard where the underground fight takes place: challenge, fight, victory or defeat,
    /// and restart. Nothing here touches the traffic, which keeps running during the fight.
    /// </summary>
    public sealed class FightArena : Interactable
    {
        public enum ArenaState { Waiting, Starting, Fighting, Victory, Defeat }

        public static FightArena Current { get; private set; }

        public OpponentBrain opponent;
        public Transform opponentStart;
        public Transform playerStart;
        [Tooltip("Rayon de la zone de combat (m). Si le joueur s'éloigne, le combat est abandonné.")]
        public float arenaRadius = 11f;
        public float startDelay = 1.2f;
        [Tooltip("Délai avant de pouvoir recommencer après la fin d'un combat (s).")]
        public float restartDelay = 1.6f;

        public ArenaState State { get; private set; } = ArenaState.Waiting;
        public Fighter OpponentFighter => opponent != null ? opponent.GetComponent<Fighter>() : null;
        public float StateTime { get; private set; }
        public int Victories { get; private set; }
        public int Defeats { get; private set; }

        public event Action<ArenaState> StateChanged;

        PlayerController m_Player;

        void Awake()
        {
            Current = this;
            range = Mathf.Max(range, 3.2f);
            priority = 5;
        }

        void Start()
        {
            if (opponent != null)
            {
                opponent.SetActive(false);
                OpponentFighter.KnockedOut += OnOpponentKO;
            }
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
            if (opponent != null && OpponentFighter != null) OpponentFighter.KnockedOut -= OnOpponentKO;
            if (m_Player != null) m_Player.Fighter.KnockedOut -= OnPlayerKO;
        }

        public override Vector3 InteractionPoint => opponent != null ? opponent.transform.position : transform.position;

        public override string GetPrompt(GameObject user)
        {
            if (opponent == null) return null;
            switch (State)
            {
                case ArenaState.Waiting: return $"Défier {OpponentFighter.displayName}";
                default: return null;
            }
        }

        public override void Interact(GameObject user)
        {
            var player = user != null ? user.GetComponent<PlayerController>() : null;
            if (player == null || State != ArenaState.Waiting) return;
            BeginFight(player);
        }

        public void BeginFight(PlayerController player)
        {
            if (m_Player != player)
            {
                if (m_Player != null) m_Player.Fighter.KnockedOut -= OnPlayerKO;
                m_Player = player;
                m_Player.Fighter.KnockedOut += OnPlayerKO;
            }
            opponent.Target = player.Fighter;
            SetState(ArenaState.Starting);
        }

        void SetState(ArenaState state)
        {
            State = state;
            StateTime = 0f;
            StateChanged?.Invoke(state);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            StateTime += dt;

            switch (State)
            {
                case ArenaState.Starting:
                    if (StateTime >= startDelay)
                    {
                        opponent.SetActive(true);
                        SetState(ArenaState.Fighting);
                    }
                    break;

                case ArenaState.Fighting:
                    if (m_Player != null && Vector3.Distance(m_Player.transform.position, transform.position) > arenaRadius)
                    {
                        // The player walked away: the fight is abandoned, the opponent waits again.
                        ResetFight(false);
                    }
                    break;

                case ArenaState.Victory:
                case ArenaState.Defeat:
                    if (StateTime >= restartDelay && GameInput.Exists)
                    {
                        var input = GameInput.Instance;
                        bool restart = input.Interact != null && input.Interact.WasPressedThisFrame();
                        if (restart) ResetFight(true);
                    }
                    break;
            }
        }

        void OnOpponentKO(Fighter f)
        {
            if (State != ArenaState.Fighting) return;
            Victories++;
            opponent.SetActive(false);
            SetState(ArenaState.Victory);
        }

        void OnPlayerKO(Fighter f)
        {
            if (State != ArenaState.Fighting && State != ArenaState.Starting) return;
            Defeats++;
            opponent.SetActive(false);
            SetState(ArenaState.Defeat);
        }

        /// <summary>Restarts the test fight: both fighters healed, opponent back to its mark, player on foot.</summary>
        public void ResetFight(bool placePlayer)
        {
            if (opponent != null)
            {
                Transform start = opponentStart != null ? opponentStart : opponent.transform;
                opponent.ResetTo(start.position, start.rotation);
            }
            if (m_Player != null)
            {
                m_Player.Revive();
                if (placePlayer && playerStart != null && m_Player.State == PlayerState.APied)
                    m_Player.Motor.Teleport(playerStart.position, playerStart.rotation);
            }
            SetState(ArenaState.Waiting);
        }

        public string StatusText
        {
            get
            {
                string key = GameInput.Exists ? InputPrompts.For(GameInput.Instance.Interact) : "F";
                switch (State)
                {
                    case ArenaState.Starting: return "Combat !";
                    case ArenaState.Victory: return StateTime >= restartDelay ? $"Victoire — {key} pour recommencer" : "Victoire";
                    case ArenaState.Defeat: return StateTime >= restartDelay ? $"K.O. — {key} pour recommencer" : "K.O.";
                    default: return null;
                }
            }
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.85f, 0.15f, 0.12f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, arenaRadius);
        }
    }
}

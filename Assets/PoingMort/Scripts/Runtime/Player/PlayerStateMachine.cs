using System;

namespace PoingMort.Player
{
    /// <summary>States named after the brief: ÀPied, Embarquement, EnVoiture, Sortie, Chute (+ KO for combat defeats).</summary>
    public enum PlayerState
    {
        APied,
        Embarquement,
        EnVoiture,
        Sortie,
        Chute,
        KO,
    }

    /// <summary>
    /// Guards the allowed transitions so only one system drives the player at a time.
    /// Pure logic (no Unity callbacks) so it can be unit tested.
    /// </summary>
    public sealed class PlayerStateMachine
    {
        public PlayerState State { get; private set; } = PlayerState.APied;
        public float TimeInState { get; private set; }

        public event Action<PlayerState, PlayerState> Changed;

        public static bool IsAllowed(PlayerState from, PlayerState to)
        {
            if (from == to) return false;
            switch (from)
            {
                case PlayerState.APied:
                    return to == PlayerState.Embarquement || to == PlayerState.Chute || to == PlayerState.KO;
                case PlayerState.Embarquement:
                    // Boarding either succeeds or is aborted back to foot (e.g. the car became unreachable).
                    return to == PlayerState.EnVoiture || to == PlayerState.APied;
                case PlayerState.EnVoiture:
                    return to == PlayerState.Sortie;
                case PlayerState.Sortie:
                    return to == PlayerState.APied || to == PlayerState.Chute;
                case PlayerState.Chute:
                    return to == PlayerState.APied || to == PlayerState.KO;
                case PlayerState.KO:
                    return to == PlayerState.APied;
                default:
                    return false;
            }
        }

        public bool CanEnter(PlayerState to) => IsAllowed(State, to);

        public bool TryEnter(PlayerState to)
        {
            if (!IsAllowed(State, to)) return false;
            var previous = State;
            State = to;
            TimeInState = 0f;
            Changed?.Invoke(previous, to);
            return true;
        }

        /// <summary>Used by respawn/reset: forces the foot state whatever happened.</summary>
        public void ForceReset()
        {
            var previous = State;
            State = PlayerState.APied;
            TimeInState = 0f;
            if (previous != PlayerState.APied)
                Changed?.Invoke(previous, PlayerState.APied);
        }

        public void Tick(float dt) => TimeInState += dt;

        public bool IsOnFoot => State == PlayerState.APied;
        public bool IsInVehicle => State == PlayerState.EnVoiture;
        public bool IsInTransition => State == PlayerState.Embarquement || State == PlayerState.Sortie;
    }
}

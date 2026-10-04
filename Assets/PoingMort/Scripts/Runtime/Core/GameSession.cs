using System;
using PoingMort.Controls;
using UnityEngine;

namespace PoingMort.Core
{
    /// <summary>
    /// Gameplay session of the "Quartier" scene: explicit pause (the only thing allowed to suspend the
    /// simulation), cursor handling and restoring the right input context when resuming.
    /// </summary>
    [DefaultExecutionOrder(-900)]
    public sealed class GameSession : MonoBehaviour
    {
        public static GameSession Current { get; private set; }

        public bool IsPaused { get; private set; }

        /// <summary>Context the gameplay wants when not paused (on foot or in vehicle).</summary>
        public InputContext GameplayContext { get; private set; } = InputContext.OnFoot;

        public event Action<bool> PauseChanged;

        void Awake()
        {
            Current = this;
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }

        void OnEnable()
        {
            var input = GameInput.Instance;
            input.SetContext(GameplayContext);
            LockCursor(true);
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }

        /// <summary>Called by the player controller when switching between on-foot and vehicle commands.</summary>
        public void SetGameplayContext(InputContext context)
        {
            GameplayContext = context;
            if (!IsPaused)
                GameInput.Instance.SetContext(context);
        }

        void Update()
        {
            var input = GameInput.Instance;
            if (!IsPaused)
            {
                bool pausePressed = (input.Pause != null && input.Pause.WasPressedThisFrame()) ||
                                    (input.VehiclePause != null && input.VehiclePause.WasPressedThisFrame());
                if (pausePressed) SetPaused(true);
            }
        }

        public void TogglePause() => SetPaused(!IsPaused);

        public void SetPaused(bool paused)
        {
            if (paused == IsPaused) return;
            IsPaused = paused;
            Time.timeScale = paused ? 0f : 1f;
            AudioListener.pause = paused;
            GameInput.Instance.SetContext(paused ? InputContext.Menu : GameplayContext);
            LockCursor(!paused);
            PauseChanged?.Invoke(paused);
        }

        public void ReturnToMainMenu()
        {
            IsPaused = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            LockCursor(false);
            SceneFlow.LoadMainMenu();
        }

        public static void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus && !IsPaused) LockCursor(true);
        }
    }
}

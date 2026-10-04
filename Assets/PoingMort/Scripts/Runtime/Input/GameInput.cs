using UnityEngine;
using UnityEngine.InputSystem;

namespace PoingMort.Controls
{
    /// <summary>Which group of commands is active. Only one gameplay map is enabled at a time.</summary>
    public enum InputContext
    {
        None,
        OnFoot,
        Vehicle,
        Menu,
    }

    public enum ControlScheme
    {
        KeyboardMouse,
        Gamepad,
    }

    /// <summary>
    /// All game commands, built in code with the Input System.
    /// Keyboard bindings use physical key positions: "&lt;Keyboard&gt;/w" is the key labelled W on QWERTY
    /// and Z on AZERTY, so ZQSD/WASD work without any setting. <see cref="InputPrompts"/> shows the
    /// label of the user's actual layout.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class GameInput : MonoBehaviour
    {
        static GameInput s_Instance;

        public static GameInput Instance
        {
            get
            {
                if (s_Instance == null)
                {
                    var go = new GameObject("[GameInput]");
                    s_Instance = go.AddComponent<GameInput>();
                }
                return s_Instance;
            }
        }

        public static bool Exists => s_Instance != null;

        public InputActionAsset Asset { get; private set; }
        public InputActionMap OnFootMap { get; private set; }
        public InputActionMap VehicleMap { get; private set; }
        public InputActionMap MenuMap { get; private set; }

        // À pied
        public InputAction Move { get; private set; }
        public InputAction LookPointer { get; private set; }
        public InputAction LookStick { get; private set; }
        public InputAction Sprint { get; private set; }
        public InputAction Interact { get; private set; }
        public InputAction LightAttack { get; private set; }
        public InputAction HeavyAttack { get; private set; }
        public InputAction Guard { get; private set; }
        public InputAction Dodge { get; private set; }
        public InputAction LockOn { get; private set; }
        public InputAction Pause { get; private set; }

        // En voiture
        public InputAction Throttle { get; private set; }
        public InputAction LaneChange { get; private set; }
        public InputAction VehicleLookPointer { get; private set; }
        public InputAction VehicleLookStick { get; private set; }
        public InputAction ExitVehicle { get; private set; }
        public InputAction Horn { get; private set; }
        public InputAction VehiclePause { get; private set; }

        // Menus
        public InputAction MenuBack { get; private set; }

        public InputContext Context { get; private set; } = InputContext.None;
        public ControlScheme Scheme { get; private set; } = ControlScheme.KeyboardMouse;

        public event System.Action<ControlScheme> SchemeChanged;
        public event System.Action<InputContext> ContextChanged;

        void Awake()
        {
            if (s_Instance != null && s_Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            s_Instance = this;
            DontDestroyOnLoad(gameObject);
            Build();
        }

        void OnDestroy()
        {
            if (s_Instance == this)
            {
                s_Instance = null;
                if (Asset != null)
                {
                    Asset.Disable();
                    Destroy(Asset);
                }
            }
        }

        void Build()
        {
            Asset = ScriptableObject.CreateInstance<InputActionAsset>();
            Asset.name = "PoingMortControls";

            // ---------------- À pied ----------------
            OnFootMap = Asset.AddActionMap("APied");

            Move = OnFootMap.AddAction("Deplacement", InputActionType.Value, expectedControlLayout: "Vector2");
            Move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            Move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");
            Move.AddBinding("<Gamepad>/leftStick");

            LookPointer = OnFootMap.AddAction("CameraSouris", InputActionType.Value, "<Mouse>/delta", expectedControlLayout: "Vector2");
            LookStick = OnFootMap.AddAction("CameraManette", InputActionType.Value, "<Gamepad>/rightStick", expectedControlLayout: "Vector2");

            Sprint = OnFootMap.AddAction("Sprint", InputActionType.Button, "<Keyboard>/leftShift");
            Sprint.AddBinding("<Gamepad>/leftStickPress");

            Interact = OnFootMap.AddAction("Interagir", InputActionType.Button, "<Keyboard>/f");
            Interact.AddBinding("<Gamepad>/buttonNorth");

            LightAttack = OnFootMap.AddAction("CoupRapide", InputActionType.Button, "<Mouse>/leftButton");
            LightAttack.AddBinding("<Gamepad>/buttonWest");

            HeavyAttack = OnFootMap.AddAction("CoupPuissant", InputActionType.Button, "<Keyboard>/e");
            HeavyAttack.AddBinding("<Gamepad>/buttonEast");

            Guard = OnFootMap.AddAction("Garde", InputActionType.Button, "<Mouse>/rightButton");
            Guard.AddBinding("<Gamepad>/leftTrigger");

            Dodge = OnFootMap.AddAction("Esquive", InputActionType.Button, "<Keyboard>/space");
            Dodge.AddBinding("<Gamepad>/buttonSouth");

            LockOn = OnFootMap.AddAction("VerrouillerCible", InputActionType.Button, "<Keyboard>/tab");
            LockOn.AddBinding("<Mouse>/middleButton");
            LockOn.AddBinding("<Gamepad>/rightStickPress");

            Pause = OnFootMap.AddAction("Pause", InputActionType.Button, "<Keyboard>/escape");
            Pause.AddBinding("<Gamepad>/start");

            // ---------------- En voiture ----------------
            VehicleMap = Asset.AddActionMap("Voiture");

            Throttle = VehicleMap.AddAction("Allure", InputActionType.Value, expectedControlLayout: "Axis");
            Throttle.AddCompositeBinding("1DAxis")
                .With("Positive", "<Keyboard>/w")
                .With("Negative", "<Keyboard>/s");
            Throttle.AddCompositeBinding("1DAxis")
                .With("Positive", "<Keyboard>/upArrow")
                .With("Negative", "<Keyboard>/downArrow");
            Throttle.AddCompositeBinding("1DAxis")
                .With("Positive", "<Gamepad>/rightTrigger")
                .With("Negative", "<Gamepad>/leftTrigger");

            LaneChange = VehicleMap.AddAction("ChangerDeVoie", InputActionType.Value, expectedControlLayout: "Axis");
            LaneChange.AddCompositeBinding("1DAxis")
                .With("Negative", "<Keyboard>/a")
                .With("Positive", "<Keyboard>/d");
            LaneChange.AddCompositeBinding("1DAxis")
                .With("Negative", "<Keyboard>/leftArrow")
                .With("Positive", "<Keyboard>/rightArrow");
            LaneChange.AddBinding("<Gamepad>/leftStick/x");

            VehicleLookPointer = VehicleMap.AddAction("CameraSouris", InputActionType.Value, "<Mouse>/delta", expectedControlLayout: "Vector2");
            VehicleLookStick = VehicleMap.AddAction("CameraManette", InputActionType.Value, "<Gamepad>/rightStick", expectedControlLayout: "Vector2");

            ExitVehicle = VehicleMap.AddAction("Sortir", InputActionType.Button, "<Keyboard>/f");
            ExitVehicle.AddBinding("<Gamepad>/buttonNorth");

            Horn = VehicleMap.AddAction("Klaxon", InputActionType.Button, "<Keyboard>/space");
            Horn.AddBinding("<Gamepad>/buttonSouth");

            VehiclePause = VehicleMap.AddAction("Pause", InputActionType.Button, "<Keyboard>/escape");
            VehiclePause.AddBinding("<Gamepad>/start");

            // ---------------- Menus ----------------
            MenuMap = Asset.AddActionMap("Menu");
            MenuBack = MenuMap.AddAction("Retour", InputActionType.Button, "<Keyboard>/escape");
            MenuBack.AddBinding("<Gamepad>/buttonEast");
            MenuBack.AddBinding("<Gamepad>/start");
        }

        /// <summary>Enables the map of the given context and disables the others.</summary>
        public void SetContext(InputContext context)
        {
            if (Asset == null) return;
            OnFootMap.Disable();
            VehicleMap.Disable();
            MenuMap.Disable();
            switch (context)
            {
                case InputContext.OnFoot: OnFootMap.Enable(); break;
                case InputContext.Vehicle: VehicleMap.Enable(); break;
                case InputContext.Menu: MenuMap.Enable(); break;
            }
            if (Context != context)
            {
                Context = context;
                ContextChanged?.Invoke(context);
            }
        }

        void Update()
        {
            DetectScheme();
        }

        void DetectScheme()
        {
            ControlScheme scheme = Scheme;
            var pad = Gamepad.current;
            if (pad != null)
            {
                if (pad.leftStick.ReadValue().sqrMagnitude > 0.2f || pad.rightStick.ReadValue().sqrMagnitude > 0.2f ||
                    pad.buttonSouth.wasPressedThisFrame || pad.buttonNorth.wasPressedThisFrame ||
                    pad.buttonWest.wasPressedThisFrame || pad.buttonEast.wasPressedThisFrame ||
                    pad.startButton.wasPressedThisFrame || pad.leftTrigger.ReadValue() > 0.3f || pad.rightTrigger.ReadValue() > 0.3f)
                    scheme = ControlScheme.Gamepad;
            }
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if ((kb != null && kb.anyKey.wasPressedThisFrame) ||
                (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame || mouse.delta.ReadValue().sqrMagnitude > 4f)))
                scheme = ControlScheme.KeyboardMouse;

            if (scheme != Scheme)
            {
                Scheme = scheme;
                SchemeChanged?.Invoke(scheme);
            }
        }

        /// <summary>Camera input in degrees for this frame, sensitivity and inversion applied.</summary>
        public Vector2 ReadLookDegrees(bool vehicle, float deltaTime)
        {
            var pointer = vehicle ? VehicleLookPointer : LookPointer;
            var stick = vehicle ? VehicleLookStick : LookStick;
            Vector2 mouse = pointer != null && pointer.enabled ? pointer.ReadValue<Vector2>() : Vector2.zero;
            Vector2 pad = stick != null && stick.enabled ? stick.ReadValue<Vector2>() : Vector2.zero;
            float sens = Core.GameSettings.MouseSensitivity;
            Vector2 result = mouse * (0.085f * sens) + pad * (150f * sens * deltaTime);
            if (Core.GameSettings.InvertY) result.y = -result.y;
            return result;
        }
    }
}

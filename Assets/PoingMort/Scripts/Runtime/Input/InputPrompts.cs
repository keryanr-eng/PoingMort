using System.Text;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace PoingMort.Controls
{
    /// <summary>
    /// French labels for the commands, matching the keyboard layout actually in use
    /// (Z/Q on AZERTY, W/A on QWERTY) and the last device used (keyboard/mouse or gamepad).
    /// </summary>
    public static class InputPrompts
    {
        public static string For(InputAction action)
        {
            if (action == null) return "?";
            var scheme = GameInput.Exists ? GameInput.Instance.Scheme : ControlScheme.KeyboardMouse;
            bool wantGamepad = scheme == ControlScheme.Gamepad;

            var bindings = action.bindings;
            for (int i = 0; i < bindings.Count; i++)
            {
                var b = bindings[i];
                if (b.isPartOfComposite) continue;
                if (b.isComposite)
                {
                    string composite = CompositeLabel(action, i, wantGamepad);
                    if (!string.IsNullOrEmpty(composite)) return composite;
                    continue;
                }
                bool isPad = b.effectivePath.StartsWith("<Gamepad>");
                if (isPad != wantGamepad) continue;
                return PathLabel(b.effectivePath);
            }
            // Fallback: first non-composite binding whatever the device.
            for (int i = 0; i < bindings.Count; i++)
            {
                var b = bindings[i];
                if (!b.isComposite && !b.isPartOfComposite) return PathLabel(b.effectivePath);
            }
            return action.name;
        }

        static string CompositeLabel(InputAction action, int compositeIndex, bool wantGamepad)
        {
            var bindings = action.bindings;
            var sb = new StringBuilder();
            bool any = false;
            for (int j = compositeIndex + 1; j < bindings.Count && bindings[j].isPartOfComposite; j++)
            {
                string path = bindings[j].effectivePath;
                bool isPad = path.StartsWith("<Gamepad>");
                if (isPad != wantGamepad) return null;
                if (path.Contains("Arrow")) return null; // keep the letter keys as the main prompt
                if (any) sb.Append(' ');
                sb.Append(PathLabel(path));
                any = true;
            }
            return any ? sb.ToString() : null;
        }

        /// <summary>Human readable French label of a control path.</summary>
        public static string PathLabel(string path)
        {
            if (string.IsNullOrEmpty(path)) return "?";
            if (path.StartsWith("<Keyboard>/"))
            {
                string keyName = path.Substring("<Keyboard>/".Length);
                var kb = Keyboard.current;
                if (kb != null)
                {
                    var control = kb.TryGetChildControl<KeyControl>(keyName);
                    if (control != null)
                        return KeyLabel(keyName, control.displayName);
                }
                return KeyLabel(keyName, keyName);
            }
            switch (path)
            {
                case "<Mouse>/leftButton": return "Clic gauche";
                case "<Mouse>/rightButton": return "Clic droit";
                case "<Mouse>/middleButton": return "Clic molette";
                case "<Mouse>/delta": return "Souris";
                case "<Gamepad>/buttonSouth": return "A";
                case "<Gamepad>/buttonEast": return "B";
                case "<Gamepad>/buttonWest": return "X";
                case "<Gamepad>/buttonNorth": return "Y";
                case "<Gamepad>/leftTrigger": return "LT";
                case "<Gamepad>/rightTrigger": return "RT";
                case "<Gamepad>/leftShoulder": return "LB";
                case "<Gamepad>/rightShoulder": return "RB";
                case "<Gamepad>/start": return "Start";
                case "<Gamepad>/leftStick": return "Stick gauche";
                case "<Gamepad>/leftStick/x": return "Stick gauche";
                case "<Gamepad>/rightStick": return "Stick droit";
                case "<Gamepad>/leftStickPress": return "L3";
                case "<Gamepad>/rightStickPress": return "R3";
            }
            int slash = path.LastIndexOf('/');
            return slash >= 0 ? path.Substring(slash + 1) : path;
        }

        static string KeyLabel(string keyName, string displayName)
        {
            switch (keyName)
            {
                case "space": return "Espace";
                case "leftShift": return "Maj";
                case "rightShift": return "Maj droite";
                case "escape": return "Échap";
                case "tab": return "Tab";
                case "enter": return "Entrée";
                case "leftCtrl": return "Ctrl";
                case "upArrow": return "↑";
                case "downArrow": return "↓";
                case "leftArrow": return "←";
                case "rightArrow": return "→";
            }
            if (string.IsNullOrEmpty(displayName)) displayName = keyName;
            return displayName.Length <= 2 ? displayName.ToUpperInvariant() : displayName;
        }
    }
}

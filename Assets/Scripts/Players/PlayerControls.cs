// TideWall 3D — one player's controls: an InputActionMap restricted to that player's device(s).
// Covers any gamepad the Input System knows (Xbox, PlayStation, Switch Pro, Steam Deck, XInput/8BitDo…),
// generic HID joysticks, and keyboard + mouse. Bindings are built in code, so there is no asset to keep in sync.
// Every binding belongs to a group ("Keyboard" = keyboard + mouse, "Gamepad" = gamepads + joysticks); the first
// binding of each group is the one players can rebind in Settings → Controls (stored in Settings.bindings).
using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TideWall
{
    /// <summary>The buttons gameplay reads. Order matters: it's also the bit order of network input packets.</summary>
    public enum Btn { Fire, Melee, Interact, Back, Reload, Swap, Dash, Pause, View, Stats }

    public class PlayerControls : IDisposable
    {
        public const string KeyboardGroup = "Keyboard", GamepadGroup = "Gamepad";

        public readonly InputActionMap Map;
        public readonly InputAction Move, Aim, Look, Point;
        readonly InputAction[] buttons;
        public readonly InputDevice[] Devices;
        public bool UsesMouse { get; }
        public string Group => UsesMouse ? KeyboardGroup : GamepadGroup;

        /// <summary>Rebindable rows shown in Settings → Controls: (action, part of a composite or "", label).</summary>
        public static readonly (string action, string part, string label)[] Rebindable =
        {
            ("Move", "up", "Move forward"), ("Move", "down", "Move back"), ("Move", "left", "Move left"), ("Move", "right", "Move right"),
            ("Fire", "", "Shoot / throw"), ("Melee", "", "Melee"), ("Interact", "", "Interact"), ("Dash", "", "Dash"),
            ("Reload", "", "Reload"), ("Swap", "", "Swap gun"), ("View", "", "Switch view"), ("Stats", "", "Stats, mods and tidal cards"),
            ("Back", "", "Back / close menu"), ("Pause", "", "Pause"),
        };

        public PlayerControls(InputDevice[] devices)
        {
            Devices = devices;
            UsesMouse = Array.Exists(devices, d => d is Mouse || d is Keyboard);
            Map = Build(out Move, out Aim, out Look, out Point, out buttons);
            ApplyOverrides(Map);
            Map.devices = devices;
            Map.Enable();
            Settings.OnChanged += Reapply;
        }

        /// <summary>The default bindings. Also used (without devices) as the template the rebinding UI edits.</summary>
        public static InputActionMap Build(out InputAction move, out InputAction aim, out InputAction look, out InputAction point, out InputAction[] buttons)
        {
            var map = new InputActionMap("Player");
            move = map.AddAction("Move", InputActionType.Value, expectedControlLayout: "Vector2");
            move.AddBinding("<Gamepad>/leftStick", groups: GamepadGroup);
            move.AddBinding("<Gamepad>/dpad", groups: GamepadGroup);
            move.AddBinding("<Joystick>/stick", groups: GamepadGroup);
            move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/w", KeyboardGroup).With("Down", "<Keyboard>/s", KeyboardGroup)
                .With("Left", "<Keyboard>/a", KeyboardGroup).With("Right", "<Keyboard>/d", KeyboardGroup);
            move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/upArrow", KeyboardGroup).With("Down", "<Keyboard>/downArrow", KeyboardGroup)
                .With("Left", "<Keyboard>/leftArrow", KeyboardGroup).With("Right", "<Keyboard>/rightArrow", KeyboardGroup);

            aim = map.AddAction("Aim", InputActionType.Value, expectedControlLayout: "Vector2");      // right stick: look (first person) or aim (top-down)
            aim.AddBinding("<Gamepad>/rightStick", groups: GamepadGroup);
            look = map.AddAction("Look", InputActionType.Value, expectedControlLayout: "Vector2");    // mouse movement (first person)
            look.AddBinding("<Mouse>/delta", groups: KeyboardGroup);
            point = map.AddAction("Point", InputActionType.Value, expectedControlLayout: "Vector2");  // cursor position (top-down)
            point.AddBinding("<Mouse>/position", groups: KeyboardGroup);

            buttons = new InputAction[Enum.GetValues(typeof(Btn)).Length];
            buttons[(int)Btn.Fire] = Button(map, "Fire", new[] { "<Gamepad>/rightTrigger", "<Joystick>/trigger" }, new[] { "<Mouse>/leftButton" });
            buttons[(int)Btn.Melee] = Button(map, "Melee", new[] { "<Gamepad>/rightShoulder", "<Joystick>/button3" }, new[] { "<Mouse>/rightButton", "<Keyboard>/f" });
            buttons[(int)Btn.Interact] = Button(map, "Interact", new[] { "<Gamepad>/buttonSouth", "<Joystick>/button2" }, new[] { "<Keyboard>/e", "<Keyboard>/enter" });
            buttons[(int)Btn.Back] = Button(map, "Back", new[] { "<Gamepad>/buttonEast", "<Joystick>/button4" }, new[] { "<Keyboard>/backspace" });
            buttons[(int)Btn.Reload] = Button(map, "Reload", new[] { "<Gamepad>/buttonWest", "<Joystick>/button5" }, new[] { "<Keyboard>/r" });
            buttons[(int)Btn.Swap] = Button(map, "Swap", new[] { "<Gamepad>/buttonNorth", "<Joystick>/button6" }, new[] { "<Keyboard>/q", "<Mouse>/middleButton" });
            buttons[(int)Btn.Dash] = Button(map, "Dash", new[] { "<Gamepad>/leftShoulder", "<Gamepad>/leftTrigger", "<Joystick>/button7" }, new[] { "<Keyboard>/space", "<Keyboard>/leftShift" });
            buttons[(int)Btn.Pause] = Button(map, "Pause", new[] { "<Gamepad>/start", "<Joystick>/button8" }, new[] { "<Keyboard>/escape", "<Keyboard>/p" });
            buttons[(int)Btn.View] = Button(map, "View", new[] { "<Gamepad>/rightStickPress" }, new[] { "<Keyboard>/v" });
            buttons[(int)Btn.Stats] = Button(map, "Stats", new[] { "<Gamepad>/select", "<Joystick>/button9" }, new[] { "<Keyboard>/tab", "<Keyboard>/c" });
            return map;
        }

        static InputAction Button(InputActionMap map, string name, string[] pad, string[] keys)
        {
            var a = map.AddAction(name, InputActionType.Button);
            foreach (var b in pad) a.AddBinding(b, groups: GamepadGroup);
            foreach (var b in keys) a.AddBinding(b, groups: KeyboardGroup);
            return a;
        }

        public bool Held(Btn b) => buttons[(int)b].IsPressed();
        public bool Pressed(Btn b) => buttons[(int)b].WasPressedThisFrame();

        // ------------------------------------------------------------------ rebinding support
        public static string Key(string action, string group, string part) => $"{action}/{group}/{part}";

        /// <summary>Index of the rebindable binding: the first binding of that group (or that composite part).</summary>
        public static int BindingIndex(InputAction action, string group, string part)
        {
            var bs = action.bindings;
            for (int i = 0; i < bs.Count; i++)
            {
                var b = bs[i];
                if (b.isComposite || b.groups == null || !b.groups.Contains(group)) continue;
                bool wantPart = !string.IsNullOrEmpty(part);
                if (wantPart != b.isPartOfComposite) continue;
                if (wantPart && !string.Equals(b.name, part, StringComparison.OrdinalIgnoreCase)) continue;
                return i;
            }
            return -1;
        }

        public static void ApplyOverrides(InputActionMap map)
        {
            map.RemoveAllBindingOverrides();
            foreach (var o in Settings.Current.bindings)
            {
                var parts = o.key.Split('/');
                if (parts.Length < 3) continue;
                var action = map.FindAction(parts[0]);
                if (action == null) continue;
                int i = BindingIndex(action, parts[1], parts[2]);
                if (i >= 0) action.ApplyBindingOverride(i, o.path);
            }
        }

        void Reapply()
        {
            bool on = Map.enabled;
            if (on) Map.Disable();
            ApplyOverrides(Map);
            if (on) Map.Enable();
        }

        /// <summary>The key/button shown in prompts, e.g. "[E]" or "[A]". Follows the player's rebinding.</summary>
        public string Glyph(Btn b) => $"[{Display(buttons[(int)b], Group, "")}]";

        public static string Display(InputAction action, string group, string part)
        {
            int i = BindingIndex(action, group, part);
            if (i < 0) return "-";
            var s = action.GetBindingDisplayString(i, InputBinding.DisplayStringOptions.DontIncludeInteractions);
            return string.IsNullOrEmpty(s) ? "-" : s;
        }

        public bool Connected
        {
            get { foreach (var d in Devices) if (d == null || !d.added) return false; return true; }
        }

        public string DeviceName => UsesMouse ? "Keyboard & mouse" : Devices[0].displayName ?? Devices[0].name;

        public void Dispose() { Settings.OnChanged -= Reapply; Map.Disable(); Map.Dispose(); }
    }
}

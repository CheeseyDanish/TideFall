// TideWall 3D — interactive control rebinding (Settings → Controls). Works on a device-less copy of the default
// bindings, listens for the next key/button of the right kind, and stores the result in Settings.bindings, which
// every PlayerControls re-applies. Escape (or Start on a pad) cancels.
using UnityEngine;
using UnityEngine.InputSystem;

namespace TideWall
{
    public static class Rebinder
    {
        static InputActionMap template;
        static InputActionRebindingExtensions.RebindingOperation op;
        /// <summary>The row being rebound ("Fire (keyboard)"), or null. Menus ignore input while this is set.</summary>
        public static string Listening { get; private set; }
        /// <summary>Unscaled time of the last finished rebind: menus wait a moment so the new key doesn't also "click".</summary>
        public static float FinishedAt { get; private set; } = -10;
        public static bool Busy => Listening != null || Time.unscaledTime - FinishedAt < .25f;

        static InputActionMap Template
        {
            get
            {
                if (template != null) return template;
                template = PlayerControls.Build(out _, out _, out _, out _, out _, out _);
                return template;
            }
        }

        public static string Current(string action, string group, string part)
        {
            PlayerControls.ApplyOverrides(Template);
            return PlayerControls.Display(Template.FindAction(action), group, part);
        }

        public static void Start(string action, string group, string part, string label)
        {
            Cancel();
            var a = Template.FindAction(action);
            int index = PlayerControls.BindingIndex(a, group, part);
            if (index < 0) return;
            Template.Disable();
            Listening = label;
            var r = a.PerformInteractiveRebinding(index)
                     .WithCancelingThrough("<Keyboard>/escape")
                     .WithControlsExcluding("<Mouse>/position").WithControlsExcluding("<Mouse>/delta").WithControlsExcluding("<Mouse>/scroll")
                     .WithControlsExcluding("<Pointer>/position").WithControlsExcluding("<Gamepad>/start")
                     .OnMatchWaitForAnother(.08f);
            if (group == PlayerControls.KeyboardGroup) r.WithControlsHavingToMatchPath("<Keyboard>").WithControlsHavingToMatchPath("<Mouse>");
            else r.WithControlsHavingToMatchPath("<Gamepad>").WithControlsHavingToMatchPath("<Joystick>")
                  .WithControlsExcluding("<Gamepad>/leftStick").WithControlsExcluding("<Gamepad>/rightStick");
            if (!string.IsNullOrEmpty(part)) r.WithExpectedControlType("Button");
            op = r.OnComplete(o =>
                {
                    var path = a.bindings[index].overridePath;
                    Settings.Current.SetOverride(PlayerControls.Key(action, group, part), path);
                    Finish();
                    Settings.Changed();
                    Sfx.Play("bell", .4f, .2f);
                })
                .OnCancel(o => Finish())
                .Start();
        }

        public static void Cancel() { if (op != null) { op.Cancel(); Finish(); } }

        static void Finish()
        {
            op?.Dispose();
            op = null;
            Listening = null;
            FinishedAt = Time.unscaledTime;
        }

        public static void ResetAll()
        {
            Cancel();
            Settings.Current.bindings.Clear();
            Settings.Changed();
        }
    }
}

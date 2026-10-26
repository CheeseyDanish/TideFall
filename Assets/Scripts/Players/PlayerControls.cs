// TideWall 3D — one player's controls: an InputActionMap restricted to that player's device(s).
// Covers any gamepad the Input System knows (Xbox, PlayStation, Switch Pro, Steam Deck, XInput/8BitDo…),
// generic HID joysticks, and keyboard + mouse. Bindings are built in code, so there is no asset to keep in sync.
using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TideWall
{
    public class PlayerControls : IDisposable
    {
        public readonly InputActionMap Map;
        public readonly InputAction Move, Aim, Point, Fire, Melee, Interact, Back, Reload, Swap, Pause, Dash;
        public readonly InputDevice[] Devices;
        public bool UsesMouse { get; }

        public PlayerControls(InputDevice[] devices)
        {
            Devices = devices;
            UsesMouse = Array.Exists(devices, d => d is Mouse || d is Keyboard);
            Map = new InputActionMap("Player");

            Move = Map.AddAction("Move", InputActionType.Value, expectedControlLayout: "Vector2");
            Move.AddBinding("<Gamepad>/leftStick");
            Move.AddBinding("<Gamepad>/dpad");
            Move.AddBinding("<Joystick>/stick");
            Move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s").With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            Move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow").With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");

            Aim = Map.AddAction("Aim", InputActionType.Value, expectedControlLayout: "Vector2");
            Aim.AddBinding("<Gamepad>/rightStick");
            Point = Map.AddAction("Point", InputActionType.Value, "<Mouse>/position", expectedControlLayout: "Vector2");

            Fire = Button("Fire", "<Gamepad>/rightTrigger", "<Mouse>/leftButton", "<Joystick>/trigger");
            Melee = Button("Melee", "<Gamepad>/rightShoulder", "<Mouse>/rightButton", "<Keyboard>/f", "<Joystick>/button3");
            Interact = Button("Interact", "<Gamepad>/buttonSouth", "<Keyboard>/e", "<Keyboard>/enter", "<Joystick>/button2");
            Back = Button("Back", "<Gamepad>/buttonEast", "<Keyboard>/backspace", "<Keyboard>/tab", "<Joystick>/button4");
            Reload = Button("Reload", "<Gamepad>/buttonWest", "<Keyboard>/r", "<Joystick>/button5");
            Swap = Button("Swap", "<Gamepad>/buttonNorth", "<Keyboard>/q", "<Mouse>/middleButton", "<Joystick>/button6");
            Dash = Button("Dash", "<Gamepad>/leftShoulder", "<Gamepad>/leftTrigger", "<Keyboard>/space", "<Keyboard>/leftShift", "<Joystick>/button7");
            Pause = Button("Pause", "<Gamepad>/start", "<Keyboard>/escape", "<Keyboard>/p", "<Joystick>/button8");

            Map.devices = devices;
            Map.Enable();
        }

        InputAction Button(string name, params string[] bindings)
        {
            var a = Map.AddAction(name, InputActionType.Button);
            foreach (var b in bindings) a.AddBinding(b);
            return a;
        }

        public bool Connected
        {
            get { foreach (var d in Devices) if (d == null || !d.added) return false; return true; }
        }

        public string DeviceName => UsesMouse ? "Keyboard & mouse" : Devices[0].displayName ?? Devices[0].name;

        public void Dispose() { Map.Disable(); Map.Dispose(); }
    }
}

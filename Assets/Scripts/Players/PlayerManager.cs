// TideWall 3D — local multiplayer: drop-in joining (any button on an unused controller, or any key),
// up to 4 players, split-screen layout, the lobby/overview camera, and team-wide checks (ready, wipe).
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.UI;

namespace TideWall
{
    public class PlayerManager : MonoBehaviour
    {
        public const int MaxPlayers = 4;
        public readonly List<Player> All = new();
        public int Count => All.Count;
        public int ReadyCount { get { int n = 0; foreach (var p in All) if (p.Ready || p.IsOut) n++; return n; } }
        public bool AllReady => All.Count > 0 && ReadyCount >= All.Count;

        GameManager gm;
        IDisposable joinListener;
        Camera overview;
        Canvas overviewUi;
        Text title, info;
        float orbit;

        void Awake() => gm = GetComponent<GameManager>();

        void Start()
        {
            joinListener = InputSystem.onAnyButtonPress.Call(OnAnyButton);
            overview = new GameObject("Overview Camera").AddComponent<Camera>();
            overview.clearFlags = CameraClearFlags.SolidColor;
            overview.fieldOfView = 55; overview.farClipPlane = 260;
            overview.depth = -10;
            overviewUi = Ui.CanvasFor(overview, "Lobby UI", -1);
            title = Ui.Text(overviewUi.transform, "Title", new Vector2(.5f, .62f), Vector2.zero, new Vector2(1100, 140), 96, TextAnchor.MiddleCenter, new Color(.95f, .97f, 1));
            info = Ui.Text(overviewUi.transform, "Info", new Vector2(.5f, .36f), Vector2.zero, new Vector2(1100, 220), 22, TextAnchor.MiddleCenter, Color.white);
            Relayout();
        }

        void OnDestroy() => joinListener?.Dispose();

        void OnAnyButton(InputControl control)
        {
            if (All.Count >= MaxPlayers || gm.State == GameState.GameOver) return;
            var device = control.device;
            InputDevice[] devices;
            if (device is Keyboard || device is Mouse)
            {
                if (Keyboard.current == null) return;
                devices = Mouse.current != null ? new InputDevice[] { Keyboard.current, Mouse.current } : new InputDevice[] { Keyboard.current };
            }
            else if (device is Gamepad || device is Joystick) devices = new[] { device };
            else return;
            foreach (var p in All) foreach (var d in p.Controls.Devices) if (Array.IndexOf(devices, d) >= 0) return;   // already playing
            Join(devices);
        }

        /// <summary>Add a player using these devices (also used by tests with virtual gamepads).</summary>
        public Player Join(InputDevice[] devices)
        {
            int index = 0;
            while (All.Exists(p => p.Index == index)) index++;
            var spawn = new Vector3(-3 + index * 2, 0, -7);
            var player = Player.Create(index, new PlayerControls(devices), spawn);
            All.Add(player);
            Relayout();
            Sfx.Play("bell", .6f, .1f);
            gm.Banner($"{player.Name.ToUpperInvariant()} JOINS", player.Controls.DeviceName);
            if (gm.State == GameState.Lobby) gm.StartGame();
            Cursor.visible = !All.Exists(p => p.Controls.UsesMouse);
            return player;
        }

        /// <summary>Split the screen between players: 1 full, 2 stacked, 3–4 in quarters (the 4th quarter shows the overview).</summary>
        void Relayout()
        {
            int n = All.Count;
            Rect[] rects = n switch
            {
                0 => new Rect[0],
                1 => new[] { new Rect(0, 0, 1, 1) },
                2 => new[] { new Rect(0, .5f, 1, .5f), new Rect(0, 0, 1, .5f) },
                _ => new[] { new Rect(0, .5f, .5f, .5f), new Rect(.5f, .5f, .5f, .5f), new Rect(0, 0, .5f, .5f), new Rect(.5f, 0, .5f, .5f) },
            };
            for (int i = 0; i < n; i++) All[i].Cam.Camera.rect = rects[i];
            overview.enabled = n == 0 || n == 3;
            overview.rect = n == 3 ? new Rect(.5f, 0, .5f, .5f) : new Rect(0, 0, 1, 1);
        }

        void Update()
        {
            // The overview: a slow orbit over the beach (lobby) or a fixed high view (3-player 4th quarter).
            orbit += Time.unscaledDeltaTime * 4;
            overview.backgroundColor = gm.Phases.SkyColor;
            if (All.Count == 0)
            {
                var pos = new Vector3(Mathf.Sin(orbit * Mathf.Deg2Rad) * 30, 16, -24 + Mathf.Cos(orbit * Mathf.Deg2Rad) * 4);
                overview.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(new Vector3(0, 0, 14) - pos));
                title.text = "<b>TIDEWALL</b>";
                info.text = "Press any button on a controller, or any key, to join\n<size=17>Up to 4 players · split screen · Xbox, PlayStation, Switch Pro, generic gamepads, keyboard + mouse</size>\n\n" +
                            "<size=16>Move: left stick / WASD · Aim: right stick / mouse · Shoot: RT / left click · Melee: RB / right click\n" +
                            "Interact: [A] / E · Dash: LB / Space · Reload: [X] / R · Swap gun: [Y] / Q · Pause: Start / Esc</size>";
            }
            else if (overview.enabled)
            {
                overview.transform.SetPositionAndRotation(new Vector3(0, 42, -20), Quaternion.Euler(58, 0, 0));
                title.text = "";
                var ph = gm.Phases.Current;
                info.text = ph == null ? "" : $"<size=30><b>Day {gm.Phases.Day} · {ph.displayName}</b></size>\nWall {Mathf.CeilToInt(gm.Wall.Hp)}/{Mathf.RoundToInt(gm.Wall.MaxHp)} · {gm.Coins} coins\n" +
                                              "<size=16>A 4th player can join here</size>";
            }
        }

        public void OnPhaseStart(PhaseDefinition p)
        {
            foreach (var pl in All)
            {
                pl.Ready = false;
                if (pl.IsOut || pl.IsDowned) pl.Respawn(new Vector3(-3 + pl.Index * 2, 0, -7));
            }
        }

        /// <summary>If nobody is left standing, the night is lost.</summary>
        public void CheckWipe()
        {
            if (All.Count == 0) return;
            foreach (var p in All) if (!p.IsDowned && !p.IsOut) return;
            gm.GameOver("Everyone went down on the beach.");
        }
    }
}

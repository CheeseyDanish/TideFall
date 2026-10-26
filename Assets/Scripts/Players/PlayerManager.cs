// TideWall 3D — players: drop-in joining (any button on an unused controller, or any key), up to 4 players
// across split screen and online, the split-screen layout (only this machine's players get a viewport), the
// overview camera, the mouse cursor, and team-wide checks (ready, wipe).
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
        /// <summary>Every player in the game: local, online, and (on a client) mirrors of the host's players.</summary>
        public readonly List<Player> All = new();
        public int Count => All.Count;
        public int ReadyCount { get { int n = 0; foreach (var p in All) if (p.Ready || p.IsOut) n++; return n; } }
        public bool AllReady => All.Count > 0 && ReadyCount >= All.Count;
        /// <summary>Offline pause: someone on this machine has the pause or settings menu open.</summary>
        public bool AnyPauseMenuOpen => All.Exists(p => p.Kind == PlayerKind.Local && p.OpenedMenu is { System: true });
        public int LocalCount { get { int n = 0; foreach (var p in All) if (p.HasView) n++; return n; } }

        GameManager gm;
        IDisposable joinListener;
        Camera overview;
        Text info;
        readonly List<InputDevice[]> pendingJoins = new();   // client: waiting for the host to assign a slot

        void Awake()
        {
            gm = GetComponent<GameManager>();
            overview = new GameObject("Overview Camera").AddComponent<Camera>();
            overview.clearFlags = CameraClearFlags.SolidColor;
            overview.fieldOfView = 55; overview.farClipPlane = 260;
            overview.depth = -10;
            overview.cullingMask = PlayerCamera.SpectatorMask();
            overview.enabled = false;
            var ui = Ui.CanvasFor(overview, "Overview UI", -1);
            info = Ui.Text(ui.transform, "Info", new Vector2(.5f, .4f), Vector2.zero, new Vector2(1100, 260), 22, TextAnchor.MiddleCenter, Color.white);
        }

        void Start() => joinListener = InputSystem.onAnyButtonPress.Call(OnAnyButton);

        void OnDestroy() => joinListener?.Dispose();

        void OnAnyButton(InputControl control)
        {
            // Drop-in only during a run (the main menu has its own input).
            if (gm.State != GameState.Playing || Rebinder.Busy || All.Count >= MaxPlayers) return;
            var device = control.device;
            InputDevice[] devices;
            if (device is Keyboard || device is Mouse)
            {
                if (Keyboard.current == null) return;
                devices = Mouse.current != null ? new InputDevice[] { Keyboard.current, Mouse.current } : new InputDevice[] { Keyboard.current };
            }
            else if (device is Gamepad || device is Joystick) devices = new[] { device };
            else return;
            foreach (var p in All) if (p.Controls != null) foreach (var d in p.Controls.Devices) if (Array.IndexOf(devices, d) >= 0) return;   // already playing
            foreach (var pj in pendingJoins) foreach (var d in pj) if (Array.IndexOf(devices, d) >= 0) return;
            if (Net.IsClient) { pendingJoins.Add(devices); Net.Client.RequestJoin(devices); return; }
            Join(devices);
        }

        public int FreeIndex()
        {
            int index = 0;
            while (All.Exists(p => p.Index == index)) index++;
            return index < MaxPlayers ? index : -1;
        }

        public static Vector3 SpawnPoint(int index) => new(-3 + index * 2, 0, -7);

        /// <summary>Add a local player using these devices (also used by tests with virtual gamepads).</summary>
        public Player Join(InputDevice[] devices)
        {
            int index = FreeIndex();
            if (index < 0) return null;
            if (gm.State == GameState.MainMenu) gm.StartGame();
            var player = Player.Create(index, new PlayerControls(devices), SpawnPoint(index));
            Add(player);
            gm.Banner($"{player.Name.ToUpperInvariant()} JOINS", player.DeviceName);
            return player;
        }

        /// <summary>Host: an online player joins from connection `owner`.</summary>
        public Player JoinRemote(int owner, string nick)
        {
            int index = FreeIndex();
            if (index < 0) return null;
            var player = Player.CreateRemote(index, owner, nick, SpawnPoint(index));
            Add(player);
            gm.Banner($"{player.Name.ToUpperInvariant()} JOINS", $"{nick} (online)");
            return player;
        }

        /// <summary>Client: a mirror of one of the host's players (ours if `devices` is set).</summary>
        public Player AddProxy(int index, InputDevice[] devices, string nick, Vector3 pos)
        {
            if (devices != null) pendingJoins.RemoveAll(d => d == devices || (d.Length == devices.Length && d[0] == devices[0]));
            var player = Player.CreateProxy(index, devices != null ? new PlayerControls(devices) : null, nick, pos);
            Add(player);
            return player;
        }

        void Add(Player p)
        {
            All.Add(p);
            All.Sort((a, b) => a.Index.CompareTo(b.Index));
            Relayout();
            Sfx.Play("bell", .6f, .1f);
        }

        public void Remove(Player p)
        {
            if (p == null) return;
            All.Remove(p);
            Destroy(p.gameObject);
            Relayout();
            CheckWipe();
        }

        public Player ByIndex(int index) => All.Find(p => p.Index == index);

        /// <summary>The camera a name tag should face: another local player's view, or the first one.</summary>
        public Camera FirstViewCamera(Player except)
        {
            foreach (var p in All) if (p != except && p.Cam != null) return p.Cam.Camera;
            return except != null && except.Cam != null ? except.Cam.Camera : null;
        }

        /// <summary>Split the screen between this machine's players: 1 full, 2 stacked, 3–4 in quarters (3: the 4th quarter is an overview).</summary>
        void Relayout()
        {
            var views = All.FindAll(p => p.Cam != null);
            int n = views.Count;
            Rect[] rects = n switch
            {
                0 => new Rect[0],
                1 => new[] { new Rect(0, 0, 1, 1) },
                2 => new[] { new Rect(0, .5f, 1, .5f), new Rect(0, 0, 1, .5f) },
                _ => new[] { new Rect(0, .5f, .5f, .5f), new Rect(.5f, .5f, .5f, .5f), new Rect(0, 0, .5f, .5f), new Rect(.5f, 0, .5f, .5f) },
            };
            for (int i = 0; i < n; i++) views[i].Cam.Camera.rect = rects[i];
            overview.rect = n == 3 ? new Rect(.5f, 0, .5f, .5f) : new Rect(0, 0, 1, 1);
        }

        void Update()
        {
            int views = LocalCount;
            // The overview: an online client still waiting to join, or the empty 4th quarter with three players.
            overview.enabled = gm.State != GameState.MainMenu && (views == 0 || views == 3);
            if (overview.enabled)
            {
                overview.backgroundColor = gm.Phases.SkyColor;
                overview.transform.SetPositionAndRotation(new Vector3(0, 42, -20), Quaternion.Euler(58, 0, 0));
                var ph = gm.Phases.Current;
                string status = ph == null ? "" : $"<size=30><b>Day {gm.Phases.Day} · {ph.displayName}</b></size>\nWall {Mathf.CeilToInt(gm.Wall.Hp)}/{Mathf.RoundToInt(gm.Wall.MaxHp)} · {gm.Coins} coins\n";
                info.text = views == 0
                    ? status + (pendingJoins.Count > 0 ? "Joining…" : Net.IsClient && !Net.Client.Connected ? "Connecting…" : "<b>Press any button or key to join</b>")
                    : status + "<size=16>A 4th player can join here</size>";
            }
            UpdateCursor();
        }

        /// <summary>Lock the mouse while a keyboard player looks around in first person; free it in menus and top-down.</summary>
        void UpdateCursor()
        {
            var kb = All.Find(p => p.HasView && p.UsesMouse);
            if (gm.State == GameState.MainMenu) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; return; }
            if (kb == null) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; return; }
            bool menu = kb.ShownMenu != null || gm.State == GameState.GameOver;
            bool look = kb.FirstPerson && !menu && !kb.IsOut;
            Cursor.lockState = look ? CursorLockMode.Locked : menu ? CursorLockMode.None : CursorLockMode.Confined;
            Cursor.visible = menu;
        }

        public void OnPhaseStart(PhaseDefinition p)
        {
            foreach (var pl in All)
            {
                pl.Ready = false;
                if (pl.Simulated && (pl.IsOut || pl.IsDowned)) pl.Respawn(SpawnPoint(pl.Index));
            }
        }

        /// <summary>If nobody is left standing, the night is lost.</summary>
        public void CheckWipe()
        {
            if (All.Count == 0 || Net.IsClient || gm.State != GameState.Playing) return;
            foreach (var p in All) if (!p.IsDowned && !p.IsOut) return;
            gm.GameOver("Everyone went down on the beach.");
        }

        /// <summary>Client: forget join requests the host refused.</summary>
        public void JoinRefused() => pendingJoins.Clear();
        public List<InputDevice[]> PendingJoins => pendingJoins;
    }
}

// TideWall 3D — the main menu: a still view from the keep's upper room, through its big arched window, over the
// town and the morning beach (beach volleyball, umbrellas, shark fins and gulls), like the 2D game's shop screen.
// A panel on the left holds the menu: continue, new game, load, host or join online, settings, delete, quit.
// Works with mouse (hover + click, wheel), keyboard and any controller. Whoever picks a game becomes player 1.
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace TideWall
{
    public class MainMenu : MonoBehaviour
    {
        /// <summary>A message to show the next time the menu opens ("The host left", "Couldn't connect"…).</summary>
        public static string Notice;

        const int Rows = 12;
        GameManager gm;
        Camera cam;
        Canvas canvas;
        GameObject scenery;
        TMP_Text title, detail, help, notice;
        readonly List<(TMP_Text text, Image bg)> rows = new();
        RectTransform listRect;
        Menu menu;
        int scroll;
        InputActionMap map;
        InputAction nav, submit, cancel, point, click, wheel;
        InputDevice lastDevice;
        float repeatT; Vector2 lastNav;
        Vector2 lastMouse;
        // Typing (address, name)
        Action<string> typingDone;
        bool typingSecret;      // show dots instead of what's typed (passwords)
        // Online: kept in memory only, never saved to disk
        string roomPassword = "", joinCode = "";
        bool hostWithCode = true;
        string typing;
        bool shown;
        float noticeT;

        void Start()
        {
            gm = GetComponent<GameManager>();
            BuildInput();
            if (gm.State == GameState.MainMenu) Show();
        }

        void OnDestroy()
        {
            map?.Disable(); map?.Dispose();
            if (Keyboard.current != null) Keyboard.current.onTextInput -= OnText;
            NetDiscovery.StopListening();
        }

        // ------------------------------------------------------------------ show / hide
        public void Show()
        {
            if (shown) return;
            shown = true;
            if (cam == null) Build();
            cam.enabled = true;
            GraphicsApply.MenuBoost = true;
            canvas.gameObject.SetActive(true);
            scenery.SetActive(true);
            map.Enable();
            Open(Root());
            if (!string.IsNullOrEmpty(Notice)) { notice.text = Notice; noticeT = 8; Notice = null; }
        }

        public void Hide()
        {
            shown = false;
            GraphicsApply.MenuBoost = false;
            if (cam == null) return;
            cam.enabled = false;
            canvas.gameObject.SetActive(false);
            scenery.SetActive(false);
            map.Disable();
            typingDone = null;
            NetDiscovery.StopListening();
        }

        void Build()
        {
            // The camera: fixed in the keep's room, looking out of the window
            cam = new GameObject("Menu Camera").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.fieldOfView = 52; cam.nearClipPlane = .05f; cam.farClipPlane = 260;
            cam.cullingMask = PlayerCamera.SpectatorMask();
            cam.depth = 5;
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            var keep = gm.World.keep;
            var at = keep != null && keep.sockets.TryGetValue("Camera", out var s) ? s.position : new Vector3(0, 6.6f, -33);
            // Turned a little left, so the window sits to the right of the menu panel
            cam.transform.SetPositionAndRotation(at, Quaternion.LookRotation(new Vector3(-10, 2.5f, 14) - at));
            scenery = MenuScenery.Build(transform);

            // The panel
            canvas = Ui.CanvasFor(null, "Main Menu", 50);
            var c = canvas.transform;
            var panel = Ui.Rect(c, "Panel", new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, .5f), new Vector2(36, 0), new Vector2(470, -60));
            var bg = panel.gameObject.AddComponent<Image>(); bg.sprite = Ui.White; bg.color = new Color(.12f, .09f, .07f, .86f);
            var edge = Ui.Rect(panel, "Edge", new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, .5f), Vector2.zero, new Vector2(4, 0));
            edge.gameObject.AddComponent<Image>().color = new Color(.95f, .75f, .25f, .8f);
            var logo = Ui.Text(panel, "Logo", new Vector2(.5f, 1), new Vector2(0, -22), new Vector2(440, 70), 54, TextAnchor.MiddleCenter, new Color(.98f, .9f, .7f));
            logo.text = "<b>TIDEWALL</b>"; logo.rectTransform.pivot = new Vector2(.5f, 1);
            var tag = Ui.Text(panel, "Tag", new Vector2(.5f, 1), new Vector2(0, -88), new Vector2(440, 24), 15, TextAnchor.MiddleCenter, new Color(.85f, .8f, .7f));
            tag.text = "A sea-wall roguelike. 1–4 players, split screen or online."; tag.rectTransform.pivot = new Vector2(.5f, 1);
            title = Ui.Text(panel, "Title", new Vector2(.5f, 1), new Vector2(0, -126), new Vector2(420, 30), 20, TextAnchor.MiddleLeft, new Color(1, .82f, .4f));
            title.rectTransform.pivot = new Vector2(.5f, 1);
            listRect = Ui.Rect(panel, "List", new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -162), new Vector2(420, Rows * 27));
            for (int i = 0; i < Rows; i++)
            {
                var row = Ui.Rect(listRect, "Row", new Vector2(0, 1), new Vector2(1, 1), new Vector2(.5f, 1), new Vector2(0, -i * 27), new Vector2(0, 26));
                var img = row.gameObject.AddComponent<Image>(); img.sprite = Ui.White; img.color = Color.clear; img.raycastTarget = false;
                var t = Ui.Text(row, "Text", new Vector2(0, .5f), new Vector2(12, 0), new Vector2(400, 26), 17, TextAnchor.MiddleLeft, Color.white);
                t.rectTransform.pivot = new Vector2(0, .5f);
                t.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
                rows.Add((t, img));
            }
            detail = Ui.Text(panel, "Detail", new Vector2(.5f, 0), new Vector2(0, 74), new Vector2(420, 70), 14, TextAnchor.UpperLeft, new Color(.85f, .88f, .95f));
            detail.rectTransform.pivot = new Vector2(.5f, 0);
            help = Ui.Text(panel, "Help", new Vector2(.5f, 0), new Vector2(0, 18), new Vector2(420, 40), 13, TextAnchor.LowerLeft, new Color(.7f, .72f, .8f));
            help.rectTransform.pivot = new Vector2(.5f, 0);
            notice = Ui.Text(c, "Notice", new Vector2(.68f, .1f), Vector2.zero, new Vector2(700, 40), 20, TextAnchor.MiddleCenter, new Color(1, .85f, .6f));
        }

        void BuildInput()
        {
            map = new InputActionMap("Menu");
            nav = map.AddAction("Navigate", InputActionType.Value, expectedControlLayout: "Vector2");
            nav.AddBinding("<Gamepad>/leftStick"); nav.AddBinding("<Gamepad>/dpad"); nav.AddBinding("<Joystick>/stick");
            nav.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow").With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            nav.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s").With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            submit = map.AddAction("Submit", InputActionType.Button);
            foreach (var b in new[] { "<Gamepad>/buttonSouth", "<Gamepad>/start", "<Joystick>/trigger", "<Keyboard>/enter", "<Keyboard>/numpadEnter", "<Keyboard>/space", "<Keyboard>/e" }) submit.AddBinding(b);
            cancel = map.AddAction("Cancel", InputActionType.Button);
            foreach (var b in new[] { "<Gamepad>/buttonEast", "<Keyboard>/escape", "<Keyboard>/backspace" }) cancel.AddBinding(b);
            point = map.AddAction("Point", InputActionType.Value, "<Mouse>/position");
            click = map.AddAction("Click", InputActionType.Button, "<Mouse>/leftButton");
            wheel = map.AddAction("Wheel", InputActionType.Value, "<Mouse>/scroll");
            if (Keyboard.current != null) Keyboard.current.onTextInput += OnText;
        }

        // ------------------------------------------------------------------ menus
        Menu Root()
        {
            var m = new Menu { Title = "" };
            var latest = SaveSystem.Latest();
            if (latest != null)
                m.Add("Continue run", $"Day {latest.day}", $"Slot {latest.slot}: {latest.Summary}.", pick: () => StartOffline(latest, latest.slot));
            m.Add("New run", ">", "Start a new run. It saves every morning so you can stop and come back, but when the powerplant melts down the run (and its save) is over.",
                  open: () => Slots("NEW RUN", true, (save, slot) => StartOffline(null, slot)));
            if (latest != null)
                m.Add("Other runs", ">", "Continue a run from another slot.", open: () => Slots("CONTINUE A RUN", false, (save, slot) => StartOffline(save, slot)));
            m.Add("Host online game", ">", "Play with friends on other computers. You host; they join with your address (or find you on the same network).", open: HostMenu);
            m.Add("Join online game", ">", "Join a friend's game on your network or by address.", open: JoinMenu);
            m.Add("Settings", ">", "Audio, game and controls (rebind keyboard and controller).", open: () => Menus.Settings(Root, null));
            if (latest != null) m.Add("Delete a save", ">", "Free up a slot.", open: DeleteMenu);
            m.Add("Quit", null, "Close TideWall.", pick: Application.Quit);
            var legacy = Legacy.Load();
            m.Header("");
            m.Header(legacy.runs > 0 ? $"<size=13>Runs: {legacy.runs} · best: day {legacy.bestDay}, level {legacy.bestLevel}</size>"
                                     : "<size=13>Protect the Nuclear Sand Powerplant. Good luck.</size>");
            m.Header("<size=13>Others join any time: press a button on a free controller (or any key).</size>");
            m.FirstSelectable();
            return m;
        }

        Menu Slots(string heading, bool forNew, Action<SaveData, int> go)
        {
            var m = new Menu { Title = heading, Parent = Root, Rebuild = () => Slots(heading, forNew, go) };
            for (int i = 1; i <= SaveSystem.Slots; i++)
            {
                int slot = i;
                var d = SaveSystem.Load(slot);
                if (!forNew && d == null) continue;
                string right = d == null ? "empty" : forNew ? "overwrite" : $"Day {d.day}";
                string info = d == null ? "An empty slot." : $"{d.Summary}. Saved {Ago(d.savedAt)}.";
                m.Add($"Slot {slot}", right, forNew && d != null ? info + " Starting here replaces it." : info, pick: () => go(d, slot));
            }
            m.Add("Back", null, null, open: Root);
            m.FirstSelectable();
            return m;
        }

        static string Ago(string iso)
        {
            if (!DateTime.TryParse(iso, null, System.Globalization.DateTimeStyles.RoundtripKind, out var t)) return "some time ago";
            var d = DateTime.UtcNow - t.ToUniversalTime();
            return d.TotalMinutes < 2 ? "just now" : d.TotalHours < 2 ? $"{d.TotalMinutes:0} minutes ago" : d.TotalDays < 2 ? $"{d.TotalHours:0} hours ago" : $"{d.TotalDays:0} days ago";
        }

        Menu DeleteMenu()
        {
            var m = new Menu { Title = "DELETE A SAVE", Parent = Root, Rebuild = DeleteMenu };
            for (int i = 1; i <= SaveSystem.Slots; i++)
            {
                int slot = i;
                var d = SaveSystem.Load(slot);
                if (d == null) continue;
                m.Add($"Slot {slot}", $"Day {d.day}", $"{d.Summary}. You will be asked to confirm.", pick: () => Confirm($"Delete slot {slot}?", () => { SaveSystem.Delete(slot); Sfx.Play("deny"); Open(Root()); }));
            }
            m.Add("Back", null, null, open: Root);
            m.FirstSelectable();
            return m;
        }

        void Confirm(string question, Action yes)
        {
            var back = menu;
            var m = new Menu { Title = question, Parent = () => back };
            m.Add("No", null, null, open: () => back);
            m.Add("Yes", null, null, pick: yes);
            m.FirstSelectable();
            Open(m);
        }

        Menu HostMenu()
        {
            var s = Settings.Current;
            var m = new Menu { Title = "HOST ONLINE GAME", Parent = Root, Rebuild = HostMenu };
            Action<SaveData, int> go = (save, slot) => { if (hostWithCode) StartRelayHost(save, slot); else StartHost(save, slot); };
            m.Add("Host a new run", ">", "Saves to a slot like a normal run.", open: () => Slots("HOST A NEW RUN", true, go));
            if (SaveSystem.Latest() != null)
                m.Add("Host a saved run", ">", "Continue a run with friends.", open: () => Slots("HOST A SAVED RUN", false, go));
            var mode = m.Add("Friends join by", hostWithCode ? "< Join code >" : "< LAN / address >",
                hostWithCode ? "Join code: friends anywhere type a short code. Encrypted through Unity Relay, no router setup."
                             : "LAN / address: friends on your network find you under Join. Over the internet this needs the UDP port forwarded and isn't encrypted.");
            mode.OnAdjust = d => { hostWithCode = !hostWithCode; Open(HostMenu()); };
            var port = m.Add("Port", $"< {s.port} >", "The UDP port for LAN / address games (default 7777).");
            port.OnAdjust = d => { s.port = Mathf.Clamp(s.port + d, 1024, 65535); Settings.Changed(); port.Right = $"< {s.port} >"; };
            m.Header("");
            m.Add("Room password", roomPassword.Length > 0 ? "set" : "none", "Optional. Friends type it when they join. It's never sent over the network. Pick to type (empty = no password).",
                  pick: () => Type("", v => { roomPassword = (v ?? "").Trim(); Open(HostMenu()); }, secret: true));
            m.Add("Your name", s.playerName, "Shown above your sailor. Pick to type.", pick: () => Type(s.playerName, v => { s.playerName = Clean(v, 16, "Sailor"); Settings.Changed(); Open(HostMenu()); }));
            m.Header($"<size=13>Your addresses: {NetDiscovery.LocalAddresses()}</size>");
            m.Add("Back", null, null, open: Root);
            m.FirstSelectable();
            return m;
        }

        Menu JoinMenu()
        {
            var s = Settings.Current;
            NetDiscovery.StartListening();
            var m = new Menu { Title = "JOIN ONLINE GAME", Parent = () => { NetDiscovery.StopListening(); return Root(); }, Rebuild = JoinMenu };
            m.Header("GAMES ON YOUR NETWORK");
            if (NetDiscovery.Games.Count == 0) m.Header("<size=13>   Looking…</size>");
            foreach (var g in NetDiscovery.Games)
            {
                var game = g;
                if (!string.IsNullOrEmpty(game.code)) m.Add($"{game.nick}'s game", game.code, $"{game.info}. Join code {game.code}.", pick: () => StartRelayClient(game.code));
                else m.Add($"{game.nick}'s game", game.address, $"{game.info}. {game.address}:{game.port}", pick: () => StartClient(game.address, game.port));
            }
            m.Header("BY JOIN CODE");
            m.Add("Join code", joinCode.Length > 0 ? joinCode : "type it", "The code on your friend's screen (their pause menu shows it). Pick to type.",
                  pick: () => Type(joinCode, v => { joinCode = NetRelay.Clean(v); Open(JoinMenu()); }));
            m.Add("Join with code", null, "Connect through Unity Relay (encrypted).", pick: () => { if (joinCode.Length == 0) Say("Type the join code first."); else StartRelayClient(joinCode); });
            m.Header("BY ADDRESS");
            m.Add("Address", s.lastAddress, "Your friend's IP address (their pause menu shows it). Pick to type.",
                  pick: () => Type(s.lastAddress, v => { s.lastAddress = Clean(v, 64, "127.0.0.1"); Settings.Changed(); Open(JoinMenu()); }));
            m.Add("Connect", $"port {s.port}", $"Connect to {s.lastAddress}:{s.port}.", pick: () => StartClient(s.lastAddress, s.port));
            m.Add("Room password", roomPassword.Length > 0 ? "set" : "none", "If the host set one. Pick to type.", pick: () => Type("", v => { roomPassword = (v ?? "").Trim(); Open(JoinMenu()); }, secret: true));
            m.Add("Your name", s.playerName, "Shown above your sailor. Pick to type.", pick: () => Type(s.playerName, v => { s.playerName = Clean(v, 16, "Sailor"); Settings.Changed(); Open(JoinMenu()); }));
            m.Add("Back", null, null, open: m.Parent);
            m.FirstSelectable();
            return m;
        }

        static string Clean(string v, int max, string fallback)
        {
            v = (v ?? "").Trim();
            if (v.Length > max) v = v.Substring(0, max);
            return v.Length == 0 ? fallback : v;
        }

        // ------------------------------------------------------------------ starting
        InputDevice[] PlayerDevices()
        {
            var d = lastDevice;
            if (d is Gamepad || d is Joystick) return new[] { d };
            if (Keyboard.current == null) return Gamepad.all.Count > 0 ? new InputDevice[] { Gamepad.all[0] } : new InputDevice[0];
            return Mouse.current != null ? new InputDevice[] { Keyboard.current, Mouse.current } : new InputDevice[] { Keyboard.current };
        }

        void StartOffline(SaveData save, int slot)
        {
            var devs = PlayerDevices();
            gm.StartRun(save, slot);
            if (devs.Length > 0) gm.Players.Join(devs);
        }

        void StartHost(SaveData save, int slot)
        {
            var s = Settings.Current;
            if (Net.StartHost(s.port, s.playerName, roomPassword, out var error) == null) { Say(error); return; }
            StartOffline(save, slot);
        }

        void StartRelayHost(SaveData save, int slot)
        {
            var s = Settings.Current;
            Waiting("SETTING UP", "Getting a join code from Unity Relay…", HostMenu);
            Net.StartRelayHost(s.playerName, roomPassword, (host, error) =>
            {
                if (!shown || gm.State != GameState.MainMenu) { if (host != null) Net.Shutdown(null); return; }
                if (host == null) { Say(error); Open(HostMenu()); return; }
                StartOffline(save, slot);
            });
        }

        void StartClient(string address, int port)
        {
            Net.StartClient(address, port, Settings.Current.playerName, roomPassword, PlayerDevices());
            Waiting("CONNECTING", $"Connecting to {address}:{port}…", JoinMenu);
        }

        void StartRelayClient(string code)
        {
            Net.StartRelayClient(code, Settings.Current.playerName, roomPassword, PlayerDevices());
            Waiting("CONNECTING", $"Joining game {code}…", JoinMenu);
        }

        void Waiting(string title, string what, Func<Menu> back)
        {
            var m = new Menu { Title = title, Parent = () => { Net.Shutdown(null); return back(); } };
            m.Add(what, null, "This can take a few seconds.");
            m.Add("Cancel", null, null, open: m.Parent);
            m.Selected = 1;
            Open(m);
        }

        void Say(string msg) { notice.text = msg; noticeT = 6; Sfx.Play("deny"); }

        // ------------------------------------------------------------------ frame
        void Open(Menu m) { menu = m; scroll = 0; if (menu.Current == null) menu.FirstSelectable(); }

        /// <summary>Show a particular menu (the screenshot tour uses this).</summary>
        public void OpenMenu(Menu m) { if (shown) Open(m); }

        void Update()
        {
            if (!shown) return;
            if (gm.State != GameState.MainMenu) { Hide(); return; }
            World.SetupCameraSky(cam, gm.Phases.SkyColor);
            NetDiscovery.Poll();
            if (Net.IsClient && Net.Client.Status == NetClient.State.Failed)
            {
                Say(Net.Client.Error);
                Net.Shutdown(null);
                Open(JoinMenu());
            }
            if (menu.Title == "JOIN ONLINE GAME" && Time.frameCount % 30 == 0) { int sel = menu.Selected; Open(JoinMenu()); menu.Selected = Mathf.Min(sel, menu.Items.Count - 1); if (menu.Current == null) menu.FirstSelectable(); }
            noticeT -= Time.unscaledDeltaTime;
            notice.color = new Color(1, .85f, .6f, Mathf.Clamp01(noticeT));
            if (typingDone == null && !Rebinder.Busy) HandleInput();
            else if (typingDone != null && submit.WasPressedThisFrame() && Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame) FinishTyping(true);
            else if (typingDone != null && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) FinishTyping(false);
            Draw();
        }

        void HandleInput()
        {
            var v = nav.ReadValue<Vector2>();
            repeatT -= Time.unscaledDeltaTime;
            int dy = v.y > .5f ? -1 : v.y < -.5f ? 1 : 0, dx = v.x > .5f ? 1 : v.x < -.5f ? -1 : 0;
            bool fresh = Mathf.Abs(lastNav.y) <= .5f && Mathf.Abs(lastNav.x) <= .5f;
            if ((dy != 0 || dx != 0) && (fresh || repeatT <= 0))
            {
                lastDevice = nav.activeControl?.device ?? lastDevice;
                if (dy != 0) { menu.Move(dy); Sfx.Play("ui", .5f); }
                else if (menu.Current?.OnAdjust != null) { menu.Current.OnAdjust(dx); Sfx.Play("ui", .5f); }
                repeatT = fresh ? .35f : .09f;
            }
            lastNav = v;

            // Mouse: hover selects, click picks, wheel scrolls
            var mp = point.ReadValue<Vector2>();
            bool moved = (mp - lastMouse).sqrMagnitude > 1; lastMouse = mp;
            int hovered = HoveredRow(mp);
            if (moved && hovered >= 0 && !menu.Items[hovered].Header) menu.Selected = hovered;
            float w = wheel.ReadValue<Vector2>().y;
            if (Mathf.Abs(w) > .1f) { menu.Move(w > 0 ? -1 : 1); }
            if (click.WasPressedThisFrame() && hovered >= 0 && !menu.Items[hovered].Header)
            {
                lastDevice = Mouse.current;
                menu.Selected = hovered;
                Pick();
                return;
            }
            if (submit.WasPressedThisFrame()) { lastDevice = submit.activeControl?.device ?? lastDevice; Pick(); }
            else if (cancel.WasPressedThisFrame()) { lastDevice = cancel.activeControl?.device ?? lastDevice; Back(); }
        }

        void Pick()
        {
            var it = menu.Current;
            if (it == null) return;
            var why = it.Blocked?.Invoke();
            if (why != null) { Say(why); return; }
            Sfx.Play("ui");
            var before = menu;
            it.OnPick?.Invoke();
            if (!shown || menu != before) return;
            var next = it.Open?.Invoke();
            if (next != null) { Open(next); return; }
            if (menu.Rebuild != null && it.OnAdjust == null && it.OnPick != null) { int sel = menu.Selected; Open(menu.Rebuild()); menu.Selected = Mathf.Clamp(sel, 0, menu.Items.Count - 1); }
        }

        void Back()
        {
            var parent = menu.Parent?.Invoke();
            if (parent != null) { Sfx.Play("ui", .6f); Open(parent); }
        }

        int HoveredRow(Vector2 screen)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                if (!rows[i].text.gameObject.activeInHierarchy) continue;
                var rt = (RectTransform)rows[i].bg.transform;
                if (RectTransformUtility.RectangleContainsScreenPoint(rt, screen, null))
                {
                    int index = scroll + i;
                    return index < menu.Items.Count ? index : -1;
                }
            }
            return -1;
        }

        void Draw()
        {
            title.text = menu.Title;
            if (menu.Selected < scroll) scroll = menu.Selected;
            if (menu.Selected >= scroll + Rows) scroll = menu.Selected - Rows + 1;
            scroll = Mathf.Clamp(scroll, 0, Mathf.Max(0, menu.Items.Count - Rows));
            for (int i = 0; i < Rows; i++)
            {
                int index = scroll + i;
                var (t, bg) = rows[i];
                bool on = index < menu.Items.Count;
                t.gameObject.SetActive(on);
                bg.color = Color.clear;
                if (!on) continue;
                var it = menu.Items[index];
                if (it.Header) { t.text = $"<color=#e8c37a><b>{it.Label}</b></color>"; continue; }
                bool sel = index == menu.Selected;
                bool blocked = it.Blocked?.Invoke() != null;
                string value = it.Right == null ? "" : $"<color=#cbd5e1>{it.Right}</color>";
                if (typingDone != null && sel) value = $"<color=#fde68a>{(typingSecret ? new string('•', typing.Length) : typing)}_</color>";
                t.text = $"<color={(blocked ? "#8a8f99" : sel ? "#fff3c4" : "#ffffff")}>{(sel ? "► " : "   ")}{it.Label}</color>   {value}";
                if (sel) bg.color = new Color(.95f, .75f, .25f, .18f);
            }
            var cur = menu.Current;
            detail.text = typingDone != null ? "Type, then Enter. Esc cancels." : cur == null ? "" : cur.Blocked?.Invoke() is { } why ? $"<color=#fca5a5>{why}</color>" : cur.Detail ?? "";
            if (Rebinder.Listening != null) detail.text = $"<b>Press the new key or button for {Rebinder.Listening}</b> (Esc cancels)";
            help.text = "Mouse, keyboard (arrows/WASD, Enter, Esc) or controller (stick, [A] pick, [B] back)." + (cur?.OnAdjust != null ? "  Left/right changes the value." : "");
        }

        // ------------------------------------------------------------------ typing
        void Type(string start, Action<string> done, bool secret = false)
        {
            if (Keyboard.current == null) { Say("Typing needs a keyboard."); return; }
            typing = start ?? "";
            typingDone = done;
            typingSecret = secret;
        }

        void OnText(char ch)
        {
            if (typingDone == null) return;
            if (ch == '\b') { if (typing.Length > 0) typing = typing.Substring(0, typing.Length - 1); return; }
            if (ch == '\r' || ch == '\n' || ch == 27 || char.IsControl(ch)) return;
            if (typing.Length < 64) typing += ch;
        }

        void FinishTyping(bool ok)
        {
            var done = typingDone;
            typingDone = null;
            repeatT = .3f;
            if (ok) done?.Invoke(typing);
        }
    }

    /// <summary>The living picture outside the keep's window while the main menu is up.</summary>
    public class MenuScenery : MonoBehaviour
    {
        Transform ball;
        readonly List<Transform> players = new();
        readonly List<(Transform t, float r, float speed, float phase, Vector3 centre)> circlers = new();
        float t;

        public static GameObject Build(Transform parent)
        {
            var root = new GameObject("Menu Scenery");
            root.transform.SetParent(parent, false);
            var s = root.AddComponent<MenuScenery>();
            // Beach volleyball on the dry sand
            var net = ModelFactory.Spawn("VolleyNet", root.transform, new Vector3(-6, 0, 9));
            net.root.transform.rotation = Quaternion.Euler(0, 90, 0);
            Vector3[] spots = { new(-9, 0, 7.5f), new(-9, 0, 10.5f), new(-3, 0, 7.5f), new(-3, 0, 10.5f) };
            for (int i = 0; i < 4; i++)
            {
                var mi = ModelFactory.Spawn("Sailor", root.transform, spots[i]);
                ModelFactory.Recolor(mi, "coat", Player.Colors[i]);
                mi.root.transform.rotation = Quaternion.Euler(0, i < 2 ? 90 : -90, 0);
                s.players.Add(mi.root.transform);
            }
            s.ball = ModelFactory.Spawn("Volleyball", root.transform, new Vector3(-6, 3, 9)).root.transform;
            // Shark fins circling offshore, gulls overhead
            for (int i = 0; i < 2; i++)
            {
                var shark = ModelFactory.Spawn("Shark", root.transform, Vector3.zero);
                s.circlers.Add((shark.root.transform, 6 + i * 3, .25f + i * .08f, i * 2.5f, new Vector3(-10 + i * 18, Arena.WaterLevel - .62f, Arena.ShoreZ + 14 + i * 8)));
            }
            for (int i = 0; i < 3; i++)
            {
                var gull = ModelFactory.Spawn("Gull", root.transform, Vector3.zero);
                s.circlers.Add((gull.root.transform, 6 + i * 2, .5f + i * .1f, i * 2.1f, new Vector3(4 + i * 3, 9 + i, 14 + i * 4)));
            }
            return root;
        }

        // Hits alternate between the teams (players 0-1 on one side of the net, 2-3 on the other).
        Transform Hitter(int n) => n % 2 == 0 ? players[(n / 2) % 2] : players[2 + (n / 2) % 2];

        void Update()
        {
            t += Time.deltaTime;
            // The ball goes back and forth over the net between the two teams
            const float rally = 1.6f;
            int hit = Mathf.FloorToInt(t / rally);
            float k = t / rally - hit;
            var from = Hitter(hit).position + Vector3.up * 2.2f;
            var to = Hitter(hit + 1).position + Vector3.up * 2.2f;
            ball.position = Vector3.Lerp(from, to, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 3.2f;
            ball.rotation = Quaternion.Euler(t * 300, t * 120, 0);
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                float jump = Mathf.Max(0, Mathf.Sin((t + i * .7f) * 3.4f)) * .25f;
                p.position = new Vector3(p.position.x, jump, p.position.z);
            }
            foreach (var (tr, r, speed, phase, centre) in circlers)
            {
                float a = t * speed + phase;
                tr.position = centre + new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a * 2) * .15f, Mathf.Sin(a) * r);
                tr.rotation = Quaternion.LookRotation(new Vector3(-Mathf.Sin(a), 0, Mathf.Cos(a)));
            }
        }
    }
}

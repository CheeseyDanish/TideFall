// TideWall 3D — a player: movement, looking/aiming (first person or top-down), guns, melee, interacting,
// carrying/throwing, health, being downed and revived, food and status effects, and the roguelike progression:
// XP and levels, stat points (spent in the Stats menu, respec for sand dollars), and weapon mods every few levels.
// Menus never stop you walking: they're driven by the d-pad / arrow keys. Guns stay holstered for the run's first
// waves (melee only). The body is jointed (legs, arms, head) and animated for walking, aiming, swinging and carrying.
// Build mode places defences (Structure) where you aim.
//
// One class, four kinds (see PlayerKind):
//   Local      offline, or the host's own players: everything is simulated here, input from PlayerControls.
//   Remote     on the host, an online player: simulated here from the input commands their machine sends.
//   LocalProxy on a client, that client's own player: predicts its movement locally (responsive) and is corrected
//              by the host; everything else (health, ammo, menus, statuses) mirrors the host.
//   Proxy      on a client, everyone else: follows the host's snapshots.
//
// The class is split by topic into Player.*.cs files (Movement, Aim, Combat, Carrying, Health, Menus, Animation,
// Building, Progression, Online). This file has the fields, setup and the frame loop.
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace TideWall
{
    public enum PlayerKind { Local, Remote, LocalProxy, Proxy }

    [RequireComponent(typeof(CharacterController))]
    public partial class Player : MonoBehaviour
    {
        public const float MaxCarry = 3;
        public const int BodyLayer = 8, ViewLayer = 12;   // + player index: hides your own body in first person
        public static readonly Color[] Colors = { new(.9f, .22f, .2f), new(.2f, .5f, .95f), new(.25f, .8f, .35f), new(.98f, .78f, .15f) };
        static readonly string[] Names = { "Red", "Blue", "Green", "Gold" };

        public PlayerKind Kind { get; private set; }
        public bool Simulated => Kind is PlayerKind.Local or PlayerKind.Remote;
        public bool HasView => Kind is PlayerKind.Local or PlayerKind.LocalProxy;
        public int Index { get; private set; }
        public string Name => Names[Index];
        /// <summary>Online name (from Settings) shown under the colour name; empty offline.</summary>
        public string Nick { get; set; } = "";
        public Color Color => Colors[Index];
        public PlayerControls Controls { get; private set; }
        public RemoteInput Remote { get; private set; }
        /// <summary>Host: the connection this Remote player belongs to.</summary>
        public int Owner { get; private set; } = -1;
        public PlayerCamera Cam { get; private set; }
        public PlayerHud Hud { get; private set; }
        public readonly StatusEffects Statuses = new();
        public readonly List<Carryable> Carrying = new();
        public int CarryCount => Simulated ? Carrying.Count : netCarryCount;
        public float CarryWeight
        {
            get { if (!Simulated) return netCarryWeight; float w = 0; foreach (var c in Carrying) if (c) w += c.weight; return w; }
        }

        public float Hp { get; private set; }
        public float MaxHp => Simulated ? (db.playerMaxHp + gm.Team.PlayerHpBonus + StatBonus(StatKind.MaxHp)) * gm.Cards.PlayerMaxHp : netMaxHp;
        public bool IsDowned { get; private set; }
        public bool IsOut { get; private set; }          // bled out: back at the next phase
        public bool IsTargetable => Simulated && !IsDowned && !IsOut && gm.State == GameState.Playing;
        public float BleedLeft { get; private set; }
        public float ReviveProgress { get; private set; }
        public bool Ready { get; set; }
        public float Wet { get; private set; }
        /// <summary>Shop, hotdog stand, pause and settings menus. On a client: the host's menu (shop) for this player.</summary>
        public Menu OpenedMenu { get; private set; }
        /// <summary>Client only: the pause/settings menu, which never goes over the network.</summary>
        public Menu LocalMenu { get; private set; }
        public Menu ShownMenu => LocalMenu ?? OpenedMenu;
        public GunDef Gun { get; private set; }
        public MeleeDef Melee { get; private set; }
        public int Ammo => Simulated ? (ammo.TryGetValue(Gun.id, out var a) ? a : 0) : netAmmo;
        public int MagSize => Simulated ? Mathf.RoundToInt(EffGun.magazine * gm.Team.Magazine) : netMag;
        public float ReloadProgress => Simulated ? (reload != null ? reload.Progress(Ammo, MagSize) : -1) : netReload;
        /// <summary>The reload minigame to draw at the reticle: the running one, or a finished one still flashing its result.</summary>
        public ReloadGame ReloadView => Simulated ? reload ?? (lastReload != null && lastReload.WordT > 0 ? lastReload : null) : netReloadGame;
        /// <summary>Damage bonus from a Perfect reload, until the magazine runs dry.</summary>
        public float PerfectBonus => Simulated ? perfectBonus : netPerfect;
        /// <summary>Kills chained within GameDatabase.comboWindow of each other, and the time left to extend the chain.</summary>
        public int Combo => Simulated ? combo : netCombo;
        public float ComboLeft => Simulated ? comboT : netComboT;
        public int BestCombo { get; private set; }
        /// <summary>The equipped weapons with this player's mods applied (what actually fires and swings).</summary>
        public GunDef EffGun { get; private set; }
        public MeleeDef EffMelee { get; private set; }

        // Progression
        public int Level { get; private set; } = 1;
        public int Xp { get; private set; }
        public int XpNext => Simulated ? db.XpForLevel(Level) : netXpNext;
        public int StatPoints { get; private set; }
        public int PendingMods { get; private set; }
        public int Respecs { get; private set; }
        /// <summary>Points put into each stat (stat id → points).</summary>
        public readonly Dictionary<string, int> Allocated = new();
        /// <summary>Mods on each weapon (weapon id → mod ids).</summary>
        public readonly Dictionary<string, List<string>> Mods = new();
        /// <summary>The mods on offer right now (null if none is waiting).</summary>
        public List<ModDef> ModOffer { get; private set; }
        public float FortuneMultiplier => 1 + StatBonus(StatKind.Fortune);
        /// <summary>Movement multiplier from stats and tidal cards (a client gets it from the host).</summary>
        public float SpeedBonus => Simulated ? (1 + StatBonus(StatKind.MoveSpeed)) * gm.Cards.PlayerSpeed : netSpeedBonus;
        public float SwingT => swingT;
        /// <summary>0 → 1 through the current melee swing (wind-up, strike, recovery); -1 when not swinging.</summary>
        public float SwingProgress => swingT > 0 && Melee != null ? 1 - swingT / Mathf.Max(.01f, Melee.SwingTime) : -1;
        /// <summary>Guns are holstered for the run's first waves: Fire swings the melee weapon instead.</summary>
        public bool GunsLocked => gm.Waves.RunWave < db.gunsUnlockWave;
        /// <summary>Build mode: the defence being placed (null = not building).</summary>
        public StructureDef Building { get; private set; }
        public float BuildYaw { get; private set; }
        public bool BuildValid { get; private set; }
        /// <summary>1 right after a shot, fading (the crosshair spreads with it).</summary>
        public float FireHeat { get; private set; }
        public Vector3 AimPoint { get; private set; }
        public Vector3 AimDir { get; private set; } = Vector3.forward;
        public Vector2 AimScreen { get; private set; }
        public Interactable Focus { get; private set; }
        public int Kills, Severs;

        /// <summary>Looking direction (degrees). First person looks along it; top-down keeps it for switching back.</summary>
        public float Yaw { get; private set; }
        public float Pitch { get; private set; }
        /// <summary>The view this player chose (Settings default, switched with the View button).</summary>
        public bool PrefersFirstPerson { get; private set; } = true;
        /// <summary>The view actually used: the ??? sauce's "wrong view" swaps it for a while.</summary>
        public bool FirstPerson => PrefersFirstPerson ^ Statuses.Has(StatusType.SwapView);
        public bool UsesMouse => Controls != null && Controls.UsesMouse;
        public string DeviceName => Controls != null ? Controls.DeviceName : "Online";
        public bool Connected => Controls != null ? Controls.Connected : Remote == null || Remote.Connected;
        public Vector3 EyePos => transform.position + Vector3.up * EyeHeight;
        public float EyeHeight => IsDowned || IsOut ? .45f : 1.62f * (Statuses.Has(StatusType.Giant) ? 1.45f : Statuses.Has(StatusType.Tiny) ? .6f : 1);
        public Quaternion LookRotation => Quaternion.Euler(Pitch, Yaw, 0);

        GameManager gm; GameDatabase db;
        CharacterController cc;
        Model model, gunModel, meleeModel;
        Transform visual;
        LineRenderer reticle;
        TextMeshPro nameTag;
        Light lantern;
        readonly HashSet<string> ownedGuns = new(), ownedMelee = new();
        readonly Dictionary<string, int> ammo = new();
        float strikeT, lockToastT, stepPhase;
        Transform legL, legR, armL, armR, head;
        GameObject ghost;
        string ghostId;
        Vector3 lastVisualPos;
        float fireCd, meleeCd, swingT, dashT, dashCd, fireBuffer, yVel, menuRepeat, hurtFlash, dripAcc;
        Vector3 dashDir, push;
        Enemy grabbedBy;
        Vector2 lastNav;
        // Mirrored from the host (proxies)
        float netMaxHp = 100, netReload = -1, netCarryWeight, netSpeedBonus = 1, netPerfect, netComboT;
        int netCombo;
        ReloadGame reload, lastReload, netReloadGame;
        float perfectBonus, comboT;
        int combo;
        bool reloadHeld;
        int netAmmo, netMag = 1, netCarryCount, netXpNext = 60;
        bool statsWanted;   // client: the pause menu asked the host to open the Stats menu
        // Online movement (client): numbered commands the host hasn't confirmed yet, replayed after each correction
        readonly List<NetCommand> unconfirmed = new();
        uint commandSeq;
        AimSample lastAim;
        /// <summary>Client: how far the camera still trails a correction from the host (eases back to zero).</summary>
        public Vector3 RenderOffset { get; private set; }
        Vector3 netPos; float netYaw; bool netFirstPerson;
        public string NetPrompt { get; private set; }

        // ------------------------------------------------------------------ setup
        public static Player Create(int index, PlayerControls controls, Vector3 spawn) => Make(index, PlayerKind.Local, controls, null, -1, spawn);

        /// <summary>Host: an online player, driven by packets from connection `owner`.</summary>
        public static Player CreateRemote(int index, int owner, string nick, Vector3 spawn)
        {
            var p = Make(index, PlayerKind.Remote, null, new RemoteInput(), owner, spawn);
            p.Nick = nick;
            return p;
        }

        /// <summary>Client: a mirror of a host player. With controls it's this machine's own player.</summary>
        public static Player CreateProxy(int index, PlayerControls controls, string nick, Vector3 pos)
        {
            var p = Make(index, controls != null ? PlayerKind.LocalProxy : PlayerKind.Proxy, controls, null, -1, pos);
            p.Nick = nick;
            return p;
        }

        static Player Make(int index, PlayerKind kind, PlayerControls controls, RemoteInput remote, int owner, Vector3 spawn)
        {
            var go = new GameObject("Player " + (index + 1));
            go.transform.position = spawn;
            var cc = go.AddComponent<CharacterController>();
            cc.height = 1.8f; cc.radius = .4f; cc.center = new Vector3(0, .9f, 0); cc.slopeLimit = 50; cc.stepOffset = .4f;
            var p = go.AddComponent<Player>();
            p.Kind = kind; p.Controls = controls; p.Remote = remote; p.Owner = owner;
            p.Setup(index);
            return p;
        }

        void Setup(int index)
        {
            gm = GameManager.I; db = gm.DB;
            Index = index;
            cc = GetComponent<CharacterController>();
            netPos = transform.position;
            Yaw = 0; Pitch = 8;
            PrefersFirstPerson = Settings.Current.startFirstPerson;
            visual = new GameObject("Visual").transform;
            visual.SetParent(transform, false);
            model = ModelFactory.Spawn("Sailor", visual, Vector3.zero);
            ModelFactory.Recolor(model, "coat", Color);
            legL = Part("Part_LegL"); legR = Part("Part_LegR"); armL = Part("Part_ArmL"); armR = Part("Part_ArmR"); head = Part("Part_Head");
            GiveGun(db.startGun);
            GiveMelee(db.startMelee);
            if (Simulated) RestoreLoadout();
            Hp = MaxHp;
            // Each player's aim reticle (top-down): a ring in their colour on the ground or on the limb they're aiming at.
            reticle = new GameObject("Reticle").AddComponent<LineRenderer>();
            reticle.loop = true; reticle.positionCount = 20; reticle.widthMultiplier = .06f; reticle.useWorldSpace = true;
            reticle.material = Mats.ParticleAdditive;
            reticle.startColor = reticle.endColor = Color;
            reticle.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lantern = new GameObject("Lantern").AddComponent<Light>();
            lantern.transform.SetParent(transform, false);
            lantern.transform.localPosition = new Vector3(0, 2.4f, 0);
            lantern.type = LightType.Point; lantern.range = 9; lantern.color = Color.Lerp(Color, new Color(1, .85f, .6f), .6f); lantern.shadows = LightShadows.None;
            gm.World.lanterns.Add(lantern);
            // A name tag, so teammates know who's who in first person
            nameTag = Ui.Label(visual, "NameTag", new Vector3(0, 2.35f, 0), .28f, Color.Lerp(Color, Color.white, .35f));
            Billboard.Add(nameTag.gameObject);
            SetLayer(visual, BodyLayer + Index);
            if (HasView)
            {
                Cam = PlayerCamera.Create(this);
                Hud = PlayerHud.Create(this);
            }
        }

        static void SetLayer(Transform t, int layer)
        {
            foreach (var c in t.GetComponentsInChildren<Transform>(true)) c.gameObject.layer = layer;
        }

        /// <summary>Loading a save: this player slot gets back the weapons it owned.</summary>
        void RestoreLoadout()
        {
            var save = gm.Loaded?.players.Find(s => s.index == Index);
            if (save == null) return;
            foreach (var g in save.guns) if (db.Gun(g) != null) { ownedGuns.Add(g); ammo[g] = Mathf.RoundToInt(db.Gun(g).magazine * gm.Team.Magazine); }
            foreach (var m in save.melees) if (db.Melee(m) != null) ownedMelee.Add(m);
            if (db.Gun(save.gun) != null) GiveGun(save.gun);
            if (db.Melee(save.melee) != null) GiveMelee(save.melee);
            Kills = save.kills; Severs = save.severs;
            Level = Mathf.Max(1, save.level); Xp = save.xp; StatPoints = save.statPoints; PendingMods = save.pendingMods; Respecs = save.respecs;
            foreach (var st in save.stats) if (db.Stat(st.id) != null) Allocated[st.id] = st.level;
            foreach (var wm in save.mods)
            {
                var parts = wm.Split('/');
                if (parts.Length == 2 && db.Mod(parts[1]) != null) ModsFor(parts[0]).Add(parts[1]);
            }
            if (PendingMods > 0) RollModOffer();
            RebuildWeapons();
        }

        public bool OwnsGun(string id) => ownedGuns.Contains(id);
        public bool OwnsMelee(string id) => ownedMelee.Contains(id);

        public void GiveGun(string id)
        {
            var g = db.Gun(id); if (g == null) return;
            ownedGuns.Add(id);
            Gun = g;
            RebuildWeapons();
            if (!ammo.ContainsKey(id)) ammo[id] = MagSize;
            reload = null; perfectBonus = 0;
            if (gunModel != null) Destroy(gunModel.root);
            gunModel = ModelFactory.Spawn(g.model, Socket("Gun"), Vector3.zero);
            SetLayer(gunModel.root.transform, BodyLayer + Index);
            if (Cam != null) Cam.View.Rebuild();
        }

        public void GiveMelee(string id)
        {
            var m = db.Melee(id); if (m == null) return;
            ownedMelee.Add(id);
            Melee = m;
            RebuildWeapons();
            if (meleeModel != null) Destroy(meleeModel.root);
            meleeModel = ModelFactory.Spawn(m.model, Socket("Melee"), Vector3.zero);
            meleeModel.root.transform.localRotation = Quaternion.Euler(-60, 0, 0);   // held up, ready to swing
            SetLayer(meleeModel.root.transform, BodyLayer + Index);
            if (Cam != null) Cam.View.Rebuild();
        }

        Transform Socket(string name) => model.Sockets.TryGetValue(name, out var s) ? s : visual;

        Transform Part(string name)
        {
            foreach (var t in model.root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }

        /// <summary>Max HP changed (upgrades, Vigor, tidal cards): never above the new maximum.</summary>
        public void RefreshMaxHp() { if (!IsDowned && !IsOut && Hp > MaxHp) Hp = MaxHp; }

        // ------------------------------------------------------------------ input helpers
        Vector2 MoveInput()
        {
            var mv = Controls != null ? Controls.Move.ReadValue<Vector2>() : Remote?.Move ?? Vector2.zero;
            return Statuses.Has(StatusType.ReverseControls) ? -mv : mv;
        }

        bool Held(Btn b) => Controls != null ? Controls.Held(b) : Remote != null && Remote.Held(b);
        Vector2 NavInput() => Controls != null ? Controls.Nav() : Remote?.Nav ?? Vector2.zero;
        bool Pressed(Btn b) => Controls != null ? Controls.Pressed(b) : Remote != null && Remote.Pressed(b);
        public string Glyph(Btn b) => Controls != null ? Controls.Glyph(b) : "";

        // ------------------------------------------------------------------ frame
        void Update()
        {
            float dt = Time.deltaTime;
            Remote?.BeginFrame();
            switch (Kind)
            {
                case PlayerKind.Local:
                case PlayerKind.Remote: SimUpdate(dt); break;
                case PlayerKind.LocalProxy: OwnProxyUpdate(dt); break;
                default: ProxyUpdate(dt); break;
            }
            Remote?.EndFrame();
            UpdateNameTag();
        }

        void SimUpdate(float dt)
        {
            Statuses.Tick(dt);
            if (hurtFlash > 0) hurtFlash -= dt;
            if (Kind == PlayerKind.Local) LocalButtons();
            else PrefersFirstPerson = Remote.FirstPerson;
            if (Pressed(Btn.Stats) && !Rebinder.Busy) ToggleStats();
            float udt = Time.unscaledDeltaTime;
            if (gm.State == GameState.GameOver) { UpdateReticle(false); return; }
            if (dt <= 0) { if (OpenedMenu != null) UpdateMenu(udt); UpdateReticle(false); return; }   // paused (offline)

            UpdateWet(dt);
            if (IsOut) { visual.gameObject.SetActive(false); reticle.enabled = false; if (OpenedMenu != null) UpdateMenu(udt); return; }
            visual.gameObject.SetActive(true);

            if (IsDowned) { UpdateDowned(dt); if (OpenedMenu != null) UpdateMenu(udt); UpdateVisual(dt); return; }
            // Menus don't stop you: walk and look while one is open (it closes if you walk away from the stall)
            bool menu = OpenedMenu != null;
            if (menu)
            {
                UpdateMenu(udt);
                if (OpenedMenu != null && OpenedMenu.Anchor.HasValue && FlatDistance(OpenedMenu.Anchor.Value) > OpenedMenu.AnchorRange) CloseMenu();
            }
            if (Kind == PlayerKind.Local) { if (OpenedMenu == null || !OpenedMenu.System) TickLook(dt); ResolveAim(LocalAim()); }
            else ApplyRemoteLook();
            UpdateMovement(dt);
            if (menu) CombatTimers(dt);
            else if (Building != null) UpdateBuild(dt);
            else { UpdateCombat(dt); UpdateInteract(dt); }
            if (!menu && Building == null && Pressed(Btn.Build) && !Rebinder.Busy) OpenMenu(Menus.Build(this));
            UpdateVisual(dt);
        }

        /// <summary>Pause, view switching and the game-over choices (local players).</summary>
        void LocalButtons()
        {
            if (Rebinder.Busy) return;
            if (Pressed(Btn.Pause))
            {
                if (OpenedMenu != null) CloseMenu();
                else if (gm.State == GameState.Playing) OpenMenu(Menus.Pause(this));
            }
            if (OpenedMenu == null && Pressed(Btn.View)) SwitchView();
            if (gm.State == GameState.GameOver)
            {
                if (Pressed(Btn.Interact)) gm.NewRun();
                else if (Pressed(Btn.Back)) gm.QuitToMenu();
            }
        }

        /// <summary>Point the view (screenshot tour, tests).</summary>
        public void SetLook(float yaw, float pitch) { Yaw = yaw; Pitch = Mathf.Clamp(pitch, -80, 80); }

        public void SwitchView()
        {
            PrefersFirstPerson = !PrefersFirstPerson;
            Toast(PrefersFirstPerson ? "First person" : "Top-down view", new Color(.8f, .9f, 1f));
            Sfx.Play("ui");
        }

        float FlatDistance(Vector3 to) { var d = to - transform.position; d.y = 0; return d.magnitude; }

        void UpdateWet(float dt)
        {
            if (Arena.InWater(transform.position) && Arena.Depth(transform.position) > .3f) Wet = 1;
            else Wet = Mathf.Max(0, Wet - db.wetDrainPerSecond * 1.5f * dt);
            Drip(dt);
        }

        void Drip(float dt)
        {
            dripAcc += Wet * 10 * dt;
            while (dripAcc >= 1) { dripAcc -= 1; Fx.Drip(transform.position + new Vector3(Random.Range(-.3f, .3f), Random.Range(.4f, 1.6f), Random.Range(-.3f, .3f))); }
        }

    }
}

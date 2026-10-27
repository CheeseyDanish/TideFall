// TideWall 3D — a player: movement, looking/aiming (first person or top-down), guns, melee, interacting,
// carrying/throwing, health, being downed and revived, food and status effects, and the roguelike progression:
// XP and levels, stat points (spent in the Stats menu, respec for sand dollars), and weapon mods every few levels.
// Menus never stop you walking: they're driven by the d-pad / arrow keys. Guns stay holstered for the run's first
// waves (melee only). The body is jointed (legs, arms, head) and animated for walking, aiming, swinging and carrying.
// Build mode places defences (Structure) where you aim.
//
// One class, four kinds (see PlayerKind):
//   Local      offline, or the host's own players: everything is simulated here, input from PlayerControls.
//   Remote     on the host, an online player: simulated here, input (and position) from their machine.
//   LocalProxy on a client, that client's own player: moves and looks locally (responsive), everything else
//              (health, ammo, menus, statuses) mirrors the host.
//   Proxy      on a client, everyone else: follows the host's snapshots.
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace TideWall
{
    public enum PlayerKind { Local, Remote, LocalProxy, Proxy }

    [RequireComponent(typeof(CharacterController))]
    public class Player : MonoBehaviour
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
        ModelInstance model, gunModel, meleeModel;
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

        Transform Socket(string name) => model.sockets.TryGetValue(name, out var s) ? s : visual;

        Transform Part(string name)
        {
            foreach (var t in model.root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }

        /// <summary>Max HP changed (upgrades, Vigor, tidal cards): never above the new maximum.</summary>
        public void RefreshMaxHp() { if (!IsDowned && !IsOut && Hp > MaxHp) Hp = MaxHp; }

        // ------------------------------------------------------------------ progression
        /// <summary>The total bonus from points in stats of this kind (+HP for MaxHp, a fraction for the rest).</summary>
        public float StatBonus(StatKind kind)
        {
            float b = 0;
            foreach (var kv in Allocated)
                if (db.Stat(kv.Key) is { } st && st.kind == kind) b += kv.Value * st.perPoint;
            return b;
        }

        public int Points(string statId) => Allocated.TryGetValue(statId, out var n) ? n : 0;

        List<string> ModsFor(string weaponId)
        {
            if (!Mods.TryGetValue(weaponId, out var list)) Mods[weaponId] = list = new List<string>();
            return list;
        }

        /// <summary>Copy the equipped weapons and apply this player's mods to the copies.</summary>
        void RebuildWeapons()
        {
            if (Gun != null)
            {
                var g = JsonUtility.FromJson<GunDef>(JsonUtility.ToJson(Gun));
                foreach (var id in ModsFor(Gun.id))
                {
                    var m = db.Mod(id); if (m == null) continue;
                    g.damage *= m.damage; g.fireRate *= m.fireRate; g.reload *= m.reload; g.range *= m.range;
                    g.magazine = Mathf.Max(1, Mathf.RoundToInt(g.magazine * m.magazine));
                    g.spreadDegrees += m.spread; g.splash += m.splash; g.knockback += m.knockback;
                    g.pierce += m.pierce; g.pellets += m.pellets; g.lifesteal += m.lifesteal; g.limbBonus += m.limbBonus;
                    if (m.setElement) g.element = m.element;
                }
                EffGun = g;
            }
            if (Melee != null)
            {
                var w = JsonUtility.FromJson<MeleeDef>(JsonUtility.ToJson(Melee));
                foreach (var id in ModsFor(Melee.id))
                {
                    var m = db.Mod(id); if (m == null) continue;
                    w.damage *= m.damage; w.cooldown *= m.cooldown; w.range *= m.range; w.arcDegrees = Mathf.Min(300, w.arcDegrees * m.arc);
                    w.knockback += m.knockback; w.stun += m.stun; w.lifesteal += m.lifesteal; w.limbBonus += m.limbBonus;
                    if (m.setElement) w.element = m.element;
                }
                EffMelee = w;
            }
        }

        /// <summary>XP from a kill (cards and Fortune scale it). Level-ups give stat points; every few levels, a mod.</summary>
        public void GainXp(float amount)
        {
            if (!Simulated || amount <= 0) return;
            Xp += Mathf.Max(1, Mathf.RoundToInt(amount * gm.Cards.Xp * (1 + StatBonus(StatKind.Fortune) * .5f)));
            while (Xp >= db.XpForLevel(Level))
            {
                Xp -= db.XpForLevel(Level);
                Level++;
                StatPoints += db.statPointsPerLevel;
                Heal(MaxHp * .15f);
                Fx.Sparks(transform.position + Vector3.up * 1.2f, new Color(1, .9f, .4f), 14);
                Sfx.Play("jackpot", .5f, .3f);
                bool mod = db.modEveryLevels > 0 && Level % db.modEveryLevels == 0;
                if (mod) { PendingMods++; if (ModOffer == null) RollModOffer(); }
                Toast(mod ? $"LEVEL {Level}! A weapon mod is ready: {Glyph(Btn.Stats)} Stats"
                          : $"LEVEL {Level}! +{db.statPointsPerLevel} stat point: {Glyph(Btn.Stats)} Stats", new Color(1, .9f, .4f));
            }
        }

        public string WhyCantSpend(string statId)
        {
            var st = db.Stat(statId);
            if (st == null) return "?";
            if (StatPoints <= 0) return "No stat points (level up to earn more)";
            if (Points(statId) >= st.maxPoints) return "Maxed";
            return null;
        }

        public void SpendPoint(string statId)
        {
            if (WhyCantSpend(statId) != null) return;
            Allocated[statId] = Points(statId) + 1;
            StatPoints--;
            if (db.Stat(statId).kind == StatKind.MaxHp) Heal(db.Stat(statId).perPoint);
            RefreshMaxHp();
            Sfx.Play("bell", .4f, .1f);
        }

        public int SpentPoints { get { int n = 0; foreach (var v in Allocated.Values) n += v; return n; } }
        public int RespecPrice => db.respecPerLevel * Level * (1 + Respecs);

        /// <summary>Refund every stat point (for sand dollars from the team purse).</summary>
        public bool Respec()
        {
            if (SpentPoints == 0 || !gm.TrySpend(RespecPrice)) return false;
            StatPoints += SpentPoints;
            Allocated.Clear();
            Respecs++;
            RefreshMaxHp();
            Sfx.Play("swing");
            Toast("Stats refunded: spend your points again", new Color(.8f, .9f, 1));
            return true;
        }

        /// <summary>Offer a few mods for the weapons in hand that they don't have yet.</summary>
        void RollModOffer()
        {
            var pool = db.mods.FindAll(m => m.gun ? !ModsFor(Gun.id).Contains(m.id) : !ModsFor(Melee.id).Contains(m.id));
            ModOffer = new List<ModDef>();
            while (ModOffer.Count < db.modChoices && pool.Count > 0)
            {
                var m = pool[Random.Range(0, pool.Count)];
                pool.Remove(m);
                ModOffer.Add(m);
            }
            if (ModOffer.Count == 0) ModOffer = null;
        }

        public void TakeMod(ModDef m)
        {
            if (ModOffer == null || !ModOffer.Contains(m)) return;
            ModsFor(m.gun ? Gun.id : Melee.id).Add(m.id);
            PendingMods = Mathf.Max(0, PendingMods - 1);
            ModOffer = null;
            if (PendingMods > 0) RollModOffer();
            RebuildWeapons();
            Sfx.Play("reload");
            Toast($"{m.displayName} fitted to your {(m.gun ? Gun.displayName : Melee.displayName)}", new Color(.7f, 1, .8f));
        }

        public IEnumerable<string> ModsOn(string weaponId) => ModsFor(weaponId);

        /// <summary>Stats button: the Stats menu (or the tidal card vote while one is running).</summary>
        void ToggleStats()
        {
            if (OpenedMenu != null && (OpenedMenu.Title == Menus.StatsTitle || OpenedMenu.Title == TidalCards.MenuTag)) { CloseMenu(); return; }
            if (OpenedMenu != null && OpenedMenu.System) return;
            OpenMenu(gm.Cards.Draft != null && !gm.Cards.Votes.ContainsKey(Index) ? Menus.CardVote(this) : Menus.Stats(this));
        }

        /// <summary>Client: the pause menu wants the host's Stats menu.</summary>
        public void RequestStats() { statsWanted = true; LocalMenu = null; }


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

        // ------------------------------------------------------------------ looking & aiming
        /// <summary>First person: mouse or right stick turns the view.</summary>
        void TickLook(float dt)
        {
            if (!FirstPerson) { if (AimDir.sqrMagnitude > .01f) Yaw = Quaternion.LookRotation(AimDir).eulerAngles.y; return; }
            var s = Settings.Current;
            Vector2 d;
            if (Controls.UsesMouse) d = Controls.Look.ReadValue<Vector2>() * .09f * s.mouseSensitivity;
            else
            {
                var st = Controls.Aim.ReadValue<Vector2>();
                float m = st.magnitude;
                st = m < .12f ? Vector2.zero : st / m * Mathf.Pow((m - .12f) / .88f, 1.6f);   // dead zone + response curve
                float slow = s.aimAssist && EnemyUnderCrosshair() ? .55f : 1;               // aim assist: slow down over targets
                d = new Vector2(st.x * 190, st.y * 120) * s.stickSensitivity * slow * dt;
            }
            if (Statuses.Has(StatusType.ReverseControls)) d = -d;
            Yaw = Mathf.Repeat(Yaw + d.x, 360);
            Pitch = Mathf.Clamp(Pitch - d.y * (s.invertY ? -1 : 1), -80, 80);
        }

        bool EnemyUnderCrosshair()
        {
            var ray = new Ray(EyePos, LookRotation * Vector3.forward);
            return Physics.Raycast(ray, out var h, 60, ~0, QueryTriggerInteraction.Collide) && h.collider.GetComponentInParent<Enemy>() is { Dead: false };
        }

        /// <summary>This machine's aim: the crosshair (first person), the cursor, or the stick's reticle (top-down).</summary>
        AimSample LocalAim()
        {
            var cam = Cam.Camera;
            var a = new AimSample { assist = !UsesMouse && Settings.Current.aimAssist };
            if (FirstPerson)
            {
                AimScreen = cam.pixelRect.center;
                a.ray = new Ray(EyePos, LookRotation * Vector3.forward);
                return a;
            }
            if (UsesMouse)
            {
                AimScreen = Controls.Point.ReadValue<Vector2>();
                a.ray = cam.ScreenPointToRay(AimScreen);
                return a;
            }
            var stick = Controls.Aim.ReadValue<Vector2>();
            if (Statuses.Has(StatusType.ReverseControls)) stick = -stick;
            float mag = Mathf.Clamp01(stick.magnitude);
            var dir = AimDir;
            if (mag > .2f) dir = new Vector3(stick.x, 0, stick.y).normalized;
            else
            {
                var mv = MoveInput();
                if (mv.magnitude > .3f) dir = new Vector3(mv.x, 0, mv.y).normalized;
            }
            float dist = 2.5f + 9 * (mag > .2f ? mag : .6f);
            AimScreen = cam.WorldToScreenPoint(transform.position + dir * dist + Vector3.up * .8f);
            a.ray = cam.ScreenPointToRay(AimScreen);
            a.dirHint = dir;
            return a;
        }

        /// <summary>Host: an online player's look and aim, as their machine sent it.</summary>
        void ApplyRemoteLook()
        {
            Yaw = Remote.Yaw; Pitch = Remote.Pitch;
            ResolveAim(Remote.Aim);
        }

        /// <summary>What's along the aim ray? That's what the shot goes for, limb and all.</summary>
        public void ResolveAim(AimSample a)
        {
            if (a.dirHint.sqrMagnitude > .01f) AimDir = a.dirHint;
            var hits = Physics.RaycastAll(a.ray, 200, ~0, QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, (x, y) => x.distance.CompareTo(y.distance));
            bool found = false;
            foreach (var h in hits)
            {
                if (h.collider.GetComponentInParent<Player>() || h.collider.GetComponentInParent<Wall>()) continue;
                var e = h.collider.GetComponentInParent<Enemy>();
                if (e != null && !e.Dead) { AimPoint = h.point; found = true; break; }
                if (h.collider.GetComponentInParent<GasCan>() is { Holder: null }) { AimPoint = h.point; found = true; break; }
                if (h.collider.isTrigger || h.collider.GetComponentInParent<Corpse>()) continue;
                AimPoint = h.point + (FirstPerson ? Vector3.zero : Vector3.up * .5f); found = true; break;
            }
            if (!found) AimPoint = a.ray.GetPoint(FirstPerson ? 60 : 30);
            if (a.assist) { if (FirstPerson) AimAssistFirstPerson(a.ray); else AimAssistTopDown(); }
            if (FirstPerson) AimDir = Quaternion.Euler(0, Yaw, 0) * Vector3.forward;
            else if (a.dirHint.sqrMagnitude < .01f)
            {
                var d = AimPoint - transform.position; d.y = 0;
                if (d.sqrMagnitude > .04f) AimDir = d.normalized;
            }
            UpdateReticle(true);
        }

        /// <summary>Top-down gamepads have no cursor: if the stick points near an enemy, aim at its body.</summary>
        void AimAssistTopDown()
        {
            foreach (var e in GameManager.Enemies)
                if (e != null && !e.Dead && e.GetComponentInChildren<Collider>() is { } c && c.bounds.Contains(AimPoint)) return;
            Enemy best = null; float bestAngle = 9;
            foreach (var e in GameManager.Enemies)
            {
                if (e == null || e.Dead) continue;
                var to = e.transform.position - transform.position; to.y = 0;
                if (to.magnitude > 16) continue;
                float ang = Vector3.Angle(AimDir, to);
                if (ang < bestAngle) { bestAngle = ang; best = e; }
            }
            if (best != null) AimPoint = ModelFactory.Bounds(best.gameObject).center;
        }

        /// <summary>First-person gamepads: a gentle pull onto a body just next to the crosshair.</summary>
        void AimAssistFirstPerson(Ray ray)
        {
            foreach (var e in GameManager.Enemies)
                if (e != null && !e.Dead && Vector3.Distance(ModelFactory.Bounds(e.gameObject).ClosestPoint(AimPoint), AimPoint) < .05f) return;
            Enemy best = null; float bestAngle = 3.5f;
            foreach (var e in GameManager.Enemies)
            {
                if (e == null || e.Dead) continue;
                var c = ModelFactory.Bounds(e.gameObject).center - ray.origin;
                if (c.magnitude > 45) continue;
                float ang = Vector3.Angle(ray.direction, c);
                if (ang < bestAngle) { bestAngle = ang; best = e; }
            }
            if (best != null) AimPoint = ModelFactory.Bounds(best.gameObject).center;
        }

        void UpdateReticle(bool show)
        {
            reticle.enabled = show && !FirstPerson && !IsOut;
            if (!reticle.enabled) return;
            float r = .3f + Mathf.Sin(Time.time * 6) * .03f;
            for (int i = 0; i < 20; i++)
            {
                float a = i / 20f * Mathf.PI * 2;
                reticle.SetPosition(i, AimPoint + new Vector3(Mathf.Cos(a) * r, .02f, Mathf.Sin(a) * r));
            }
        }

        // ------------------------------------------------------------------ movement
        float SpeedMultiplier()
        {
            float m = 1;
            if (Statuses.Has(StatusType.Speed)) m *= 1.4f;
            if (Statuses.Has(StatusType.Slow)) m *= .6f;
            if (Statuses.Has(StatusType.Giant)) m *= .9f;
            if (Statuses.Has(StatusType.Tiny)) m *= 1.15f;
            if (Arena.InWater(transform.position)) m *= Mathf.Lerp(1, db.playerWaterSpeedMult, Mathf.Clamp01(Arena.Depth(transform.position) / .8f));
            m *= 1 - (1 - db.carrySpeedMult) * CarryWeight / MaxCarry;
            if (swingT > 0 && Melee.id == "anchor") m *= .4f;
            return m * SpeedBonus;
        }

        void UpdateMovement(float dt)
        {
            // Online players are moved by their commands as they arrive (RunRemoteCommands); here they only turn
            if (Kind == PlayerKind.Remote) { FaceAim(dt); return; }
            var raw = Controls != null ? Controls.Move.ReadValue<Vector2>() : Vector2.zero;
            var dir = StepMove(raw, Pressed(Btn.Dash), Yaw, FirstPerson, dt);
            if (FirstPerson || UsesMouse || (Controls != null && Controls.Aim.ReadValue<Vector2>().magnitude > .2f) || dir.sqrMagnitude > .01f) FaceAim(dt);
        }

        /// <summary>
        /// One step of movement from one frame of controls (raw stick/WASD). The same code moves local players, online
        /// players on the host (one call per command) and a client's prediction and replays, so they all agree.
        /// Returns the world direction moved in.
        /// </summary>
        Vector3 StepMove(Vector2 move, bool dash, float yaw, bool firstPerson, float dt)
        {
            if (IsOut || dt <= 0) return Vector3.zero;
            if (Statuses.Has(StatusType.ReverseControls)) move = -move;
            var dir = new Vector3(move.x, 0, move.y);
            if (firstPerson) dir = Quaternion.Euler(0, yaw, 0) * dir;
            if (dir.magnitude > 1) dir.Normalize();
            if (grabbedBy != null || Statuses.Has(StatusType.Grabbed)) dir = Vector3.zero;
            if (IsDowned) { Gravity(dt, dir); return dir; }   // crawl slowly toward safety
            dashCd -= dt;
            if (dash && dashCd <= 0 && dir.sqrMagnitude > .01f)
            {
                dashT = db.dashSeconds; dashCd = db.dashCooldown / (1 + (SpeedBonus - 1) * 2); dashDir = dir.normalized;
                if (Simulated) Fx.Smoke(transform.position, 4, .3f, new Color(.9f, .85f, .7f, .5f));
            }
            Vector3 vel = dir * db.playerSpeed * SpeedMultiplier();
            if (dashT > 0) { dashT -= dt; vel = dashDir * db.dashSpeed; }
            vel += push / dt; push = Vector3.zero;
            Gravity(dt, vel);
            return dir;
        }

        /// <summary>Host: an online player's new commands, in order. Buttons and look always count; movement only
        /// within the real time that has passed (RemoteInput's budget), so a sped-up client can't outrun the game.</summary>
        public void RunRemoteCommands(float frameDt)
        {
            Remote.AddBudget(frameDt);
            bool canPlay = gm.State == GameState.Playing && Time.timeScale > 0;
            while (Remote.Next(out var c, out bool canMove))
            {
                PrefersFirstPerson = c.firstPerson;
                if (canMove && canPlay) StepMove(c.move, c.Has(Btn.Dash), c.yaw, FirstPerson, c.dt);
            }
        }

        /// <summary>Client: this frame's controls as a numbered command. We move by it straight away (prediction)
        /// and keep it until the host confirms it.</summary>
        void PredictMove(float dt, bool canMove)
        {
            var c = new NetCommand { seq = ++commandSeq, dt = dt, yaw = Yaw, pitch = Pitch, firstPerson = PrefersFirstPerson };
            if (statsWanted) { c.pressed |= 1 << (int)Btn.Stats; statsWanted = false; }
            if (Controls != null && LocalMenu == null && !Rebinder.Busy)
            {
                c.nav = Controls.Nav();
                if (canMove) c.move = Controls.Move.ReadValue<Vector2>();
                foreach (Btn b in System.Enum.GetValues(typeof(Btn)))
                {
                    if (b is Btn.Pause or Btn.View) continue;  // handled here (Stats goes to the host, which owns that menu)
                    if (Controls.Held(b)) c.held |= (ushort)(1 << (int)b);
                    if (Controls.Pressed(b)) c.pressed |= (ushort)(1 << (int)b);
                }
            }
            c = c.Quantized();
            if (canMove) StepMove(c.move, c.Has(Btn.Dash), c.yaw, FirstPerson, c.dt);
            unconfirmed.Add(c);
            if (unconfirmed.Count > 240) unconfirmed.RemoveAt(0);   // the host has stopped answering: don't grow forever
            if (Cam != null) lastAim = LocalAim();
        }

        /// <summary>Client: the commands to send, newest last (every unconfirmed one, up to max), with the current aim.</summary>
        public void WriteCommands(System.IO.BinaryWriter w, int max)
        {
            w.Write((byte)Index);
            w.Write(lastAim);
            int from = Mathf.Max(0, unconfirmed.Count - max);
            w.Write((byte)(unconfirmed.Count - from));
            for (int i = from; i < unconfirmed.Count; i++) unconfirmed[i].Write(w);
        }

        /// <summary>What the host's simulation says about a player's movement (sent to the player's own machine).</summary>
        public struct MoveState
        {
            public uint confirmed;
            public Vector3 pos, dashDir;
            public float yVel, dashT, dashCd;
        }

        public void WriteMoveState(System.IO.BinaryWriter w)
        {
            w.Write(Remote?.Processed ?? 0); w.Write(transform.position); w.Write(dashDir);
            w.Write(yVel); w.Write(dashT); w.Write(dashCd);
        }

        public static MoveState ReadMoveState(System.IO.BinaryReader r) => new()
        {
            confirmed = r.ReadUInt32(), pos = r.ReadVector3(), dashDir = r.ReadVector3(),
            yVel = r.ReadSingle(), dashT = r.ReadSingle(), dashCd = r.ReadSingle(),
        };

        /// <summary>
        /// Client: the host says where we were after command `confirmed`. Start from there and replay every command
        /// it hasn't seen yet. Usually that lands exactly where we already are; when it doesn't (a push, a grab, a
        /// collision we didn't predict) we take the host's answer and let the camera ease across the difference.
        /// </summary>
        public void Reconcile(MoveState m)
        {
            unconfirmed.RemoveAll(c => c.seq <= m.confirmed);
            var predicted = transform.position;
            Place(m.pos);
            yVel = m.yVel; dashT = m.dashT; dashCd = m.dashCd; dashDir = m.dashDir;
            bool canMove = !IsOut && gm.State == GameState.Playing;
            if (canMove) foreach (var c in unconfirmed) StepMove(c.move, c.Has(Btn.Dash), c.yaw, FirstPerson, c.dt);
            var error = predicted - transform.position;
            RenderOffset = error.magnitude < 3 ? RenderOffset + error : Vector3.zero;
        }

        void FaceAim(float dt)
        {
            var target = FirstPerson ? Quaternion.Euler(0, Yaw, 0) : Quaternion.LookRotation(AimDir);
            visual.rotation = Quaternion.RotateTowards(visual.rotation, target, 900 * dt);
        }

        void Gravity(float dt, Vector3 vel)
        {
            yVel = cc.isGrounded ? -2 : yVel + Physics.gravity.y * dt;
            cc.Move((vel + Vector3.up * yVel) * dt);
            // Never sink below the sand/seabed (CharacterController + MeshCollider ground also handles this)
            var p = transform.position;
            float g = Arena.GroundY(p.z);
            if (p.y < g - .05f) { p.y = g; transform.position = p; yVel = 0; }
        }

        /// <summary>Shove the player (knockback, an octopus pulling). Applied on their next movement step; an online
        /// player's machine catches up through reconciliation.</summary>
        public void Push(Vector3 delta) => push += delta;

        /// <summary>Put the player somewhere directly (respawns, tests). The CharacterController has to be off for it.</summary>
        public void Place(Vector3 at)
        {
            cc.enabled = false; transform.position = at; cc.enabled = true;
        }

        // ------------------------------------------------------------------ combat
        float DamageMult(bool melee)
        {
            float m = melee ? gm.Team.MeleeDamage : gm.Team.GunDamage;
            m *= 1 + StatBonus(melee ? StatKind.MeleeDamage : StatKind.GunDamage);
            m *= gm.Cards.PlayerDamage;
            if (gm.Cards.Has("screenswap") && gm.Cards.SharesScreen(this)) m *= 1.4f;
            if (Statuses.Has(StatusType.Rage)) m *= 1.5f;
            if (Statuses.Has(StatusType.DoubleDamage)) m *= 2;
            if (Statuses.Has(StatusType.Giant)) m *= 1.25f;
            if (Statuses.Has(StatusType.Tiny)) m *= .7f;
            return m;
        }

        /// <summary>Timers that keep running whatever you're doing (reloads finish, swings land, even with a menu open).</summary>
        void CombatTimers(float dt)
        {
            fireCd -= dt; meleeCd -= dt; swingT -= dt; fireBuffer -= dt; lockToastT -= dt;
            FireHeat = Mathf.MoveTowards(FireHeat, 0, dt * 3);
            comboT -= dt;
            if (comboT <= 0) combo = 0;
            lastReload?.Update(dt, ref dummyAmmo, 0);
            if (reload != null)
            {
                int a = Ammo;
                bool done = reload.Update(dt, ref a, MagSize);
                ammo[Gun.id] = a;
                if (done) FinishReload();
            }
            if (strikeT > 0)
            {
                strikeT -= dt;
                if (strikeT <= 0) Strike();
            }
        }

        /// <summary>Start a melee swing; the hit lands when the wind-up ends.</summary>
        void StartSwing()
        {
            meleeCd = EffMelee.cooldown; swingT = Melee.SwingTime; strikeT = Mathf.Max(.01f, Melee.windup);
            Sfx.Play("swing");
        }

        void Strike()
        {
            int hit = Combat.Melee(this, EffMelee, transform.position + Vector3.up * .9f + AimDir * .3f, AimDir, DamageMult(true));
            if (hit > 0) { Sfx.Play("hit"); Shake(.08f + (Melee.style == MeleeStyle.Slam ? .2f : 0)); }
            if (Melee.style == MeleeStyle.Slam) Fx.Debris(transform.position + AimDir * 1.6f, new Color(.9f, .82f, .6f), 10);
        }

        void UpdateCombat(float dt)
        {
            CombatTimers(dt);
            if (grabbedBy != null)
            {
                // Mash to break free
                if (Pressed(Btn.Melee) || Pressed(Btn.Interact) || Pressed(Btn.Fire)) grabbedBy.Struggle();
                return;
            }
            if (!GunsLocked && Pressed(Btn.Swap)) CycleGun();
            if (!GunsLocked && Pressed(Btn.Reload))
            {
                if (reload != null) { int a = Ammo; reload.Press(ref a, MagSize); ammo[Gun.id] = a; if (reload.Done) FinishReload(); }
                else StartReload();
            }
            bool rHeld = Held(Btn.Reload);
            if (reloadHeld && !rHeld && reload != null) { reload.Release(); if (reload.Done) FinishReload(); }
            reloadHeld = rHeld;
            // One-at-a-time reloads stop when you fire with rounds in
            if (reload != null && reload.PerRound && Ammo > 0 && Pressed(Btn.Fire)) { lastReload = null; reload = null; }

            if (Pressed(Btn.Fire)) fireBuffer = .25f;
            if (Carrying.Count > 0)
            {
                if (fireBuffer > 0) { fireBuffer = 0; ThrowTop(); }
            }
            else if (GunsLocked)
            {
                // The first waves are melee only: the trigger swings your melee weapon
                if (fireBuffer > 0 && meleeCd <= 0) { fireBuffer = 0; StartSwing(); }
                if (Pressed(Btn.Fire) && lockToastT <= 0)
                {
                    lockToastT = 6;
                    Toast($"Guns unlock at wave {db.gunsUnlockWave}: melee only until then!", new Color(1, .8f, .5f));
                }
            }
            else if ((Held(Btn.Fire) || fireBuffer > 0) && fireCd <= 0 && reload == null)
            {
                if (Ammo <= 0) StartReload();
                else Shoot();
            }
            if (Pressed(Btn.Melee) && meleeCd <= 0) StartSwing();
        }

        /// <summary>Where bullets leave from: the first-person gun (bottom right of your view) or the model's muzzle.</summary>
        public Vector3 MuzzlePosition()
        {
            if (FirstPerson) return EyePos + LookRotation * new Vector3(.26f, -.2f, .7f);
            return gunModel.sockets.TryGetValue("Muzzle", out var mz) ? mz.position : transform.position + Vector3.up * 1.2f + AimDir * .6f;
        }

        void Shoot()
        {
            fireBuffer = 0;
            float rate = EffGun.fireRate * (1 + StatBonus(StatKind.Handling) * .5f) * (Statuses.Has(StatusType.SugarRush) ? 1.25f : 1);
            fireCd = 1 / rate;
            ammo[Gun.id] = Ammo - 1;
            var origin = MuzzlePosition();
            Combat.FireGun(this, EffGun, origin, AimPoint, DamageMult(false) * (1 + perfectBonus));
            Fx.Flash(origin, new Color(1, .8f, .4f), 5, 3, .06f);
            Fx.Smoke(origin, 1, .05f);
            Sfx.Play(Gun.id switch { "scattergun" => "shotgun", "harpoon" => "harpoon", "flare" => "flare", _ => "shot" });
            Shake(EffGun.recoil * .025f);
            FireHeat = 1;
            Pitch = Mathf.Clamp(Pitch - EffGun.recoil * .25f, -80, 80);   // the barrel climbs a little (the camera kick is bigger, and recovers)
            if (Ammo <= 0) { perfectBonus = 0; StartReload(); }
        }

        int dummyAmmo;

        /// <summary>Start the gun's reload minigame (CLAUDE.md R6b). With no input it finishes in the normal reload time.</summary>
        void StartReload()
        {
            if (reload != null || Ammo >= MagSize) return;
            float time = EffGun.reload / (1 + StatBonus(StatKind.Handling));
            reload = ReloadGame.Begin(db.Reload(Gun.reloadStyle), time, MagSize);
            lastReload = null;
            Sfx.Play("reload");
        }

        /// <summary>Tour and tests: empty the gun and start its reload.</summary>
        public void EmptyAndReload() { ammo[Gun.id] = 0; reload = null; StartReload(); }

        void FinishReload()
        {
            if (!reload.PerRound) ammo[Gun.id] = MagSize;
            if (reload.Perfect)
            {
                perfectBonus = reload.R.bonus > 0 ? reload.R.bonus : db.perfectReloadBonus;
                Toast($"Perfect reload: +{perfectBonus * 100:0}% damage this magazine", new Color(1, .85f, .3f));
            }
            Sfx.Play("reload");
            lastReload = reload;
            reload = null;
        }

        void CycleGun()
        {
            var list = db.guns.FindAll(g => ownedGuns.Contains(g.id));
            if (list.Count < 2) return;
            int i = list.IndexOf(Gun);
            GiveGun(list[(i + 1) % list.Count].id);
            Sfx.Play("ui");
        }

        public void OnHitEnemy(Enemy e, float dmg, bool crit, bool melee = false)
        {
            float steal = melee ? EffMelee.lifesteal : EffGun.lifesteal;
            if (steal > 0) Heal(dmg * steal);
            if (Hud != null) Hud.HitMarker(crit);
            else if (Kind == PlayerKind.Remote && Net.IsHost) Net.Host.HitMarker(this, crit);
        }
        public void OnSever(Enemy e, LimbDef limb) { Severs++; Toast($"{e.Def.displayName}: {limb.part} off!", new Color(1, .85f, .5f)); }
        public void OnKill(Enemy e)
        {
            Kills++;
            if (Hud != null) Hud.KillMarker();
            Sfx.Play("kill", .7f, .05f);
            // Kill combos: kills within the window chain; each one past the first pays a little more
            combo = comboT > 0 ? combo + 1 : 1;
            comboT = db.comboWindow;
            BestCombo = Mathf.Max(BestCombo, combo);
            if (combo >= 2)
            {
                int step = Mathf.Min(combo, db.comboMax) - 1;
                if (db.comboSandDollars > 0) gm.AddSandDollars(step * db.comboSandDollars);
                if (db.comboXp > 0) GainXp(step * db.comboXp);
                Sfx.Play("combo", .5f, .05f);
            }
        }

        // ------------------------------------------------------------------ interaction & carrying
        void UpdateInteract(float dt)
        {
            Focus = LookedAt() ?? Interactable.Nearest(this);
            var downedMate = DownedTeammateNearby();
            if (downedMate != null && Held(Btn.Interact))
            {
                // Shared Snacks (split screen): screen-mates revive each other twice as fast
                downedMate.ReviveTick(dt * (gm.Cards.Has("snacks") && downedMate.Owner == Owner ? 2 : 1));
                return;
            }
            if (!Pressed(Btn.Interact)) return;
            if (Focus != null) Focus.Interact(this);
            else if (Carrying.Count > 0) Drop(Carrying[^1]);
        }

        /// <summary>In first person, the thing under your crosshair (within reach) wins: much easier to grab the right corpse.</summary>
        Interactable LookedAt()
        {
            if (!FirstPerson) return null;
            var ray = new Ray(EyePos, LookRotation * Vector3.forward);
            var hits = Physics.RaycastAll(ray, 4.5f, ~0, QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in hits)
            {
                if (h.collider.GetComponentInParent<Player>() == this) continue;
                var it = h.collider.GetComponentInParent<Interactable>();
                if (it != null && it.isActiveAndEnabled && it.Prompt(this) != null) return it;
                if (!h.collider.isTrigger) break;   // a wall or the ground is in the way
            }
            return null;
        }

        public Player DownedTeammateNearby()
        {
            foreach (var p in gm.Players.All)
                if (p != this && p.IsDowned && Vector3.Distance(p.transform.position, transform.position) < 1.9f) return p;
            return null;
        }

        /// <summary>What Interact does here, with "{btn}" where the button glyph goes (each machine fills in its own).</summary>
        public string PromptText()
        {
            if (!Simulated) return NetPrompt ?? "";
            if (IsDowned || IsOut || OpenedMenu != null || gm.State != GameState.Playing) return "";
            var mate = DownedTeammateNearby();
            if (mate != null) return $"Hold {{btn}} to revive {mate.Name} ({mate.ReviveProgress * 100:0}%)";
            if (Focus != null) return "{btn} " + Focus.Prompt(this);
            if (Carrying.Count > 0) return "{btn} Drop";
            var ph = gm.Phases.Current;
            if (ph != null && ph.endWhenAllReady) return $"Ring the bell when you're ready · {gm.Players.ReadyCount}/{gm.Players.Count} ready";
            return "";
        }

        public void PickUp(Carryable c)
        {
            if (CarryWeight + c.weight > MaxCarry) return;
            Carrying.Add(c);
            c.Holder = this;
            foreach (var th in c.GetComponents<Thrown>()) Destroy(th);
            c.transform.SetParent(visual, true);
            SetLayer(c.transform, BodyLayer + Index);   // out of your own first-person view
            LayoutCarried();
            Sfx.Play("ui");
        }

        void LayoutCarried()
        {
            float y = 2.0f;
            foreach (var c in Carrying)
            {
                if (!c) continue;
                c.transform.localPosition = new Vector3(0, y, .1f);
                c.transform.localRotation = Quaternion.identity;
                y += .35f + c.weight * .25f;
            }
        }

        public void Drop(Carryable c)
        {
            if (!Carrying.Remove(c)) return;
            c.Holder = null;
            c.transform.SetParent(null, true);
            SetLayer(c.transform, 0);
            var p = transform.position + AimDir * .9f;
            c.transform.position = new Vector3(p.x, Arena.GroundY(p.z), p.z);
            LayoutCarried();
        }

        void ThrowTop()
        {
            var c = Carrying[^1];
            Carrying.RemoveAt(Carrying.Count - 1);
            SetLayer(c.transform, 0);
            var flat = AimPoint - transform.position; flat.y = 0;
            float dist = Mathf.Clamp(flat.magnitude, 2, 12);
            const float T = .7f;
            var vel = AimDir * (dist / T) + Vector3.up * (-Physics.gravity.y * T / 2);
            c.Throw(transform.position + Vector3.up * 2f, vel);
            LayoutCarried();
            Sfx.Play("swing");
        }

        // ------------------------------------------------------------------ health
        public void TakeDamage(float amount, Enemy source, string what, Vector3? knock = null, bool quiet = false)
        {
            if (!IsTargetable || amount <= 0) return;
            if (Statuses.Has(StatusType.Invincible)) return;
            if (Statuses.Has(StatusType.Giant)) amount *= .8f;
            Hp -= amount;
            if (knock.HasValue) Push(knock.Value * .15f);
            if (!quiet) { hurtFlash = .25f; Sfx.Play("hurt", .7f, .1f); Shake(.15f); }
            if (Hp <= 0) GoDown(what);
        }

        public void Heal(float amount) { if (!IsDowned && !IsOut) Hp = Mathf.Min(MaxHp, Hp + amount); }

        void GoDown(string what)
        {
            Hp = 0; IsDowned = true; BleedLeft = db.bleedOutSeconds; ReviveProgress = 0;
            for (int i = Carrying.Count - 1; i >= 0; i--) Drop(Carrying[i]);
            if (grabbedBy != null) Release(grabbedBy);
            if (OpenedMenu != null && !OpenedMenu.System) CloseMenu();
            gm.Banner($"{Name.ToUpperInvariant()} IS DOWN", what != null ? $"Taken out by {what}. Hold Interact next to them to revive!" : "Hold Interact next to them to revive!");
            gm.Players.CheckWipe();
        }

        void UpdateDowned(float dt)
        {
            BleedLeft -= dt;
            reticle.enabled = false;
            if (Kind == PlayerKind.Local) { TickLook(dt); StepMove(Controls != null ? Controls.Move.ReadValue<Vector2>() : Vector2.zero, false, Yaw, FirstPerson, dt); }
            else { Yaw = Remote.Yaw; Pitch = Remote.Pitch; }   // online: crawling comes with their commands
            if (BleedLeft <= 0)
            {
                IsDowned = false; IsOut = true;
                gm.Banner($"{Name.ToUpperInvariant()} BLED OUT", "They'll be back next phase.");
                gm.Players.CheckWipe();
            }
        }

        public void ReviveTick(float dt)
        {
            ReviveProgress += dt / db.reviveSeconds;
            if (ReviveProgress >= 1) Revive(db.reviveHpFraction);
        }

        public void Revive(float hpFraction)
        {
            IsDowned = false; IsOut = false; ReviveProgress = 0;
            Hp = MaxHp * hpFraction;
            Statuses.Add(StatusType.Invincible, 2);
            Sfx.Play("bell", .4f, .2f);
        }

        /// <summary>Back in the game at the start of a phase (if they bled out).</summary>
        public void Respawn(Vector3 at)
        {
            if (!IsOut && !IsDowned) return;
            Place(at);
            Revive(1);
        }

        public void Grab(Enemy by, float seconds) { grabbedBy = by; Statuses.Add(StatusType.Grabbed, seconds); Shake(.2f); }
        public void Release(Enemy by) { if (grabbedBy == by) { grabbedBy = null; Statuses.Clear(StatusType.Grabbed); } }

        // ------------------------------------------------------------------ food
        public void Eat(FoodDef food, CondimentDef sauce)
        {
            Heal(food.heal);
            if (gm.Cards.Has("snacks"))   // Shared Snacks: everyone on your screen eats too
                foreach (var o in gm.Players.All) if (o != this && o.Owner == Owner) { o.Heal(food.heal); o.Toast($"{Name} shared a {food.displayName}: +{food.heal:0} HP", new Color(1, .8f, .5f)); }
            if (food.status != StatusType.None) Statuses.Add(food.status, food.statusSeconds);
            if (food.cures) { Statuses.Clear(StatusType.InkVision); Statuses.Clear(StatusType.Slow); Statuses.Clear(StatusType.Burning); }
            Sfx.Play(food.id == "drink" ? "gulp" : "eat");
            string line = $"+{food.heal:0} HP";
            if (sauce != null)
            {
                if (sauce.status == StatusType.None) line = Sauce.Roll(this);
                else { Statuses.Add(sauce.status, sauce.statusSeconds); line = sauce.description; }
                Fx.Gore(transform.position + Vector3.up * 1.6f, sauce.color, 6);
            }
            Toast($"{food.displayName}{(sauce != null ? " + " + sauce.displayName : "")}: {line}", new Color(1, .9f, .6f));
        }

        // ------------------------------------------------------------------ menus
        /// <summary>Open a menu in this player's view. On a client, pause/settings menus stay local.</summary>
        public void OpenMenu(Menu m)
        {
            if (Kind == PlayerKind.LocalProxy) LocalMenu = m; else OpenedMenu = m;
            Sfx.Play("ui");
        }

        public void CloseMenu()
        {
            if (Kind == PlayerKind.LocalProxy) LocalMenu = null; else OpenedMenu = null;
        }

        void UpdateMenu(float dt)
        {
            var menu = ShownMenu;
            if (menu == null || Rebinder.Busy) return;
            var nav = NavInput();
            menuRepeat -= dt;
            int dir = nav.y > .5f ? -1 : nav.y < -.5f ? 1 : 0;
            int side = nav.x > .5f ? 1 : nav.x < -.5f ? -1 : 0;
            bool freshV = Mathf.Abs(lastNav.y) <= .5f, freshH = Mathf.Abs(lastNav.x) <= .5f;
            if (dir != 0 && (freshV || menuRepeat <= 0))
            {
                if (menu.Columns > 0) menu.MoveGrid(0, dir); else menu.Move(dir);
                menuRepeat = freshV ? .35f : .1f;
                Sfx.Play("ui", .5f);
            }
            if (side != 0 && (freshH || menuRepeat <= 0))
            {
                var it = menu.Current;
                if (menu.Columns > 0) { menu.MoveGrid(side, 0); Sfx.Play("ui", .5f); menuRepeat = freshH ? .35f : .1f; }
                else if (menu.Horizontal) { menu.Move(side); Sfx.Play("ui", .5f); menuRepeat = freshH ? .35f : .1f; }
                else if (it?.OnAdjust != null) { it.OnAdjust(side); Sfx.Play("ui", .5f); menuRepeat = freshH ? .35f : .08f; }
            }
            lastNav = nav;
            if (menu.OnTab != null && (Pressed(Btn.Swap) || Pressed(Btn.Reload)))
            {
                var next = menu.OnTab(Pressed(Btn.Swap) ? 1 : -1);
                if (next != null) { ReplaceMenu(next); Sfx.Play("ui"); }
                return;
            }
            if (Pressed(Btn.Back))
            {
                var parent = menu.Parent?.Invoke();
                if (parent != null) ReplaceMenu(parent); else CloseMenu();
                return;
            }
            if (Pressed(Btn.Interact))
            {
                var item = menu.Current;
                if (item == null) return;
                var why = item.Blocked?.Invoke();
                if (why != null) { Sfx.Play("deny"); Toast(why, new Color(1, .6f, .6f)); return; }
                item.OnPick?.Invoke();
                var next = item.Open?.Invoke();
                if (ShownMenu != menu) return;              // the pick closed or replaced the menu itself
                if (next != null) { ReplaceMenu(next); return; }
                int sel = menu.Selected;
                var rebuilt = menu.Rebuild?.Invoke();
                if (rebuilt != null) { rebuilt.Selected = Mathf.Clamp(sel, 0, rebuilt.Items.Count - 1); ReplaceMenu(rebuilt); }
            }
        }

        void ReplaceMenu(Menu m)
        {
            if (Kind == PlayerKind.LocalProxy) LocalMenu = m; else OpenedMenu = m;
        }

        // ------------------------------------------------------------------ look
        void UpdateVisual(float dt)
        {
            float scale = Statuses.Has(StatusType.Giant) ? 1.45f : Statuses.Has(StatusType.Tiny) ? .6f : 1;
            visual.localScale = Vector3.Lerp(visual.localScale, Vector3.one * scale, dt * 6);
            if (Statuses.Has(StatusType.Rainbow)) ModelFactory.Recolor(model, "coat", Color.HSVToRGB(Time.time * .5f % 1, .8f, 1));
            else if (Statuses.Has(StatusType.Rage)) ModelFactory.Recolor(model, "coat", Color.Lerp(Color, Color.red, .5f + .5f * Mathf.Sin(Time.time * 12)));
            if (Statuses.Has(StatusType.Bubbles) && Random.value < dt * 10 && Simulated) Fx.Bubbles(transform.position + Vector3.up * 1.8f, 1);
            if (Statuses.Has(StatusType.Speed) && Random.value < dt * 12 && Simulated) Fx.Smoke(transform.position, 1, .2f, new Color(1, .9f, .4f, .4f));
            bool blink = Statuses.Has(StatusType.Invincible) && Mathf.Sin(Time.time * 30) > 0;
            foreach (var r in model.renderers) r.enabled = !blink;
            AnimateBody(dt);
        }

        // ------------------------------------------------------------------ body animation
        /// <summary>The jointed sailor: legs stride with your speed, the gun arm aims where you look, the other arm swings
        /// the melee weapon in that weapon's style, both arms go up to carry, the head follows your gaze.</summary>
        void AnimateBody(float dt)
        {
            if (legL == null) return;
            var moved = transform.position - lastVisualPos; moved.y = 0;
            lastVisualPos = transform.position;
            float speed = dt > 0 ? moved.magnitude / dt : 0;
            float gait = Mathf.Clamp01(speed / Mathf.Max(1, db.playerSpeed));
            stepPhase += dt * (4 + speed * 1.6f);
            float k = 1 - Mathf.Exp(-dt * 18);

            // Legs
            float stride = Mathf.Sin(stepPhase) * 38 * gait;
            if (IsDowned) stride = Mathf.Sin(stepPhase * .5f) * 15 * gait;   // a feeble crawl
            legL.localRotation = Quaternion.Slerp(legL.localRotation, Quaternion.Euler(stride, 0, IsDowned ? -10 : 0), k);
            legR.localRotation = Quaternion.Slerp(legR.localRotation, Quaternion.Euler(-stride, 0, IsDowned ? 10 : 0), k);

            // Body: bob with the steps, lean into a dash, breathe when still
            var bodyPose = Quaternion.Euler((IsDowned ? -80 : 0) + (dashT > 0 ? 18 : gait * 5), 0, 0);
            model.root.transform.localRotation = Quaternion.Slerp(model.root.transform.localRotation, bodyPose, dt * 10);
            model.root.transform.localPosition = new Vector3(0, IsDowned ? .25f : Mathf.Abs(Mathf.Sin(stepPhase)) * .06f * gait, 0);

            // Head follows where you look (up/down)
            if (head != null) head.localRotation = Quaternion.Slerp(head.localRotation, Quaternion.Euler(Mathf.Clamp(Pitch, -40, 40) * .6f, 0, 0), k);

            // Arms
            Quaternion right, left;
            float sp = SwingProgress;
            bool carrying = CarryCount > 0;
            if (carrying) { right = Quaternion.Euler(-165, 0, -12); left = Quaternion.Euler(-165, 0, 12); }
            else if (IsDowned) { right = Quaternion.Euler(-150 + Mathf.Sin(stepPhase * .5f) * 30, 0, 0); left = Quaternion.Euler(-150 - Mathf.Sin(stepPhase * .5f) * 30, 0, 0); }
            else
            {
                // Gun arm: held out along the aim (the gun is holstered while guns are locked)
                right = GunsLocked ? Quaternion.Euler(-Mathf.Sin(stepPhase) * 25 * gait, 0, 6) : Quaternion.Euler(-90 + Pitch * .8f - FireHeat * 12, -6, 0);
                left = sp >= 0 ? SwingArm(sp) : Quaternion.Euler(-35 + Mathf.Sin(stepPhase) * 20 * gait, 0, -6);
            }
            armR.localRotation = Quaternion.Slerp(armR.localRotation, right, sp >= 0 ? 1 : k);
            armL.localRotation = sp >= 0 ? left : Quaternion.Slerp(armL.localRotation, left, k);

            // Weapons in hand: the gun points where you aim whatever the arm does; the melee weapon extends the arm
            if (gunModel != null)
            {
                gunModel.root.SetActive(!GunsLocked && !carrying);
                gunModel.root.transform.localRotation = Quaternion.Inverse(armR.localRotation) * Quaternion.Euler(Pitch * .8f - FireHeat * 12, 0, 0);
            }
            if (meleeModel != null) meleeModel.root.transform.localRotation = Quaternion.Euler(90, 0, 0) * Quaternion.Euler(sp >= 0 ? SwingWrist(sp) : -30, 0, 0);
        }

        /// <summary>The swinging arm through wind-up → strike → recovery, shaped by the weapon's style.</summary>
        Quaternion SwingArm(float t)
        {
            float w = Melee.windup / Melee.SwingTime, st = (Melee.windup + Melee.strike) / Melee.SwingTime;
            // a: 0 (rest) → 1 (fully wound up) → strike to 2 → recover to 3
            float a = t < w ? t / w : t < st ? 1 + (t - w) / Mathf.Max(.001f, st - w) : 2 + (t - st) / Mathf.Max(.001f, 1 - st);
            switch (Melee.style)
            {
                case MeleeStyle.Slash:   // cutlass: cocked back by the shoulder, a flat backhand slash across, wrist rolling
                    return a < 1 ? Quaternion.Euler(Mathf.Lerp(-35, -95, a), Mathf.Lerp(0, 70, a), Mathf.Lerp(-6, -40, a))
                         : a < 2 ? Quaternion.Euler(-95, Mathf.Lerp(70, -80, a - 1), Mathf.Lerp(-40, 30, a - 1))
                         : Quaternion.Euler(Mathf.Lerp(-95, -35, a - 2), Mathf.Lerp(-80, 0, a - 2), Mathf.Lerp(30, -6, a - 2));
                case MeleeStyle.Sweep:   // oar: a big two-handed side-to-side sweep, low and wide
                    return a < 1 ? Quaternion.Euler(Mathf.Lerp(-35, -70, a), Mathf.Lerp(0, 95, a), 0)
                         : a < 2 ? Quaternion.Euler(-70, Mathf.Lerp(95, -95, a - 1), 0)
                         : Quaternion.Euler(Mathf.Lerp(-70, -35, a - 2), Mathf.Lerp(-95, 0, a - 2), 0);
                case MeleeStyle.Thrust:  // boat hook: draw back, jab forward, then yank back toward you
                    return a < 1 ? Quaternion.Euler(Mathf.Lerp(-35, -60, a), 0, 0)
                         : a < 2 ? Quaternion.Euler(Mathf.Lerp(-60, -95, a - 1), 0, 0)
                         : Quaternion.Euler(Mathf.Lerp(-95, -35, a - 2), 0, 0);
                case MeleeStyle.Slam:    // anchor: heaved right up over the head, then brought crashing down
                    return a < 1 ? Quaternion.Euler(Mathf.Lerp(-35, -200, Mathf.SmoothStep(0, 1, a)), 0, 0)
                         : a < 2 ? Quaternion.Euler(Mathf.Lerp(-200, -10, (a - 1) * (a - 1)), 0, 0)
                         : Quaternion.Euler(Mathf.Lerp(-10, -35, a - 2), 0, 0);
                default:                 // spade: up and over, a downward chop
                    return a < 1 ? Quaternion.Euler(Mathf.Lerp(-35, -165, a), 0, 0)
                         : a < 2 ? Quaternion.Euler(Mathf.Lerp(-165, -40, a - 1), 0, 0)
                         : Quaternion.Euler(Mathf.Lerp(-40, -35, a - 2), 0, 0);
            }
        }

        /// <summary>The wrist: weapons tilt back during the wind-up and snap through on the strike.</summary>
        float SwingWrist(float t)
        {
            float w = Melee.windup / Melee.SwingTime;
            return t < w ? Mathf.Lerp(-30, -70, t / w) : Mathf.Lerp(-70, 10, Mathf.Clamp01((t - w) / .35f));
        }

        // ------------------------------------------------------------------ building
        public void StartBuilding(StructureDef def)
        {
            Building = def;
            BuildYaw = Mathf.Round(Yaw / 45) * 45;
            CloseMenu();
            Toast($"Building {def.displayName}: {Glyph(Btn.Interact)} place · {Glyph(Btn.Reload)} turn · {Glyph(Btn.Back)} cancel", new Color(.7f, .9f, 1));
        }

        public void StopBuilding() { Building = null; UpdateGhost(); }

        /// <summary>Where the current build would go (on the ground under your aim, within reach).</summary>
        public Vector3 BuildSpot()
        {
            var to = AimPoint - transform.position; to.y = 0;
            if (to.magnitude > 9) to = to.normalized * 9;
            if (to.magnitude < 2) to = AimDir * 2.5f;
            var p = transform.position + to;
            return new Vector3(p.x, Arena.GroundY(p.z), p.z);
        }

        void UpdateBuild(float dt)
        {
            CombatTimers(dt);
            var spot = BuildSpot();
            string why = Structure.WhyNot(Building, spot);
            BuildValid = why == null && gm.SandDollars >= Building.sandDollars && gm.Pearls >= Building.pearls;
            if (Pressed(Btn.Reload)) BuildYaw += 45;
            if (Pressed(Btn.Back) || Pressed(Btn.Build)) { StopBuilding(); return; }
            if (Pressed(Btn.Interact) || Pressed(Btn.Fire))
            {
                if (why != null) { Sfx.Play("deny"); Toast(why, new Color(1, .6f, .6f)); return; }
                if (!gm.TrySpend(Building.sandDollars, Building.pearls)) { Toast("Not enough sand dollars or pearls", new Color(1, .6f, .6f)); return; }
                Structure.Build(Building, spot, BuildYaw);
                Sfx.Play("reload");
                Fx.Debris(spot + Vector3.up * .3f, new Color(.9f, .82f, .6f), 8);
                if (gm.SandDollars < Building.sandDollars || gm.Pearls < Building.pearls) StopBuilding();   // can't afford another
            }
            UpdateGhost();
        }

        /// <summary>The see-through preview of what you're placing (only your own camera sees it).</summary>
        void UpdateGhost()
        {
            if (!HasView) return;
            if (Building == null) { if (ghost != null) { Destroy(ghost); ghost = null; ghostId = null; } return; }
            if (ghost == null || ghostId != Building.id)
            {
                if (ghost != null) Destroy(ghost);
                var mi = ModelFactory.Spawn(Building.model, null, Vector3.zero);
                ghost = mi.root; ghostId = Building.id;
                foreach (var t in ghost.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = ViewLayer + Index;
                foreach (var r in mi.renderers) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.sharedMaterial = Mats.Transparent(new Color(.4f, 1f, .5f, .45f)); }
            }
            var spot = BuildSpot();
            bool ok = Kind == PlayerKind.LocalProxy ? Structure.WhyNot(Building, spot) == null && gm.SandDollars >= Building.sandDollars && gm.Pearls >= Building.pearls : BuildValid;
            ghost.transform.SetPositionAndRotation(spot, Quaternion.Euler(0, BuildYaw, 0));
            var col = ok ? new Color(.4f, 1f, .5f, .45f) : new Color(1f, .35f, .3f, .45f);
            foreach (var r in ghost.GetComponentsInChildren<Renderer>()) r.sharedMaterial = Mats.Transparent(col);
        }


        void UpdateNameTag()
        {
            if (nameTag == null) return;
            nameTag.text = string.IsNullOrEmpty(Nick) ? Name : $"{Name} · {Nick}";
            var cam = gm.Players.FirstViewCamera(this);
            nameTag.gameObject.SetActive(cam != null && gm.Players.Count > 1);
        }

        public float HurtFlash => hurtFlash;

        public void Shake(float amount)
        {
            if (Cam != null) Cam.AddShake(amount);
            else if (Kind == PlayerKind.Remote && Net.IsHost) Net.Host.Shake(this, amount);
        }

        public void Toast(string msg, Color? color = null)
        {
            if (Hud != null) Hud.Toast(msg, color ?? Color.white);
            else if (Kind == PlayerKind.Remote && Net.IsHost) Net.Host.Toast(this, msg, color ?? Color.white);
        }

        // ------------------------------------------------------------------ online mirror (client side)
        /// <summary>Client: the host's view of this player (from a snapshot).</summary>
        public void ApplyNet(NetPlayerState s)
        {
            netPos = s.pos; netYaw = s.yaw; netFirstPerson = s.firstPerson;
            if (Kind == PlayerKind.Proxy) { Pitch = s.pitch; PrefersFirstPerson = s.firstPerson; }
            Hp = s.hp; netMaxHp = s.maxHp;
            IsDowned = s.downed; IsOut = s.isOut; Ready = s.ready;
            BleedLeft = s.bleedLeft; ReviveProgress = s.revive; Wet = s.wet;
            if (s.ammo < netAmmo && Kind == PlayerKind.Proxy) FireHeat = 1;   // they fired (before netAmmo updates)
            netAmmo = s.ammo; netMag = Mathf.Max(1, s.mag); netReload = s.reload;
            netReloadGame = s.reloadGame; netPerfect = s.perfect; netCombo = s.combo; netComboT = s.comboLeft;
            netCarryCount = s.carryCount; netCarryWeight = s.carryWeight;
            if (s.swing > swingT) swingT = s.swing;
            if (Kind == PlayerKind.Proxy) AimPoint = s.aim;
            if (Gun == null || Gun.id != s.gun) GiveGun(s.gun);
            if (Melee == null || Melee.id != s.melee) GiveMelee(s.melee);
            Statuses.ClearAll();
            foreach (var (type, left) in s.statuses) Statuses.Add(type, left);
            Kills = s.kills;
            Level = s.level; Xp = s.xp; netXpNext = s.xpNext; StatPoints = s.statPoints; PendingMods = s.pendingMods; netSpeedBonus = s.speedBonus;
        }

        /// <summary>Client: the host's menu for this player (rows only; picking happens on the host).</summary>
        public void ApplyNetMenu(Menu m, string prompt, string building, float buildYaw)
        {
            OpenedMenu = m;
            NetPrompt = prompt;
            Building = string.IsNullOrEmpty(building) ? null : db.Structure(building);
            BuildYaw = buildYaw;
        }

        public void NetHurt() { hurtFlash = .25f; }

        /// <summary>Client: other players glide to where the host says they are.</summary>
        void ProxyUpdate(float dt)
        {
            Statuses.Tick(dt);
            if (hurtFlash > 0) hurtFlash -= dt;
            float k = 1 - Mathf.Exp(-dt * 14);
            cc.enabled = false;
            transform.position = Vector3.Distance(transform.position, netPos) > 6 ? netPos : Vector3.Lerp(transform.position, netPos, k);
            cc.enabled = true;
            swingT -= dt;
            visual.gameObject.SetActive(!IsOut);
            visual.rotation = Quaternion.Slerp(visual.rotation, Quaternion.Euler(0, netYaw, 0), k);
            if (!IsOut) Drip(dt);
            UpdateReticle(!IsDowned && !netFirstPerson && gm.State == GameState.Playing);
            UpdateVisual(dt);
        }

        /// <summary>Client: this machine's own player. Movement and looking are local; the rest mirrors the host.</summary>
        void OwnProxyUpdate(float dt)
        {
            Statuses.Tick(dt);
            if (hurtFlash > 0) hurtFlash -= dt;
            swingT -= dt;
            float udt = Time.unscaledDeltaTime;
            if (!Rebinder.Busy)
            {
                if (Pressed(Btn.Pause)) { if (LocalMenu != null) LocalMenu = null; else OpenMenu(Menus.Pause(this)); }
                if (gm.State == GameState.GameOver && Pressed(Btn.Back) && LocalMenu == null) gm.QuitToMenu();
                if (LocalMenu == null && OpenedMenu == null && Pressed(Btn.View)) SwitchView();
            }
            RenderOffset = Vector3.Lerp(RenderOffset, Vector3.zero, 1 - Mathf.Exp(-dt * 10));
            bool playing = !IsOut && gm.State == GameState.Playing;
            if (LocalMenu != null) { UpdateMenu(udt); PredictMove(dt, playing); UpdateVisual(dt); UpdateReticle(false); return; }
            visual.gameObject.SetActive(!IsOut);
            if (!playing) { PredictMove(dt, false); UpdateReticle(false); return; }
            TickLook(dt);
            ResolveAim(LocalAim());
            PredictMove(dt, true);
            if (IsDowned) UpdateReticle(false);
            else FaceAim(dt);
            UpdateGhost();
            Drip(dt);
            UpdateVisual(dt);
        }

        void OnDestroy()
        {
            Controls?.Dispose();
            if (reticle) Destroy(reticle.gameObject);
            if (Cam) Destroy(Cam.gameObject);
            if (Hud) Destroy(Hud.gameObject);
            if (gm != null && gm.World != null) gm.World.lanterns.Remove(lantern);
            foreach (var c in Carrying) if (c) { c.Holder = null; c.transform.SetParent(null, true); SetLayer(c.transform, 0); }
        }
    }
}

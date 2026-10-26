// TideWall 3D — a player: movement, looking/aiming (first person or top-down), guns, melee, interacting,
// carrying/throwing, health, being downed and revived, food and status effects.
//
// One class, four kinds (see PlayerKind):
//   Local      offline, or the host's own players: everything is simulated here, input from PlayerControls.
//   Remote     on the host, an online player: simulated here, input (and position) from their machine.
//   LocalProxy on a client, that client's own player: moves and looks locally (responsive), everything else
//              (health, ammo, menus, statuses) mirrors the host.
//   Proxy      on a client, everyone else: follows the host's snapshots.
using System.Collections.Generic;
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
        public float MaxHp => Simulated ? db.playerMaxHp + gm.Team.PlayerHpBonus : netMaxHp;
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
        public int MagSize => Simulated ? Mathf.RoundToInt(Gun.magazine * gm.Team.Magazine) : netMag;
        public float ReloadProgress => Simulated ? (reloadT > 0 ? 1 - reloadT / Gun.reload : -1) : netReload;
        public float SwingT => swingT;
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
        TextMesh nameTag;
        Light lantern;
        readonly HashSet<string> ownedGuns = new(), ownedMelee = new();
        readonly Dictionary<string, int> ammo = new();
        float fireCd, meleeCd, reloadT, swingT, dashT, dashCd, fireBuffer, yVel, menuRepeat, hurtFlash, dripAcc, ignoreRemotePosUntil;
        Vector3 dashDir, push;
        Enemy grabbedBy;
        Vector2 lastNav;
        // Mirrored from the host (proxies)
        float netMaxHp = 100, netReload = -1, netCarryWeight;
        int netAmmo, netMag = 1, netCarryCount;
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
            nameTag = new GameObject("NameTag").AddComponent<TextMesh>();
            nameTag.transform.SetParent(visual, false);
            nameTag.transform.localPosition = new Vector3(0, 2.35f, 0);
            nameTag.font = Ui.Font; nameTag.fontSize = 40; nameTag.characterSize = .05f; nameTag.anchor = TextAnchor.MiddleCenter;
            nameTag.color = Color.Lerp(Color, Color.white, .35f);
            nameTag.GetComponent<MeshRenderer>().sharedMaterial = Ui.Font.material;
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
        }

        public bool OwnsGun(string id) => ownedGuns.Contains(id);
        public bool OwnsMelee(string id) => ownedMelee.Contains(id);

        public void GiveGun(string id)
        {
            var g = db.Gun(id); if (g == null) return;
            ownedGuns.Add(id);
            Gun = g;
            if (!ammo.ContainsKey(id)) ammo[id] = Mathf.RoundToInt(g.magazine * gm.Team.Magazine);
            reloadT = 0;
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
            if (meleeModel != null) Destroy(meleeModel.root);
            meleeModel = ModelFactory.Spawn(m.model, Socket("Melee"), Vector3.zero);
            meleeModel.root.transform.localRotation = Quaternion.Euler(-60, 0, 0);   // held up, ready to swing
            SetLayer(meleeModel.root.transform, BodyLayer + Index);
            if (Cam != null) Cam.View.Rebuild();
        }

        Transform Socket(string name) => model.sockets.TryGetValue(name, out var s) ? s : visual;

        public void RefreshMaxHp() { if (!IsDowned && !IsOut) Hp = Mathf.Min(MaxHp, Hp + gm.DB.upgrades.Find(u => u.kind == UpgradeKind.PlayerMaxHp)?.amount ?? 0); }

        // ------------------------------------------------------------------ input helpers
        Vector2 MoveInput()
        {
            var mv = Controls != null ? Controls.Move.ReadValue<Vector2>() : Remote?.Move ?? Vector2.zero;
            return Statuses.Has(StatusType.ReverseControls) ? -mv : mv;
        }

        bool Held(Btn b) => Controls != null ? Controls.Held(b) : Remote != null && Remote.Held(b);
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
            float udt = Time.unscaledDeltaTime;
            if (gm.State == GameState.GameOver) { UpdateReticle(false); return; }
            if (dt <= 0) { if (OpenedMenu != null) UpdateMenu(udt); UpdateReticle(false); return; }   // paused (offline)

            UpdateWet(dt);
            if (IsOut) { visual.gameObject.SetActive(false); reticle.enabled = false; if (OpenedMenu != null) UpdateMenu(udt); return; }
            visual.gameObject.SetActive(true);

            if (IsDowned) { UpdateDowned(dt); if (OpenedMenu != null) UpdateMenu(udt); UpdateVisual(dt); return; }
            if (OpenedMenu != null) { UpdateMenu(udt); if (Kind == PlayerKind.Local) Gravity(dt, Vector3.zero); UpdateReticle(false); UpdateVisual(dt); return; }

            if (Kind == PlayerKind.Local) { TickLook(dt); ResolveAim(LocalAim()); }
            else ApplyRemoteLook();
            UpdateMovement(dt);
            UpdateCombat(dt);
            UpdateInteract(dt);
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
                if (Pressed(Btn.Interact)) gm.RetryFromSave();
                else if (Pressed(Btn.Back)) gm.QuitToMenu();
            }
        }

        public void SwitchView()
        {
            PrefersFirstPerson = !PrefersFirstPerson;
            Toast(PrefersFirstPerson ? "First person" : "Top-down view", new Color(.8f, .9f, 1f));
            Sfx.Play("ui");
        }

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
            return m;
        }

        /// <summary>Stick/WASD as a world direction: relative to where you look in first person, to the screen top-down.</summary>
        Vector3 MoveDir()
        {
            var mv = MoveInput();
            var dir = new Vector3(mv.x, 0, mv.y);
            if (FirstPerson) dir = Quaternion.Euler(0, Yaw, 0) * dir;
            return dir.magnitude > 1 ? dir.normalized : dir;
        }

        void UpdateMovement(float dt)
        {
            if (Kind == PlayerKind.Remote)
            {
                // Online players move on their own machine; the host takes their position (it's a co-op game).
                if (Remote.HasPosition && Time.time >= ignoreRemotePosUntil) Teleport(Remote.Position, false);
                if (Pressed(Btn.Dash)) Fx.Smoke(transform.position, 4, .3f, new Color(.9f, .85f, .7f, .5f));
                FaceAim(dt);
                return;
            }
            var dir = grabbedBy != null || Statuses.Has(StatusType.Grabbed) ? Vector3.zero : MoveDir();
            dashCd -= dt;
            if (Pressed(Btn.Dash) && dashCd <= 0 && dir.sqrMagnitude > .01f && !Statuses.Has(StatusType.Grabbed))
            {
                dashT = db.dashSeconds; dashCd = db.dashCooldown; dashDir = dir.normalized;
                if (Simulated) Fx.Smoke(transform.position, 4, .3f, new Color(.9f, .85f, .7f, .5f));
            }
            Vector3 vel = dir * db.playerSpeed * SpeedMultiplier();
            if (dashT > 0) { dashT -= dt; vel = dashDir * db.dashSpeed; }
            vel += push / Mathf.Max(dt, .0001f); push = Vector3.zero;
            Gravity(dt, vel);
            if (FirstPerson || UsesMouse || (Controls != null && Controls.Aim.ReadValue<Vector2>().magnitude > .2f) || dir.sqrMagnitude > .01f) FaceAim(dt);
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

        public void Push(Vector3 delta)
        {
            if (Kind == PlayerKind.Remote) { if (Net.IsHost) Net.Host.Push(this, delta); return; }
            push += delta;
        }

        void Teleport(Vector3 at, bool tellOwner)
        {
            cc.enabled = false; transform.position = at; cc.enabled = true;
            if (tellOwner && Kind == PlayerKind.Remote && Net.IsHost) { Net.Host.Teleport(this, at); ignoreRemotePosUntil = Time.time + .75f; }
        }

        // ------------------------------------------------------------------ combat
        float DamageMult(bool melee)
        {
            float m = melee ? gm.Team.MeleeDamage : gm.Team.GunDamage;
            if (Statuses.Has(StatusType.Rage)) m *= 1.5f;
            if (Statuses.Has(StatusType.DoubleDamage)) m *= 2;
            if (Statuses.Has(StatusType.Giant)) m *= 1.25f;
            if (Statuses.Has(StatusType.Tiny)) m *= .7f;
            return m;
        }

        void UpdateCombat(float dt)
        {
            fireCd -= dt; meleeCd -= dt; swingT -= dt; fireBuffer -= dt;
            if (reloadT > 0)
            {
                reloadT -= dt;
                if (reloadT <= 0) { ammo[Gun.id] = MagSize; Sfx.Play("reload"); }
            }
            if (grabbedBy != null)
            {
                // Mash to break free
                if (Pressed(Btn.Melee) || Pressed(Btn.Interact) || Pressed(Btn.Fire)) grabbedBy.Struggle();
                return;
            }
            if (Pressed(Btn.Swap)) CycleGun();
            if (Pressed(Btn.Reload)) StartReload();

            if (Pressed(Btn.Fire)) fireBuffer = .25f;
            if (Carrying.Count > 0)
            {
                if (fireBuffer > 0) { fireBuffer = 0; ThrowTop(); }
            }
            else if ((Held(Btn.Fire) || fireBuffer > 0) && fireCd <= 0 && reloadT <= 0)
            {
                if (Ammo <= 0) StartReload();
                else Shoot();
            }
            if (Pressed(Btn.Melee) && meleeCd <= 0)
            {
                meleeCd = Melee.cooldown; swingT = .25f;
                Sfx.Play("swing");
                int hit = Combat.Melee(this, Melee, transform.position + Vector3.up * .9f + AimDir * .3f, AimDir, DamageMult(true));
                if (hit > 0) { Sfx.Play("hit"); Shake(.08f); }
            }
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
            float rate = Gun.fireRate * (Statuses.Has(StatusType.SugarRush) ? 1.25f : 1);
            fireCd = 1 / rate;
            ammo[Gun.id] = Ammo - 1;
            var origin = MuzzlePosition();
            Combat.FireGun(this, Gun, origin, AimPoint, DamageMult(false));
            Fx.Flash(origin, new Color(1, .8f, .4f), 5, 3, .06f);
            Fx.Smoke(origin, 1, .05f);
            Sfx.Play(Gun.id switch { "scattergun" => "shotgun", "harpoon" => "harpoon", "flare" => "flare", _ => "shot" });
            Shake(Gun.id == "scattergun" ? .12f : .05f);
            if (Ammo <= 0) StartReload();
        }

        void StartReload()
        {
            if (reloadT > 0 || Ammo >= MagSize) return;
            reloadT = Gun.reload;
            Sfx.Play("reload");
        }

        void CycleGun()
        {
            var list = db.guns.FindAll(g => ownedGuns.Contains(g.id));
            if (list.Count < 2) return;
            int i = list.IndexOf(Gun);
            GiveGun(list[(i + 1) % list.Count].id);
            Sfx.Play("ui");
        }

        public void OnHitEnemy(Enemy e, float dmg, bool crit)
        {
            if (Hud != null) Hud.HitMarker(crit);
            else if (Kind == PlayerKind.Remote && Net.IsHost) Net.Host.HitMarker(this, crit);
        }
        public void OnSever(Enemy e, LimbDef limb) { Severs++; Toast($"{e.Def.displayName}: {limb.part} off!", new Color(1, .85f, .5f)); }
        public void OnKill(Enemy e) => Kills++;

        // ------------------------------------------------------------------ interaction & carrying
        void UpdateInteract(float dt)
        {
            Focus = Interactable.Nearest(this);
            var downedMate = DownedTeammateNearby();
            if (downedMate != null && Held(Btn.Interact))
            {
                downedMate.ReviveTick(dt);
                return;
            }
            if (!Pressed(Btn.Interact)) return;
            if (Focus != null) Focus.Interact(this);
            else if (Carrying.Count > 0) Drop(Carrying[^1]);
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
            if (Kind == PlayerKind.Local) { TickLook(dt); Gravity(dt, MoveDir() * 1f); }   // crawl slowly toward safety
            else if (Remote.HasPosition && Time.time >= ignoreRemotePosUntil) { Teleport(Remote.Position, false); Yaw = Remote.Yaw; Pitch = Remote.Pitch; }
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
            Teleport(at, true);
            Revive(1);
        }

        public void Grab(Enemy by, float seconds) { grabbedBy = by; Statuses.Add(StatusType.Grabbed, seconds); Shake(.2f); }
        public void Release(Enemy by) { if (grabbedBy == by) { grabbedBy = null; Statuses.Clear(StatusType.Grabbed); } }

        // ------------------------------------------------------------------ food
        public void Eat(FoodDef food, CondimentDef sauce)
        {
            Heal(food.heal);
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
            var nav = MoveInput();
            menuRepeat -= dt;
            int dir = nav.y > .5f ? -1 : nav.y < -.5f ? 1 : 0;
            if (dir != 0 && (Mathf.Abs(lastNav.y) <= .5f || menuRepeat <= 0))
            {
                menu.Move(dir);
                menuRepeat = Mathf.Abs(lastNav.y) <= .5f ? .35f : .1f;
                Sfx.Play("ui", .5f);
            }
            int side = nav.x > .5f ? 1 : nav.x < -.5f ? -1 : 0;
            if (side != 0 && (Mathf.Abs(lastNav.x) <= .5f || menuRepeat <= 0))
            {
                var it = menu.Current;
                if (it?.OnAdjust != null) { it.OnAdjust(side); Sfx.Play("ui", .5f); menuRepeat = Mathf.Abs(lastNav.x) <= .5f ? .35f : .08f; }
            }
            lastNav = nav;
            if (Pressed(Btn.Back) || (Pressed(Btn.Melee) && !menu.System))
            {
                var parent = menu.Parent?.Invoke();
                if (parent != null) ReplaceMenu(parent); else CloseMenu();
                return;
            }
            if (Pressed(Btn.Interact) || Pressed(Btn.Fire))
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
            var pose = Quaternion.Euler(IsDowned ? -80 : 0, 0, 0);
            model.root.transform.localRotation = Quaternion.Slerp(model.root.transform.localRotation, pose, dt * 8);
            if (Statuses.Has(StatusType.Rainbow)) ModelFactory.Recolor(model, "coat", Color.HSVToRGB(Time.time * .5f % 1, .8f, 1));
            else if (Statuses.Has(StatusType.Rage)) ModelFactory.Recolor(model, "coat", Color.Lerp(Color, Color.red, .5f + .5f * Mathf.Sin(Time.time * 12)));
            if (Statuses.Has(StatusType.Bubbles) && Random.value < dt * 10 && Simulated) Fx.Bubbles(transform.position + Vector3.up * 1.8f, 1);
            if (Statuses.Has(StatusType.Speed) && Random.value < dt * 12 && Simulated) Fx.Smoke(transform.position, 1, .2f, new Color(1, .9f, .4f, .4f));
            bool blink = Statuses.Has(StatusType.Invincible) && Mathf.Sin(Time.time * 30) > 0;
            foreach (var r in model.renderers) r.enabled = !blink;
            // Melee swing: sweep the weapon across the arc
            if (meleeModel != null)
                meleeModel.root.transform.localRotation = swingT > 0
                    ? Quaternion.Euler(-10, Mathf.Lerp(Melee.arcDegrees / 2, -Melee.arcDegrees / 2, 1 - swingT / .25f), 0)
                    : Quaternion.Euler(-60, 0, 0);
        }

        void UpdateNameTag()
        {
            if (nameTag == null) return;
            nameTag.text = string.IsNullOrEmpty(Nick) ? Name : $"{Name} · {Nick}";
            var cam = gm.Players.FirstViewCamera(this);
            nameTag.gameObject.SetActive(cam != null && gm.Players.Count > 1);
            if (cam != null) nameTag.transform.rotation = Quaternion.LookRotation(nameTag.transform.position - cam.transform.position);
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
            netAmmo = s.ammo; netMag = Mathf.Max(1, s.mag); netReload = s.reload;
            netCarryCount = s.carryCount; netCarryWeight = s.carryWeight;
            if (s.swing > swingT) swingT = s.swing;
            if (Kind == PlayerKind.Proxy) AimPoint = s.aim;
            if (Gun == null || Gun.id != s.gun) GiveGun(s.gun);
            if (Melee == null || Melee.id != s.melee) GiveMelee(s.melee);
            Statuses.ClearAll();
            foreach (var (type, left) in s.statuses) Statuses.Add(type, left);
            Kills = s.kills;
            // Our own player: we move it, but a big disagreement (respawn, teleport) snaps to the host.
            if (Kind == PlayerKind.LocalProxy && Vector3.Distance(s.pos, transform.position) > 4) Teleport(s.pos, false);
        }

        /// <summary>Client: the host's menu for this player (rows only; picking happens on the host).</summary>
        public void ApplyNetMenu(Menu m, string prompt)
        {
            OpenedMenu = m;
            NetPrompt = prompt;
        }

        public void NetTeleport(Vector3 at) => Teleport(at, false);
        public void NetPush(Vector3 delta) => push += delta;
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
            if (LocalMenu != null) { UpdateMenu(udt); UpdateVisual(dt); UpdateReticle(false); return; }
            visual.gameObject.SetActive(!IsOut);
            if (IsOut || gm.State != GameState.Playing) { UpdateReticle(false); return; }
            if (OpenedMenu == null) TickLook(dt);
            ResolveAim(LocalAim());
            if (OpenedMenu != null) { Gravity(dt, Vector3.zero); UpdateReticle(false); }
            else if (IsDowned) { Gravity(dt, MoveDir() * 1f); UpdateReticle(false); }
            else UpdateMovement(dt);
            Drip(dt);
            UpdateVisual(dt);
        }

        /// <summary>Client: this frame's input for the host (NetClient sends it).</summary>
        public NetInput CollectInput()
        {
            var i = new NetInput { playerIndex = Index, yaw = Yaw, pitch = Pitch, firstPerson = PrefersFirstPerson, pos = transform.position };
            if (LocalMenu != null || Rebinder.Busy || Controls == null) return i;   // our pause menu is open: stand still
            i.move = Controls.Move.ReadValue<Vector2>();
            foreach (Btn b in System.Enum.GetValues(typeof(Btn)))
            {
                if (b is Btn.Pause or Btn.View) continue;  // handled here
                if (Controls.Held(b)) i.held |= (ushort)(1 << (int)b);
                if (Controls.Pressed(b)) i.pressed |= (ushort)(1 << (int)b);
            }
            var a = Cam != null ? LocalAim() : default;
            i.aim = a;
            return i;
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

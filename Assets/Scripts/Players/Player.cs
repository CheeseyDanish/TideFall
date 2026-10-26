// TideWall 3D — a local co-op player: movement, aiming (twin-stick or mouse), guns, melee, interacting,
// carrying/throwing, health, being downed and revived, food and status effects.
using System.Collections.Generic;
using UnityEngine;

namespace TideWall
{
    [RequireComponent(typeof(CharacterController))]
    public class Player : MonoBehaviour
    {
        public const float MaxCarry = 3;
        public static readonly Color[] Colors = { new(.9f, .22f, .2f), new(.2f, .5f, .95f), new(.25f, .8f, .35f), new(.98f, .78f, .15f) };
        static readonly string[] Names = { "Red", "Blue", "Green", "Gold" };

        public int Index { get; private set; }
        public string Name => Names[Index];
        public Color Color => Colors[Index];
        public PlayerControls Controls { get; private set; }
        public PlayerCamera Cam { get; private set; }
        public PlayerHud Hud { get; private set; }
        public readonly StatusEffects Statuses = new();
        public readonly List<Carryable> Carrying = new();
        public float CarryWeight { get { float w = 0; foreach (var c in Carrying) if (c) w += c.weight; return w; } }

        public float Hp { get; private set; }
        public float MaxHp => db.playerMaxHp + gm.Team.PlayerHpBonus;
        public bool IsDowned { get; private set; }
        public bool IsOut { get; private set; }          // bled out: back at the next phase
        public bool IsTargetable => !IsDowned && !IsOut && gm.State == GameState.Playing;
        public float BleedLeft { get; private set; }
        public float ReviveProgress { get; private set; }
        public bool Ready { get; set; }
        public float Wet { get; private set; }
        public Menu OpenedMenu { get; private set; }
        public GunDef Gun { get; private set; }
        public MeleeDef Melee { get; private set; }
        public int Ammo => ammo.TryGetValue(Gun.id, out var a) ? a : 0;
        public int MagSize => Mathf.RoundToInt(Gun.magazine * gm.Team.Magazine);
        public float ReloadProgress => reloadT > 0 ? 1 - reloadT / Gun.reload : -1;
        public Vector3 AimPoint { get; private set; }
        public Vector3 AimDir { get; private set; } = Vector3.forward;
        public Vector2 AimScreen { get; private set; }
        public Interactable Focus { get; private set; }
        public int Kills, Severs;

        GameManager gm; GameDatabase db;
        CharacterController cc;
        ModelInstance model, gunModel, meleeModel;
        Transform visual;
        LineRenderer reticle;
        Light lantern;
        readonly HashSet<string> ownedGuns = new(), ownedMelee = new();
        readonly Dictionary<string, int> ammo = new();
        float fireCd, meleeCd, reloadT, swingT, dashT, dashCd, fireBuffer, yVel, menuRepeat, hurtFlash, dripAcc, revivingT;
        Vector3 dashDir, push;
        Enemy grabbedBy;
        Vector2 lastNav;

        // ------------------------------------------------------------------ setup
        public static Player Create(int index, PlayerControls controls, Vector3 spawn)
        {
            var go = new GameObject("Player " + (index + 1));
            go.transform.position = spawn;
            var cc = go.AddComponent<CharacterController>();
            cc.height = 1.8f; cc.radius = .4f; cc.center = new Vector3(0, .9f, 0); cc.slopeLimit = 50; cc.stepOffset = .4f;
            var p = go.AddComponent<Player>();
            p.Setup(index, controls);
            return p;
        }

        void Setup(int index, PlayerControls controls)
        {
            gm = GameManager.I; db = gm.DB;
            Index = index; Controls = controls;
            cc = GetComponent<CharacterController>();
            visual = new GameObject("Visual").transform;
            visual.SetParent(transform, false);
            model = ModelFactory.Spawn("Sailor", visual, Vector3.zero);
            ModelFactory.Recolor(model, "coat", Color);
            GiveGun(db.startGun);
            GiveMelee(db.startMelee);
            Hp = MaxHp;
            // Each player's aim reticle: a ring in their colour on the ground or on the limb they're aiming at.
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
            Cam = PlayerCamera.Create(this);
            Hud = PlayerHud.Create(this);
        }

        public bool OwnsGun(string id) => ownedGuns.Contains(id);
        public bool OwnsMelee(string id) => ownedMelee.Contains(id);

        public void GiveGun(string id)
        {
            var g = db.Gun(id); if (g == null) return;
            ownedGuns.Add(id);
            Gun = g;
            if (!ammo.ContainsKey(id)) ammo[id] = MagSize;
            reloadT = 0;
            if (gunModel != null) Destroy(gunModel.root);
            gunModel = ModelFactory.Spawn(g.model, Socket("Gun"), Vector3.zero);
        }

        public void GiveMelee(string id)
        {
            var m = db.Melee(id); if (m == null) return;
            ownedMelee.Add(id);
            Melee = m;
            if (meleeModel != null) Destroy(meleeModel.root);
            meleeModel = ModelFactory.Spawn(m.model, Socket("Melee"), Vector3.zero);
            meleeModel.root.transform.localRotation = Quaternion.Euler(-60, 0, 0);   // held up, ready to swing
        }

        Transform Socket(string name) => model.sockets.TryGetValue(name, out var s) ? s : visual;

        public void RefreshMaxHp() { if (!IsDowned && !IsOut) Hp = Mathf.Min(MaxHp, Hp + gm.DB.upgrades.Find(u => u.kind == UpgradeKind.PlayerMaxHp)?.amount ?? 0); }

        // ------------------------------------------------------------------ frame
        void Update()
        {
            float dt = Time.deltaTime;
            Statuses.Tick(dt);
            if (hurtFlash > 0) hurtFlash -= dt;
            HandleGlobalButtons();
            if (gm.Paused || dt <= 0) { UpdateReticle(false); return; }
            if (gm.State == GameState.GameOver) return;

            UpdateWet(dt);
            if (IsOut) { visual.gameObject.SetActive(false); reticle.enabled = false; return; }
            visual.gameObject.SetActive(true);

            if (IsDowned) { UpdateDowned(dt); return; }
            if (OpenedMenu != null) { UpdateMenu(dt); Gravity(dt, Vector3.zero); UpdateReticle(false); return; }

            UpdateAim();
            UpdateMovement(dt);
            UpdateCombat(dt);
            UpdateInteract(dt);
            UpdateVisual(dt);
        }

        void HandleGlobalButtons()
        {
            if (Controls.Pause.WasPressedThisFrame())
            {
                if (OpenedMenu != null) CloseMenu();
                else gm.TogglePause();
            }
            if (gm.State == GameState.GameOver && Controls.Interact.WasPressedThisFrame()) gm.Restart();
        }

        void UpdateWet(float dt)
        {
            if (Arena.InWater(transform.position) && Arena.Depth(transform.position) > .3f) Wet = 1;
            else Wet = Mathf.Max(0, Wet - db.wetDrainPerSecond * 1.5f * dt);
            dripAcc += Wet * 10 * dt;
            while (dripAcc >= 1) { dripAcc -= 1; Fx.Drip(transform.position + new Vector3(Random.Range(-.3f, .3f), Random.Range(.4f, 1.6f), Random.Range(-.3f, .3f))); }
        }

        // ------------------------------------------------------------------ aiming
        void UpdateAim()
        {
            var cam = Cam.Camera;
            Vector3 flatPos = transform.position;
            if (Controls.UsesMouse)
            {
                AimScreen = Controls.Point.ReadValue<Vector2>();
            }
            else
            {
                var stick = Controls.Aim.ReadValue<Vector2>();
                if (Statuses.Has(StatusType.ReverseControls)) stick = -stick;
                float mag = Mathf.Clamp01(stick.magnitude);
                if (mag > .2f) AimDir = new Vector3(stick.x, 0, stick.y).normalized;
                else
                {
                    var mv = Controls.Move.ReadValue<Vector2>();
                    if (mv.magnitude > .3f) AimDir = new Vector3(mv.x, 0, mv.y).normalized;
                }
                float dist = 2.5f + 9 * (mag > .2f ? mag : .6f);
                AimScreen = cam.WorldToScreenPoint(flatPos + AimDir * dist + Vector3.up * .8f);
            }
            // What's under the reticle on screen? That's what the shot goes for, limb and all.
            var ray = cam.ScreenPointToRay(AimScreen);
            var hits = Physics.RaycastAll(ray, 200, ~0, QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            bool found = false;
            foreach (var h in hits)
            {
                if (h.collider.GetComponentInParent<Player>() || h.collider.GetComponentInParent<Wall>()) continue;
                var e = h.collider.GetComponentInParent<Enemy>();
                if (e != null && !e.Dead) { AimPoint = h.point; found = true; break; }
                if (h.collider.GetComponentInParent<GasCan>() is { Holder: null }) { AimPoint = h.point; found = true; break; }
                if (h.collider.isTrigger || h.collider.GetComponentInParent<Corpse>()) continue;
                AimPoint = h.point + Vector3.up * .5f; found = true; break;
            }
            if (!found) AimPoint = ray.GetPoint(30);
            if (!Controls.UsesMouse) AimAssist();
            var d = AimPoint - flatPos; d.y = 0;
            if (Controls.UsesMouse && d.sqrMagnitude > .04f) AimDir = d.normalized;
            UpdateReticle(true);
        }

        /// <summary>Controllers have no cursor: if the stick points near an enemy, aim at its body.</summary>
        void AimAssist()
        {
            var flatAim = AimPoint - transform.position; flatAim.y = 0;
            foreach (var e in GameManager.Enemies)
                if (e != null && !e.Dead && e.GetComponentInChildren<Collider>() is { } c && c.bounds.Contains(AimPoint)) return;
            Enemy best = null; float bestAngle = 9;
            foreach (var e in GameManager.Enemies)
            {
                if (e == null || e.Dead) continue;
                var to = e.transform.position - transform.position; to.y = 0;
                if (to.magnitude > 16) continue;
                float a = Vector3.Angle(AimDir, to);
                if (a < bestAngle) { bestAngle = a; best = e; }
            }
            if (best != null) AimPoint = ModelFactory.Bounds(best.gameObject).center;
        }

        void UpdateReticle(bool show)
        {
            reticle.enabled = show && !Statuses.Has(StatusType.FirstPerson);
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

        void UpdateMovement(float dt)
        {
            var mv = Controls.Move.ReadValue<Vector2>();
            if (Statuses.Has(StatusType.ReverseControls)) mv = -mv;
            var dir = new Vector3(mv.x, 0, mv.y);
            if (dir.magnitude > 1) dir.Normalize();
            if (grabbedBy != null) dir = Vector3.zero;
            dashCd -= dt;
            if (Controls.Dash.WasPressedThisFrame() && dashCd <= 0 && grabbedBy == null && dir.sqrMagnitude > .01f)
            {
                dashT = db.dashSeconds; dashCd = db.dashCooldown; dashDir = dir.normalized;
                Fx.Smoke(transform.position, 4, .3f, new Color(.9f, .85f, .7f, .5f));
            }
            Vector3 vel = dir * db.playerSpeed * SpeedMultiplier();
            if (dashT > 0) { dashT -= dt; vel = dashDir * db.dashSpeed; }
            vel += push / Mathf.Max(dt, .0001f); push = Vector3.zero;
            Gravity(dt, vel);
            if (Controls.UsesMouse || Controls.Aim.ReadValue<Vector2>().magnitude > .2f || dir.sqrMagnitude > .01f)
                visual.rotation = Quaternion.RotateTowards(visual.rotation, Quaternion.LookRotation(AimDir), 900 * dt);
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

        public void Push(Vector3 delta) => push += delta;

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
                if (Controls.Melee.WasPressedThisFrame() || Controls.Interact.WasPressedThisFrame() || Controls.Fire.WasPressedThisFrame()) grabbedBy.Struggle();
                return;
            }
            if (Controls.Swap.WasPressedThisFrame()) CycleGun();
            if (Controls.Reload.WasPressedThisFrame()) StartReload();

            if (Controls.Fire.WasPressedThisFrame()) fireBuffer = .25f;
            if (Carrying.Count > 0)
            {
                if (fireBuffer > 0) { fireBuffer = 0; ThrowTop(); }
            }
            else if ((Controls.Fire.IsPressed() || fireBuffer > 0) && fireCd <= 0 && reloadT <= 0)
            {
                if (Ammo <= 0) StartReload();
                else Shoot();
            }
            if (Controls.Melee.WasPressedThisFrame() && meleeCd <= 0)
            {
                meleeCd = Melee.cooldown; swingT = .25f;
                Sfx.Play("swing");
                int hit = Combat.Melee(this, Melee, transform.position + Vector3.up * .9f + AimDir * .3f, AimDir, DamageMult(true));
                if (hit > 0) { Sfx.Play("hit"); Shake(.08f); }
            }
        }

        void Shoot()
        {
            fireBuffer = 0;
            float rate = Gun.fireRate * (Statuses.Has(StatusType.SugarRush) ? 1.25f : 1);
            fireCd = 1 / rate;
            ammo[Gun.id] = Ammo - 1;
            var origin = gunModel.sockets.TryGetValue("Muzzle", out var mz) ? mz.position : transform.position + Vector3.up * 1.2f + AimDir * .6f;
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

        public void OnHitEnemy(Enemy e, float dmg, bool crit) => Hud.HitMarker(crit);
        public void OnSever(Enemy e, LimbDef limb) { Severs++; Hud.Toast($"{e.Def.displayName}: {limb.part} off!", new Color(1, .85f, .5f)); }
        public void OnKill(Enemy e) => Kills++;

        // ------------------------------------------------------------------ interaction & carrying
        void UpdateInteract(float dt)
        {
            Focus = Interactable.Nearest(this);
            var downedMate = DownedTeammateNearby();
            if (downedMate != null && Controls.Interact.IsPressed())
            {
                revivingT += dt;
                downedMate.ReviveTick(dt);
                return;
            }
            revivingT = 0;
            if (!Controls.Interact.WasPressedThisFrame()) return;
            if (Focus != null) Focus.Interact(this);
            else if (Carrying.Count > 0) Drop(Carrying[^1]);
        }

        Player DownedTeammateNearby()
        {
            foreach (var p in gm.Players.All)
                if (p != this && p.IsDowned && Vector3.Distance(p.transform.position, transform.position) < 1.9f) return p;
            return null;
        }

        public void PickUp(Carryable c)
        {
            if (CarryWeight + c.weight > MaxCarry) return;
            Carrying.Add(c);
            c.Holder = this;
            foreach (var th in c.GetComponents<Thrown>()) Destroy(th);
            c.transform.SetParent(visual, true);
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
            var p = transform.position + AimDir * .9f;
            c.transform.position = new Vector3(p.x, Arena.GroundY(p.z), p.z);
            LayoutCarried();
        }

        void ThrowTop()
        {
            var c = Carrying[^1];
            Carrying.RemoveAt(Carrying.Count - 1);
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
            if (knock.HasValue) push += knock.Value * .15f;
            if (!quiet) { hurtFlash = .25f; Sfx.Play("hurt", .7f, .1f); Shake(.15f); }
            if (Hp <= 0) GoDown(what);
        }

        public void Heal(float amount) { if (!IsDowned && !IsOut) Hp = Mathf.Min(MaxHp, Hp + amount); }

        void GoDown(string what)
        {
            Hp = 0; IsDowned = true; BleedLeft = db.bleedOutSeconds; ReviveProgress = 0;
            for (int i = Carrying.Count - 1; i >= 0; i--) Drop(Carrying[i]);
            if (grabbedBy != null) Release(grabbedBy);
            CloseMenu();
            visual.localRotation = Quaternion.Euler(-80, visual.eulerAngles.y, 0);
            gm.Banner($"{Name.ToUpperInvariant()} IS DOWN", what != null ? $"Taken out by {what}. Hold Interact next to them to revive!" : "Hold Interact next to them to revive!");
            gm.Players.CheckWipe();
        }

        void UpdateDowned(float dt)
        {
            BleedLeft -= dt;
            // Crawl slowly toward safety
            var mv = Controls.Move.ReadValue<Vector2>();
            Gravity(dt, new Vector3(mv.x, 0, mv.y) * 1f);
            reticle.enabled = false;
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
            visual.localRotation = Quaternion.Euler(0, visual.eulerAngles.y, 0);
            Statuses.Add(StatusType.Invincible, 2);
            Sfx.Play("bell", .4f, .2f);
        }

        /// <summary>Back in the game at the start of a phase (if they bled out).</summary>
        public void Respawn(Vector3 at)
        {
            if (!IsOut && !IsDowned) return;
            cc.enabled = false; transform.position = at; cc.enabled = true;
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
        public void OpenMenu(Menu m) { OpenedMenu = m; Sfx.Play("ui"); }
        public void CloseMenu() { OpenedMenu = null; }

        void UpdateMenu(float dt)
        {
            var nav = Controls.Move.ReadValue<Vector2>();
            menuRepeat -= dt;
            int dir = nav.y > .5f ? -1 : nav.y < -.5f ? 1 : 0;
            if (dir != 0 && (Mathf.Abs(lastNav.y) <= .5f || menuRepeat <= 0))
            {
                OpenedMenu.Move(dir);
                menuRepeat = Mathf.Abs(lastNav.y) <= .5f ? .35f : .1f;
                Sfx.Play("ui", .5f);
            }
            lastNav = nav;
            if (Controls.Back.WasPressedThisFrame() || Controls.Melee.WasPressedThisFrame()) { CloseMenu(); return; }
            if (Controls.Interact.WasPressedThisFrame() || Controls.Fire.WasPressedThisFrame())
            {
                var item = OpenedMenu.Items[OpenedMenu.Selected];
                var why = item.Blocked?.Invoke();
                if (why != null) { Sfx.Play("deny"); Toast(why, new Color(1, .6f, .6f)); return; }
                item.OnPick?.Invoke();
                int sel = OpenedMenu.Selected;
                OpenedMenu = OpenedMenu.Rebuild?.Invoke() ?? OpenedMenu;
                OpenedMenu.Selected = Mathf.Clamp(sel, 0, OpenedMenu.Items.Count - 1);
            }
        }

        // ------------------------------------------------------------------ look
        void UpdateVisual(float dt)
        {
            float scale = Statuses.Has(StatusType.Giant) ? 1.45f : Statuses.Has(StatusType.Tiny) ? .6f : 1;
            visual.localScale = Vector3.Lerp(visual.localScale, Vector3.one * scale, dt * 6);
            if (Statuses.Has(StatusType.Rainbow)) ModelFactory.Recolor(model, "coat", Color.HSVToRGB(Time.time * .5f % 1, .8f, 1));
            else if (Statuses.Has(StatusType.Rage)) ModelFactory.Recolor(model, "coat", Color.Lerp(Color, Color.red, .5f + .5f * Mathf.Sin(Time.time * 12)));
            if (Statuses.Has(StatusType.Bubbles) && Random.value < dt * 10) Fx.Bubbles(transform.position + Vector3.up * 1.8f, 1);
            if (Statuses.Has(StatusType.Speed) && Random.value < dt * 12) Fx.Smoke(transform.position, 1, .2f, new Color(1, .9f, .4f, .4f));
            bool blink = Statuses.Has(StatusType.Invincible) && Mathf.Sin(Time.time * 30) > 0;
            foreach (var r in model.renderers) r.enabled = !blink;
            // Melee swing: sweep the weapon across the arc
            if (meleeModel != null)
                meleeModel.root.transform.localRotation = swingT > 0
                    ? Quaternion.Euler(-10, Mathf.Lerp(Melee.arcDegrees / 2, -Melee.arcDegrees / 2, 1 - swingT / .25f), 0)
                    : Quaternion.Euler(-60, 0, 0);
        }

        public float HurtFlash => hurtFlash;
        public void Shake(float amount) { if (Cam != null) Cam.AddShake(amount); }
        public void Toast(string msg, Color? color = null) { if (Hud != null) Hud.Toast(msg, color ?? Color.white); }

        void OnDestroy()
        {
            Controls?.Dispose();
            if (reticle) Destroy(reticle.gameObject);
            if (Cam) Destroy(Cam.gameObject);
            if (Hud) Destroy(Hud.gameObject);
        }
    }
}

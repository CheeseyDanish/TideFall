// TideWall 3D — Player: being the Tide Caller. No body and nothing can touch you: you move a marker over the water in an
// isometric view of the whole sandbar (or a closer one, on the View button), the same way on a keyboard, a controller and online (the marker is this player's own position, so it uses the
// usual numbered commands and prediction). Or you take over a creature, and drive it from just behind.
// (One part of the Player class; Lighthouse.Caller.cs has the rules.)
using UnityEngine;

namespace TideWall
{
    public partial class Player
    {
        public bool IsCaller { get; private set; }
        LineRenderer callerRing, callerTarget;
        float callCd;
        bool navHeld;
        readonly LineRenderer[] sailorRings = new LineRenderer[PlayerManager.MaxPlayers];
        /// <summary>This machine's choice of view from above: isometric (the whole battlefield) or close. Only the camera cares.</summary>
        public bool CallerIso { get; private set; } = true;

        public void ToggleCallerView()
        {
            CallerIso = !CallerIso;
            Toast(CallerIso ? "Isometric view" : "Close view", new Color(.8f, .9f, 1f));
            Sfx.Play("ui");
        }

        /// <summary>Isometric: the view holds its angle, and Melee swings it round a quarter turn. True if that's the view now.</summary>
        bool TickCallerTurn()
        {
            if (!IsCaller || !CallerIso || gm.Level is Lighthouse { Ridden: not null }) return false;
            Yaw = 45 + 90 * Mathf.Round((Yaw - 45) / 90);
            if (Controls.Pressed(Btn.Melee)) Yaw += 90;
            Yaw = Mathf.Repeat(Yaw, 360);
            return true;
        }

        public void SetCallerRole(bool on)
        {
            if (IsCaller == on) return;
            IsCaller = on;
            if (on)
            {
                if (Simulated)
                {
                    fishing = null; reload = null;
                    Building = null;
                    if (ghost != null) { Destroy(ghost); ghost = null; }
                    for (int i = Carrying.Count - 1; i >= 0; i--) if (Carrying[i]) Drop(Carrying[i]);
                    if (OpenedMenu != null) CloseMenu();
                    Focus = null;
                }
                Wet = 0;
                cc.enabled = false;
                if (HasView) Yaw = 45;
                gm.Level.lanterns.Remove(lantern);
            }
            else
            {
                cc.enabled = true;
                if (!gm.Level.lanterns.Contains(lantern)) gm.Level.lanterns.Add(lantern);
                if (Simulated) Place(gm.Level.SpawnPoint(Index));
            }
            lantern.enabled = !on;
            if (nameTag != null) nameTag.enabled = !on;
        }

        /// <summary>The frame, on the machine that simulates this player.</summary>
        void CallerUpdate(float dt, float udt)
        {
            if (OpenedMenu != null) UpdateMenu(udt);
            if (Kind == PlayerKind.Local)
            {
                if (OpenedMenu == null || !OpenedMenu.System) TickLook(dt);
                StepMove(Controls.Move.ReadValue<Vector2>(), false, Yaw, dt);
            }
            else { Yaw = Remote.Yaw; Pitch = Remote.Pitch; }
            AimDir = Quaternion.Euler(0, Yaw, 0) * Vector3.forward;
            callCd -= dt;
            if (OpenedMenu == null && gm.Level is Lighthouse l) CallerButtons(l);
        }

        void CallerButtons(Lighthouse l)
        {
            var e = l.Ridden;
            if (e != null)
            {
                if (Held(Btn.Fire)) e.RiderStrike();
                if (Pressed(Btn.Dash)) e.RiderDash();
                if (Pressed(Btn.Interact) || Pressed(Btn.Back)) l.EndRide();
                return;
            }
            var nav = NavInput();
            bool navNow = Mathf.Abs(nav.x) > .5f || Mathf.Abs(nav.y) > .5f;
            if (navNow && !navHeld) l.CyclePick(nav.x < -.5f || nav.y > .5f ? -1 : 1);
            navHeld = navNow;
            if (Pressed(Btn.Swap)) l.CyclePick(1);
            if (Pressed(Btn.Fire) || (Held(Btn.Fire) && callCd <= 0))
            {
                l.TryCall(transform.position, Pressed(Btn.Fire));
                callCd = .3f;
            }
            if (Pressed(Btn.Sight)) l.TryRally(transform.position);
            if (Pressed(Btn.Interact)) l.TryRide(transform.position);
            if (Pressed(Btn.Back) && l.Wave == 0) l.SetCaller(null);
        }

        /// <summary>Where the driven creature is told to go: the stick, relative to the view.</summary>
        public Vector3 RideDir()
        {
            var mv = Controls != null ? Controls.Move.ReadValue<Vector2>() : Remote?.Move ?? Vector2.zero;
            return Vector3.ClampMagnitude(Quaternion.Euler(0, Yaw, 0) * new Vector3(mv.x, 0, mv.y), 1);
        }

        /// <summary>One movement step of the marker (from StepMove, so a client predicts it exactly as the host runs it).</summary>
        void CallerMove(Vector2 move, float yaw, float dt)
        {
            if (dt <= 0 || (gm.Level is Lighthouse { Ridden: not null })) return;
            var at = transform.position + Quaternion.Euler(0, yaw, 0) * new Vector3(move.x, 0, move.y) * db.lastLight.callerMoveSpeed * dt;
            at.y = 0;
            transform.position = Vector3.ClampMagnitude(at, Arena.IslandBound);
        }

        /// <summary>The Caller's view: high above the marker, or just behind the creature they're driving.</summary>
        public void CallerView(out Vector3 focus, out Quaternion look, out float distance, out float orthoSize)
        {
            orthoSize = 0;
            if (gm.Level is Lighthouse { Ridden: not null } l)
            {
                var e = l.Ridden;
                focus = e.transform.position + Vector3.up * (1.2f + e.Radius);
                look = Quaternion.Euler(Mathf.Clamp(Pitch, 5, 50), Yaw, 0);
                distance = 5 + e.Radius * 2;
                return;
            }
            focus = transform.position + RenderOffset;
            focus.y = Mathf.Max(Arena.WaterLevel, Arena.GroundAt(focus));
            if (CallerIso)
            {
                look = Quaternion.Euler(35.26f, Yaw, 0);
                distance = db.lastLight.callerIsoDistance;
                orthoSize = db.lastLight.callerIsoSize;
                return;
            }
            look = Quaternion.Euler(52, Yaw, 0);
            distance = db.lastLight.callerViewDistance;
        }

        /// <summary>Rings on the water: where a call would land, and what "take over" would pick. Only this player's camera sees them.</summary>
        void UpdateCallerRings()
        {
            bool show = IsCaller && HasView && gm.Level is Lighthouse { Ridden: null } && ShownMenu == null;
            if (!show)
            {
                if (callerRing != null) callerRing.enabled = callerTarget.enabled = false;
                foreach (var r in sailorRings) if (r != null) r.enabled = false;
                return;
            }
            if (callerRing == null) { callerRing = MakeRing("Caller Ring"); callerTarget = MakeRing("Caller Target"); }
            var l = (Lighthouse)gm.Level;
            var at = transform.position;
            at.y = Mathf.Max(Arena.WaterLevel, Arena.GroundAt(at)) + .15f;
            callerRing.startColor = callerRing.endColor = l.CallBlocked(at) == null ? new Color(.4f, 1, .9f) : new Color(1, .35f, .3f);
            DrawRing(callerRing, at, 1.6f);
            var e = l.RideLeft > 0 ? null : l.RideTarget(transform.position);
            callerTarget.enabled = e != null;
            if (e != null) DrawRing(callerTarget, e.transform.position + Vector3.up * .3f, e.Radius + .7f);
            // Each sailor, with the circle a creature can't be called inside
            for (int i = 0; i < sailorRings.Length; i++)
            {
                var s = gm.Players.ByIndex(i);
                bool on = s != null && s != this && !s.IsOut;
                if (on && sailorRings[i] == null) { sailorRings[i] = MakeRing("Sailor Ring"); sailorRings[i].widthMultiplier = .14f; sailorRings[i].startColor = sailorRings[i].endColor = Colors[i]; }
                if (sailorRings[i] == null) continue;
                sailorRings[i].enabled = on;
                if (!on) continue;
                var c = s.transform.position; c.y = Mathf.Max(Arena.WaterLevel, c.y) + .2f;
                DrawRing(sailorRings[i], c, db.lastLight.callerSafeRadius);
            }
        }

        LineRenderer MakeRing(string name)
        {
            var r = new GameObject(name).AddComponent<LineRenderer>();
            r.gameObject.layer = ViewLayer + Index;
            r.loop = true; r.positionCount = 28; r.widthMultiplier = .22f; r.useWorldSpace = true;
            r.material = Mats.ParticleAdditive;
            r.startColor = r.endColor = new Color(1, .85f, .4f);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return r;
        }

        static void DrawRing(LineRenderer r, Vector3 at, float radius)
        {
            for (int i = 0; i < r.positionCount; i++)
            {
                float a = i / (float)r.positionCount * Mathf.PI * 2;
                r.SetPosition(i, at + new Vector3(Mathf.Cos(a) * radius, 0, Mathf.Sin(a) * radius));
            }
        }
    }
}

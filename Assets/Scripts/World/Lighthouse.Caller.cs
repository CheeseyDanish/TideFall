// TideWall 3D — The Last Light: Tide Caller, the one-against-the-rest version. Whoever picks up the conch stops being a
// sailor and looks down on the sandbar from above. They spend brine to call creatures up out of the water, rally the ones
// nearby to a point, or take one over and drive it themselves. Everyone else keeps the lamp burning: if the light lasts
// callerTides tides the sailors win; if the lighthouse falls, the lamp stays dark or everyone goes down, the Caller does.
// The Caller leaving hands the sea back to the usual waves, and the conch goes back on its rock for anyone to take.
// (One part of the Lighthouse class. The numbers are GameDatabase.lastLight; the Caller's controls are Player.Caller.cs.)
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TideWall
{
    public partial class Lighthouse
    {
        public const string HeldReason = "The light held.";

        /// <summary>This run is Tide Caller (mirrored online).</summary>
        public bool Versus { get; private set; }
        public Player Caller { get; private set; }
        public bool CallerActive => Versus && Caller != null;
        public float Brine { get; private set; }
        public float MaxBrine { get; private set; } = 1;
        public EnemyDef Pick { get; private set; }
        /// <summary>The creature the Caller is driving (null: looking down from above).</summary>
        public Enemy Ridden { get; private set; }
        public float RallyLeft { get; private set; }
        public float RideLeft { get; private set; }

        uint rosterMask;
        Enemy boss;
        bool hadCaller;
        GameObject conch;

        void BeginVersus(SaveData save)
        {
            Versus = save != null && save.versus;
            SyncConch();
        }

        /// <summary>The first tide waits until someone's picked up the conch.</summary>
        bool Mustering => Versus && Caller == null && Wave == 0;

        void SyncConch()
        {
            if (!Versus) return;
            if (conch == null)
            {
                conch = new GameObject("Conch");
                conch.transform.SetParent(transform, false);
                conch.transform.position = new Vector3(5.5f, Arena.IslandTop, -2);
                ModelFactory.Spawn("Rock", conch.transform, Vector3.zero, .6f);
                ModelFactory.Spawn("Conch", conch.transform, Vector3.up * .55f, 1.2f);
                Interactable.Add<ConchShell>(conch, "THE CONCH", 2.8f);
            }
            bool free = Caller == null;
            if (conch.activeSelf != free) conch.SetActive(free);
        }

        /// <summary>Who calls the tide: a sailor taking the conch, or null to give it back.</summary>
        public void SetCaller(Player p)
        {
            EndRide();
            if (Caller != null) Caller.SetCallerRole(false);
            Caller = p;
            hadCaller = p != null;
            if (p != null)
            {
                if (Keeper == p) SetKeeper(null);
                p.SetCallerRole(true);
                var from = p.transform.position; from.y = 0;
                p.Place((from.sqrMagnitude > .01f ? from.normalized : Vector3.forward) * def.spawnRadius * .7f);
                if (Wave == 0) { Brine = def.brineStart; StateLeft = def.firstSlackSeconds; }
                MaxBrine = def.brineCapBase + def.brineCapPerTide * Wave;
                gm.Banner($"{p.Name.ToUpperInvariant()} CALLS THE TIDE", $"Keep the light burning for {def.callerTides} tides.");
                Sfx.Play("horn", 1, .1f);
            }
            UpdateRoster();
            SyncConch();
        }

        void UpdateCaller(float dt)
        {
            if (!Versus) return;
            if (hadCaller && Caller == null)
            {
                // They left: the sea goes back to its usual self until someone else takes the conch
                hadCaller = false;
                EndRide();
                gm.Banner("THE TIDE CALLS ITSELF", "The Caller's gone. The conch is back on its rock.");
                SyncConch();
            }
            if (Caller == null) return;
            MaxBrine = def.brineCapBase + def.brineCapPerTide * Wave;
            Brine = Mathf.Min(MaxBrine, Brine + dt * (def.brineBase + def.brinePerTide * Wave));
            RallyLeft = Mathf.Max(0, RallyLeft - dt);
            RideLeft = Mathf.Max(0, RideLeft - dt);
            if (Ridden == null || Ridden.Dead) { if (ridden) EndRide(); }
            UpdateRoster();
        }
        bool ridden;

        void UpdateRoster()
        {
            uint mask = 0;
            var all = gm.DB.enemies;
            for (int i = 0; i < all.Count && i < 32; i++)
            {
                var e = all[i];
                if (e.cost > 0 && e.behaviour != EnemyBehaviour.Boss && e.minDay <= gm.Waves.Day && e.minWave <= Wave + 3) mask |= 1u << i;
            }
            rosterMask = mask;
            if (Pick == null || !Roster().Contains(Pick)) { var r = Roster(); Pick = r.Count > 0 ? r[0] : null; }
        }

        /// <summary>What the Caller can call right now, cheapest first.</summary>
        public List<EnemyDef> Roster()
        {
            var r = new List<EnemyDef>();
            var all = gm.DB.enemies;
            for (int i = 0; i < all.Count && i < 32; i++) if ((rosterMask & (1u << i)) != 0) r.Add(all[i]);
            r.Sort((a, b) => a.cost.CompareTo(b.cost));
            return r;
        }

        public void CyclePick(int step)
        {
            var r = Roster();
            if (r.Count == 0) return;
            Pick = r[(int)Mathf.Repeat(r.IndexOf(Pick) + step, r.Count)];
            Sfx.Play("ui", .6f);
        }

        // ------------------------------------------------------------------ what the Caller can do
        /// <summary>Why a creature can't be called here right now, or null if it can.</summary>
        public string CallBlocked(Vector3 at)
        {
            if (State == Tide.Slack) return "Slack water: the sea's saving its brine";
            if (Arena.Depth(at) < def.callerMinDepth) return "Too shallow: call them from the water";
            foreach (var p in gm.Players.All)
            {
                if (p == Caller || p.IsOut) continue;
                var d = p.transform.position - at; d.y = 0;
                if (d.magnitude < def.callerSafeRadius) return "Too close to a sailor";
            }
            return null;
        }

        /// <summary>Spend brine: one creature up out of the water here. True if it came.</summary>
        public bool TryCall(Vector3 at, bool loud)
        {
            string why = CallBlocked(at);
            if (why == null && Pick == null) why = "Nothing to call";
            if (why == null && Brine < Pick.cost) why = "Not enough brine";
            if (why == null && GameManager.Enemies.Count >= gm.DB.maxAlive) why = "The sea's full";
            if (why != null) { if (loud && Caller != null) { Caller.Toast(why, new Color(1, .6f, .5f)); Sfx.Play("deny", .6f); } return false; }
            Brine -= Pick.cost;
            at.y = Pick.behaviour == EnemyBehaviour.Flyer ? 9 : Arena.GroundAt(at);
            gm.Waves.SpawnExtra(Pick, at);
            Fx.Splash(new Vector3(at.x, Arena.WaterLevel, at.z), 8);
            Sfx.Play("splash", .7f, .15f);
            return true;
        }

        /// <summary>Everything nearby makes for this spot, faster than it would on its own.</summary>
        public bool TryRally(Vector3 at)
        {
            if (RallyLeft > 0 || Brine < def.rallyCost) { Sfx.Play("deny", .6f); return false; }
            Brine -= def.rallyCost;
            RallyLeft = def.rallyCooldown;
            int n = 0;
            foreach (var e in GameManager.Enemies)
            {
                if (e == null || e.Dead || !CanCommand(e) || e.Def.behaviour == EnemyBehaviour.Boss) continue;
                var d = e.transform.position - at; d.y = 0;
                if (d.magnitude > def.rallyRadius) continue;
                e.Order(at, def.rallySeconds);
                n++;
            }
            Fx.Splash(new Vector3(at.x, Mathf.Max(Arena.WaterLevel, Arena.GroundAt(at)), at.z), 14);
            Sfx.Play("horn", .5f, .15f);
            Caller?.Toast($"{n} creature{(n == 1 ? "" : "s")} rally", new Color(.5f, .95f, .9f));
            return true;
        }

        static bool CanCommand(Enemy e) => e.Def.behaviour is not (EnemyBehaviour.Flyer or EnemyBehaviour.Clam);

        /// <summary>The creature "take over" would pick from here.</summary>
        public Enemy RideTarget(Vector3 at)
        {
            Enemy best = null; float bd = def.rideRange;
            foreach (var e in GameManager.Enemies)
            {
                if (e == null || e.Dead || !CanCommand(e)) continue;
                var d = e.transform.position - at; d.y = 0;
                if (d.magnitude < bd) { bd = d.magnitude; best = e; }
            }
            return best;
        }

        public bool TryRide(Vector3 at)
        {
            var e = RideLeft > 0 ? null : RideTarget(at);
            if (e == null || Caller == null) { Sfx.Play("deny", .6f); return false; }
            Ridden = e; ridden = true;
            e.SetRider(Caller);
            Sfx.Play("growl", .8f, .1f);
            return true;
        }

        public void EndRide()
        {
            if (!ridden) return;
            ridden = false;
            if (Ridden != null)
            {
                Ridden.SetRider(null);
                if (Caller != null) { var at = Ridden.transform.position; at.y = 0; Caller.Place(at); }
            }
            Ridden = null;
            RideLeft = def.rideCooldown;
        }

        /// <summary>The sailors got the one the Caller was driving: a bounty, and the Caller's thrown out for a while.</summary>
        public void OnRiddenKilled(Enemy e, Player killer)
        {
            EndRide();
            RideLeft = def.rideKnockout;
            SandDollarPickup.Drop(e.transform.position + Vector3.up, def.rideBounty, killer);
            gm.Banner("THE CALLER'S THROWN!", killer != null ? $"{killer.Name} gets the bounty." : "A bounty on the sand.");
            Sfx.Play("jackpot", .5f, .1f);
        }

        /// <summary>A sailor down feeds the sea.</summary>
        public override void OnPlayerDown(Player p)
        {
            if (!CallerActive) return;
            Brine = Mathf.Min(MaxBrine, Brine + def.brinePerDown);
            Caller.Toast($"{p.Name} is down: +{def.brinePerDown:0} brine", new Color(.5f, .95f, .9f));
        }

        TMPro.TextMeshPro riderMark;

        /// <summary>Everyone can see which creature has the Caller in it: that's the one worth killing.</summary>
        void UpdateRiderMark()
        {
            bool on = Ridden != null && !Ridden.Dead;
            if (!on) { if (riderMark != null) riderMark.gameObject.SetActive(false); return; }
            if (riderMark == null)
            {
                riderMark = Ui.Label(transform, "Rider Mark", Vector3.zero, .55f, new Color(1, .45f, .35f));
                riderMark.text = "<b>TIDE CALLER</b>";
                Billboard.Add(riderMark.gameObject);
            }
            riderMark.gameObject.SetActive(true);
            if (Caller != null) riderMark.gameObject.layer = Player.TagLayer + Caller.Index;   // for the sailors: not across the driver's own view
            riderMark.transform.position = Ridden.transform.position + Vector3.up * (2.2f + Ridden.Radius * 1.5f);
        }

        /// <summary>A tide's over. True if that was the last one, and the sailors have won.</summary>
        bool LightHeld()
        {
            if (!Versus || Wave < def.callerTides) return false;
            gm.GameOver(HeldReason);
            return true;
        }

        public override string GameOverTitle(Player p)
        {
            if (!Versus) return null;
            bool held = gm.GameOverReason == HeldReason;
            if (p != null && p.IsCaller) return held ? "THE SAILORS HOLD" : "YOU DROWNED THE LIGHT";
            return held ? "THE LIGHT HOLDS" : "THE TIDE CALLER WINS";
        }

        /// <summary>What the Caller's HUD says the buttons do.</summary>
        public string CallerHelp(Player p)
        {
            if (Ridden != null)
                return $"Move · {p.Glyph(Btn.Fire)} strike · {p.Glyph(Btn.Dash)} lunge · {p.Glyph(Btn.Interact)} let go";
            var target = RideLeft > 0 ? null : RideTarget(p.transform.position);
            string ride = target != null ? $"{p.Glyph(Btn.Interact)} take over the {target.Def.displayName}" : $"<color=#94a3b8>{p.Glyph(Btn.Interact)} take over (get close)</color>";
            string rally = RallyLeft > 0 ? $"<color=#94a3b8>rally {Mathf.CeilToInt(RallyLeft)}s</color>" : $"{p.Glyph(Btn.Sight)} rally ({def.rallyCost})";
            string back = Wave == 0 ? $" · {p.Glyph(Btn.Back)} put the conch down" : "";
            string view = p.CallerIso ? $"{p.Glyph(Btn.Melee)} turn · {p.Glyph(Btn.View)} close view" : $"{p.Glyph(Btn.View)} isometric";
            return $"Move · {view} · {p.Glyph(Btn.Fire)} call · {p.Glyph(Btn.Swap)} next creature · {rally} · {ride}{back}";
        }

        // ------------------------------------------------------------------ online
        void WriteCaller(BinaryWriter w)
        {
            w.Write(Versus);
            if (!Versus) return;
            w.Write((sbyte)(Caller != null ? Caller.Index : -1));
            w.Write(Brine); w.Write(MaxBrine); w.Write(rosterMask);
            w.Write((short)(Pick != null ? gm.DB.enemies.IndexOf(Pick) : -1));
            int rid = Ridden != null && Ridden.TryGetComponent<NetId>(out var n) ? n.Id : 0;
            w.Write(rid);
            w.Write(RallyLeft); w.Write(RideLeft);
        }

        void ReadCaller(BinaryReader r)
        {
            bool versus = r.ReadBoolean();
            if (!versus) return;
            Versus = true;
            int index = r.ReadSByte();
            Brine = r.ReadSingle(); MaxBrine = r.ReadSingle(); rosterMask = r.ReadUInt32();
            int pick = r.ReadInt16();
            Pick = pick >= 0 && pick < gm.DB.enemies.Count ? gm.DB.enemies[pick] : null;
            int rid = r.ReadInt32();
            RallyLeft = r.ReadSingle(); RideLeft = r.ReadSingle();
            Caller = index >= 0 ? gm.Players.ByIndex(index) : null;
            foreach (var p in gm.Players.All) p.SetCallerRole(p == Caller);
            if (rid == 0) Ridden = null;
            else if (Ridden == null || !Ridden.TryGetComponent<NetId>(out var n) || n.Id != rid)
            {
                var found = NetId.All.Find(x => x.Id == rid);
                Ridden = found != null ? found.GetComponent<Enemy>() : null;
            }
            SyncConch();
        }
    }
}

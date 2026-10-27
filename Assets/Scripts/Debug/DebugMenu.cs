// TideWall 3D — the debug menu (F1, or Debug tools in the pause menu): playtesting shortcuts. Switches (god mode,
// infinite ammo, noclip, frozen creatures, collider outlines, stats overlay, time scale), money and levels, spawning any
// creature where you aim, skipping phases and days, the wall and powerplant, Part II's stages, objectives and endings,
// tidal cards, the casino, and an editor for the legacy file that only writes when you say so.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TideWall
{
    public static class DebugMenu
    {
        public const string Tag = "DEBUG";
        static readonly float[] Speeds = { 0, .25f, .5f, 1, 2, 4 };
        static readonly int[] SpawnCounts = { 1, 3, 5, 10 };
        static int spawnCount;
        static int dayPick = 1, stagePick;
        static Legacy draft;

        static string OnOff(bool v) => v ? "< On >" : "< Off >";

        static void Toggle(Menu m, string label, Func<bool> get, Action<bool> set, string detail)
        {
            var it = m.Add(label, OnOff(get()), detail);
            void Flip() { set(!get()); it.Right = OnOff(get()); }
            it.OnPick = Flip;
            it.OnAdjust = _ => Flip();
        }

        static Menu Sub(Player p, string title, Func<Menu> self)
        {
            var m = new Menu { Title = $"{Tag} · {title}", Rebuild = self, Parent = () => Main(p) };
            return m;
        }

        static void Done(Player p, string what) { p.Toast(what, new Color(.6f, .9f, 1)); Sfx.Play("ui"); }

        // ------------------------------------------------------------------ main
        public static Menu Main(Player p)
        {
            var gm = GameManager.I;
            var m = new Menu { Title = Tag, Rebuild = () => Main(p) };
            m.Header(gm.Assault ? $"{gm.Expedition.Stage.title} · {GameManager.Enemies.Count} creatures" : $"Day {gm.Phases.Day} · {gm.Phases.Current?.displayName} · {GameManager.Enemies.Count} creatures");
            Toggle(m, "God mode", () => DebugTools.God, v => DebugTools.God = v, "Nobody on the team takes damage.");
            Toggle(m, "Infinite ammo", () => DebugTools.InfiniteAmmo, v => DebugTools.InfiniteAmmo = v, "Guns never empty.");
            Toggle(m, "Noclip (fly)", () => DebugTools.Noclip, v => DebugTools.Noclip = v, "This machine's players fly where they look, through walls. Dash flies fast.");
            Toggle(m, "Freeze creatures", () => DebugTools.Freeze, v => DebugTools.Freeze = v, "Every creature stops where it is (they can still be shot).");
            Toggle(m, "Show colliders", () => DebugTools.Hitboxes, v => DebugTools.Hitboxes = v, "Outlines: creatures red, weak points yellow, players green, defences blue, stalls purple.");
            Toggle(m, "Stats overlay", () => DebugTools.Overlay, v => DebugTools.Overlay = v, "Frame rate, time scale, phase or stage, creatures, purse, casino result.");
            var speed = m.Add("Time scale", Speed(), "Left/right: frozen, ¼, ½, normal, 2× or 4× speed. Menus still work when frozen.");
            speed.OnAdjust = d => { int i = Mathf.Max(0, Array.IndexOf(Speeds, DebugTools.TimeScale)); DebugTools.TimeScale = Speeds[Mathf.Clamp(i + d, 0, Speeds.Length - 1)]; speed.Right = Speed(); };
            speed.OnPick = () => { DebugTools.TimeScale = 1; speed.Right = Speed(); };
            m.Header("TOOLS");
            m.Add("Players & money", ">", "Sand dollars, pearls, levels, weapons, healing, teleporting, a test player.", open: () => Players(p));
            m.Add("Creatures", ">", "Spawn any creature where you aim; kill them all.", open: () => Creatures(p));
            if (!gm.Assault)
            {
                m.Add("Waves & days", ">", "Next phase, any day, clear the wave, a tidal card draft.", open: () => Waves(p));
                m.Add("Wall & powerplant", ">", "Repair or break them.", open: () => Wall(p));
            }
            m.Add(gm.Assault ? "The expedition (Part II)" : "Part II", ">", "Jump to a stage, finish objectives, refill the tide, the endings.", open: () => Expedition(p));
            m.Add("Tidal cards", ">", "Turn any card on or off.", open: () => Cards(p));
            m.Add("Casino", ">", "Open the Lucky Barnacle right here.", open: () => CasinoTools(p));
            m.Add("Legacy file", ">", "Edit runs, records, Spiral Shards and the ending. Writes only when you choose to.", open: () => LegacyEditor(p, reload: true));
            m.Add("Turn every switch off", null, "God mode, ammo, noclip, freeze, colliders, overlay, casino hours and time scale back to normal.",
                  pick: () => { DebugTools.ResetAll(); Done(p, "Debug switches off"); }, blocked: () => DebugTools.AnyOn ? null : "They're all off");
            m.Add("Close", null, null, pick: p.CloseMenu);
            m.FirstSelectable();
            return m;
        }

        static string Speed() => DebugTools.TimeScale == 0 ? "< Frozen >" : $"< {DebugTools.TimeScale:0.##}× >";

        // ------------------------------------------------------------------ players & money
        static Menu Players(Player p)
        {
            var gm = GameManager.I;
            var m = Sub(p, "PLAYERS & MONEY", () => Players(p));
            m.Header($"Team purse {gm.SandDollars} SD · {gm.Pearls} pearls · you are level {p.Level}");
            foreach (int amount in new[] { 100, 1000, 10000 })
            {
                int a = amount;
                m.Add($"+{a:N0} sand dollars", null, "Into the team's purse.", pick: () => gm.AddSandDollars(a, null, "Debug"));
            }
            m.Add("Empty the purse", null, "Sand dollars to 0.", pick: () => gm.TrySpend(gm.SandDollars));
            m.Add("+5 golden pearls", null, null, pick: () => gm.AddPearls(5));
            m.Add("Level up", $"level {p.Level}", "One level for you (stat points and mods as normal).", pick: () => LevelUp(p, 1));
            m.Add("Level up everyone ×5", null, "Five levels for every player.", pick: () => { foreach (var q in gm.Players.All) LevelUp(q, 5); });
            m.Add("Every weapon", null, "You own every gun and melee weapon (switch with Swap; the one in your hands stays).", pick: () =>
            {
                string gun = p.Gun.id, melee = p.Melee.id;
                foreach (var g in gm.DB.guns) p.GiveGun(g.id);
                foreach (var w in gm.DB.melee) p.GiveMelee(w.id);
                p.GiveGun(gun); p.GiveMelee(melee);
                Done(p, "Every weapon");
            });
            m.Add("Refill ammo", null, null, pick: () => { foreach (var q in gm.Players.All) if (q.Simulated) q.DebugRefill(); });
            m.Add("Heal everyone", null, null, pick: () => { foreach (var q in gm.Players.All) if (q.Simulated) q.Heal(q.MaxHp); });
            m.Add("Revive everyone", null, "Downed and bled-out players get back up.", pick: () =>
            {
                foreach (var q in gm.Players.All)
                    if (q.Simulated && q.IsOut) q.Respawn(PlayerManager.SpawnPoint(q.Index));
                    else if (q.Simulated && q.IsDowned) q.Revive(1);
            });
            m.Add("Clear status effects", null, "Food effects, ink, slows and the rest.", pick: () => { foreach (var q in gm.Players.All) if (q.Simulated) q.Statuses.ClearAll(); });
            m.Add("Teleport to where you aim", null, null, pick: () => { p.Place(p.AimPoint + Vector3.up * .2f); p.CloseMenu(); });
            m.Add("Add a test player", null, "A virtual controller joins as another split-screen player (it stands still).", pick: () =>
            {
                var pad = InputSystem.AddDevice<Gamepad>($"DebugPad{gm.Players.Count}");
                if (gm.Players.Join(new InputDevice[] { pad }) == null) { InputSystem.RemoveDevice(pad); p.Toast("Four players already", new Color(1, .6f, .6f)); }
            }, blocked: () => gm.Players.Count >= PlayerManager.MaxPlayers ? "Four players already" : null);
            m.Add("Back", null, null, open: () => Main(p));
            m.FirstSelectable();
            return m;
        }

        static void LevelUp(Player p, int levels)
        {
            var db = GameManager.I.DB;
            for (int i = 0; i < levels; i++)
            {
                int need = db.XpForLevel(p.Level) - p.Xp;
                int lv = p.Level;
                for (int guard = 0; guard < 8 && p.Level == lv; guard++) p.GainXp(need * (guard + 1));   // card and stat multipliers vary
            }
        }

        // ------------------------------------------------------------------ creatures
        static Menu Creatures(Player p)
        {
            var gm = GameManager.I;
            var m = Sub(p, "CREATURES", () => Creatures(p));
            m.Header($"{GameManager.Enemies.Count} alive · spawns appear where you aim");
            var count = m.Add("How many", $"< {SpawnCounts[spawnCount]} >", "Left/right: how many each pick spawns.");
            count.OnAdjust = d => { spawnCount = Mathf.Clamp(spawnCount + d, 0, SpawnCounts.Length - 1); count.Right = $"< {SpawnCounts[spawnCount]} >"; };
            m.Add("Kill every creature", null, "They drop their sand dollars and corpses as normal.", pick: () => KillAll());
            Toggle(m, "Freeze creatures", () => DebugTools.Freeze, v => DebugTools.Freeze = v, "Every creature stops where it is.");
            m.Header("SPAWN");
            foreach (var def in gm.DB.enemies)
            {
                var d = def;
                m.Add(d.displayName, $"{d.hp:0} HP", $"Spawn {d.displayName} where you aim." + (d.id == "tidemother" ? " (Part II's boss)" : ""), pick: () =>
                {
                    int n = SpawnCounts[spawnCount];
                    for (int i = 0; i < n; i++)
                    {
                        var at = p.AimPoint + (n > 1 ? new Vector3(UnityEngine.Random.Range(-2f, 2f), 0, UnityEngine.Random.Range(-2f, 2f)) : Vector3.zero);
                        at.y = Mathf.Max(at.y, Arena.GroundY(at.z));
                        gm.Waves.SpawnExtra(d, at);
                    }
                });
            }
            m.Add("Back", null, null, open: () => Main(p));
            m.FirstSelectable();
            return m;
        }

        public static void KillAll()
        {
            foreach (var e in new List<Enemy>(GameManager.Enemies)) if (e != null && !e.Dead) e.Die(new DamageInfo { amount = 99999, point = e.transform.position });
        }

        // ------------------------------------------------------------------ waves & days (Part I)
        static Menu Waves(Player p)
        {
            var gm = GameManager.I;
            var m = Sub(p, "WAVES & DAYS", () => Waves(p));
            m.Header($"Day {gm.Phases.Day} · {gm.Phases.Current?.displayName} · wave {gm.Waves.Wave}/{gm.Waves.WaveCount} · {GameManager.Enemies.Count} alive");
            m.Add("Next phase", gm.Phases.Current?.displayName, "End this phase now and start the next (as if it had finished).", pick: () => gm.Phases.Advance());
            if (dayPick < 1) dayPick = gm.Phases.Day;
            var day = m.Add("Go to day", $"< {dayPick} >", "Left/right choose a day, then pick: that day starts from its first phase. Creatures get tougher each day.");
            day.OnAdjust = d => { dayPick = Mathf.Clamp(dayPick + d, 1, 99); day.Right = $"< {dayPick} >"; };
            day.OnPick = () => { KillAll(); gm.Phases.SetDay(dayPick); gm.Waves.Day = dayPick; gm.Phases.Enter(0); };
            m.Add("Clear this wave", null, "Kill everything and skip what's left to come this phase.", pick: () => { gm.Waves.DebugSkipQueue(); KillAll(); });
            m.Add("Tidal card vote now", null, "Offer a draft of tidal cards to vote on.", pick: () => { gm.Cards.OfferDraft(); p.CloseMenu(); });
            m.Add("Back", null, null, open: () => Main(p));
            m.FirstSelectable();
            return m;
        }

        // ------------------------------------------------------------------ wall & powerplant (Part I)
        static Menu Wall(Player p)
        {
            var gm = GameManager.I;
            var m = Sub(p, "WALL & POWERPLANT", () => Wall(p));
            m.Header($"Wall {gm.Wall.Hp:0}/{gm.Wall.MaxHp:0} ({gm.Wall.BreachCount} breaches) · powerplant {gm.Plant.Hp:0}/{gm.Plant.MaxHp:0}");
            m.Add("Repair the wall", null, "Every section to full; breaches close.", pick: () => gm.Wall.Repair(1));
            m.Add("Break a wall section", null, "Knock a hole in a random standing section.", pick: () =>
            {
                var standing = gm.Wall.Sections.FindAll(s => !s.breached);
                if (standing.Count == 0) return;
                var s = standing[UnityEngine.Random.Range(0, standing.Count)];
                gm.Wall.Damage(s.hp + 1, s.t.position);
            });
            m.Add("Repair the powerplant", null, null, pick: () => gm.Plant.Repair(1));
            m.Add("Damage the powerplant 25%", null, "At 0 the run is over.", pick: () => gm.Plant.Damage(gm.Plant.MaxHp * .25f, gm.Plant.transform.position));
            m.Add("Back", null, null, open: () => Main(p));
            m.FirstSelectable();
            return m;
        }

        // ------------------------------------------------------------------ Part II
        static Menu Expedition(Player p)
        {
            var gm = GameManager.I;
            var part = gm.Campaign.parts.Find(x => x.kind == PartKind.Assault);
            var m = Sub(p, "PART II", () => Expedition(p));
            var ex = gm.Expedition;
            if (ex != null)
            {
                m.Header($"{ex.Stage.title} · {ex.StatusLine()}");
                m.Add("Finish the objectives", null, "Every spire, beacon, wreck wall or courtyard here counts as done; the way on opens.", pick: () =>
                {
                    foreach (var o in ex.objectives) if (o != null && !o.Done) o.DebugComplete();
                });
                m.Add("Refill the tide clock", $"{ex.TideLeft:0}s", null, pick: ex.DebugRefillTide);
            }
            else m.Header("You're in Part I. Jumping starts a fresh, unsaved expedition with this team.");
            var stage = m.Add("Go to stage", $"< {stagePick + 1}: {part.stages[stagePick].title} >", "Left/right choose, then pick. The team, purse and weapons come along (not saved).");
            stage.OnAdjust = d => { stagePick = Mathf.Clamp(stagePick + d, 0, part.stages.Count - 1); stage.Right = $"< {stagePick + 1}: {part.stages[stagePick].title} >"; };
            stage.OnPick = () =>
            {
                var save = gm.Assault ? SaveSystem.Capture(gm) : TideWall.Expedition.NewSave(gm, 0, stagePick);
                save.stage = stagePick;
                GameManager.Load(part.stages[stagePick].scene, gm.Carry(save, 0));
            };
            if (ex != null && ex.Stage.objective == ObjectiveKind.Heart)
                foreach (var end in new[] { "strike", "spiral" })
                {
                    var ending = gm.Campaign.Ending(end);
                    if (ending == null) continue;
                    string id = end;
                    m.Add($"Ending: {ending.choice}", null, "Plays the ending now. It's recorded in the legacy file, as a real ending would be.", pick: () => { p.CloseMenu(); ex.Finish(id); });
                }
            m.Add("Find every Spiral Shard", $"{Legacy.Load().ShardCount}/5", "Writes all five shards to the legacy file (Lay down the Crest needs them).", pick: () =>
            {
                for (int i = 0; i < part.stages.Count; i++) Legacy.FindShard(i);
                Done(p, "All five Spiral Shards found");
            });
            m.Add("Back", null, null, open: () => Main(p));
            m.FirstSelectable();
            return m;
        }

        // ------------------------------------------------------------------ tidal cards
        static Menu Cards(Player p)
        {
            var gm = GameManager.I;
            var m = Sub(p, "TIDAL CARDS", () => Cards(p));
            m.Header($"{gm.Cards.Actives.Count} active · pick a card to turn it on or off");
            foreach (var c in gm.DB.cards)
            {
                var card = c;
                var on = gm.Cards.Actives.Find(a => a.def == card);
                m.Add(card.displayName, on != null ? $"ON ({on.wavesLeft} waves)" : "off", card.description, pick: () =>
                {
                    if (gm.Cards.Actives.RemoveAll(a => a.def == card) == 0) gm.Cards.Activate(card);
                    else foreach (var q in gm.Players.All) q.RefreshMaxHp();
                });
            }
            m.Add("Back", null, null, open: () => Main(p));
            m.FirstSelectable();
            return m;
        }

        // ------------------------------------------------------------------ casino
        static Menu CasinoTools(Player p)
        {
            var gm = GameManager.I;
            var m = Sub(p, "CASINO", () => CasinoTools(p));
            m.Header($"This run at the tables: {gm.Casino.Net:+0;-0;0} SD over {gm.Casino.HandsPlayed} hands");
            m.Add("Open the casino here", null, "The Lucky Barnacle's games, wherever you stand.", pick: () => p.OpenMenu(Casino.Lobby(p, p.transform.position)));
            Toggle(m, "Open during fights", () => DebugTools.CasinoAlwaysOpen, v => DebugTools.CasinoAlwaysOpen = v, "Normally Part I's tables close while a fight is on.");
            m.Add("Back", null, null, open: () => Main(p));
            m.FirstSelectable();
            return m;
        }

        // ------------------------------------------------------------------ legacy file
        static Menu LegacyEditor(Player p, bool reload = false)
        {
            if (reload || draft == null) draft = Legacy.Load();
            var d = draft;
            var m = Sub(p, "LEGACY FILE", () => LegacyEditor(p));
            m.Header("Change values, then Write. Nothing touches legacy.json until you do.");
            Number(m, "Runs", () => d.runs, v => d.runs = v, "Tally marks on the keep wall, wrecks in the Hull Graveyard.");
            Number(m, "Best day", () => d.bestDay, v => d.bestDay = v, null);
            Number(m, "Best level", () => d.bestLevel, v => d.bestLevel = v, null);
            Number(m, "Expeditions", () => d.expeditions, v => d.expeditions = v, "Part II runs.");
            Number(m, "Best stage", () => d.bestStage, v => d.bestStage = Mathf.Min(v, 5), "Furthest Part II stage reached (1–5).");
            for (int i = 0; i < 5; i++)
            {
                int bit = i;
                Toggle(m, $"Spiral Shard {bit + 1}", () => (d.shards & (1 << bit)) != 0, v => d.shards = v ? d.shards | (1 << bit) : d.shards & ~(1 << bit), $"The shard hidden in stage {bit + 1}.");
            }
            Number(m, "Times the Heart was struck", () => d.strikes, v => d.strikes = v, null);
            Toggle(m, "The Crest laid down (peace)", () => d.spiral, v => { d.spiral = v; if (v && d.runsAtPeace == 0) d.runsAtPeace = d.runs; }, "The calm sea in the menu; the tally stops.");
            m.Add("Write to legacy.json", null, "Saves these values over the real legacy file.", pick: () => { d.DebugWrite(); Done(p, "Legacy file written"); });
            m.Add("Reload from the file", null, "Throw away changes made here.", pick: () => { draft = Legacy.Load(); });
            m.Add("Start from a blank legacy", null, "Everything zero (still only written when you pick Write).", pick: () => { draft = new Legacy(); });
            m.Add("Back", null, null, open: () => Main(p));
            m.FirstSelectable();
            return m;
        }

        static void Number(Menu m, string label, Func<int> get, Action<int> set, string detail)
        {
            var it = m.Add(label, $"< {get()} >", (detail != null ? detail + " " : "") + "Left/right change it.");
            it.OnAdjust = d => { set(Mathf.Max(0, get() + d)); it.Right = $"< {get()} >"; };
        }
    }
}

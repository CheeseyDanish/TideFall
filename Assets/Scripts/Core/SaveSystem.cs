// TideWall 3D — save slots (a suspended roguelike run; deleted when the run ends). A run autosaves when a phase with `saveOnStart` begins (Morning in the shipped cycle),
// and players can save again during that phase from the pause menu. A save is the team's state at that point:
// day, phase, sand dollars, wall sections, powerplant, upgrades, active tidal cards, and each player slot's weapons,
// level, stats and mods. Files: <Storage.Root>/saves/slot<N>.json.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace TideWall
{
    [Serializable]
    public class SaveData
    {
        public int version = 4;
        public int slot;
        [Tooltip("Campaign part id: \"wall\" (Part I) or \"undertow\" (Part II)")] public string part = "wall";
        [Tooltip("Part II: the stage to play next (0-based)")] public int stage;
        public string savedAt;          // ISO 8601, for "Continue" (most recent wins)
        public int day = 1, phaseIndex;
        public int sandDollars, pearls, runWave;
        public List<StructureSave> structures = new();
        public float wallHp, wallMaxHp;                       // totals (for the summary)
        public List<float> wallSections = new(), wallSectionMax = new();
        public float plantHp, plantMaxHp;
        public List<CardSave> cards = new();
        public List<UpgradeLevel> upgrades = new();
        public float gunDamage = 1, meleeDamage = 1, magazine = 1, playerHpBonus;
        public int extraGasCans;
        public List<PlayerSave> players = new();
        public int kills, severs;

        public string Summary => part == "wall"
            ? $"Day {day} · {sandDollars} sand dollars · powerplant {Mathf.CeilToInt(plantHp)}/{Mathf.RoundToInt(plantMaxHp)}"
            : $"Part II · stage {stage + 1} · {sandDollars} sand dollars";
    }

    [Serializable] public class UpgradeLevel { public string id; public int level; }
    [Serializable] public class StructureSave { public string id; public float x, z, yaw, hp; }

    [Serializable]
    public class PlayerSave
    {
        public int index;
        public string gun, melee;
        public List<string> guns = new(), melees = new();
        public int kills, severs;
        public int level = 1, xp, statPoints, pendingMods, respecs;
        public List<UpgradeLevel> stats = new();          // stat id → points
        public List<string> mods = new();                 // "weaponId/modId"
    }

    public static class SaveSystem
    {
        public const int Slots = 3;
        static string File(int slot) => $"saves/slot{slot}.json";

        public static SaveData Load(int slot)
        {
            var json = Storage.Read(File(slot));
            if (string.IsNullOrEmpty(json)) return null;
            try { var d = JsonUtility.FromJson<SaveData>(json); d.slot = slot; return d; }
            catch (Exception e) { Debug.LogWarning($"TideWall: save slot {slot} is unreadable ({e.Message})"); return null; }
        }

        public static bool Write(SaveData d)
        {
            d.savedAt = DateTime.UtcNow.ToString("o");
            return Storage.Write(File(d.slot), JsonUtility.ToJson(d, true));
        }

        public static void Delete(int slot) => Storage.Delete(File(slot));

        /// <summary>The most recently saved slot, or null if there are no saves.</summary>
        public static SaveData Latest()
        {
            SaveData best = null;
            for (int i = 1; i <= Slots; i++)
            {
                var d = Load(i);
                if (d != null && (best == null || string.CompareOrdinal(d.savedAt, best.savedAt) > 0)) best = d;
            }
            return best;
        }

        public static int FirstEmptySlot()
        {
            for (int i = 1; i <= Slots; i++) if (Load(i) == null) return i;
            return 0;
        }

        /// <summary>Snapshot the running game into a save for its slot.</summary>
        public static SaveData Capture(GameManager gm)
        {
            var d = new SaveData
            {
                slot = gm.SaveSlot, day = gm.Phases.Day, phaseIndex = Mathf.Max(0, gm.Phases.Index), sandDollars = gm.SandDollars, pearls = gm.Pearls, runWave = gm.Waves.RunWave,
                gunDamage = gm.Team.GunDamage, meleeDamage = gm.Team.MeleeDamage, magazine = gm.Team.Magazine,
                playerHpBonus = gm.Team.PlayerHpBonus, extraGasCans = gm.Team.ExtraGasCans,
            };
            if (gm.World != null)
            {
                d.wallHp = gm.Wall.Hp; d.wallMaxHp = gm.Wall.MaxHp; d.plantHp = gm.Plant.Hp; d.plantMaxHp = gm.Plant.MaxHp;
                foreach (var st in Structure.All)
                    if (st != null && !st.Dead) d.structures.Add(new StructureSave { id = st.Def.id, x = st.transform.position.x, z = st.transform.position.z, yaw = st.transform.eulerAngles.y, hp = st.Hp });
                foreach (var sec in gm.Wall.Sections) { d.wallSections.Add(sec.hp); d.wallSectionMax.Add(sec.max); }
            }
            else { d.part = gm.Campaign.parts.Find(p => p.kind == PartKind.Assault)?.id ?? "undertow"; d.stage = gm.Expedition.StageIndex; }
            foreach (var a in gm.Cards.Actives) d.cards.Add(new CardSave { id = a.def.id, wavesLeft = a.wavesLeft });
            foreach (var u in gm.DB.upgrades) if (gm.Team.Level(u.id) > 0) d.upgrades.Add(new UpgradeLevel { id = u.id, level = gm.Team.Level(u.id) });
            foreach (var p in gm.Players.All)
            {
                var ps = new PlayerSave
                {
                    index = p.Index, gun = p.Gun.id, melee = p.Melee.id, kills = p.Kills, severs = p.Severs,
                    level = p.Level, xp = p.Xp, statPoints = p.StatPoints, pendingMods = p.PendingMods, respecs = p.Respecs,
                };
                foreach (var kv in p.Allocated) ps.stats.Add(new UpgradeLevel { id = kv.Key, level = kv.Value });
                foreach (var kv in p.Mods) foreach (var m in kv.Value) ps.mods.Add(kv.Key + "/" + m);
                foreach (var g in gm.DB.guns) if (p.OwnsGun(g.id)) ps.guns.Add(g.id);
                foreach (var m in gm.DB.melee) if (p.OwnsMelee(m.id)) ps.melees.Add(m.id);
                d.players.Add(ps);
                d.kills += p.Kills; d.severs += p.Severs;
            }
            // Players who left keep their loadout in the save
            if (gm.Loaded != null)
                foreach (var old in gm.Loaded.players)
                    if (!d.players.Exists(x => x.index == old.index)) d.players.Add(old);
            return d;
        }
    }
}

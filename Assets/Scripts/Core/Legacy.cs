// TideWall 3D — the record that outlives runs: how many runs, the best day reached, the highest level, all-time kills,
// how far expeditions got, the Spiral Shards found, and how the Heart was answered. Stored in <Storage.Root>/legacy.json.
// The keep wall in the main menu shows one tally mark per run; the Hull Graveyard shows one wreck per run.
using System;
using UnityEngine;

namespace TideWall
{
    [Serializable]
    public class Legacy
    {
        public int runs, bestDay, bestLevel, kills;
        [Tooltip("Part II runs, and the furthest stage reached (1-based)")] public int expeditions, bestStage;
        [Tooltip("Spiral Shards found: bit i = the shard of stage i")] public int shards;
        [Tooltip("Times the Heart was struck")] public int strikes;
        [Tooltip("The Crest was laid down: the sea is calm")] public bool spiral;
        [Tooltip("Runs played when the Crest was laid down (the keep wall's tally stops there)")] public int runsAtPeace;
        [Tooltip("Weapon skins unlocked with golden pearls")] public System.Collections.Generic.List<string> skins = new();
        [Tooltip("The Last Light: lamp oil to spend, the deepest tide, how many runs")] public int lampOil, bestTide, lightRuns;
        public System.Collections.Generic.List<UpgradeLevel> keeper = new();
        [Tooltip("Every kind of fish ever landed")] public System.Collections.Generic.List<string> fishLog = new();
        const string File = "legacy.json";

        public int ShardCount { get { int n = 0; for (int s = shards; s != 0; s >>= 1) n += s & 1; return n; } }
        public bool HasShard(int stage) => (shards & (1 << stage)) != 0;
        /// <summary>The tally scratched on the keep wall, and the wrecks in the Hull Graveyard: one per run, until the peace.</summary>
        public int Tally => spiral ? runsAtPeace : runs;

        public static Legacy Load()
        {
            var json = Storage.Read(File);
            if (string.IsNullOrEmpty(json)) return new Legacy();
            try { return JsonUtility.FromJson<Legacy>(json) ?? new Legacy(); } catch { return new Legacy(); }
        }

        void Save() => Storage.Write(File, JsonUtility.ToJson(this, true));
        /// <summary>Debug menu: write this record over legacy.json.</summary>
        public void DebugWrite() => Save();

        /// <summary>A Part I run ended: fold it into the record.</summary>
        public static Legacy Record(int day, int level, int kills)
        {
            var l = Load();
            l.runs++;
            l.bestDay = Mathf.Max(l.bestDay, day);
            l.bestLevel = Mathf.Max(l.bestLevel, level);
            l.kills += kills;
            l.Save();
            return l;
        }

        /// <summary>A Part II run ended at `stage` (lost there, or the Heart answered).</summary>
        public static Legacy RecordExpedition(int stage, int level, int kills)
        {
            var l = Load();
            l.runs++;
            l.expeditions++;
            l.bestStage = Mathf.Max(l.bestStage, stage + 1);
            l.bestLevel = Mathf.Max(l.bestLevel, level);
            l.kills += kills;
            l.Save();
            return l;
        }

        /// <summary>A Spiral Shard was found. Returns how many there are now.</summary>
        public static int FindShard(int stage)
        {
            var l = Load();
            l.shards |= 1 << stage;
            l.Save();
            return l.ShardCount;
        }

        public int KeeperLevel(string id) { var u = keeper.Find(k => k.id == id); return u != null ? u.level : 0; }

        /// <summary>The total bonus of this kind from the Keeper's Log.</summary>
        public float KeeperBonus(LastLightDef def, KeeperUpgradeKind kind)
        {
            float b = 0;
            foreach (var u in def.upgrades) if (u.kind == kind) b += KeeperLevel(u.id) * u.amount;
            return b;
        }

        /// <summary>A Last Light run ended.</summary>
        public static Legacy RecordLight(int tide, int level, int kills, int oil)
        {
            var l = Load();
            l.runs++;
            l.lightRuns++;
            l.bestTide = Mathf.Max(l.bestTide, tide);
            l.bestLevel = Mathf.Max(l.bestLevel, level);
            l.kills += kills;
            l.lampOil += oil;
            l.Save();
            return l;
        }

        /// <summary>Spend lamp oil on a Keeper's Log upgrade. False if there isn't enough.</summary>
        public static bool BuyKeeper(string id, int price)
        {
            var l = Load();
            if (l.lampOil < price) return false;
            l.lampOil -= price;
            var u = l.keeper.Find(k => k.id == id);
            if (u == null) l.keeper.Add(new UpgradeLevel { id = id, level = 1 }); else u.level++;
            l.Save();
            return true;
        }

        public static void LogFish(string id)
        {
            var l = Load();
            if (l.fishLog.Contains(id)) return;
            l.fishLog.Add(id);
            l.Save();
        }

        /// <summary>A skin was bought: it's unlocked for good.</summary>
        public static void UnlockSkin(string id)
        {
            var l = Load();
            if (!l.skins.Contains(id)) l.skins.Add(id);
            l.Save();
        }

        /// <summary>The Heart was answered.</summary>
        public static Legacy RecordEnding(string ending)
        {
            var l = Load();
            if (ending == "spiral") { if (!l.spiral) l.runsAtPeace = l.runs; l.spiral = true; }
            else l.strikes++;
            l.Save();
            return l;
        }
    }
}

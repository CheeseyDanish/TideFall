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

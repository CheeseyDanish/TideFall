// TideWall 3D — the roguelike record that outlives runs: how many runs, the best day reached, the highest level,
// all-time kills. Stored in <Storage.Root>/legacy.json and shown on the main menu.
using System;
using UnityEngine;

namespace TideWall
{
    [Serializable]
    public class Legacy
    {
        public int runs, bestDay, bestLevel, kills;
        const string File = "legacy.json";

        public static Legacy Load()
        {
            var json = Storage.Read(File);
            if (string.IsNullOrEmpty(json)) return new Legacy();
            try { return JsonUtility.FromJson<Legacy>(json) ?? new Legacy(); } catch { return new Legacy(); }
        }

        /// <summary>A run ended: fold it into the record.</summary>
        public static Legacy Record(int day, int level, int kills)
        {
            var l = Load();
            l.runs++;
            l.bestDay = Mathf.Max(l.bestDay, day);
            l.bestLevel = Mathf.Max(l.bestLevel, level);
            l.kills += kills;
            Storage.Write(File, JsonUtility.ToJson(l, true));
            return l;
        }
    }
}

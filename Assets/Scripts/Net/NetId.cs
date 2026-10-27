// TideWall 3D — network identity for things the host replicates to online clients: enemies (and the corpses they
// become, which keep the same id), gas cans and picnic food. Players are identified by their player index instead.
// Ids are handed out on every machine that simulates (offline too, it's cheap); clients receive them in snapshots.
using System.Collections.Generic;
using UnityEngine;

namespace TideWall
{
    public enum NetKind : byte { Enemy = 1, GasCan = 2, Food = 3, Breaker = 4, SandDollar = 5, Structure = 6 }

    public class NetId : MonoBehaviour
    {
        public static readonly List<NetId> All = new();
        static int next = 1;

        public int Id;
        public NetKind Kind;
        public int Def;       // index into GameDatabase.enemies / foods

        public static NetId Tag(GameObject go, NetKind kind, int def, int id = 0)
        {
            var n = go.AddComponent<NetId>();
            n.Kind = kind; n.Def = def;
            n.Id = id != 0 ? id : next++;
            All.Add(n);
            return n;
        }

        public static void Reset() { All.Clear(); next = 1; }

        void OnDestroy() => All.Remove(this);
    }
}

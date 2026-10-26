// TideWall 3D — network identity for things the host replicates to online clients: enemies (and the corpses they
// become, which keep the same id), gas cans and picnic food. Players are identified by their player index instead.
// Ids are handed out on every machine that simulates (offline too, it's cheap); clients receive them in snapshots.
using System.Collections.Generic;
using UnityEngine;

namespace TideWall
{
    public enum NetKind : byte { Enemy = 1, GasCan = 2, Food = 3 }

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

    /// <summary>Client: a replicated thing that isn't a living enemy (corpse, gas can, food) glides to the host's pose.</summary>
    public class NetFollow : MonoBehaviour
    {
        public Vector3 pos;
        public Quaternion rot = Quaternion.identity;
        bool started;

        public void Set(Vector3 p, Quaternion r)
        {
            pos = p; rot = r;
            if (!started) { transform.SetPositionAndRotation(p, r); started = true; }
        }

        void LateUpdate()
        {
            if (!started) return;
            float k = 1 - Mathf.Exp(-Time.deltaTime * 14);
            transform.position = Vector3.Distance(transform.position, pos) > 8 ? pos : Vector3.Lerp(transform.position, pos, k);
            transform.rotation = Quaternion.Slerp(transform.rotation, rot, k);
        }
    }
}

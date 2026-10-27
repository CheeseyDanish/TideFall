// TideWall 3D — something a Part II stage asks the expedition to do (BroodSpire, Beacon, WreckBarrier, HoldRing,
// HeartAltar). Saved in the stage scene; the Expedition activates them, ticks them on the host, and counts them done.
// Each one writes its own state into the host's snapshots so clients see the same thing.
using System.IO;
using UnityEngine;

namespace TideWall
{
    public abstract class Objective : MonoBehaviour
    {
        protected Expedition expedition;
        public bool Done { get; protected set; }

        /// <summary>The stage begins (host and clients).</summary>
        public virtual void Activate(Expedition e) => expedition = e;

        /// <summary>Host, every frame while the stage is being played.</summary>
        public virtual void Tick(float dt) { }

        /// <summary>A word or two for the HUD while this one is in progress ("Beacon 45%"), or null.</summary>
        public virtual string Progress => null;

        /// <summary>How hard the sea pushes back while this one is going on (1 = normal, 2 = twice the creatures).</summary>
        public virtual float Pressure => 1;

        public virtual void WriteNet(BinaryWriter w) => w.Write(Done);
        public virtual void ReadNet(BinaryReader r) { bool d = r.ReadBoolean(); if (d && !Done) Finish(); }

        /// <summary>Done: the look and sound of it (host and clients).</summary>
        protected virtual void Finish() { Done = true; }
        /// <summary>Debug menu: finish it now, as if the players had.</summary>
        public void DebugComplete() { if (!Done) Finish(); }

        protected static Player NearestPlayer(Vector3 at, float range)
        {
            Player best = null; float bd = range;
            foreach (var p in GameManager.I.Players.All)
            {
                if (p.IsOut) continue;
                var d = p.transform.position - at; d.y = 0;
                if (d.magnitude < bd) { bd = d.magnitude; best = p; }
            }
            return best;
        }

        protected static int PlayersWithin(Vector3 at, float range)
        {
            int n = 0;
            foreach (var p in GameManager.I.Players.All)
            {
                if (p.IsOut || p.IsDowned) continue;
                var d = p.transform.position - at; d.y = 0;
                if (d.magnitude < range) n++;
            }
            return n;
        }
    }
}

// TideWall 3D — the wreck-wall (The Hull Graveyard): old hulls, chain and timber jammed across the trench. Bullets do
// nothing to it; fire does. Carry gas cans from the cache, throw them at it and set them off: `blasts` explosions open
// it. The cache keeps a few cans lying ready.
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TideWall
{
    public class WreckBarrier : Objective, IShootable
    {
        [SerializeField] List<Transform> pieces = new();
        [SerializeField] Collider blocker;
        [SerializeField] Transform cache;
        public int blasts = 3;
        public float reach = 6;
        int hits;
        float cacheT, openT, hitCooldown;
        readonly List<GasCan> cans = new();

        public bool Alive => !Done && expedition != null;

        public static WreckBarrier Create(Transform parent, Vector3 pos, float halfWidth, Vector3 cachePos)
        {
            var go = new GameObject("Wreck-wall");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var b = go.AddComponent<WreckBarrier>();
            var rng = new System.Random(31);
            for (float x = -halfWidth; x <= halfWidth; x += 5.5f)
            {
                var hull = ModelFactory.Spawn("WreckHull", go.transform, new Vector3(x, 0, (float)rng.NextDouble() * 1.5f - .75f), 1.1f, colliders: true, triggers: false);
                hull.root.transform.localRotation = Quaternion.Euler((float)rng.NextDouble() * 20 - 10, 80 + (float)rng.NextDouble() * 20, 60 + (float)rng.NextDouble() * 30);
                b.pieces.Add(hull.root.transform);
                Glyphs.Decal(hull.root.transform, "crest", new Vector3(.62f, 1.1f, 0), Quaternion.Euler(0, -90, 0), 1.2f, new Color(.9f, .85f, .7f, .7f));
                Glyphs.Decal(hull.root.transform, "tally", new Vector3(.62f, .5f, 1.2f), Quaternion.Euler(0, -90, 0), .7f, new Color(.9f, .85f, .7f, .55f));
            }
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 2, 0); col.size = new Vector3(halfWidth * 2 + 6, 4, 3);
            b.blocker = col;
            var c = new GameObject("Gas cache").transform;
            c.SetParent(parent, false);
            c.position = cachePos;
            ModelFactory.Spawn("Crate", c, new Vector3(-1.2f, 0, 0)).root.transform.localRotation = Quaternion.Euler(0, 20, 0);
            ModelFactory.Spawn("Crate", c, new Vector3(1.4f, 0, .4f)).root.transform.localRotation = Quaternion.Euler(0, -15, 0);
            b.cache = c;
            return b;
        }

        public override void Tick(float dt)
        {
            if (Done) return;
            hitCooldown -= dt;
            // The cache keeps three cans lying ready
            cans.RemoveAll(x => x == null);
            cacheT -= dt;
            if (cache && cans.Count < 3 && cacheT <= 0)
            {
                cacheT = 6;
                cans.Add(GasCan.Spawn(cache.position + new Vector3(Random.Range(-2.5f, 2.5f), 0, Random.Range(-2f, 2f))));
            }
        }

        /// <summary>Only fire hurts it: explosions within reach count as a blast (one per moment, so a chain counts once).</summary>
        public void Shot(DamageInfo hit)
        {
            if (Done || Net.IsClient || !hit.explosion || hitCooldown > 0) return;
            var d = hit.point - transform.position; d.y = 0;
            if (Mathf.Abs(d.z) > reach) return;
            hitCooldown = .4f;
            hits++;
            Sfx.Play("sever", .8f);
            Fx.Debris(hit.point + Vector3.up, new Color(.45f, .32f, .22f), 18);
            foreach (var p in GameManager.I.Players.All) p.Shake(.25f);
            if (hits >= blasts) { Finish(); expedition.ObjectiveDone(this); }
            else GameManager.I.Banner("THE WRECK-WALL SHUDDERS", $"{blasts - hits} more");
        }

        public override string Progress => Done ? null : $"{hits}/{blasts} blasts";

        protected override void Finish()
        {
            base.Finish();
            openT = .001f;
            if (blocker) blocker.enabled = false;
            foreach (var p in pieces) foreach (var c in p.GetComponentsInChildren<Collider>()) c.enabled = false;
            Sfx.Play("boom");
            Fx.Explosion(transform.position + Vector3.up * 2, 6);
        }

        void Update()
        {
            if (openT <= 0 || openT >= 1) return;
            // The hulls collapse outward and sink
            openT = Mathf.Min(1, openT + Time.deltaTime * .5f);
            for (int i = 0; i < pieces.Count; i++)
            {
                var p = pieces[i];
                if (!p) continue;
                float side = Mathf.Sign(p.localPosition.x + .01f);
                p.localPosition += new Vector3(side * 3f, -1.6f, .5f) * Time.deltaTime * .5f;
                p.localRotation *= Quaternion.Euler(0, 0, side * 25 * Time.deltaTime);
            }
        }

        public override void WriteNet(BinaryWriter w) { base.WriteNet(w); w.Write((byte)hits); }
        public override void ReadNet(BinaryReader r) { base.ReadNet(r); hits = r.ReadByte(); }
    }
}

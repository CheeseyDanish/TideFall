// TideWall 3D — a Spiral Shard: one per Part II stage, off the main path. A small stone with one arc of the Crest on
// it. Found shards are kept for good (Legacy); all five open the second answer at the Heart. It never says why.
using UnityEngine;

namespace TideWall
{
    public class ShardStone : Interactable
    {
        [SerializeField] int stage;
        [SerializeField] Renderer glyph;
        [SerializeField] Light glow;
        bool taken;

        public static ShardStone Create(Transform parent, Vector3 pos, int stage)
        {
            var go = new GameObject("Spiral Shard");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var s = go.AddComponent<ShardStone>();
            s.stage = stage; s.radius = 2.2f;
            var stone = ModelFactory.Spawn("ShardStone", go.transform, Vector3.zero);
            stone.root.transform.localRotation = Quaternion.Euler(0, 72 * stage, 0);
            s.glyph = Glyphs.Decal(stone.root.transform, "crest", new Vector3(0, .62f, -.17f), Quaternion.Euler(0, 0, 72 * stage), .5f, new Color(1f, .85f, .45f), glow: true).GetComponent<Renderer>();
            s.glow = new GameObject("Glow").AddComponent<Light>();
            s.glow.transform.SetParent(go.transform, false);
            s.glow.transform.localPosition = new Vector3(0, 1, 0);
            s.glow.type = LightType.Point; s.glow.range = 5; s.glow.intensity = 2.5f; s.glow.color = new Color(1f, .85f, .45f); s.glow.shadows = LightShadows.None;
            return s;
        }

        void Start() { if (Legacy.Load().HasShard(stage)) Take(false); }

        public override string Prompt(Player p) => taken ? null : "Take the stone";

        public override void Interact(Player p)
        {
            if (taken || Net.IsClient) return;
            Collect(stage, announce: true);
            if (Net.IsHost) Net.Host.Shard(stage);
        }

        /// <summary>A shard was found (here, or on the host): keep it, and show it taken. The host announces it (the
        /// banner reaches clients on its own).</summary>
        public static void Collect(int stage, bool announce)
        {
            int n = Legacy.FindShard(stage);
            foreach (var s in FindObjectsByType<ShardStone>()) if (s.stage == stage) s.Take(true);
            foreach (var a in FindObjectsByType<HeartAltar>()) a.ShardFound(stage);
            if (announce) GameManager.I.Banner("A SPIRAL SHARD", $"{n} of {GameManager.I.Campaign.shardCount}");
            Sfx.Play("jackpot", .7f);
        }

        void Take(bool now)
        {
            taken = true;
            if (glyph) glyph.enabled = false;
            if (glow) glow.enabled = false;
            if (now) Fx.Sparks(transform.position + Vector3.up * .6f, new Color(1f, .85f, .45f), 24);
        }

        void Update()
        {
            if (!taken && glow) glow.intensity = 2 + Mathf.Sin(Time.time * 2.4f) * .8f;
        }
    }
}

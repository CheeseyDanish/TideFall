// TideWall 3D — a beacon (The Kelp Cathedral): an old stone brazier. Stand by it and it slowly catches; the more of
// you, the faster. The sea comes for the light while it does. Once lit it stays lit.
// (Docs/STORY.md: these are the sea's own lamps, and the expedition is lighting them for it.)
using System.IO;
using UnityEngine;

namespace TideWall
{
    public class Beacon : Objective
    {
        [SerializeField] Light flame;
        [SerializeField] Renderer glyph;
        public float radius = 4.5f, catchSeconds = 14;
        float progress, flicker;

        public static Beacon Create(Transform parent, Vector3 pos, float yaw)
        {
            var go = new GameObject("Beacon");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            var b = go.AddComponent<Beacon>();
            var model = ModelFactory.Spawn("Beacon", go.transform, Vector3.zero);
            var col = go.AddComponent<BoxCollider>(); col.center = new Vector3(0, 1, 0); col.size = new Vector3(1.4f, 2, 1.4f);
            b.flame = new GameObject("Flame").AddComponent<Light>();
            b.flame.transform.SetParent(go.transform, false);
            b.flame.transform.localPosition = new Vector3(0, 2.6f, 0);
            b.flame.type = LightType.Point; b.flame.range = 16; b.flame.color = new Color(.55f, 1f, .8f); b.flame.intensity = 0; b.flame.shadows = LightShadows.None;
            // The ring on the ground where you stand to light it
            b.glyph = Glyphs.Decal(go.transform, "heart", new Vector3(0, .03f, 0), Quaternion.Euler(90, 0, 0), b.radius * 2, new Color(.55f, 1f, .8f, .35f), glow: true).GetComponent<Renderer>();
            return b;
        }

        public override void Tick(float dt)
        {
            if (Done) return;
            int n = PlayersWithin(transform.position, radius);
            progress = n > 0 ? Mathf.Min(1, progress + dt / catchSeconds * (1 + .3f * (n - 1))) : Mathf.Max(0, progress - dt / (catchSeconds * 3));
            if (progress >= 1) { Finish(); expedition.ObjectiveDone(this); }
        }

        public bool Catching => !Done && progress > 0;
        public override float Pressure => Catching ? 1.7f : 1;
        public override string Progress => Done ? null : progress > 0 ? $"{Mathf.RoundToInt(progress * 100)}%" : null;

        protected override void Finish()
        {
            base.Finish();
            progress = 1;
            Sfx.Play("bell", .6f);
            Fx.Sparks(transform.position + Vector3.up * 2.6f, new Color(.55f, 1f, .8f), 30);
        }

        void Update()
        {
            flicker += Time.deltaTime;
            if (flame) flame.intensity = (Done ? 9 : progress * 6) * (.85f + .15f * Mathf.PerlinNoise(flicker * 4, transform.position.x));
            if (glyph) glyph.enabled = !Done;
            if (!Done && progress > 0 && Random.value < Time.deltaTime * 12 * progress) Fx.Sparks(transform.position + Vector3.up * 2.4f, new Color(.55f, 1f, .8f), 1);
        }

        public override void WriteNet(BinaryWriter w) { base.WriteNet(w); w.Write(progress); }
        public override void ReadNet(BinaryReader r) { base.ReadNet(r); progress = r.ReadSingle(); }
    }
}

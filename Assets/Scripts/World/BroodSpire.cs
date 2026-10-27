// TideWall 3D — a brood spire (The Ebb): a tall, carved coral stack that creatures hatch from while anyone is near.
// Shoot, cut or blow it up; it topples. Carved with the Spiral and an Eye (docs/STORY.md).
using System.IO;
using UnityEngine;

namespace TideWall
{
    public class BroodSpire : Objective, IShootable
    {
        [SerializeField] Model model;
        [SerializeField] Renderer[] glyphs;
        public float maxHp = 900;
        float hp, spawnT = 3, fall, flashT;
        Quaternion upright;
        MaterialPropertyBlock mpb;

        public bool Alive => !Done && expedition != null;

        public static BroodSpire Create(Transform parent, Vector3 pos, float yaw)
        {
            var go = new GameObject("Brood Spire");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            var s = go.AddComponent<BroodSpire>();
            s.model = ModelFactory.Spawn("BroodSpire", go.transform, Vector3.zero, 1, colliders: true, triggers: false);
            var g1 = Glyphs.DecalOn(go.transform, "spiral", pos + new Vector3(0, 3.1f, 0) + go.transform.rotation * new Vector3(0, 0, -1.05f), go.transform.rotation * Vector3.back, 1.4f, new Color(.6f, 1f, .85f, .85f), glow: true);
            var g2 = Glyphs.DecalOn(go.transform, "eye", pos + new Vector3(0, 5.2f, 0) + go.transform.rotation * new Vector3(0, 0, -.75f), go.transform.rotation * Vector3.back, .9f, new Color(.6f, 1f, .85f, .85f), glow: true);
            s.glyphs = new[] { g1.GetComponent<Renderer>(), g2.GetComponent<Renderer>() };
            return s;
        }

        public override void Activate(Expedition e)
        {
            base.Activate(e);
            hp = maxHp * (1 + .35f * Mathf.Max(0, GameManager.I.Players.Count - 1));
            upright = transform.rotation;
        }

        public override void Tick(float dt)
        {
            if (Done) return;
            // Creatures hatch while someone is close enough to see it
            if (NearestPlayer(transform.position, 42) != null)
            {
                spawnT -= dt;
                if (spawnT <= 0) { spawnT = Random.Range(7f, 11f); expedition.SpawnNear(transform.position + transform.rotation * new Vector3(0, 0, -2.5f), 1.5f); Fx.Splash(transform.position + Vector3.up, 6); }
            }
        }

        public void Shot(DamageInfo hit)
        {
            if (Done || Net.IsClient) return;
            hp -= hit.amount * (hit.explosion ? 2.5f : 1);
            flashT = .12f;
            Fx.Debris(hit.point, new Color(.85f, .55f, .55f), 3);
            if (hp <= 0)
            {
                Finish();
                expedition.ObjectiveDone(this);
            }
        }

        public override string Progress => Done ? null : $"{Mathf.CeilToInt(Mathf.Max(0, hp) / Mathf.Max(1, maxHp) * 100)}%";

        protected override void Finish()
        {
            base.Finish();
            fall = .001f;
            Sfx.Play("boom", .9f);
            Fx.Debris(transform.position + Vector3.up * 3, new Color(.85f, .55f, .55f), 30);
            Fx.Smoke(transform.position + Vector3.up * 2, 12, 1f, new Color(.7f, .75f, .8f, .6f));
            foreach (var r in glyphs) if (r) r.enabled = false;
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
        }

        void Update()
        {
            if (fall > 0 && fall < 1)
            {
                // Topples away from the expedition and sinks into the sand
                fall = Mathf.Min(1, fall + Time.deltaTime * .7f);
                float k = fall * fall;
                transform.rotation = upright * Quaternion.Euler(80 * k, 0, 0);
                transform.position += Vector3.down * Time.deltaTime * k * .8f;
            }
            if (flashT > 0 && model != null)
            {
                flashT -= Time.deltaTime;
                mpb ??= new MaterialPropertyBlock();
                foreach (var r in model.Renderers)
                {
                    if (flashT <= 0) { r.SetPropertyBlock(null); continue; }
                    r.GetPropertyBlock(mpb);
                    mpb.SetColor("_BaseColor", Color.white);
                    r.SetPropertyBlock(mpb);
                }
            }
        }

        public override void WriteNet(BinaryWriter w) { base.WriteNet(w); w.Write(Mathf.Max(0, hp)); }
        public override void ReadNet(BinaryReader r) { base.ReadNet(r); hp = r.ReadSingle(); }
    }
}

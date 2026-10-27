// TideWall 3D — the keep's courtyard (The Drowned Keep): step inside and the deep comes for you from every gate.
// Hold it: the clock only runs while someone is inside. Part I turned inside out — now you hold a ring of stone.
using System.IO;
using UnityEngine;

namespace TideWall
{
    public class HoldRing : Objective
    {
        public float radius = 11, seconds = 120;
        [SerializeField] Renderer ring;
        float held;
        bool started;

        public static HoldRing Create(Transform parent, Vector3 pos, float radius, float seconds)
        {
            var go = new GameObject("Courtyard");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var h = go.AddComponent<HoldRing>();
            h.radius = radius; h.seconds = seconds;
            h.ring = Glyphs.Decal(go.transform, "spiral", new Vector3(0, .04f, 0), Quaternion.Euler(90, 0, 0), radius * 2, new Color(.5f, 1f, .75f, .22f), glow: true).GetComponent<Renderer>();
            return h;
        }

        public override void Activate(Expedition e)
        {
            base.Activate(e);
            seconds = e.Stage.holdSeconds > 0 ? e.Stage.holdSeconds : seconds;
        }

        public override void Tick(float dt)
        {
            if (Done) return;
            bool inside = PlayersWithin(transform.position, radius) > 0;
            if (inside && !started)
            {
                started = true;
                GameManager.I.Banner("HOLD THE KEEP", "Everything in the deep is coming.");
                Sfx.Play("horn");
            }
            if (started && inside) held += dt;
            if (held >= seconds) { Finish(); expedition.ObjectiveDone(this); }
        }

        public bool Started => started;
        public override float Pressure => started && !Done ? 2.2f : 1;
        public override string Progress => Done ? null : started ? $"{Mathf.CeilToInt(seconds - held) / 60}:{Mathf.CeilToInt(seconds - held) % 60:00}" : "enter the courtyard";

        protected override void Finish()
        {
            base.Finish();
            held = seconds;
            if (ring) ring.enabled = false;
            Sfx.Play("bell");
        }

        void Update()
        {
            if (ring && !Done) ring.transform.localRotation = Quaternion.Euler(90, Time.time * (started ? -40 : -8), 0);
        }

        public override void WriteNet(BinaryWriter w) { base.WriteNet(w); w.Write(started); w.Write(held); }
        public override void ReadNet(BinaryReader r) { base.ReadNet(r); started = r.ReadBoolean(); held = r.ReadSingle(); }
    }
}

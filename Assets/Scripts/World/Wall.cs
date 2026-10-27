// TideWall 3D — the breachable sea wall: sections with their own HP that darken as they're battered, collapse
// into rubble when breached (with broken ends shown on the neighbours), and come back when repaired.
using System;
using Random = UnityEngine.Random;
using System.Collections.Generic;
using UnityEngine;

namespace TideWall
{
    /// <summary>The sea wall: sections with their own HP. A section at 0 HP is breached: it collapses, and creatures
    /// can walk through the gap to the powerplant. Repairs rebuild breached sections.</summary>
    public class Wall : MonoBehaviour
    {
        [Serializable]
        public class Section
        {
            public Transform t;
            public Renderer[] rs;
            public Collider col;
            public float hp, max, x;
            public bool breached;
            public Vector3 basePos;
            /// <summary>Broken-end caps shown when the neighbour on that side is breached, and this section's rubble.</summary>
            public GameObject capL, capR, rubble;
        }

        public List<Section> Sections = new();
        public float Hp { get { float h = 0; foreach (var s in Sections) h += s.hp; return h; } }
        public float MaxHp { get { float h = 0; foreach (var s in Sections) h += s.max; return h; } }
        public int BreachCount { get { int n = 0; foreach (var s in Sections) if (s.breached) n++; return n; } }
        MaterialPropertyBlock mpb;

        /// <summary>A fresh run: every section whole, with this many HP.</summary>
        public void Init(float hp)
        {
            foreach (var s in Sections) { s.hp = s.max = hp; s.breached = false; Look(s); }
        }

        public void Add(Transform t, Collider col, float hp)
        {
            mpb ??= new MaterialPropertyBlock();
            var s = new Section { t = t, rs = t.GetComponentsInChildren<Renderer>(), col = col, hp = hp, max = hp, x = t.position.x, basePos = t.localPosition };
            // Sections join seamlessly (their models have no end faces), so a breach needs caps on its neighbours' ends
            var lp = t.localPosition;
            s.capL = Decor("WallEnd", lp + new Vector3(-Arena.SegmentLength / 2, 0, 0), 180);
            s.capR = Decor("WallEnd", lp + new Vector3(Arena.SegmentLength / 2, 0, 0), 0);
            s.rubble = Decor("WallRubble", lp, (s.x * 37) % 10 - 5);
            Sections.Add(s);
        }

        GameObject Decor(string model, Vector3 localPos, float yaw)
        {
            var go = ModelFactory.Spawn(model, transform, localPos).root;
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            go.SetActive(false);
            return go;
        }

        Section At(float x)
        {
            foreach (var s in Sections) if (Mathf.Abs(s.x - x) < .1f) return s;
            return null;
        }

        /// <summary>Show a broken end wherever an intact section meets a breached one.</summary>
        void RefreshCaps()
        {
            foreach (var s in Sections)
            {
                var l = At(s.x - Arena.SegmentLength); var r = At(s.x + Arena.SegmentLength);
                if (s.capL) s.capL.SetActive(!s.breached && l != null && l.breached);
                if (s.capR) s.capR.SetActive(!s.breached && r != null && r.breached);
            }
        }

        Section Nearest(float x, bool intactOnly)
        {
            Section best = null; float bd = Arena.SegmentLength * .9f;
            foreach (var s in Sections)
            {
                if (intactOnly && s.breached) continue;
                float d = Mathf.Abs(s.x - x);
                if (d < bd) { bd = d; best = s; }
            }
            return best;
        }

        public bool NearestBreach(float x, out float bx)
        {
            bx = 0; float bd = float.MaxValue; bool found = false;
            foreach (var s in Sections)
                if (s.breached && Mathf.Abs(s.x - x) < bd) { bd = Mathf.Abs(s.x - x); bx = s.x; found = true; }
            return found;
        }

        /// <summary>Can something this wide stand at x on the wall line (a breached section)?</summary>
        public bool IsOpen(float x, float radius)
        {
            foreach (var s in Sections)
                if (s.breached && Mathf.Abs(s.x - x) < Arena.SegmentLength / 2 - radius * .4f) return true;
            return false;
        }

        public void Damage(float amount, Vector3 where)
        {
            var gm = GameManager.I;
            if (gm.State != GameState.Playing || amount <= 0 || Net.IsClient) return;
            var s = Nearest(where.x, true);
            if (s == null) return;
            s.hp = Mathf.Max(0, s.hp - amount);
            Look(s);
            Fx.Debris(new Vector3(where.x, 1, Arena.WallFront), new Color(.5f, .48f, .45f), 3);
            Sfx.Play("hit", .5f, .08f);
            if (s.hp <= 0) Breach(s);
        }

        void Breach(Section s)
        {
            s.breached = true;
            Look(s);
            Fx.Debris(s.t.position + Vector3.up, new Color(.5f, .48f, .45f), 30);
            Fx.Smoke(s.t.position + Vector3.up, 16, 1.2f, new Color(.75f, .7f, .62f, .7f));
            Sfx.Play("boom", .8f);
            GameManager.I.Banner("THE WALL IS BREACHED", "Creatures can reach the powerplant through the gap. Repair it in the morning!");
            foreach (var p in GameManager.I.Players.All) p.Shake(.3f);
        }

        /// <summary>A section's look: battered stone darkens; a breached one collapses into rubble and stops blocking.</summary>
        void Look(Section s)
        {
            mpb ??= new MaterialPropertyBlock();
            float dmg = 1 - s.hp / Mathf.Max(1, s.max);
            if (s.col) s.col.enabled = !s.breached;
            s.t.gameObject.SetActive(!s.breached);
            if (s.rubble) s.rubble.SetActive(s.breached);
            RefreshCaps();
            if (dmg <= .001f && !s.breached) { foreach (var r in s.rs) r.SetPropertyBlock(null); return; }
            var c = Color.Lerp(Color.white, new Color(.35f, .3f, .28f), s.breached ? 1 : dmg);
            foreach (var r in s.rs)
            {
                r.GetPropertyBlock(mpb);
                mpb.SetColor("_BaseColor", c * new Color(.55f, .53f, .5f, 1) * 1.6f);
                r.SetPropertyBlock(mpb);
            }
        }

        /// <summary>Repair upgrade: every section regains a fraction of its HP; breached ones are rebuilt.</summary>
        public void Repair(float fraction)
        {
            foreach (var s in Sections)
            {
                if (s.breached) { s.breached = false; s.hp = s.max * fraction; }
                else s.hp = Mathf.Min(s.max, s.hp + s.max * fraction);
                Look(s);
            }
        }

        public void Grow(float mult) { foreach (var s in Sections) { s.max *= mult; s.hp *= mult; } }

        public bool Damaged => Sections.Exists(s => s.breached || s.hp < s.max - .5f);

        /// <summary>Loading a save, or a client mirroring the host: set every section directly.</summary>
        public void Restore(IList<float> hps, IList<float> maxes)
        {
            if (hps == null || maxes == null) return;
            for (int i = 0; i < Sections.Count && i < hps.Count && i < maxes.Count; i++)
            {
                var s = Sections[i];
                bool wasBreached = s.breached;
                if (Mathf.Approximately(s.hp, hps[i]) && Mathf.Approximately(s.max, maxes[i])) continue;
                s.max = Mathf.Max(1, maxes[i]); s.hp = Mathf.Clamp(hps[i], 0, s.max);
                s.breached = s.hp <= 0;
                if (s.breached && !wasBreached && Net.IsClient) { Fx.Debris(s.t.position + Vector3.up, new Color(.5f, .48f, .45f), 20); }
                Look(s);
            }
        }
    }
}

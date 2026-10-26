// TideWall 3D — runtime references to the built world, plus the things that change per phase
// (picnic food, gas cans, torches, the lighthouse).
using System.Collections.Generic;
using UnityEngine;

namespace TideWall
{
    public class World : MonoBehaviour
    {
        public Light sun;
        public Wall wall;
        public Transform lighthouseLamp;
        public ModelInstance keep;
        public Light lighthouseBeam;
        public readonly List<Light> torches = new();
        public readonly List<Light> lanterns = new();       // player lanterns register here
        public readonly List<GameObject> phaseLoot = new(); // food and gas cans placed this morning

        float torchLevel, lanternLevel;

        public void OnPhaseStart(PhaseDefinition p, int extraGasCans)
        {
            var db = GameManager.I.DB;
            if (p.spawnFood)
            {
                // Picnic leftovers: free food scattered around the beach towels.
                var beachFoods = db.foods.FindAll(f => f.spawnsOnBeach);
                int n = Random.Range(3, 6);
                for (int i = 0; i < n && beachFoods.Count > 0; i++)
                    phaseLoot.Add(FoodPickup.Spawn(beachFoods[Random.Range(0, beachFoods.Count)], RandomBeachSpot(3, 15)).gameObject);
            }
            if (p.spawnGasCans)
            {
                int n = db.baseGasCans + extraGasCans;
                for (int i = 0; i < n; i++) phaseLoot.Add(GasCan.Spawn(RandomBeachSpot(2, 16)).gameObject);
            }
            phaseLoot.RemoveAll(g => g == null);
        }

        public static Vector3 RandomBeachSpot(float zMin, float zMax) =>
            new(Random.Range(-Arena.WallHalfLength + 2, Arena.WallHalfLength - 2), 0, Random.Range(zMin, zMax));

        public void SetNightLights(float torches01, float lanterns01)
        {
            torchLevel = torches01; lanternLevel = lanterns01;
            if (lighthouseBeam) lighthouseBeam.intensity = 60 * torches01;
        }

        void Update()
        {
            float t = Time.time;
            for (int i = 0; i < torches.Count; i++)
            {
                var l = torches[i];
                l.intensity = torchLevel * (6f + Mathf.PerlinNoise(t * 3, i * 7.3f) * 3f);
                l.enabled = torchLevel > .01f;
            }
            foreach (var l in lanterns) if (l) { l.intensity = lanternLevel * 4f; l.enabled = lanternLevel > .01f; }
            if (lighthouseLamp) lighthouseLamp.rotation = Quaternion.Euler(12, t * 40, 0);
        }
    }

    /// <summary>The sea wall: one shared HP pool across its segments. Damage shows on the nearest segment.</summary>
    public class Wall : MonoBehaviour
    {
        public float Hp { get; private set; }
        public float MaxHp { get; private set; }
        readonly List<(Transform t, Renderer[] rs, float dmg)> segments = new();
        MaterialPropertyBlock mpb;

        public void Init(float maxHp) { MaxHp = Hp = maxHp; mpb = new MaterialPropertyBlock(); }
        public void AddSegment(Transform t) => segments.Add((t, t.GetComponentsInChildren<Renderer>(), 0));

        public void Damage(float amount, Vector3 where)
        {
            var gm = GameManager.I;
            if (gm.State != GameState.Playing || amount <= 0) return;
            Hp = Mathf.Max(0, Hp - amount);
            int best = 0; float bd = float.MaxValue;
            for (int i = 0; i < segments.Count; i++)
            {
                float d = Mathf.Abs(segments[i].t.position.x - where.x);
                if (d < bd) { bd = d; best = i; }
            }
            if (segments.Count > 0)
            {
                var s = segments[best];
                s.dmg = Mathf.Min(1, s.dmg + amount / (MaxHp / segments.Count) * .5f);
                segments[best] = s;
                Tint(s.rs, s.dmg);
                Fx.Debris(new Vector3(where.x, 1, Arena.WallFront), new Color(.5f, .48f, .45f), 3);
            }
            Sfx.Play("hit", .5f, .08f);
            if (Hp <= 0) gm.GameOver("The sea broke through the wall.");
        }

        void Tint(Renderer[] rs, float dmg)
        {
            // Battered stone darkens and cracks (a darker base colour) as the segment takes hits.
            var c = Color.Lerp(Color.white, new Color(.35f, .3f, .28f), dmg);
            foreach (var r in rs)
            {
                r.GetPropertyBlock(mpb);
                mpb.SetColor("_BaseColor", c * new Color(.55f, .53f, .5f, 1) * 1.6f);
                r.SetPropertyBlock(mpb);
            }
        }

        public void Heal(float amount)
        {
            Hp = Mathf.Min(MaxHp, Hp + amount);
            float frac = 1 - Hp / MaxHp;
            for (int i = 0; i < segments.Count; i++)
            {
                var s = segments[i];
                s.dmg = Mathf.Min(s.dmg, frac);
                segments[i] = s;
                if (s.dmg <= .001f) foreach (var r in s.rs) r.SetPropertyBlock(null); else Tint(s.rs, s.dmg);
            }
        }

        public void Grow(float mult) { MaxHp *= mult; Hp *= mult; }

        /// <summary>Loading a save, or a client mirroring the host: set HP directly and re-tint the stones.</summary>
        public void Restore(float hp, float maxHp)
        {
            if (Mathf.Approximately(hp, Hp) && Mathf.Approximately(maxHp, MaxHp)) return;
            MaxHp = Mathf.Max(1, maxHp);
            Hp = Mathf.Clamp(hp, 0, MaxHp);
            float frac = 1 - Hp / MaxHp;
            for (int i = 0; i < segments.Count; i++)
            {
                var s = segments[i];
                s.dmg = frac;
                segments[i] = s;
                if (frac <= .001f) foreach (var r in s.rs) r.SetPropertyBlock(null); else Tint(s.rs, frac);
            }
        }
    }

    /// <summary>A gently rolling sea surface (CPU vertex waves on a grid).</summary>
    public class Water : MonoBehaviour
    {
        Mesh mesh; Vector3[] baseVerts, verts;

        public static Water Build(Transform parent, Rect area, float step)
        {
            var go = new GameObject("Sea");
            go.transform.SetParent(parent, false);
            var w = go.AddComponent<Water>();
            int nx = Mathf.CeilToInt(area.width / step) + 1, nz = Mathf.CeilToInt(area.height / step) + 1;
            var v = new Vector3[nx * nz]; var uv = new Vector2[v.Length]; var tris = new int[(nx - 1) * (nz - 1) * 6];
            for (int z = 0, i = 0; z < nz; z++) for (int x = 0; x < nx; x++, i++)
                { v[i] = new Vector3(area.xMin + x * step, 0, area.yMin + z * step); uv[i] = new Vector2(x, z) * .1f; }
            for (int z = 0, t = 0; z < nz - 1; z++) for (int x = 0; x < nx - 1; x++)
                {
                    int i = z * nx + x;
                    tris[t++] = i; tris[t++] = i + nx; tris[t++] = i + 1;
                    tris[t++] = i + 1; tris[t++] = i + nx; tris[t++] = i + nx + 1;
                }
            w.mesh = new Mesh { name = "Sea", vertices = v, uv = uv, triangles = tris };
            w.mesh.MarkDynamic();
            w.mesh.RecalculateNormals();
            go.AddComponent<MeshFilter>().sharedMesh = w.mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Mats.Transparent(new Color(.16f, .45f, .7f, .78f));
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            w.baseVerts = v; w.verts = (Vector3[])v.Clone();
            return w;
        }

        void Update()
        {
            float t = Time.time;
            for (int i = 0; i < verts.Length; i++)
            {
                var b = baseVerts[i];
                verts[i].y = Mathf.Sin(b.z * .35f - t * 1.6f) * .12f + Mathf.Sin(b.x * .22f + t * .9f) * .07f;
            }
            mesh.vertices = verts;
            mesh.RecalculateNormals();
        }
    }
}

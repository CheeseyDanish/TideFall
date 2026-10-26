// TideWall 3D — runtime references to the built world, plus the things that change per phase
// (picnic food, gas cans, torches, the lighthouse), the breachable sea wall and the sea itself.
using System.Collections.Generic;
using UnityEngine;

namespace TideWall
{
    public class World : MonoBehaviour
    {
        public Light sun;
        public Wall wall;
        public PowerPlant plant;
        public Transform lighthouseLamp;
        public ModelInstance keep;
        public Vendor chef, fishmonger;
        public Light lighthouseBeam;
        public readonly List<Light> torches = new();
        public readonly List<Light> lanterns = new();       // player lanterns register here
        public readonly List<GameObject> phaseLoot = new(); // food and gas cans placed this morning
        public readonly List<(Transform t, float speed)> clouds = new();

        float torchLevel, lanternLevel;

        public void OnPhaseStart(PhaseDefinition p, int extraGasCans)
        {
            var db = GameManager.I.DB;
            if (p.spawnFood)
            {
                // Picnic leftovers: free food scattered around the beach towels.
                var beachFoods = db.foods.FindAll(f => f.spawnsOnBeach);
                int n = Random.Range(5, 9);
                for (int i = 0; i < n && beachFoods.Count > 0; i++)
                    phaseLoot.Add(FoodPickup.Spawn(beachFoods[Random.Range(0, beachFoods.Count)], RandomBeachSpot(4, 30)).gameObject);
            }
            if (p.spawnGasCans)
            {
                int n = db.baseGasCans + extraGasCans;
                for (int i = 0; i < n; i++) phaseLoot.Add(GasCan.Spawn(RandomBeachSpot(3, 32)).gameObject);
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

        Material sky;

        /// <summary>The procedural sky follows the phase: tint from its sky colour, brightness from its sun.</summary>
        public void SetSky(Color tint, float sunIntensity)
        {
            if (sky == null)
            {
                var baseSky = Resources.Load<Material>("Materials/Sky");
                if (baseSky == null) return;
                sky = new Material(baseSky) { name = "Sky (runtime)" };
                RenderSettings.skybox = sky;
                RenderSettings.sun = sun;
            }
            float day = Mathf.Clamp01(sunIntensity / 1.3f);
            sky.SetColor("_SkyTint", Color.Lerp(new Color(.5f, .5f, .5f), tint, .4f));
            sky.SetColor("_GroundColor", Color.Lerp(new Color(.05f, .07f, .14f), new Color(.45f, .55f, .62f), day));
            sky.SetFloat("_Exposure", Mathf.Lerp(.18f, 1.1f, day));
            sky.SetFloat("_AtmosphereThickness", Mathf.Lerp(.5f, .85f, day));
            sky.SetFloat("_SunSize", .045f);
        }

        /// <summary>Every frame for every camera: the sky (when there is one; else the phase colour) and the graphics settings.</summary>
        public static void SetupCameraSky(Camera c, Color fallback)
        {
            GraphicsApply.ConfigureCamera(c);   // anti-aliasing, post-processing, draw distance from Settings
            if (RenderSettings.skybox != null) c.clearFlags = CameraClearFlags.Skybox;
            else { c.clearFlags = CameraClearFlags.SolidColor; c.backgroundColor = fallback; }
        }

        /// <summary>Where a creature at `from` should head when it isn't chasing a player:
        /// through the nearest breach if there is one (and on to the powerplant), else to the wall in front of it.</summary>
        public Vector3 Goal(Vector3 from, float radius, out GoalKind kind)
        {
            if (Arena.Inside(from)) { kind = GoalKind.Plant; return plant.Approach(from, radius); }
            if (wall.NearestBreach(from.x, out float bx) && Mathf.Abs(bx - from.x) < 40)
            {
                kind = GoalKind.Breach;
                // Line up with the gap first, then walk through it.
                return Mathf.Abs(from.x - bx) > .8f && from.z > Arena.WallFront + radius + 1.5f
                    ? new Vector3(bx, 0, Arena.WallFront + radius + 1)
                    : new Vector3(bx, 0, Arena.WallZ - 2.5f);
            }
            kind = GoalKind.Wall;
            return Arena.WallTarget(from.x, radius);
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
            foreach (var (c, speed) in clouds)
            {
                var p = c.position;
                p.x += speed * Time.deltaTime;
                if (p.x > 220) p.x = -220;
                c.position = p;
            }
        }
    }

    public enum GoalKind { Wall, Breach, Plant }

    /// <summary>The sea wall: sections with their own HP. A section at 0 HP is breached: it collapses, and creatures
    /// can walk through the gap to the powerplant. Repairs rebuild breached sections.</summary>
    public class Wall : MonoBehaviour
    {
        public class Section
        {
            public Transform t;
            public Renderer[] rs;
            public Collider col;
            public float hp, max, x;
            public bool breached;
            public Vector3 basePos;
        }

        public readonly List<Section> Sections = new();
        public float Hp { get { float h = 0; foreach (var s in Sections) h += s.hp; return h; } }
        public float MaxHp { get { float h = 0; foreach (var s in Sections) h += s.max; return h; } }
        public int BreachCount { get { int n = 0; foreach (var s in Sections) if (s.breached) n++; return n; } }
        MaterialPropertyBlock mpb;

        public void Add(Transform t, Collider col, float hp)
        {
            mpb ??= new MaterialPropertyBlock();
            Sections.Add(new Section { t = t, rs = t.GetComponentsInChildren<Renderer>(), col = col, hp = hp, max = hp, x = t.position.x, basePos = t.localPosition });
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

        /// <summary>A section's look: battered stone darkens; a breached one slumps into rubble and stops blocking.</summary>
        void Look(Section s)
        {
            float dmg = 1 - s.hp / Mathf.Max(1, s.max);
            if (s.col) s.col.enabled = !s.breached;
            s.t.localPosition = s.breached ? s.basePos + new Vector3(0, -1.9f, 0) : s.basePos;
            s.t.localRotation = s.breached ? Quaternion.Euler(8, 0, (s.x % 3) - 1.5f) : Quaternion.identity;
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

    /// <summary>A gently rolling sea (CPU vertex waves on a grid) with breaking-wave swells and a foam line at the shore.</summary>
    public class Water : MonoBehaviour
    {
        Mesh mesh; Vector3[] baseVerts, verts;
        Transform foam;

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
            w.mesh = new Mesh { name = "Sea", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32, vertices = v, uv = uv, triangles = tris };
            w.mesh.MarkDynamic();
            w.mesh.RecalculateNormals();
            go.AddComponent<MeshFilter>().sharedMesh = w.mesh;
            var mr = go.AddComponent<MeshRenderer>();
            var m = new Material(Mats.Transparent(new Color(.14f, .46f, .68f, .8f))) { name = "Sea" };
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", .9f);
            mr.sharedMaterial = m;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            w.baseVerts = v; w.verts = (Vector3[])v.Clone();

            // Foam where the waves meet the sand
            var f = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(f.GetComponent<Collider>());
            f.name = "Foam";
            f.transform.SetParent(parent, false);
            f.transform.localScale = new Vector3(area.width, .02f, 2.2f);
            var fr = f.GetComponent<MeshRenderer>();
            fr.sharedMaterial = Mats.Transparent(new Color(.97f, .99f, 1f, .55f));
            fr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            w.foam = f.transform;
            return w;
        }

        void Update()
        {
            float t = Time.time;
            var breakers = Breaker.All;
            for (int i = 0; i < verts.Length; i++)
            {
                var b = baseVerts[i];
                float y = Mathf.Sin(b.z * .35f - t * 1.6f) * .12f + Mathf.Sin(b.x * .22f + t * .9f) * .07f;
                for (int k = 0; k < breakers.Count; k++)
                {
                    var br = breakers[k];
                    float dz = b.z - br.Z;
                    if (dz < -6 || dz > 10) continue;
                    float dx = Mathf.Abs(b.x - br.X) - br.Width * .5f;
                    float edge = dx <= 0 ? 1 : Mathf.Clamp01(1 - dx / 5);
                    y += br.Swell * edge * Mathf.Exp(-(dz * dz) / (dz < 0 ? 3f : 14f));   // steep front, long back
                }
                verts[i].y = y;
            }
            mesh.vertices = verts;
            mesh.RecalculateNormals();
            if (foam)
            {
                float s = Mathf.Sin(t * .9f);
                foam.position = new Vector3(0, .03f + Mathf.Max(0, s) * .02f, Arena.ShoreZ - .6f - s * 1.4f);
            }
        }
    }
}

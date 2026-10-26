// TideWall 3D — a breaking wave. Creatures don't just appear: the WaveDirector puts them on a breaker far out at
// sea, it rolls in (swelling the sea mesh as it comes), and at the shore it breaks and drops them in the surf
// (further up the sand under the King Tide card). Flyers come in over the water on their own.
// Riders surf in on little boards, wobbling for balance; about one in six wipes out on the way (cartwheels into the
// surf, board flying). The lip throws spray, and the swell under it rears up as it nears the shore.
using System.Collections.Generic;
using UnityEngine;

namespace TideWall
{
    public class Breaker : MonoBehaviour
    {
        public static readonly List<Breaker> All = new();

        public float X => transform.position.x;
        public float Z => transform.position.z;
        public float Width { get; private set; }
        /// <summary>How high the sea swells under it (0 once it has broken and drained away).</summary>
        public float Swell { get; private set; } = 1.4f;
        public bool Broken { get; private set; }
        public int Riders => riders.Count;
        public float BornAt { get; private set; }

        readonly List<(Enemy e, float dx, float dz, float wipeZ)> riders = new();
        float sprayT;
        ModelInstance model;
        float releaseZ, speed, fade;
        bool proxy;

        public static Breaker Launch(float x, float width, float startZ, float releaseZ, float speed)
        {
            var b = Make(x, width, startZ);
            b.releaseZ = releaseZ; b.speed = speed;
            NetId.Tag(b.gameObject, NetKind.Breaker, Mathf.RoundToInt(width));
            return b;
        }

        /// <summary>Client: a mirror of the host's breaker (position comes from snapshots).</summary>
        public static Breaker SpawnProxy(int id, float x, float width, float z)
        {
            var b = Make(x, width, z);
            b.proxy = true;
            NetId.Tag(b.gameObject, NetKind.Breaker, Mathf.RoundToInt(width), id);
            b.gameObject.AddComponent<NetFollow>().Set(b.transform.position, b.transform.rotation);
            return b;
        }

        static Breaker Make(float x, float width, float z)
        {
            var go = new GameObject("Breaker");
            go.transform.SetPositionAndRotation(new Vector3(x, Arena.WaterLevel - .2f, z), Quaternion.Euler(0, 180, 0));   // the curl faces the shore
            var b = go.AddComponent<Breaker>();
            b.Width = width;
            b.BornAt = Time.time;
            b.model = ModelFactory.Spawn("Breaker", go.transform, Vector3.zero);
            b.model.root.transform.localScale = new Vector3(width, 1, 1);
            foreach (var r in b.model.renderers)
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                // Water you can half see through (the foam stays near-solid white)
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var c = mats[i] != null && mats[i].HasProperty("_BaseColor") ? mats[i].GetColor("_BaseColor") : Color.white;
                    bool foam = c.r > .85f && c.g > .85f;
                    var w = foam ? c : Color.Lerp(c, new Color(.22f, .58f, .72f), .65f);   // sea-glass blue under the foam
                    mats[i] = Mats.Transparent(new Color(w.r, w.g, w.b, foam ? .95f : .8f));
                }
                r.sharedMaterials = mats;
            }
            All.Add(b);
            return b;
        }

        void OnDestroy() => All.Remove(this);

        /// <summary>A creature climbs aboard at an offset along the crest (canWipeOut: false keeps it on to the shore).</summary>
        public void Carry(Enemy e, float dx, float dz, bool canWipeOut = true)
        {
            // Some won't make it: they wipe out somewhere between here and the shore
            float wipeZ = canWipeOut && Random.value < GameManager.I.DB.breakerWipeoutChance ? Random.Range(releaseZ + 6, Mathf.Max(releaseZ + 7, transform.position.z - 8)) : float.NegativeInfinity;
            riders.Add((e, dx, dz, wipeZ));
            e.Ride(this);
        }

        public Vector3 RiderPosition(float dx, float dz) => new(X + dx, Arena.WaterLevel + Swell * .55f, Z + dz);

        void Update()
        {
            float dt = Time.deltaTime;
            // Spray off the lip (every machine makes its own)
            sprayT -= dt;
            if (sprayT <= 0 && !Broken && Swell > .5f)
            {
                sprayT = .08f;
                Fx.Spray(new Vector3(X + Random.Range(-Width / 2, Width / 2), Arena.WaterLevel + Swell * 1.4f, Z - 1.2f));
            }
            if (proxy)
            {
                // The host decides when it breaks; a sinking crest means it has.
                Swell = Mathf.Clamp(1.4f * (transform.position.y + 1.2f) / 1.0f, 0, 1.4f);
                return;
            }
            if (!Broken)
            {
                var p = transform.position;
                p.z -= speed * dt;
                p.y = Arena.WaterLevel - .2f + Mathf.Max(0, Arena.GroundY(p.z) - Arena.WaterLevel);   // rides up the sand under a King Tide
                transform.position = p;
                Swell = Mathf.Lerp(1.4f, 2.0f, Mathf.InverseLerp(releaseZ + 40, releaseZ, p.z));   // it rears up near the shore
                model.root.transform.localScale = new Vector3(Width, Swell / 1.4f * 1.5f, 1.3f);
                for (int i = riders.Count - 1; i >= 0; i--)
                {
                    var (e, dx, dz, wipeZ) = riders[i];
                    if (e == null || e.Dead) { riders.RemoveAt(i); continue; }
                    if (p.z <= wipeZ) { riders.RemoveAt(i); e.WipeOut(); continue; }
                    // Bob with the face of the wave, a little behind the lip
                    e.transform.position = RiderPosition(dx, dz) + Vector3.up * Mathf.Sin(Time.time * 3 + dx) * .15f;
                }
                if (p.z <= releaseZ) Break();
                return;
            }
            // Broken: the water drains away, then the breaker is gone
            fade += dt;
            Swell = Mathf.Max(0, 1.4f * (1 - fade / 1.2f));
            transform.position += Vector3.down * dt * 1.2f;
            if (fade > 1.5f) Destroy(gameObject);
        }

        void Break()
        {
            Broken = true;
            foreach (var (e, _, _, _) in riders)
                if (e != null && !e.Dead) e.Ride(null);
            riders.Clear();
            for (float x = -Width / 2; x <= Width / 2; x += 3) Fx.Splash(new Vector3(X + x, .3f, Z), 6);
            Sfx.Play("splash", .7f, .15f);
        }
    }
}

// TideWall 3D — sand dollars, the money. Creatures drop them when they die (their bounty, scaled by tidal cards);
// they scatter on the sand and slide to any player who comes close (Fortune adds to what you pick up). Left too
// long, or dropped in the surf, they wash away. Collected sand dollars go into the shared team purse.
using UnityEngine;

namespace TideWall
{
    public class SandDollarPickup : MonoBehaviour
    {
        public int Value;
        Vector3 vel;
        float life, spin;
        bool landed, proxy;
        Player target;

        /// <summary>Scatter `total` sand dollars from a point (a few discs, each worth a share).</summary>
        public static void Drop(Vector3 at, int total)
        {
            if (Net.IsClient || total <= 0 || GameManager.I == null) return;
            int n = Mathf.Clamp(total / 3, 1, 6);
            for (int i = 0; i < n; i++)
            {
                int v = total / n + (i < total % n ? 1 : 0);
                if (v > 0) Spawn(at + Vector3.up * .3f, v, new Vector3(Random.Range(-2.5f, 2.5f), Random.Range(3f, 5.5f), Random.Range(-2.5f, 2.5f)));
            }
        }

        static SandDollarPickup Spawn(Vector3 at, int value, Vector3 velocity)
        {
            var go = new GameObject("SandDollar");
            go.transform.position = at;
            var s = go.AddComponent<SandDollarPickup>();
            s.Value = value; s.vel = velocity;
            float cardLife = GameManager.I.Cards.SandDollarLifetime;
            s.life = cardLife > 0 ? cardLife : GameManager.I.DB.sandDollarLifetime;
            var mi = ModelFactory.Spawn("SandDollar", go.transform, Vector3.zero, 1.6f + Mathf.Min(value, 20) * .03f);
            foreach (var r in mi.renderers) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            NetId.Tag(go, NetKind.SandDollar, value);
            return s;
        }

        /// <summary>Client: a mirror of the host's sand dollar.</summary>
        public static SandDollarPickup SpawnProxy(int id, int value, Vector3 at)
        {
            var go = new GameObject("SandDollar");
            go.transform.position = at;
            var s = go.AddComponent<SandDollarPickup>();
            s.Value = value; s.proxy = true;
            var mi = ModelFactory.Spawn("SandDollar", go.transform, Vector3.zero, 1.6f + Mathf.Min(value, 20) * .03f);
            foreach (var r in mi.renderers) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            NetId.Tag(go, NetKind.SandDollar, value, id);
            go.AddComponent<NetFollow>().Set(at, Quaternion.identity);
            return s;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            spin += dt * 120;
            if (proxy) { transform.GetChild(0).localRotation = Quaternion.Euler(0, spin, 0); return; }
            var gm = GameManager.I;
            var p = transform.position;
            if (!landed)
            {
                vel += Physics.gravity * dt;
                p += vel * dt;
                float g = Arena.GroundY(p.z) + .05f;
                if (p.y <= g) { p.y = g; landed = true; vel = Vector3.zero; }
            }
            // Slide to the nearest player in reach
            if (target == null || !target.IsTargetable || Vector3.Distance(target.transform.position, p) > gm.DB.sandDollarMagnet * 1.5f)
            {
                target = null; float bd = gm.DB.sandDollarMagnet;
                foreach (var pl in gm.Players.All)
                {
                    if (!pl.IsTargetable) continue;
                    float d = Vector3.Distance(pl.transform.position, p);
                    if (d < bd) { bd = d; target = pl; }
                }
            }
            if (target != null && landed)
            {
                var to = target.transform.position + Vector3.up * .8f - p;
                p += to.normalized * Mathf.Min(to.magnitude, (6 + 14 / Mathf.Max(.5f, to.magnitude)) * dt);
                if (to.magnitude < .7f) { Collect(target); return; }
            }
            // The surf takes them; so does time
            if (Arena.InWater(p)) { p.z += dt * 1.5f; life -= dt * 3; }
            life -= dt;
            transform.position = p;
            transform.GetChild(0).localRotation = Quaternion.Euler(0, spin, 0);
            bool blink = life < 3 && Mathf.Sin(Time.time * 18) > 0;
            transform.GetChild(0).gameObject.SetActive(!blink);
            if (life <= 0) Destroy(gameObject);
        }

        void Collect(Player p)
        {
            int v = Mathf.Max(1, Mathf.RoundToInt(Value * p.FortuneMultiplier));
            GameManager.I.AddSandDollars(v, transform.position, null);
            Fx.Sparks(transform.position, new Color(1, .95f, .75f), 5);
            Destroy(gameObject);
        }
    }
}

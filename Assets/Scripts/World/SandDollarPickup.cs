// TideWall 3D — loot: sand dollars (the money) and golden pearls (rare, from giant clams). When a creature dies
// they burst out of it and FLY TO WHOEVER KILLED IT, low over the sand, so a teammate standing in the way can
// intercept them ("caught it!"). With no killer (fire, explosions), they land and slide to the nearest player.
// Fortune adds to the sand dollars you catch. Left too long, or in the surf, they wash away.
// Everything goes into the shared team purse.
using UnityEngine;

namespace TideWall
{
    public class SandDollarPickup : MonoBehaviour
    {
        public int Value;
        public bool Pearl { get; private set; }
        Vector3 vel;
        float life, spin, flyDelay;
        bool landed, proxy;
        Player target, killer;

        /// <summary>Burst `total` sand dollars (or pearls) out of a creature, flying toward its killer.</summary>
        public static void Drop(Vector3 at, int total, Player killer, bool pearl = false)
        {
            if (Net.IsClient || total <= 0 || GameManager.I == null) return;
            int n = pearl ? total : Mathf.Clamp(total / 3, 1, 6);
            for (int i = 0; i < n; i++)
            {
                int v = pearl ? 1 : total / n + (i < total % n ? 1 : 0);
                if (v <= 0) continue;
                var s = Spawn(at + Vector3.up * .3f, v, pearl, new Vector3(Random.Range(-2f, 2f), Random.Range(3f, 5f), Random.Range(-2f, 2f)));
                s.killer = killer != null && killer.IsTargetable ? killer : null;
                s.flyDelay = .35f + i * .06f;   // pop up first, then streak off
            }
        }

        static SandDollarPickup Spawn(Vector3 at, int value, bool pearl, Vector3 velocity)
        {
            var go = new GameObject(pearl ? "Pearl" : "SandDollar");
            go.transform.position = at;
            var s = go.AddComponent<SandDollarPickup>();
            s.Value = value; s.vel = velocity; s.Pearl = pearl;
            float cardLife = GameManager.I.Cards.SandDollarLifetime;
            s.life = pearl ? 60 : cardLife > 0 ? cardLife : GameManager.I.DB.sandDollarLifetime;
            s.Model();
            NetId.Tag(go, NetKind.SandDollar, pearl ? -value : value);
            return s;
        }

        void Model()
        {
            var mi = ModelFactory.Spawn(Pearl ? "Pearl" : "SandDollar", transform, Vector3.zero, Pearl ? 1.3f : 1.6f + Mathf.Min(Value, 20) * .03f);
            foreach (var r in mi.Renderers) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (Pearl)
            {
                foreach (var r in mi.Renderers) foreach (var m in r.materials) Mats.SetEmission(m, new Color(1f, .8f, .3f) * .8f);
                var l = new GameObject("Glow").AddComponent<Light>();
                l.transform.SetParent(transform, false);
                l.type = LightType.Point; l.range = 3; l.intensity = 2; l.color = new Color(1, .85f, .4f); l.shadows = LightShadows.None;
            }
        }

        /// <summary>Client: a mirror of the host's sand dollar or pearl (def < 0 = pearl).</summary>
        public static SandDollarPickup SpawnProxy(int id, int def, Vector3 at)
        {
            var go = new GameObject(def < 0 ? "Pearl" : "SandDollar");
            go.transform.position = at;
            var s = go.AddComponent<SandDollarPickup>();
            s.Value = Mathf.Abs(def); s.proxy = true; s.Pearl = def < 0;
            s.Model();
            NetId.Tag(go, NetKind.SandDollar, def, id);
            go.AddComponent<NetFollow>().Set(at, Quaternion.identity);
            return s;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            spin += dt * 160;
            var model = transform.GetChild(0);
            if (proxy) { model.localRotation = Quaternion.Euler(0, spin, 0); return; }
            var gm = GameManager.I;
            var db = gm.DB;
            var p = transform.position;
            flyDelay -= dt;

            // Anyone who gets close catches it, including a teammate it flies past on its way to the killer
            foreach (var pl in gm.Players.All)
            {
                if (!pl.IsTargetable) continue;
                if (Vector3.Distance(pl.transform.position + Vector3.up * .9f, p) < (flyDelay <= 0 ? db.lootIntercept : .6f)) { Collect(pl); return; }
            }

            if (killer != null && killer.IsTargetable && flyDelay <= 0)
            {
                // Streak toward the killer's chest, low over the sand
                var to = killer.transform.position + Vector3.up * .9f - p;
                vel = Vector3.Lerp(vel, to.normalized * db.lootFlySpeed, dt * 4);
                p += vel * dt;
                p.y = Mathf.Max(p.y, Arena.GroundY(p.z) + .3f);
                if (Random.value < dt * 20) Fx.Sparkle(p, Pearl);
            }
            else
            {
                if (!landed)
                {
                    vel += Physics.gravity * dt;
                    p += vel * dt;
                    float g = Arena.GroundY(p.z) + .05f;
                    if (p.y <= g) { p.y = g; landed = true; vel = Vector3.zero; }
                }
                // Slide to the nearest player in reach
                if (target == null || !target.IsTargetable || Vector3.Distance(target.transform.position, p) > db.sandDollarMagnet * 1.5f)
                {
                    target = null; float bd = db.sandDollarMagnet;
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
                }
                // The surf takes them; so does time
                if (Arena.InWater(p)) { p.z += dt * 1.5f; life -= dt * 3; }
                life -= dt;
            }
            transform.position = p;
            model.localRotation = Quaternion.Euler(0, spin, 0);
            bool blink = life < 3 && Mathf.Sin(Time.time * 18) > 0;
            model.gameObject.SetActive(!blink);
            if (life <= 0) Destroy(gameObject);
        }

        void Collect(Player p)
        {
            var gm = GameManager.I;
            if (Pearl)
            {
                gm.AddPearls(Value, transform.position);
                p.Toast(killer != null && killer != p ? $"You caught {killer.Name}'s golden pearl!" : "A golden pearl!", new Color(1, .85f, .4f));
            }
            else
            {
                int v = Mathf.Max(1, Mathf.RoundToInt(Value * p.FortuneMultiplier));
                gm.AddSandDollars(v, transform.position, null);
                if (killer != null && killer != p) p.Toast($"Intercepted {v} sand dollars from {killer.Name}!", new Color(1f, .93f, .75f));
            }
            Fx.Sparks(transform.position, Pearl ? new Color(1, .85f, .3f) : new Color(1, .95f, .75f), Pearl ? 12 : 5);
            Destroy(gameObject);
        }
    }
}

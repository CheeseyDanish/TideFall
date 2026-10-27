// TideWall 3D — the only ways to hurt things: gun shots, melee swings and explosions.
// Shots fly from the muzzle toward the 3D point under the player's reticle, so you hit the limb you can see
// on screen. Melee hits the limb nearest the blade. Enemy colliders are triggers, so queries include triggers.
using System.Collections.Generic;
using UnityEngine;

namespace TideWall
{
    public struct DamageInfo
    {
        public float amount;
        public Element element;
        public bool ignoreArmor, melee, explosion, chained;
        public Player source;
        public Vector3 point, dir;
        public float knockback, stun, pull;
        public float limbBonus;   // weapon mods: extra damage to limbs (0.6 = +60%)
    }

    public static class Combat
    {
        static readonly RaycastHit[] hits = new RaycastHit[48];
        static readonly Collider[] overlaps = new Collider[96];
        static readonly HashSet<Enemy> struck = new();

        /// <summary>Fire one shot (all pellets) from origin toward target.</summary>
        public static void FireGun(Player p, GunDef g, Vector3 origin, Vector3 target, float damageMult)
        {
            var baseDir = (target - origin).normalized;
            for (int i = 0; i < g.pellets; i++)
            {
                var dir = Quaternion.Euler(Random.Range(-g.spreadDegrees, g.spreadDegrees) * .3f, Random.Range(-g.spreadDegrees, g.spreadDegrees), 0) * baseDir;
                Vector3 end = origin + dir * g.range;
                int n = Physics.RaycastNonAlloc(origin, dir, hits, g.range, ~0, QueryTriggerInteraction.Collide);
                System.Array.Sort(hits, 0, n, Comparer<RaycastHit>.Create((a, b) => a.distance.CompareTo(b.distance)));
                int pierce = g.pierce;
                struck.Clear();
                for (int k = 0; k < n; k++)
                {
                    var h = hits[k];
                    var c = h.collider;
                    if (c.GetComponentInParent<Player>() != null || c.GetComponentInParent<Wall>() != null) continue;   // shots clear the wall top
                    if (c.GetComponentInParent<Carryable>() is { Holder: not null }) continue;
                    var e = c.GetComponentInParent<Enemy>();
                    if (e != null)
                    {
                        if (e.Dead || !struck.Add(e)) continue;
                        e.TakeHit(new DamageInfo
                        {
                            amount = g.damage * damageMult, element = g.element, source = p, point = h.point, dir = dir,
                            knockback = g.knockback, ignoreArmor = g.pierce > 0, limbBonus = g.limbBonus,
                        }, c.GetComponentInParent<Limb>());
                        if (g.splash > 0) Explode(h.point, g.splash, g.damage * damageMult * .5f, g.element, p, false, e);
                        if (pierce-- <= 0) { end = h.point; break; }
                        continue;
                    }
                    if (c.GetComponentInParent<GasCan>() is { } can) { can.Hit(g.element, p); end = h.point; break; }
                    if (c.GetComponentInParent<IShootable>() is { Alive: true } shootable)
                    {
                        shootable.Shot(new DamageInfo { amount = g.damage * damageMult, element = g.element, source = p, point = h.point, dir = dir });
                        Fx.Sparks(h.point, new Color(.9f, 1f, .9f), 4);
                        if (g.splash > 0) Explode(h.point, g.splash, g.damage * damageMult * .5f, g.element, p, false);
                        end = h.point;
                        break;
                    }
                    if (c.isTrigger || c.GetComponentInParent<Corpse>() != null) continue;
                    // Hit the world (sand, rocks, stalls)
                    end = h.point;
                    if (Arena.InWater(h.point)) Fx.Splash(h.point, 4); else Fx.Smoke(h.point, 2, .1f, new Color(.9f, .82f, .6f, .6f));
                    if (g.splash > 0) Explode(h.point, g.splash, g.damage * damageMult * .5f, g.element, p, false);
                    break;
                }
                Fx.Tracer(origin, end, g.element == Element.Fire ? new Color(1, .55f, .2f) : new Color(1, .95f, .7f), g.pierce > 0 ? .08f : .045f);
            }
        }

        /// <summary>A melee swing: everything in the arc takes a hit on the limb nearest the blade.</summary>
        public static int Melee(Player p, MeleeDef m, Vector3 center, Vector3 forward, float damageMult)
        {
            int count = Physics.OverlapSphereNonAlloc(center, m.range, overlaps, ~0, QueryTriggerInteraction.Collide);
            var blade = center + forward * m.range * .65f;
            struck.Clear();
            var cans = new List<GasCan>();
            var targets = new HashSet<IShootable>();
            int landed = 0;
            for (int i = 0; i < count; i++)
            {
                var c = overlaps[i];
                var to = c.bounds.ClosestPoint(center) - center; to.y = 0;
                if (to.sqrMagnitude > .01f && Vector3.Angle(forward, to) > m.arcDegrees / 2) continue;
                if (c.GetComponentInParent<GasCan>() is { Holder: null } can) { cans.Add(can); continue; }
                if (c.GetComponentInParent<IShootable>() is { Alive: true } target && targets.Add(target))
                {
                    target.Shot(new DamageInfo { amount = m.damage * damageMult, element = m.element, melee = true, source = p, point = c.bounds.ClosestPoint(blade), dir = forward });
                    landed++;
                    continue;
                }
                var e = c.GetComponentInParent<Enemy>();
                if (e == null || e.Dead || !struck.Add(e)) continue;
                var limb = e.LimbNearest(blade);
                var dir = (e.transform.position - center); dir.y = 0; dir.Normalize();
                e.TakeHit(new DamageInfo
                {
                    amount = m.damage * damageMult, element = m.element, melee = true, source = p, point = c.bounds.ClosestPoint(blade),
                    dir = dir, knockback = m.knockback, stun = m.stun, pull = m.pull, limbBonus = m.limbBonus,
                }, limb);
                if (e.Def.thorns > 0) p.TakeDamage(e.Def.thorns, e, "spines");
                landed++;
            }
            foreach (var can in cans) if (can != null) can.Hit(m.element, p);   // after the loop: an explosion reuses physics queries
            return landed;
        }

        /// <summary>Damage everything within radius (falls off to half at the edge). Gas cans chain.</summary>
        public static void Explode(Vector3 pos, float radius, float damage, Element element, Player source, bool hurtsPlayers = false, Enemy exclude = null)
        {
            Fx.Explosion(pos, radius);
            Sfx.Play("boom", Mathf.Clamp01(radius / 4f), .05f);
            // Own buffers: explosions can happen inside a shot or swing loop (a gas can going off), which use the shared ones.
            var found = Physics.OverlapSphere(pos, radius, ~0, QueryTriggerInteraction.Collide);
            var hitSet = new HashSet<Enemy>();
            var targets = new HashSet<IShootable>();
            var cans = new List<GasCan>();
            foreach (var c in found)
            {
                if (c.GetComponentInParent<GasCan>() is { } can) { cans.Add(can); continue; }
                if (c.GetComponentInParent<IShootable>() is { Alive: true } target && targets.Add(target))
                {
                    target.Shot(new DamageInfo { amount = damage, element = element, explosion = true, ignoreArmor = true, source = source, point = pos });
                    continue;
                }
                var e = c.GetComponentInParent<Enemy>();
                if (e == null || e == exclude || e.Dead || !hitSet.Add(e)) continue;
                float d = Vector3.Distance(pos, e.transform.position);
                var dir = e.transform.position - pos; dir.y = 0;
                e.TakeHit(new DamageInfo
                {
                    amount = damage * Mathf.Lerp(1, .5f, d / radius), element = element, explosion = true, ignoreArmor = true,
                    source = source, point = e.transform.position, dir = dir.normalized, knockback = 6,
                }, null);
            }
            if (hurtsPlayers)
                foreach (var pl in GameManager.I.Players.All)
                {
                    float d = Vector3.Distance(pos, pl.transform.position);
                    if (d < radius) pl.TakeDamage(damage * .5f * Mathf.Lerp(1, .4f, d / radius), null, "an explosion", (pl.transform.position - pos).normalized * 6);
                }
            foreach (var pl in GameManager.I.Players.All)
                pl.Shake(Mathf.Clamp01(1 - Vector3.Distance(pos, pl.transform.position) / (radius * 4)) * radius * .12f);
            foreach (var can in cans) if (can != null) can.Explode(source);
        }
    }
}

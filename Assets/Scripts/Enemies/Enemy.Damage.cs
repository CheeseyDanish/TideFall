// TideWall 3D — Enemy: hits, weak points, severed limbs, burning, death, corpses and loot.
// (One part of the Enemy class; Enemy.cs has the overview, the fields and the frame loop.)
using System.Collections.Generic;
using UnityEngine;

namespace TideWall
{
    public partial class Enemy
    {
        // ------------------------------------------------------------------ damage
        public Limb LimbNearest(Vector3 point)
        {
            Limb best = null; float bd = .9f;   // only counts if the blade is actually near the limb
            foreach (var l in Limbs)
            {
                if (l.Severed) continue;
                float d = Vector3.Distance(point, ModelFactory.Bounds(l.gameObject).ClosestPoint(point));
                if (d < bd) { bd = d; best = l; }
            }
            return best;
        }

        public void TakeHit(DamageInfo d, Limb limb)
        {
            if (Dead) return;
            if (limb != null && (limb.Severed || limb.Owner != this)) limb = null;
            float dmg = d.amount;
            if (limb != null) dmg *= limb.Def.critMultiplier;
            if (d.element == Element.Shock && Wet > .3f)
            {
                dmg *= gm.DB.shockWetMultiplier;
                Fx.Sparks(bounds.center, new Color(.6f, .9f, 1f), 10);
                Sfx.Play("zap");
                if (!d.chained) ChainShock(d);
            }
            // A Clawback's raised claws turn aside anything from the front; a Shellback's bulk covers those around it
            if (Def.frontGuard < 1 && limb == null && !d.explosion && BothClaws() && d.dir.sqrMagnitude > .01f && Vector3.Dot(d.dir, transform.forward) < -.3f)
            {
                dmg *= Def.frontGuard;
                Fx.Sparks(d.point == Vector3.zero ? bounds.center : d.point, new Color(1, .75f, .5f), 6);
                Sfx.Play("clink", .7f, .05f);
            }
            if (!d.explosion) dmg *= Guarded();
            if (Tucked) dmg *= .3f;
            if (!d.ignoreArmor && armor > 0)
            {
                // Glancing off a shell: you can see and hear that this isn't the place to shoot
                dmg = Mathf.Max(1, dmg - armor);
                Fx.Sparks(d.point == Vector3.zero ? bounds.center : d.point, new Color(1, .9f, .6f), 3);
                Sfx.Play("clink", .5f, .05f);
            }
            if (d.element == Element.Fire)
            {
                if (Wet > .3f) { Wet = 0; Fx.Smoke(bounds.center, 8, .3f, new Color(.95f, .95f, 1f, .6f)); }   // steam: dries, no burn
                else { burnT = gm.DB.burnSeconds; burnDps = gm.DB.burnDps * (d.source != null ? 1 : 1.5f); }
            }
            Hp -= dmg;
            flashT = .08f;
            flinch = .18f;
            hitLean = d.dir.sqrMagnitude > .01f ? Quaternion.Inverse(transform.rotation) * d.dir : Vector3.forward * -1;
            bool crit = limb != null && limb.Def.critMultiplier > 1;
            if (d.melee || crit) stallT = Mathf.Max(stallT, d.melee ? .1f : .05f);   // a solid hit holds it for a moment
            bool boss = Def.behaviour == EnemyBehaviour.Boss;
            if (!boss && d.dir.sqrMagnitude > .01f) knock += d.dir * Mathf.Min(1.5f, dmg * .05f) / Mathf.Max(1, Radius);    // every hit shoves it, a little
            poiseAcc += dmg / Mathf.Max(1, MaxHp);
            if (poiseAcc >= Def.poise && Hp > 0)
            {
                // Hit hard enough, fast enough, it staggers: whatever it was winding up to is off
                poiseAcc = 0; windupT = 0; windupTarget = null;
                stunT = Mathf.Max(stunT, boss ? .15f : .4f);
                Sfx.Play("thud", .5f, .08f);
            }
            if (d.source != null) Fx.DamageNumber(d.point == Vector3.zero ? bounds.center + Vector3.up * bounds.extents.y : d.point, dmg,
                                                   limb != null && limb.Def.critMultiplier > 1, d.source);
            var wound = d.point == Vector3.zero ? bounds.center : d.point;
            if (d.dir.sqrMagnitude > .01f) { Fx.Burst(wound, -d.dir + Vector3.up * .5f, GoreColor, crit ? 9 : 5, crit ? 5 : 3.5f); Fx.Burst(wound, d.dir + Vector3.up * .3f, GoreColor, 3, 5); }
            else Fx.Gore(wound, GoreColor, 4);
            Sfx.Play("hit", .6f);
            d.source?.OnHitEnemy(this, dmg, limb != null && limb.Def.critMultiplier > 1, d.melee);
            if (d.knockback > 0) knock += d.dir * d.knockback * (Def.behaviour == EnemyBehaviour.Boss ? .2f : 1);
            if (d.pull > 0 && d.source != null)
            {
                var toward = d.source.transform.position - transform.position; toward.y = 0;
                knock += toward.normalized * d.pull * 4;
            }
            if (d.stun > 0) stunT = Mathf.Max(stunT, Def.behaviour == EnemyBehaviour.Boss ? d.stun * .25f : d.stun);
            if (grabbed != null && d.source != null && d.source != grabbed) { stunT = Mathf.Max(stunT, .4f); Release(); }   // teammates break grabs
            if (limb != null)
            {
                limb.Hp -= dmg * (1 + d.limbBonus);
                if (limb.Hp <= 0) { Sever(limb, d); if (Dead) return; }
            }
            if (Hp <= 0) { Die(d); return; }
            if (d.explosion) BlastLimb(d);
            if (Def.tuckBelow > 0 && tuckCd <= 0 && Hp < MaxHp * Def.tuckBelow && armor > 0) Tuck();
        }

        void ChainShock(DamageInfo d)
        {
            int n = 0;
            foreach (var o in GameManager.Enemies.ToArray())
            {
                if (o == this || o.Dead || o.Wet < .3f || Vector3.Distance(o.transform.position, transform.position) > 5) continue;
                Fx.Tracer(bounds.center, o.bounds.center, new Color(.6f, .9f, 1f), .06f, .12f);
                var c = d; c.chained = true; c.amount *= .5f; c.knockback = 0; c.pull = 0; c.point = o.bounds.center;
                o.TakeHit(c, null);
                if (++n >= 2) break;
            }
        }

        Color GoreColor => Def.behaviour switch
        {
            EnemyBehaviour.Octopus => new Color(.15f, .05f, .2f),
            EnemyBehaviour.Jelly => new Color(1f, .7f, .9f),
            EnemyBehaviour.Urchin => new Color(.5f, .2f, .55f),
            EnemyBehaviour.Walker when Def.id is "crab" or "hermit" or "lobster" => new Color(.4f, .7f, .6f),   // crustaceans bleed bluish-green
            _ => new Color(.6f, .05f, .05f),
        };

        void Sever(Limb limb, DamageInfo d)
        {
            limb.Severed = true;
            var t = limb.transform;
            var joint = t.position;
            t.SetParent(null, true);
            foreach (var c in t.GetComponentsInChildren<Collider>()) { c.isTrigger = false; }
            var rb = t.gameObject.AddComponent<Rigidbody>();
            rb.mass = .4f;
            var away = d.dir.sqrMagnitude > .01f ? d.dir : (t.position - transform.position).normalized;
            rb.linearVelocity = away * 5.5f + Vector3.up * 4.5f + Random.insideUnitSphere;
            rb.angularVelocity = Random.insideUnitSphere * 14;
            Destroy(limb);
            Destroy(t.gameObject, 8);
            Fx.Gore(joint, GoreColor, 18);
            Fx.Burst(joint, away + Vector3.up, GoreColor, 8, 6);
            stumps.Add((transform, transform.InverseTransformPoint(joint), Time.time + 3.5f));   // the stump keeps pumping for a while
            stunT = Mathf.Max(stunT, Def.behaviour == EnemyBehaviour.Boss ? .1f : .3f);   // losing a limb stops anything for a moment
            windupT = 0; windupTarget = null;
            d.source?.Shake(.06f);
            if (Def.behaviour == EnemyBehaviour.Octopus) Fx.Ink(joint, 6);
            Sfx.Play("sever");
            d.source?.OnSever(this, limb.Def);
            switch (limb.Def.effect)
            {
                case LimbEffect.Kill:
                    if (Hp > 0 && Random.value < gm.DB.headlessChance && Def.behaviour != EnemyBehaviour.Boss) GoHeadless(d);
                    else Die(d);
                    break;
                case LimbEffect.WeakenAttack: attackMult *= .5f; break;
                case LimbEffect.Slow:
                    speedMult *= Def.behaviour == EnemyBehaviour.Octopus ? .88f : .6f;
                    if (limb.Kind == "Leg") { legsLost++; climbs = false; }
                    break;
                case LimbEffect.Ground: grounded = true; speedMult *= .4f; break;
                case LimbEffect.NoTail: hasTail = false; break;
                case LimbEffect.ShellOff: armor = 0; speedMult *= 1.6f; break;
                case LimbEffect.WeakenSting: stingMult *= .75f; break;
            }
        }

        public void Die(DamageInfo d, bool corpse = true)
        {
            if (Dead || Proxy) return;
            Dead = true;
            Release();
            GameManager.Enemies.Remove(this);
            gm.Waves.OnEnemyDied(this);
            if (ride != null) ride = null;
            // Sand dollars wash out of it onto the sand; XP for the killer (and a share for the team)
            SandDollarPickup.Drop(bounds.center, Mathf.RoundToInt(Def.bounty * gm.Cards.SandDollars), d.source);
            if (Def.pearls > 0) SandDollarPickup.Drop(bounds.center, Def.pearls, d.source, pearl: true);
            if (stolen != null) { stolen.transform.SetParent(null, true); stolen.Thief = null; stolen.Throw(stolen.transform.position, Vector3.down); stolen = null; }
            ShowBoard(false, launch: true);
            gm.Players.AwardKill(d.source, Def.Xp);
            d.source?.OnKill(this);
            windupT = 0; windupTarget = null;
            Fx.Gore(bounds.center, GoreColor, 16);
            if (d.source != null && Radius > 1) d.source.Shake(Mathf.Clamp(Radius * .08f, .05f, .3f));
            if (Def.behaviour == EnemyBehaviour.Puffer && corpse)
            {
                // A popped puffer still bursts, smaller, hurting its neighbours.
                Combat.Explode(transform.position + Vector3.up * .5f, 2.4f, Def.damage * .6f, Element.None, d.source, hurtsPlayers: true, exclude: this);
                corpse = false;
            }
            if (!corpse) { Destroy(gameObject); return; }
            // Become a corpse: tip over where it fell and keep dripping.
            int intact = 0;
            foreach (var l in Limbs) if (l != null && !l.Severed) intact++;
            float frac = Limbs.Count == 0 ? 1 : .5f + .5f * intact / Def.limbs.Count;
            var c = gameObject.AddComponent<Corpse>();
            c.Init(Def, Mathf.Max(1, Mathf.RoundToInt(Def.corpseValue * frac * gm.Cards.CorpseValue)), Wet);
            // It goes over the way the blow sent it (the corpse pose is where it ends up, on its back or its side)
            bool onBack = Def.behaviour is EnemyBehaviour.Walker or EnemyBehaviour.Croc;
            bool falls = Def.behaviour == EnemyBehaviour.Flyer || Def.behaviour == EnemyBehaviour.Jelly;
            var thrown = d.dir.sqrMagnitude > .01f ? d.dir * (d.explosion ? 6 : d.melee ? 4.5f : 3) / Mathf.Max(1, Radius) : Vector3.zero;
            thrown.y = 0;
            DeathFlop.Begin(this, c, Quaternion.Euler(0, 0, onBack ? 180 : 85), thrown, falls);
            foreach (var r in Model.Renderers) if (r) r.SetPropertyBlock(null);
            foreach (var l in Limbs) if (l != null) Destroy(l);
            enabled = false;
        }
    }
}

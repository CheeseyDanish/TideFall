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
            if (!d.ignoreArmor) dmg = Mathf.Max(1, dmg - armor);
            if (d.element == Element.Fire)
            {
                if (Wet > .3f) { Wet = 0; Fx.Smoke(bounds.center, 8, .3f, new Color(.95f, .95f, 1f, .6f)); }   // steam: dries, no burn
                else { burnT = gm.DB.burnSeconds; burnDps = gm.DB.burnDps * (d.source != null ? 1 : 1.5f); }
            }
            Hp -= dmg;
            flashT = .08f;
            flinch = .18f;
            if (d.source != null) Fx.DamageNumber(d.point == Vector3.zero ? bounds.center + Vector3.up * bounds.extents.y : d.point, dmg,
                                                   limb != null && limb.Def.critMultiplier > 1, d.source);
            Fx.Gore(d.point == Vector3.zero ? bounds.center : d.point, GoreColor, 4);
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
            if (Hp <= 0) Die(d);
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
            rb.linearVelocity = away * 4 + Vector3.up * 3.5f + Random.insideUnitSphere;
            rb.angularVelocity = Random.insideUnitSphere * 12;
            Destroy(limb);
            Destroy(t.gameObject, 8);
            Fx.Gore(joint, GoreColor, 18);
            if (Def.behaviour == EnemyBehaviour.Octopus) Fx.Ink(joint, 6);
            Sfx.Play("sever");
            d.source?.OnSever(this, limb.Def);
            switch (limb.Def.effect)
            {
                case LimbEffect.Kill: Die(d); break;
                case LimbEffect.WeakenAttack: attackMult *= .5f; break;
                case LimbEffect.Slow: speedMult *= Def.behaviour == EnemyBehaviour.Octopus ? .88f : .6f; break;
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
            Fx.Gore(bounds.center, GoreColor, 16);
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
            Model.root.transform.localPosition = Vector3.up * .1f;
            Model.root.transform.localRotation = Quaternion.Euler(0, 0, Def.behaviour is EnemyBehaviour.Walker or EnemyBehaviour.Croc ? 180 : 85);
            Model.root.transform.localScale = Vector3.one * Def.scale;
            LiftCorpse();
            if (Def.behaviour == EnemyBehaviour.Flyer || Def.behaviour == EnemyBehaviour.Jelly)
                transform.position = new Vector3(transform.position.x, Arena.GroundY(transform.position.z), transform.position.z);
            foreach (var r in Model.Renderers) if (r) r.SetPropertyBlock(null);
            foreach (var l in Limbs) if (l != null) Destroy(l);
            enabled = false;
        }
    }
}

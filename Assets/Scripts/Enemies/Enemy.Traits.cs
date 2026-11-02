// TideWall 3D — Enemy: what happens after a limb comes off, and what makes each kind more than something that walks at
// the wall. Everything here is switched on by numbers in its EnemyDef: Crablings swarm, Drowners climb the wall, Hermit
// Crabs tuck into their shells, Urchins fire spines, Shellbacks shield their neighbours, Clawbacks guard with their claws.
// (One part of the Enemy class; Enemy.cs has the overview, the fields and the frame loop.)
using UnityEngine;

namespace TideWall
{
    public partial class Enemy
    {
        void UpdateTraits(float dt)
        {
            if (tuckT > 0) tuckT -= dt;
            if (tuckCd > 0) tuckCd -= dt;
            UpdateStumps();
            if (headless && (bleedT -= dt) <= 0) { Die(new DamageInfo { source = headlessKiller }); return; }
            if (Def.swarmBonus > 0 && (swarmT -= dt) <= 0) { swarmT = .5f; swarmMult = CountKin(4) >= 3 ? 1 + Def.swarmBonus : 1; }
            if (Def.spineRadius > 0 && stunT <= 0 && !headless && ride == null) Spines(dt);
        }

        int CountKin(float radius)
        {
            int n = 0;
            foreach (var o in GameManager.Enemies)
                if (o != this && o.Def == Def && !o.Dead && FlatDist(o.transform.position) < radius) n++;
            return n;
        }

        // ------------------------------------------------------------------ losing limbs
        /// <summary>Its head or eyes are gone, but nobody's told the rest of it yet: it stumbles about blind, flailing at
        /// nothing, and bleeds out in a few seconds. Whoever took its head gets the kill.</summary>
        void GoHeadless(DamageInfo d)
        {
            headless = true;
            headlessKiller = d.source;
            bleedT = Random.Range(gm.DB.headlessSeconds.x, gm.DB.headlessSeconds.y);
            Hp = Mathf.Min(Hp, MaxHp * .3f);
            windupT = 0; windupTarget = null; climbs = false;
            stumbleT = 0;
            d.source?.Toast($"{Def.displayName}: still going!", new Color(1, .85f, .5f));
        }

        void Stumble(float dt)
        {
            if ((stumbleT -= dt) <= 0)
            {
                stumbleT = Random.Range(.5f, 1.3f);
                stumbleDir = Quaternion.Euler(0, Random.Range(0, 360f), 0) * Vector3.forward;
            }
            Face(stumbleDir, dt);
            transform.position += stumbleDir * Speed * .55f * dt;
        }

        void UpdateStumps()
        {
            if (stumps.Count == 0 || (stumpT -= Time.deltaTime) > 0) return;
            stumpT = .3f;
            for (int i = stumps.Count - 1; i >= 0; i--)
            {
                var s = stumps[i];
                if (Time.time > s.until) { stumps.RemoveAt(i); continue; }
                var at = transform.TransformPoint(s.local);
                Fx.Burst(at, (at - bounds.center).normalized + Vector3.up, GoreColor, 3, 2.5f);
            }
        }

        /// <summary>An explosion that doesn't kill it takes something with it (never the head: that's a kill of its own).</summary>
        void BlastLimb(DamageInfo d)
        {
            if (Random.value >= gm.DB.blastLimbChance) return;
            int n = 0;
            Limb pick = null;
            foreach (var l in Limbs)
                if (l != null && !l.Severed && l.Def.effect != LimbEffect.Kill && Random.Range(0, ++n) == 0) pick = l;
            if (pick != null) Sever(pick, d);
        }

        // ------------------------------------------------------------------ Hermit Crab: into the shell
        void Tuck()
        {
            tuckT = 2.2f; tuckCd = 7;
            windupT = 0; windupTarget = null;
            Sfx.Play("clink", .8f, .1f);
        }

        // ------------------------------------------------------------------ Clawback and Shellback
        bool BothClaws()
        {
            int claws = 0;
            foreach (var l in Limbs) if (l != null && !l.Severed && l.Kind == "Claw") claws++;
            return claws >= 2;
        }

        /// <summary>Damage multiplier from a Shellback close by: a blue line shows which one is covering this creature.</summary>
        float Guarded()
        {
            if (Def.guardRadius > 0) return 1;
            foreach (var o in GameManager.Enemies)
            {
                if (o == this || o.Dead || o.Def.guardRadius <= 0 || FlatDist(o.transform.position) > o.Def.guardRadius) continue;
                Fx.Tracer(o.bounds.center, bounds.center, new Color(.45f, .8f, 1f), .05f, .12f);
                return .6f;
            }
            return 1;
        }

        // ------------------------------------------------------------------ Urchin: a ring of spines
        void Spines(float dt)
        {
            bool bristling = spineCd < .55f;
            if (bristling && windupT <= 0) windupT = .01f;   // (shows on the model and online as a wind-up)
            if ((spineCd -= dt) > 0) return;
            spineCd = Random.Range(3.2f, 4.5f);
            windupT = 0;
            bool anyone = false;
            foreach (var p in gm.Players.All)
                if (p.IsTargetable && FlatDist(p.transform.position) < Def.spineRadius)
                {
                    p.TakeDamage(Damage * .6f, this, "urchin spines");
                    anyone = true;
                }
            for (int i = 0; i < 12; i++)
                Fx.Burst(bounds.center, Quaternion.Euler(0, i * 30, 0) * Vector3.forward, new Color(.6f, .35f, .75f), 1, Def.spineRadius * 3, false);
            Sfx.Play(anyone ? "bite" : "clink", .6f, .1f);
        }

        // ------------------------------------------------------------------ Drowner: over the wall
        bool AtWall() => transform.position.z <= Arena.WallFront + Radius + Mathf.Max(.7f, Def.attackRange);

        /// <summary>A climber waits a few seconds at the wall (beating on it), then starts up. True once it has.</summary>
        bool WantsToClimb(float dt)
        {
            if (!climbs || gm.World == null || legsLost > 0) return false;
            if ((climbWait -= dt) > 0) return false;
            climbs = false;
            climbT = 1.8f;
            climbFrom = transform.position;
            Sfx.Play("growl", .6f, .2f);
            return true;
        }

        void UpdateClimb(float dt)
        {
            climbT -= dt;
            float k = 1 - Mathf.Clamp01(climbT / 1.8f);
            var over = new Vector3(climbFrom.x, 0, Arena.WallZ - 1.6f);
            // Up the face in the first two thirds, over the top and down the far side in the rest
            float up = k < .66f ? k / .66f : 1 - (k - .66f) / .34f;
            var p = Vector3.Lerp(climbFrom, over, k < .55f ? k * .35f : Mathf.InverseLerp(.55f, 1, k));
            p.y = Mathf.SmoothStep(0, 2, up);
            transform.position = p;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.Euler(0, 180, 0), 360 * dt);
            hop = 0;
            if (climbT <= 0)
            {
                transform.position = over;
                Fx.Debris(over, new Color(.75f, .7f, .6f), 5);
            }
        }

        // ------------------------------------------------------------------ how all that looks
        void TraitPose(ref Vector3 pos, ref Vector3 euler, ref Vector3 scale)
        {
            if (headless)
            {
                // Lurching, spinning a little, arms flung about
                euler.z += Mathf.Sin(animT * 7) * 20;
                euler.x += Mathf.Sin(animT * 4.3f) * 14;
            }
            if (legsLost >= 2)
            {
                // No legs: dragging itself along on its arms
                euler.x += 72;
                pos.y -= .55f;
            }
            else if (legsLost == 1)
            {
                // One leg: hopping, listing to the side it lost
                pos.y += Mathf.Abs(Mathf.Sin(animT * 6)) * .14f - .1f;
                euler.z += 14;
            }
            if (tuckT > 0) { scale *= .82f; pos.y -= .12f; pos.x += Mathf.Sin(animT * 45) * .008f; }
            if (climbT > 0) euler.x -= 62;   // hauling itself up, belly to the stone
            if (swarmMult > 1) pos.y += Mathf.Abs(Mathf.Sin(animT * 13)) * .03f;   // whipped into a frenzy
            if (Def.spineRadius > 0 && spineCd < .55f) scale *= 1 + (1 - spineCd / .55f) * .35f;   // bristling up to fire
        }
    }
}

// TideWall 3D — Enemy: choosing targets and how each kind of creature moves and attacks.
// (One part of the Enemy class; Enemy.cs has the overview, the fields and the frame loop.)
using System.Collections.Generic;
using UnityEngine;

namespace TideWall
{
    public partial class Enemy
    {
        // ------------------------------------------------------------------ targeting & movement
        float Speed => Def.speed * speedMult * gm.Cards.EnemySpeed * (Arena.InWater(transform.position) ? Def.waterSpeedMult : 1) * (snaredT > 0 ? 1 - snareSlow : 1);

        /// <summary>A kelp snare has hold of it for a moment.</summary>
        public void Snare(float slow, float seconds) { snareSlow = slow; snaredT = Mathf.Max(snaredT, seconds); }

        Player NearestPlayer(float range, bool beachOnly)
        {
            Player best = null; float bd = range;
            foreach (var p in gm.Players.All)
            {
                if (!p.IsTargetable) continue;
                if (beachOnly && gm.World != null && !Arena.Inside(transform.position) && p.transform.position.z < Arena.WallFront - .2f) continue;   // behind the wall: out of reach (unless we're through a breach too)
                float d = FlatDist(p.transform.position);
                if (d < bd) { bd = d; best = p; }
            }
            return best;
        }

        float FlatDist(Vector3 p) { var d = p - transform.position; d.y = 0; return d.magnitude; }

        bool MoveToward(Vector3 target, float speed, float dt, float stopAt)
        {
            var d = target - transform.position; d.y = 0;
            float dist = d.magnitude;
            if (dist > .01f) Face(d, dt);
            if (dist <= stopAt) return true;
            transform.position += d / dist * Mathf.Min(speed * dt, dist - stopAt);
            return false;
        }

        void Face(Vector3 dir, float dt)
        {
            if (dir.sqrMagnitude < .0001f) return;
            var target = Quaternion.LookRotation(new Vector3(dir.x, 0, dir.z));
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, 360 * dt);
        }

        /// <summary>Chase a player on the beach if one is close, otherwise go for the wall. Attack whatever is in reach.</summary>
        void Walker(float dt, float speedScale)
        {
            var p = NearestPlayer(Def.aggroRange, true);
            if (p != null)
            {
                if (MoveToward(p.transform.position, Speed * speedScale, dt, Def.attackRange * .8f + Radius * .5f)) TryAttackPlayer(p, dt);
            }
            else if (Structure.BaitFor(transform.position) is { } bait)
            {
                // A bait bucket nearby: the fish heads win
                if (MoveToward(bait.transform.position, Speed * speedScale, dt, Radius + bait.Def.radius + .2f) && AttackReady(dt)) { Lunge(); bait.Damage(Damage); }
            }
            else if (blockedBy != null && !blockedBy.Dead)
            {
                // A barricade's in the way: chew through it
                Face(blockedBy.transform.position - transform.position, dt);
                if (AttackReady(dt)) { Lunge(); blockedBy.Damage(Damage); }
            }
            else
            {
                var goal = gm.Level.Goal(transform.position, Radius, out var kind);
                bool there = MoveToward(goal, Speed * speedScale, dt, .05f);
                if (kind == GoalKind.Plant) { if (there || gm.World.plant.InReach(transform.position, Def.attackRange)) TryAttackPlant(dt); }
                else if (kind == GoalKind.Wall && there) TryAttackWall(dt);
            }
        }

        /// <summary>Damage whatever defence is nearest: the powerplant if we're at it, otherwise the wall.</summary>
        void HitDefences(float amount)
        {
            if (gm.World == null) return;   // Part II: there's no wall or powerplant out here
            if (gm.World.plant.InReach(transform.position, Mathf.Max(2, Def.attackRange))) gm.World.plant.Damage(amount, transform.position);
            else if (!Arena.Inside(transform.position)) gm.Wall.Damage(amount, transform.position);
        }

        void TryAttackPlant(float dt)
        {
            if (!AttackReady(dt)) return;
            Lunge();
            gm.World.plant.Damage(Damage, transform.position);
        }

        bool AttackReady(float dt)
        {
            attackT -= dt;
            if (attackT > 0) return false;
            attackT = 1 / Mathf.Max(.1f, Def.attackRate);
            return true;
        }

        float Damage => Def.damage * attackMult * damageScale * gm.Cards.EnemyDamage;

        void TryAttackPlayer(Player p, float dt)
        {
            if (!AttackReady(dt)) return;
            Lunge();
            p.TakeDamage(Damage, this, Def.displayName);
        }

        void TryAttackWall(float dt)
        {
            if (!AttackReady(dt)) return;
            Lunge();
            gm.Wall.Damage(Damage, transform.position);
        }

        void Lunge() { swingT = .25f; }
        float swingT;

        void Leaper(float dt)
        {
            lungeCd -= dt;
            if (lungeT > 0)
            {
                lungeT -= dt;
                transform.position += transform.forward * Speed * 2.5f * dt;
                hop = lungeT > 0 ? Mathf.Sin(Mathf.PI * (1 - lungeT / .45f)) * .9f : 0;
                return;
            }
            Walker(dt, 1);
            if (lungeCd <= 0 && hasTail)
            {
                lungeCd = Random.Range(1.6f, 3f);
                lungeT = .45f;
            }
        }

        void Puffer(float dt)
        {
            var p = NearestPlayer(Def.aggroRange, true);
            Vector3 target = p != null ? p.transform.position : gm.Level.Goal(transform.position, Radius, out _);
            float d = FlatDist(target);
            swelling = d < 4;
            puff = Mathf.MoveTowards(puff, swelling ? 1 - d / 4 * .6f : 0, dt * 3);   // puffs up as it closes in
            bool atDefences = gm.World != null && (gm.World.plant.InReach(transform.position, 1.5f) || !Arena.Inside(transform.position) && transform.position.z <= Arena.WallFront + Radius + .2f);
            if (MoveToward(target, Speed, dt, Radius * .6f + .3f) && (p != null || (gm.World != null && d < .5f) || atDefences))
            {
                Combat.Explode(transform.position + Vector3.up * .5f, 3f, Damage, Element.None, null, hurtsPlayers: true, exclude: this);
                if (p == null) HitDefences(Damage);
                Die(default, corpse: false);
            }
        }

        void Shark(float dt)
        {
            bool inWater = Arena.InWater(transform.position);
            var p = NearestPlayer(Def.aggroRange, true);
            if (p != null && !Arena.InWater(p.transform.position) && !beached) p = null;   // sharks hunt swimmers
            if (inWater && !beached)
            {
                // Swim with the dorsal fin above the surface.
                float y = Mathf.Max(Arena.GroundY(transform.position.z), Arena.WaterLevel - .62f);
                transform.position = new Vector3(transform.position.x, Mathf.Lerp(transform.position.y, y, dt * 3), transform.position.z);
                if (p != null)
                {
                    if (MoveToward(p.transform.position, Speed * 1.1f, dt, Def.attackRange)) TryAttackPlayer(p, dt);
                }
                else
                {
                    var shore = new Vector3(transform.position.x, 0, Arena.ShoreZ - 1);
                    if (MoveToward(shore, Speed * .6f, dt, .5f)) beached = true;
                }
                if (Random.value < dt * 4) Fx.Splash(transform.position + transform.forward * .8f + Vector3.up * .6f, 1);
            }
            else
            {
                // Beached: a shark on the sand flops toward the wall, slowly and furiously.
                beached = true;
                flop = true;
                transform.position = new Vector3(transform.position.x, Arena.GroundY(transform.position.z), transform.position.z);
                Walker(dt, .25f);
            }
        }

        void Croc(float dt)
        {
            var p = NearestPlayer(Def.aggroRange, true);
            specialT -= dt;
            // Tail swipe at anyone standing behind it
            if (hasTail && specialT <= 0)
                foreach (var pl in gm.Players.All)
                {
                    if (!pl.IsTargetable) continue;
                    var to = pl.transform.position - transform.position; to.y = 0;
                    if (to.magnitude < 2.8f && Vector3.Dot(transform.forward, to.normalized) < -.3f)
                    {
                        specialT = 3;
                        pl.TakeDamage(Damage * .5f, this, "a tail swipe", to.normalized * 9);
                        Sfx.Play("swing");
                        Model.Limbs.TryGetValue("Tail", out var tail);
                        if (tail) tail.localRotation *= Quaternion.Euler(0, 60, 0);
                        break;
                    }
                }
            if (p != null)
            {
                if (MoveToward(p.transform.position, Speed, dt, Def.attackRange * .7f) && AttackReady(dt))
                {
                    // Bite and death roll: the player is held until it finishes or a teammate hurts the croc.
                    grabbed = p; grabT = 2.2f;
                    p.Grab(this, grabT);
                    Sfx.Play("bite");
                }
            }
            else Walker(dt, 1);
        }

        void Octopus(float dt)
        {
            specialT -= dt;
            int tents = 0;
            foreach (var l in Limbs) if (l.Kind == "Tent" && !l.Severed) tents++;
            var p = NearestPlayer(Def.aggroRange, false);
            // Ink: needs at least three tentacles
            if (p != null && specialT <= 0 && tents >= 3 && FlatDist(p.transform.position) < 9)
            {
                specialT = Random.Range(6f, 8f);
                p.Statuses.Add(StatusType.InkVision, 4);
                Fx.Ink(p.transform.position + Vector3.up * 1.5f);
                Fx.Ink(transform.position + Vector3.up, 8);
                Sfx.Play("splash");
            }
            if (p != null && tents >= 2 && FlatDist(p.transform.position) <= Def.attackRange && AttackReady(dt))
            {
                grabbed = p; grabT = 1.5f;     // tentacle grab: pulls the player in
                p.Grab(this, grabT);
                return;
            }
            Walker(dt, 1);
        }

        void Jelly(float dt)
        {
            // Hovers just above the sand, drifting toward the nearest target, stinging everyone close.
            var p = NearestPlayer(Def.aggroRange + 4, true);
            Vector3 target = p != null ? p.transform.position : gm.Level.Goal(transform.position, Radius, out _);
            MoveToward(target, Speed, dt, Radius * .5f);
            var pos = transform.position;
            pos.y = Mathf.Max(Arena.GroundY(pos.z), Arena.InWater(pos) ? -.6f : 0) + .4f + Mathf.Sin(animT * 2) * .15f;
            transform.position = pos;
            if (!AttackReady(dt)) return;
            bool stung = false;
            foreach (var pl in gm.Players.All)
                if (pl.IsTargetable && FlatDist(pl.transform.position) < Def.attackRange)
                {
                    float wetBonus = pl.Wet > .3f ? gm.DB.shockWetMultiplier : 1;   // stings hit harder in the water
                    pl.TakeDamage(Damage * stingMult * wetBonus, this, "a jellyfish sting");
                    stung = true;
                }
            if (gm.World != null && ((!Arena.Inside(pos) && pos.z <= Arena.WallFront + Radius + .2f) || gm.World.plant.InReach(pos, Def.attackRange))) { HitDefences(Damage * stingMult * .5f); stung = true; }
            if (stung) { Sfx.Play("zap"); Fx.Sparks(bounds.center, new Color(.7f, .9f, 1f), 8); }
        }

        /// <summary>Giant clam: creeps along shut (heavily armoured), gapes open now and then (soft, and it bites).</summary>
        void Clam(float dt)
        {
            clamT -= dt;
            bool lid = Limbs.Exists(l => l != null && l.Kind == "Lid" && !l.Severed);
            bool open = clamT < 0;
            if (clamT < -2.2f) clamT = Random.Range(2.5f, 4.5f);
            clamOpen = Mathf.MoveTowards(clamOpen, open ? 1 : 0, dt * (open ? 2.5f : 6));
            armor = lid && clamOpen < .5f ? Def.armor : 0;
            var p = NearestPlayer(Def.aggroRange, true);
            if (p != null && clamOpen > .7f && FlatDist(p.transform.position) <= Def.attackRange + Radius && AttackReady(dt))
            {
                clamT = Random.Range(2.5f, 4f); swingT = .25f;     // SNAP
                p.TakeDamage(Damage, this, "a giant clam");
                Sfx.Play("bite");
                return;
            }
            if (clamOpen < .3f) Walker(dt, .9f);                   // only moves with its shell shut
        }

        /// <summary>Make this gull a morning thief: it goes for corpses, not people.</summary>
        public void MakeThief() { thief = true; flyHeight = 9; }

        void Thieve(float dt)
        {
            var pos = transform.position;
            if (stolen == null)
            {
                // Pick the nearest corpse nobody is carrying
                Corpse best = null; float bd = float.MaxValue;
                foreach (var c in Corpse.Live)
                {
                    if (c.Holder != null || c.Thief != null || c.gameObject == gameObject) continue;
                    float d = Vector3.Distance(c.transform.position, pos);
                    if (d < bd) { bd = d; best = c; }
                }
                if (best == null) { FlyAway(dt); return; }
                var target = best.transform.position + Vector3.up * .6f;
                var d2 = target - pos;
                Face(d2, dt);
                transform.position += d2.normalized * Mathf.Min(Speed * 1.1f * dt, d2.magnitude);
                if (d2.magnitude < .8f)
                {
                    stolen = best; best.Thief = this;
                    best.transform.SetParent(transform, true);
                    Sfx.Play("splash", .5f);
                    gm.Banner("A GULL STOLE A CORPSE!", "Shoot it before it gets away!");
                }
                return;
            }
            FlyAway(dt);
        }

        void FlyAway(float dt)
        {
            var away = new Vector3(transform.position.x, 14, Arena.ShoreZ + 70) - transform.position;
            Face(away, dt);
            transform.position += away.normalized * Speed * .8f * dt;   // slower when carrying
            if (transform.position.z > Arena.ShoreZ + 60)
            {
                if (stolen != null) Destroy(stolen.gameObject);
                GameManager.Enemies.Remove(this);
                Destroy(gameObject);
            }
        }

        void Flyer(float dt)
        {
            var p = NearestPlayer(40, false);
            // Flyers ignore the wall: no player nearby means straight for the level's target (Part I: the powerplant)
            Vector3 target = p != null ? p.transform.position + Vector3.up * 1.2f : gm.Level.FlyerTarget(transform.position);
            var pos = transform.position;
            var flat = target - pos; flat.y = 0;
            if (diveState <= 0)
            {
                // Circle in, then dive when close
                var hover = new Vector3(target.x, flyHeight + Mathf.Sin(animT * 2) * .4f, target.z);
                var d = hover - pos;
                if (d.magnitude > .1f) { transform.position += d.normalized * Mathf.Min(Speed * dt, d.magnitude); Face(d, dt); }
                if (flat.magnitude < 3 && AttackReady(dt)) diveState = 1;
            }
            else if (diveState == 1)
            {
                var d = target - pos;
                transform.position += d.normalized * Speed * 1.6f * dt;
                Face(d, dt);
                if (d.magnitude < .9f)
                {
                    if (p != null) p.TakeDamage(Damage, this, Def.displayName); else if (gm.World != null) gm.World.plant.Damage(Damage, pos);
                    diveState = 2;
                }
            }
            else
            {
                transform.position += (transform.forward * -1 + Vector3.up) .normalized * Speed * dt;
                if (transform.position.y >= flyHeight) diveState = 0;
            }
        }

        void Boss(float dt)
        {
            if (Def.id == "tidemother") { Tidemother(dt); return; }
            summonT -= dt;
            if (summonT <= 0)
            {
                summonT = 10;
                var crab = gm.DB.Enemy("crab");
                for (int i = 0; i < 2 && crab != null; i++)
                    gm.Waves.SpawnExtra(crab, transform.position + new Vector3(Random.Range(-3f, 3f), 0, 2));
                Sfx.Play("horn");
            }
            Walker(dt, 1);
            if (swingT > .2f) foreach (var pl in gm.Players.All) pl.Shake(.25f);
        }

        /// <summary>The Heart's keeper: calls her children from the stage's own creatures, and every few seconds brings
        /// her claws down in a ring that throws everyone near her back.</summary>
        void Tidemother(float dt)
        {
            summonT -= dt;
            if (summonT <= 0)
            {
                summonT = Hp < MaxHp * .5f ? 9 : 13;
                var pool = gm.Expedition != null ? gm.Expedition.Stage.enemies : null;
                for (int i = 0; i < 3; i++)
                {
                    var def = pool != null && pool.Count > 0 ? gm.DB.Enemy(pool[Random.Range(0, pool.Count)]) : gm.DB.Enemy("crab");
                    if (def != null && def.behaviour != EnemyBehaviour.Boss)
                        gm.Waves.SpawnExtra(def, transform.position + Quaternion.Euler(0, i * 120 + Random.Range(-30, 30), 0) * Vector3.forward * 5);
                }
                Sfx.Play("horn");
            }
            specialT -= dt;
            if (specialT <= 0 && stunT <= 0)
            {
                specialT = Hp < MaxHp * .5f ? 5 : 7;
                Fx.Splash(transform.position + Vector3.up * .3f, 30);
                Sfx.Play("splash", 1, .05f);
                foreach (var pl in gm.Players.All)
                {
                    if (!pl.IsTargetable) continue;
                    var to = pl.transform.position - transform.position; to.y = 0;
                    if (to.magnitude > 7) { pl.Shake(.3f); continue; }
                    pl.TakeDamage(Damage * .8f, this, Def.displayName);
                    pl.Push(to.normalized * 4);
                    pl.Shake(.7f);
                }
            }
            Walker(dt, 1);
        }

        void UpdateGrab(float dt)
        {
            grabT -= dt;
            if (grabbed == null || !grabbed.IsTargetable || grabT <= 0 || stunT > 0) { Release(); return; }
            var to = transform.position - grabbed.transform.position; to.y = 0;
            if (Def.behaviour == EnemyBehaviour.Octopus && to.magnitude > Radius + .8f)
                grabbed.Push(to.normalized * 2.5f * dt);
            grabbed.TakeDamage(Damage * .5f * dt, this, Def.displayName, quiet: true);
            rollSpin = Def.behaviour == EnemyBehaviour.Croc ? animT * 720 : 0;   // death roll
        }

        /// <summary>Climb aboard a breaker (null = it broke: drop into the surf and get walking).</summary>
        public void Ride(Breaker b)
        {
            ride = b;
            if (b != null) { Wet = 1; ShowBoard(true); return; }
            ShowBoard(false);
            var p = transform.position;
            if (Def.behaviour != EnemyBehaviour.Shark && Def.behaviour != EnemyBehaviour.Jelly) p.y = Arena.GroundY(p.z);
            transform.position = p;
            lastPos = p;
            knock += Vector3.back * 3;   // the wave throws it forward
        }

        /// <summary>Wiped out before reaching the shore: off the board, cartwheeling into the surf.</summary>
        public void WipeOut()
        {
            if (ride == null) return;
            ride = null;
            tumble = 1.2f;
            ShowBoard(false, launch: true);
            Fx.Splash(transform.position + Vector3.up * .5f, 10);
            Sfx.Play("splash", .7f, .1f);
        }

        /// <summary>The surfboard under its feet while it rides a wave in (flips away when it gets off).</summary>
        public void ShowBoard(bool on, bool launch = false)
        {
            if (on && board == null)
            {
                var mi = ModelFactory.Spawn("Surfboard", transform, Vector3.zero, Mathf.Clamp(Radius * 1.2f, .7f, 2.2f));
                mi.root.transform.localRotation = Quaternion.Euler(0, Random.value < .5f ? 0 : 90, 0);
                foreach (var r in mi.Renderers) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                board = mi.root;
            }
            else if (!on && board != null)
            {
                board.transform.SetParent(null, true);
                var rb = board.AddComponent<Rigidbody>();
                rb.mass = .3f;
                rb.linearVelocity = (launch ? Vector3.up * 7 : Vector3.up * 3) + Random.insideUnitSphere * 2;
                rb.angularVelocity = Random.insideUnitSphere * 10;
                board.AddComponent<BoxCollider>();
                Destroy(board, 2.5f);
                board = null;
            }
        }

        /// <summary>The grabbed player mashes buttons: each press shortens the hold.</summary>
        public void Struggle() { grabT -= .3f; Fx.Splash(transform.position + Vector3.up * .5f, 2); }

        void Release()
        {
            if (grabbed != null) grabbed.Release(this);
            grabbed = null;
            rollSpin = 0;
        }

        void Separate()
        {
            if (Def.behaviour == EnemyBehaviour.Flyer && !grounded) return;
            var pos = transform.position;
            foreach (var o in GameManager.Enemies)
            {
                if (o == this || o.Dead) continue;
                var d = pos - o.transform.position; d.y = 0;
                float min = (Radius + o.Radius) * .8f, m = d.magnitude;
                if (m < min && m > .001f) pos += d / m * (min - m) * .5f;
            }
            transform.position = pos;
        }

        void ClampToArena()
        {
            var p = transform.position;
            bool flying = Def.behaviour == EnemyBehaviour.Flyer && !grounded;
            if (!flying)
            {
                // Nothing on foot gets past the wall, except through a breach (Part I)
                bool inside = gm.World != null && Arena.Inside(p);
                if (gm.World != null && !inside && !gm.Wall.IsOpen(p.x, Radius)) p.z = Mathf.Max(p.z, Arena.WallFront + Radius * .9f);
                if (inside)
                {
                    p.z = Mathf.Max(p.z, Arena.TownMinZ);
                    var fromPlant = p - gm.World.plant.transform.position; fromPlant.y = 0;   // and it can't walk into the reactor
                    float minD = Arena.PlantRadius + Radius * .4f;
                    if (fromPlant.magnitude < minD && fromPlant.sqrMagnitude > .0001f) p = gm.World.plant.transform.position + fromPlant.normalized * minD + Vector3.up * p.y;
                }
                // Barricades: can't walk through them (Walker chews on whatever blocked it)
                blockedBy = null;
                foreach (var st in Structure.All)
                {
                    if (st == null || st.Dead || st.Def.kind != StructureKind.Barricade) continue;
                    var d = p - st.transform.position; d.y = 0;
                    float minD = st.Def.radius + Radius * .6f;
                    if (d.magnitude < minD && d.sqrMagnitude > .0001f) { p = st.transform.position + d.normalized * minD + Vector3.up * p.y; blockedBy = st; }
                }
                if (Def.behaviour != EnemyBehaviour.Jelly && !(Def.behaviour == EnemyBehaviour.Shark && !beached) && lungeT <= 0 && tumble <= 0)
                    p.y = Arena.GroundY(p.z);
                if (grounded) p.y = Mathf.MoveTowards(transform.position.y, Arena.GroundY(p.z), 9 * Time.deltaTime);
            }
            p.x = Mathf.Clamp(p.x, -60, 60);
            transform.position = p;
        }
    }
}

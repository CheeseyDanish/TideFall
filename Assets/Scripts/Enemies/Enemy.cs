// TideWall 3D — a sea creature. One class runs every EnemyBehaviour (see the switch in Update).
// Limbs (model parts named Limb_*) take their own damage and fly off when severed, with effects from LimbDef.
// Wetness: 1 in the sea, drains on land; drips are emitted in proportion to it ("more drops = wetter").
// Online clients don't simulate enemies: they spawn proxies (Enemy.SpawnProxy) that follow the host's snapshots,
// lose the same limbs and become the same corpses (ApplyNet).
using System.Collections.Generic;
using UnityEngine;

namespace TideWall
{
    public class Limb : MonoBehaviour
    {
        public Enemy Owner;
        public LimbDef Def;
        public float Hp;
        public bool Severed;
        public Quaternion BaseRotation;
        public string Kind;   // Leg / Arm / Wing / Tent / Claw / Tail / Head / Eyes / Fin / Shell
        public int Side;      // -1 left, +1 right, 0 centre
        public float Phase;   // animation offset so tentacles don't move in lockstep
    }

    public class Enemy : MonoBehaviour
    {
        public EnemyDef Def { get; private set; }
        public float Hp { get; private set; }
        public float MaxHp { get; private set; }
        public float Wet { get; private set; } = 1;
        public int Wave { get; private set; } = -1;
        public bool Dead { get; private set; }
        public float Radius { get; private set; } = .5f;
        public ModelInstance Model { get; private set; }
        public readonly List<Limb> Limbs = new();
        public float BurnSeconds => burnT;
        /// <summary>Client mirror of a host enemy: no AI, no damage, just looks.</summary>
        public bool Proxy { get; private set; }
        public float Swing => swingT;
        public bool Grounded => grounded;
        public bool Flashing => flashT > 0;

        GameManager gm;
        float damageScale, armor, speedMult = 1, attackMult = 1, stingMult = 1;
        bool grounded, hasTail = true, beached, swelling;
        float attackT, burnT, burnDps, stunT, flashT, dripAcc, boundsT, lungeT, lungeCd, specialT, summonT, animT, flyHeight, diveState;
        Vector3 knock, lastPos;
        Bounds bounds;
        Player grabbed;
        float grabT;
        MaterialPropertyBlock mpb;

        // ------------------------------------------------------------------ spawning
        public static Enemy Spawn(EnemyDef def, Vector3 pos, int day, int wave)
        {
            var go = new GameObject(def.displayName);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, 180, 0));   // face the shore (-Z)
            var e = go.AddComponent<Enemy>();
            e.Init(def, day, wave);
            NetId.Tag(go, NetKind.Enemy, GameManager.I.DB.enemies.IndexOf(def));
            return e;
        }

        /// <summary>Client: a mirror of the host's enemy with this network id.</summary>
        public static Enemy SpawnProxy(EnemyDef def, int id, Vector3 pos, float yaw)
        {
            var go = new GameObject(def.displayName);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            var e = go.AddComponent<Enemy>();
            e.Proxy = true;
            e.Init(def, 1, -1);
            e.netPos = pos; e.netRot = go.transform.rotation;
            NetId.Tag(go, NetKind.Enemy, GameManager.I.DB.enemies.IndexOf(def), id);
            return e;
        }

        void Init(EnemyDef def, int day, int wave)
        {
            gm = GameManager.I;
            Def = def; Wave = wave;
            MaxHp = Hp = def.hp * gm.DB.HpScale(day);
            damageScale = gm.DB.DamageScale(day);
            armor = def.armor;
            Model = ModelFactory.Spawn(def.model, transform, Vector3.zero, def.scale, colliders: true);
            mpb = new MaterialPropertyBlock();
            bounds = ModelFactory.Bounds(gameObject);
            Radius = Mathf.Clamp(Mathf.Max(bounds.extents.x, bounds.extents.z) * .8f, .35f, 3f);
            foreach (var ld in def.limbs)
            {
                if (!Model.limbs.TryGetValue(ld.part, out var t)) { Debug.LogWarning($"{def.model} has no Limb_{ld.part}"); continue; }
                var limb = t.gameObject.AddComponent<Limb>();
                limb.Owner = this; limb.Def = ld; limb.Hp = MaxHp * ld.hpFraction; limb.BaseRotation = t.localRotation;
                limb.Kind = KindOf(ld.part);
                limb.Side = ld.part.EndsWith("L") ? -1 : ld.part.EndsWith("R") ? 1 : 0;
                limb.Phase = Random.value * 6.28f;
                Limbs.Add(limb);
            }
            if (def.behaviour == EnemyBehaviour.Flyer) { flyHeight = Random.Range(4f, 6f); Wet = 0; }
            lungeCd = Random.Range(1f, 3f);
            specialT = Random.Range(3f, 6f);
            summonT = 10;
            lastPos = transform.position;
            GameManager.Enemies.Add(this);
        }

        static string KindOf(string part)
        {
            foreach (var k in new[] { "Leg", "Arm", "Wing", "Tent", "Claw", "Tail", "Head", "Eyes", "Fin", "Shell" })
                if (part.StartsWith(k)) return k;
            return "Other";
        }

        // ------------------------------------------------------------------ per frame
        void Update()
        {
            float dt = Time.deltaTime;
            if (Dead || dt <= 0) return;
            if (Proxy) { ProxyUpdate(dt); return; }
            animT += dt;
            if ((boundsT -= dt) <= 0) { bounds = ModelFactory.Bounds(gameObject); boundsT = .4f; }
            UpdateWet(dt);
            UpdateStatuses(dt);
            if (Dead) return;

            if (Mathf.Abs(knock.x) + Mathf.Abs(knock.z) > .05f)
            {
                transform.position += knock * dt;
                knock = Vector3.Lerp(knock, Vector3.zero, dt * 6);
            }
            if (grabbed != null) UpdateGrab(dt);
            else if (stunT > 0) stunT -= dt;
            else
            {
                switch (Def.behaviour)
                {
                    case EnemyBehaviour.Flyer: if (grounded) Walker(dt, 1f); else Flyer(dt); break;
                    case EnemyBehaviour.Shark: Shark(dt); break;
                    case EnemyBehaviour.Jelly: Jelly(dt); break;
                    case EnemyBehaviour.Octopus: Octopus(dt); break;
                    case EnemyBehaviour.Croc: Croc(dt); break;
                    case EnemyBehaviour.Leaper: Leaper(dt); break;
                    case EnemyBehaviour.Puffer: Puffer(dt); break;
                    case EnemyBehaviour.Boss: Boss(dt); break;
                    default: Walker(dt, 1f); break;   // Walker, Urchin
                }
            }
            Separate();
            ClampToArena();
            Animate(dt);
            ApplyLook();
            lastPos = transform.position;
        }

        void UpdateWet(float dt)
        {
            if (Arena.InWater(transform.position) && Def.behaviour != EnemyBehaviour.Flyer) Wet = 1;
            else Wet = Mathf.Max(0, Wet - gm.DB.wetDrainPerSecond * dt);
            dripAcc += Wet * gm.DB.maxDripsPerSecond * Mathf.Clamp(Radius, .6f, 2f) * dt;
            while (dripAcc >= 1)
            {
                dripAcc -= 1;
                Fx.Drip(new Vector3(Random.Range(bounds.min.x, bounds.max.x), Mathf.Lerp(bounds.min.y, bounds.max.y, Random.Range(.15f, .7f)),
                                    Random.Range(bounds.min.z, bounds.max.z)));
            }
        }

        void UpdateStatuses(float dt)
        {
            if (flashT > 0) flashT -= dt;
            if (burnT > 0)
            {
                burnT -= dt;
                if (Wet > .3f) { burnT = 0; Fx.Smoke(bounds.center, 4); return; }   // the sea puts it out
                Hp -= burnDps * dt;
                if (Random.value < dt * 14) Fx.Fire(new Vector3(Random.Range(bounds.min.x, bounds.max.x), bounds.center.y, Random.Range(bounds.min.z, bounds.max.z)), 1, .1f);
                if (Hp <= 0) Die(default);
            }
        }

        // ------------------------------------------------------------------ targeting & movement
        float Speed => Def.speed * speedMult * (Arena.InWater(transform.position) ? Def.waterSpeedMult : 1);

        Player NearestPlayer(float range, bool beachOnly)
        {
            Player best = null; float bd = range;
            foreach (var p in gm.Players.All)
            {
                if (!p.IsTargetable) continue;
                if (beachOnly && p.transform.position.z < Arena.WallFront - .2f) continue;   // behind the wall: out of reach
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
            else
            {
                var w = Arena.WallTarget(transform.position.x, Radius);
                if (MoveToward(w, Speed * speedScale, dt, .05f)) TryAttackWall(dt);
            }
        }

        bool AttackReady(float dt)
        {
            attackT -= dt;
            if (attackT > 0) return false;
            attackT = 1 / Mathf.Max(.1f, Def.attackRate);
            return true;
        }

        float Damage => Def.damage * attackMult * damageScale;

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
                Model.root.transform.localPosition = new Vector3(0, Mathf.Sin(Mathf.PI * (1 - lungeT / .45f)) * .9f, 0);
                if (lungeT <= 0) Model.root.transform.localPosition = Vector3.zero;
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
            Vector3 target = p != null ? p.transform.position : Arena.WallTarget(transform.position.x, Radius);
            float d = FlatDist(target);
            swelling = d < 4;
            Model.root.transform.localScale = Vector3.one * Def.scale * (swelling ? 1 + .45f * (1 - d / 4) : 1);
            Model.root.transform.localPosition = new Vector3(0, Mathf.Abs(Mathf.Sin(animT * 4)) * .25f, 0);
            if (MoveToward(target, Speed, dt, Radius * .6f + .3f))
            {
                Combat.Explode(transform.position + Vector3.up * .5f, 3f, Damage, Element.None, null, hurtsPlayers: true, exclude: this);
                if (p == null) gm.Wall.Damage(Damage, transform.position);
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
                transform.position = new Vector3(transform.position.x, Arena.GroundY(transform.position.z), transform.position.z);
                Model.root.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(animT * 9) * 18);
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
                        Model.limbs.TryGetValue("Tail", out var tail);
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
            Vector3 target = p != null ? p.transform.position : Arena.WallTarget(transform.position.x, Radius);
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
            if (pos.z <= Arena.WallFront + Radius + .2f) { gm.Wall.Damage(Damage * stingMult * .5f, pos); stung = true; }
            if (stung) { Sfx.Play("zap"); Fx.Sparks(bounds.center, new Color(.7f, .9f, 1f), 8); }
        }

        void Flyer(float dt)
        {
            var p = NearestPlayer(40, false);
            Vector3 target = p != null ? p.transform.position + Vector3.up * 1.2f : Arena.WallTarget(transform.position.x, 0) + Vector3.up;
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
                    if (p != null) p.TakeDamage(Damage, this, Def.displayName); else gm.Wall.Damage(Damage, pos);
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

        void UpdateGrab(float dt)
        {
            grabT -= dt;
            if (grabbed == null || !grabbed.IsTargetable || grabT <= 0 || stunT > 0) { Release(); return; }
            var to = transform.position - grabbed.transform.position; to.y = 0;
            if (Def.behaviour == EnemyBehaviour.Octopus && to.magnitude > Radius + .8f)
                grabbed.Push(to.normalized * 2.5f * dt);
            grabbed.TakeDamage(Damage * .5f * dt, this, Def.displayName, quiet: true);
            Model.root.transform.localRotation = Quaternion.Euler(0, 0, Def.behaviour == EnemyBehaviour.Croc ? animT * 720 : 0);   // death roll
        }

        /// <summary>The grabbed player mashes buttons: each press shortens the hold.</summary>
        public void Struggle() { grabT -= .3f; Fx.Splash(transform.position + Vector3.up * .5f, 2); }

        void Release()
        {
            if (grabbed != null) grabbed.Release(this);
            grabbed = null;
            Model.root.transform.localRotation = Quaternion.identity;
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
                p.z = Mathf.Max(p.z, Arena.WallFront + Radius * .9f);   // nothing on foot gets past the wall
                if (Def.behaviour != EnemyBehaviour.Jelly && !(Def.behaviour == EnemyBehaviour.Shark && !beached) && lungeT <= 0)
                    p.y = Arena.GroundY(p.z);
                if (grounded) p.y = Mathf.MoveTowards(transform.position.y, Arena.GroundY(p.z), 9 * Time.deltaTime);
            }
            p.x = Mathf.Clamp(p.x, -40, 40);
            transform.position = p;
        }

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
            Fx.Gore(d.point == Vector3.zero ? bounds.center : d.point, GoreColor, 4);
            Sfx.Play("hit", .6f);
            d.source?.OnHitEnemy(this, dmg, limb != null && limb.Def.critMultiplier > 1);
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
                limb.Hp -= dmg;
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
            gm.AddCoins(Def.bounty, transform.position, null);
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
            c.Init(Def, Mathf.Max(1, Mathf.RoundToInt(Def.corpseValue * frac)), Wet);
            Model.root.transform.localPosition = Vector3.up * .1f;
            Model.root.transform.localRotation = Quaternion.Euler(0, 0, Def.behaviour is EnemyBehaviour.Walker or EnemyBehaviour.Croc ? 180 : 85);
            if (Def.behaviour == EnemyBehaviour.Flyer || Def.behaviour == EnemyBehaviour.Jelly)
                transform.position = new Vector3(transform.position.x, Arena.GroundY(transform.position.z), transform.position.z);
            foreach (var r in Model.renderers) if (r) r.SetPropertyBlock(null);
            foreach (var l in Limbs) if (l != null) Destroy(l);
            enabled = false;
        }

        // ------------------------------------------------------------------ look & animation
        void ApplyLook()
        {
            // Wet things look glossy; hits flash white.
            foreach (var r in Model.renderers)
            {
                if (r == null) continue;
                mpb.Clear();
                mpb.SetFloat("_Smoothness", .25f + .65f * Wet);
                if (flashT > 0) mpb.SetColor("_BaseColor", Color.white);
                if (burnT > 0) mpb.SetColor("_BaseColor", new Color(.35f, .2f, .15f));
                r.SetPropertyBlock(mpb);
            }
        }

        void Animate(float dt)
        {
            float moved = (transform.position - lastPos).magnitude / Mathf.Max(dt, .0001f);
            float gait = Mathf.Clamp01(moved / Mathf.Max(.5f, Def.speed));
            float s = animT * (4 + Def.speed * 2);
            if (swingT > 0) swingT -= dt;
            foreach (var l in Limbs)
            {
                if (l == null || l.Severed) continue;
                Quaternion r = Quaternion.identity;
                switch (l.Kind)
                {
                    case "Leg": r = Quaternion.Euler(Mathf.Sin(s + (l.Side > 0 ? Mathf.PI : 0)) * 30 * gait, 0, 0); break;
                    case "Arm": r = Quaternion.Euler(swingT > 0 ? -70 : Mathf.Sin(s + (l.Side > 0 ? 0 : Mathf.PI)) * 20 * gait, 0, 0); break;
                    case "Wing": r = Quaternion.Euler(0, 0, l.Side * (grounded ? 10 : Mathf.Sin(animT * 14) * 40)); break;
                    case "Tent": r = Quaternion.Euler(Mathf.Sin(animT * 3 + l.Phase) * 12, 0, Mathf.Cos(animT * 2.5f + l.Phase) * 12); break;
                    case "Claw": r = Quaternion.Euler(0, l.Side * (swingT > 0 ? 35 : Mathf.Sin(animT * 3) * 6), 0); break;
                    case "Tail": r = Quaternion.Euler(0, Mathf.Sin(animT * 4) * 20, 0); break;
                    case "Head": r = Quaternion.Euler(Mathf.Sin(animT * 2) * 4, 0, 0); break;
                }
                l.transform.localRotation = Quaternion.Slerp(l.transform.localRotation, l.BaseRotation * r, dt * 12);
            }
            if (Def.behaviour == EnemyBehaviour.Urchin) Model.root.transform.localRotation *= Quaternion.Euler(Speed * 50 * dt * gait, 0, 0);
        }

        // ------------------------------------------------------------------ online
        Vector3 netPos, netRootPos; Quaternion netRot = Quaternion.identity, netRootRot = Quaternion.identity; float netRootScale = 1;

        /// <summary>Host: everything a client needs to draw this enemy (or its corpse).</summary>
        public void WriteNet(ref NetEntityState s)
        {
            s.pos = transform.position; s.rot = transform.rotation;
            var r = Model.root.transform;
            s.rootPos = r.localPosition; s.rootRot = r.localRotation; s.rootScale = r.localScale.x / Mathf.Max(.0001f, Def.scale);
            var c = Dead ? GetComponent<Corpse>() : null;
            s.wet = c != null ? c.Wet : Wet;
            s.flags = (byte)((Dead ? 1 : 0) | (flashT > 0 ? 2 : 0) | (burnT > 0 ? 4 : 0) | (grounded ? 8 : 0) | (swingT > 0 ? 16 : 0));
            uint mask = 0;
            for (int i = 0; i < Limbs.Count && i < 32; i++) if (Limbs[i] == null || Limbs[i].Severed) mask |= 1u << i;
            s.severed = mask;
            s.value = c != null ? c.Value : 0;
        }

        /// <summary>Client: follow the host's state for this enemy.</summary>
        public void ApplyNet(in NetEntityState s)
        {
            netPos = s.pos; netRot = s.rot;
            netRootPos = s.rootPos; netRootRot = s.rootRot; netRootScale = s.rootScale;
            Wet = s.wet;
            flashT = (s.flags & 2) != 0 ? .08f : flashT;
            burnT = (s.flags & 4) != 0 ? 1 : 0;
            grounded = (s.flags & 8) != 0;
            if ((s.flags & 16) != 0) swingT = .25f;
            for (int i = 0; i < Limbs.Count && i < 32; i++)
                if ((s.severed & (1u << i)) != 0 && Limbs[i] != null && !Limbs[i].Severed) SeverVisual(Limbs[i]);
            if ((s.flags & 1) != 0 && !Dead) DieVisual(s.value);
            if (Dead && TryGetComponent<Corpse>(out var c)) { c.Value = s.value; c.Wet = s.wet; }
        }

        void ProxyUpdate(float dt)
        {
            animT += dt;
            float k = 1 - Mathf.Exp(-dt * 12);
            transform.position = Vector3.Distance(transform.position, netPos) > 6 ? netPos : Vector3.Lerp(transform.position, netPos, k);
            transform.rotation = Quaternion.Slerp(transform.rotation, netRot, k);
            var r = Model.root.transform;
            r.localPosition = Vector3.Lerp(r.localPosition, netRootPos, k);
            r.localRotation = Quaternion.Slerp(r.localRotation, netRootRot, k);
            r.localScale = Vector3.one * Def.scale * Mathf.Lerp(r.localScale.x / Mathf.Max(.0001f, Def.scale), netRootScale, k);
            if (flashT > 0) flashT -= dt;
            if ((boundsT -= dt) <= 0) { bounds = ModelFactory.Bounds(gameObject); boundsT = .4f; }
            dripAcc += Wet * gm.DB.maxDripsPerSecond * Mathf.Clamp(Radius, .6f, 2f) * dt;
            while (dripAcc >= 1)
            {
                dripAcc -= 1;
                Fx.Drip(new Vector3(Random.Range(bounds.min.x, bounds.max.x), Mathf.Lerp(bounds.min.y, bounds.max.y, Random.Range(.15f, .7f)), Random.Range(bounds.min.z, bounds.max.z)));
            }
            Animate(dt);
            ApplyLook();
            lastPos = transform.position;
        }

        /// <summary>Client: the host severed this limb. Same physics fly-off; the gore and sound came from the host.</summary>
        void SeverVisual(Limb limb)
        {
            limb.Severed = true;
            var t = limb.transform;
            t.SetParent(null, true);
            foreach (var c in t.GetComponentsInChildren<Collider>()) c.isTrigger = false;
            var rb = t.gameObject.AddComponent<Rigidbody>();
            rb.mass = .4f;
            rb.linearVelocity = (t.position - transform.position).normalized * 4 + Vector3.up * 3.5f + Random.insideUnitSphere;
            rb.angularVelocity = Random.insideUnitSphere * 12;
            Destroy(limb);
            Destroy(t.gameObject, 8);
        }

        /// <summary>Client: the host says it died. Turn into a corpse that follows the host (it can be carried and thrown).</summary>
        void DieVisual(int value)
        {
            Dead = true;
            GameManager.Enemies.Remove(this);
            var c = gameObject.AddComponent<Corpse>();
            c.Init(Def, value, Wet);
            foreach (var r in Model.renderers) if (r) r.SetPropertyBlock(null);
            foreach (var l in Limbs) if (l != null) Destroy(l);
            var f = gameObject.AddComponent<NetFollow>();
            f.Set(transform.position, transform.rotation);
            f.pos = netPos; f.rot = netRot;
            Model.root.transform.localPosition = netRootPos; Model.root.transform.localRotation = netRootRot;
            enabled = false;
        }

        /// <summary>Client: a corpse's pose (it moves when carried or thrown on the host).</summary>
        public void ApplyCorpsePose(in NetEntityState s)
        {
            if (TryGetComponent<NetFollow>(out var f)) f.Set(s.pos, s.rot);
            Model.root.transform.localPosition = s.rootPos; Model.root.transform.localRotation = s.rootRot;
        }

        void OnDestroy() => GameManager.Enemies.Remove(this);
    }
}

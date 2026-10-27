// TideWall 3D — a sea creature. One class runs every EnemyBehaviour (see the switch in Update).
// Limbs (model parts named Limb_*) take their own damage and fly off when severed, with effects from LimbDef.
// Wetness: 1 in the sea, drains on land; drips are emitted in proportion to it ("more drops = wetter").
// They arrive riding breakers (Breaker.cs), then head for the wall, through any breach, to the powerplant.
// Each kind has its own procedural animation (PoseRoot + limbs): crabs scuttle sideways, puffers bob and puff up…
// Online clients don't simulate enemies: they spawn proxies (Enemy.SpawnProxy) that follow the host's snapshots,
// lose the same limbs and become the same corpses (ApplyNet).
using System.Collections.Generic;
using UnityEngine;

namespace TideWall
{
    public partial class Enemy : MonoBehaviour
    {
        public EnemyDef Def { get; private set; }
        public float Hp { get; private set; }
        public float MaxHp { get; private set; }
        public float Wet { get; private set; } = 1;
        public int Wave { get; private set; } = -1;
        public bool Dead { get; private set; }
        public float Radius { get; private set; } = .5f;
        public Model Model { get; private set; }
        public readonly List<Limb> Limbs = new();
        public float BurnSeconds => burnT;
        /// <summary>Client mirror of a host enemy: no AI, no damage, just looks.</summary>
        public bool Proxy { get; private set; }
        public float Swing => swingT;
        public bool Grounded => grounded;
        public bool Flashing => flashT > 0;
        public bool Riding => ride != null;

        GameManager gm;
        float damageScale, armor, speedMult = 1, attackMult = 1, stingMult = 1;
        bool grounded, hasTail = true, beached, swelling;
        float attackT, burnT, burnDps, stunT, flashT, dripAcc, boundsT, lungeT, lungeCd, specialT, summonT, animT, flyHeight, diveState;
        Vector3 knock, lastPos;
        Bounds bounds;
        Player grabbed;
        float grabT;
        MaterialPropertyBlock mpb;
        Breaker ride;
        // Root pose ingredients, set by behaviours and combined in PoseRoot()
        float hop, puff, rollSpin, rollAngle, sideYaw;
        bool flop;
        // Clams open and shut; thieves steal corpses; defences block, slow and lure; hits make it flinch
        float clamOpen, clamT = 2, flinch, snaredT, snareSlow;
        bool thief;
        Corpse stolen;
        Structure blockedBy;
        GameObject board;
        float tumble;
        public bool Thief => thief;
        public float ClamOpen => clamOpen;

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
            if (flinch > 0) flinch -= dt;
            if (snaredT > 0) snaredT -= dt;
            if (ride != null)
            {
                // Surfing in on a breaker: standing on a board, arms out, wobbling
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.Euler(0, 180 + Mathf.Sin(animT * 1.3f) * 25, 0), 180 * dt);
                lastPos = transform.position;
                AnimateLimbs(dt, .3f);
                PoseRoot(dt, .3f);
                ApplyLook();
                return;
            }
            if (tumble > 0)
            {
                // Wiped out: cartwheeling through the surf
                tumble -= dt;
                transform.position += (Vector3.back * 3 + Vector3.down * 1.5f) * dt;
                Model.root.transform.localRotation *= Quaternion.Euler(600 * dt, 0, 300 * dt);
                if (Random.value < dt * 10) Fx.Splash(transform.position + Vector3.up * .3f, 2);
                if (tumble <= 0) Model.root.transform.localRotation = Quaternion.identity;
                ClampToArena();
                return;
            }

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
                    case EnemyBehaviour.Flyer: if (thief && !grounded) Thieve(dt); else if (grounded) Walker(dt, 1f); else Flyer(dt); break;
                    case EnemyBehaviour.Clam: Clam(dt); break;
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
            float gait = Gait(dt);
            AnimateLimbs(dt, gait);
            PoseRoot(dt, gait);
            ApplyLook();
            lastPos = transform.position;
        }

        void UpdateWet(float dt)
        {
            if ((Arena.InWater(transform.position) && Def.behaviour != EnemyBehaviour.Flyer) || ride != null || gm.Cards.AlwaysWet) Wet = 1;
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

    }
}

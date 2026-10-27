// TideWall 3D — Enemy: each creature's procedural animation and look.
// (One part of the Enemy class; Enemy.cs has the overview, the fields and the frame loop.)
using System.Collections.Generic;
using UnityEngine;

namespace TideWall
{
    public partial class Enemy
    {
        // ------------------------------------------------------------------ look & animation
        void ApplyLook()
        {
            // Wet things look glossy; hits flash white.
            foreach (var r in Model.Renderers)
            {
                if (r == null) continue;
                mpb.Clear();
                mpb.SetFloat("_Smoothness", .25f + .65f * Wet);
                if (flashT > 0) mpb.SetColor("_BaseColor", Color.white);
                if (burnT > 0) mpb.SetColor("_BaseColor", new Color(.35f, .2f, .15f));
                r.SetPropertyBlock(mpb);
            }
        }

        /// <summary>How hard it's walking (0 = standing, 1 = full speed), from how far it moved this frame.</summary>
        float Gait(float dt)
        {
            float moved = (transform.position - lastPos).magnitude / Mathf.Max(dt, .0001f);
            return Mathf.Clamp01(moved / Mathf.Max(.5f, Def.speed));
        }

        bool Crustacean => Def.id is "crab" or "hermit";

        /// <summary>Limbs: legs stride, arms reach, claws snap, wings flap, tentacles and tails sway. Each kind its own way.</summary>
        void AnimateLimbs(float dt, float gait)
        {
            float s = animT * (4 + Def.speed * 2);
            if (swingT > 0) swingT -= dt;
            foreach (var l in Limbs)
            {
                if (l == null || l.Severed) continue;
                Quaternion r = Quaternion.identity;
                switch (l.Kind)
                {
                    case "Leg": r = Quaternion.Euler(Mathf.Sin(s + (l.Side > 0 ? Mathf.PI : 0)) * 30 * gait, 0, 0); break;
                    case "Arm":
                        // Drowners shamble with their arms out in front, swaying
                        r = Quaternion.Euler(swingT > 0 ? -110 : -75 + Mathf.Sin(s * .5f + l.Side) * 12, l.Side * 6, Mathf.Sin(animT * 1.3f + l.Side) * 8); break;
                    case "Wing": r = Quaternion.Euler(0, 0, l.Side * (grounded ? 10 + Mathf.Abs(Mathf.Sin(animT * 6)) * 25 : Mathf.Sin(animT * 14) * 40)); break;
                    case "Tent":
                        r = Def.behaviour == EnemyBehaviour.Jelly
                            ? Quaternion.Euler(Mathf.Sin(animT * 3 + l.Phase) * 25, 0, Mathf.Cos(animT * 2 + l.Phase) * 10)    // trailing streamers
                            : Quaternion.Euler(Mathf.Sin(animT * 3 + l.Phase) * 16, 0, Mathf.Cos(animT * 2.5f + l.Phase) * 16); break;
                    case "Claw":
                        // Crabs snap their claws while they scuttle; everyone raises them to strike
                        float snap = Crustacean ? Mathf.Abs(Mathf.Sin(animT * 9 + l.Side)) * 22 : Mathf.Sin(animT * 3) * 6;
                        r = Quaternion.Euler(swingT > 0 ? -35 : 0, l.Side * (swingT > 0 ? 35 : snap), 0); break;
                    case "Tail":
                        float tailSpeed = Def.behaviour is EnemyBehaviour.Shark or EnemyBehaviour.Leaper ? 9 : Def.behaviour == EnemyBehaviour.Croc ? 3 : 4;
                        float tailAmp = Def.behaviour == EnemyBehaviour.Croc ? 25 : 20;
                        r = Quaternion.Euler(0, Mathf.Sin(animT * tailSpeed) * tailAmp * (.4f + .6f * Mathf.Max(gait, ride != null ? 1 : 0)), 0); break;
                    case "Head":
                        r = Def.id == "turtle"
                            ? Quaternion.Euler(Mathf.Sin(animT * 1.5f) * 10, Mathf.Sin(animT * .7f) * 20, 0)       // a slow look around
                            : Def.behaviour == EnemyBehaviour.Croc ? Quaternion.Euler(swingT > 0 ? -25 : Mathf.Sin(animT * 2) * 5, 0, 0)   // jaws up to bite
                            : Quaternion.Euler(Mathf.Sin(animT * 2) * 6, 0, Mathf.Sin(animT * 1.1f) * 10); break;   // a drowner's lolling head
                    case "Eyes": r = Quaternion.Euler(0, Mathf.Sin(animT * 4) * 12, 0); break;   // eye stalks look about
                    case "Shell": r = Quaternion.Euler(0, 0, Mathf.Sin(s * 2) * 6 * gait); break;    // the shell wobbles as the hermit scurries
                    case "Fin": r = Quaternion.Euler(0, 0, Mathf.Sin(animT * 6) * 6); break;
                    case "Lid": r = Quaternion.Euler(-clamOpen * 55 + (swingT > 0 ? 25 : 0), 0, 0); break;   // gapes open on its back hinge, snaps shut
                }
                l.transform.localRotation = Quaternion.Slerp(l.transform.localRotation, l.BaseRotation * r, dt * 12);
            }
        }

        /// <summary>The whole body: each kind moves its own way. Behaviours only set the ingredients (hop, puff, flop, roll).</summary>
        void PoseRoot(float dt, float gait)
        {
            var root = Model.root.transform;
            float s = animT * (4 + Def.speed * 2);
            Vector3 pos = new(0, hop, 0), euler = Vector3.zero, scale = Vector3.one;
            float targetSide = 0;
            switch (Def.behaviour)
            {
                case EnemyBehaviour.Walker when Crustacean:
                    // Crabs walk like crabs: sideways, on tiptoe, with a rapid jittery bob
                    targetSide = gait > .15f ? 90 : 0;
                    pos.y += Mathf.Abs(Mathf.Sin(s * 2.2f)) * .06f * gait;
                    euler.z = Mathf.Sin(s * 2.2f) * 6 * gait;
                    break;
                case EnemyBehaviour.Walker when Def.id == "lobster":
                    pos.y += Mathf.Abs(Mathf.Sin(s)) * .05f * gait;
                    euler.x = Mathf.Sin(animT * 2) * 3 - (swingT > 0 ? 8 : 0);   // rears up to strike
                    break;
                case EnemyBehaviour.Walker when Def.id == "turtle":
                    euler.z = Mathf.Sin(s * .5f) * 5 * gait;                       // a heavy side-to-side plod
                    pos.y += Mathf.Abs(Mathf.Sin(s * .5f)) * .05f * gait;
                    break;
                case EnemyBehaviour.Walker:                                        // drowners shamble
                    euler.z = Mathf.Sin(s * .5f) * 9 * gait;
                    euler.x = 10 + Mathf.Sin(s) * 3;
                    pos.y += Mathf.Abs(Mathf.Sin(s * .5f)) * .08f * gait;
                    break;
                case EnemyBehaviour.Flyer:
                    if (grounded) pos.y += Mathf.Abs(Mathf.Sin(animT * 7)) * .18f;   // grounded gulls hop
                    else { pos.y += Mathf.Sin(animT * 3) * .2f; euler.z = Mathf.Sin(animT * 1.3f) * 15; }   // glide and bank
                    break;
                case EnemyBehaviour.Puffer:
                    // Bobs along paddling, then puffs into a spiky ball that quivers and spins
                    pos.y += .2f + Mathf.Sin(animT * 3) * .15f;
                    euler.z = Mathf.Sin(animT * 2.4f) * 10 * (1 - puff);
                    euler.y = puff * animT * 140;
                    scale *= 1 + puff * .6f + Mathf.Sin(animT * 24) * .05f * puff;
                    break;
                case EnemyBehaviour.Urchin:
                    rollAngle += Speed * 50 * dt * gait;                          // rolls along on its spines
                    euler.x = rollAngle;
                    scale *= 1 + Mathf.Sin(animT * 6) * .05f;                    // spines bristle
                    break;
                case EnemyBehaviour.Jelly:
                {
                    float pulse = Mathf.Sin(animT * 3);                         // the bell pulses as it swims
                    scale = new Vector3(1 + pulse * .12f, 1 - pulse * .16f, 1 + pulse * .12f);
                    euler.z = Mathf.Sin(animT * .9f) * 8;
                    break;
                }
                case EnemyBehaviour.Shark:
                    euler.y = Mathf.Sin(animT * 4.5f) * 9;                       // swims with a sweeping S
                    if (flop) euler.z = Mathf.Sin(animT * 9) * 18;               // beached: flops furiously
                    break;
                case EnemyBehaviour.Leaper:
                    euler.y = Mathf.Sin(animT * 9) * 16;                         // eels slither
                    euler.x = hop > 0 ? -25 * hop : 0;
                    break;
                case EnemyBehaviour.Croc:
                    euler.y = Mathf.Sin(s * .8f) * 10 * gait;                    // a low belly waddle
                    euler.z = rollSpin;                                          // the death roll
                    pos.y += Mathf.Abs(Mathf.Sin(s * .8f)) * .04f * gait;
                    break;
                case EnemyBehaviour.Octopus:
                    pos.y += Mathf.Sin(animT * 2) * .12f;
                    scale *= 1 + Mathf.Sin(animT * 2) * .06f;                    // the mantle breathes
                    euler.y = Mathf.Sin(animT * .8f) * 12;
                    break;
                case EnemyBehaviour.Clam:
                    pos.y += Mathf.Abs(Mathf.Sin(animT * 3.2f)) * .22f * gait * (1 - clamOpen);   // hops along while shut
                    euler.z = Mathf.Sin(animT * 3.2f) * 6 * gait;
                    break;
                case EnemyBehaviour.Boss:
                {
                    float b = Mathf.Abs(Mathf.Sin(animT * 2.6f));               // the Glizzy Monster bounces and squashes
                    pos.y += b * .45f;
                    scale = new Vector3(1 + (1 - b) * .1f, 1 - (1 - b) * .12f, 1 + (1 - b) * .1f);
                    euler.z = Mathf.Sin(animT * 1.3f) * 6;
                    break;
                }
            }
            sideYaw = Mathf.MoveTowards(sideYaw, targetSide, 360 * dt);
            euler.y += sideYaw;
            if (ride != null)
            {
                // Surfing: up on the board, leaning and wobbling for balance
                pos.y += .12f;
                euler.z += Mathf.Sin(animT * 4.5f) * 14;
                euler.x += Mathf.Sin(animT * 2.1f) * 6 - 6;
            }
            if (flinch > 0) { euler.x -= flinch / .18f * 14; pos += Random.insideUnitSphere * .04f * flinch / .18f; }   // recoils from a hit
            root.localPosition = Vector3.Lerp(root.localPosition, pos, dt * 14);
            root.localRotation = Quaternion.Slerp(root.localRotation, Quaternion.Euler(euler), dt * 14);
            root.localScale = Vector3.Scale(Vector3.one * Def.scale, scale);
        }
    }
}

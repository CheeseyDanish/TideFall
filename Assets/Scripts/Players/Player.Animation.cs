// TideWall 3D — Player: the jointed sailor's animation: stride, aiming arm, melee swings, carrying, downed.
// (One part of the Player class; Player.cs has the overview, the fields and the frame loop.)
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace TideWall
{
    public partial class Player
    {
        // ------------------------------------------------------------------ body animation
        /// <summary>The jointed sailor: legs stride with your speed, the gun arm aims where you look, the other arm swings
        /// the melee weapon in that weapon's style, both arms go up to carry, the head follows your gaze.</summary>
        void AnimateBody(float dt)
        {
            if (legL == null) return;
            var moved = transform.position - lastVisualPos; moved.y = 0;
            lastVisualPos = transform.position;
            float speed = dt > 0 ? moved.magnitude / dt : 0;
            float gait = Mathf.Clamp01(speed / Mathf.Max(1, db.playerSpeed));
            stepPhase += dt * (4 + speed * 1.6f);
            float k = 1 - Mathf.Exp(-dt * 18);

            // Legs
            float stride = Mathf.Sin(stepPhase) * 38 * gait;
            if (IsDowned) stride = Mathf.Sin(stepPhase * .5f) * 15 * gait;   // a feeble crawl
            legL.localRotation = Quaternion.Slerp(legL.localRotation, Quaternion.Euler(stride, 0, IsDowned ? -10 : 0), k);
            legR.localRotation = Quaternion.Slerp(legR.localRotation, Quaternion.Euler(-stride, 0, IsDowned ? 10 : 0), k);

            // Body: bob with the steps, lean into a dash, breathe when still
            var bodyPose = Quaternion.Euler((IsDowned ? -80 : 0) + (dashT > 0 ? 18 : gait * 5), 0, 0);
            model.root.transform.localRotation = Quaternion.Slerp(model.root.transform.localRotation, bodyPose, dt * 10);
            model.root.transform.localPosition = new Vector3(0, IsDowned ? .25f : Mathf.Abs(Mathf.Sin(stepPhase)) * .06f * gait, 0);

            // Head follows where you look (up/down)
            if (head != null) head.localRotation = Quaternion.Slerp(head.localRotation, Quaternion.Euler(Mathf.Clamp(Pitch, -40, 40) * .6f, 0, 0), k);

            // Arms
            Quaternion right, left;
            float sp = SwingProgress;
            bool carrying = CarryCount > 0;
            if (carrying) { right = Quaternion.Euler(-165, 0, -12); left = Quaternion.Euler(-165, 0, 12); }
            else if (IsDowned) { right = Quaternion.Euler(-150 + Mathf.Sin(stepPhase * .5f) * 30, 0, 0); left = Quaternion.Euler(-150 - Mathf.Sin(stepPhase * .5f) * 30, 0, 0); }
            else
            {
                // Gun arm: held out along the aim (the gun is holstered while guns are locked)
                right = GunsLocked ? Quaternion.Euler(-Mathf.Sin(stepPhase) * 25 * gait, 0, 6) : Quaternion.Euler(-90 + Pitch * .8f - FireHeat * 12, -6, 0);
                left = sp >= 0 ? SwingArm(sp) : Quaternion.Euler(-35 + Mathf.Sin(stepPhase) * 20 * gait, 0, -6);
            }
            armR.localRotation = Quaternion.Slerp(armR.localRotation, right, sp >= 0 ? 1 : k);
            armL.localRotation = sp >= 0 ? left : Quaternion.Slerp(armL.localRotation, left, k);

            // Weapons in hand: the gun points where you aim whatever the arm does; the melee weapon extends the arm
            if (gunModel != null)
            {
                gunModel.root.SetActive(!GunsLocked && !carrying);
                gunModel.root.transform.localRotation = Quaternion.Inverse(armR.localRotation) * Quaternion.Euler(Pitch * .8f - FireHeat * 12, 0, 0);
            }
            if (meleeModel != null) meleeModel.root.transform.localRotation = Quaternion.Euler(90, 0, 0) * Quaternion.Euler(sp >= 0 ? SwingWrist(sp) : -30, 0, 0);
        }

        /// <summary>The swinging arm through wind-up → strike → recovery, shaped by the weapon's style.</summary>
        Quaternion SwingArm(float t)
        {
            float w = Melee.windup / Melee.SwingTime, st = (Melee.windup + Melee.strike) / Melee.SwingTime;
            // a: 0 (rest) → 1 (fully wound up) → strike to 2 → recover to 3
            float a = t < w ? t / w : t < st ? 1 + (t - w) / Mathf.Max(.001f, st - w) : 2 + (t - st) / Mathf.Max(.001f, 1 - st);
            switch (Melee.style)
            {
                case MeleeStyle.Slash:   // cutlass: cocked back by the shoulder, a flat backhand slash across, wrist rolling
                    return a < 1 ? Quaternion.Euler(Mathf.Lerp(-35, -95, a), Mathf.Lerp(0, 70, a), Mathf.Lerp(-6, -40, a))
                         : a < 2 ? Quaternion.Euler(-95, Mathf.Lerp(70, -80, a - 1), Mathf.Lerp(-40, 30, a - 1))
                         : Quaternion.Euler(Mathf.Lerp(-95, -35, a - 2), Mathf.Lerp(-80, 0, a - 2), Mathf.Lerp(30, -6, a - 2));
                case MeleeStyle.Sweep:   // oar: a big two-handed side-to-side sweep, low and wide
                    return a < 1 ? Quaternion.Euler(Mathf.Lerp(-35, -70, a), Mathf.Lerp(0, 95, a), 0)
                         : a < 2 ? Quaternion.Euler(-70, Mathf.Lerp(95, -95, a - 1), 0)
                         : Quaternion.Euler(Mathf.Lerp(-70, -35, a - 2), Mathf.Lerp(-95, 0, a - 2), 0);
                case MeleeStyle.Thrust:  // boat hook: draw back, jab forward, then yank back toward you
                    return a < 1 ? Quaternion.Euler(Mathf.Lerp(-35, -60, a), 0, 0)
                         : a < 2 ? Quaternion.Euler(Mathf.Lerp(-60, -95, a - 1), 0, 0)
                         : Quaternion.Euler(Mathf.Lerp(-95, -35, a - 2), 0, 0);
                case MeleeStyle.Slam:    // anchor: heaved right up over the head, then brought crashing down
                    return a < 1 ? Quaternion.Euler(Mathf.Lerp(-35, -200, Mathf.SmoothStep(0, 1, a)), 0, 0)
                         : a < 2 ? Quaternion.Euler(Mathf.Lerp(-200, -10, (a - 1) * (a - 1)), 0, 0)
                         : Quaternion.Euler(Mathf.Lerp(-10, -35, a - 2), 0, 0);
                default:                 // spade: up and over, a downward chop
                    return a < 1 ? Quaternion.Euler(Mathf.Lerp(-35, -165, a), 0, 0)
                         : a < 2 ? Quaternion.Euler(Mathf.Lerp(-165, -40, a - 1), 0, 0)
                         : Quaternion.Euler(Mathf.Lerp(-40, -35, a - 2), 0, 0);
            }
        }

        /// <summary>The wrist: weapons tilt back during the wind-up and snap through on the strike.</summary>
        float SwingWrist(float t)
        {
            float w = Melee.windup / Melee.SwingTime;
            return t < w ? Mathf.Lerp(-30, -70, t / w) : Mathf.Lerp(-70, 10, Mathf.Clamp01((t - w) / .35f));
        }
    }
}

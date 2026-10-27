// TideWall 3D — Player: looking (mouse, stick, first person or top-down) and what the aim ray points at.
// (One part of the Player class; Player.cs has the overview, the fields and the frame loop.)
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace TideWall
{
    public partial class Player
    {
        // ------------------------------------------------------------------ looking & aiming
        /// <summary>First person: mouse or right stick turns the view.</summary>
        void TickLook(float dt)
        {
            if (!FirstPerson) { if (AimDir.sqrMagnitude > .01f) Yaw = Quaternion.LookRotation(AimDir).eulerAngles.y; return; }
            var s = Settings.Current;
            Vector2 d;
            if (Controls.UsesMouse) d = Controls.Look.ReadValue<Vector2>() * .09f * s.mouseSensitivity;
            else
            {
                var st = Controls.Aim.ReadValue<Vector2>();
                float m = st.magnitude;
                st = m < .12f ? Vector2.zero : st / m * Mathf.Pow((m - .12f) / .88f, 1.6f);   // dead zone + response curve
                float slow = s.aimAssist && EnemyUnderCrosshair() ? .55f : 1;               // aim assist: slow down over targets
                d = new Vector2(st.x * 190, st.y * 120) * s.stickSensitivity * slow * dt;
            }
            if (Statuses.Has(StatusType.ReverseControls)) d = -d;
            Yaw = Mathf.Repeat(Yaw + d.x, 360);
            Pitch = Mathf.Clamp(Pitch - d.y * (s.invertY ? -1 : 1), -80, 80);
        }

        bool EnemyUnderCrosshair()
        {
            var ray = new Ray(EyePos, LookRotation * Vector3.forward);
            return Physics.Raycast(ray, out var h, 60, ~0, QueryTriggerInteraction.Collide) && h.collider.GetComponentInParent<Enemy>() is { Dead: false };
        }

        /// <summary>This machine's aim: the crosshair (first person), the cursor, or the stick's reticle (top-down).</summary>
        AimSample LocalAim()
        {
            var cam = Cam.Camera;
            var a = new AimSample { assist = !UsesMouse && Settings.Current.aimAssist };
            if (FirstPerson)
            {
                AimScreen = cam.pixelRect.center;
                a.ray = new Ray(EyePos, LookRotation * Vector3.forward);
                return a;
            }
            if (UsesMouse)
            {
                AimScreen = Controls.Point.ReadValue<Vector2>();
                a.ray = cam.ScreenPointToRay(AimScreen);
                return a;
            }
            var stick = Controls.Aim.ReadValue<Vector2>();
            if (Statuses.Has(StatusType.ReverseControls)) stick = -stick;
            float mag = Mathf.Clamp01(stick.magnitude);
            var dir = AimDir;
            if (mag > .2f) dir = new Vector3(stick.x, 0, stick.y).normalized;
            else
            {
                var mv = MoveInput();
                if (mv.magnitude > .3f) dir = new Vector3(mv.x, 0, mv.y).normalized;
            }
            float dist = 2.5f + 9 * (mag > .2f ? mag : .6f);
            AimScreen = cam.WorldToScreenPoint(transform.position + dir * dist + Vector3.up * .8f);
            a.ray = cam.ScreenPointToRay(AimScreen);
            a.dirHint = dir;
            return a;
        }

        /// <summary>Host: an online player's look and aim, as their machine sent it.</summary>
        void ApplyRemoteLook()
        {
            Yaw = Remote.Yaw; Pitch = Remote.Pitch;
            ResolveAim(Remote.Aim);
        }

        /// <summary>What's along the aim ray? That's what the shot goes for, limb and all.</summary>
        public void ResolveAim(AimSample a)
        {
            if (a.dirHint.sqrMagnitude > .01f) AimDir = a.dirHint;
            var hits = Physics.RaycastAll(a.ray, 200, ~0, QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, (x, y) => x.distance.CompareTo(y.distance));
            bool found = false;
            foreach (var h in hits)
            {
                if (h.collider.GetComponentInParent<Player>() || h.collider.GetComponentInParent<Wall>()) continue;
                var e = h.collider.GetComponentInParent<Enemy>();
                if (e != null && !e.Dead) { AimPoint = h.point; found = true; break; }
                if (h.collider.GetComponentInParent<GasCan>() is { Holder: null }) { AimPoint = h.point; found = true; break; }
                if (h.collider.isTrigger || h.collider.GetComponentInParent<Corpse>()) continue;
                AimPoint = h.point + (FirstPerson ? Vector3.zero : Vector3.up * .5f); found = true; break;
            }
            if (!found) AimPoint = a.ray.GetPoint(FirstPerson ? 60 : 30);
            if (a.assist) { if (FirstPerson) AimAssistFirstPerson(a.ray); else AimAssistTopDown(); }
            if (FirstPerson) AimDir = Quaternion.Euler(0, Yaw, 0) * Vector3.forward;
            else if (a.dirHint.sqrMagnitude < .01f)
            {
                var d = AimPoint - transform.position; d.y = 0;
                if (d.sqrMagnitude > .04f) AimDir = d.normalized;
            }
            UpdateReticle(true);
        }

        /// <summary>Top-down gamepads have no cursor: if the stick points near an enemy, aim at its body.</summary>
        void AimAssistTopDown()
        {
            foreach (var e in GameManager.Enemies)
                if (e != null && !e.Dead && e.GetComponentInChildren<Collider>() is { } c && c.bounds.Contains(AimPoint)) return;
            Enemy best = null; float bestAngle = 9;
            foreach (var e in GameManager.Enemies)
            {
                if (e == null || e.Dead) continue;
                var to = e.transform.position - transform.position; to.y = 0;
                if (to.magnitude > 16) continue;
                float ang = Vector3.Angle(AimDir, to);
                if (ang < bestAngle) { bestAngle = ang; best = e; }
            }
            if (best != null) AimPoint = ModelFactory.Bounds(best.gameObject).center;
        }

        /// <summary>First-person gamepads: a gentle pull onto a body just next to the crosshair.</summary>
        void AimAssistFirstPerson(Ray ray)
        {
            foreach (var e in GameManager.Enemies)
                if (e != null && !e.Dead && Vector3.Distance(ModelFactory.Bounds(e.gameObject).ClosestPoint(AimPoint), AimPoint) < .05f) return;
            Enemy best = null; float bestAngle = 3.5f;
            foreach (var e in GameManager.Enemies)
            {
                if (e == null || e.Dead) continue;
                var c = ModelFactory.Bounds(e.gameObject).center - ray.origin;
                if (c.magnitude > 45) continue;
                float ang = Vector3.Angle(ray.direction, c);
                if (ang < bestAngle) { bestAngle = ang; best = e; }
            }
            if (best != null) AimPoint = ModelFactory.Bounds(best.gameObject).center;
        }

        void UpdateReticle(bool show)
        {
            reticle.enabled = show && !FirstPerson && !IsOut;
            if (!reticle.enabled) return;
            float r = .3f + Mathf.Sin(Time.time * 6) * .03f;
            for (int i = 0; i < 20; i++)
            {
                float a = i / 20f * Mathf.PI * 2;
                reticle.SetPosition(i, AimPoint + new Vector3(Mathf.Cos(a) * r, .02f, Mathf.Sin(a) * r));
            }
        }
    }
}

// TideWall 3D — Player: looking (mouse or stick, first or third person) and what the aim ray points at.
// (One part of the Player class; Player.cs has the overview, the fields and the frame loop.)
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace TideWall
{
    public partial class Player
    {
        // ------------------------------------------------------------------ looking & aiming
        /// <summary>Mouse or right stick turns the view (first and third person alike).</summary>
        void TickLook(float dt)
        {
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

        /// <summary>This machine's aim: the centre crosshair. In third person the ray leaves the camera behind your shoulder
        /// and starts level with you, so nothing between the camera and your sailor gets aimed at.</summary>
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
            AimScreen = cam.pixelRect.center;
            var origin = Cam.transform.position;
            var dir = LookRotation * Vector3.forward;
            float skip = Vector3.Dot(EyePos - origin, dir);
            a.ray = new Ray(skip > 0 ? origin + dir * skip : origin, dir);
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
                AimPoint = h.point; found = true; break;
            }
            if (!found) AimPoint = a.ray.GetPoint(60);
            if (a.assist) AimAssist(a.ray);
            AimDir = Quaternion.Euler(0, Yaw, 0) * Vector3.forward;
            UpdateReticle(true);
        }

        /// <summary>Gamepads: a gentle pull onto a body just next to the crosshair.</summary>
        void AimAssist(Ray ray)
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
            reticle.enabled = false;   // the old top-down ground ring: both views use the centre crosshair now
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

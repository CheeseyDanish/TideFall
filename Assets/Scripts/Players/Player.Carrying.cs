// TideWall 3D — Player: interacting with the nearest thing, picking up, carrying and throwing.
// (One part of the Player class; Player.cs has the overview, the fields and the frame loop.)
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace TideWall
{
    public partial class Player
    {
        // ------------------------------------------------------------------ interaction & carrying
        void UpdateInteract(float dt)
        {
            Focus = LookedAt() ?? Interactable.Nearest(this);
            var downedMate = DownedTeammateNearby();
            if (downedMate != null && Held(Btn.Interact))
            {
                // Shared Snacks (split screen): screen-mates revive each other twice as fast
                downedMate.ReviveTick(dt * (gm.Cards.Has("snacks") && downedMate.Owner == Owner ? 2 : 1));
                return;
            }
            if (!Pressed(Btn.Interact)) return;
            if (Focus != null) Focus.Interact(this);
            else if (Carrying.Count > 0) Drop(Carrying[^1]);
        }

        /// <summary>In first person, the thing under your crosshair (within reach) wins: much easier to grab the right corpse.</summary>
        Interactable LookedAt()
        {
            if (!FirstPerson) return null;
            var ray = new Ray(EyePos, LookRotation * Vector3.forward);
            var hits = Physics.RaycastAll(ray, 4.5f, ~0, QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in hits)
            {
                if (h.collider.GetComponentInParent<Player>() == this) continue;
                var it = h.collider.GetComponentInParent<Interactable>();
                if (it != null && it.isActiveAndEnabled && it.Prompt(this) != null) return it;
                if (!h.collider.isTrigger) break;   // a wall or the ground is in the way
            }
            return null;
        }

        public Player DownedTeammateNearby()
        {
            foreach (var p in gm.Players.All)
                if (p != this && p.IsDowned && Vector3.Distance(p.transform.position, transform.position) < 1.9f) return p;
            return null;
        }

        /// <summary>What Interact does here, with "{btn}" where the button glyph goes (each machine fills in its own).</summary>
        public string PromptText()
        {
            if (!Simulated) return NetPrompt ?? "";
            if (IsDowned || IsOut || OpenedMenu != null || gm.State != GameState.Playing) return "";
            var mate = DownedTeammateNearby();
            if (mate != null) return $"Hold {{btn}} to revive {mate.Name} ({mate.ReviveProgress * 100:0}%)";
            if (Focus != null) return "{btn} " + Focus.Prompt(this);
            if (Carrying.Count > 0) return "{btn} Drop";
            var ph = gm.Phases.Current;
            if (ph != null && ph.endWhenAllReady) return $"Ring the bell when you're ready · {gm.Players.ReadyCount}/{gm.Players.Count} ready";
            return "";
        }

        public void PickUp(Carryable c)
        {
            if (CarryWeight + c.weight > MaxCarry) return;
            Carrying.Add(c);
            c.Holder = this;
            foreach (var th in c.GetComponents<Thrown>()) Destroy(th);
            c.transform.SetParent(visual, true);
            SetLayer(c.transform, BodyLayer + Index);   // out of your own first-person view
            LayoutCarried();
            Sfx.Play("ui");
        }

        void LayoutCarried()
        {
            float y = 2.0f;
            foreach (var c in Carrying)
            {
                if (!c) continue;
                c.transform.localPosition = new Vector3(0, y, .1f);
                c.transform.localRotation = Quaternion.identity;
                y += .35f + c.weight * .25f;
            }
        }

        public void Drop(Carryable c)
        {
            if (!Carrying.Remove(c)) return;
            c.Holder = null;
            c.transform.SetParent(null, true);
            SetLayer(c.transform, 0);
            var p = transform.position + AimDir * .9f;
            c.transform.position = new Vector3(p.x, Arena.GroundY(p.z), p.z);
            LayoutCarried();
        }

        void ThrowTop()
        {
            var c = Carrying[^1];
            Carrying.RemoveAt(Carrying.Count - 1);
            SetLayer(c.transform, 0);
            var flat = AimPoint - transform.position; flat.y = 0;
            float dist = Mathf.Clamp(flat.magnitude, 2, 12);
            const float T = .7f;
            var vel = AimDir * (dist / T) + Vector3.up * (-Physics.gravity.y * T / 2);
            c.Throw(transform.position + Vector3.up * 2f, vel);
            LayoutCarried();
            Sfx.Play("swing");
        }
    }
}

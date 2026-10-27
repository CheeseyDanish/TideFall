// TideWall 3D — Player: damage, being downed, bleeding out, reviving and respawning, and eating.
// (One part of the Player class; Player.cs has the overview, the fields and the frame loop.)
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace TideWall
{
    public partial class Player
    {
        // ------------------------------------------------------------------ health
        public void TakeDamage(float amount, Enemy source, string what, Vector3? knock = null, bool quiet = false)
        {
            if (!IsTargetable || amount <= 0) return;
            if (Statuses.Has(StatusType.Invincible)) return;
            if (Statuses.Has(StatusType.Giant)) amount *= .8f;
            Hp -= amount;
            if (knock.HasValue) Push(knock.Value * .15f);
            if (!quiet) { hurtFlash = .25f; Sfx.Play("hurt", .7f, .1f); Shake(.15f); }
            if (Hp <= 0) GoDown(what);
        }

        public void Heal(float amount) { if (!IsDowned && !IsOut) Hp = Mathf.Min(MaxHp, Hp + amount); }

        void GoDown(string what)
        {
            Hp = 0; IsDowned = true; BleedLeft = db.bleedOutSeconds; ReviveProgress = 0;
            for (int i = Carrying.Count - 1; i >= 0; i--) Drop(Carrying[i]);
            if (grabbedBy != null) Release(grabbedBy);
            if (OpenedMenu != null && !OpenedMenu.System) CloseMenu();
            gm.Banner($"{Name.ToUpperInvariant()} IS DOWN", what != null ? $"Taken out by {what}. Hold Interact next to them to revive!" : "Hold Interact next to them to revive!");
            gm.Players.CheckWipe();
        }

        void UpdateDowned(float dt)
        {
            BleedLeft -= dt;
            reticle.enabled = false;
            if (Kind == PlayerKind.Local) { TickLook(dt); StepMove(Controls != null ? Controls.Move.ReadValue<Vector2>() : Vector2.zero, false, Yaw, FirstPerson, dt); }
            else { Yaw = Remote.Yaw; Pitch = Remote.Pitch; }   // online: crawling comes with their commands
            if (BleedLeft <= 0)
            {
                IsDowned = false; IsOut = true;
                gm.Banner($"{Name.ToUpperInvariant()} BLED OUT", "They'll be back next phase.");
                gm.Players.CheckWipe();
            }
        }

        public void ReviveTick(float dt)
        {
            ReviveProgress += dt / db.reviveSeconds;
            if (ReviveProgress >= 1) Revive(db.reviveHpFraction);
        }

        public void Revive(float hpFraction)
        {
            IsDowned = false; IsOut = false; ReviveProgress = 0;
            Hp = MaxHp * hpFraction;
            Statuses.Add(StatusType.Invincible, 2);
            Sfx.Play("bell", .4f, .2f);
        }

        /// <summary>Back in the game at the start of a phase (if they bled out).</summary>
        public void Respawn(Vector3 at)
        {
            if (!IsOut && !IsDowned) return;
            Place(at);
            Revive(1);
        }

        public void Grab(Enemy by, float seconds) { grabbedBy = by; Statuses.Add(StatusType.Grabbed, seconds); Shake(.2f); }
        public void Release(Enemy by) { if (grabbedBy == by) { grabbedBy = null; Statuses.Clear(StatusType.Grabbed); } }

        // ------------------------------------------------------------------ food
        public void Eat(FoodDef food, CondimentDef sauce)
        {
            Heal(food.heal);
            if (gm.Cards.Has("snacks"))   // Shared Snacks: everyone on your screen eats too
                foreach (var o in gm.Players.All) if (o != this && o.Owner == Owner) { o.Heal(food.heal); o.Toast($"{Name} shared a {food.displayName}: +{food.heal:0} HP", new Color(1, .8f, .5f)); }
            if (food.status != StatusType.None) Statuses.Add(food.status, food.statusSeconds);
            if (food.cures) { Statuses.Clear(StatusType.InkVision); Statuses.Clear(StatusType.Slow); Statuses.Clear(StatusType.Burning); }
            Sfx.Play(food.id == "drink" ? "gulp" : "eat");
            string line = $"+{food.heal:0} HP";
            if (sauce != null)
            {
                if (sauce.status == StatusType.None) line = Sauce.Roll(this);
                else { Statuses.Add(sauce.status, sauce.statusSeconds); line = sauce.description; }
                Fx.Gore(transform.position + Vector3.up * 1.6f, sauce.color, 6);
            }
            Toast($"{food.displayName}{(sauce != null ? " + " + sauce.displayName : "")}: {line}", new Color(1, .9f, .6f));
        }
    }
}

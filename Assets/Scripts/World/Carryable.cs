// TideWall 3D — anything a player can pick up, stack, carry and throw (corpses, gas cans).
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace TideWall
{
    /// <summary>Anything a player can pick up, carry (stacked, up to weight 3) and throw.</summary>
    public abstract class Carryable : Interactable
    {
        public float weight = 1;
        public Player Holder { get; set; }
        public abstract string DisplayName { get; }

        public override string Prompt(Player p)
        {
            if (Holder != null || p.IsDowned) return null;
            return p.CarryWeight + weight <= Player.MaxCarry ? $"Pick up {DisplayName}" : null;
        }

        public override void Interact(Player p) => p.PickUp(this);

        /// <summary>Launch from the player's hands. Lands via Thrown, then OnLanded.</summary>
        public void Throw(Vector3 from, Vector3 velocity)
        {
            Holder = null;
            transform.SetParent(null, true);
            transform.position = from;
            var th = gameObject.AddComponent<Thrown>();
            th.velocity = velocity;
            th.onLand = OnLanded;
        }

        public virtual void OnLanded() { }
    }
}

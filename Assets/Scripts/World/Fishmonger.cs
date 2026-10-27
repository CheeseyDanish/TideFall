// TideWall 3D — the fishmonger's cart: sells the corpses you carry (or throw) into it.
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace TideWall
{
    public class Fishmonger : Interactable
    {
        public override string Prompt(Player p)
        {
            int n = 0, v = 0;
            foreach (var c in p.Carrying) if (c is Corpse corpse) { n++; v += corpse.Value; }
            if (!GameManager.I.Phases.ShopOpen) return n > 0 ? "Fishmonger (buys in the morning)" : null;
            return n > 0 ? $"Sell {n} {(n == 1 ? "corpse" : "corpses")} (+{v})" : "Fishmonger: bring corpses (or throw them in!)";
        }

        public override void Interact(Player p)
        {
            if (!GameManager.I.Phases.ShopOpen) { Sfx.Play("deny"); GameManager.I.World.fishmonger?.Say("Come back in the morning!"); return; }
            int sold = 0;
            for (int i = p.Carrying.Count - 1; i >= 0; i--)
                if (p.Carrying[i] is Corpse c) { p.Carrying.RemoveAt(i); Sell(c); sold++; }
            if (sold > 0) GameManager.I.World.fishmonger?.Say(sold > 2 ? "What a haul!" : null);
        }

        public static void Sell(Corpse c)
        {
            GameManager.I.AddSandDollars(c.Value, c.transform.position, "sold " + c.Def.displayName);
            Fx.Splash(c.transform.position, 6);
            Destroy(c.gameObject);
        }

        /// <summary>A corpse landed: if it's in the cart and the shop is open, it sells.</summary>
        public static bool TryCatch(Corpse c)
        {
            if (!GameManager.I.Phases.ShopOpen) return false;
            foreach (var it in All)
                if (it is Fishmonger f && Vector3.Distance(Flat(f.transform.position), Flat(c.transform.position)) < 2.6f) { Sell(c); return true; }
            return false;
        }

        static Vector3 Flat(Vector3 v) => new(v.x, 0, v.z);
    }
}

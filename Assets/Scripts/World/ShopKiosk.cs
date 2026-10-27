// TideWall 3D — the shop stall: opens the shop (guns, melee, upgrades, defences) in the morning.
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace TideWall
{
    public class ShopKiosk : Interactable
    {
        public override string Prompt(Player p) => GameManager.I.Phases.ShopOpen ? "Shop" : "Shop (closed until morning)";

        public override void Interact(Player p)
        {
            if (!GameManager.I.Phases.ShopOpen) { Sfx.Play("deny"); p.Toast("The shopkeeper is hiding until morning."); return; }
            p.OpenMenu(Menus.Shop(p, Menus.ShopTab.Guns, transform.position));
        }
    }
}

// TideWall 3D — the hotdog stand (on two tires): food and condiments, open every phase.
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace TideWall
{
    public class HotdogStand : Interactable
    {
        public override string Prompt(Player p) => "Hotdog stand";
        public override void Interact(Player p)
        {
            p.OpenMenu(Menus.Hotdogs(p, transform.position));
            GameManager.I.World.chef?.Say();
        }
    }
}

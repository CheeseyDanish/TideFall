// TideWall 3D — the two slot machine cabinets beside the Lucky Barnacle: walk up and Interact to play the Tide Machine
// straight away (the same seat as the booth's Slots).
using UnityEngine;

namespace TideWall
{
    public class SlotMachineProp : Interactable
    {
        public override string Prompt(Player p) => "Play the Tide Machine (slots)";
        public override void Interact(Player p)
        {
            var c = GameManager.I.Casino;
            p.OpenMenu(Casino.Table(p, c.Seat<Slots>(p, transform.position), transform.position));
        }
    }
}

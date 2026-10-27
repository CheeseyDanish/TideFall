// TideWall 3D — the Lucky Barnacle's booth in town, and the crab's card crate on the seabed: Interact opens the casino.
using UnityEngine;

namespace TideWall
{
    /// <summary>The Lucky Barnacle's booth in town, and the card crate on the seabed.</summary>
    public class CasinoBooth : Interactable
    {
        public override string Prompt(Player p) => "The Lucky Barnacle (casino)";
        public override void Interact(Player p)
        {
            p.OpenMenu(Casino.Lobby(p, transform.position));
            GameManager.I.World?.casinoCrab?.Say();
        }
    }
}

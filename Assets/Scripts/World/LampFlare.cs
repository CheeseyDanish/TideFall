// TideWall 3D — The Last Light: the lever in the lamp room that sets off the flare.
namespace TideWall
{
    public class LampFlare : Interactable
    {
        public override string Prompt(Player p) => GameManager.I.Level is Lighthouse l ? l.FlarePrompt : null;

        public override void Interact(Player p) { if (GameManager.I.Level is Lighthouse l) l.TryFlare(p); }
    }
}

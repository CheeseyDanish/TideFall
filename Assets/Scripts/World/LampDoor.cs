// TideWall 3D — The Last Light: up in the lamp room. Take the lamp and you aim it yourself, out of reach of anything that
// doesn't fly; but a manned lamp burns oil far faster, and only one sailor fits. Beside it, the flare.
namespace TideWall
{
    public class LampDoor : Interactable
    {
        public override string Prompt(Player p)
        {
            if (GameManager.I.Level is not Lighthouse l) return null;
            return l.Keeper != null ? "Someone's at the lamp" : "Aim the lamp yourself (burns oil faster)";
        }

        public override void Interact(Player p)
        {
            if (GameManager.I.Level is Lighthouse l && l.Keeper == null && l.Oil > 0) l.SetKeeper(p);
            else Sfx.Play("deny");
        }
    }

    public class LampFlare : Interactable
    {
        public override string Prompt(Player p) => GameManager.I.Level is Lighthouse l ? l.FlarePrompt : null;

        public override void Interact(Player p) { if (GameManager.I.Level is Lighthouse l) l.TryFlare(p); }
    }
}

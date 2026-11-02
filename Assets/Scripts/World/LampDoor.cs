// TideWall 3D — The Last Light: the door at the foot of the lighthouse. Go up and you aim the lamp yourself, and you can
// shoot from up there, out of reach; but a manned lamp burns oil far faster, and only one sailor fits.
namespace TideWall
{
    public class LampDoor : Interactable
    {
        public override string Prompt(Player p)
        {
            if (GameManager.I.Level is not Lighthouse l) return null;
            return l.Keeper != null ? "Someone's at the lamp" : "Climb up and aim the lamp (burns oil faster)";
        }

        public override void Interact(Player p)
        {
            if (GameManager.I.Level is Lighthouse l && l.Keeper == null && l.Oil > 0) l.SetKeeper(p);
            else Sfx.Play("deny");
        }
    }
}

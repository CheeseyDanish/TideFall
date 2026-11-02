// TideWall 3D — Tide Caller: the conch on its rock by the lighthouse. Whoever takes it calls the tide against everyone else.
namespace TideWall
{
    public class ConchShell : Interactable
    {
        public override string Prompt(Player p)
        {
            if (GameManager.I.Level is not Lighthouse l || l.Caller != null) return null;
            return GameManager.I.Players.Count < 2 ? "The Tide Caller needs someone to fight (a second player)" : "Take the conch and call the tide against them";
        }

        public override void Interact(Player p)
        {
            if (GameManager.I.Level is Lighthouse l && l.Caller == null && GameManager.I.Players.Count >= 2) l.SetCaller(p);
            else Sfx.Play("deny");
        }
    }
}

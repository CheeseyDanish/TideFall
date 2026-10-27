// TideWall 3D — the ready bell: ring it to say you're ready for the next phase.
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace TideWall
{
    public class ReadyBell : Interactable
    {
        public override string Prompt(Player p)
        {
            var ph = GameManager.I.Phases.Current;
            if (ph == null || !ph.endWhenAllReady || GameManager.I.State != GameState.Playing) return null;
            return p.Ready ? "Not ready yet" : "Ring the bell (ready)";
        }

        public override void Interact(Player p)
        {
            p.Ready = !p.Ready;
            Sfx.Play("bell", p.Ready ? 1 : .5f, .2f);
            var pm = GameManager.I.Players;
            GameManager.I.Banner(p.Ready ? $"{p.Name} is ready" : $"{p.Name} isn't ready", $"{pm.ReadyCount}/{pm.Count} ready");
        }
    }
}

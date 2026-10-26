// TideWall 3D — timed status effects on a player (condiments, food, ??? sauce, grabs).
using System.Collections.Generic;
using UnityEngine;

namespace TideWall
{
    public class StatusEffects
    {
        readonly Dictionary<StatusType, float> active = new();
        readonly List<StatusType> expired = new();

        public void Add(StatusType t, float seconds)
        {
            if (t == StatusType.None) return;
            active[t] = Mathf.Max(seconds, Remaining(t));
            if (t == StatusType.Giant) active.Remove(StatusType.Tiny);
            if (t == StatusType.Tiny) active.Remove(StatusType.Giant);
        }

        public bool Has(StatusType t) => active.ContainsKey(t);
        public float Remaining(StatusType t) => active.TryGetValue(t, out var s) ? s : 0;
        public void Clear(StatusType t) => active.Remove(t);
        public void ClearAll() => active.Clear();
        public IEnumerable<KeyValuePair<StatusType, float>> Active => active;

        public void Tick(float dt)
        {
            expired.Clear();
            foreach (var k in new List<StatusType>(active.Keys))
            {
                active[k] -= dt;
                if (active[k] <= 0) expired.Add(k);
            }
            foreach (var k in expired) active.Remove(k);
        }

        public static string Label(StatusType t) => t switch
        {
            StatusType.Rage => "RAGE", StatusType.Speed => "SPEED", StatusType.SugarRush => "SUGAR RUSH", StatusType.Giant => "GIANT",
            StatusType.Invincible => "INVINCIBLE", StatusType.DoubleDamage => "DOUBLE DAMAGE", StatusType.UpsideDown => "UPSIDE DOWN",
            StatusType.ReverseControls => "REVERSED", StatusType.Tiny => "TINY", StatusType.Fisheye => "FISHEYE", StatusType.Slow => "SLOW",
            StatusType.SwapView => "WRONG VIEW", StatusType.InkVision => "INKED", StatusType.Tunnel => "TUNNEL VISION",
            StatusType.Rainbow => "RAINBOW", StatusType.DutchTilt => "TILTED", StatusType.Bubbles => "BUBBLES", StatusType.Grabbed => "GRABBED",
            _ => t.ToString().ToUpperInvariant(),
        };

        /// <summary>Good (green), bad (red) or neutral (grey) — the HUD colours status names by this.</summary>
        public static int Mood(StatusType t) => t switch
        {
            StatusType.Rage or StatusType.Speed or StatusType.SugarRush or StatusType.Giant or StatusType.Invincible or StatusType.DoubleDamage => 1,
            StatusType.UpsideDown or StatusType.ReverseControls or StatusType.Tiny or StatusType.Fisheye or StatusType.Slow or StatusType.SwapView
                or StatusType.InkVision or StatusType.Tunnel or StatusType.Grabbed or StatusType.Burning or StatusType.Stunned => -1,
            _ => 0,
        };
    }

    /// <summary>The ??? sauce: a random outcome, good, bad or neutral. Very rarely, a jackpot.</summary>
    public static class Sauce
    {
        static readonly (StatusType status, float seconds, float weight, string line)[] Table =
        {
            (StatusType.Giant, 20, 1, "You feel ENORMOUS."),
            (StatusType.Invincible, 8, .6f, "Nothing can touch you!"),
            (StatusType.DoubleDamage, 15, 1, "Double damage!"),
            (StatusType.None, 0, .8f, "HEAL"),           // full heal
            (StatusType.None, 0, .8f, "CASH"),          // a handful of sand dollars
            (StatusType.UpsideDown, 12, 1, "The world flips over."),
            (StatusType.ReverseControls, 10, 1, "Left is right. Right is left."),
            (StatusType.Tiny, 20, 1, "You shrink!"),
            (StatusType.Fisheye, 15, 1, "Everything bulges."),
            (StatusType.Slow, 12, 1, "Your legs feel like lead."),
            (StatusType.SwapView, 20, 1, "Your view flips to the other camera. Weird."),
            (StatusType.InkVision, 6, .7f, "It was squid ink."),
            (StatusType.Tunnel, 15, .8f, "Tunnel vision."),
            (StatusType.Rainbow, 30, 1, "You're glowing every colour."),
            (StatusType.DutchTilt, 20, .9f, "Everything's at an angle."),
            (StatusType.Bubbles, 30, .8f, "You're... blowing bubbles?"),
        };

        public static string Roll(Player p)
        {
            var gm = GameManager.I;
            if (Random.value < gm.DB.sauceJackpotChance)
            {
                gm.AddSandDollars(gm.DB.sauceJackpotSandDollars, p.transform.position, "SAUCE JACKPOT");
                Sfx.Play("jackpot");
                gm.Banner("SAUCE JACKPOT!", $"{p.Name} found {gm.DB.sauceJackpotSandDollars} sand dollars at the bottom of the bottle");
                return "JACKPOT!";
            }
            float total = 0;
            foreach (var e in Table) total += e.weight;
            float r = Random.value * total;
            foreach (var e in Table)
            {
                r -= e.weight;
                if (r > 0) continue;
                if (e.line == "HEAL") { p.Heal(9999); return "It tastes like a full recovery."; }
                if (e.line == "CASH") { int c = Random.Range(25, 151); gm.AddSandDollars(c, p.transform.position, "sauce"); return $"There were {c} sand dollars in it."; }
                p.Statuses.Add(e.status, e.seconds);
                return e.line;
            }
            return "Nothing happened. Suspicious.";
        }
    }
}

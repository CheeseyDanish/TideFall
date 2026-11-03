// TideWall 3D — the fishmonger's cart: sells the corpses you carry (or throw) into it.
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace TideWall
{
    public class Fishmonger : Interactable
    {
        public override string Prompt(Player p)
        {
            int n = 0, v = 0;
            foreach (var c in p.Carrying) if (c is Corpse corpse) { n++; v += corpse.Value; }
            if (GameManager.I.Level is Lighthouse)
            {
                int loose = Loose();
                return n + loose == 0 ? "Try-pot: nothing to render" : $"Vacuum up {n + loose} {(n + loose == 1 ? "corpse" : "corpses")} for oil and sand dollars";
            }
            if (!GameManager.I.Phases.ShopOpen) return n > 0 ? "Fishmonger (buys in the morning)" : null;
            return n > 0 ? $"Sell {n} {(n == 1 ? "corpse" : "corpses")} (+{v})" : "Fishmonger: bring corpses (or throw them in!)";
        }

        public override void Interact(Player p)
        {
            if (GameManager.I.Level is Lighthouse)
            {
                int carried = 0;
                for (int i = p.Carrying.Count - 1; i >= 0; i--)
                    if (p.Carrying[i] is Corpse held) { p.Carrying.RemoveAt(i); Sell(held); carried++; }
                if (VacuumAll() + carried == 0) Sfx.Play("deny");
                return;
            }
            if (!GameManager.I.Phases.ShopOpen) { Sfx.Play("deny"); GameManager.I.World?.fishmonger?.Say("Come back in the morning!"); return; }
            int sold = 0;
            for (int i = p.Carrying.Count - 1; i >= 0; i--)
                if (p.Carrying[i] is Corpse c) { p.Carrying.RemoveAt(i); Sell(c); sold++; }
            if (sold > 0) GameManager.I.World?.fishmonger?.Say(sold > 2 ? "What a haul!" : null);
        }

        public static void Sell(Corpse c)
        {
            GameManager.I.AddSandDollars(c.Value, c.transform.position, "sold " + c.Def.displayName);
            GameManager.I.Level.OnCorpseSold(c.Value);
            Fx.Splash(c.transform.position, 6);
            Destroy(c.gameObject);
        }

        static int Loose()
        {
            int n = 0;
            foreach (var c in Corpse.Live) if (c.Holder == null && c.Thief == null && !c.GetComponent<CorpseVacuum>()) n++;
            return n;
        }

        /// <summary>The Last Light's try-pot: every corpse lying around flies in. Happens by itself when a night ends. How many it took.</summary>
        public static int VacuumAll()
        {
            Fishmonger pot = null;
            foreach (var it in All) if (it is Fishmonger f) pot = f;
            if (pot == null) return 0;
            int n = 0;
            foreach (var c in Corpse.Live.ToArray())
                if (c.Holder == null && c.Thief == null && !c.GetComponent<CorpseVacuum>())
                    CorpseVacuum.Begin(c, pot.transform.position + Vector3.up * 1.2f, n++ * .06f);
            if (n > 0) Sfx.Play("splash", .8f, .2f);
            return n;
        }

        /// <summary>A corpse landed: if it's in the cart and the shop is open, it sells.</summary>
        public static bool TryCatch(Corpse c)
        {
            if (!GameManager.I.Phases.ShopOpen && GameManager.I.Level is not Lighthouse) return false;
            foreach (var it in All)
                if (it is Fishmonger f && Vector3.Distance(Flat(f.transform.position), Flat(c.transform.position)) < 2.6f) { Sell(c); return true; }
            return false;
        }

        static Vector3 Flat(Vector3 v) => new(v.x, 0, v.z);
    }

    /// <summary>A corpse on its way into the try-pot.</summary>
    public class CorpseVacuum : MonoBehaviour
    {
        const float Seconds = .8f;
        Corpse corpse; Vector3 from, to; float t;

        public static void Begin(Corpse c, Vector3 to, float delay)
        {
            var v = c.gameObject.AddComponent<CorpseVacuum>();
            v.corpse = c; v.from = c.transform.position; v.to = to; v.t = -delay;
            if (c.TryGetComponent<DeathFlop>(out var flop)) Destroy(flop);
        }

        void Update()
        {
            if (corpse == null || corpse.Holder != null) { Destroy(this); return; }
            t += Time.deltaTime / Seconds;
            if (t <= 0) return;
            float k = t * t;   // slowly, then all at once
            var at = Vector3.Lerp(from, to, k);
            at.y += Mathf.Sin(k * Mathf.PI) * 2.5f;
            transform.position = at;
            transform.localScale = Vector3.one * Mathf.Lerp(1, .3f, k);
            if (t >= 1) Fishmonger.Sell(corpse);
        }
    }
}

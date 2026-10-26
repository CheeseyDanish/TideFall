// TideWall 3D — tidal cards: the run's roguelike modifiers. At the start of a phase with `tidalDraft`, the team is
// offered three cards (GameDatabase.cards) plus a face-down random one, and rarely a re-roll; everyone votes (each
// player in their own view; the timer decides for anyone who doesn't). A card lasts a number of waves, carrying over into later phases, and several can be active.
//
// Some cards are exclusive: Online cards only appear when players on different computers share the game, and
// Split Screen cards only when two or more people share one screen. Their effects are unique to that situation:
//   buddybeam   a laser links teammates on different computers; creatures that cross it burn
//   bottle      every wave, each player gets a condiment boost "sent" by a teammate on another computer
//   screenswap  every 20 s the viewports on each screen shuffle; +40% damage for players sharing a screen
//   snacks      food heals everyone on your screen; reviving a screen-mate is twice as fast
using System.Collections.Generic;
using UnityEngine;

namespace TideWall
{
    public class TidalCards : MonoBehaviour
    {
        public class Active { public TidalCardDef def; public int wavesLeft; }

        public readonly List<Active> Actives = new();
        /// <summary>The cards being voted on (null when there's no draft).</summary>
        public List<TidalCardDef> Draft { get; private set; }
        public float DraftLeft { get; private set; }
        /// <summary>This vote also offers a re-roll (three new cards). Choices: 0..n-1 the cards, n random, n+1 re-roll.</summary>
        public bool RerollOffered { get; private set; }
        public readonly Dictionary<int, int> Votes = new();   // player index → choice
        public const string MenuTag = "TIDAL CARDS";
        public float SwapClock { get; private set; }

        GameManager gm;
        float beamT;

        void Awake() => gm = GetComponent<GameManager>();

        // ------------------------------------------------------------------ modifiers (product / any / max of the active cards)
        float Product(System.Func<TidalCardDef, float> f) { float m = 1; foreach (var a in Actives) m *= f(a.def); return m; }
        public float EnemyHp => Product(c => c.enemyHp);
        public float EnemySpeed => Product(c => c.enemySpeed);
        public float EnemyDamage => Product(c => c.enemyDamage);
        public float Budget => Product(c => c.budget);
        public float SandDollars => Product(c => c.sandDollars);
        public float Xp => Product(c => c.xp);
        public float PlayerDamage => Product(c => c.playerDamage);
        public float PlayerSpeed => Product(c => c.playerSpeed);
        public float PlayerMaxHp => Product(c => c.playerMaxHp);
        public float CorpseValue => Product(c => c.corpseValue);
        public bool AlwaysWet => Actives.Exists(a => a.def.alwaysWet);
        public float Fog { get { float f = 0; foreach (var a in Actives) f += a.def.fogDensity; return f; } }
        public float BreakerReach { get { float r = 0; foreach (var a in Actives) r = Mathf.Max(r, a.def.breakerReach); return r; } }
        public float SandDollarLifetime { get { float l = 0; foreach (var a in Actives) if (a.def.sandDollarLifetime > 0) l = l == 0 ? a.def.sandDollarLifetime : Mathf.Min(l, a.def.sandDollarLifetime); return l; } }
        public bool Has(string special) => Actives.Exists(a => a.def.special == special);

        // ------------------------------------------------------------------ who's playing how
        /// <summary>Players on two or more different computers (an online game with company).</summary>
        public bool OnlineEligible
        {
            get
            {
                if (!Net.Online) return false;
                var owners = new HashSet<int>();
                foreach (var p in gm.Players.All) owners.Add(p.Owner);
                return owners.Count >= 2;
            }
        }

        /// <summary>Two or more people sharing one screen (on any computer in the game).</summary>
        public bool SplitEligible
        {
            get
            {
                var counts = new Dictionary<int, int>();
                foreach (var p in gm.Players.All) { counts.TryGetValue(p.Owner, out int n); counts[p.Owner] = n + 1; }
                foreach (var n in counts.Values) if (n >= 2) return true;
                return false;
            }
        }

        /// <summary>Is this player sharing their screen with someone (same computer)?</summary>
        public bool SharesScreen(Player p) => gm.Players.All.Exists(o => o != p && o.Owner == p.Owner);

        bool Allowed(TidalCardDef c) => c.exclusive switch
        {
            CardExclusive.Online => OnlineEligible,
            CardExclusive.SplitScreen => SplitEligible,
            _ => true,
        };

        // ------------------------------------------------------------------ the draft
        bool rerolling;

        public void OfferDraft()
        {
            if (Net.IsClient || gm.DB.cards.Count == 0 || gm.Players.Count == 0) return;
            var pool = gm.DB.cards.FindAll(Allowed);
            var exclusive = pool.FindAll(c => c.exclusive != CardExclusive.None);
            var draft = new List<TidalCardDef>();
            if (exclusive.Count > 0 && Random.value < gm.DB.exclusiveCardChance) draft.Add(Pick(exclusive, draft));
            while (draft.Count < gm.DB.cardChoices && draft.Count < pool.Count) draft.Add(Pick(pool, draft));
            Draft = draft;
            RerollOffered = !rerolling && Random.value < gm.DB.cardRerollChance;
            DraftLeft = gm.DB.cardVoteSeconds;
            Votes.Clear();
            // (no banner: the card vote itself opens in everyone's view)
            Sfx.Play("bell", .7f, .2f);
            foreach (var p in gm.Players.All)
                if (p.Simulated && (p.OpenedMenu == null || !p.OpenedMenu.System)) p.OpenMenu(Menus.CardVote(p));
        }

        static TidalCardDef Pick(List<TidalCardDef> from, List<TidalCardDef> taken)
        {
            float total = 0;
            foreach (var c in from) if (!taken.Contains(c)) total += c.weight;
            float r = Random.value * total;
            foreach (var c in from)
            {
                if (taken.Contains(c)) continue;
                r -= c.weight;
                if (r <= 0) return c;
            }
            return from.Find(c => !taken.Contains(c));
        }

        public void Vote(Player p, int choice)
        {
            if (Draft == null || choice < 0 || choice > Draft.Count + (RerollOffered ? 1 : 0)) return;
            Votes[p.Index] = choice;
            Sfx.Play("ui");
            if (Votes.Count >= gm.Players.Count) Resolve();
        }

        void Resolve()
        {
            if (Draft == null) return;
            var tally = new int[Draft.Count + (RerollOffered ? 2 : 1)];
            foreach (var v in Votes.Values) tally[v]++;
            int best = -1; var tied = new List<int>();
            for (int i = 0; i < tally.Length; i++)
            {
                if (tally[i] > best) { best = tally[i]; tied.Clear(); }
                if (tally[i] == best) tied.Add(i);
            }
            int pick = tied[Random.Range(0, tied.Count)];
            if (pick == Draft.Count + 1)
            {
                // Re-roll: three fresh cards (no second re-roll)
                Draft = null; Votes.Clear();
                rerolling = true;
                OfferDraft();
                rerolling = false;
                gm.Banner("RE-ROLL!", "Three new cards. Vote again.");
                return;
            }
            TidalCardDef chosen;
            if (pick == Draft.Count)
            {
                var pool = gm.DB.cards.FindAll(Allowed);
                chosen = pool[Random.Range(0, pool.Count)];
                gm.Banner("THE TIDE DECIDES…", $"The random card was {chosen.displayName}!");
            }
            else chosen = Draft[pick];
            Draft = null;
            Votes.Clear();
            foreach (var p in gm.Players.All) if (p.OpenedMenu != null && p.OpenedMenu.Title == MenuTag) p.CloseMenu();
            Activate(chosen);
        }

        public void Activate(TidalCardDef def)
        {
            var existing = Actives.Find(a => a.def == def);
            if (existing != null) existing.wavesLeft = Mathf.Max(existing.wavesLeft, def.waves);
            else Actives.Add(new Active { def = def, wavesLeft = def.waves });
            gm.Banner(def.displayName.ToUpperInvariant(), $"{def.description}\n<size=14>Lasts {def.waves} waves</size>");
            Sfx.Play("horn");
            foreach (var p in gm.Players.All) p.RefreshMaxHp();
        }

        /// <summary>A wave ended (the next one started, or the phase's last wave was cleared).</summary>
        public void WaveFinished()
        {
            if (Net.IsClient) return;
            if (Has("bottle")) SendBottles();
            for (int i = Actives.Count - 1; i >= 0; i--)
            {
                var a = Actives[i];
                if (--a.wavesLeft > 0) continue;
                Actives.RemoveAt(i);
                gm.Banner($"{a.def.displayName.ToUpperInvariant()} EBBS AWAY", "That tidal card has run out.");
                foreach (var p in gm.Players.All) p.RefreshMaxHp();
            }
        }

        // ------------------------------------------------------------------ exclusive effects
        void SendBottles()
        {
            var boosts = gm.DB.condiments.FindAll(c => c.status != StatusType.None);
            if (boosts.Count == 0) return;
            foreach (var p in gm.Players.All)
            {
                var others = gm.Players.All.FindAll(o => o.Owner != p.Owner);
                if (others.Count == 0) continue;
                var from = others[Random.Range(0, others.Count)];
                var c = boosts[Random.Range(0, boosts.Count)];
                p.Statuses.Add(c.status, c.statusSeconds);
                p.Toast($"Message in a bottle from {from.Name}: {c.displayName}! {c.description}", new Color(.6f, .9f, .5f));
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (Has("screenswap")) SwapClock += dt; else SwapClock = 0;
            if (Net.IsClient || gm.State != GameState.Playing) return;
            if (Draft != null)
            {
                DraftLeft -= dt;
                if (DraftLeft <= 0) Resolve();
            }
            if (Has("buddybeam"))
            {
                beamT -= dt;
                if (beamT <= 0) { beamT = .15f; BuddyBeams(.15f); }
            }
        }

        /// <summary>Lasers between teammates on different computers; creatures touching one burn.</summary>
        void BuddyBeams(float step)
        {
            var ps = gm.Players.All;
            for (int i = 0; i < ps.Count; i++)
                for (int j = i + 1; j < ps.Count; j++)
                {
                    var a = ps[i]; var b = ps[j];
                    if (a.Owner == b.Owner || !a.IsTargetable || !b.IsTargetable) continue;
                    Vector3 pa = a.transform.position + Vector3.up, pb = b.transform.position + Vector3.up;
                    if (Vector3.Distance(pa, pb) > 30) continue;   // too far apart: the beam snaps
                    Fx.Tracer(pa, pb, new Color(.3f, 1f, 1f), .07f, step * 1.4f);
                    foreach (var e in GameManager.Enemies.ToArray())
                    {
                        if (e == null || e.Dead) continue;
                        var c = e.transform.position + Vector3.up * .5f;
                        var ab = pb - pa;
                        float t = Mathf.Clamp01(Vector3.Dot(c - pa, ab) / Mathf.Max(.01f, ab.sqrMagnitude));
                        if (Vector3.Distance(c, pa + ab * t) < 1.1f)
                            e.TakeHit(new DamageInfo { amount = 45 * step, element = Element.Fire, ignoreArmor = true, source = a, point = c }, null);
                    }
                }
        }

        // ------------------------------------------------------------------ saves and clients
        public void Restore(List<CardSave> saved)
        {
            Actives.Clear();
            if (saved == null) return;
            foreach (var c in saved) if (gm.DB.Card(c.id) is { } def) Actives.Add(new Active { def = def, wavesLeft = c.wavesLeft });
        }

        /// <summary>Client: the host's cards and draft.</summary>
        public void ApplyNet(List<(string id, int left)> actives, List<string> draft, float draftLeft, Dictionary<int, int> votes, bool reroll)
        {
            RerollOffered = reroll;
            Actives.Clear();
            foreach (var (id, left) in actives) if (gm.DB.Card(id) is { } def) Actives.Add(new Active { def = def, wavesLeft = left });
            if (draft == null) Draft = null;
            else { Draft = draft.ConvertAll(id => gm.DB.Card(id)); Draft.RemoveAll(c => c == null); }
            DraftLeft = draftLeft;
            Votes.Clear();
            foreach (var kv in votes) Votes[kv.Key] = kv.Value;
        }
    }

    [System.Serializable] public class CardSave { public string id; public int wavesLeft; }
}

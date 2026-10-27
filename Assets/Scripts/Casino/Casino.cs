// TideWall 3D — the casino: the Lucky Barnacle, a booth in town run by a crab (and a crate of cards beside the supply
// cache on Part II's seabed). Five games, all played with the team's sand dollars, never real money:
//   Slots, Blackjack, Roulette, Video Poker (Jacks or Better) and Fathoms (baccarat, as sailors play it).
// Each game is a CasinoGame, one per player per game, run on the host (online players see its table through the
// menu's Board). Its menu uses the Table layout: the felt (what's on the table, redrawn every frame so reels can spin
// and cards turn over) above the list of actions. In Part I the casino only deals between fights.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace TideWall
{
    /// <summary>One casino game at one player's seat.</summary>
    public abstract class CasinoGame
    {
        public Player Player;
        public Casino Casino;
        public Vector3 Seat;
        public abstract string Name { get; }
        /// <summary>One line for the casino's list of games.</summary>
        public abstract string Blurb { get; }
        /// <summary>What it pays at best, for the list ("up to 80×").</summary>
        public abstract string Pays { get; }
        /// <summary>"How to play": pages of rules shown on the felt instead of the table.</summary>
        public abstract string[] Rules { get; }
        public int RulesPage = -1;
        /// <summary>A hand or spin is under way: the bet can't change and you can't start another.</summary>
        public abstract bool Busy { get; }
        /// <summary>The table as rich text.</summary>
        protected abstract string Table();
        /// <summary>The same wager again in one press (Reload): spin again, deal again, re-bet and spin. Null label = nothing to repeat now.</summary>
        public abstract string RepeatLabel { get; }
        public abstract void Repeat();
        /// <summary>The game's own actions (deal, hit, spin…), added before the bet row.</summary>
        public abstract void Actions(Menu m);
        /// <summary>This game takes the shared bet row (roulette places its own chips, but uses the same amount).</summary>
        public virtual bool UsesBet => true;
        public virtual void Tick(float dt) { }

        public string Board() => RulesPage >= 0 && RulesPage < Rules.Length
            ? $"<size=15><align=left>{Rules[RulesPage]}</align></size>\n<size=13>{Felt.Dim($"How to play · page {RulesPage + 1} of {Rules.Length}")}</size>"
            : Table();

        protected int Bet => Casino.Bet(Player);
        /// <summary>Take a stake from the team's purse. False (and a toast) if the team can't cover it.</summary>
        protected bool Stake(int amount) => Casino.Stake(Player, amount);
        /// <summary>Hand back stake + winnings (the whole amount returned to the purse).</summary>
        protected void Pay(int amount) => Casino.Pay(amount, Seat, Name);
        /// <summary>Why a new hand can't start, or null.</summary>
        protected string CantStart(int stake) => Busy ? "Wait for this one to finish" : Casino.Closed() ?? (Casino.Gm.SandDollars < stake ? "The team doesn't have enough sand dollars" : null);
    }

    public class Casino : MonoBehaviour
    {
        public const string Title = "THE LUCKY BARNACLE";
        public static readonly int[] Bets = { 5, 10, 25, 50, 100, 250, 500, 1000 };
        /// <summary>What the crab behind the counter shouts.</summary>
        public static readonly List<string> CrabLines = new()
        {
            "Step right up! Every sailor's a winner! Eventually!", "Feeling lucky? You look lucky.", "No refunds. Crabs don't do refunds.",
            "That's the wall's repair money? Even better.", "The house always wins. I am the house.", "Slots! Cards! The wheel! Fathoms!",
            "Don't count cards in front of the crab.", "Double or nothing, sailor?",
        };

        public GameManager Gm { get; private set; }
        /// <summary>This run's result across every table (positive: the team is up).</summary>
        public int Net { get; private set; }
        public int HandsPlayed { get; private set; }
        readonly Dictionary<int, int> betIndex = new();
        readonly Dictionary<(int, Type), CasinoGame> seats = new();

        void Awake() => Gm = GetComponent<GameManager>();

        void Update()
        {
            float dt = Time.deltaTime;
            foreach (var g in seats.Values) g.Tick(dt);
        }

        /// <summary>This player's seat at game T (made on first visit; it keeps a hand in progress if you walk off).</summary>
        public T Seat<T>(Player p, Vector3 at) where T : CasinoGame, new()
        {
            if (!seats.TryGetValue((p.Index, typeof(T)), out var g) || g.Player != p)
            {
                g = new T { Player = p, Casino = this };
                seats[(p.Index, typeof(T))] = g;
            }
            g.Seat = at;
            return (T)g;
        }

        public int Bet(Player p) => Bets[betIndex.TryGetValue(p.Index, out var i) ? i : 1];
        public void ChangeBet(Player p, int d)
        {
            int i = betIndex.TryGetValue(p.Index, out var v) ? v : 1;
            betIndex[p.Index] = Mathf.Clamp(i + d, 0, Bets.Length - 1);
        }

        /// <summary>Why the tables aren't dealing right now, or null. Part I deals between fights only.</summary>
        public string Closed()
        {
            if (Gm.State != GameState.Playing) return "The casino is closed";
            if (DebugTools.CasinoAlwaysOpen) return null;
            if (!Gm.Assault && Gm.Phases.Current != null && !Gm.Phases.ShopOpen) return "Closed during the fight. Come back when it's quiet.";
            return null;
        }

        public bool Stake(Player p, int amount)
        {
            if (!Gm.TrySpend(amount)) { p.Toast("Not enough sand dollars", new Color(1, .6f, .6f)); Sfx.Play("deny"); return false; }
            Net -= amount;
            HandsPlayed++;
            Sfx.Play("coin", .4f, .1f);
            return true;
        }

        public void Pay(int amount, Vector3 where, string game)
        {
            if (amount <= 0) return;
            Net += amount;
            Gm.AddSandDollars(amount, where + Vector3.up * 2, game);
        }

        // ------------------------------------------------------------------ menus
        /// <summary>The casino's front: the five games.</summary>
        public static Menu Lobby(Player p, Vector3 at)
        {
            var c = GameManager.I.Casino;
            var m = new Menu { Title = Title, Layout = MenuLayout.Table, Anchor = at, AnchorRange = 7 };
            m.Rebuild = () => Lobby(p, at);
            m.Board = () =>
            {
                string net = c.Net > 0 ? Felt.Good($"+{c.Net}") : c.Net < 0 ? Felt.Bad(c.Net.ToString()) : "0";
                string closed = c.Closed();
                return $"<size=34><color=#fde68a><b>~ THE LUCKY BARNACLE ~</b></color></size>\n" +
                       "<size=17>Five games. All played with the team's sand dollars.</size>\n\n" +
                       $"<size=18>This run at the tables: <b>{net} SD</b> over {c.HandsPlayed} hand{(c.HandsPlayed == 1 ? "" : "s")}</size>\n" +
                       (closed != null ? $"\n<size=18>{Felt.Bad(closed)}</size>" : "\n<size=15>" + Felt.Dim("Pick a game below. Every game has a How to play page.") + "</size>");
            };
            void Game<T>() where T : CasinoGame, new()
            {
                var g = c.Seat<T>(p, at);
                m.Add(g.Name, g.Pays, g.Blurb, open: () => Table(p, g, at));
            }
            Game<Slots>();
            Game<Blackjack>();
            Game<Roulette>();
            Game<VideoPoker>();
            Game<Fathoms>();
            m.Add("Leave", null, "Back to the beach.", pick: p.CloseMenu);
            m.FirstSelectable();
            return m;
        }

        /// <summary>One game's table: its actions, the bet, how to play.</summary>
        public static Menu Table(Player p, CasinoGame g, Vector3 at)
        {
            var c = g.Casino;
            var m = new Menu { Title = $"{Title} · {g.Name.ToUpperInvariant()}", Layout = MenuLayout.Table, Anchor = at, AnchorRange = 7, Board = g.Board };
            m.Rebuild = () => Table(p, g, at);
            m.Parent = () => Lobby(p, at);
            m.Quick = () => { if (g.RepeatLabel != null) g.Repeat(); };
            m.QuickLabel = () => g.RepeatLabel;
            g.Actions(m);
            if (g.UsesBet)
            {
                var bet = m.Add("Bet", $"< {c.Bet(p)} SD >", "Left/right change the bet. Bets come out of the team's purse.");
                void Change(int d) { if (g.Busy) return; c.ChangeBet(p, d); bet.Right = $"< {c.Bet(p)} SD >"; }
                bet.OnAdjust = Change;
                m.OnSide = Change;   // left/right changes the bet from any row
                bet.OnPick = () => Change(c.Bet(p) == Bets[^1] ? -Bets.Length : 1);
            }
            m.Add("How to play", g.RulesPage < 0 ? $"{g.Rules.Length} page{(g.Rules.Length == 1 ? "" : "s")}" : g.RulesPage + 1 < g.Rules.Length ? "Next page" : "Back to the table",
                  "The rules, on the table.", pick: () => g.RulesPage = g.RulesPage + 1 >= g.Rules.Length ? -1 : g.RulesPage + 1);
            m.Add("Other games", null, null, open: () => Lobby(p, at));
            m.FirstSelectable();
            return m;
        }
    }
}

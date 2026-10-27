// TideWall 3D — casino: video poker, Jacks or Better (the classic 9/6 machine, with the royal flush paying its
// full-coin 800). Five cards from a fresh deck; hold any of them, draw once to replace the rest, and the final hand
// is paid from the table. A pair of jacks or better gets your bet back.
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace TideWall
{
    public class VideoPoker : CasinoGame
    {
        public enum Rank { Nothing, JacksOrBetter, TwoPair, ThreeOfAKind, Straight, Flush, FullHouse, FourOfAKind, StraightFlush, RoyalFlush }
        public static readonly int[] Paytable = { 0, 1, 2, 3, 4, 6, 9, 25, 50, 800 };
        public static readonly string[] RankNames = { "Nothing", "Jacks or better", "Two pair", "Three of a kind", "Straight", "Flush", "Full house", "Four of a kind", "Straight flush", "Royal flush" };

        public override string Name => "Video Poker";
        public override string Blurb => "Jacks or Better: five cards, hold the ones you want, draw once. Paid by poker hand.";
        public override string Pays => "up to 800×";
        public override string[] Rules => new[]
        {
            "<b>VIDEO POKER · JACKS OR BETTER</b>\n" +
            "1  <b>Deal</b>: five cards from a freshly shuffled deck.\n" +
            "2  <b>Hold</b> the cards you want to keep (pick a card to hold it; pick again to let it go).\n" +
            "3  <b>Draw</b>: every card you didn't hold is replaced, once.\n" +
            "4  Your final five cards are paid by poker hand, times your bet (the payout includes your bet):\n" +
            "    Royal flush 800 · Straight flush 50 · Four of a kind 25 · Full house 9 · Flush 6\n" +
            "    Straight 4 · Three of a kind 3 · Two pair 2 · <b>a pair of jacks, queens, kings or aces 1</b>\n" +
            "5  Anything less loses the bet. Played well, this machine pays back about 99.5%.",
        };

        public enum Phase { Deal, Draw }
        public readonly Shoe Deck = new(1);
        public readonly PlayingCard[] Cards = new PlayingCard[5];
        public readonly bool[] Held = new bool[5];
        public Phase State { get; private set; } = Phase.Deal;
        bool dealtOnce;
        int stake;
        public Rank Last { get; private set; }
        int lastWin;

        public override bool Busy => State == Phase.Draw;
        public override string RepeatLabel => State == Phase.Draw ? "draw" : dealtOnce ? "deal again" : "deal";
        public override void Repeat() { if (State == Phase.Draw) Draw(); else Deal(); }

        public override void Actions(Menu m)
        {
            if (State == Phase.Deal)
            {
                m.Add(dealtOnce ? "Deal again" : "Deal", $"{Bet} SD", "Five new cards.", pick: Deal, blocked: () => CantStart(Bet));
                return;
            }
            m.Add("Draw", $"{5 - HeldCount} new card{(5 - HeldCount == 1 ? "" : "s")}", "Replace every card you didn't hold.", pick: Draw);
            for (int i = 0; i < 5; i++)
            {
                int k = i;
                m.Add($"Card {k + 1}:  {Cards[k]}", Held[k] ? "HELD" : "hold", Held[k] ? "Let this card go." : "Keep this card when you draw.", pick: () => Held[k] = !Held[k]);
            }
        }

        int HeldCount { get { int n = 0; foreach (var h in Held) if (h) n++; return n; } }

        public void Deal()
        {
            if (Busy || CantStart(Bet) != null || !Stake(Bet)) return;
            stake = Bet;
            Deck.Shuffle();
            for (int i = 0; i < 5; i++) { Cards[i] = Deck.Draw(); Held[i] = false; }
            State = Phase.Draw;
            dealtOnce = true;
            Sfx.Play("ui");
        }

        /// <summary>Tests: these five cards, as if dealt.</summary>
        public void DealThese(params PlayingCard[] five) { Deal(); if (Busy) for (int i = 0; i < 5; i++) Cards[i] = five[i]; }

        public void Draw()
        {
            if (State != Phase.Draw) return;
            for (int i = 0; i < 5; i++) if (!Held[i]) Cards[i] = Deck.Draw();
            State = Phase.Deal;
            Last = Evaluate(Cards);
            lastWin = stake * Paytable[(int)Last];
            Pay(lastWin);
            if (Last >= Rank.Straight) Sfx.Play("bell", .6f, .2f);
        }

        public static Rank Evaluate(IList<PlayingCard> hand)
        {
            var counts = new Dictionary<int, int>();
            bool flush = true;
            foreach (var c in hand) { counts[c.Rank] = counts.TryGetValue(c.Rank, out var n) ? n + 1 : 1; if (c.Suit != hand[0].Suit) flush = false; }
            var ranks = new List<int>(counts.Keys); ranks.Sort();
            bool straight = false, royal = false;
            if (counts.Count == 5)
            {
                if (ranks[4] - ranks[0] == 4) straight = true;
                if (ranks[0] == 1 && ranks[1] == 10) { straight = true; royal = true; }   // 10 J Q K A
            }
            if (straight && flush) return royal ? Rank.RoyalFlush : Rank.StraightFlush;
            var groups = new List<int>(counts.Values); groups.Sort(); groups.Reverse();
            if (groups[0] == 4) return Rank.FourOfAKind;
            if (groups[0] == 3 && groups[1] == 2) return Rank.FullHouse;
            if (flush) return Rank.Flush;
            if (straight) return Rank.Straight;
            if (groups[0] == 3) return Rank.ThreeOfAKind;
            if (groups[0] == 2 && groups[1] == 2) return Rank.TwoPair;
            if (groups[0] == 2) foreach (var kv in counts) if (kv.Value == 2 && (kv.Key == 1 || kv.Key >= 11)) return Rank.JacksOrBetter;
            return Rank.Nothing;
        }

        protected override string Table()
        {
            var sb = new StringBuilder();
            if (!dealtOnce) sb.Append($"{Felt.Back(40)}  {Felt.Back(40)}  {Felt.Back(40)}  {Felt.Back(40)}  {Felt.Back(40)}\n");
            else
            {
                for (int i = 0; i < 5; i++) { if (i > 0) sb.Append("  "); sb.Append(Felt.Card(Cards[i], 40, State == Phase.Draw && Held[i])); }
                sb.Append('\n');
            }
            // Which cards are held (lit gold above)
            var held = new List<string>();
            for (int i = 0; i < 5; i++) if (State == Phase.Draw && Held[i]) held.Add((i + 1).ToString());
            sb.Append($"<size=16>{(State == Phase.Draw ? held.Count > 0 ? Felt.Gold($"HELD: card{(held.Count > 1 ? "s" : "")} {string.Join(", ", held)}") : Felt.Dim("Nothing held yet") : " ")}</size>\n");
            if (State == Phase.Draw) sb.Append($"<size=20>Hold the cards to keep, then draw.  {Felt.Dim($"(now: {RankNames[(int)Evaluate(Cards)]})")}</size>\n");
            else if (dealtOnce) sb.Append($"<size=22><b>{RankNames[(int)Last]}</b> · " + (lastWin > 0 ? Felt.Good($"pays {lastWin} SD") : Felt.Bad("no win")) + "</size>\n");
            else sb.Append("<size=20>Deal to start.</size>\n");
            // The paytable, with the hand just made lit up
            sb.Append("<size=13>");
            for (int r = Paytable.Length - 1; r >= 1; r--)
            {
                string row = $"{RankNames[r]} {Paytable[r]}";
                sb.Append(dealtOnce && State == Phase.Deal && (int)Last == r ? Felt.Gold($"<b>{row}</b>") : Felt.Dim(row));
                if (r > 1) sb.Append(r == 6 ? "\n" : " · ");
            }
            sb.Append("</size>");
            return sb.ToString();
        }
    }
}

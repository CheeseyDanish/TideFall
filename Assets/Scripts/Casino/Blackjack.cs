// TideWall 3D — casino: blackjack from a six-deck shoe. Standard rules: blackjack pays 3 to 2, the dealer stands on
// all 17s and checks for blackjack under an ace or a ten, double on any first two cards, split a pair once (split aces
// take one card each). No insurance.
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace TideWall
{
    public class Blackjack : CasinoGame
    {
        public class Hand
        {
            public readonly List<PlayingCard> Cards = new();
            public int Stake;
            public bool Done, Doubled, FromSplit;
            public string Result;
            public int Total => Value(Cards, out _);
            public bool Natural => Cards.Count == 2 && Total == 21 && !FromSplit;
        }

        public enum Phase { Betting, Playing, Over }

        public override string Name => "Blackjack";
        public override string Blurb => "Beat the dealer's hand without going over 21. Blackjack pays 3 to 2.";
        public override string Pays => "3 to 2";
        public override string Icon => "CardFan";
        public override string[] Rules => new[]
        {
            "<b>BLACKJACK</b>\n" +
            "1  Get closer to <b>21</b> than the dealer without going over. Going over (<b>bust</b>) loses at once.\n" +
            "2  Cards count their number; J, Q and K count 10; an ace counts 11, or 1 if 11 would bust you.\n" +
            "3  You and the dealer get two cards. One of the dealer's is face down.\n" +
            "4  <b>Hit</b> takes a card; <b>Stand</b> stops. <b>Double</b>: double your bet, take exactly one card.\n" +
            "    <b>Split</b>: two cards of the same value become two hands, each with its own bet (once per hand).\n" +
            "5  Then the dealer turns over and must hit until 17 or more (and stands on every 17).\n" +
            "6  Win: paid 1 to 1. Ace + ten-card on your first two (<b>blackjack</b>): paid 3 to 2. A tie (<b>push</b>) returns your bet.",
        };

        public readonly Shoe Shoe = new(6);
        public readonly List<PlayingCard> Dealer = new();
        public readonly List<Hand> Hands = new();
        public int Active { get; private set; }
        public Phase State { get; private set; } = Phase.Betting;
        string message = "Place your bet and deal.";

        public override bool Busy => State == Phase.Playing;
        public override string RepeatLabel => Busy ? null : State == Phase.Over ? "deal again" : "deal";
        public override void Repeat() => Deal();
        Hand Current => State == Phase.Playing && Active < Hands.Count ? Hands[Active] : null;

        /// <summary>A hand's best total; soft = an ace is counting 11.</summary>
        public static int Value(IList<PlayingCard> cards, out bool soft)
        {
            int total = 0, aces = 0;
            foreach (var c in cards) { total += c.Rank == 1 ? 1 : Mathf.Min(10, c.Rank); if (c.Rank == 1) aces++; }
            soft = aces > 0 && total + 10 <= 21;
            return soft ? total + 10 : total;
        }

        static int Ten(PlayingCard c) => c.Rank == 1 ? 11 : Mathf.Min(10, c.Rank);

        public override void Actions(Menu m)
        {
            if (State != Phase.Playing)
            {
                m.Add(State == Phase.Over ? "Deal again" : "Deal", $"{Bet} SD", "Start a hand with this bet.", pick: Deal, blocked: () => CantStart(Bet));
                return;
            }
            var h = Current;
            m.Add("Hit", null, "Take another card.", pick: Hit);
            m.Add("Stand", null, "Keep this total.", pick: Stand);
            m.Add("Double", h != null ? $"+{h.Stake} SD" : null, "Double the bet, take exactly one more card, then stand.", pick: Double,
                  blocked: () => Current is not { Cards: { Count: 2 } } ? "Only on your first two cards" : Casino.Gm.SandDollars < Current.Stake ? "The team doesn't have enough sand dollars" : null);
            m.Add("Split", h != null ? $"+{h.Stake} SD" : null, "Two cards of the same value become two hands, each with this bet.", pick: Split,
                  blocked: () => CanSplit() ? null : Hands.Count > 1 ? "One split per hand" : "Only a pair (two cards of the same value)");
        }

        public void Deal()
        {
            if (Busy || CantStart(Bet) != null || !Stake(Bet)) return;
            Shoe.CheckCut();
            Dealer.Clear(); Hands.Clear(); Active = 0;
            var h = new Hand { Stake = Bet };
            Hands.Add(h);
            h.Cards.Add(Shoe.Draw()); Dealer.Add(Shoe.Draw()); h.Cards.Add(Shoe.Draw()); Dealer.Add(Shoe.Draw());
            State = Phase.Playing;
            message = "Hit or stand?";
            Sfx.Play("ui");
            // The dealer peeks under an ace or a ten; blackjacks end the hand straight away
            bool dealerBj = Value(Dealer, out _) == 21;
            if (h.Natural || (dealerBj && Ten(Dealer[0]) >= 10)) Settle();
        }

        public void Hit()
        {
            var h = Current; if (h == null) return;
            h.Cards.Add(Shoe.Draw());
            Sfx.Play("ui", .6f);
            if (h.Total > 21) { h.Done = true; h.Result = "Bust"; Next(); }
            else if (h.Total == 21) { h.Done = true; Next(); }
        }

        public void Stand() { var h = Current; if (h == null) return; h.Done = true; Next(); }

        public void Double()
        {
            var h = Current; if (h == null || h.Cards.Count != 2 || !Stake(h.Stake)) return;
            h.Stake *= 2; h.Doubled = true;
            h.Cards.Add(Shoe.Draw());
            h.Done = true;
            if (h.Total > 21) h.Result = "Bust";
            Next();
        }

        bool CanSplit() => Hands.Count == 1 && Current is { Cards: { Count: 2 } } h && Ten(h.Cards[0]) == Ten(h.Cards[1]);

        public void Split()
        {
            if (!CanSplit()) return;
            var a = Current;
            if (!Stake(a.Stake)) return;
            var b = new Hand { Stake = a.Stake, FromSplit = true };
            a.FromSplit = true;
            b.Cards.Add(a.Cards[1]); a.Cards.RemoveAt(1);
            a.Cards.Add(Shoe.Draw()); b.Cards.Add(Shoe.Draw());
            Hands.Add(b);
            if (a.Cards[0].Rank == 1) { a.Done = b.Done = true; Next(); return; }   // split aces: one card each
            if (a.Total == 21) { a.Done = true; Next(); }
        }

        void Next()
        {
            while (Active < Hands.Count && Hands[Active].Done) Active++;
            if (Active < Hands.Count) { var h = Hands[Active]; if (h.Total == 21) { h.Done = true; Next(); } return; }
            Settle();
        }

        /// <summary>The dealer plays (unless every hand busted), then every hand is paid.</summary>
        void Settle()
        {
            State = Phase.Over;
            bool anyLive = Hands.Exists(h => h.Total <= 21);
            bool anyNatural = Hands.Exists(h => h.Natural);
            if (anyLive && !(Hands.Count == 1 && anyNatural))
                while (Value(Dealer, out _) < 17) Dealer.Add(Shoe.Draw());
            int dealer = Value(Dealer, out _);
            bool dealerBj = Dealer.Count == 2 && dealer == 21;
            int paid = 0, staked = 0;
            foreach (var h in Hands)
            {
                staked += h.Stake;
                int t = h.Total, back;
                if (t > 21) { back = 0; h.Result = "Bust"; }
                else if (h.Natural && !dealerBj) { back = h.Stake * 5 / 2; h.Result = "Blackjack!"; }
                else if (dealerBj && !h.Natural) { back = 0; h.Result = "Dealer blackjack"; }
                else if (dealer > 21 || t > dealer) { back = h.Stake * 2; h.Result = "Win"; }
                else if (t == dealer) { back = h.Stake; h.Result = "Push"; }
                else { back = 0; h.Result = "Lose"; }
                paid += back;
            }
            Pay(paid);
            message = paid > staked ? Felt.Good($"You win {paid - staked} SD") : paid == staked ? "Push: your bet comes back" : Felt.Bad($"The house takes {staked - paid} SD");
            if (paid > staked) { Sfx.Play("bell", .5f, .2f); Cheer(Hands.Exists(h => h.Result == "Blackjack!") ? .8f : .45f); }
        }

        protected override string Table()
        {
            var sb = new StringBuilder();
            bool hide = State == Phase.Playing;
            int shown = hide ? Ten(Dealer.Count > 0 ? Dealer[0] : default) : Value(Dealer, out _);
            sb.Append($"<size=15>{Felt.Dim("DEALER")}  {(Dealer.Count > 0 ? (hide ? $"shows {shown}" : $"<b>{shown}</b>") : "")}</size>\n");
            sb.Append(Dealer.Count > 0 ? Felt.Hand(Dealer, 36, hide ? 1 : int.MaxValue) : Felt.Dim("<size=36> </size>"));
            sb.Append("\n<size=12> </size>\n");
            if (Hands.Count == 0) sb.Append("<size=22>Blackjack pays 3 to 2 · dealer stands on 17</size>\n");
            for (int i = 0; i < Hands.Count; i++)
            {
                var h = Hands[i];
                bool on = State == Phase.Playing && i == Active;
                Value(h.Cards, out bool soft);
                string label = $"{(on ? Felt.Gold("► ") : "")}YOU{(Hands.Count > 1 ? $" (hand {i + 1})" : "")}  <b>{(soft && h.Total < 21 ? "soft " : "")}{h.Total}</b>  {Felt.Dim($"{h.Stake} SD{(h.Doubled ? " doubled" : "")}")}" +
                               (h.Result != null && State == Phase.Over ? $"  {(h.Result is "Win" or "Blackjack!" ? Felt.Good(h.Result) : h.Result == "Push" ? h.Result : Felt.Bad(h.Result))}" : "");
                sb.Append($"<size=15>{label}</size>\n{Felt.Hand(h.Cards, Hands.Count > 1 ? 30 : 36)}\n");
            }
            sb.Append($"<size=20>{message}</size>");
            return sb.ToString();
        }
    }
}

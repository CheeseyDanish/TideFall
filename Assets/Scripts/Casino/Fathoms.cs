// TideWall 3D — casino: Fathoms, baccarat as sailors play it. Two hands are dealt, the Sailor's and the Captain's;
// you only bet on which ends up DEEPER (closer to 9 fathoms), or on Slack Water (a tie). Every card is drawn by fixed
// rules (real baccarat's), so the dealer plays both hands and says, step by step, which rule it's following.
//   Count: A = 1, 2–9 their number, 10 J Q K = 0; only the last digit of a total counts (7 + 8 = 15 → 5 fathoms).
//   High Tide: 8 or 9 on the first two cards and nobody draws.
//   The Sailor casts a third line on 0–5. The Captain draws by their total and the Sailor's third card (the chart).
//   Pays: Sailor 1 to 1, Captain 1 to 1 less a 5% harbour fee, Slack Water 8 to 1 (Sailor and Captain bets come back).
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace TideWall
{
    public class Fathoms : CasinoGame
    {
        public enum Side { Sailor, Captain, Slack }
        public static readonly string[] SideNames = { "the Sailor", "the Captain", "Slack Water" };

        public override string Name => "Fathoms";
        public override string Blurb => "Sailors' baccarat. Bet on the Sailor's hand, the Captain's, or a tie. The dealer explains every draw.";
        public override string Pays => "up to 8 to 1";
        public override string[] Rules => new[]
        {
            "<b>FATHOMS</b> · baccarat, as sailors play it. You never play a hand: you only bet on one.\n" +
            "1  <b>Bet</b> on the <color=#7dd3fc>Sailor</color>, the <color=#fca5a5>Captain</color>, or <b>Slack Water</b> (a tie).\n" +
            "2  Both hands get two cards. The hand that ends up <b>deeper</b> (closer to 9 fathoms) wins.\n" +
            "3  <b>Counting</b>: ace = 1, 2–9 = their number, 10 / J / Q / K = 0.\n" +
            "    Only the <b>last digit</b> of a total counts: 7 + 8 = 15 → <b>5 fathoms</b>. 9 is the deepest.\n" +
            "4  <b>High Tide</b>: 8 or 9 on the first two cards. Nobody draws; the deeper hand wins.\n" +
            "5  Otherwise either hand may take one more card (cast a third line), by fixed rules. You decide nothing:\n" +
            "    the dealer follows the rules and tells you why (next page).\n" +
            "6  <b>Pays</b>: Sailor 1 to 1. Captain 1 to 1, less a 5% harbour fee. Slack Water 8 to 1.\n" +
            "    On Slack Water, Sailor and Captain bets come back.",

            "<b>FATHOMS</b> · when a third card is drawn (the dealer does all of this)\n" +
            "<b>The Sailor</b> goes first: draws on <b>0–5</b>, holds on <b>6 or 7</b>.\n" +
            "<b>The Captain</b>: if the Sailor held, the Captain also draws on 0–5 and holds on 6–7.\n" +
            "If the Sailor drew, the Captain looks at the Sailor's third card:\n" +
            "    Captain at <b>0–2</b>  always draws\n" +
            "    Captain at <b>3</b>  draws unless the Sailor's third card was an 8\n" +
            "    Captain at <b>4</b>  draws if it was 2–7\n" +
            "    Captain at <b>5</b>  draws if it was 4–7\n" +
            "    Captain at <b>6</b>  draws if it was 6 or 7\n" +
            "    Captain at <b>7</b>  holds\n" +
            "Why the harbour fee? These rules let the Captain win a little more often than the Sailor.",
        };

        public readonly Shoe Shoe = new(8);
        public readonly List<PlayingCard> Sailor = new(), Captain = new();
        public Side Pick = Side.Sailor;
        /// <summary>What the dealer says, one line per step; shown one at a time as the cards go down.</summary>
        readonly List<(string line, int sailorCards, int captainCards)> steps = new();
        float revealT = -1;
        int shown;
        int stake, payout;
        Side? winner;
        public Side? Winner => Busy ? null : winner;
        const float StepTime = 1.1f;

        public override bool Busy => revealT >= 0;
        public override string RepeatLabel => Busy ? "skip ahead" : steps.Count > 0 ? $"again on {Title(Pick)}" : null;
        public override void Repeat() { if (Busy) revealT = steps.Count * StepTime; else Deal(); }

        public static int Points(PlayingCard c) => c.Rank >= 10 ? 0 : c.Rank;
        public static int Depth(IList<PlayingCard> hand) { int t = 0; foreach (var c in hand) t += Points(c); return t % 10; }

        /// <summary>Real baccarat's drawing rule for the banker (here the Captain), given the player's (Sailor's) third card.</summary>
        public static bool CaptainDraws(int captain, int sailorThird) => captain switch
        {
            <= 2 => true,
            3 => sailorThird != 8,
            4 => sailorThird is >= 2 and <= 7,
            5 => sailorThird is >= 4 and <= 7,
            6 => sailorThird is 6 or 7,
            _ => false,
        };

        public override void Actions(Menu m)
        {
            m.Add(steps.Count > 0 ? "Cast off again" : "Cast off", $"{Bet} SD on {Title(Pick)}", "Deal the two hands. The dealer explains each step.", pick: Deal, blocked: () => CantStart(Bet));
            var side = m.Add("Bet on", $"< {Title(Pick)} >", "Left/right choose: the Sailor (1 to 1), the Captain (1 to 1 less a 5% harbour fee), or Slack Water, a tie (8 to 1).");
            side.OnAdjust = d => { if (!Busy) { Pick = (Side)(((int)Pick + d + 3) % 3); side.Right = $"< {Title(Pick)} >"; } };
            side.OnPick = () => { if (!Busy) Pick = (Side)(((int)Pick + 1) % 3); };
            m.Add("Skip ahead", null, "Show the rest of this hand at once.", pick: () => { if (Busy) revealT = steps.Count * StepTime; },
                  blocked: () => Busy ? null : "Nothing to skip");
        }

        static string Title(Side s) => s switch { Side.Sailor => "Sailor", Side.Captain => "Captain", _ => "Slack Water" };

        public void Deal()
        {
            if (Busy || CantStart(Bet) != null || !Stake(Bet)) return;
            stake = Bet;
            Shoe.CheckCut();
            Play();
            revealT = 0; shown = 0;
            Sfx.Play("ui");
        }

        /// <summary>Tests: deal these cards, in dealing order (Sailor, Captain, Sailor, Captain, then thirds).</summary>
        public void DealThese(params PlayingCard[] order) { Shoe.CheckCut(); Shoe.Stack(order); Deal(); }

        /// <summary>Plays the whole hand now (the cards are shown step by step), and writes down why each card was drawn.</summary>
        void Play()
        {
            Sailor.Clear(); Captain.Clear(); steps.Clear();
            Sailor.Add(Shoe.Draw()); Captain.Add(Shoe.Draw()); Sailor.Add(Shoe.Draw()); Captain.Add(Shoe.Draw());
            int s = Depth(Sailor), c = Depth(Captain);
            steps.Add(($"The Sailor's two cards: {Sum(Sailor)} → <b>{s} fathoms</b>.", 2, 0));
            steps.Add(($"The Captain's two cards: {Sum(Captain)} → <b>{c} fathoms</b>.", 2, 2));
            if (s >= 8 || c >= 8)
                steps.Add(($"<b>High Tide!</b> {(s >= 8 ? "The Sailor" : "The Captain")} has {Mathf.Max(s, c)} on two cards, so nobody draws.", 2, 2));
            else
            {
                int third = -1;
                if (s <= 5)
                {
                    Sailor.Add(Shoe.Draw());
                    third = Points(Sailor[2]);
                    steps.Add(($"The Sailor is at {s} (0–5), so casts a third line: {Sailor[2]} → <b>{Depth(Sailor)}</b>.", 3, 2));
                }
                else steps.Add(($"The Sailor is at {s} (6–7) and holds.", 2, 2));
                bool draw = third < 0 ? c <= 5 : CaptainDraws(c, third);
                string why = third < 0 ? (draw ? $"The Sailor held and the Captain is at {c} (0–5), so the Captain draws" : $"The Sailor held and the Captain is at {c} (6–7), so the Captain holds")
                                       : draw ? $"The Captain is at {c} and the Sailor's third card counts {third}: the chart says draw"
                                              : $"The Captain is at {c} and the Sailor's third card counts {third}: the chart says hold";
                int sc = Sailor.Count;
                if (draw) { Captain.Add(Shoe.Draw()); steps.Add(($"{why}: {Captain[2]} → <b>{Depth(Captain)}</b>.", sc, 3)); }
                else steps.Add(($"{why}.", sc, 2));
            }
            s = Depth(Sailor); c = Depth(Captain);
            winner = s > c ? Side.Sailor : c > s ? Side.Captain : Side.Slack;
            payout = Return(Pick, winner.Value, stake);
            string verdict = winner == Side.Slack ? $"<b>Slack Water</b>: {s} and {c}, a tie."
                           : $"Sailor {s}, Captain {c}: <b>{(winner == Side.Sailor ? "the Sailor" : "the Captain")}</b> runs deeper.";
            steps.Add((verdict, Sailor.Count, Captain.Count));
        }

        /// <summary>What a bet on `pick` returns (stake + winnings; the stake back on a push) when `winner` wins.</summary>
        public static int Return(Side pick, Side winner, int stake)
        {
            if (winner == Side.Slack) return pick == Side.Slack ? stake * 9 : stake;   // Slack Water pays 8 to 1; the others come back
            if (pick != winner) return 0;
            return pick == Side.Sailor ? stake * 2 : stake * 2 - Mathf.CeilToInt(stake * .05f);   // the harbour fee
        }

        static string Sum(List<PlayingCard> hand)
        {
            var parts = new List<string>();
            int total = 0;
            foreach (var card in hand) { parts.Add(Points(card).ToString()); total += Points(card); }
            return string.Join(" + ", parts) + $" = {total}";
        }

        public override void Tick(float dt)
        {
            if (revealT < 0) return;
            revealT += dt;
            int want = Mathf.Min(steps.Count, 1 + Mathf.FloorToInt(revealT / StepTime));
            if (want > shown) { shown = want; Sfx.Play("ui", .5f, .1f); }
            if (revealT < (steps.Count - 1) * StepTime + .6f) return;
            shown = steps.Count;
            revealT = -1;
            Pay(payout);
            if (payout > stake) Sfx.Play("bell", .5f, .2f);
        }

        protected override string Table()
        {
            var sb = new StringBuilder();
            sb.Append($"<size=13>{Felt.Dim("~  ~  ~   F A T H O M S   ~  ~  ~")}</size>\n");
            if (steps.Count == 0)
            {
                sb.Append("\n<size=22>Bet on the <color=#7dd3fc>Sailor</color>, the <color=#fca5a5>Captain</color>, or <b>Slack Water</b>.</size>\n");
                sb.Append("<size=17>The deeper hand wins: closest to <b>9 fathoms</b>. Only the last digit counts.</size>\n");
                sb.Append($"<size=15>{Felt.Dim("New to it? Open How to play: two short pages.")}</size>");
                return sb.ToString();
            }
            var last = steps[Mathf.Clamp(shown - 1, 0, steps.Count - 1)];
            int sn = last.sailorCards, cn = last.captainCards;
            string Row(string who, string colour, List<PlayingCard> hand, int n)
            {
                var visible = hand.GetRange(0, Mathf.Min(n, hand.Count));
                int d = Depth(visible);
                string depth = n > 0 ? $"<b>{d}</b> fathom{(d == 1 ? "" : "s")}" : "";
                bool won = !Busy && winner.HasValue && ((winner == Side.Sailor && who == "SAILOR") || (winner == Side.Captain && who == "CAPTAIN"));
                return $"<size=15><color={colour}>{who}</color>  {depth}{(won ? "  " + Felt.Gold("◄ deeper") : "")}</size>\n" +
                       (n > 0 ? Felt.Hand(visible, 30) : Felt.Back(30) + "  " + Felt.Back(30)) + "\n";
            }
            sb.Append(Row("SAILOR", "#7dd3fc", Sailor, sn));
            sb.Append(Row("CAPTAIN", "#fca5a5", Captain, cn));
            // The dealer's last two lines of explanation
            sb.Append("<size=15>");
            for (int i = Mathf.Max(0, shown - 2); i < shown; i++) sb.Append(i == shown - 1 ? steps[i].line : Felt.Dim(steps[i].line)).Append('\n');
            sb.Append("</size>");
            if (!Busy)
            {
                string bet = $"Your {stake} SD on {SideNames[(int)Pick]}: ";
                sb.Append($"<size=17>{bet}{(payout > stake ? Felt.Good($"you get back {payout} SD") : payout == stake ? "it comes back" : Felt.Bad("lost to the sea"))}" +
                          (payout > stake && Pick == Side.Captain ? Felt.Dim($" (after the {stake * 2 - payout} SD harbour fee)") : "") + "</size>");
            }
            return sb.ToString();
        }
    }
}

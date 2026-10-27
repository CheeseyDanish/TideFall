// TideWall 3D — casino: a three-reel slot machine. Every reel carries the same weighted strip; three of a kind pays
// from the table, and two cherries anywhere pay 5×. The reels stop left to right. Pays back about 93% over time.
using System.Text;
using UnityEngine;

namespace TideWall
{
    public class Slots : CasinoGame
    {
        public static readonly string[] Symbols = { "CHERRY", "LEMON", "PLUM", "BELL", "BAR", "7" };
        static readonly string[] Colours = { "#ff6b6b", "#fde047", "#d8b4fe", "#fbbf24", "#ffffff", "#ff4d6d" };
        /// <summary>How many of each symbol are on a reel's strip (out of 25).</summary>
        public static readonly int[] Weights = { 5, 6, 5, 4, 3, 2 };
        /// <summary>Three of a kind pays this many times the bet.</summary>
        public static readonly int[] Three = { 10, 8, 12, 18, 30, 80 };
        public const int TwoCherries = 5;
        const float SpinTime = .9f, StopGap = .45f;

        public override string Name => "Slots";
        public override string Blurb => "Pull the lever: three reels spin. Three of a kind pays; so do two cherries.";
        public override string Pays => "up to 80×";
        public override string[] Rules => new[]
        {
            "<b>SLOTS</b>\n" +
            "1  Set your bet, then <b>Spin</b>. The bet comes out of the team's purse.\n" +
            "2  The three reels stop one at a time, left to right.\n" +
            "3  <b>Three of a kind</b> on the middle line pays the table below, times your bet.\n" +
            "4  <b>Two cherries</b> anywhere on the line pay 5×.\n" +
            "5  Anything else loses the bet. Wins include your bet back (a 10× win on 10 SD returns 100 SD).\n\n" +
            $"Three 7s ×{Three[5]} · three BARs ×{Three[4]} · three BELLs ×{Three[3]} · three PLUMs ×{Three[2]}\n" +
            $"three CHERRYs ×{Three[0]} · three LEMONs ×{Three[1]} · any two CHERRYs ×{TwoCherries}\n" +
            "Over many spins the machine pays back about 93 of every 100 SD.",
        };

        public readonly int[] Reels = { 5, 5, 5 };
        float spinT = -1;
        int stake;
        string result = "Set a bet and spin.";
        public int LastWin { get; private set; }

        public override bool Busy => spinT >= 0;
        public override string RepeatLabel => Busy ? null : "spin";
        public override void Repeat() => Spin();

        public override void Actions(Menu m)
        {
            m.Add("Spin", $"{Bet} SD", "Pull the lever.", pick: Spin, blocked: () => CantStart(Bet));
        }

        public void Spin()
        {
            if (CantStart(Bet) != null || !Stake(Bet)) return;
            stake = Bet;
            for (int i = 0; i < 3; i++) Reels[i] = Roll();
            spinT = 0;
            LastWin = 0;
            result = "Spinning…";
            Sfx.Play("reload", .5f, .2f);
        }

        /// <summary>Tests: spin to these symbols.</summary>
        public void SpinTo(int a, int b, int c) { Spin(); if (Busy) { Reels[0] = a; Reels[1] = b; Reels[2] = c; } }

        static int Roll()
        {
            int total = 0; foreach (var w in Weights) total += w;
            int r = Random.Range(0, total);
            for (int i = 0; i < Weights.Length; i++) { r -= Weights[i]; if (r < 0) return i; }
            return 0;
        }

        /// <summary>What a line pays, times the bet (0 = a loss).</summary>
        public static int Payout(int a, int b, int c)
        {
            if (a == b && b == c) return Three[a];
            int cherries = (a == 0 ? 1 : 0) + (b == 0 ? 1 : 0) + (c == 0 ? 1 : 0);
            return cherries == 2 ? TwoCherries : 0;
        }

        public override void Tick(float dt)
        {
            if (spinT < 0) return;
            float before = spinT;
            spinT += dt;
            for (int i = 0; i < 3; i++)
            {
                float stop = SpinTime + i * StopGap;
                if (before < stop && spinT >= stop) Sfx.Play("ui", .8f, .05f);   // clunk
            }
            if (spinT < SpinTime + 2 * StopGap) return;
            spinT = -1;
            int mult = Payout(Reels[0], Reels[1], Reels[2]);
            LastWin = stake * mult;
            if (mult > 0)
            {
                Pay(LastWin);
                result = Reels[0] == Reels[1] && Reels[1] == Reels[2] ? $"THREE {Symbols[Reels[0]]}S! ×{mult}" : $"Two cherries ×{mult}";
                if (mult >= 30) Sfx.Play("bell", .9f, .1f);
            }
            else result = "No luck.";
        }

        protected override string Table()
        {
            var sb = new StringBuilder("<size=16>" + Felt.Dim("·  ·  ·  THE LUCKY BARNACLE  ·  ·  ·") + "</size>\n\n");
            for (int i = 0; i < 3; i++)
            {
                bool rolling = spinT >= 0 && spinT < SpinTime + i * StopGap;
                int s = rolling ? (Mathf.FloorToInt(Time.time * 16) + i * 3) % Symbols.Length : Reels[i];
                string word = Symbols[s].PadLeft((6 + Symbols[s].Length) / 2, '\u00A0').PadRight(6, '\u00A0');
                sb.Append($"<size=40><mark=#ffffff22><color={(rolling ? "#94a3b8" : Colours[s])}><b><mspace=0.62em>\u00A0{word}\u00A0</mspace></b></color></mark></size>");
                if (i < 2) sb.Append("  ");
            }
            sb.Append("\n\n");
            if (spinT >= 0) sb.Append($"<size=24>{Felt.Dim("Spinning…")}</size>");
            else if (LastWin > 0) sb.Append($"<size=28><b>{Felt.Gold(result)}</b></size>\n<size=22>{Felt.Good($"+{LastWin} SD")}</size>");
            else sb.Append($"<size=24>{result}</size>");
            sb.Append($"\n\n<size=13>{Felt.Dim($"7 7 7 ×{Three[5]} · BAR ×{Three[4]} · BELL ×{Three[3]} · PLUM ×{Three[2]} · CHERRY ×{Three[0]} · LEMON ×{Three[1]} · two cherries ×{TwoCherries}")}</size>");
            return sb.ToString();
        }
    }
}

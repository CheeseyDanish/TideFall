// TideWall 3D — casino: the Tide Machine, a three-reel slot machine with the beach on its reels (shells, starfish,
// hotdogs, anchors, golden pearls, and the Glizzy Monster for the jackpot). Each reel is a fixed strip of 25 symbols;
// a spin picks where each stops, the reels scroll and slow left to right with a little bounce, and when the first two
// land on the same symbol the third holds its breath (spins longer, its window glowing). Three of a kind pays the
// table; two shells anywhere pay 4×.
// High Tide: every losing spin raises the tide meter; at 10 the next spin is free and pays double. With the meter, the
// machine pays back about 92% over time (Slots.SimulateReturn).
// The menu's Reels (positions, lever, glow, tide) drive TableView's reel windows; Board is the result line under them.
using System;
using System.Text;
using UnityEngine;

namespace TideWall
{
    public class Slots : CasinoGame
    {
        public static readonly string[] Symbols = { "SHELL", "STARFISH", "HOTDOG", "ANCHOR", "PEARL", "GLIZZY" };
        /// <summary>The models drawn on the reels (menu icons rendered from them).</summary>
        public static readonly string[] Models = { "Shell", "Starfish", "Hotdog", "Anchor", "Pearl", "GlizzyMonster" };
        /// <summary>How many of each symbol are on a reel's strip (out of 25).</summary>
        public static readonly int[] Weights = { 5, 6, 5, 4, 3, 2 };
        /// <summary>Three of a kind pays this many times the bet.</summary>
        public static readonly int[] Three = { 8, 8, 10, 16, 25, 80 };
        public const int TwoShells = 4, TideFill = 10, TideMultiplier = 2, StripLength = 25;
        const float SpinTime = .9f, StopGap = .45f, Suspense = 1.3f;

        /// <summary>The three reel strips: each holds every symbol Weights times, in its own fixed shuffled order.</summary>
        public static readonly int[][] Strips = MakeStrips();

        static int[][] MakeStrips()
        {
            var strips = new int[3][];
            for (int r = 0; r < 3; r++)
            {
                var list = new System.Collections.Generic.List<int>();
                for (int s = 0; s < Weights.Length; s++) for (int k = 0; k < Weights[s]; k++) list.Add(s);
                var rng = new System.Random(7919 * (r + 1));
                for (int i = list.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (list[i], list[j]) = (list[j], list[i]); }
                strips[r] = list.ToArray();
            }
            return strips;
        }

        public override string Name => "Slots";
        public override string Blurb => "The Tide Machine: three reels of beach. Match three, or two shells. Losing spins raise the tide; at High Tide the next spin is free and pays double.";
        public override string Pays => "up to 80×";
        public override string Icon => "SlotMachine";
        public override string[] Rules => new[]
        {
            "<b>SLOTS · THE TIDE MACHINE</b>\n" +
            "1  Set your bet, then <b>Spin</b> (or pull with Reload). The bet comes out of the team's purse.\n" +
            "2  The reels stop left to right. When the first two match, the third holds its breath.\n" +
            "3  <b>Three of a kind</b> on the gold line pays the table on the left, times your bet.\n" +
            $"4  <b>Two shells</b> anywhere on the line pay {TwoShells}×.\n" +
            $"5  <b>High Tide</b>: every losing spin raises the tide meter. At {TideFill}, your next spin is <b>free</b>\n" +
            $"    and pays <b>×{TideMultiplier}</b>. The meter stays with your seat between visits.\n" +
            "6  Wins include your bet back (8× on 10 SD returns 80 SD).\n" +
            "With High Tide, the machine pays back about 92 of every 100 SD over time.",
        };

        /// <summary>Where each reel's strip is (symbol units; the centre line shows strip[round(pos) mod 25]).</summary>
        readonly float[] pos = { 0, 8, 16 };
        readonly float[] from = new float[3], to = new float[3], stopAt = new float[3];
        public readonly int[] Reels = { 0, 0, 0 };
        float spinT = -1, leverT = -1;
        int stake, mult = 1;
        string result = "Pull the lever.";
        public int LastWin { get; private set; }
        /// <summary>Losing spins since the last High Tide (0..TideFill); at TideFill the next spin is free and pays double.</summary>
        public int Tide { get; private set; }
        public bool HighTide => Tide >= TideFill;
        int suspenseReel = -1;

        public override bool Busy => spinT >= 0;
        public override string RepeatLabel => Busy ? null : HighTide ? "free spin" : "spin";
        public override void Repeat() => Spin();

        public override void Actions(Menu m)
        {
            m.Add(HighTide ? "Free spin (High Tide ×2)" : "Spin", HighTide ? "free" : $"{Bet} SD", HighTide ? "The tide's in: this one's on the house, and pays double." : "Pull the lever.",
                  pick: Spin, blocked: () => HighTide && !Busy ? Casino.Closed() : CantStart(Bet));
            m.Reels = Show;
        }

        public void Spin()
        {
            if (Busy) return;
            bool free = HighTide;
            if (free) { if (Casino.Closed() != null) return; stake = Bet; mult = TideMultiplier; Tide = 0; }
            else { if (CantStart(Bet) != null || !Stake(Bet)) return; stake = Bet; mult = 1; }
            var rng = new System.Random(Guid.NewGuid().GetHashCode());
            Start(rng.Next(StripLength), rng.Next(StripLength), rng.Next(StripLength));
        }

        /// <summary>Tests: spin to these symbols (a stop on each strip showing them).</summary>
        public void SpinTo(int a, int b, int c)
        {
            Spin();
            if (Busy) Start(Array.IndexOf(Strips[0], a), Array.IndexOf(Strips[1], b), Array.IndexOf(Strips[2], c));
        }

        void Start(int s0, int s1, int s2)
        {
            int[] stops = { s0, s1, s2 };
            for (int r = 0; r < 3; r++)
            {
                Reels[r] = Strips[r][stops[r]];
                from[r] = pos[r];
                // Two to four laps, then on to the stop
                to[r] = Mathf.Floor(pos[r] / StripLength) * StripLength + stops[r];
                while (to[r] - from[r] < StripLength * (2 + r)) to[r] += StripLength;
                stopAt[r] = SpinTime + r * StopGap;
            }
            // The first two match: the third reel holds its breath
            suspenseReel = Reels[0] == Reels[1] ? 2 : -1;
            if (suspenseReel == 2) { stopAt[2] += Suspense; to[2] += StripLength; }
            spinT = 0; leverT = 0;
            LastWin = 0;
            result = mult > 1 ? "HIGH TIDE! Free spin…" : "Spinning…";
            Sfx.Play("reload", .5f, .2f);
        }

        /// <summary>What a line pays, times the bet (0 = a loss).</summary>
        public static int Payout(int a, int b, int c)
        {
            if (a == b && b == c) return Three[a];
            int shells = (a == 0 ? 1 : 0) + (b == 0 ? 1 : 0) + (c == 0 ? 1 : 0);
            return shells == 2 ? TwoShells : 0;
        }

        /// <summary>The machine's long-run return per SD staked, High Tide included (simulated).</summary>
        public static double SimulateReturn(int spins, int seed)
        {
            var rng = new System.Random(seed);
            int meter = 0; bool free = false; double staked = 0, back = 0;
            for (int i = 0; i < spins; i++)
            {
                int p = Payout(Strips[0][rng.Next(StripLength)], Strips[1][rng.Next(StripLength)], Strips[2][rng.Next(StripLength)]);
                if (free) { back += p * TideMultiplier; free = false; continue; }
                staked++; back += p;
                if (p == 0 && ++meter >= TideFill) { meter = 0; free = true; }
            }
            return back / staked;
        }

        public override void Tick(float dt)
        {
            if (leverT >= 0) { leverT += dt; if (leverT > .6f) leverT = -1; }
            if (spinT < 0) return;
            float before = spinT;
            spinT += dt;
            for (int r = 0; r < 3; r++)
            {
                float u = Mathf.Clamp01(spinT / stopAt[r]);
                pos[r] = Mathf.Lerp(from[r], to[r], 1 - (1 - u) * (1 - u) * (1 - u));
                if (spinT > stopAt[r]) { float k = spinT - stopAt[r]; pos[r] = to[r] - .18f * Mathf.Sin(k * 26) * Mathf.Exp(-k * 11); }   // the bounce
                if (before < stopAt[r] && spinT >= stopAt[r]) Sfx.Play("ui", .8f, .05f);
            }
            if (spinT < stopAt[2] + .3f) return;
            for (int r = 0; r < 3; r++) pos[r] = to[r];
            spinT = -1;
            suspenseReel = -1;
            int pay = Payout(Reels[0], Reels[1], Reels[2]) * mult;
            LastWin = stake * pay;
            if (pay > 0)
            {
                Pay(LastWin);
                bool three = Reels[0] == Reels[1] && Reels[1] == Reels[2];
                result = (three ? Reels[0] == 5 ? "GLIZZY JACKPOT!" : $"THREE {Symbols[Reels[0]]}S!" : "Two shells") + $"  ×{pay}" + (mult > 1 ? " (High Tide)" : "");
                Cheer(pay >= 25 ? 1 : pay >= 8 ? .7f : .4f);
                if (pay >= 16) Sfx.Play("jackpot", .7f, .1f); else Sfx.Play("bell", .6f, .1f);
            }
            else
            {
                result = mult > 1 ? "The tide went out empty." : "No luck.";
                if (mult == 1) Tide = Mathf.Min(TideFill, Tide + 1);
                if (HighTide) { result = "HIGH TIDE! Your next spin is free and pays double."; Cheer(.35f); Sfx.Play("horn", .5f); }
            }
            mult = 1;
        }

        /// <summary>What TableView's reel windows draw: [reel 0, 1, 2 positions, lever pull 0..1, glowing reel or -1,
        /// tide 0..1, High Tide 0/1].</summary>
        float[] Show() => new[]
        {
            pos[0], pos[1], pos[2],
            leverT < 0 ? 0 : Mathf.Sin(Mathf.Clamp01(leverT / .6f) * Mathf.PI),
            spinT >= 0 && suspenseReel >= 0 && spinT > stopAt[1] ? suspenseReel : -1,
            Tide / (float)TideFill, HighTide ? 1 : 0,
        };

        protected override string Table()
        {
            var sb = new StringBuilder();
            if (spinT >= 0) sb.Append($"<size=24>{(suspenseReel >= 0 && spinT > stopAt[1] ? Felt.Gold("…") : Felt.Dim(result))}</size>");
            else if (LastWin > 0) sb.Append($"<size=28><b>{Felt.Gold(result)}</b></size>   <size=24>{Felt.Good($"+{LastWin} SD")}</size>");
            else sb.Append($"<size=22>{result}</size>");
            return sb.ToString();
        }
    }
}

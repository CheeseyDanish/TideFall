// TideWall 3D — casino: European roulette (one zero, 37 pockets). Put chips on any of the usual bets, then spin; the
// ball runs round the wheel and slows into a pocket. Single number pays 35 to 1, dozens 2 to 1, red/black, odd/even
// and low/high 1 to 1. Zero beats every bet except a bet on zero.
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace TideWall
{
    public class Roulette : CasinoGame
    {
        /// <summary>The pockets in order round a European wheel.</summary>
        public static readonly int[] Wheel = { 0, 32, 15, 19, 4, 21, 2, 25, 17, 34, 6, 27, 13, 36, 11, 30, 8, 23, 10, 5, 24, 16, 33, 1, 20, 14, 31, 9, 22, 18, 29, 7, 28, 12, 35, 3, 26 };
        static readonly HashSet<int> Reds = new() { 1, 3, 5, 7, 9, 12, 14, 16, 18, 19, 21, 23, 25, 27, 30, 32, 34, 36 };
        public static bool IsRed(int n) => Reds.Contains(n);

        public enum Kind { Number, Red, Black, Odd, Even, Low, High, Dozen1, Dozen2, Dozen3 }
        public class Chip { public Kind Kind; public int Number, Amount; }

        public override string Name => "Roulette";
        public override string Blurb => "Bet on where the ball lands: a number, a colour, odd or even, a dozen. One zero.";
        public override string Pays => "up to 35 to 1";
        public override string Icon => "RouletteWheel";
        public override string[] Rules => new[]
        {
            "<b>ROULETTE</b> (European: one green zero)\n" +
            "1  Put chips on as many bets as you like. Each chip is your current bet amount.\n" +
            "2  <b>Spin</b>: the ball lands in one of 37 pockets, 0 to 36.\n" +
            "3  <b>Single number</b> pays 35 to 1. <b>A dozen</b> (1–12, 13–24, 25–36) pays 2 to 1.\n" +
            "    <b>Red / Black</b>, <b>Odd / Even</b>, <b>1–18 / 19–36</b> pay 1 to 1.\n" +
            "4  Winning chips come back with their winnings; losing chips stay on the table for the house.\n" +
            "5  <b>Zero</b> is green: it isn't red or black, odd or even, low or high. Only a bet on 0 wins.\n" +
            "    That one pocket is the house's edge: about 2.7% of every bet over time.",
        };

        public readonly List<Chip> Chips = new();
        /// <summary>The chips on the last spin, for Re-bet & spin.</summary>
        public readonly List<Chip> LastChips = new();
        public readonly List<int> History = new();
        public int Pick = 17;
        float spinT = -1, spinLength;
        int startPos, endPos, steps;
        public int Result { get; private set; } = -1;
        int lastWin, lastStaked;

        public override bool Busy => spinT >= 0;
        public override string RepeatLabel => Busy ? null : Chips.Count > 0 ? "spin" : LastChips.Count > 0 ? "re-bet & spin" : null;
        /// <summary>Spin what's on the table; with an empty table, put the last spin's chips back first.</summary>
        public override void Repeat()
        {
            if (Busy || Casino.Closed() != null) return;
            if (Chips.Count == 0)
                foreach (var c in LastChips)
                {
                    if (Casino.Gm.SandDollars < c.Amount || !Stake(c.Amount)) break;
                    Chips.Add(new Chip { Kind = c.Kind, Number = c.Number, Amount = c.Amount });
                }
            Spin();
        }
        int Staked { get { int n = 0; foreach (var c in Chips) n += c.Amount; return n; } }

        public static string Label(Kind k, int n) => k switch
        {
            Kind.Number => $"Number {n}", Kind.Red => "Red", Kind.Black => "Black", Kind.Odd => "Odd", Kind.Even => "Even",
            Kind.Low => "1–18", Kind.High => "19–36", Kind.Dozen1 => "1st dozen (1–12)", Kind.Dozen2 => "2nd dozen (13–24)", _ => "3rd dozen (25–36)",
        };

        /// <summary>What a chip returns (stake + winnings) when the ball lands on n; 0 if it loses.</summary>
        public static int Return(Chip c, int n)
        {
            bool win = c.Kind switch
            {
                Kind.Number => n == c.Number,
                Kind.Red => n != 0 && IsRed(n), Kind.Black => n != 0 && !IsRed(n),
                Kind.Odd => n != 0 && n % 2 == 1, Kind.Even => n != 0 && n % 2 == 0,
                Kind.Low => n is >= 1 and <= 18, Kind.High => n >= 19,
                Kind.Dozen1 => n is >= 1 and <= 12, Kind.Dozen2 => n is >= 13 and <= 24, _ => n >= 25,
            };
            if (!win) return 0;
            int odds = c.Kind == Kind.Number ? 35 : c.Kind is Kind.Dozen1 or Kind.Dozen2 or Kind.Dozen3 ? 2 : 1;
            return c.Amount * (odds + 1);
        }

        public override void Actions(Menu m)
        {
            bool rebet = Chips.Count == 0 && LastChips.Count > 0;
            int last = 0; foreach (var c in LastChips) last += c.Amount;
            m.Add(rebet ? "Re-bet & spin" : "Spin", Chips.Count > 0 ? $"{Staked} SD on the table" : rebet ? $"{last} SD, same chips" : null,
                  rebet ? "Put the last spin's chips back down and spin." : "Spin the wheel.", pick: Repeat,
                  blocked: () => Busy ? "The ball is rolling" : Chips.Count == 0 && LastChips.Count == 0 ? "Put some chips down first" : Casino.Closed());
            var num = m.Add("Single number", $"< {Pick} >  35 to 1", "Left/right choose the number (0–36), then pick to put a chip on it.");
            num.OnAdjust = d => { Pick = (Pick + d + 37) % 37; num.Right = $"< {Pick} >  35 to 1"; };
            num.OnPick = () => Place(Kind.Number, Pick);
            num.Blocked = () => CantPlace();
            foreach (var k in new[] { Kind.Red, Kind.Black, Kind.Odd, Kind.Even, Kind.Low, Kind.High, Kind.Dozen1, Kind.Dozen2, Kind.Dozen3 })
            {
                var kind = k;
                m.Add(Label(kind, 0), kind is Kind.Dozen1 or Kind.Dozen2 or Kind.Dozen3 ? "2 to 1" : "1 to 1", $"Put a {Bet} SD chip on {Label(kind, 0)}.",
                      pick: () => Place(kind, 0), blocked: CantPlace);
            }
            m.Add("Take chips back", Chips.Count > 0 ? $"{Staked} SD" : null, "Clear the table before the spin.", pick: Clear,
                  blocked: () => Busy ? "The ball is rolling" : Chips.Count == 0 ? "No chips down" : null);
        }

        string CantPlace() => Busy ? "The ball is rolling" : Casino.Closed() ?? (Casino.Gm.SandDollars < Bet ? "The team doesn't have enough sand dollars" : null);

        public void Place(Kind k, int number)
        {
            if (CantPlace() != null || !Stake(Bet)) return;
            var same = Chips.Find(c => c.Kind == k && c.Number == number);
            if (same != null) same.Amount += Bet; else Chips.Add(new Chip { Kind = k, Number = number, Amount = Bet });
        }

        void Clear()
        {
            if (Busy) return;
            int back = Staked;
            Chips.Clear();
            if (back > 0) { Casino.Pay(back, Seat, Name); }
        }

        public void Spin() => SpinTo(Random.Range(0, 37));

        /// <summary>Spin, landing on n (tests; Spin picks n at random).</summary>
        public void SpinTo(int n)
        {
            if (Busy || Chips.Count == 0 || Casino.Closed() != null) return;
            startPos = Result >= 0 ? System.Array.IndexOf(Wheel, Result) : 0;
            endPos = System.Array.IndexOf(Wheel, n);
            steps = 37 * 3 + ((endPos - startPos) % 37 + 37) % 37;   // three laps, then on to the pocket
            spinLength = 4f;
            spinT = 0;
            Result = n;
            Sfx.Play("reload", .5f, .1f);
        }

        int Position => spinT < 0 ? System.Array.IndexOf(Wheel, Mathf.Max(0, Result)) : (startPos + Mathf.FloorToInt(steps * Ease(spinT / spinLength))) % 37;
        static float Ease(float t) { t = Mathf.Clamp01(t); return 1 - (1 - t) * (1 - t) * (1 - t); }

        public override void Tick(float dt)
        {
            if (spinT < 0) return;
            int before = Position;
            spinT += dt;
            if (Position != before && Random.value < .5f) Sfx.Play("ui", .25f, .3f);
            if (spinT < spinLength) return;
            spinT = -1;
            lastStaked = Staked;
            lastWin = 0;
            foreach (var c in Chips) lastWin += Return(c, Result);
            LastChips.Clear(); LastChips.AddRange(Chips);
            Pay(lastWin);
            if (lastWin > lastStaked) { Sfx.Play("bell", .5f, .2f); Cheer(lastWin >= lastStaked * 10 ? 1 : .5f); }
            History.Insert(0, Result);
            if (History.Count > 12) History.RemoveAt(History.Count - 1);
            Chips.Clear();
        }

        static string Num(int n, bool big) { string s = n.ToString().PadLeft(2, '\u00A0'); return big ? "\u00A0" + s + "\u00A0" : s; }

        static string Pocket(int n, int size, bool big = false)
        {
            string ink = n == 0 ? "#4ade80" : IsRed(n) ? "#ff6b6b" : "#ffffff";
            string wash = n == 0 ? "#22c55e33" : IsRed(n) ? "#ef444433" : "#00000066";
            return $"<size={size}><mark={wash}><color={ink}><b><mspace=0.62em>{Num(n, big)}</mspace></b></color></mark></size>";
        }

        protected override string Table()
        {
            var sb = new StringBuilder();
            // The wheel, as a strip of pockets running past the pointer
            int pos = Position;
            for (int i = -4; i <= 4; i++)
            {
                int n = Wheel[(pos + i + 37) % 37];
                sb.Append(i == 0 ? $" {Pocket(n, 44, true)} " : Pocket(n, 24 - Mathf.Abs(i) * 2) + " ");
            }
            sb.Append($"\n<size=18>{Felt.Gold("▲")}</size>\n");
            if (spinT >= 0) sb.Append($"<size=20>{Felt.Dim("The ball is rolling…")}</size>\n");
            else if (Result >= 0 && lastStaked > 0)
            {
                string colour = Result == 0 ? "green" : IsRed(Result) ? "red" : "black";
                sb.Append($"<size=20><b>{Result} {colour}</b> · " + (lastWin > 0 ? Felt.Good($"you get back {lastWin} SD") : Felt.Bad($"the house takes {lastStaked} SD")) + "</size>\n");
            }
            else sb.Append("<size=20>Put chips down, then spin.</size>\n");
            // The chips on the table
            if (Chips.Count > 0)
            {
                var parts = new List<string>();
                foreach (var c in Chips) parts.Add($"{Label(c.Kind, c.Number)} {Felt.Gold(c.Amount + " SD")}");
                sb.Append($"<size=15>On the table: {string.Join(" · ", parts)}</size>\n");
            }
            if (History.Count > 0)
            {
                sb.Append($"<size=13>{Felt.Dim("Last spins")} ");
                foreach (var h in History) sb.Append(Pocket(h, 13) + " ");
                sb.Append("</size>");
            }
            return sb.ToString();
        }
    }
}

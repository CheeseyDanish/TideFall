// TideWall 3D — the casino's cards: a playing card, a shoe of shuffled decks, and how cards look on the felt
// (white tiles with the rank and suit; red for hearts and diamonds; face down in navy).
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace TideWall
{
    public readonly struct PlayingCard
    {
        /// <summary>1 = ace … 11 = jack, 12 = queen, 13 = king.</summary>
        public readonly int Rank;
        /// <summary>0 spades, 1 hearts, 2 diamonds, 3 clubs.</summary>
        public readonly int Suit;
        public PlayingCard(int rank, int suit) { Rank = rank; Suit = suit; }

        public bool Red => Suit is 1 or 2;
        public string RankName => Rank switch { 1 => "A", 11 => "J", 12 => "Q", 13 => "K", _ => Rank.ToString() };
        public string SuitSymbol => Suit switch { 0 => "♠", 1 => "♥", 2 => "♦", _ => "♣" };
        public override string ToString() => RankName + SuitSymbol;
    }

    /// <summary>Cards dealt from `decks` shuffled decks, reshuffled when the shoe runs low.</summary>
    public class Shoe
    {
        readonly int decks;
        readonly List<PlayingCard> cards = new();
        readonly System.Random rng;

        public Shoe(int decks, int seed = 0)
        {
            this.decks = Mathf.Max(1, decks);
            rng = seed != 0 ? new System.Random(seed) : new System.Random();
            Shuffle();
        }

        public int Left => cards.Count;

        public void Shuffle()
        {
            cards.Clear();
            for (int d = 0; d < decks; d++)
                for (int s = 0; s < 4; s++)
                    for (int r = 1; r <= 13; r++) cards.Add(new PlayingCard(r, s));
            for (int i = cards.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (cards[i], cards[j]) = (cards[j], cards[i]); }
        }

        /// <summary>A fresh shuffle once fewer than a quarter of the cards are left (before a hand, never during one).</summary>
        public void CheckCut() { if (cards.Count < decks * 13) Shuffle(); }

        public PlayingCard Draw()
        {
            if (cards.Count == 0) Shuffle();
            var c = cards[^1];
            cards.RemoveAt(cards.Count - 1);
            return c;
        }

        /// <summary>Tests: put these cards on top, to be drawn first in this order.</summary>
        public void Stack(params PlayingCard[] top) { for (int i = top.Length - 1; i >= 0; i--) cards.Add(top[i]); }
    }

    public static class Felt
    {
        /// <summary>A card as a pale tile (all the same width). TextMeshPro draws &lt;mark&gt; over the text, so the tile is a light wash.</summary>
        public static string Card(PlayingCard c, int size = 40, bool lit = false)
        {
            string ink = c.Red ? "#ff7a7a" : "#ffffff";
            return $"<size={size}><mark={(lit ? "#fde68a55" : "#ffffff22")}><color={ink}><b><mspace=0.62em>\u00A0{c.RankName.PadLeft(2, '\u00A0')}{c.SuitSymbol}\u00A0</mspace></b></color></mark></size>";
        }

        /// <summary>A card lying face down.</summary>
        public static string Back(int size = 40) => $"<size={size}><mark=#1e3a8a88><color=#93c5fd><mspace=0.62em>\u00A0~~~\u00A0</mspace></color></mark></size>";

        public static string Hand(IList<PlayingCard> cards, int size = 40, int hideFrom = int.MaxValue)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < cards.Count; i++) { if (i > 0) sb.Append("  "); sb.Append(i >= hideFrom ? Back(size) : Card(cards[i], size)); }
            return sb.ToString();
        }

        public static string Gold(string s) => $"<color=#fde68a>{s}</color>";
        public static string Good(string s) => $"<color=#86efac>{s}</color>";
        public static string Bad(string s) => $"<color=#fca5a5>{s}</color>";
        public static string Dim(string s) => $"<color=#94a3b8>{s}</color>";
    }
}

// TideWall 3D — the symbols of the hidden story (docs/STORY.md), drawn as white shapes on transparent textures.
// The editor setup saves them as PNGs in Assets/Content/Glyphs (repaint those freely; the game uses the PNGs); this
// code is how they were made, and the fallback if a PNG is missing.
//
// The Crest and the Mark are the two halves of the Spiral, drawn from the same curve: overlay them and they are the
// Spiral exactly. That is the whole hidden story in one picture, so keep them matched if you redraw them.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace TideWall
{
    public static class GlyphArt
    {
        public const int Size = 256;
        /// <summary>Every glyph, by name.</summary>
        public static readonly string[] Names =
            { "spiral", "crest", "mark", "petals", "tally", "hand", "eye", "heart", "wave", "figure", "keep", "boat" };

        // The spiral: r = a + b·θ over 2.5 turns. The Crest is the outer part, the Mark the inner part.
        const float SpiralA = .04f, SpiralB = .0262f, SpiralTurns = 2.5f, SpiralSplit = 1.25f;

        /// <summary>The glyph's alpha, Size × Size, row by row from the bottom (Texture2D order).</summary>
        public static float[] Draw(string name)
        {
            var shapes = new List<Shape>();
            switch (name)
            {
                case "spiral": shapes.Add(Spiral(0, SpiralTurns, .042f)); break;
                case "crest": shapes.Add(Spiral(SpiralSplit, SpiralTurns, .042f)); break;
                case "mark": shapes.Add(Spiral(0, SpiralSplit, .042f)); break;
                case "petals":
                    for (int i = 0; i < 5; i++)
                    {
                        float a = Mathf.PI / 2 + i * Mathf.PI * 2 / 5;
                        var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                        shapes.Add(Ellipse(dir * .2f, dir, .17f, .06f, fill: false, w: .03f));
                    }
                    shapes.Add(Ring(Vector2.zero, .43f, .025f));
                    shapes.Add(Disc(Vector2.zero, .04f));
                    break;
                case "tally":
                    for (int i = 0; i < 4; i++) shapes.Add(Line(new Vector2(-.27f + i * .18f, -.36f), new Vector2(-.25f + i * .18f, .36f), .035f));
                    shapes.Add(Line(new Vector2(-.42f, -.26f), new Vector2(.42f, .24f), .035f));
                    break;
                case "hand":
                    shapes.Add(Ellipse(new Vector2(0, -.16f), Vector2.up, .2f, .2f, fill: true));
                    for (int i = 0; i < 4; i++)
                    {
                        float x = -.15f + i * .1f;
                        shapes.Add(Line(new Vector2(x, -.05f), new Vector2(x * 1.15f, .3f + (i == 1 || i == 2 ? .06f : 0)), .045f));
                    }
                    shapes.Add(Line(new Vector2(-.17f, -.15f), new Vector2(-.36f, .05f), .045f));
                    break;
                case "eye":
                    shapes.Add(Arc(new Vector2(0, -.35f), .55f, 50, 130, .03f));
                    shapes.Add(Arc(new Vector2(0, .35f), .55f, 230, 310, .03f));
                    shapes.Add(Disc(Vector2.zero, .11f));
                    break;
                case "heart":
                    shapes.Add(Ring(Vector2.zero, .2f, .035f));
                    shapes.Add(Disc(Vector2.zero, .07f));
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i * Mathf.PI / 4;
                        var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                        shapes.Add(Line(d * .28f, d * (i % 2 == 0 ? .44f : .37f), .03f));
                    }
                    break;
                case "wave":
                    for (int row = 0; row < 2; row++)
                    {
                        var pts = new List<Vector2>();
                        for (int i = 0; i <= 40; i++) { float x = -.42f + i * .84f / 40; pts.Add(new Vector2(x, -.08f + row * .2f + Mathf.Sin(x * 9) * .08f)); }
                        shapes.Add(Poly(pts, .03f));
                    }
                    break;
                case "figure":
                    shapes.Add(Disc(new Vector2(0, .3f), .08f));
                    shapes.Add(Line(new Vector2(0, .2f), new Vector2(0, -.12f), .04f));
                    shapes.Add(Line(new Vector2(0, -.12f), new Vector2(-.14f, -.42f), .04f));
                    shapes.Add(Line(new Vector2(0, -.12f), new Vector2(.14f, -.42f), .04f));
                    shapes.Add(Line(new Vector2(-.2f, .1f), new Vector2(.2f, .1f), .04f));
                    break;
                case "keep":
                    shapes.Add(Poly(new List<Vector2> { new(-.3f, -.42f), new(-.3f, .2f), new(-.18f, .2f), new(-.18f, .3f), new(-.06f, .3f), new(-.06f, .2f),
                        new(.06f, .2f), new(.06f, .3f), new(.18f, .3f), new(.18f, .2f), new(.3f, .2f), new(.3f, -.42f), new(-.3f, -.42f) }, .03f));
                    shapes.Add(Ellipse(new Vector2(0, -.02f), Vector2.up, .1f, .07f, fill: false, w: .025f));   // the window
                    break;
                case "boat":
                    shapes.Add(Poly(new List<Vector2> { new(-.4f, -.15f), new(-.28f, -.33f), new(.28f, -.33f), new(.4f, -.15f), new(-.4f, -.15f) }, .03f));
                    shapes.Add(Line(new Vector2(0, -.15f), new Vector2(0, .4f), .03f));
                    shapes.Add(Poly(new List<Vector2> { new(.02f, .38f), new(.28f, -.08f), new(.02f, -.08f) }, .025f));
                    break;
                default: throw new ArgumentException($"no glyph called {name}");
            }

            var alpha = new float[Size * Size];
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    var p = new Vector2((x + .5f) / Size - .5f, (y + .5f) / Size - .5f);
                    float a = 0;
                    foreach (var s in shapes) a = Mathf.Max(a, s(p));
                    alpha[y * Size + x] = a;
                }
            return alpha;
        }

        /// <summary>A white texture with this glyph as its alpha.</summary>
        public static Texture2D Texture(string name)
        {
            var a = Draw(name);
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, true) { name = name, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[a.Length];
            for (int i = 0; i < a.Length; i++) px[i] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a[i] * 255));
            tex.SetPixels32(px); tex.Apply(true);
            return tex;
        }

        // ---- shapes: each gives alpha (0..1) at a point; the glyph spans -0.5..0.5 ----
        delegate float Shape(Vector2 p);
        const float Soft = 1.5f / Size;   // anti-aliasing edge
        static float Edge(float d, float halfWidth) => Mathf.Clamp01((halfWidth - d) / Soft + .5f);

        static Shape Disc(Vector2 c, float r) => p => Edge((p - c).magnitude, r);
        static Shape Ring(Vector2 c, float r, float w) => p => Edge(Mathf.Abs((p - c).magnitude - r), w / 2);
        static Shape Line(Vector2 a, Vector2 b, float w) => p => Edge(SegDist(p, a, b), w / 2);

        static Shape Poly(List<Vector2> pts, float w) => p =>
        {
            float d = float.MaxValue;
            for (int i = 1; i < pts.Count; i++) d = Mathf.Min(d, SegDist(p, pts[i - 1], pts[i]));
            return Edge(d, w / 2);
        };

        static Shape Arc(Vector2 c, float r, float fromDeg, float toDeg, float w)
        {
            var pts = new List<Vector2>();
            for (int i = 0; i <= 32; i++) { float a = Mathf.Lerp(fromDeg, toDeg, i / 32f) * Mathf.Deg2Rad; pts.Add(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r); }
            return Poly(pts, w);
        }

        static Shape Ellipse(Vector2 c, Vector2 axis, float along, float across, bool fill, float w = .03f)
        {
            axis.Normalize();
            var side = new Vector2(-axis.y, axis.x);
            if (fill) return p => { var d = p - c; float u = Vector2.Dot(d, axis) / along, v = Vector2.Dot(d, side) / across; return Mathf.Clamp01((1 - Mathf.Sqrt(u * u + v * v)) * Mathf.Min(along, across) / Soft + .5f); };
            var pts = new List<Vector2>();
            for (int i = 0; i <= 48; i++) { float a = i * Mathf.PI * 2 / 48; pts.Add(c + axis * Mathf.Cos(a) * along + side * Mathf.Sin(a) * across); }
            return Poly(pts, w);
        }

        static Shape Spiral(float fromTurn, float toTurn, float w)
        {
            var pts = new List<Vector2>();
            int n = Mathf.CeilToInt((toTurn - fromTurn) * 90);
            for (int i = 0; i <= n; i++)
            {
                float th = Mathf.Lerp(fromTurn, toTurn, i / (float)n) * Mathf.PI * 2;
                float r = SpiralA + SpiralB * th;
                pts.Add(new Vector2(Mathf.Cos(th), Mathf.Sin(th)) * r);
            }
            return Poly(pts, w);
        }

        static float SegDist(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = ab.sqrMagnitude > 0 ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0;
            return (p - (a + ab * t)).magnitude;
        }
    }
}

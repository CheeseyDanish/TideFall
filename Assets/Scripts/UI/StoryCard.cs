// TideWall 3D — the story's cards: a part's opening words, each stage's title, and the endings. A full-screen overlay
// (over every split-screen view) that plays a few slides: a symbol, a kicker ("PART II"), a title, a line. Any key or
// button moves on. The Spiral ending plays the one animation the hidden story is built around: the Crest and the
// Mark slide together and become the Spiral (docs/STORY.md).
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TideWall
{
    public class StoryCard : MonoBehaviour
    {
        class Slide
        {
            public string glyph, kicker, title, body;
            public float seconds = 3.5f;
            public bool merge;          // the Crest and the Mark become the Spiral
            public Color glyphColor = new(.95f, .88f, .7f);
        }

        static StoryCard current;
        public static bool Showing => current != null;
        /// <summary>Tests and the screenshot tour turn the cards off.</summary>
        public static bool Disabled;

        readonly Queue<Slide> slides = new();
        Slide slide;
        Action done;
        float t, darkness;
        bool finishing;
        Image shade, glyph, half1, half2;
        TMP_Text kicker, title, body;

        static StoryCard Make(float darkness)
        {
            if (current != null) Destroy(current.gameObject);
            var canvas = Ui.CanvasFor(null, "Story card", 300);
            var c = canvas.gameObject.AddComponent<StoryCard>();
            c.darkness = darkness;
            var root = canvas.transform;
            c.shade = Ui.Fill(root, "Shade", new Color(0, 0, 0, 0));
            c.glyph = Ui.Image(root, "Symbol", new Vector2(.5f, .5f), new Vector2(0, 120), new Vector2(150, 150), new Color(1, 1, 1, 0));
            c.half1 = Ui.Image(root, "Crest", new Vector2(.5f, .5f), new Vector2(0, 120), new Vector2(150, 150), new Color(1, 1, 1, 0));
            c.half2 = Ui.Image(root, "Mark", new Vector2(.5f, .5f), new Vector2(0, 120), new Vector2(150, 150), new Color(1, 1, 1, 0));
            c.half1.sprite = Glyphs.Sprite("crest"); c.half2.sprite = Glyphs.Sprite("mark");
            c.kicker = Ui.Text(root, "Kicker", new Vector2(.5f, .5f), new Vector2(0, 20), new Vector2(900, 30), 18, TextAnchor.MiddleCenter, new Color(.75f, .85f, .95f, 0));
            c.kicker.characterSpacing = 18;
            c.title = Ui.Text(root, "Title", new Vector2(.5f, .5f), new Vector2(0, -26), new Vector2(1100, 70), 48, TextAnchor.MiddleCenter, new Color(1, .95f, .85f, 0));
            c.body = Ui.Text(root, "Body", new Vector2(.5f, .5f), new Vector2(0, -96), new Vector2(900, 80), 21, TextAnchor.UpperCenter, new Color(.9f, .92f, 1, 0));
            current = c;
            return c;
        }

        /// <summary>A Part II stage begins: the part's opening words first (its first stage only), then the stage's title.
        /// Returns how long it lasts.</summary>
        public static float Stage(PartDef part, int stageIndex)
        {
            if (Disabled || part == null || stageIndex < 0 || stageIndex >= part.stages.Count) return 0;
            var c = Make(.82f);
            if (stageIndex == 0) c.AddPart(part);
            var st = part.stages[stageIndex];
            c.slides.Enqueue(new Slide { glyph = StageGlyph(stageIndex), kicker = $"PART {part.numeral} · {stageIndex + 1} OF {part.stages.Count}", title = st.title, body = st.epigraph, seconds = 4 });
            return c.Total();
        }

        /// <summary>Put the card away now (the screenshot tour).</summary>
        public static void Close() { if (current != null) current.Dismiss(); }

        /// <summary>A part with no stages begins (Part I): its opening words.</summary>
        public static float Part(PartDef part)
        {
            if (Disabled || part == null) return 0;
            var c = Make(.82f);
            c.AddPart(part);
            return c.Total();
        }

        /// <summary>The Heart was answered.</summary>
        public static void Ending(EndingDef ending, Action whenDone)
        {
            if (ending == null || Disabled) { whenDone?.Invoke(); return; }
            var c = Make(1);
            c.done = whenDone;
            bool spiral = ending.id == "spiral";
            c.slides.Enqueue(new Slide { glyph = spiral ? "spiral" : "tally", merge = spiral, title = ending.title, seconds = spiral ? 6 : 4.5f, glyphColor = spiral ? new Color(.7f, 1f, .85f) : new Color(.95f, .88f, .7f) });
            foreach (var line in ending.lines) c.slides.Enqueue(new Slide { glyph = spiral ? "spiral" : "tally", body = line, seconds = 4.2f, glyphColor = spiral ? new Color(.7f, 1f, .85f) : new Color(.95f, .88f, .7f) });
        }

        void AddPart(PartDef part)
        {
            slides.Enqueue(new Slide { glyph = part.kind == PartKind.Defense ? "crest" : "wave", kicker = $"PART {part.numeral}", title = part.title, body = part.subtitle, seconds = 4 });
            foreach (var line in part.intro) slides.Enqueue(new Slide { glyph = part.kind == PartKind.Defense ? "crest" : "wave", body = line, seconds = 4.2f });
        }

        float Total() { float s = 0; foreach (var x in slides) s += x.seconds; return s; }

        /// <summary>The symbol under each stage's title (docs/STORY.md: what each stage shows).</summary>
        static string StageGlyph(int stage) => stage switch { 0 => "eye", 1 => "hand", 2 => "tally", 3 => "keep", 4 => "heart", _ => "spiral" };

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (slide == null)
            {
                if (slides.Count == 0) { Dismiss(); return; }
                slide = slides.Dequeue();
                t = 0;
                glyph.sprite = slide.glyph != null ? Glyphs.Sprite(slide.glyph) : null;
                kicker.text = slide.kicker ?? ""; title.text = slide.title ?? ""; body.text = slide.body ?? "";
            }
            t += dt;
            if (t > .6f && Pressed()) t = Mathf.Max(t, slide.seconds - .5f);   // move on
            // Fade each slide in and out; the shade stays while slides remain
            float a = Mathf.Clamp01(t / .6f) * Mathf.Clamp01((slide.seconds - t) / .5f);
            float shadeA = darkness * (slides.Count > 0 || t < slide.seconds - .5f ? Mathf.Clamp01((t + (finishing ? 0 : 1)) / .6f) : Mathf.Clamp01((slide.seconds - t) / .5f));
            shade.color = new Color(0, 0, 0, shadeA);
            var gc = slide.glyphColor;
            if (slide.merge)
            {
                // The two halves come together from either side, then the whole Spiral glows
                float k = Mathf.SmoothStep(0, 1, Mathf.Clamp01((t - .3f) / 3f));
                half1.rectTransform.anchoredPosition = new Vector2(-180 * (1 - k), 120);
                half2.rectTransform.anchoredPosition = new Vector2(180 * (1 - k), 120);
                float join = Mathf.Clamp01((t - 3.2f) / .8f);
                half1.color = new Color(gc.r, gc.g, gc.b, a * (1 - join));
                half2.color = new Color(gc.r, gc.g, gc.b, a * (1 - join));
                glyph.color = new Color(gc.r, gc.g, gc.b, a * join);
            }
            else
            {
                half1.color = half2.color = Color.clear;
                glyph.color = slide.glyph != null ? new Color(gc.r, gc.g, gc.b, a * .9f) : Color.clear;
            }
            Fade(kicker, a); Fade(title, a); Fade(body, a);
            if (t >= slide.seconds) { slide = null; finishing = slides.Count == 0; }
        }

        static void Fade(TMP_Text x, float a) { var c = x.color; c.a = a; x.color = c; }

        static bool Pressed()
        {
            if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) return true;
            foreach (var g in Gamepad.all) if (g.buttonSouth.wasPressedThisFrame || g.startButton.wasPressedThisFrame) return true;
            return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        }

        void Dismiss()
        {
            var d = done; done = null;
            if (current == this) current = null;
            Destroy(gameObject);
            d?.Invoke();
        }
    }
}

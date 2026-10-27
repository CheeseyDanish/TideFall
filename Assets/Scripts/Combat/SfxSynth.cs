// TideWall 3D — the sound effects as formulas (noise, sine sweeps, envelopes), like the 2D version. The editor bakes
// them into WAV files (TideWall → Bake Sounds, part of setup: Assets/Content/Sounds), which the game plays. Edit a
// formula and re-bake, or replace a WAV with a recorded sound of the same name.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace TideWall
{
    public static class SfxSynth
    {
        public const int Rate = 22050;

        /// <summary>Every sound's samples (mono, Rate Hz, -1..1). Seeded, so baking twice gives identical files.</summary>
        public static Dictionary<string, float[]> All()
        {
            var all = new Dictionary<string, float[]>();
            var rng = new System.Random(1);
            float Env(float t, float d, float a = .004f) => t < a ? t / a : Mathf.Exp(-(t - a) / d);
            void Make(string name, float seconds, Func<float, float> f)
            {
                int n = Mathf.CeilToInt(seconds * Rate);
                var data = new float[n];
                for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(f(i / (float)Rate), -1, 1);
                all[name] = data;
            }
            float Noise() => (float)rng.NextDouble() * 2 - 1;
            Make("shot", .18f, t => Noise() * Env(t, .04f) * .8f + Mathf.Sin(t * 2 * Mathf.PI * 90) * Env(t, .05f) * .6f);
            Make("shotgun", .35f, t => Noise() * Env(t, .09f) + Mathf.Sin(t * 2 * Mathf.PI * 60) * Env(t, .1f) * .7f);
            Make("harpoon", .4f, t => Noise() * Env(t, .05f) * .4f + Mathf.Sin(t * 2 * Mathf.PI * (300 - t * 500)) * Env(t, .15f) * .6f);
            Make("flare", .5f, t => Noise() * Env(t, .25f) * .5f * Mathf.Sin(t * 40));
            Make("swing", .22f, t => Noise() * Mathf.Sin(Mathf.PI * t / .22f) * .5f);
            Make("hit", .08f, t => Noise() * Env(t, .015f));
            Make("sever", .3f, t => (Noise() * .6f + Mathf.Sin(t * 2 * Mathf.PI * (220 - t * 400))) * Env(t, .08f) * .7f);
            Make("boom", 1.1f, t => (Noise() * .8f + Mathf.Sin(t * 2 * Mathf.PI * 45)) * Env(t, .3f, .01f));
            Make("coin", .25f, t => Mathf.Sin(t * 2 * Mathf.PI * (t < .07f ? 1320 : 1980)) * Env(t, .1f) * .4f);
            Make("bell", 1.6f, t => (Mathf.Sin(t * 2 * Mathf.PI * 660) + .5f * Mathf.Sin(t * 2 * Mathf.PI * 1650) + .3f * Mathf.Sin(t * 2 * Mathf.PI * 2640)) * Env(t, .5f) * .35f);
            Make("splash", .5f, t => Noise() * Env(t, .15f) * .5f);
            Make("bite", .2f, t => Noise() * Env(t, .03f) + (t > .08f ? Noise() * Env(t - .08f, .03f) : 0));
            Make("eat", .45f, t => Noise() * Mathf.Abs(Mathf.Sin(t * 30)) * Env(t, .2f) * .5f);
            Make("zap", .25f, t => (Mathf.Repeat(t * (900 + Noise() * 300), 1) * 2 - 1) * Env(t, .08f) * .35f);
            Make("hurt", .25f, t => Mathf.Sign(Mathf.Sin(t * 2 * Mathf.PI * (180 - t * 300))) * Env(t, .08f) * .3f);
            Make("reload", .12f, t => Noise() * Env(t, .01f) + (t > .07f ? Noise() * Env(t - .07f, .01f) : 0));
            Make("ui", .06f, t => Mathf.Sin(t * 2 * Mathf.PI * 900) * Env(t, .02f) * .3f);
            Make("deny", .2f, t => Mathf.Sign(Mathf.Sin(t * 2 * Mathf.PI * 160)) * Env(t, .08f) * .2f);
            Make("horn", 1.4f, t => (Mathf.Repeat(t * 98, 1) * 2 - 1) * Mathf.Min(1, t * 6) * Mathf.Exp(-t * 1.2f) * .3f);
            Make("jackpot", 1.2f, t => Mathf.Sin(t * 2 * Mathf.PI * (523 * Mathf.Pow(1.26f, Mathf.Floor(t * 8) % 4))) * .35f * Mathf.Exp(-t));
            Make("kill", .22f, t => (Mathf.Sin(t * 2 * Mathf.PI * (160 - t * 300)) * 1.2f + Noise() * .3f) * Env(t, .06f, .002f) * .6f);
            // Reload minigames and combos
            Make("tick", .03f, t => Mathf.Sin(t * 2 * Mathf.PI * 1800) * Env(t, .01f) * .25f);
            Make("load", .07f, t => (Noise() * .5f + Mathf.Sin(t * 2 * Mathf.PI * 700)) * Env(t, .02f) * .5f);
            Make("jam", .25f, t => (Mathf.Sign(Mathf.Sin(t * 2 * Mathf.PI * 110)) * .6f + Noise() * .4f) * Env(t, .1f) * .35f);
            Make("perfect", .35f, t => Mathf.Sin(t * 2 * Mathf.PI * (t < .1f ? 880 : 1320)) * Env(t, .15f) * .35f);
            Make("combo", .18f, t => Mathf.Sin(t * 2 * Mathf.PI * (600 + t * 2400)) * Env(t, .08f) * .3f);
            Make("gulp", .3f, t => Mathf.Sin(t * 2 * Mathf.PI * (300 + 600 * t)) * Env(t, .1f) * .4f);

            // Surf: smoothed noise swelling every few seconds. Loops seamlessly (the swell is a whole number of cycles).
            float smooth = 0;
            Make("surf", 8f, t =>
            {
                smooth = Mathf.Lerp(smooth, Noise(), .08f);
                float swell = .35f + .65f * Mathf.Pow(.5f + .5f * Mathf.Sin(t * 2 * Mathf.PI / 4f), 2);
                return smooth * swell * 1.6f;
            });
            return all;
        }
    }
}

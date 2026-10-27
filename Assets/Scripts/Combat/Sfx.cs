// TideWall 3D — sound effects: the WAVs in Assets/Content/Sounds (baked from SfxSynth's formulas).
// Two volume groups (Settings: effects, interface) under the master volume, plus a looping surf ambience.
// On an online host, gameplay sounds are also sent to the clients.
using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace TideWall
{
    public static class Sfx
    {
        static AudioSource src, ambience;
        static readonly HashSet<string> Interface = new() { "ui", "deny", "coin", "bell", "jackpot" };
        static readonly Dictionary<string, AudioClip> clips = new();
        static readonly Dictionary<string, float> last = new();
        public static bool Muted;

        public static void Init(GameObject host)
        {
            src = host.AddComponent<AudioSource>();
            src.spatialBlend = 0; src.playOnAwake = false;
            // Baked WAVs from the content library; the formulas directly if a library is missing (fresh checkout, tests)
            foreach (var kv in Content.Sounds) clips[kv.Key] = kv.Value;
            if (!clips.ContainsKey("surf"))
                foreach (var kv in SfxSynth.All())
                {
                    var clip = AudioClip.Create(kv.Key, kv.Value.Length, 1, SfxSynth.Rate, false);
                    clip.SetData(kv.Value, 0);
                    clips[kv.Key] = clip;
                }
            ambience = host.AddComponent<AudioSource>();
            ambience.clip = clips["surf"]; ambience.loop = true; ambience.spatialBlend = 0; ambience.playOnAwake = false;
            ambience.volume = Settings.Current.ambience * .5f;
            ambience.Play();
            Settings.OnChanged -= ApplyVolumes;
            Settings.OnChanged += ApplyVolumes;
        }

        static void ApplyVolumes() { if (ambience != null) ambience.volume = Settings.Current.ambience * .5f; }

        /// <summary>Play a sound. Identical sounds closer together than `gap` seconds are skipped, so SMG-like spam can't clip.</summary>
        public static void Play(string name, float volume = 1f, float gap = .03f)
        {
            if (Muted || src == null || !clips.TryGetValue(name, out var c)) return;
            float now = Time.unscaledTime;
            if (last.TryGetValue(name, out var t) && now - t < gap) return;
            last[name] = now;
            if (Net.IsHost && name != "ui" && name != "deny") Net.Host.Sound(name, volume);
            var s = Settings.Current;
            src.pitch = Random.Range(.92f, 1.08f);
            src.PlayOneShot(c, volume * (Interface.Contains(name) ? s.interfaceVolume : s.effects));
        }
    }
}

// TideWall 3D — a gun's reload minigame, ported from the 2D game's reload.js (CLAUDE.md R13 there, R6b here).
// Every style is optional: doing nothing finishes a normal reload in the gun's reload time. Skill makes it faster and
// can earn a Perfect (bonus damage until the magazine is empty); a miss jams (adds time).
//
//   Zone      tap in the green zone                     Heavy     one narrow chance on an accelerating marker
//   Rhythm    tap on each beat to snap in a round        Drop      the zone moves every reload
//   Shells    tap as each shell slides into the zone     Mash      mash to charge faster
//   Bolt      tap in the first zone, then the second     Pressure  hold, release in the green; it vents at the top
//
// The player (host side) owns one while reloading and calls Update / Press / Release. Online, its state is written into
// NetPlayerState so a client's HUD can draw the same widget (PlayerHud → ReloadWidget).
using System.IO;
using UnityEngine;

namespace TideWall
{
    public class ReloadGame
    {
        public ReloadStyle Style { get; private set; }
        public ReloadDef R { get; private set; }
        /// <summary>Time spent, and the time it takes (grows with jams).</summary>
        public float T, Need;
        public float Real;
        public bool Tried, Jammed, Done, Perfect;
        public Vector2 Zone, Zone2;
        public int Step;
        // One round at a time (Rhythm, Shells)
        public float Per, RoundT, Fumble, BeatT;
        int lastBeat = -1, usedBeat = -1, passive;
        bool clean = true;
        // Pressure
        public bool Hold;
        public float Needle;
        /// <summary>Feedback flashed at the reticle ("PERFECT!", "JAMMED", "FUMBLE").</summary>
        public string Word = "";
        public float WordT;
        public bool WordGood;

        public bool PerRound => R.perRound;

        /// <summary>Where the moving marker is (0..1). Heavy accelerates: position = progress².</summary>
        public float Marker
        {
            get
            {
                float p = Need > 0 ? Mathf.Clamp01(T / Need) : 1;
                return Style == ReloadStyle.Heavy ? p * p : p;
            }
        }

        public float RoundFraction => Per > 0 ? Mathf.Clamp01(RoundT / Per) : 0;

        /// <summary>Rhythm: 1 → 0 as the next beat arrives, and how close to a beat we are (1 = on it).</summary>
        public float ToNextBeat => R.beat > 0 ? (R.beat - Mod(BeatT, R.beat)) / R.beat : 0;
        public float NearBeat
        {
            get
            {
                if (R.beat <= 0) return 0;
                float ph = Mod(BeatT, R.beat);
                return 1 - Mathf.Min(ph, R.beat - ph) / (R.beat / 2);
            }
        }

        public static ReloadGame Begin(ReloadDef def, float time, int mag)
        {
            var g = new ReloadGame { Style = def.style, R = def, Need = time };
            if (def.style == ReloadStyle.Drop)
            {
                float w = Mathf.Min(.5f, def.width), lo = Random.Range(.22f, .88f - w);
                g.Zone = new Vector2(lo, lo + w);
            }
            else g.Zone = def.zone;
            g.Zone2 = def.zone2;
            if (def.perRound)
            {
                g.Per = time / Mathf.Max(1, mag);
                g.BeatT = -.35f;
                // the revolver's lead-in before the first beat also delays its passive loading
                if (def.style == ReloadStyle.Rhythm) g.RoundT = g.BeatT;
            }
            return g;
        }

        /// <summary>0..1 for the HUD (per-round styles: how full the magazine is).</summary>
        public float Progress(int ammo, int mag) => Done ? 1 : PerRound ? ammo / (float)Mathf.Max(1, mag) : Mathf.Clamp01(T / Mathf.Max(.01f, Need));

        /// <summary>Advance the reload. Rounds go into `ammo` as they load. True when the reload is complete.</summary>
        public bool Update(float dt, ref int ammo, int mag)
        {
            WordT -= dt;
            if (Done) return true;
            Real += dt;
            if (PerRound)
            {
                if (Fumble > 0) Fumble -= dt;
                else RoundT += dt;
                if (RoundT >= Per) LoadRound(ref ammo, mag, false);
                if (Style == ReloadStyle.Rhythm)
                {
                    BeatT += dt;
                    int k = Mathf.FloorToInt(BeatT / R.beat);
                    if (k >= 0 && k != lastBeat) { lastBeat = k; Sfx.Play("tick", .5f, .02f); }
                }
                if (ammo >= mag)
                {
                    // Perfect: every round went in by skill, with no fumbles
                    if (clean && passive == 0) Succeed();
                    Done = true;
                    return true;
                }
                return false;
            }
            if (Style == ReloadStyle.Pressure)
            {
                if (Hold)
                {
                    Needle += dt / Mathf.Max(.1f, R.fill);
                    if (Needle >= 1) { Hold = false; Needle = 0; Jam(R.jam, "VENTED!"); }
                }
                else Needle = Mathf.Max(0, Needle - dt * 2.5f);
            }
            T += dt;
            if (T >= Need)
            {
                if (Style == ReloadStyle.Mash && Tried && Real <= R.perfectUnder * Need) Succeed();
                Done = true;
            }
            return Done;
        }

        /// <summary>The reload button went down.</summary>
        public void Press(ref int ammo, int mag)
        {
            if (Done) return;
            switch (Style)
            {
                case ReloadStyle.Zone:
                case ReloadStyle.Drop:
                case ReloadStyle.Heavy:
                    if (Tried) return;
                    Tried = true;
                    if (In(Marker, Zone)) Finish(); else Jam(R.jam);
                    return;
                case ReloadStyle.Bolt:
                {
                    if (Tried) return;
                    var z = Step == 0 ? Zone : Zone2;
                    if (!In(Marker, z)) { Jam(R.jam); return; }
                    Sfx.Play("load");
                    if (Step == 0) { Step = 1; T = Mathf.Max(T, .5f * Need); Say("OPEN", true); }   // bolt back: skip ahead
                    else Finish();
                    return;
                }
                case ReloadStyle.Mash:
                    Tried = true;
                    T += R.perTap * Need;
                    Sfx.Play("zap", .4f, .02f);
                    return;
                case ReloadStyle.Pressure:
                    if (!Tried) Hold = true;
                    return;
                case ReloadStyle.Rhythm:
                {
                    float ph = Mod(BeatT, R.beat);
                    int nearest = Mathf.RoundToInt(BeatT / R.beat);
                    if (nearest == usedBeat) return;   // one round per beat; double taps are ignored
                    if (Mathf.Min(ph, R.beat - ph) <= R.window && BeatT > -R.window)
                    {
                        usedBeat = nearest;
                        LoadRound(ref ammo, mag, true);
                    }
                    else { Fumble = R.fumble; clean = false; Say("FUMBLE", false); Sfx.Play("jam", .6f); }
                    return;
                }
                case ReloadStyle.Shells:
                    if (In(RoundFraction, Zone)) LoadRound(ref ammo, mag, true);
                    else { RoundT -= R.slip; clean = false; Say("SLIP", false); Sfx.Play("jam", .6f); }
                    return;
            }
        }

        /// <summary>The reload button came up (Pressure: release in the green).</summary>
        public void Release()
        {
            if (Style != ReloadStyle.Pressure || !Hold || Done) return;
            Hold = false;
            if (In(Needle, Zone)) Finish();
            // released too early: the needle just falls back, try again
        }

        void LoadRound(ref int ammo, int mag, bool byTap)
        {
            if (ammo >= mag) return;
            ammo++;
            RoundT = 0;
            if (!byTap) passive++;
            Sfx.Play("load", .7f, .02f);
        }

        void Finish() { Succeed(); Done = true; }

        void Succeed()
        {
            Perfect = true;
            Say("PERFECT!", true);
            Sfx.Play("perfect");
        }

        void Jam(float secs, string word = "JAMMED")
        {
            Need += secs; Jammed = true; Tried = true;
            Say(word, false);
            Sfx.Play("jam");
        }

        void Say(string word, bool good) { Word = word; WordGood = good; WordT = .9f; }

        static bool In(float v, Vector2 z) => v >= z.x && v <= z.y;
        static float Mod(float a, float b) => ((a % b) + b) % b;

        // ---- online: a client draws the host's reload ----
        public void Write(BinaryWriter w)
        {
            w.Write((byte)Style);
            w.Write((byte)((Tried ? 1 : 0) | (Jammed ? 2 : 0) | (Done ? 4 : 0) | (Perfect ? 8 : 0) | (Hold ? 16 : 0) | (WordGood ? 32 : 0)));
            w.Write(T); w.Write(Need); w.Write(Zone.x); w.Write(Zone.y); w.Write((byte)Step);
            w.Write(RoundT); w.Write(Per); w.Write(BeatT); w.Write(Fumble); w.Write(Needle);
            w.Write(Word ?? ""); w.Write(WordT);
        }

        public static ReloadGame Read(BinaryReader r)
        {
            var style = (ReloadStyle)r.ReadByte();
            var def = GameManager.I != null ? GameManager.I.DB.Reload(style) : new ReloadDef { style = style };
            var g = new ReloadGame { Style = style, R = def, Zone2 = def.zone2 };
            int f = r.ReadByte();
            g.Tried = (f & 1) != 0; g.Jammed = (f & 2) != 0; g.Done = (f & 4) != 0; g.Perfect = (f & 8) != 0; g.Hold = (f & 16) != 0; g.WordGood = (f & 32) != 0;
            g.T = r.ReadSingle(); g.Need = r.ReadSingle(); g.Zone = new Vector2(r.ReadSingle(), r.ReadSingle()); g.Step = r.ReadByte();
            g.RoundT = r.ReadSingle(); g.Per = r.ReadSingle(); g.BeatT = r.ReadSingle(); g.Fumble = r.ReadSingle(); g.Needle = r.ReadSingle();
            g.Word = r.ReadString(); g.WordT = r.ReadSingle();
            return g;
        }
    }
}

// TideWall 3D — fishing: cast, sit through the nibbles, strike when it really bites (fast is better), then fight it on the
// line. The fish runs left and right; lean the rod the other way (the move stick) to take the strain off. Hold Fire to reel:
// that brings it in but tightens the line, and too much snaps it, so you pump: reel, ease off, reel. Let the line go slack
// for long and it shakes the hook. Leapers jump clear of the water, and reeling while one's in the air is how lines break.
// Each kind of fish fights its own way and signals its big moves. Sometimes treasure shows off to one side: lean over and
// hold there, if you dare leave the fish. Catches come in sizes, the odd one a trophy. Pure numbers, run where the game is simulated.
using UnityEngine;

namespace TideWall
{
    public enum FishStage : byte { None, Cast, Waiting, Bite, Reeling }
    public enum FishResult { None, Caught, Lost }
    /// <summary>How it fights. Drifter: wanders about. Darter: short, quick runs. Diver: long, heavy runs. Leaper: jumps from side to side.</summary>
    public enum FishStyle : byte { Drifter, Darter, Diver, Leaper }

    /// <summary>What the HUD draws (sent to online clients). Fish, rod and treasure run from -1 (left) to 1 (right).</summary>
    public struct FishView
    {
        public FishStage stage;
        public float fish, rod, progress, tension;
        public Vector3 at;
        /// <summary>The bobber's dipping; the fish is about to go; it's in the air. The treasure's place (out of range: none), and how much of it you've got.</summary>
        public bool nibble, warning, leaping;
        public float treasure, treasureFill;
        public const float NoTreasure = -9;
    }

    public class FishingGame
    {
        const float TreasureChance = .35f, TreasureSeconds = 5, TreasureGrab = 1.1f, TreasureReach = .25f, PerfectHotSeconds = .4f;
        public const float DangerTension = .85f;

        public readonly FishDef Fish;
        public FishStage Stage { get; private set; } = FishStage.Cast;
        public float FishPos { get; private set; }
        public float Rod { get; private set; }
        /// <summary>How far it's been brought in: 1 and it's landed.</summary>
        public float Progress { get; private set; }
        /// <summary>The strain on the line: 1 and it snaps.</summary>
        public float Tension { get; private set; } = .4f;

        /// <summary>How big this one is: pay and lamp oil scale with it. Bigger fish also fight harder.</summary>
        public readonly float SizeMul = 1;
        public readonly bool Trophy;
        public float Kg => SizeMul * Mathf.Lerp(.8f, 9, Fish.difficulty);
        /// <summary>Struck the moment it bit: it starts well on its way in.</summary>
        public bool CleanStrike { get; private set; }
        public bool Nibbling => nibble > 0;
        public bool Warning => warn > 0;
        public bool Leaping => surging && leap;
        public float TreasurePos { get; private set; } = FishView.NoTreasure;
        public float TreasureFill { get; private set; }
        public bool GotTreasure { get; private set; }
        /// <summary>The line was never in the red for long.</summary>
        public bool PerfectCatch => hot < PerfectHotSeconds;

        float Difficulty => Mathf.Clamp01(Fish.difficulty + (Trophy ? .15f : 0));
        bool HasTreasure => TreasurePos > FishView.NoTreasure;

        float t = .55f, target, targetT, nibble, nibbleIn, warn, surgeTo, hot, slack, treasureIn, treasureLeft;
        bool surging, leap, nextLeap;
        readonly float biteWindow;

        public FishingGame(FishDef fish, float biteWindow)
        {
            Fish = fish; this.biteWindow = biteWindow;
            treasureIn = Random.value < TreasureChance ? Random.Range(1.5f, 5f) : float.MaxValue;
            if (fish.noSize) return;
            float roll = Random.value;
            Trophy = roll > .92f;
            SizeMul = Trophy ? 2 : Mathf.Lerp(.75f, 1.4f, roll / .92f);
        }

        /// <param name="strike">The strike button went down.</param>
        /// <param name="reel">The reel button is held.</param>
        /// <param name="lean">Which way the rod's being leaned, -1 to 1.</param>
        public FishResult Update(float dt, bool strike, bool reel, float lean = 0)
        {
            t -= dt;
            switch (Stage)
            {
                case FishStage.Cast:
                    if (t <= 0) { Stage = FishStage.Waiting; t = Random.Range(1.6f, 4.2f); nibbleIn = Random.Range(.4f, 1.2f); Sfx.Play("splash", .4f, .1f); }
                    break;
                case FishStage.Waiting:
                    if (strike) return FishResult.Lost;   // pulled it out of the water too soon: a nibble isn't a bite
                    nibble -= dt;
                    if ((nibbleIn -= dt) <= 0 && t > .35f) { nibble = .28f; nibbleIn = Random.Range(.7f, 1.5f); Sfx.Play("tick", .5f, .15f); }
                    if (t <= 0) { Stage = FishStage.Bite; t = biteWindow; nibble = 0; Sfx.Play("bell", .35f, .1f); }
                    break;
                case FishStage.Bite:
                    if (strike)
                    {
                        CleanStrike = t > biteWindow * .55f;
                        if (CleanStrike) Progress = .2f;
                        Stage = FishStage.Reeling;
                        Sfx.Play("reload", .7f);
                    }
                    else if (t <= 0) return FishResult.Lost;
                    break;
                case FishStage.Reeling:
                    return Fight(dt, reel, lean);
            }
            return FishResult.None;
        }

        /// <summary>Tests: straight to the fight.</summary>
        public void SkipToReel() => Stage = FishStage.Reeling;
        /// <summary>Tests and the capture: treasure here, now.</summary>
        public void ForceTreasure(float at) { TreasurePos = at; treasureLeft = TreasureSeconds; treasureIn = float.MaxValue; }

        FishResult Fight(float dt, bool reel, float lean)
        {
            float d = Difficulty;
            Rod = Mathf.MoveTowards(Rod, Mathf.Clamp(lean, -1, 1), dt * 3.5f);
            MoveFish(dt, d);

            // Leaning against the run takes the strain; leaning with it, or not at all, the line takes it
            float pull = Mathf.Abs(FishPos);
            float against = pull < .1f ? 1 : Mathf.Clamp01(-Rod * Mathf.Sign(FishPos));
            float strain = pull * (1 - against) * Mathf.Lerp(.35f, 1, d) * (Fish.style == FishStyle.Diver ? 1.3f : 1);
            float rate = (reel ? Mathf.Lerp(.28f, .4f, d) : -.45f) + strain;
            if (Leaping && reel) rate += 2.2f;
            Tension = Mathf.Max(0, Tension + rate * dt);
            if (Tension > DangerTension) hot += dt;
            if (Tension >= 1) { Sfx.Play("jam", .8f, .1f); return FishResult.Lost; }   // snap

            // It only comes in on the reel, and easier when you're working against it. Left alone on a run, it takes line
            if (reel) Progress += dt * Mathf.Lerp(.14f, .085f, d) * (.4f + .6f * against);
            else Progress = Mathf.Max(0, Progress - dt * .05f * pull * (1 - against));

            slack = !reel && Tension < .03f ? slack + dt : 0;
            if (slack > Mathf.Lerp(3, 2, d)) return FishResult.Lost;   // it's shaken the hook
            UpdateTreasure(dt);
            return Progress >= 1 ? FishResult.Caught : FishResult.None;
        }

        void MoveFish(float dt, float d)
        {
            float speed = Mathf.Lerp(.5f, 1.3f, d);
            if (warn > 0)
            {
                // About to go: it holds still for a moment, and the HUD shows it
                if ((warn -= dt) <= 0)
                {
                    target = surgeTo; surging = true; leap = nextLeap;
                    if (leap) Sfx.Play("splash", .8f, .15f);
                }
                return;
            }
            if (surging)
            {
                FishPos = Mathf.MoveTowards(FishPos, target, dt * (speed * 2.4f + .5f));
                if (Mathf.Approximately(FishPos, target)) surging = leap = false;
                return;
            }
            if ((targetT -= dt) <= 0) Decide(d);
            if (warn <= 0) FishPos = Mathf.MoveTowards(FishPos, target, dt * speed);
        }

        void Decide(float d)
        {
            float pace = Mathf.Lerp(1.3f, .6f, d);
            float side = Random.value < .5f ? -1 : 1;
            bool settled = Mathf.Abs(FishPos) < .3f;
            switch (Fish.style)
            {
                case FishStyle.Darter:
                    if (settled) { Surge(side * Random.Range(.5f, .9f), .3f, false); targetT = Random.Range(.5f, .9f) * pace; }
                    else { target = 0; targetT = Random.Range(.5f, 1.1f) * pace; }
                    break;
                case FishStyle.Diver:
                    if (settled) { Surge(side, .45f, false); targetT = Random.Range(1.8f, 3f) * pace; }
                    else { target = 0; targetT = Random.Range(1f, 1.6f) * pace; }
                    break;
                case FishStyle.Leaper:
                    Surge(FishPos > 0 ? -.9f : .9f, .45f, true);
                    targetT = Random.Range(1.2f, 2.1f) * pace;
                    break;
                default:
                    target = Random.Range(-.55f, .55f);
                    targetT = Random.Range(.6f, 1.6f) * pace;
                    break;
            }
        }

        void Surge(float to, float warning, bool leaps) { surgeTo = to; warn = warning; nextLeap = leaps; }

        void UpdateTreasure(float dt)
        {
            if (GotTreasure) return;
            if (!HasTreasure)
            {
                if ((treasureIn -= dt) > 0) return;
                treasureIn = float.MaxValue;
                // On the far side from the fish: that's the point
                TreasurePos = (FishPos > 0 ? -1 : 1) * Random.Range(.4f, .9f);
                treasureLeft = TreasureSeconds;
                Sfx.Play("coin", .5f, .1f);
                return;
            }
            if ((treasureLeft -= dt) <= 0) { TreasurePos = FishView.NoTreasure; TreasureFill = 0; return; }
            if (Mathf.Abs(TreasurePos - Rod) > TreasureReach) return;
            TreasureFill += dt / TreasureGrab;
            if (TreasureFill < 1) return;
            GotTreasure = true; TreasurePos = FishView.NoTreasure;
            Sfx.Play("perfect", .5f, .1f);
        }
    }
}

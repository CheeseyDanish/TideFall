// TideWall 3D — the fishing minigame's rules: cast, sit through the nibbles, strike when it really bites (fast is better),
// then keep the catch zone on the fish with the Fire button (hold to rise, let go to sink) until the meter fills. Each kind
// of fish fights its own way, and its big moves are signalled first. Sometimes treasure drifts past: go and get it, if you
// dare leave the fish. Catches come in sizes, the odd one a trophy. Pure numbers, run where the game is simulated.
using UnityEngine;

namespace TideWall
{
    public enum FishStage : byte { None, Cast, Waiting, Bite, Reeling }
    public enum FishResult { None, Caught, Lost }
    /// <summary>How it fights. Drifter: wanders. Darter: sits still, then snaps away. Diver: hugs the bottom, then surges up. Leaper: rests, then jumps the whole bar.</summary>
    public enum FishStyle : byte { Drifter, Darter, Diver, Leaper }

    /// <summary>What the HUD draws (sent to online clients): the bar runs 0 (bottom) to 1 (top).</summary>
    public struct FishView
    {
        public FishStage stage;
        public float fish, zone, zoneSize, progress;
        public Vector3 at;
        /// <summary>The bobber's dipping; the fish is about to move; where the treasure is (below 0: none), and how much of it you've got.</summary>
        public bool nibble, warning;
        public float treasure, treasureFill;
    }

    public class FishingGame
    {
        const float TreasureChance = .35f, TreasureSeconds = 5, TreasureGrab = 1.1f, PerfectOffSeconds = .5f;

        public readonly FishDef Fish;
        public FishStage Stage { get; private set; } = FishStage.Cast;
        public float FishPos { get; private set; } = .5f;
        public float Zone { get; private set; } = .3f;
        public float Progress { get; private set; } = .3f;
        public float ZoneSize => Mathf.Lerp(.36f, .17f, Difficulty);

        /// <summary>How big this one is: pay and lamp oil scale with it. Bigger fish also fight harder.</summary>
        public readonly float SizeMul = 1;
        public readonly bool Trophy;
        public float Kg => SizeMul * Mathf.Lerp(.8f, 9, Fish.difficulty);
        /// <summary>Struck the moment it bit: the meter starts well ahead.</summary>
        public bool CleanStrike { get; private set; }
        public bool Nibbling => nibble > 0;
        public bool Warning => warn > 0;
        public float TreasurePos { get; private set; } = -1;
        public float TreasureFill { get; private set; }
        public bool GotTreasure { get; private set; }
        /// <summary>Barely left the fish all fight.</summary>
        public bool PerfectCatch => offFish < PerfectOffSeconds;

        float Difficulty => Mathf.Clamp01(Fish.difficulty + (Trophy ? .15f : 0));

        float t = .55f, zoneVel, target = .5f, targetT, nibble, nibbleIn, warn, surgeTo, offFish, treasureIn, treasureLeft;
        bool surging, wasOn;
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

        public FishResult Update(float dt, bool strike, bool reel)
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
                        if (CleanStrike) Progress = .5f;
                        Stage = FishStage.Reeling;
                        Sfx.Play("reload", .7f);
                    }
                    else if (t <= 0) return FishResult.Lost;
                    break;
                case FishStage.Reeling:
                    return Reel(dt, reel);
            }
            return FishResult.None;
        }

        /// <summary>Tests: straight to the reel.</summary>
        public void SkipToReel() => Stage = FishStage.Reeling;
        /// <summary>Tests and the capture: treasure here, now.</summary>
        public void ForceTreasure(float at) { TreasurePos = at; treasureLeft = TreasureSeconds; treasureIn = float.MaxValue; }

        FishResult Reel(float dt, bool reel)
        {
            MoveFish(dt);
            zoneVel += (reel ? 2.6f : -2.1f) * dt;
            zoneVel *= Mathf.Exp(-dt * .9f);
            Zone += zoneVel * dt;
            if (Zone <= 0 || Zone >= 1) { Zone = Mathf.Clamp01(Zone); zoneVel *= -.25f; }
            bool on = Mathf.Abs(FishPos - Zone) <= ZoneSize / 2;
            if (on) wasOn = true;
            else if (wasOn) offFish += dt;   // (getting onto it to begin with doesn't count)
            Progress += (on ? .32f : -.2f) * dt;
            UpdateTreasure(dt);
            if (Progress >= 1) return FishResult.Caught;
            return Progress <= 0 ? FishResult.Lost : FishResult.None;
        }

        void MoveFish(float dt)
        {
            float d = Difficulty, speed = Mathf.Lerp(.3f, 1.05f, d);
            if (warn > 0)
            {
                // About to go: it holds still for a moment, and the HUD shows it
                if ((warn -= dt) <= 0) { target = surgeTo; surging = true; }
                return;
            }
            if (surging)
            {
                FishPos = Mathf.MoveTowards(FishPos, target, dt * (speed * 2.2f + .5f));
                if (Mathf.Approximately(FishPos, target)) surging = false;
                return;
            }
            if ((targetT -= dt) <= 0) Decide(d);
            if (warn <= 0) FishPos = Mathf.MoveTowards(FishPos, target, dt * speed);
        }

        void Decide(float d)
        {
            float pace = Mathf.Lerp(1.3f, .55f, d);
            switch (Fish.style)
            {
                case FishStyle.Darter:
                    target = FishPos;   // sits still...
                    targetT = Random.Range(.7f, 1.5f) * pace;
                    Surge(Mathf.Clamp01(FishPos + (Random.value < .5f ? -1 : 1) * Random.Range(.25f, .5f)), .3f);
                    break;
                case FishStyle.Diver:
                    if (FishPos > .35f) { target = Random.Range(0f, .25f); targetT = Random.Range(1f, 1.8f) * pace; }
                    else { targetT = Random.Range(1f, 1.8f) * pace; Surge(Random.Range(.7f, 1f), .45f); }
                    break;
                case FishStyle.Leaper:
                    targetT = Random.Range(1.1f, 2f) * pace;
                    Surge(FishPos > .5f ? Random.Range(0f, .25f) : Random.Range(.75f, 1f), .45f);
                    break;
                default:
                    targetT = Random.Range(.35f, 1.3f) * pace;
                    target = Mathf.Clamp01(FishPos + Random.Range(-1f, 1f) * Mathf.Lerp(.25f, .8f, d));
                    break;
            }
        }

        void Surge(float to, float warning) { surgeTo = to; warn = warning; }

        void UpdateTreasure(float dt)
        {
            if (GotTreasure) return;
            if (TreasurePos < 0)
            {
                if ((treasureIn -= dt) > 0) return;
                treasureIn = float.MaxValue;
                // Well away from the fish: that's the point
                TreasurePos = FishPos > .5f ? Random.Range(.05f, Mathf.Max(.06f, FishPos - .35f)) : Random.Range(Mathf.Min(.94f, FishPos + .35f), .95f);
                treasureLeft = TreasureSeconds;
                Sfx.Play("coin", .5f, .1f);
                return;
            }
            if ((treasureLeft -= dt) <= 0) { TreasurePos = -1; TreasureFill = 0; return; }
            if (Mathf.Abs(TreasurePos - Zone) > ZoneSize / 2) return;
            TreasureFill += dt / TreasureGrab;
            if (TreasureFill < 1) return;
            GotTreasure = true; TreasurePos = -1;
            Sfx.Play("perfect", .5f, .1f);
        }
    }
}

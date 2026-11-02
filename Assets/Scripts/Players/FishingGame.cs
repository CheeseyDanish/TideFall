// TideWall 3D — the fishing minigame's rules: cast, wait, strike when it bites, then keep the catch zone on the fish with
// the Fire button (hold to rise, let go to sink) until the meter fills. Pure numbers, run where the game is simulated.
using UnityEngine;

namespace TideWall
{
    public enum FishStage : byte { None, Cast, Waiting, Bite, Reeling }
    public enum FishResult { None, Caught, Lost }

    /// <summary>What the HUD draws (sent to online clients): the bar runs 0 (bottom) to 1 (top).</summary>
    public struct FishView
    {
        public FishStage stage;
        public float fish, zone, zoneSize, progress;
        public Vector3 at;
    }

    public class FishingGame
    {
        public readonly FishDef Fish;
        public FishStage Stage { get; private set; } = FishStage.Cast;
        public float FishPos { get; private set; } = .5f;
        public float Zone { get; private set; } = .3f;
        public float Progress { get; private set; } = .3f;
        public float ZoneSize => Mathf.Lerp(.36f, .17f, Fish.difficulty);

        float t = .55f, zoneVel, target = .5f, targetT;
        readonly float biteWindow;

        public FishingGame(FishDef fish, float biteWindow) { Fish = fish; this.biteWindow = biteWindow; }

        public FishResult Update(float dt, bool strike, bool reel)
        {
            t -= dt;
            switch (Stage)
            {
                case FishStage.Cast:
                    if (t <= 0) { Stage = FishStage.Waiting; t = Random.Range(1f, 3.2f); Sfx.Play("splash", .4f, .1f); }
                    break;
                case FishStage.Waiting:
                    if (strike) return FishResult.Lost;   // pulled it out of the water too soon
                    if (t <= 0) { Stage = FishStage.Bite; t = biteWindow; Sfx.Play("bell", .35f, .1f); }
                    break;
                case FishStage.Bite:
                    if (strike) { Stage = FishStage.Reeling; Sfx.Play("reload", .7f); }
                    else if (t <= 0) return FishResult.Lost;
                    break;
                case FishStage.Reeling:
                    return Reel(dt, reel);
            }
            return FishResult.None;
        }

        /// <summary>Tests: straight to the reel.</summary>
        public void SkipToReel() => Stage = FishStage.Reeling;

        FishResult Reel(float dt, bool reel)
        {
            // The fish darts: harder fish change their mind sooner and go further
            if ((targetT -= dt) <= 0)
            {
                targetT = Random.Range(.35f, 1.3f) * Mathf.Lerp(1.3f, .55f, Fish.difficulty);
                target = Mathf.Clamp01(FishPos + Random.Range(-1f, 1f) * Mathf.Lerp(.25f, .8f, Fish.difficulty));
            }
            FishPos = Mathf.MoveTowards(FishPos, target, dt * Mathf.Lerp(.3f, 1.05f, Fish.difficulty));
            zoneVel += (reel ? 2.6f : -2.1f) * dt;
            Zone += zoneVel * dt;
            if (Zone <= 0 || Zone >= 1) { Zone = Mathf.Clamp01(Zone); zoneVel *= -.25f; }
            bool on = Mathf.Abs(FishPos - Zone) <= ZoneSize / 2;
            Progress += (on ? .32f : -.2f) * dt;
            if (Progress >= 1) return FishResult.Caught;
            return Progress <= 0 ? FishResult.Lost : FishResult.None;
        }
    }
}

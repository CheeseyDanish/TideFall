// TideWall 3D — Player: fishing (Interact on a patch of bubbles; Interact strikes, Fire reels, Back or walking off gives
// up) and being up at The Last Light's lamp (the view lifts to the lamp room; you can't be reached, but it burns oil).
// (One part of the Player class; Player.cs has the overview, the fields and the frame loop.)
using UnityEngine;

namespace TideWall
{
    public partial class Player
    {
        FishingGame fishing;
        float fishPause;
        FishingSpot fishingAt;
        FishView netFish;
        LineRenderer fishLine;

        /// <summary>The fishing minigame as the HUD draws it (mirrored from the host online).</summary>
        public FishView Fish => !Simulated ? netFish
            : fishing == null ? default
            : new FishView { stage = fishing.Stage, fish = fishing.FishPos, zone = fishing.Zone, zoneSize = fishing.ZoneSize, progress = fishing.Progress, at = fishingAt != null ? fishingAt.transform.position : transform.position };

        /// <summary>Metres the view is lifted (0 = on the ground).</summary>
        public float Perch { get; private set; }

        public void SetPerch(float height)
        {
            Perch = height;
            if (height > 0) Toast($"{Glyph(Btn.Dash)} flare · {Glyph(Btn.Interact)} leave the lamp", new Color(1, .9f, .6f));
            if (Simulated && height > 0) fishing = null;
        }

        void UpdatePerch()
        {
            if (Perch <= 0 || Kind == PlayerKind.Proxy || Kind == PlayerKind.LocalProxy) return;
            if (gm.Level is not Lighthouse l || l.Keeper != this) return;
            if ((Pressed(Btn.Interact) || Pressed(Btn.Back)) && perchSettled > .2f) l.SetKeeper(null);
            else if (Pressed(Btn.Dash)) l.TryFlare(this);
        }
        float perchSettled;

        public void StartFishing(FishingSpot spot)
        {
            if (fishing != null || Perch > 0) return;
            fishing = new FishingGame(RollFish(), db.fishBiteWindow);
            fishingAt = spot;
            reload = null;
            Sfx.Play("swing", .6f);
        }

        /// <summary>Tests and the tour: start with this fish on the line, already reeling.</summary>
        public FishingGame StartReeling(FishDef fish, FishingSpot spot)
        {
            fishing = new FishingGame(fish, db.fishBiteWindow);
            fishing.SkipToReel();
            fishingAt = spot;
            return fishing;
        }

        FishDef RollFish()
        {
            float total = 0;
            foreach (var f in db.fish) if (f.minWave <= gm.Waves.RunWave) total += f.weight;
            float r = Random.value * total;
            foreach (var f in db.fish)
            {
                if (f.minWave > gm.Waves.RunWave) continue;
                r -= f.weight;
                if (r <= 0) return f;
            }
            return db.fish[0];
        }

        /// <summary>From UpdateCombat: true while fishing (the gun stays down and Fire reels instead).</summary>
        bool UpdateFishing(float dt)
        {
            if (fishing == null) return false;
            if (grabbedBy != null || fishingAt == null || FlatDistance(fishingAt.transform.position) > fishingAt.radius + 2 || Pressed(Btn.Back)) { fishing = null; return false; }
            var r = fishing.Update(dt, Pressed(Btn.Interact), Held(Btn.Fire));
            if (r == FishResult.None) return true;
            fishPause = .3f;   // the press that ended it mustn't cast again
            if (r == FishResult.Caught) Land(fishing.Fish);
            else { Toast("It got away!", new Color(.8f, .9f, 1)); Sfx.Play("splash", .5f, .1f); }
            fishing = null;
            return true;
        }

        void Land(FishDef f)
        {
            var at = fishingAt != null ? fishingAt.transform.position : transform.position;
            fishingAt?.Caught();
            Fx.Splash(at, 12);
            Sfx.Play("perfect", .7f, .1f);
            int sd = Mathf.RoundToInt(f.sandDollars * FortuneMultiplier);
            if (sd > 0) gm.AddSandDollars(sd, at, f.displayName);
            if (f.pearls > 0) gm.AddPearls(f.pearls, at);
            if (f.heal > 0) Heal(f.heal);
            gm.Level.OnFishCaught(f);
            Legacy.LogFish(f.id);
            if (db.Enemy(f.creature) is { } creature)
            {
                var c = at; c.y = Arena.GroundAt(c);
                gm.Waves.SpawnExtra(creature, c);
            }
            Toast($"{f.displayName}! {f.description}", new Color(.6f, .95f, 1));
            // It flies up out of the water toward you
            if (Content.Model(f.model) != null)
            {
                var tossed = ModelFactory.Spawn(f.model, null, at + Vector3.up * .3f, 1.1f);
                var rb = tossed.root.AddComponent<Rigidbody>();
                rb.mass = .2f;
                rb.linearVelocity = (transform.position - at).normalized * 3 + Vector3.up * 6;
                rb.angularVelocity = Random.insideUnitSphere * 10;
                rb.detectCollisions = false;
                Destroy(tossed.root, 1.4f);
            }
        }

        /// <summary>Tests: this fish is landed now.</summary>
        public void DebugLandFish() { if (fishing != null) { Land(fishing.Fish); fishing = null; } }

        /// <summary>The line from your rod to the bobber, and lifting the body up the tower.</summary>
        void UpdateFishLine()
        {
            perchSettled = Perch > 0 ? perchSettled + Time.deltaTime : 0;
            visual.localPosition = Vector3.up * Perch;
            var f = Fish;
            bool on = f.stage != FishStage.None;
            if (!on) { if (fishLine != null) fishLine.enabled = false; return; }
            if (fishLine == null)
            {
                fishLine = new GameObject("Fishing Line").AddComponent<LineRenderer>();
                fishLine.material = Mats.ParticleAdditive; fishLine.positionCount = 2; fishLine.useWorldSpace = true;
                fishLine.startWidth = fishLine.endWidth = .008f;
                fishLine.startColor = fishLine.endColor = new Color(1, 1, 1, .6f);
                fishLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            fishLine.enabled = true;
            bool own = FirstPerson && Cam != null;
            fishLine.SetPosition(0, own ? Cam.View.RodTip : transform.position + Vector3.up * 1.3f + AimDir * .6f);
            fishLine.SetPosition(1, f.at);
        }
    }
}

// TideWall 3D — a reload miss costs only the bonus, the scattergun rewards good timing, and fish fight in their own ways.
using System.Collections;
using NUnit.Framework;
using UnityEngine;

namespace TideWall.Tests
{
    public class ReelAndReloadTests
    {
        static GameDatabase Db => GameDatabase.Load();

        static float Run(ReloadGame g, int mag, System.Action<float> each)
        {
            int ammo = 0; float spent = 0;
            while (!g.Update(1 / 60f, ref ammo, mag) && spent < 20) { spent += 1 / 60f; each?.Invoke(spent); }
            return spent;
        }

        [Test]
        public void MissingNeverMakesAReloadSlower()
        {
            foreach (var r in Db.reloads)
            {
                var idle = ReloadGame.Begin(r, 2, 6);
                float normal = Run(idle, 6, null);
                var bad = ReloadGame.Begin(r, 2, 6);
                int a = 0;
                bad.Press(ref a, 6);   // at the very start: outside every zone
                float missed = Run(bad, 6, null);
                Assert.LessOrEqual(missed, normal + .05f, r.style.ToString());
                Assert.IsFalse(bad.Perfect, $"{r.style}: no Perfect for that");
            }
        }

        [Test]
        public void TheScattergunIsSlowUnlessYouTimeIt()
        {
            var gun = Db.Gun("scattergun");
            int mag = Mathf.RoundToInt(gun.magazine);
            var r = Db.Reload(gun.reloadStyle);
            float lazy = Run(ReloadGame.Begin(r, gun.reload, mag), mag, null);
            Assert.Greater(lazy, 3, "left alone it's slow");
            var good = ReloadGame.Begin(r, gun.reload, mag);
            int ammo = 0; float spent = 0;
            while (!good.Update(1 / 60f, ref ammo, mag) && spent < 20)
            {
                spent += 1 / 60f;
                if (good.RoundFraction >= (good.Zone.x + good.Zone.y) / 2) good.Press(ref ammo, mag);
            }
            Assert.Less(spent, lazy * .8f, "timed, it's a good deal quicker");
            Assert.IsTrue(good.Perfect, "and Perfect");
        }

        static FishDef Fish(FishStyle style, float difficulty = .5f) => new() { id = "t", displayName = "t", difficulty = difficulty, style = style, noSize = true };

        [Test]
        public void LeapersSignalThenJumpTheBar()
        {
            var g = new FishingGame(Fish(FishStyle.Leaper), 1);
            g.SkipToReel();
            bool warned = false; float low = 1, high = 0;
            for (int i = 0; i < 500; i++)
            {
                g.Update(.02f, false, i % 2 == 0);
                warned |= g.Warning;
                low = Mathf.Min(low, g.FishPos); high = Mathf.Max(high, g.FishPos);
            }
            Assert.IsTrue(warned, "you get a warning");
            Assert.Greater(high - low, .4f, "and it goes a long way");
        }

        [Test]
        public void ADiverHugsTheBottom()
        {
            var g = new FishingGame(Fish(FishStyle.Diver, .2f), 1);
            g.SkipToReel();
            float low = 1;
            for (int i = 0; i < 300; i++) { g.Update(.02f, false, g.Zone < g.FishPos); low = Mathf.Min(low, g.FishPos); }
            Assert.Less(low, .3f);
        }

        [Test]
        public void TreasureIsGotBySittingOnItAndItsSizeIsSet()
        {
            var g = new FishingGame(Fish(FishStyle.Drifter), 1);
            g.SkipToReel();
            g.ForceTreasure(.3f);
            for (int i = 0; i < 200 && !g.GotTreasure; i++) g.Update(.02f, false, g.Zone < .3f);
            Assert.IsTrue(g.GotTreasure);
            Assert.AreEqual(-1, g.TreasurePos);

            Assert.AreEqual(1, g.SizeMul, "a boot's a boot");
            bool trophy = false;
            var sized = new FishDef { id = "s", displayName = "s", difficulty = .4f };
            for (int i = 0; i < 300; i++)
            {
                var f = new FishingGame(sized, 1);
                Assert.That(f.SizeMul, Is.InRange(.74f, 2.01f));
                trophy |= f.Trophy;
            }
            Assert.IsTrue(trophy, "the odd one's a trophy");
        }

        [Test]
        public void NibblesComeFirstAndAFastStrikeGivesAHeadStart()
        {
            var g = new FishingGame(Fish(FishStyle.Drifter), 1);
            bool nibbled = false;
            for (int i = 0; i < 500 && g.Stage != FishStage.Bite; i++) { g.Update(.02f, false, false); nibbled |= g.Nibbling; }
            Assert.AreEqual(FishStage.Bite, g.Stage);
            Assert.IsTrue(nibbled);
            g.Update(.02f, true, false);
            Assert.IsTrue(g.CleanStrike);
            Assert.Greater(g.Progress, .45f);
        }
    }
}

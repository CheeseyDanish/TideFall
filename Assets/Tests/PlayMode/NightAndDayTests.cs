// TideWall 3D — The Last Light's days and nights: the walk-in tower, the flare, the try-pot's vacuum, rescue and loss.
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TideWall.Tests
{
    public class NightAndDayTests
    {
        GameManager gm;
        Lighthouse level;
        Player p;
        float Top => Arena.IslandTop;
        readonly System.Collections.Generic.List<InputDevice> devices = new();
        string storage;

        IEnumerator Boot()
        {
            Time.timeScale = 1;
            storage ??= Path.Combine(Application.temporaryCachePath, "tidewall-tests-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(storage);
            Storage.Root = storage;   // (never the player's files)
            Settings.Reload();
            StoryCard.Disabled = true;
            DebugTools.ResetAll();
            GameManager.Pending = new RunRequest { save = new SaveData { slot = 0, part = Lighthouse.PartId } };
            SceneManager.LoadScene(Lighthouse.SceneName);
            yield return null; yield return null;
            gm = GameManager.I;
            level = gm.Lighthouse;
            var pad = InputSystem.AddDevice<Gamepad>();
            devices.Add(pad);
            p = gm.Players.Join(new InputDevice[] { pad });
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Down()
        {
            foreach (var d in devices) if (d.added) InputSystem.RemoveDevice(d);
            devices.Clear();
            DebugTools.ResetAll();
            StoryCard.Disabled = false;
            Time.timeScale = 1;
            yield return null;
            Storage.Root = null;
            Settings.Reload();
            if (storage != null && Directory.Exists(storage)) Directory.Delete(storage, true);
            storage = null;
        }

        /// <summary>Is there something to stand on, a short way below here?</summary>
        static bool Floor(Vector3 at, float within = .8f) => Physics.Raycast(at + Vector3.up * within, Vector3.down, within * 2, ~0, QueryTriggerInteraction.Ignore);

        [UnityTest]
        public IEnumerator YouCanWalkFromTheDoorAllTheWayToTheCatwalk()
        {
            yield return Boot();
            // The door's open and the wall beside it isn't
            var eye = new Vector3(0, Top + 1.2f, -8);
            Assert.IsFalse(Physics.Raycast(eye, Vector3.forward, 5, ~0, QueryTriggerInteraction.Ignore), "the door");
            Assert.IsTrue(Physics.Raycast(Quaternion.Euler(0, 60, 0) * eye, Quaternion.Euler(0, 60, 0) * Vector3.forward, 5, ~0, QueryTriggerInteraction.Ignore), "the wall");
            // The ramp is unbroken from the ground floor to the deck, with headroom all the way
            float from = 205, turns = 2.5f;
            for (float a = 0; a <= 360 * turns; a += 5)
            {
                float y = Top + LighthouseTower.DeckHeight * a / (360 * turns);
                var at = Quaternion.Euler(0, from + a, 0) * Vector3.forward * 4.6f + Vector3.up * y;
                Assert.IsTrue(Floor(at), $"ramp at {a}");
                Assert.IsFalse(Physics.Raycast(at + Vector3.up * .3f, Vector3.up, 1.7f, ~0, QueryTriggerInteraction.Ignore), $"head room at {a}");
            }
            // A sailor actually gets up it: walk them along it a little at a time
            var deck = Top + LighthouseTower.DeckHeight;
            Assert.IsTrue(Floor(new Vector3(0, deck, -9)), "the catwalk");
            Assert.IsTrue(Floor(new Vector3(0, deck, -5)), "the deck");
            p.Place(new Vector3(0, deck + .3f, -9));
            yield return new WaitForSeconds(.5f);
            Assert.Greater(p.transform.position.y, deck - .2f, "it holds you up");
            Assert.IsTrue(p.IsAloft && !p.IsTargetable, "out of reach up here");
            p.Place(new Vector3(0, Top + .3f, 0));
            yield return new WaitForSeconds(.3f);
            Assert.IsFalse(p.IsTargetable, "indoors, nothing can get at you");
            p.Place(level.SpawnPoint(0));
            yield return null;
            Assert.IsTrue(p.IsTargetable, "outside, it can");
        }

        [UnityTest]
        public IEnumerator DayIsForGettingReadyAndCreaturesComeAtNight()
        {
            yield return Boot();
            Assert.AreEqual(Lighthouse.Tide.Slack, level.State);
            Assert.IsFalse(level.Night);
            Assert.AreEqual(gm.DB.lastLight.look, gm.Phases.Current, "daylight");
            Assert.IsTrue(gm.Phases.ShopOpen, "the shop's open");
            StringAssert.Contains($"{gm.DB.lastLight.rescueDays} days until rescue", level.Heading());
            float oil = level.Oil;
            yield return new WaitForSeconds(.3f);
            Assert.AreEqual(oil, level.Oil, "the lamp's not burning oil by day");
            Assert.IsFalse(level.beam.gameObject.activeSelf, "nor showing");

            level.StartFloodForTests();
            yield return null;
            Assert.IsTrue(level.Night);
            Assert.AreEqual(gm.DB.lastLight.nightLook, gm.Phases.Current, "night falls");
            Assert.IsFalse(gm.Phases.ShopOpen, "the shop's shut");
            Assert.IsTrue(level.beam.gameObject.activeSelf, "the lamp is lit");
            StringAssert.Contains($"{gm.DB.lastLight.rescueDays - 1} days until rescue", level.Heading());

            level.AddOil(-1000);
            level.SetDarkForTests(99);
            yield return null;
            Assert.AreEqual(gm.DB.lastLight.blackoutLook, gm.Phases.Current, "with the lamp out it's far darker");
            Assert.IsTrue(level.eyes.activeSelf, "and something's watching");
        }

        [UnityTest]
        public IEnumerator TheFlareSpendsOilAndStunsWhatItReaches()
        {
            yield return Boot();
            level.StartFloodForTests();
            var d = gm.DB.lastLight;
            var near = new Vector3(0, 0, -20); near.y = Arena.GroundAt(near);
            var crab = gm.Waves.SpawnExtra(gm.DB.Enemy("crab"), near);
            var far = new Vector3(0, 0, -60); far.y = Arena.GroundAt(far);
            var away = gm.Waves.SpawnExtra(gm.DB.Enemy("crab"), far);
            yield return null;
            float oil = level.Oil;
            level.TryFlare(p);
            Assert.AreEqual(oil - d.flareCost, level.Oil, .5f, "it costs oil");
            Assert.Greater(level.FlareLeft, 0);
            yield return new WaitForSeconds(.2f);
            Assert.IsTrue(level.flareLight.enabled, "it lights everything");
            Assert.IsTrue(crab.Stunned, "in reach");
            Assert.IsFalse(away.Stunned, "out of reach");
            oil = level.Oil;
            level.TryFlare(p);
            Assert.AreEqual(oil, level.Oil, .5f, "not twice at once");
        }

        [UnityTest]
        public IEnumerator TheTryPotVacuumsCorpsesAtTheEndOfTheNightOrOnDemand()
        {
            yield return Boot();
            level.StartFloodForTests();
            for (int i = 0; i < 3; i++)
            {
                var at = new Vector3(i * 2 - 2, 0, -18); at.y = Arena.GroundAt(at);
                gm.Waves.SpawnExtra(gm.DB.Enemy("crab"), at).Die(new DamageInfo { dir = Vector3.forward });
            }
            yield return null;
            Assert.AreEqual(3, Corpse.Live.Count);
            int money = gm.SandDollars;
            Assert.AreEqual(3, Fishmonger.VacuumAll(), "the button takes them all");
            float t = 0;
            while (Corpse.Live.Count > 0 && t < 4) { t += Time.deltaTime; yield return null; }
            Assert.AreEqual(0, Corpse.Live.Count, "they fly in");
            Assert.Greater(gm.SandDollars, money, "and pay");
        }

        [UnityTest]
        public IEnumerator SurviveTheLastNightAndYoureRescued_LoseTheLightAndYoure()
        {
            yield return Boot();
            level.EndForTests(true);
            Assert.AreEqual(GameState.GameOver, gm.State);
            Assert.AreEqual("RESCUED!", level.GameOverTitle(p));
            Assert.Greater(level.Banked, 0, "with oil for the log");
            Assert.Greater(level.GameOverTint(p).g, .2f, "gold");
            StringAssert.Contains("Creatures killed", level.GameOverStats(p));
            Time.timeScale = 1;

            yield return Boot();
            level.EndForTests(false);
            Assert.AreEqual("LOST AT SEA", level.GameOverTitle(p));
            Assert.Less(level.GameOverTint(p).g, .1f, "red");
        }

        [UnityTest]
        public IEnumerator SharksLeapAtPeopleNearTheWaterline()
        {
            yield return Boot();
            level.StartFloodForTests();
            var shore = Vector3.forward * (Arena.ShoreRadius() - 3); shore.y = Arena.GroundAt(shore);
            p.Place(shore + Vector3.up * .1f);
            var at = Vector3.forward * (Arena.ShoreRadius() + 3); at.y = Arena.GroundAt(at);
            var shark = gm.Waves.SpawnExtra(gm.DB.Enemy("shark"), at);
            float hp = p.Hp, t = 0;
            while (p.Hp >= hp && t < 4) { t += Time.deltaTime; yield return null; }
            Assert.Less(p.Hp, hp, "you're not safe just for being on the sand");
        }

        [UnityTest]
        public IEnumerator ACorpseIsThrownAboutThenLies()
        {
            yield return Boot();
            var at = level.SpawnPoint(1) + new Vector3(0, 0, 4); at.y = Arena.GroundAt(at);
            var drowner = gm.Waves.SpawnExtra(gm.DB.Enemy("drowner"), at);
            yield return null;
            var start = drowner.transform.position;
            drowner.Die(new DamageInfo { dir = Vector3.forward, explosion = true });
            Assert.IsNotNull(drowner.GetComponent<LimbRag>(), "its limbs go loose");
            float up = 0, t = 0;
            while (drowner.GetComponent<DeathFlop>() != null && t < 3) { t += Time.deltaTime; up = Mathf.Max(up, drowner.Model.root.transform.localPosition.y); yield return null; }
            Assert.Less(t, 3, "it comes to rest");
            Assert.Greater(up, .1f, "after being thrown in the air");
            Assert.Greater(Vector3.Distance(start, drowner.transform.position), .2f, "and along the blow");
        }
    }
}

// TideWall 3D — play-mode tests for The Last Light (the rising tide, the lamp and its oil, bosses and salvage, the Keeper's
// Log) and the fishing minigame. Saves go to a temporary Storage.Root, never the player's files.
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TideWall.Tests
{
    public class LastLightTests
    {
        readonly System.Collections.Generic.List<InputDevice> devices = new();
        GameManager gm;
        Lighthouse level;
        string storage;

        IEnumerator Boot(string scene)
        {
            Time.timeScale = 1;
            storage = Path.Combine(Application.temporaryCachePath, "tidewall-tests-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(storage);
            Storage.Root = storage;
            Settings.Reload();
            StoryCard.Disabled = true;
            DebugTools.ResetAll();
            SceneManager.LoadScene(scene);
            yield return null;
            yield return null;
            gm = GameManager.I;
            level = gm.Level as Lighthouse;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var d in devices) if (d.added) InputSystem.RemoveDevice(d);
            devices.Clear();
            DebugTools.ResetAll();
            StoryCard.Disabled = false;
            Time.timeScale = 1;
            SceneManager.LoadScene("TideWall");
            yield return null;
            yield return null;
            Arena.Island = false;
            try { Directory.Delete(storage, true); } catch (System.Exception) { }
        }

        Player JoinPad(out Gamepad pad)
        {
            pad = InputSystem.AddDevice<Gamepad>();
            devices.Add(pad);
            return gm.Players.Join(new InputDevice[] { pad });
        }

        static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }

        [UnityTest]
        public IEnumerator TheSeaRisesUpTheHillAndCreaturesComeForTheLight()
        {
            yield return Boot(Lighthouse.SceneName);
            Assert.IsNotNull(level, "the scene has the mode's level");
            Assert.IsTrue(Arena.Island);
            var p = JoinPad(out _);
            yield return Frames(3);
            Assert.AreEqual(GameState.Playing, gm.State);
            Assert.IsFalse(p.GunsLocked, "armed from the start");
            Assert.AreEqual(Arena.GroundAt(p.transform.position), p.transform.position.y, .3f, "standing on the hill");
            Assert.Greater(Arena.IslandTop, Arena.WaterLevel);
            float shore = Arena.ShoreRadius();
            Assert.Greater(shore, 30);

            // A point on the slope is dry now, and under water once the tide's up
            var low = new Vector3(shore - 3, 0, 0);
            Assert.IsFalse(Arena.InWater(low));
            Arena.WaterLevel += .8f;
            Assert.IsTrue(Arena.InWater(low), "the sea came up over it");
            Assert.Less(Arena.ShoreRadius(), shore);
            Arena.WaterLevel -= .8f;

            // A creature on the slope walks up and hurts the tower
            float hp = level.Hp;
            var crab = gm.Waves.SpawnExtra(gm.DB.Enemy("crab"), new Vector3(9, Arena.IslandY(9, 0), 0));
            p.Place(new Vector3(-20, Arena.IslandY(-20, 0), 0));
            Time.timeScale = 3;
            float t = 0;
            while (level.Hp >= hp && t < 15) { t += Time.unscaledDeltaTime; yield return null; }
            Time.timeScale = 1;
            Assert.Less(level.Hp, hp, "it goes for the lighthouse");
            StringAssert.Contains("Lighthouse", gm.Level.StatusLine());
            crab.Die(default);
        }

        [UnityTest]
        public IEnumerator TheLampBurnsOilScorchesAndGoingDarkEndsTheRun()
        {
            yield return Boot(Lighthouse.SceneName);
            var p = JoinPad(out var pad);
            yield return Frames(3);
            var door = Object.FindAnyObjectByType<LampDoor>();
            Assert.IsNotNull(door);
            p.Place(door.transform.position + Vector3.up * .1f);   // (you walk up there now)
            door.Interact(p);
            Assert.AreEqual(p, level.Keeper);
            Assert.Greater(p.EyePos.y, 8, "the view goes up the tower");
            Assert.IsFalse(p.IsTargetable, "out of reach up there");
            float oil = level.Oil;
            yield return Frames(20);
            Assert.Less(level.Oil, oil, "a manned lamp burns oil, even at slack water");

            // Aim at a soaked creature: it dries, then burns
            var e = gm.Waves.SpawnExtra(gm.DB.Enemy("drowner"), new Vector3(0, Arena.IslandY(0, 18), 18));
            DebugTools.Freeze = true;
            var to = Quaternion.LookRotation(e.transform.position + Vector3.up - p.EyePos).eulerAngles;
            p.SetLook(to.y, to.x > 180 ? to.x - 360 : to.x);
            float wet = e.Wet;
            float hp = e.Hp;
            for (float t = 0; t < 1.5f; t += Time.deltaTime) yield return null;
            Assert.Less(e.Wet, Mathf.Max(wet, .1f) + .001f);
            DebugTools.Freeze = false;
            Assert.Less(e.Hp, hp, "the beam hurts");
            e.Die(default);

            // Climb back down
            level.SetKeeper(null);
            Assert.AreEqual(0, p.Perch);
            p.Place(level.SpawnPoint(0));
            Assert.IsTrue(p.IsTargetable);

            // No oil in a fight: a countdown, then it's over; oil saves it
            level.StartFloodForTests();
            level.AddOil(-9999);
            yield return Frames(3);
            Assert.Greater(level.DarkLeft, 0, "the countdown started");
            level.AddOil(50);
            Assert.AreEqual(0, level.DarkLeft);
            level.AddOil(-9999);
            level.SetDarkForTests(.05f);
            for (float t = 0; t < .3f; t += Time.unscaledDeltaTime) yield return null;
            Assert.AreEqual(GameState.GameOver, gm.State);
            Assert.Greater(Legacy.Load().bestTide, 0, "the run goes in the record");
        }

        [UnityTest]
        public IEnumerator AfterABossTheTideDropsAndWrecksCanBeLooted()
        {
            yield return Boot(Lighthouse.SceneName);
            var p = JoinPad(out _);
            yield return Frames(3);
            float before = Arena.WaterLevel;
            level.StartLowTideForTests();
            Assert.AreEqual(Lighthouse.Tide.LowTide, level.State);
            var crates = Object.FindObjectsByType<SalvageCrate>();
            Assert.AreEqual(gm.DB.lastLight.salvageCrates, crates.Length);
            int purse = gm.SandDollars;
            crates[0].Interact(p);
            yield return null;
            Assert.Greater(gm.SandDollars, purse, "there's money in it");
            Assert.AreEqual(crates.Length - 1, Object.FindObjectsByType<SalvageCrate>().Length, "it's opened");
            Time.timeScale = 4;
            for (float t = 0; t < 2.5f; t += Time.unscaledDeltaTime) yield return null;
            Time.timeScale = 1;
            Assert.Less(Arena.WaterLevel, before, "the sea's going out");
            level.EndLowTideForTests();
            yield return null;
            Assert.AreEqual(0, Object.FindObjectsByType<SalvageCrate>().Length, "the sea takes what's left");
            Assert.AreEqual(Lighthouse.Tide.Slack, level.State);
        }

        [UnityTest]
        public IEnumerator FishingCanBeWonAndLost()
        {
            yield return Boot("TideWall");
            var p = JoinPad(out var pad);
            yield return Frames(2);
            var db = gm.DB;
            Assert.GreaterOrEqual(db.fish.Count, 8);
            foreach (var f in db.fish) Assert.IsNotEmpty(f.description, f.id);
            var spot = FishingSpot.Spawn(p.transform.position + Vector3.forward);
            spot.radius = 30;
            Assert.IsNotNull(spot.Prompt(p));

            // Bad strike: pulled out before it bit
            p.StartFishing(spot);
            Assert.AreEqual(FishStage.Cast, p.Fish.stage);
            for (float t = 0; t < .8f; t += Time.deltaTime) yield return null;
            Assert.AreEqual(FishStage.Waiting, p.Fish.stage);
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.South));
            yield return null;
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return null;
            Assert.AreEqual(FishStage.None, p.Fish.stage, "too soon, and it's gone");

            // Rules alone: staying on the fish fills the meter; losing it empties
            var easy = new FishDef { id = "t", difficulty = 0, displayName = "t" };
            var g = new FishingGame(easy, 1);
            g.SkipToReel();
            FishResult r = FishResult.None;
            for (int i = 0; i < 4000 && r == FishResult.None; i++)
            {
                bool reel = g.Zone < g.FishPos;   // a perfect player
                r = g.Update(.02f, false, reel);
            }
            Assert.AreEqual(FishResult.Caught, r, "a good player lands it");
            var g2 = new FishingGame(easy, 1);
            g2.SkipToReel();
            r = FishResult.None;
            for (int i = 0; i < 4000 && r == FishResult.None; i++) r = g2.Update(.02f, false, true);
            Assert.AreEqual(FishResult.Lost, r, "holding the button at the top loses it");

            // A landed fish pays, and goes in the log. Holding Fire reels; the gun doesn't fire
            int purse = gm.SandDollars;
            gm.Waves.RunWave = gm.DB.gunsUnlockWave;
            var sardine = db.fish.Find(f => f.sandDollars > 0 && string.IsNullOrEmpty(f.creature));
            p.StartReeling(sardine, spot);
            int ammo = p.Ammo;
            InputSystem.QueueStateEvent(pad, new GamepadState { rightTrigger = 1 });
            yield return Frames(3);
            InputSystem.QueueStateEvent(pad, new GamepadState());
            Assert.AreEqual(ammo, p.Ammo, "Fire reels while fishing");
            p.DebugLandFish();
            Assert.Greater(gm.SandDollars, purse);
            Assert.Contains(sardine.id, Legacy.Load().fishLog);
            Assert.AreEqual(FishStage.None, p.Fish.stage);
        }

        [UnityTest]
        public IEnumerator TheMouseChoosesAndBuysInTheShop()
        {
            yield return Boot("TideWall");
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var mouse = InputSystem.AddDevice<Mouse>();
            devices.Add(keyboard); devices.Add(mouse);
            var p = gm.Players.Join(new InputDevice[] { keyboard, mouse });
            yield return Frames(3);
            Assert.IsTrue(p.UsesMouse);
            gm.AddSandDollars(5000);
            var shop = Menus.Shop(p, Menus.ShopTab.Guns, p.transform.position);
            p.OpenMenu(shop);
            yield return Frames(3);
            int gun = shop.Items.FindIndex(i => i.Label == "Scattergun");
            p.DebugPointer = p.Hud.GridScreenOf(gun);
            yield return Frames(2);
            Assert.AreEqual(gun, p.ShownMenu.Selected, "hovering chooses");
            int purse = gm.SandDollars;
            p.DebugClick = true;
            yield return Frames(2);
            Assert.Less(gm.SandDollars, purse, "clicking buys it");
            Assert.AreEqual("scattergun", p.Gun.id);
            // A click off every card does nothing
            purse = gm.SandDollars;
            p.DebugPointer = new Vector2(1, 1);
            yield return Frames(2);
            p.DebugClick = true;
            yield return Frames(2);
            Assert.AreEqual(purse, gm.SandDollars);
        }

        [Test]
        public void TheKeepersLogPersistsAndSpendsOil()
        {
            storage = Path.Combine(Application.temporaryCachePath, "tidewall-tests-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(storage);
            Storage.Root = storage;
            var db = GameDatabase.Load();
            Assert.GreaterOrEqual(db.lastLight.upgrades.Count, 5);
            Assert.IsFalse(Legacy.BuyKeeper("towerhp", 10), "no oil yet");
            Legacy.RecordLight(7, 3, 20, 100);
            var l = Legacy.Load();
            Assert.AreEqual(7, l.bestTide); Assert.AreEqual(100, l.lampOil);
            Assert.IsTrue(Legacy.BuyKeeper("towerhp", 10));
            l = Legacy.Load();
            Assert.AreEqual(90, l.lampOil); Assert.AreEqual(1, l.KeeperLevel("towerhp"));
            Assert.Greater(l.KeeperBonus(db.lastLight, KeeperUpgradeKind.TowerHp), 0);
        }
    }
}

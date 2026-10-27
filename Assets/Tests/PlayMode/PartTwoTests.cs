// TideWall 3D — play-mode tests for Part II (the Undertow): the campaign's data, each stage's scene booting into an
// expedition, its objectives (spires, beacons, the wreck-wall, the courtyard, the Heart), Spiral Shards, moving on to
// the next stage with the team, and the endings. Like the other tests, saves and the legacy record go to a temporary
// Storage.Root, never the player's files. The story cards are turned off so nothing waits on them.
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TideWall.Tests
{
    public class PartTwoTests
    {
        readonly System.Collections.Generic.List<InputDevice> devices = new();
        GameManager gm;
        string storage;

        [SetUp]
        public void SetUp()
        {
            Time.timeScale = 1;
            storage = Path.Combine(Application.temporaryCachePath, "tidewall-tests-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(storage);
            Storage.Root = storage;
            Settings.Reload();
            StoryCard.Disabled = true;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Net.Shutdown(null);
            foreach (var d in devices) if (d.added) InputSystem.RemoveDevice(d);
            devices.Clear();
            GameManager.Pending = null;
            StoryCard.Disabled = false;
            Time.timeScale = 1;
            // Back to an empty scene so the next test starts clean
            SceneManager.LoadScene("TideWall");
            yield return null;
            yield return null;
            try { Directory.Delete(storage, true); } catch (System.Exception) { }
        }

        IEnumerator Stage(int index)
        {
            var part = Campaign.Load().parts.Find(p => p.kind == PartKind.Assault);
            SceneManager.LoadScene(part.stages[index].scene);
            yield return null;
            yield return null;
            gm = GameManager.I;
        }

        Player JoinPad()
        {
            var pad = InputSystem.AddDevice<Gamepad>();
            devices.Add(pad);
            return gm.Players.Join(new InputDevice[] { pad });
        }

        static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }

        // ------------------------------------------------------------------ data
        [Test]
        public void TheCampaignHasPartTwoInFiveStagesAndTwoEndings()
        {
            var c = Campaign.Load();
            Assert.NotNull(c, "Campaign.asset is in the content library");
            var wall = c.parts.Find(p => p.kind == PartKind.Defense);
            var undertow = c.parts.Find(p => p.kind == PartKind.Assault);
            Assert.NotNull(wall); Assert.NotNull(undertow);
            Assert.Less(c.parts.IndexOf(wall), c.parts.IndexOf(undertow), "Part I comes first");
            Assert.AreEqual(5, undertow.stages.Count, "five stages: five founders, five petals, five shards");
            Assert.AreEqual(5, c.shardCount);
            foreach (var st in undertow.stages)
            {
                Assert.IsTrue(Application.CanStreamedLevelBeLoaded(st.scene), $"{st.scene} is in the build");
                Assert.IsNotEmpty(st.epigraph, $"{st.title} has its line");
                foreach (var id in st.enemies) Assert.NotNull(GameDatabase.Load().Enemy(id), $"{st.title}: no creature '{id}'");
            }
            Assert.AreEqual(ObjectiveKind.Heart, undertow.stages[4].objective, "the last stage is the Heart");
            Assert.IsFalse(c.Ending("strike").needsShards);
            Assert.IsTrue(c.Ending("spiral").needsShards, "laying down the Crest takes every shard");
            Assert.NotNull(GameDatabase.Load().Enemy("tidemother"));
        }

        // ------------------------------------------------------------------ a stage
        [UnityTest]
        public IEnumerator AStageSceneStartsAnExpeditionOnTheSeabed()
        {
            yield return Stage(0);
            Assert.IsTrue(gm.Assault);
            Assert.IsNull(gm.Wall, "no wall out here");
            Assert.AreEqual(GameState.Playing, gm.State, "a stage starts its run straight away");
            Assert.IsTrue(Arena.Seabed);
            Assert.AreEqual(0f, Arena.GroundY(120));
            var p = JoinPad();
            yield return Frames(3);
            Assert.GreaterOrEqual(p.Level, gm.Expedition.Part.startLevel, "newcomers start at the part's level");
            Assert.IsFalse(p.GunsLocked, "sailors come armed");
            Assert.AreEqual(4, gm.Expedition.objectives.Count);
            StringAssert.Contains("Tide", gm.Level.StatusLine());
            Assert.IsNotNull(Object.FindAnyObjectByType<ShardStone>(), "every stage hides a shard");
            Assert.IsNotNull(gm.Expedition.exit);
            Assert.IsFalse(gm.Expedition.exit.Open);
        }

        [UnityTest]
        public IEnumerator SpiresFallAndTheWayOpens()
        {
            yield return Stage(0);
            JoinPad();
            yield return Frames(2);
            foreach (var o in gm.Expedition.objectives)
            {
                var spire = (BroodSpire)o;
                Assert.IsTrue(spire.Alive);
                spire.Shot(new DamageInfo { amount = 2000, explosion = true, point = spire.transform.position });
                Assert.IsTrue(spire.Done, "explosions fell a spire fast");
            }
            yield return null;
            Assert.IsTrue(gm.Expedition.Cleared);
            Assert.IsTrue(gm.Expedition.exit.Open);
        }

        [UnityTest]
        public IEnumerator ABeaconCatchesWhileSomeoneStandsByIt()
        {
            yield return Stage(1);
            var p = JoinPad();
            yield return Frames(2);
            var beacon = (Beacon)gm.Expedition.objectives[0];
            Time.timeScale = 10;
            for (float t = 0; !beacon.Done && t < 40; t += Time.deltaTime)
            {
                p.transform.position = beacon.transform.position + new Vector3(1.5f, 0, 0);
                foreach (var e in GameManager.Enemies.ToArray()) if (e != null && !e.Dead) e.Die(default);
                yield return null;
            }
            Time.timeScale = 1;
            Assert.IsTrue(beacon.Done, "the beacon caught");
            Assert.IsFalse(gm.Expedition.Cleared, "two more to go");
        }

        [UnityTest]
        public IEnumerator OnlyBlastsBreakTheWreckWall()
        {
            yield return Stage(2);
            JoinPad();
            yield return Frames(2);
            var wall = (WreckBarrier)gm.Expedition.objectives[0];
            wall.Shot(new DamageInfo { amount = 999, point = wall.transform.position });
            Assert.IsFalse(wall.Done, "bullets don't move a wall of ships");
            wall.Shot(new DamageInfo { amount = 50, explosion = true, point = wall.transform.position });
            wall.Shot(new DamageInfo { amount = 50, explosion = true, point = wall.transform.position });
            Assert.IsFalse(wall.Done, "a chain of blasts counts once");
            for (int i = 1; i < wall.blasts; i++)
            {
                for (float t = 0; t < .5f; t += Time.deltaTime) yield return null;
                wall.Shot(new DamageInfo { amount = 50, explosion = true, point = wall.transform.position });
            }
            Assert.IsTrue(wall.Done);
            Assert.Greater(gm.Expedition.memorialField.size.x, 1, "the Hull Graveyard keeps a field for the wrecks of earlier runs");
        }

        [UnityTest]
        public IEnumerator TheCourtyardOnlyCountsWhileHeld()
        {
            yield return Stage(3);
            var p = JoinPad();
            yield return Frames(2);
            var ring = (HoldRing)gm.Expedition.objectives[0];
            Assert.AreEqual(gm.Expedition.Stage.holdSeconds, ring.seconds, "the stage sets how long");
            ring.seconds = 2;
            p.transform.position = ring.transform.position + new Vector3(30, 0, -20);
            yield return Frames(30);
            Assert.IsFalse(ring.Done, "nobody inside: no time passes");
            for (float t = 0; !ring.Done && t < 10; t += Time.deltaTime)
            {
                p.transform.position = ring.transform.position;
                yield return null;
            }
            Assert.IsTrue(ring.Done);
        }

        [UnityTest]
        public IEnumerator AShardIsKeptForGood()
        {
            yield return Stage(0);
            JoinPad();
            yield return Frames(2);
            var stone = Object.FindAnyObjectByType<ShardStone>();
            Assert.NotNull(stone.Prompt(gm.Players.All[0]));
            stone.Interact(gm.Players.All[0]);
            Assert.IsNull(stone.Prompt(gm.Players.All[0]), "taken");
            Assert.IsTrue(Legacy.Load().HasShard(0));
            Assert.AreEqual(1, Legacy.Load().ShardCount);
        }

        [UnityTest]
        public IEnumerator TheGateTakesTheTeamToTheNextStage()
        {
            yield return Stage(0);
            var p = JoinPad();
            yield return Frames(2);
            gm.AddSandDollars(123);
            int money = gm.SandDollars, level = p.Level;
            gm.Expedition.NextStage();
            yield return Frames(4);
            gm = GameManager.I;
            Assert.AreEqual(1, gm.Expedition.StageIndex, "on to the Kelp Cathedral");
            Assert.AreEqual(GameState.Playing, gm.State);
            Assert.AreEqual(1, gm.Players.Count, "the player came along");
            Assert.AreEqual(money, gm.SandDollars, "and so did the money");
            Assert.AreEqual(level, gm.Players.All[0].Level);
        }

        [UnityTest]
        public IEnumerator TheTidemotherRisesAndTheHeartIsAnswered()
        {
            yield return Stage(4);
            var p = JoinPad();
            yield return Frames(2);
            var altar = (HeartAltar)gm.Expedition.objectives[0];
            Assert.AreEqual(HeartAltar.Phase.Waiting, altar.State);
            Assert.IsNull(gm.Expedition.exit, "the last stage has no way on");
            p.transform.position = altar.transform.position + new Vector3(0, 0, -15);
            yield return Frames(3);
            Assert.AreEqual(HeartAltar.Phase.Fight, altar.State);
            Assert.NotNull(altar.Boss);
            Assert.AreEqual("tidemother", altar.Boss.Def.id);
            altar.Boss.Die(default);
            yield return Frames(2);
            Assert.AreEqual(HeartAltar.Phase.Choosing, altar.State);
            Assert.IsTrue(altar.Offers("strike"));
            Assert.IsFalse(altar.Offers("spiral"), "not without every shard");
            altar.Choose("strike");
            Assert.AreEqual(HeartAltar.Phase.Chosen, altar.State);
            var legacy = Legacy.Load();
            Assert.AreEqual(1, legacy.strikes);
            Assert.IsFalse(legacy.spiral);
            Assert.AreEqual(5, legacy.bestStage);
            yield return Frames(4);
            Assert.AreEqual("TideWall", SceneManager.GetActiveScene().name, "home to the menu");
        }

        [UnityTest]
        public IEnumerator WithEveryShardTheCrestCanBeLaidDown()
        {
            for (int i = 0; i < 5; i++) Legacy.FindShard(i);
            yield return Stage(4);
            var p = JoinPad();
            yield return Frames(2);
            var altar = (HeartAltar)gm.Expedition.objectives[0];
            p.transform.position = altar.transform.position + new Vector3(0, 0, -15);
            yield return Frames(3);
            altar.Boss.Die(default);
            yield return Frames(2);
            Assert.IsTrue(altar.Offers("spiral"));
            int runs = Legacy.Load().runs;
            altar.Choose("spiral");
            var legacy = Legacy.Load();
            Assert.IsTrue(legacy.spiral);
            Assert.AreEqual(runs + 1, legacy.Tally, "the tally stops at the peace");
            Legacy.Record(1, 1, 0);
            Assert.AreEqual(runs + 1, Legacy.Load().Tally, "and stays stopped");
        }

        [UnityTest]
        public IEnumerator TheMainMenuStartsPartTwoInItsFirstStage()
        {
            SceneManager.LoadScene("TideWall");
            yield return Frames(2);
            gm = GameManager.I;
            Assert.AreEqual(GameState.MainMenu, gm.State);
            var save = Expedition.NewSave(gm, 2, 0);
            Assert.AreNotEqual("wall", save.part);
            gm.StartRun(save, 2);
            yield return Frames(4);
            gm = GameManager.I;
            Assert.IsTrue(gm.Assault);
            Assert.AreEqual(0, gm.Expedition.StageIndex);
            Assert.AreEqual(gm.Expedition.Part.startSandDollars, gm.SandDollars, "the expedition's purse");
        }
    }
}

// TideWall 3D — play-mode tests for Tide Caller: who gets the conch, what the Caller can and can't do, and who wins.
// Saves go to a temporary Storage.Root, never the player's files.
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TideWall.Tests
{
    public class TideCallerTests
    {
        readonly System.Collections.Generic.List<InputDevice> devices = new();
        GameManager gm;
        Lighthouse level;
        string storage;

        IEnumerator Boot()
        {
            Time.timeScale = 1;
            storage = Path.Combine(Application.temporaryCachePath, "tidewall-tests-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(storage);
            Storage.Root = storage;
            Settings.Reload();
            StoryCard.Disabled = true;
            DebugTools.ResetAll();
            GameManager.Pending = new RunRequest { save = new SaveData { slot = 0, part = Lighthouse.PartId, versus = true } };
            SceneManager.LoadScene(Lighthouse.SceneName);
            yield return null;
            yield return null;
            gm = GameManager.I;
            level = gm.Lighthouse;
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

        Player JoinPad()
        {
            var pad = InputSystem.AddDevice<Gamepad>();
            devices.Add(pad);
            return gm.Players.Join(new InputDevice[] { pad });
        }

        static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }

        ConchShell Conch() => Object.FindAnyObjectByType<ConchShell>();

        /// <summary>A spot in deep water, far from the sailors on the hill.</summary>
        static Vector3 DeepWater() => new(0, 0, 60);

        [UnityTest]
        public IEnumerator TheFirstTideWaitsForSomeoneToTakeTheConch()
        {
            yield return Boot();
            Assert.IsTrue(level.Versus);
            var a = JoinPad();
            yield return Frames(3);
            float left = level.StateLeft;
            yield return new WaitForSeconds(.3f);
            Assert.AreEqual(left, level.StateLeft, .01f, "the clock holds");

            Assert.IsNotNull(Conch());
            Conch().Interact(a);
            Assert.IsNull(level.Caller, "nobody to fight yet");

            var b = JoinPad();
            yield return Frames(2);
            Conch().Interact(b);
            Assert.AreEqual(b, level.Caller);
            Assert.IsTrue(b.IsCaller);
            Assert.IsNull(Conch(), "it's been taken");
            yield return new WaitForSeconds(.3f);
            Assert.Less(level.StateLeft, gm.DB.lastLight.firstSlackSeconds, "the clock runs");
        }

        [UnityTest]
        public IEnumerator TheCallerCantBeTouchedAndDoesntCountForAWipe()
        {
            yield return Boot();
            var sailor = JoinPad();
            var caller = JoinPad();
            yield return Frames(2);
            level.SetCaller(caller);
            yield return Frames(2);
            Assert.IsFalse(caller.IsTargetable);
            Assert.IsTrue(sailor.IsTargetable);
            Assert.IsFalse(caller.GetComponent<CharacterController>().enabled, "no body in the way of shots");

            sailor.TakeDamage(99999, null, "test");
            yield return Frames(2);
            Assert.IsTrue(sailor.IsDowned);
            Assert.AreEqual(GameState.GameOver, gm.State, "the only sailor's down: the Caller wins");
            Assert.AreEqual("YOU DROWNED THE LIGHT", level.GameOverTitle(caller));
            Assert.AreEqual("THE TIDE CALLER WINS", level.GameOverTitle(sailor));
        }

        [UnityTest]
        public IEnumerator CallingCostsBrineAndFollowsTheRules()
        {
            yield return Boot();
            var sailor = JoinPad();
            var caller = JoinPad();
            yield return Frames(2);
            level.SetCaller(caller);
            yield return Frames(2);
            var at = DeepWater();
            Assert.IsNotNull(level.CallBlocked(at), "not in slack water");
            level.StartFloodForTests();
            Assert.IsNull(level.CallBlocked(at), level.CallBlocked(at));

            int before = GameManager.Enemies.Count;
            float brine = level.Brine;
            int cost = level.Pick.cost;
            Assert.IsTrue(level.TryCall(at, true));
            Assert.AreEqual(before + 1, GameManager.Enemies.Count);
            Assert.AreEqual(brine - cost, level.Brine, .2f);

            Assert.IsFalse(level.TryCall(new Vector3(0, 0, 8), true), "on dry land");
            var near = at; near.x += 3;
            sailor.Place(new Vector3(near.x + 2, Arena.GroundAt(near), near.z));
            Assert.IsNotNull(level.CallBlocked(near), "right on top of a sailor");
            sailor.Place(level.SpawnPoint(0));

            // Out of brine, nothing comes
            while (level.TryCall(at, false)) { }
            Assert.Less(level.Brine, level.Pick.cost + .5f);
            int count = GameManager.Enemies.Count;
            Assert.IsFalse(level.TryCall(at, true));
            Assert.AreEqual(count, GameManager.Enemies.Count);
        }

        [UnityTest]
        public IEnumerator TheRosterGrowsAndTheAiStaysOutWhileThereIsACaller()
        {
            yield return Boot();
            JoinPad();
            var caller = JoinPad();
            yield return Frames(2);
            level.SetCaller(caller);
            yield return Frames(2);
            int first = level.Roster().Count;
            Assert.Greater(first, 0);
            var pick = level.Pick;
            level.CyclePick(1);
            if (first > 1) Assert.AreNotEqual(pick, level.Pick);

            level.StartFloodForTests();
            yield return new WaitForSeconds(3f);
            Assert.AreEqual(0, GameManager.Enemies.Count, "the sea only sends what the Caller calls");
        }

        [UnityTest]
        public IEnumerator TakingOverACreatureDrivesItAndDyingLetsGo()
        {
            yield return Boot();
            JoinPad();
            var caller = JoinPad();
            yield return Frames(2);
            level.SetCaller(caller);
            yield return Frames(2);
            level.StartFloodForTests();
            var at = DeepWater();
            caller.Place(at);
            yield return null;
            level.TryCall(at, true);
            var e = GameManager.Enemies[0];
            yield return Frames(2);
            Assert.IsTrue(level.TryRide(caller.transform.position));
            Assert.AreEqual(e, level.Ridden);
            Assert.AreEqual(caller, e.Rider);

            // It holds still until told otherwise: the AI's let go of it
            var held = e.transform.position;
            yield return new WaitForSeconds(.5f);
            Assert.Less(Vector3.Distance(new Vector3(held.x, 0, held.z), new Vector3(e.transform.position.x, 0, e.transform.position.z)), 1.5f);

            e.Die(default);
            yield return Frames(3);
            Assert.IsNull(level.Ridden, "back up above");
            Assert.Greater(level.RideLeft, 0, "with a moment before the next one");
        }

        [UnityTest]
        public IEnumerator TheCallerSeesTheWholeBattlefieldAndCanLookCloser()
        {
            yield return Boot();
            JoinPad();
            var caller = JoinPad();
            yield return Frames(2);
            level.SetCaller(caller);
            yield return Frames(2);
            Assert.IsTrue(caller.Cam.Camera.orthographic, "isometric by default");
            Assert.AreEqual(35.26f, caller.Cam.transform.eulerAngles.x, 1);
            caller.SwitchView();
            yield return Frames(2);
            Assert.IsFalse(caller.Cam.Camera.orthographic);
            level.SetCaller(null);
            yield return Frames(2);
            Assert.IsFalse(caller.Cam.Camera.orthographic, "back to a sailor's eyes");
        }

        [UnityTest]
        public IEnumerator BothSidesGetPaidForTheirBestMoments()
        {
            yield return Boot();
            var sailor = JoinPad();
            JoinPad();
            var caller = JoinPad();
            yield return Frames(2);
            level.SetCaller(caller);
            yield return Frames(2);
            level.StartFloodForTests();

            // A sailor down feeds the sea
            float brine = level.Brine;
            sailor.TakeDamage(99999, null, "test");
            Assert.Greater(level.Brine, brine);

            // Killing the driven creature throws the Caller out
            var at = DeepWater();
            caller.Place(at);
            level.TryCall(at, true);
            var e = GameManager.Enemies[0];
            Assert.IsTrue(level.TryRide(at));
            int drops = Object.FindObjectsByType<SandDollarPickup>().Length;
            e.Die(default);
            yield return Frames(2);
            Assert.AreEqual(gm.DB.lastLight.rideKnockout, level.RideLeft, .5f);
            Assert.Greater(Object.FindObjectsByType<SandDollarPickup>().Length, drops, "a bounty on the sand");
        }

        [UnityTest]
        public IEnumerator ARallySendsThemToTheMarker()
        {
            yield return Boot();
            JoinPad();
            var caller = JoinPad();
            yield return Frames(2);
            level.SetCaller(caller);
            yield return Frames(2);
            level.StartFloodForTests();
            var at = DeepWater();
            level.TryCall(at, true);
            var e = GameManager.Enemies[0];
            var target = at + Vector3.right * 12;
            float brine = level.Brine;
            Assert.IsTrue(level.TryRally(target));
            Assert.IsTrue(e.Ordered);
            Assert.Less(level.Brine, brine);
            Assert.IsFalse(level.TryRally(target), "it has to come back first");
            yield return new WaitForSeconds(1.5f);
            float start = Vector3.Distance(at, target);
            var now = e.transform.position; now.y = 0;
            Assert.Less(Vector3.Distance(now, target), start, "heading for it, not the tower");
        }

        [UnityTest]
        public IEnumerator TheSailorsWinIfTheLightLastsAndTheLogIsLeftAlone()
        {
            yield return Boot();
            var sailor = JoinPad();
            var caller = JoinPad();
            yield return Frames(2);
            level.SetCaller(caller);
            yield return Frames(2);
            int runs = Legacy.Load().lightRuns;
            for (int i = 0; i < gm.DB.lastLight.callerTides; i++) level.StartFloodForTests();
            Assert.AreEqual(gm.DB.lastLight.callerTides, level.Wave);
            foreach (var e in GameManager.Enemies.ToArray()) e.Die(default);   // the last tide's boss
            yield return Frames(3);
            Assert.AreEqual(GameState.GameOver, gm.State);
            Assert.AreEqual(Lighthouse.HeldReason, gm.GameOverReason);
            Assert.AreEqual("THE LIGHT HOLDS", level.GameOverTitle(sailor));
            Assert.AreEqual(runs, Legacy.Load().lightRuns, "a match isn't a Last Light run");
        }

        [UnityTest]
        public IEnumerator IfTheCallerLeavesTheSeaGoesBackToItself()
        {
            yield return Boot();
            JoinPad();
            var caller = JoinPad();
            yield return Frames(2);
            level.SetCaller(caller);
            yield return Frames(2);
            level.StartFloodForTests();
            gm.Players.Remove(caller);
            yield return Frames(3);
            Assert.IsFalse(level.CallerActive);
            Assert.IsNotNull(Conch(), "back on its rock");
            yield return new WaitForSeconds(3f);
            Assert.Greater(GameManager.Enemies.Count, 0, "the usual waves take over");
        }
    }
}

// TideWall 3D — play-mode tests. They boot the real game (GameManager boots itself into the test scene),
// join players with virtual gamepads, and exercise phases, limbs, corpses, food, explosions and every enemy.
// Run: unity test <project> --mode PlayMode   (any Debug.LogError / exception fails the test)
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace TideWall.Tests
{
    public class TideWallTests
    {
        readonly System.Collections.Generic.List<InputDevice> devices = new();
        GameManager gm;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1;
            if (GameManager.I == null) new GameObject("TideWall").AddComponent<GameManager>();
            yield return null;
            gm = GameManager.I;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var d in devices) if (d.added) InputSystem.RemoveDevice(d);
            devices.Clear();
            Object.Destroy(gm.gameObject);
            foreach (var p in Object.FindObjectsByType<Player>()) Object.Destroy(p.gameObject);
            foreach (var e in Object.FindObjectsByType<Enemy>(FindObjectsInactive.Include)) Object.Destroy(e.gameObject);
            foreach (var c in Object.FindObjectsByType<Corpse>()) Object.Destroy(c.gameObject);
            foreach (var c in Object.FindObjectsByType<GasCan>()) Object.Destroy(c.gameObject);
            foreach (var c in Object.FindObjectsByType<FoodPickup>()) Object.Destroy(c.gameObject);
            Time.timeScale = 1;
            yield return null;
        }

        Player JoinPad()
        {
            var pad = InputSystem.AddDevice<Gamepad>();
            devices.Add(pad);
            return gm.Players.Join(new InputDevice[] { pad });
        }

        IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }

        /// <summary>Kill everything as it spawns until the phase changes (at most 300 s of game time, at 20× speed).</summary>
        IEnumerator RunPhase(string id)
        {
            Time.timeScale = 20;
            for (float t = 0; gm.Phases.Current.id == id && t < 300; t += Time.deltaTime)
            {
                foreach (var e in GameManager.Enemies.ToArray()) if (e != null && !e.Dead) e.Die(default);
                yield return null;
            }
            Time.timeScale = 1;
        }

        IEnumerator KillAll()
        {
            for (int guard = 0; guard < 400 && !gm.Waves.Cleared; guard++)
            {
                foreach (var e in GameManager.Enemies.ToArray()) if (e != null && !e.Dead) e.Die(default);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator BootsIntoLobbyWithWorld()
        {
            Assert.AreEqual(GameState.Lobby, gm.State);
            Assert.NotNull(gm.World.sun);
            Assert.AreEqual(gm.DB.wallMaxHp, gm.Wall.MaxHp);
            Assert.GreaterOrEqual(gm.Cycle.phases.Count, 3, "Morning, Evening and Night");
            Assert.NotNull(ModelFactory.Prefab("Crab"), "Blender models are imported");
            yield return null;
        }

        [UnityTest]
        public IEnumerator JoiningStartsTheGameAndSplitsTheScreen()
        {
            var p1 = JoinPad();
            yield return null;
            Assert.AreEqual(GameState.Playing, gm.State);
            Assert.AreEqual("morning", gm.Phases.Current.id);
            Assert.AreEqual(new Rect(0, 0, 1, 1), p1.Cam.Camera.rect);
            var p2 = JoinPad();
            yield return null;
            Assert.AreEqual(2, gm.Players.Count);
            Assert.AreEqual(.5f, p1.Cam.Camera.rect.height, 1e-4, "two players stack top/bottom");
            Assert.AreEqual(.5f, p2.Cam.Camera.rect.height, 1e-4);
            JoinPad(); JoinPad();
            yield return null;
            Assert.AreEqual(4, gm.Players.Count);
            foreach (var p in gm.Players.All) Assert.AreEqual(.25f, p.Cam.Camera.rect.width * p.Cam.Camera.rect.height, 1e-4);
        }

        [UnityTest]
        public IEnumerator GamepadStickMovesThePlayer()
        {
            var p = JoinPad();
            yield return Frames(3);
            var pad = (Gamepad)p.Controls.Devices[0];
            var start = p.transform.position;
            // Hold the stick for half a second of game time (headless frames are very short).
            for (float t = 0; t < .5f; t += Time.deltaTime)
            {
                InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = new Vector2(1, 0) });
                yield return null;
            }
            Assert.Greater(p.transform.position.x - start.x, .5f, "left stick right moves +X");
        }

        [UnityTest]
        public IEnumerator PhasesCycleAndTheDayAdvances()
        {
            var p = JoinPad();
            yield return null;
            Assert.AreEqual("morning", gm.Phases.Current.id);
            p.Ready = true;                                  // ring the bell
            yield return Frames(2);
            Assert.AreEqual("evening", gm.Phases.Current.id);
            // Fast-forward: kill everything as it spawns until the phase ends (bounded by game time).
            yield return RunPhase("evening");
            Assert.AreEqual("night", gm.Phases.Current.id);
            yield return RunPhase("night");
            Assert.AreEqual("morning", gm.Phases.Current.id);
            Assert.AreEqual(2, gm.Phases.Day);
        }

        [UnityTest]
        public IEnumerator ShootingLimbsOffWeakensAndLowersCorpseValue()
        {
            JoinPad();
            yield return null;
            var crab = Enemy.Spawn(gm.DB.Enemy("crab"), new Vector3(0, 0, 8), 1, -1);
            yield return null;
            var claw = crab.Limbs.Find(l => l.Def.part == "ClawL");
            Assert.NotNull(claw);
            var clawGo = claw.gameObject;
            crab.TakeHit(new DamageInfo { amount = crab.MaxHp * .4f, ignoreArmor = true, dir = Vector3.left }, claw);
            yield return null;
            Assert.Null(clawGo.transform.parent, "the claw flew off");
            Assert.NotNull(clawGo.GetComponent<Rigidbody>());
            Assert.False(crab.Dead);
            var eyes = crab.Limbs.Find(l => l.Def.part == "Eyes");
            crab.TakeHit(new DamageInfo { amount = 999, ignoreArmor = true }, eyes);
            yield return null;
            Assert.True(crab.Dead, "losing the eyes kills");
            var corpse = crab.GetComponent<Corpse>();
            Assert.NotNull(corpse);
            Assert.Less(corpse.Value, gm.DB.Enemy("crab").corpseValue, "a crab missing parts is worth less");
        }

        [UnityTest]
        public IEnumerator CorpsesSellAtTheFishmongerInTheMorning()
        {
            var p = JoinPad();
            yield return null;
            var e = Enemy.Spawn(gm.DB.Enemy("drowner"), new Vector3(2, 0, 6), 1, -1);
            yield return null;
            e.Die(default);
            var corpse = e.GetComponent<Corpse>();
            p.PickUp(corpse);
            Assert.AreEqual(1, p.Carrying.Count);
            int before = gm.Coins;
            Object.FindAnyObjectByType<Fishmonger>().Interact(p);
            yield return null;
            Assert.AreEqual(before + corpse.Value, gm.Coins);
            Assert.AreEqual(0, p.Carrying.Count);
        }

        [UnityTest]
        public IEnumerator CondimentsAndMysterySauce()
        {
            var p = JoinPad();
            yield return null;
            var hotdog = gm.DB.Food("hotdog");
            p.Eat(hotdog, gm.DB.condiments.Find(c => c.id == "ketchup"));
            Assert.True(p.Statuses.Has(StatusType.Rage));
            p.Eat(hotdog, gm.DB.condiments.Find(c => c.id == "mustard"));
            Assert.True(p.Statuses.Has(StatusType.Speed));
            var mystery = gm.DB.condiments.Find(c => c.id == "mystery");
            for (int i = 0; i < 300; i++) p.Eat(hotdog, mystery);     // every outcome path, no exceptions
            float chance = gm.DB.sauceJackpotChance;
            gm.DB.sauceJackpotChance = 1;
            int before = gm.Coins;
            p.Eat(hotdog, mystery);
            gm.DB.sauceJackpotChance = chance;
            Assert.AreEqual(before + gm.DB.sauceJackpotCoins, gm.Coins, "the 0.01% jackpot pays out");
            yield return Frames(30);   // camera effects (upside down, first person, fisheye) run without errors
        }

        [UnityTest]
        public IEnumerator GasCansExplodeAndHurtEnemies()
        {
            JoinPad();
            yield return null;
            var drowner = Enemy.Spawn(gm.DB.Enemy("drowner"), new Vector3(5, 0, 10), 1, -1);
            var can = GasCan.Spawn(new Vector3(5.5f, 0, 10));
            yield return null;
            float hp = drowner.Hp;
            can.Hit(Element.Fire, null);     // fire always sets it off
            yield return null;
            Assert.True(can == null, "the can is gone");
            Assert.Less(drowner.Hp, hp);
        }

        [UnityTest]
        public IEnumerator EveryEnemyBehaviourRuns()
        {
            JoinPad();
            JoinPad();
            yield return null;
            var players = gm.Players.All;
            players[0].transform.position = new Vector3(-4, 0, 6);   // on the beach, as bait
            int i = 0;
            foreach (var def in gm.DB.enemies)
            {
                float z = def.behaviour == EnemyBehaviour.Shark ? 30 : 12;
                Enemy.Spawn(def, new Vector3(-20 + i * 3, Arena.GroundY(z), z), 3, -1);
                i++;
            }
            Assert.AreEqual(gm.DB.enemies.Count, GameManager.Enemies.Count);
            float t = 0;
            while (t < 6) { t += Time.deltaTime; yield return null; }   // let them walk, fly, swim, grab and sting
            // Any exception or error log during those frames fails the test.
        }
    }
}

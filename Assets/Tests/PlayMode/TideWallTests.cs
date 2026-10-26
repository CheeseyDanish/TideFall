// TideWall 3D — play-mode tests. They boot the real game (GameManager boots itself into the test scene),
// join players with virtual gamepads, and exercise phases, limbs, corpses, food, explosions, every enemy,
// first-person view, settings and rebinding, saves, and online hosting (with a raw TCP client).
// Settings and saves go to a temporary folder (Storage.Root): tests never touch the player's real files.
// Run: unity test <project> --mode PlayMode   (any Debug.LogError / exception fails the test)
using System.Collections;
using System.IO;
using System.Net.Sockets;
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
        string storage;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1;
            storage = Path.Combine(Application.temporaryCachePath, "tidewall-tests-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(storage);
            Storage.Root = storage;
            Settings.Reload();
            if (GameManager.I == null) new GameObject("TideWall").AddComponent<GameManager>();
            yield return null;
            gm = GameManager.I;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Net.Shutdown(null);
            Rebinder.Cancel();
            foreach (var d in devices) if (d.added) InputSystem.RemoveDevice(d);
            devices.Clear();
            if (GameManager.I != null) Object.Destroy(GameManager.I.gameObject);
            foreach (var p in Object.FindObjectsByType<Player>()) Object.Destroy(p.gameObject);
            foreach (var e in Object.FindObjectsByType<Enemy>(FindObjectsInactive.Include)) Object.Destroy(e.gameObject);
            foreach (var c in Object.FindObjectsByType<Corpse>()) Object.Destroy(c.gameObject);
            foreach (var c in Object.FindObjectsByType<GasCan>()) Object.Destroy(c.gameObject);
            foreach (var c in Object.FindObjectsByType<FoodPickup>()) Object.Destroy(c.gameObject);
            Time.timeScale = 1;
            yield return null;
            try { Directory.Delete(storage, true); } catch (System.Exception) { }
        }

        Gamepad AddPad()
        {
            var pad = InputSystem.AddDevice<Gamepad>();
            devices.Add(pad);
            return pad;
        }

        Player JoinPad() => gm.Players.Join(new InputDevice[] { AddPad() });

        IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }

        /// <summary>Hold a gamepad state for some game time (headless frames are very short).</summary>
        IEnumerator Hold(Gamepad pad, GamepadState state, float seconds)
        {
            for (float t = 0; t < seconds; t += Time.unscaledDeltaTime)
            {
                InputSystem.QueueStateEvent(pad, state);
                yield return null;
            }
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return null;
        }

        IEnumerator Tap(Gamepad pad, GamepadButton button)
        {
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(button));
            yield return null;
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return null;
        }

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

        // ------------------------------------------------------------------ boot, joining, phases
        [UnityTest]
        public IEnumerator BootsIntoTheMainMenuWithWorld()
        {
            Assert.AreEqual(GameState.MainMenu, gm.State);
            Assert.NotNull(gm.World.sun);
            Assert.NotNull(gm.World.keep, "the keep (main menu room) is built");
            Assert.AreEqual(gm.DB.wallMaxHp, gm.Wall.MaxHp);
            Assert.GreaterOrEqual(gm.Cycle.phases.Count, 3, "Morning, Evening and Night");
            Assert.IsTrue(gm.Cycle.phases.Exists(p => p.saveOnStart), "some phase autosaves");
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
        public IEnumerator PhasesCycleAndTheDayAdvances()
        {
            var p = JoinPad();
            yield return null;
            Assert.AreEqual("morning", gm.Phases.Current.id);
            p.Ready = true;                                  // ring the bell
            yield return Frames(2);
            Assert.AreEqual("evening", gm.Phases.Current.id);
            yield return RunPhase("evening");
            Assert.AreEqual("night", gm.Phases.Current.id);
            yield return RunPhase("night");
            Assert.AreEqual("morning", gm.Phases.Current.id);
            Assert.AreEqual(2, gm.Phases.Day);
        }

        // ------------------------------------------------------------------ first person
        [UnityTest]
        public IEnumerator StartsInFirstPersonAndTheViewButtonSwitches()
        {
            var p = JoinPad();
            var pad = (Gamepad)p.Controls.Devices[0];
            yield return Frames(3);
            Assert.IsTrue(p.FirstPerson, "first person by default");
            Assert.Less(Vector3.Distance(p.Cam.transform.position, p.EyePos), .3f, "the camera is at the player's eyes");
            Assert.AreEqual(0, p.Cam.Camera.cullingMask & (1 << (Player.BodyLayer + p.Index)), "your own body is hidden in first person");
            yield return Tap(pad, GamepadButton.RightStick);   // R3 = View
            yield return Frames(30);
            Assert.IsFalse(p.FirstPerson, "R3 switches to top-down");
            Assert.Greater(p.Cam.transform.position.y, p.EyePos.y + 5, "top-down camera is high above");
            Assert.AreNotEqual(0, p.Cam.Camera.cullingMask & (1 << (Player.BodyLayer + p.Index)), "top-down shows your sailor");
            p.Statuses.Add(StatusType.SwapView, 5);           // ??? sauce: wrong view
            Assert.IsTrue(p.FirstPerson);
        }

        [UnityTest]
        public IEnumerator SticksLookAndMoveRelativeToTheView()
        {
            var p = JoinPad();
            var pad = (Gamepad)p.Controls.Devices[0];
            yield return Frames(3);
            float yaw0 = p.Yaw;
            yield return Hold(pad, new GamepadState { rightStick = new Vector2(1, 0) }, .5f);
            Assert.Greater(Mathf.DeltaAngle(yaw0, p.Yaw), 20, "right stick turns the view");
            var forward = Quaternion.Euler(0, p.Yaw, 0) * Vector3.forward;
            var start = p.transform.position;
            yield return Hold(pad, new GamepadState { leftStick = new Vector2(0, 1) }, .5f);
            var moved = p.transform.position - start; moved.y = 0;
            Assert.Greater(moved.magnitude, .5f, "left stick moves");
            Assert.Greater(Vector3.Dot(moved.normalized, forward), .8f, "forward is where you look");
        }

        // ------------------------------------------------------------------ pause, settings, rebinding
        [UnityTest]
        public IEnumerator PauseMenuPausesOfflineAndSettingsSave()
        {
            var p = JoinPad();
            var pad = (Gamepad)p.Controls.Devices[0];
            yield return Frames(2);
            yield return Tap(pad, GamepadButton.Start);
            yield return Frames(2);
            Assert.NotNull(p.OpenedMenu);
            Assert.IsTrue(p.OpenedMenu.System);
            Assert.IsTrue(gm.Paused);
            Assert.AreEqual(0, Time.timeScale);
            // Into settings, then nudge the master volume down with the stick
            p.OpenMenu(Menus.Settings(() => Menus.Pause(p), p));
            var master = p.OpenedMenu.Items.Find(i => i.Label == "Master volume");
            float before = Settings.Current.master;
            p.OpenedMenu.Selected = p.OpenedMenu.Items.IndexOf(master);
            yield return Hold(pad, new GamepadState { leftStick = new Vector2(-1, 0) }, .05f);
            Assert.Less(Settings.Current.master, before, "left on a slider lowers it");
            Settings.Reload();
            Assert.Less(Settings.Current.master, before, "and it was saved to settings.json");
            yield return Tap(pad, GamepadButton.Start);
            yield return Frames(2);
            Assert.IsNull(p.OpenedMenu);
            Assert.IsFalse(gm.Paused);
            Assert.AreEqual(1, Time.timeScale);
        }

        [UnityTest]
        public IEnumerator RebindingAControllerButtonWorksInGame()
        {
            var p = JoinPad();
            var pad = (Gamepad)p.Controls.Devices[0];
            yield return Frames(2);
            Rebinder.Start("Reload", PlayerControls.GamepadGroup, "", "Reload");
            Assert.IsNotNull(Rebinder.Listening);
            yield return Frames(2);
            yield return Hold(pad, new GamepadState().WithButton(GamepadButton.DpadUp), .3f);
            yield return Frames(5);
            Assert.IsNull(Rebinder.Listening, "the press finished the rebind");
            Assert.AreEqual("<Gamepad>/dpad/up", Settings.Current.Override("Reload/Gamepad/"));
            // The live player's controls picked it up
            bool reloaded = false;
            p.Controls.Map.FindAction("Reload").performed += _ => reloaded = true;
            yield return Tap(pad, GamepadButton.DpadUp);
            yield return Frames(2);
            Assert.IsTrue(reloaded, "d-pad up now reloads");
            Rebinder.ResetAll();
            Assert.IsNull(Settings.Current.Override("Reload/Gamepad/"));
        }

        // ------------------------------------------------------------------ saves
        [UnityTest]
        public IEnumerator SavesAndLoadsTheTeamsProgress()
        {
            gm.StartRun(null, 2);
            var p = JoinPad();
            yield return Frames(2);
            gm.AddCoins(500);
            var upgrade = gm.DB.upgrades.Find(u => u.kind == UpgradeKind.GunDamage);
            gm.Team.Apply(upgrade);
            p.GiveGun("scattergun");
            gm.Wall.Damage(100, Vector3.zero);
            Assert.IsTrue(gm.CanSaveNow, "the morning allows saving");
            Assert.IsTrue(gm.SaveNow());
            Assert.IsTrue(File.Exists(Path.Combine(storage, "saves", "slot2.json")));
            int coins = gm.Coins; float wall = gm.Wall.Hp; float dmg = gm.Team.GunDamage;

            // Start over and load it
            Object.Destroy(gm.gameObject);
            foreach (var pl in Object.FindObjectsByType<Player>()) Object.Destroy(pl.gameObject);
            yield return null;
            new GameObject("TideWall").AddComponent<GameManager>();
            yield return null;
            gm = GameManager.I;
            Assert.AreEqual(GameState.MainMenu, gm.State);
            var save = SaveSystem.Latest();
            Assert.NotNull(save);
            Assert.AreEqual(2, save.slot);
            gm.StartRun(save, save.slot);
            var again = JoinPad();
            yield return Frames(2);
            Assert.AreEqual(coins, gm.Coins);
            Assert.AreEqual(wall, gm.Wall.Hp, .01f);
            Assert.AreEqual(dmg, gm.Team.GunDamage, 1e-4);
            Assert.AreEqual(1, gm.Team.Level(upgrade.id));
            Assert.IsTrue(again.OwnsGun("scattergun"), "player 1's weapons come back");
            Assert.AreEqual("scattergun", again.Gun.id);
        }

        // ------------------------------------------------------------------ online
        [UnityTest]
        public IEnumerator HostAcceptsAnOnlinePlayerAndRunsTheirInput()
        {
            const int port = 47791;
            Assert.NotNull(Net.StartHost(port, "Test Host"));
            gm.StartRun(null, 0);
            var local = JoinPad();
            yield return Frames(2);

            using var tcp = new TcpClient();
            tcp.Connect("127.0.0.1", port);
            var conn = new NetConnection(99, tcp);
            var hello = NetIO.Begin(Msg.Hello); hello.Write(NetProtocol.Version); hello.Write("Tester");
            conn.Send(hello.Bytes());
            var join = NetIO.Begin(Msg.Join); join.Write(7); join.Write("Test pad");
            conn.Send(join.Bytes());

            int assigned = -1, clientId = -1, snapshots = 0, playersInSnapshot = 0;
            for (float t = 0; t < 5 && (assigned < 0 || snapshots == 0); t += Time.unscaledDeltaTime)
            {
                while (conn.Inbox.TryDequeue(out var data))
                {
                    var r = new BinaryReader(new MemoryStream(data));
                    var type = (Msg)r.ReadByte();
                    if (type == Msg.Welcome) clientId = r.ReadInt32();
                    if (type == Msg.Assigned) { Assert.AreEqual(7, r.ReadInt32()); assigned = r.ReadInt32(); }
                    if (type == Msg.Snapshot && assigned >= 0)
                    {
                        r.ReadByte(); r.ReadInt32(); r.ReadInt32(); r.ReadSingle(); r.ReadInt32(); r.ReadSingle(); r.ReadSingle();
                        r.ReadInt32(); r.ReadInt32(); r.ReadInt32();
                        playersInSnapshot = r.ReadByte();
                        snapshots++;
                    }
                }
                yield return null;
            }
            Assert.Greater(clientId, 0, "welcomed");
            Assert.GreaterOrEqual(assigned, 0, "got a player slot");
            Assert.AreEqual(2, playersInSnapshot, "snapshots carry both players");
            var remote = gm.Players.ByIndex(assigned);
            Assert.NotNull(remote);
            Assert.AreEqual(PlayerKind.Remote, remote.Kind);
            Assert.AreEqual("Tester", remote.Nick);
            Assert.IsNull(remote.Cam, "remote players have no camera on the host");

            // Their machine moves them and pulls the trigger
            int ammo = remote.Ammo;
            var at = remote.transform.position + new Vector3(1.5f, 0, 1);
            var input = new NetInput
            {
                playerIndex = assigned, pos = at, yaw = 0, pitch = 5, firstPerson = true,
                pressed = (ushort)(1 << (int)Btn.Fire), held = (ushort)(1 << (int)Btn.Fire),
                aim = new AimSample { ray = new Ray(at + Vector3.up * 1.6f, Vector3.forward) },
            };
            var w = NetIO.Begin(Msg.Input); w.Write((byte)1); w.Write(input);
            conn.Send(w.Bytes());
            for (float t = 0; t < 2 && remote.Ammo == ammo; t += Time.unscaledDeltaTime) yield return null;
            Assert.Less(remote.Ammo, ammo, "the host fired their gun");
            Assert.Less(Vector3.Distance(remote.transform.position, at), .5f, "and moved them where their machine said");

            // Leaving removes their player
            conn.Close();
            for (float t = 0; t < 3 && gm.Players.ByIndex(assigned) != null; t += Time.unscaledDeltaTime) yield return null;
            Assert.IsNull(gm.Players.ByIndex(assigned), "disconnecting removes the player");
            Assert.NotNull(local);
        }

        // ------------------------------------------------------------------ combat and creatures
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
            yield return Frames(30);   // camera effects (upside down, wrong view, fisheye) run without errors
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

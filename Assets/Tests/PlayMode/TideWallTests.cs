// TideWall 3D — play-mode tests. They boot the real game (GameManager boots itself into the test scene),
// join players with virtual gamepads, and exercise phases, limbs, corpses, food, explosions, every enemy,
// first-person view, settings and rebinding, saves, online hosting (with a raw TCP client), and the roguelike
// systems: tidal cards (and exclusives), levels/stats/respec/mods, breakers, breaches, the powerplant, sand dollars.
// Settings and saves go to a temporary folder (Storage.Root): tests never touch the player's real files.
// Run: unity test <project> --mode PlayMode   (any Debug.LogError / exception fails the test)
using System.Collections;
using System.Linq;
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
            // The real scene: its GameManager, content library and level
            UnityEngine.SceneManagement.SceneManager.LoadScene("TideWall");
            yield return null;
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
            foreach (var c in Object.FindObjectsByType<SandDollarPickup>()) Object.Destroy(c.gameObject);
            foreach (var c in Object.FindObjectsByType<Breaker>()) Object.Destroy(c.gameObject);
            foreach (var c in Object.FindObjectsByType<Structure>()) Object.Destroy(c.gameObject);
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
            Assert.AreEqual(gm.DB.wallSegmentHp * gm.Wall.Sections.Count, gm.Wall.MaxHp, .01f);
            Assert.GreaterOrEqual(gm.Wall.Sections.Count, 18, "the doubled beach has a long wall");
            Assert.AreEqual(gm.DB.plantMaxHp, gm.Plant.MaxHp);
            Assert.GreaterOrEqual(gm.Cycle.phases.Count, 3, "Morning, Evening and Night");
            Assert.IsTrue(gm.Cycle.phases.Exists(p => p.saveOnStart), "some phase autosaves");
            Assert.NotNull(Content.Model("Crab"), "Blender models are imported");
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
            Assert.IsFalse(p.FirstPerson, "R3 switches to third person");
            var back = p.Cam.transform.position - p.EyePos;
            Assert.That(back.magnitude, Is.InRange(1.5f, 5f), "third person: the camera sits just behind the sailor");
            Assert.Less(Vector3.Dot(back, p.Cam.transform.forward), 0, "behind, looking the same way");
            Assert.AreNotEqual(0, p.Cam.Camera.cullingMask & (1 << (Player.BodyLayer + p.Index)), "third person shows your sailor");
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
            yield return Hold(pad, new GamepadState().WithButton(GamepadButton.DpadLeft), .05f);
            Assert.Less(Settings.Current.master, before, "d-pad left on a slider lowers it");
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
            gm.AddSandDollars(500);
            var upgrade = gm.DB.upgrades.Find(u => u.kind == UpgradeKind.GunDamage);
            gm.Team.Apply(upgrade);
            p.GiveGun("scattergun");
            p.FitAttachment(gm.DB.Attachment("brake"));
            gm.Wall.Damage(100, Vector3.zero);
            p.GainXp(400);
            p.SpendPoint("vigor");
            Assert.IsTrue(gm.CanSaveNow, "the morning allows saving");
            Assert.IsTrue(gm.SaveNow());
            Assert.IsTrue(File.Exists(Path.Combine(storage, "saves", "slot2.json")));
            int sandDollars = gm.SandDollars; float wall = gm.Wall.Hp; float dmg = gm.Team.GunDamage; int level = p.Level;

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
            Assert.AreEqual(sandDollars, gm.SandDollars);
            Assert.AreEqual(wall, gm.Wall.Hp, .01f);
            Assert.AreEqual(dmg, gm.Team.GunDamage, 1e-4);
            Assert.AreEqual(1, gm.Team.Level(upgrade.id));
            Assert.IsTrue(again.OwnsGun("scattergun"), "player 1's weapons come back");
            Assert.AreEqual("scattergun", again.Gun.id);
            Assert.IsTrue(again.HasAttachment("scattergun", "brake"), "and the attachments on them");
            Assert.AreEqual(level, again.Level, "levels come back");
            Assert.AreEqual(1, again.Points("vigor"), "and stat points");
        }

        // ------------------------------------------------------------------ online
        /// <summary>A bare client for the online tests: a NetLink to the host, and what it has received.</summary>
        class FakeClient
        {
            public NetLink link;
            public int host = -1, clientId = -1, assigned = -1, snapshots, playersInSnapshot;
            public uint lastSnapshot;
            public string bye;
            public byte[] challengeSalt, challengeNonce;
            public readonly byte[] nonce = NetAuth.Nonce();
            uint seq;

            public FakeClient(int port) { link = NetLink.Connect("127.0.0.1", (ushort)port, out _); }

            public void Send(Channel ch, BinaryWriter w) => link.Send(host, ch, w.Bytes());

            public void Pump()
            {
                link.Update();
                while (link.Inbox.Count > 0)
                {
                    var e = link.Inbox.Dequeue();
                    if (e.Kind == NetLink.EventKind.Connected)
                    {
                        host = e.Peer;
                        var hello = NetIO.Begin(Msg.Hello); hello.Write(NetProtocol.Version); hello.Write("Tester"); hello.Write(nonce);
                        Send(Channel.Reliable, hello);
                    }
                    if (e.Kind != NetLink.EventKind.Data) continue;
                    var r = new BinaryReader(new MemoryStream(e.Data));
                    var type = (Msg)r.ReadByte();
                    if (type == Msg.Welcome) clientId = r.ReadInt32();
                    if (type == Msg.Bye) bye = r.ReadString();
                    if (type == Msg.Challenge) { challengeSalt = r.ReadBytes(16); challengeNonce = r.ReadBytes(16); }
                    if (type == Msg.Assigned) { r.ReadInt32(); assigned = r.ReadInt32(); }
                    if (type == Msg.Snapshot)
                    {
                        lastSnapshot = r.ReadUInt32();
                        r.ReadInt16();   // scene
                        r.ReadByte(); r.ReadInt32(); r.ReadInt32(); r.ReadSingle(); r.ReadInt32(); r.ReadInt32();   // state, phase, day, elapsed, sand dollars, pearls
                        int sections = r.ReadByte();
                        for (int i = 0; i < sections; i++) { r.ReadSingle(); r.ReadSingle(); }
                        r.ReadSingle(); r.ReadSingle();                                    // powerplant
                        int activeCards = r.ReadByte();
                        for (int i = 0; i < activeCards; i++) { r.ReadString(); r.ReadByte(); }
                        r.ReadByte();                                                      // no vote in the morning
                        r.ReadInt32(); r.ReadInt32(); r.ReadInt32(); r.ReadInt32(); r.ReadString();   // waves, enemies left, run wave, preview
                        playersInSnapshot = r.ReadByte();
                        snapshots++;
                    }
                }
            }

            /// <summary>Send commands for our player: `frames` frames of `dt` each, with these controls.</summary>
            public void Commands(int frames, float dt, Vector2 move, ushort pressed, ushort held)
            {
                var w = NetIO.Begin(Msg.Input);
                w.Write(lastSnapshot); w.Write((byte)1);
                w.Write((byte)assigned);
                w.Write(new AimSample { ray = new Ray(new Vector3(0, 1.6f, 0), Vector3.forward) });
                w.Write((byte)frames);
                for (int i = 0; i < frames; i++) new NetCommand { seq = ++seq, dt = dt, move = move, yaw = 0, pitch = 5, firstPerson = true, pressed = i == 0 ? pressed : (ushort)0, held = held }.Write(w);
                Send(Channel.Unreliable, w);
                link.Update();
            }
        }

        IEnumerator Until(FakeClient c, System.Func<bool> done, float seconds)
        {
            for (float t = 0; t < seconds && !done(); t += Time.unscaledDeltaTime) { c.Pump(); yield return null; }
            c.Pump();
        }

        [UnityTest]
        public IEnumerator HostRunsAnOnlinePlayersCommandsAndOwnsTheirMovement()
        {
            const int port = 47791;
            Assert.NotNull(Net.StartHost(port, "Test Host", "", out _));
            gm.StartRun(null, 0);
            var local = JoinPad();
            yield return Frames(2);

            var c = new FakeClient(port);
            yield return Until(c, () => c.clientId > 0, 5);
            Assert.Greater(c.clientId, 0, "welcomed");
            var join = NetIO.Begin(Msg.Join); join.Write(7); join.Write("Test pad");
            c.Send(Channel.Reliable, join);
            yield return Until(c, () => c.assigned >= 0 && c.snapshots > 0, 5);
            Assert.GreaterOrEqual(c.assigned, 0, "got a player slot");
            Assert.AreEqual(2, c.playersInSnapshot, "snapshots carry both players");
            var remote = gm.Players.ByIndex(c.assigned);
            Assert.NotNull(remote);
            Assert.AreEqual(PlayerKind.Remote, remote.Kind);
            Assert.AreEqual("Tester", remote.Nick);
            Assert.IsNull(remote.Cam, "remote players have no camera on the host");

            // Their machine pulls the trigger and walks forward; the host moves them itself
            gm.Waves.RunWave = gm.DB.gunsUnlockWave;
            int ammo = remote.Ammo;
            var start = remote.transform.position;
            // One command per frame for half a second of real time, like a real client
            bool first = true;
            for (float t = 0; t < .5f; t += Time.unscaledDeltaTime)
            {
                c.Commands(1, Time.unscaledDeltaTime, new Vector2(0, 1), first ? (ushort)(1 << (int)Btn.Fire) : (ushort)0, (ushort)(1 << (int)Btn.Fire));
                first = false;
                yield return null;
                c.Pump();
            }
            yield return Until(c, () => remote.Ammo < ammo, 2);
            Assert.Less(remote.Ammo, ammo, "the host fired their gun");
            float moved = Vector3.Distance(Flat(remote.transform.position), Flat(start));
            Assert.Greater(moved, 1f, "and walked them forward by their commands");
            Assert.Less(moved, gm.DB.playerSpeed * 1.5f * .5f + .5f, "no further than the commands allow");

            // A sped-up client: ten seconds of walking sent at once only moves them as far as real time allows
            start = remote.transform.position;
            for (int k = 0; k < 10; k++) c.Commands(24, NetCommand.MaxDt, new Vector2(0, -1), 0, 0);
            yield return Frames(3);
            moved = Vector3.Distance(Flat(remote.transform.position), Flat(start));
            Assert.Less(moved, gm.DB.playerSpeed * 1.2f, "commands can't outrun real time");

            // Leaving removes their player
            c.link.Dispose();
            for (float t = 0; t < 15 && gm.Players.ByIndex(c.assigned) != null; t += Time.unscaledDeltaTime) yield return null;
            Assert.IsNull(gm.Players.ByIndex(c.assigned), "disconnecting removes the player");
            Assert.NotNull(local);
        }

        static Vector3 Flat(Vector3 v) => new(v.x, 0, v.z);

        [UnityTest]
        public IEnumerator RoomPasswordsAreCheckedWithoutSendingThem()
        {
            const int port = 47792;
            Assert.NotNull(Net.StartHost(port, "Test Host", "seashell", out _));
            gm.StartRun(null, 0);
            yield return Frames(2);

            // Wrong password: turned away
            var wrong = new FakeClient(port);
            yield return Until(wrong, () => wrong.challengeNonce != null, 5);
            Assert.NotNull(wrong.challengeNonce, "a password game challenges first");
            Assert.AreEqual(-1, wrong.clientId, "and doesn't welcome anyone yet");
            var bad = NetIO.Begin(Msg.Auth); bad.Write(NetAuth.Proof(NetAuth.Key("barnacle", wrong.challengeSalt), wrong.challengeNonce, wrong.nonce));
            wrong.Send(Channel.Reliable, bad);
            yield return Until(wrong, () => wrong.bye != null, 5);
            Assert.AreEqual("Wrong password.", wrong.bye);
            Assert.AreEqual(-1, wrong.clientId);
            wrong.link.Dispose();
            yield return new WaitForSecondsRealtime(2.5f);   // the host closes that connection, and a wrong guess locks the address out for 2 s

            // Right password: welcomed (the proof is an HMAC of the host's nonce, never the password itself)
            var right = new FakeClient(port);
            yield return Until(right, () => right.challengeNonce != null, 5);
            var good = NetIO.Begin(Msg.Auth); good.Write(NetAuth.Proof(NetAuth.Key("seashell", right.challengeSalt), right.challengeNonce, right.nonce));
            right.Send(Channel.Reliable, good);
            yield return Until(right, () => right.clientId > 0, 5);
            Assert.Greater(right.clientId, 0, "the right password gets in");
            right.link.Dispose();
        }

        // ------------------------------------------------------------------ roguelike
        [UnityTest]
        public IEnumerator TidalCardsAreVotedLastSeveralWavesAndExclusivesNeedTheirMode()
        {
            var p1 = JoinPad();
            yield return null;
            gm.Cards.OfferDraft();
            Assert.NotNull(gm.Cards.Draft);
            Assert.AreEqual(gm.DB.cardChoices, gm.Cards.Draft.Count);
            Assert.IsTrue(gm.Cards.Draft.TrueForAll(c => c.exclusive == CardExclusive.None), "alone offline: no exclusive cards");
            Assert.AreEqual(TidalCards.MenuTag, p1.OpenedMenu?.Title, "the vote opens for each player");
            var picked = gm.Cards.Draft[1];
            gm.Cards.Vote(p1, 1);                        // the only player voted: resolves now
            Assert.IsNull(gm.Cards.Draft);
            var active = gm.Cards.Actives.Find(a => a.def == picked);
            Assert.NotNull(active, "the voted card is active");
            for (int w = 1; w < picked.waves; w++) gm.Cards.WaveFinished();
            Assert.IsTrue(gm.Cards.Actives.Exists(a => a.def == picked), "it lasts several waves");
            gm.Cards.WaveFinished();
            Assert.IsFalse(gm.Cards.Actives.Exists(a => a.def == picked), "then it ebbs away");

            // Two people on one screen: split-screen cards can appear, online ones still can't
            var p2 = JoinPad();
            yield return null;
            Assert.IsTrue(gm.Cards.SplitEligible);
            Assert.IsFalse(gm.Cards.OnlineEligible);
            bool sawSplit = false;
            for (int i = 0; i < 60; i++)
            {
                gm.Cards.OfferDraft();
                Assert.IsFalse(gm.Cards.Draft.Exists(c => c.exclusive == CardExclusive.Online));
                sawSplit |= gm.Cards.Draft.Exists(c => c.exclusive == CardExclusive.SplitScreen);
                gm.Cards.Vote(p1, 0); gm.Cards.Vote(p2, 0);
            }
            Assert.IsTrue(sawSplit, "split-screen cards show up when people share a screen");
            // Screen Swap: same-screen players hit harder (it's in DamageMult) and viewports shuffle
            gm.Cards.Activate(gm.DB.Card("screenswap"));
            Assert.IsTrue(gm.Cards.SharesScreen(p1));
        }

        [UnityTest]
        public IEnumerator LevelsStatsRespecAndWeaponMods()
        {
            var p = JoinPad();
            yield return null;
            float hp0 = p.MaxHp;
            p.GainXp(gm.DB.XpForLevel(1) + 1);
            Assert.AreEqual(2, p.Level);
            Assert.AreEqual(1, p.StatPoints);
            p.SpendPoint("vigor");
            Assert.AreEqual(hp0 + gm.DB.Stat("vigor").perPoint, p.MaxHp, .01f, "Vigor raises max HP");
            Assert.AreEqual(0, p.StatPoints);
            gm.AddSandDollars(1000);
            Assert.IsTrue(p.Respec());
            Assert.AreEqual(1, p.StatPoints, "respec refunds the point");
            Assert.AreEqual(hp0, p.MaxHp, .01f);
            // Every 5 levels: a weapon mod
            while (p.Level < gm.DB.modEveryLevels) p.GainXp(p.XpNext);
            Assert.NotNull(p.ModOffer, "a mod is offered at level 5");
            Assert.AreEqual(gm.DB.modChoices, p.ModOffer.Count);
            var mag = gm.DB.Mod("extmag");
            if (!p.ModOffer.Contains(mag)) p.ModOffer[0] = mag;   // make the test deterministic
            int before = p.MagSize;
            p.TakeMod(mag);
            Assert.Greater(p.MagSize, before, "Extended Mag fits the gun in hand");
            Assert.IsNull(p.ModOffer);
            p.OpenMenu(Menus.Stats(p));
            yield return Frames(3);   // the Stats menu builds and renders
        }

        [UnityTest]
        public IEnumerator CreaturesRideBreakersToTheShore()
        {
            JoinPad();
            yield return null;
            var b = Breaker.Launch(0, 14, Arena.ShoreZ + 30, Arena.ShoreZ + 1, 30);
            var crab = Enemy.Spawn(gm.DB.Enemy("crab"), b.RiderPosition(0, 0), 1, -1);
            b.Carry(crab, 0, 0, canWipeOut: false);
            Assert.IsTrue(crab.Riding);
            for (float t = 0; t < 4 && !b.Broken; t += Time.deltaTime) yield return null;
            Assert.IsTrue(b.Broken, "the breaker reached the shore and broke");
            Assert.IsFalse(crab.Riding, "and dropped its rider");
            Assert.Less(crab.transform.position.z, Arena.ShoreZ + 4, "in the surf");
            Assert.AreEqual(1, crab.Wet, .01f, "soaked");
        }

        [UnityTest]
        public IEnumerator BreachedWallLetsCreaturesReachThePowerplant()
        {
            JoinPad();
            gm.Players.All[0].Place(new Vector3(-40, 0, -20));   // out of the way
            yield return null;
            var section = gm.Wall.Sections.Find(sec => Mathf.Abs(sec.x) < 3);
            gm.Wall.Damage(section.max + 1, new Vector3(section.x, 0, 1));
            Assert.IsTrue(section.breached);
            Assert.AreEqual(1, gm.Wall.BreachCount);
            float plantHp = gm.Plant.Hp;
            var drowner = Enemy.Spawn(gm.DB.Enemy("drowner"), new Vector3(section.x + 3, 0, 6), 1, -1);
            Time.timeScale = 4;
            for (float t = 0; t < 40 && gm.Plant.Hp >= plantHp; t += Time.deltaTime) yield return null;
            Time.timeScale = 1;
            Assert.Less(gm.Plant.Hp, plantHp, "it walked through the breach and hit the powerplant");
            gm.Team.Apply(gm.DB.upgrades.Find(u => u.kind == UpgradeKind.RepairWall));
            Assert.AreEqual(0, gm.Wall.BreachCount, "repairs rebuild breached sections");
            Assert.NotNull(drowner);
        }

        [UnityTest]
        public IEnumerator MeltdownEndsTheRunAndDeletesItsSave()
        {
            gm.StartRun(null, 1);
            JoinPad();
            yield return Frames(2);
            Assert.NotNull(SaveSystem.Load(1), "the morning autosaved");
            gm.Plant.Damage(gm.Plant.MaxHp * 2, gm.Plant.transform.position);
            Assert.AreEqual(GameState.GameOver, gm.State);
            Assert.IsNull(SaveSystem.Load(1), "roguelike: the run's save is gone");
            Assert.AreEqual(1, Legacy.Load().runs, "and the legacy record counted it");
        }

        [UnityTest]
        public IEnumerator CreaturesDropSandDollarsThatSlideToPlayers()
        {
            var p = JoinPad();
            yield return null;
            p.Place(new Vector3(0, 0, 8));   // (moves the CharacterController properly)
            var crab = Enemy.Spawn(gm.DB.Enemy("crab"), new Vector3(1, 0, 9), 1, -1);
            yield return null;
            int before = gm.SandDollars;
            crab.Die(default);
            Assert.Greater(Object.FindObjectsByType<SandDollarPickup>().Length, 0, "sand dollars scatter");
            for (float t = 0; t < 4 && gm.SandDollars == before; t += Time.deltaTime) yield return null;
            Assert.Greater(gm.SandDollars, before, "and slide into the player's pocket");
            Assert.NotNull(Ui.SandDollarIcon, "the money has an icon");
        }

        // ------------------------------------------------------------------ this round: menus, melee-first, loot, defences
        [UnityTest]
        public IEnumerator YouCanWalkWithAMenuOpenAndItClosesWhenYouLeave()
        {
            var p = JoinPad();
            var pad = (Gamepad)p.Controls.Devices[0];
            yield return Frames(2);
            p.OpenMenu(Menus.Shop(p, Menus.ShopTab.Guns, p.transform.position));
            Assert.AreEqual(MenuLayout.Grid, p.OpenedMenu.Layout);
            var start = p.transform.position;
            yield return Hold(pad, new GamepadState { leftStick = new Vector2(0, -1) }, .6f);
            Assert.Greater(Vector3.Distance(p.transform.position, start), .5f, "the left stick still walks with the shop open");
            yield return Tap(pad, GamepadButton.DpadRight);
            Assert.AreEqual(1, p.OpenedMenu?.Selected ?? -1, "the d-pad moves through the item cards");
            yield return Tap(pad, GamepadButton.Y);
            Assert.AreEqual(1, p.OpenedMenu?.Tab ?? -1, "Swap switches the shop tab");
            p.Place(start + new Vector3(0, 0, 12));
            yield return Frames(3);
            Assert.IsNull(p.OpenedMenu, "walking away from the stall closes it");
        }

        [UnityTest]
        public IEnumerator TheFirstWavesAreMeleeOnlyAndGullsComeLater()
        {
            var p = JoinPad();
            var pad = (Gamepad)p.Controls.Devices[0];
            yield return Frames(2);
            gm.Waves.RunWave = 1;
            Assert.IsTrue(p.GunsLocked);
            int ammo = p.Ammo;
            yield return Hold(pad, new GamepadState { rightTrigger = 1 }, .1f);
            yield return Frames(10);
            Assert.AreEqual(ammo, p.Ammo, "the trigger doesn't fire a holstered gun");
            Assert.GreaterOrEqual(p.SwingProgress, -1);   // it swung the melee weapon instead (or just finished)
            gm.Waves.RunWave = gm.DB.gunsUnlockWave;
            Assert.IsFalse(p.GunsLocked, "guns unlock at wave 4");
            Assert.GreaterOrEqual(gm.DB.Enemy("gull").minWave, 5, "gulls wait until wave 5");
            // Plan many early nights: no gulls in the first four waves
            var evening = gm.Cycle.phases.Find(ph => ph.waves > 0);
            for (int i = 0; i < 20; i++)
            {
                gm.Waves.RunWave = 0;
                gm.Waves.StartPhase(evening, 3);
                Assert.IsFalse(gm.Waves.Preview.Contains("Gull"), "no gulls planned before wave 5");
            }
            gm.Waves.StartPhase(gm.Cycle.phases[0], 1);
        }

        /// <summary>Step a reload game until `until` is true (or it's done), in 1/120 s ticks.</summary>
        static void Step(ReloadGame g, ref int ammo, int mag, System.Func<bool> until, float max = 10)
        {
            for (float t = 0; t < max && !g.Done && !until(); t += 1 / 120f) g.Update(1 / 120f, ref ammo, mag);
        }

        [UnityTest]
        public IEnumerator ReloadMinigamesAreOptionalAndRewardSkill()
        {
            yield return null;
            var db = gm.DB;
            foreach (ReloadStyle style in System.Enum.GetValues(typeof(ReloadStyle)))
            {
                Assert.IsNotNull(db.reloads.Find(r => r.style == style), $"{style} has its numbers");
                // Doing nothing: the reload finishes in its normal time, with a full magazine
                var idle = ReloadGame.Begin(db.Reload(style), 1.5f, 6);
                int a = 0; float spent = 0;
                while (!idle.Update(1 / 60f, ref a, 6) && spent < 5) spent += 1 / 60f;
                Assert.IsTrue(idle.Done, $"{style} completes with no input");
                Assert.Less(spent, 2f, $"{style} takes about the reload time when ignored");
                Assert.IsFalse(idle.Perfect, $"{style} isn't Perfect by doing nothing");
                if (idle.PerRound) Assert.AreEqual(6, a, $"{style} loads every round");
            }
            foreach (var g in db.guns) Assert.IsNotNull(db.reloads.Find(r => r.style == g.reloadStyle), $"{g.displayName} has a reload minigame");

            // Zone: in the green is Perfect; outside it jams (takes longer)
            int ammo = 0;
            var zone = ReloadGame.Begin(db.Reload(ReloadStyle.Zone), 1.5f, 6);
            Step(zone, ref ammo, 6, () => zone.Marker > (zone.Zone.x + zone.Zone.y) / 2);
            zone.Press(ref ammo, 6);
            Assert.IsTrue(zone.Done && zone.Perfect, "a tap in the green finishes early, Perfect");
            var miss = ReloadGame.Begin(db.Reload(ReloadStyle.Zone), 1.5f, 6);
            miss.Press(ref ammo, 6);
            Assert.IsTrue(miss.Jammed && miss.Need > 1.5f && !miss.Done, "a tap outside it jams");

            // Bolt: both zones in order
            var bolt = ReloadGame.Begin(db.Reload(ReloadStyle.Bolt), 2f, 5);
            Step(bolt, ref ammo, 5, () => bolt.Marker > (bolt.Zone.x + bolt.Zone.y) / 2);
            bolt.Press(ref ammo, 5);
            Assert.AreEqual(1, bolt.Step, "bolt open");
            Step(bolt, ref ammo, 5, () => bolt.Marker > (bolt.Zone2.x + bolt.Zone2.y) / 2);
            bolt.Press(ref ammo, 5);
            Assert.IsTrue(bolt.Perfect, "bolt closed: Perfect");

            // Rhythm: a tap on every beat loads every round by skill
            ammo = 0;
            var rhythm = ReloadGame.Begin(db.Reload(ReloadStyle.Rhythm), 1.4f, 6);
            for (int i = 0; i < 6 && !rhythm.Done; i++)
            {
                Step(rhythm, ref ammo, 6, () => rhythm.BeatT >= i * rhythm.R.beat);
                rhythm.Press(ref ammo, 6);
                rhythm.Update(.001f, ref ammo, 6);
            }
            Assert.AreEqual(6, ammo, "six beats, six rounds");
            Assert.IsTrue(rhythm.Done && rhythm.Perfect, "every round on the beat is Perfect");

            // Shells: a tap outside the zone slips
            ammo = 0;
            var shells = ReloadGame.Begin(db.Reload(ReloadStyle.Shells), 2f, 6);
            shells.Press(ref ammo, 6);
            Assert.AreEqual("SLIP", shells.Word);
            Step(shells, ref ammo, 6, () => shells.RoundFraction > (shells.Zone.x + shells.Zone.y) / 2);
            shells.Press(ref ammo, 6);
            Assert.AreEqual(1, ammo, "a tap in the zone loads a shell");

            // Pressure: hold, release in the green; hold too long and it vents
            var pressure = ReloadGame.Begin(db.Reload(ReloadStyle.Pressure), 2f, 40);
            pressure.Press(ref ammo, 40);
            Step(pressure, ref ammo, 40, () => pressure.Needle > (pressure.Zone.x + pressure.Zone.y) / 2);
            pressure.Release();
            Assert.IsTrue(pressure.Perfect, "released in the green");
            var vent = ReloadGame.Begin(db.Reload(ReloadStyle.Pressure), 2f, 40);
            vent.Press(ref ammo, 40);
            Step(vent, ref ammo, 40, () => vent.Jammed);
            Assert.IsTrue(vent.Jammed, "held too long: it vents");

            // Mash: fast enough is Perfect, and quicker than waiting
            var mash = ReloadGame.Begin(db.Reload(ReloadStyle.Mash), 2f, 10);
            float took = 0;
            while (!mash.Done && took < 5) { mash.Press(ref ammo, 10); mash.Update(.1f, ref ammo, 10); took += .1f; }
            Assert.IsTrue(mash.Perfect && took < 1.2f, "mashing charges it fast");
        }

        [UnityTest]
        public IEnumerator PlayersReloadWithTheMinigameAndPerfectsHitHarder()
        {
            var p = JoinPad();
            var pad = (Gamepad)p.Controls.Devices[0];
            yield return Frames(2);
            gm.Waves.RunWave = gm.DB.gunsUnlockWave;
            p.GiveGun("smg");
            Assert.AreEqual(ReloadStyle.Zone, p.Gun.reloadStyle);
            // Empty the magazine: the reload starts by itself
            for (float t = 0; t < 8 && p.ReloadView == null; t += Time.deltaTime)
            {
                InputSystem.QueueStateEvent(pad, new GamepadState { rightTrigger = 1 });
                yield return null;
            }
            InputSystem.QueueStateEvent(pad, new GamepadState());
            Assert.IsNotNull(p.ReloadView, "an empty gun starts its reload minigame");
            Assert.GreaterOrEqual(p.ReloadProgress, 0);
            var g = p.ReloadView;
            for (float t = 0; t < 4 && g.Marker < (g.Zone.x + g.Zone.y) / 2; t += Time.deltaTime) yield return null;
            yield return Tap(pad, GamepadButton.West);
            Assert.IsTrue(g.Perfect, "Reload in the green: Perfect");
            Assert.AreEqual(p.MagSize, p.Ammo, "and the magazine is full");
            Assert.Greater(p.PerfectBonus, 0, "a Perfect reload adds damage until the magazine is empty");
        }

        [UnityTest]
        public IEnumerator KillCombosChainPayAndRunOut()
        {
            var p = JoinPad();
            yield return Frames(2);
            int sd = gm.SandDollars;
            for (int i = 0; i < 4; i++)
            {
                var crab = Enemy.Spawn(gm.DB.Enemy("crab"), new Vector3(i * 2, 0, 12), 1, -1);
                yield return null;
                crab.Die(new DamageInfo { source = p });
            }
            Assert.AreEqual(4, p.Combo, "four quick kills: x4");
            Assert.AreEqual(4, p.BestCombo);
            Assert.GreaterOrEqual(gm.SandDollars - sd, (1 + 2 + 3) * gm.DB.comboSandDollars, "the combo pays out");
            Time.timeScale = 10;
            for (float t = 0; t < gm.DB.comboWindow + 1; t += Time.deltaTime) yield return null;
            Time.timeScale = 1;
            Assert.AreEqual(0, p.Combo, "the chain breaks when you stop killing");
        }

        [UnityTest]
        public IEnumerator TheWallIsOnePieceThatBreaksIntoRubble()
        {
            JoinPad();
            yield return null;
            var wall = gm.Wall;
            var mid = wall.Sections.Find(sec => Mathf.Abs(sec.x - 10) < 3);
            var left = wall.Sections.Find(sec => Mathf.Abs(sec.x - (mid.x - Arena.SegmentLength)) < .1f);
            var right = wall.Sections.Find(sec => Mathf.Abs(sec.x - (mid.x + Arena.SegmentLength)) < .1f);
            Assert.IsFalse(wall.Sections.Exists(sec => sec.capL.activeSelf || sec.capR.activeSelf || sec.rubble.activeSelf), "an intact wall shows no broken ends");
            Assert.AreEqual(2, Object.FindObjectsByType<Transform>().Count(t => t.name.StartsWith("WallGate")), "a gatehouse over each sally port");
            wall.Damage(mid.max + 1, new Vector3(mid.x, 0, 1));
            Assert.IsTrue(mid.breached && mid.rubble.activeSelf && !mid.t.gameObject.activeSelf, "the breached section collapses into rubble");
            Assert.IsTrue(left.capR.activeSelf && right.capL.activeSelf, "its neighbours show broken ends");
            wall.Repair(1);
            Assert.IsTrue(!mid.breached && mid.t.gameObject.activeSelf && !mid.rubble.activeSelf && !left.capR.activeSelf, "repaired: one piece again");
        }

        [Test]
        public void OnlineInputIsValidated()
        {
            var r = new RemoteInput();
            r.Receive(new NetCommand { seq = 1, dt = .016f, move = new Vector2(float.NaN, 0) });
            Assert.AreEqual(0u, r.Received, "a command with NaN in it is ignored");
            r.Receive(new NetCommand { seq = 2, dt = 5, move = new Vector2(5, 0), pitch = 200 });
            Assert.IsTrue(r.Next(out var c, out _));
            Assert.AreEqual(1, c.move.magnitude, .001f, "movement is clamped");
            Assert.LessOrEqual(c.pitch, 89, "so is looking");
            Assert.LessOrEqual(c.dt, NetCommand.MaxDt, "and a command's time step");
            r.Receive(new NetCommand { seq = 2, dt = .016f });
            Assert.IsFalse(r.Next(out _, out _), "repeats (commands are sent several times) run once");
            r.ReceiveAim(new AimSample { ray = new Ray(new Vector3(1e9f, 0, 0), Vector3.forward) });
            Assert.LessOrEqual(r.Aim.ray.origin.x, 150, "aim stays inside the world");
        }

        [UnityTest]
        public IEnumerator LootFliesToTheKillerAndCanBeIntercepted()
        {
            var killer = JoinPad();
            var mate = JoinPad();
            yield return Frames(2);
            killer.Place(new Vector3(-12, 0, 10));
            mate.Place(new Vector3(-6, 0, 10));   // standing between the clam and the killer
            var clam = Enemy.Spawn(gm.DB.Enemy("clam"), new Vector3(0, 0, 10), 1, -1);
            yield return null;
            int sd = gm.SandDollars, pearls = gm.Pearls;
            clam.Die(new DamageInfo { source = killer });
            for (float t = 0; t < 6 && Object.FindObjectsByType<SandDollarPickup>().Length > 0; t += Time.deltaTime) yield return null;
            Assert.Greater(gm.Pearls, pearls, "the giant clam drops golden pearls");
            Assert.Greater(gm.SandDollars, sd, "and sand dollars");
            Assert.AreEqual(0, Object.FindObjectsByType<SandDollarPickup>().Length, "all of it was caught on the way");
        }

        [UnityTest]
        public IEnumerator DefencesAreBuiltAndBlockCreatures()
        {
            var p = JoinPad();
            yield return Frames(2);
            gm.AddSandDollars(1000); gm.AddPearls(5);
            var barricade = gm.DB.Structure("barricade");
            p.StartBuilding(barricade);
            p.SetLook(0, 30);
            p.Place(new Vector3(0, 0, 8));
            yield return Frames(3);
            int sd = gm.SandDollars;
            var pad = (Gamepad)p.Controls.Devices[0];
            yield return Tap(pad, GamepadButton.A);
            yield return Frames(2);
            Assert.AreEqual(1, Structure.All.Count, "the barricade was placed");
            Assert.AreEqual(sd - barricade.sandDollars, gm.SandDollars, "and paid for");
            Assert.IsNotNull(Structure.WhyNot(barricade, Structure.All[0].transform.position), "nothing else fits on top of it");
            Assert.IsNotNull(Structure.WhyNot(gm.DB.Structure("turret"), new Vector3(0, 0, Arena.ShoreZ + 2)), "not in the sea");
            p.StopBuilding();
            // A crab walking into it has to chew through
            var b = Structure.All[0];
            float hp = b.Hp;
            p.Place(new Vector3(-40, 0, -20));
            var crab = Enemy.Spawn(gm.DB.Enemy("crab"), b.transform.position + new Vector3(0, 0, 4), 1, -1);
            Time.timeScale = 4;
            for (float t = 0; t < 20 && b != null && b.Hp >= hp; t += Time.deltaTime) yield return null;
            Time.timeScale = 1;
            Assert.IsTrue(b == null || b.Hp < hp, "the crab attacked the barricade in its way");
            Assert.NotNull(crab);
            // A turret shoots on its own
            var turret = Structure.Build(gm.DB.Structure("turret"), new Vector3(20, 0, 6), 0);
            var drowner = Enemy.Spawn(gm.DB.Enemy("drowner"), new Vector3(20, 0, 14), 1, -1);
            float dh = drowner.Hp;
            for (float t = 0; t < 4 && drowner != null && !drowner.Dead && drowner.Hp >= dh; t += Time.deltaTime) yield return null;
            Assert.IsTrue(drowner == null || drowner.Dead || drowner.Hp < dh, "the harpoon turret hit it");
            Assert.NotNull(turret);
        }

        [UnityTest]
        public IEnumerator VendorsCorpsesAndTheCardVoteOptions()
        {
            JoinPad();
            yield return Frames(2);
            Assert.NotNull(gm.World.chef, "the hotdog chef is at the stand");
            Assert.NotNull(gm.World.fishmonger, "the fishmonger is at the cart");
            // Corpses sit on the sand, not in it
            var croc = Enemy.Spawn(gm.DB.Enemy("croc"), new Vector3(5, 0, 12), 1, -1);
            yield return null;
            croc.Die(default);
            yield return null;
            Assert.GreaterOrEqual(ModelFactory.Bounds(croc.Model.root).min.y, -.05f, "the corpse isn't sunk into the ground");
            // The vote offers the cards plus a random one (and sometimes a re-roll)
            gm.Cards.OfferDraft();
            var p = gm.Players.All[0];
            Assert.AreEqual(MenuLayout.Cards, p.OpenedMenu.Layout);
            Assert.AreEqual(gm.Cards.Draft.Count + (gm.Cards.RerollOffered ? 2 : 1), p.OpenedMenu.Items.Count);
            gm.Cards.Vote(p, gm.Cards.Draft.Count);   // the face-down random card
            Assert.IsNull(gm.Cards.Draft);
            Assert.AreEqual(1, gm.Cards.Actives.Count, "a random card was dealt");
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
            int before = gm.SandDollars;
            Object.FindAnyObjectByType<Fishmonger>().Interact(p);
            yield return null;
            Assert.AreEqual(before + corpse.Value, gm.SandDollars);
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
            int before = gm.SandDollars;
            p.Eat(hotdog, mystery);
            gm.DB.sauceJackpotChance = chance;
            Assert.AreEqual(before + gm.DB.sauceJackpotSandDollars, gm.SandDollars, "the 0.01% jackpot pays out");
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
            players[0].Place(new Vector3(-4, 0, 6));   // on the beach, as bait
            int i = 0;
            foreach (var def in gm.DB.enemies)
            {
                float z = def.behaviour == EnemyBehaviour.Shark ? Arena.ShoreZ + 10 : 12;
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

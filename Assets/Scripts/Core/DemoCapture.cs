// TideWall 3D — automated runs for checking the game without a human at the controls.
//   TideWall.exe -tidewall-capture <folder>                 screenshot tour: main menu, first person, top-down,
//                                                           menus, fights, sauce effects, bestiary, four players
//   TideWall.exe -tidewall-host-demo [seconds]              host an online game (port 7777) with one virtual
//                                                           player and a few enemies, then quit
//   TideWall.exe -tidewall-join <address> -tidewall-capture <folder>
//                                                           join that host as a client, screenshot what the
//                                                           client sees, log TIDEWALL_CLIENT_OK, quit
//   add -tidewall-stage <1-5> to the host to host a Part II stage instead of Part I's evening
//   add -tidewall-relay to the host to host through Unity Relay instead (it logs TIDEWALL_JOIN_CODE <code>),
//   and use -tidewall-join-code <code> instead of -tidewall-join on the client
// Every mode keeps settings, saves and the legacy record in a temporary folder: never the player's files.
using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace TideWall
{
    public class DemoCapture : MonoBehaviour
    {
        string dir, join, joinCode;
        bool relay;
        float hostSeconds = -1;
        int hostStage = -1;
        Gamepad pad1, pad2;
        Vector2 look1;
        bool fire;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Check()
        {
            if (FindAnyObjectByType<DemoCapture>() != null) return;
            var args = Environment.GetCommandLineArgs();
            int c = Array.IndexOf(args, "-tidewall-capture"), h = Array.IndexOf(args, "-tidewall-host-demo"), j = Array.IndexOf(args, "-tidewall-join");
            int jc = Array.IndexOf(args, "-tidewall-join-code");
            if (c < 0 && h < 0) return;
            // Never the player's files: settings, saves and the legacy record (a tour's run can end and be recorded)
            // live in a temporary folder for the whole session
            Storage.Root = Path.Combine(Application.temporaryCachePath, "tidewall-capture-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Storage.Root);
            Settings.Reload();
            Storage.Write("legacy.json", "{\"runs\": 7}");   // a few runs behind us, so the keep wall's tally shows
            var d = new GameObject("DemoCapture").AddComponent<DemoCapture>();
            DontDestroyOnLoad(d.gameObject);
            // Absolute: ScreenCapture resolves a relative path against the data folder, not the working directory
            d.dir = Path.GetFullPath(c >= 0 && c + 1 < args.Length ? args[c + 1] : "Captures");
            if (h >= 0) d.hostSeconds = h + 1 < args.Length && float.TryParse(args[h + 1], out var s) ? s : 60;
            if (j >= 0 && j + 1 < args.Length) d.join = args[j + 1];
            if (jc >= 0 && jc + 1 < args.Length) d.joinCode = args[jc + 1];
            d.relay = Array.IndexOf(args, "-tidewall-relay") >= 0;
            int st = Array.IndexOf(args, "-tidewall-stage");
            if (st >= 0 && st + 1 < args.Length && int.TryParse(args[st + 1], out var stage)) d.hostStage = stage - 1;
        }

        void Update()
        {
            // Hold the virtual sticks/triggers every frame (state events are one-shot).
            if (pad1 != null && pad1.added) InputSystem.QueueStateEvent(pad1, new GamepadState { rightStick = look1, rightTrigger = fire ? 1 : 0 });
        }

        IEnumerator Start()
        {
            if (hostSeconds > 0) return Host();
            if (join != null || joinCode != null) return Join();
            return Tour();
        }

        IEnumerator Tour()
        {
            Directory.CreateDirectory(dir);
            yield return new WaitForSecondsRealtime(2.5f);
            var gm = GameManager.I;
            yield return Shot("01-main-menu");
            gm.Menu.OpenMenu(Menus.Settings(null, null));
            yield return new WaitForSecondsRealtime(.3f);
            yield return Shot("02-main-menu-settings");

            pad1 = InputSystem.AddDevice<Gamepad>();
            pad2 = InputSystem.AddDevice<Gamepad>();
            var p1 = gm.Players.Join(new InputDevice[] { pad1 });
            var p2 = gm.Players.Join(new InputDevice[] { pad2 });
            StoryCard.Close();   // (a first run opens with Part I's card)
            yield return new WaitForSecondsRealtime(3f);
            yield return Shot("03-morning-first-person-two-players");

            while (p2.Level < 5) p2.GainXp(p2.XpNext);   // a level-5 sailor with a mod to choose
            p1.OpenMenu(Menus.Shop(p1, Menus.ShopTab.Guns, p1.transform.position));
            p2.OpenMenu(Menus.Stats(p2));
            yield return new WaitForSecondsRealtime(.6f);
            yield return Shot("04-shop-and-stats");
            p1.OpenMenu(Menus.Hotdogs(p1, p1.transform.position));
            p2.OpenMenu(Menus.Pause(p2));
            yield return new WaitForSecondsRealtime(.4f);
            yield return Shot("04b-hotdogs-and-pause");
            p1.CloseMenu(); p2.CloseMenu();
            // The vendors, and a defence being placed
            gm.AddSandDollars(500); gm.AddPearls(5);
            Teleport(p1, new Vector3(1, 0, -5)); p1.SetLook(200, 10);
            p2.StartBuilding(gm.DB.Structure("turret"));
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Shot("04c-vendor-and-building");
            p2.StopBuilding();
            Structure.Build(gm.DB.Structure("barricade"), new Vector3(-6, 0, 8), 0);
            Structure.Build(gm.DB.Structure("turret"), new Vector3(4, 0, 6), 0);
            Structure.Build(gm.DB.Structure("spikes"), new Vector3(0, 0, 12), 0);

            gm.Phases.Advance();   // evening: the tidal card vote opens
            yield return new WaitForSecondsRealtime(.6f);
            yield return Shot("05a-tidal-card-vote");
            gm.Cards.Vote(p1, 0); gm.Cards.Vote(p2, 0);
            var section = gm.Wall.Sections.Find(sec => Mathf.Abs(sec.x - 10) < 3);
            if (section != null) gm.Wall.Damage(section.max + 1, new Vector3(section.x, 0, 1));   // a breach to show
            Teleport(p1, new Vector3(-3, 0, 4)); Teleport(p2, new Vector3(4, 0, -2));
            foreach (var id in new[] { "crab", "crab", "drowner", "hermit", "jelly", "urchin", "gull" })
                gm.Waves.SpawnExtra(gm.DB.Enemy(id), new Vector3(UnityEngine.Random.Range(-6f, 6f), 0, UnityEngine.Random.Range(10f, 16f)));
            gm.Waves.SpawnExtra(gm.DB.Enemy("shark"), new Vector3(0, -1, Arena.ShoreZ + 8));
            yield return new WaitForSecondsRealtime(4f);
            fire = true;
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Shot("05-evening-fight-first-person");

            // Reload minigames at the reticle (the SMG's green zone, the revolver's beat) and a kill combo
            fire = false;
            p1.GiveGun("smg"); p2.GiveGun("revolver");
            p1.EmptyAndReload(); p2.EmptyAndReload();
            int combo = 0;
            foreach (var e in GameManager.Enemies.ToArray())
                if (e != null && !e.Dead && combo++ < 4) e.Die(new DamageInfo { source = p1 });
            yield return new WaitForSecondsRealtime(.45f);
            yield return Shot("05d-reload-minigames-and-combo");

            // From the wall's tower: breakers rolling in with creatures aboard
            var b = Breaker.Launch(-8, 18, Arena.ShoreZ + 34, Arena.ShoreZ + 1, 7);
            foreach (var id in new[] { "crab", "drowner", "hermit", "croc", "octopus" })
                b.Carry(gm.Waves.SpawnExtra(gm.DB.Enemy(id), b.RiderPosition(UnityEngine.Random.Range(-8f, 8f), 0)), UnityEngine.Random.Range(-8f, 8f), .3f);
            var view = new GameObject("TowerCam").AddComponent<Camera>();
            view.depth = 40; view.fieldOfView = 55;
            Level.SetupCameraSky(view, gm.Phases.SkyColor);
            view.transform.SetPositionAndRotation(new Vector3(-20, 14, -4), Quaternion.LookRotation(new Vector3(-6, 0, 36) - new Vector3(-20, 14, -4)));
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Shot("05b-breakers-rolling-in");
            var plantView = Arena.PlantPos + new Vector3(-15, 7, 11);
            view.transform.SetPositionAndRotation(plantView, Quaternion.LookRotation(Arena.PlantPos + Vector3.up * 2.5f - plantView));
            yield return new WaitForSecondsRealtime(.4f);
            yield return Shot("05c-nuclear-sand-powerplant");
            Destroy(view.gameObject);

            gm.Phases.Advance();   // night (enemies still alive keep fighting)
            p2.SwitchView();       // player 2 goes top-down
            foreach (var id in new[] { "croc", "octopus", "lobster", "turtle", "puffer", "eel", "drowner", "drowner" })
                gm.Waves.SpawnExtra(gm.DB.Enemy(id), new Vector3(UnityEngine.Random.Range(-10f, 10f), 0, UnityEngine.Random.Range(9f, 18f)));
            yield return new WaitForSecondsRealtime(5f);
            yield return Shot("06-night-first-person-and-top-down");

            p2.Statuses.Add(StatusType.UpsideDown, 10);
            p1.Statuses.Add(StatusType.Giant, 10);
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Shot("07-sauce-upside-down-and-giant");
            p2.Statuses.Clear(StatusType.UpsideDown);
            p1.Statuses.Add(StatusType.SwapView, 10);
            yield return new WaitForSecondsRealtime(1f);
            yield return Shot("08-sauce-wrong-view");

            // A close look at the whole bestiary, lined up on the sand.
            fire = false;
            foreach (var e in GameManager.Enemies.ToArray()) if (e) Destroy(e.gameObject);
            gm.Players.All.ForEach(p => p.gameObject.SetActive(false));
            var cam = new GameObject("BestiaryCam").AddComponent<Camera>();
            cam.depth = 50; cam.backgroundColor = new Color(.6f, .78f, .95f); cam.clearFlags = CameraClearFlags.SolidColor;
            int i = 0;
            foreach (var def in gm.DB.enemies)
            {
                if (def.behaviour == EnemyBehaviour.Boss) continue;
                var en = Enemy.Spawn(def, new Vector3(-15 + (i % 7) * 5, 0, 8 + (i / 7) * 5), 1, -1);
                en.enabled = false;   // freeze them for the photo
                en.transform.rotation = Quaternion.Euler(0, 180 + 25, 0);
                i++;
            }
            cam.transform.SetPositionAndRotation(new Vector3(0, 9, -6), Quaternion.Euler(32, 0, 0));
            cam.fieldOfView = 60;
            yield return new WaitForSecondsRealtime(.5f);
            yield return Shot("09-bestiary");
            var boss = Enemy.Spawn(gm.DB.Enemy("glizzy"), new Vector3(0, Arena.GroundY(12), 12), 5, -1);   // on the sand, not under the sea
            boss.enabled = false;
            cam.transform.SetPositionAndRotation(new Vector3(0, 7, 0), Quaternion.Euler(18, 0, 0));
            yield return new WaitForSecondsRealtime(.5f);
            yield return Shot("10-glizzy-boss");
            Destroy(cam.gameObject);
            gm.Players.All.ForEach(p => p.gameObject.SetActive(true));

            var pad3 = InputSystem.AddDevice<Gamepad>(); var pad4 = InputSystem.AddDevice<Gamepad>();
            gm.Players.Join(new InputDevice[] { pad3 }); gm.Players.Join(new InputDevice[] { pad4 });
            yield return new WaitForSecondsRealtime(2f);
            yield return Shot("11-four-players");
            yield return Undertow(gm);
            Debug.Log("TIDEWALL_CAPTURE_DONE");
            Application.Quit();
        }

        /// <summary>Part II: each stage in turn (the team carried through the gates), its title card, the lane, and at
        /// the Heart the Tidemother, the choice, and the Spiral ending's card.</summary>
        IEnumerator Undertow(GameManager gm)
        {
            var part = gm.Campaign.parts.Find(x => x.kind == PartKind.Assault);
            for (int i = 0; i < part.stages.Count; i++)
            {
                var req = gm.Carry(Expedition.NewSave(gm, 0, i), 0);
                while (req.devices.Count > 2) req.devices.RemoveAt(req.devices.Count - 1);   // two sailors
                GameManager.Load(part.stages[i].scene, req);
                yield return new WaitForSecondsRealtime(i == 0 ? 2.2f : 1.4f);
                gm = GameManager.I;
                yield return Shot($"20-{i + 1}a-card");
                StoryCard.Close();
                var ex = gm.Expedition;
                var p1 = gm.Players.All[0];
                var target = ex.objectives.Count > 0 ? ex.objectives[0].transform.position : new Vector3(0, 0, 60);
                Teleport(p1, target + new Vector3(3, 0, -16));
                p1.SetLook(Mathf.Atan2(-3, 16) * Mathf.Rad2Deg, 8);
                if (gm.Players.Count > 1) Teleport(gm.Players.All[1], target + new Vector3(-2, 0, -13));
                yield return new WaitForSecondsRealtime(2.5f);
                yield return Shot($"20-{i + 1}b-{part.stages[i].id}");
                // From above the lane
                var view = new GameObject("StageCam").AddComponent<Camera>();
                view.depth = 40; view.fieldOfView = 60;
                Level.SetupCameraSky(view, gm.Phases.SkyColor);
                var at = new Vector3(0, 26, target.z - 34);
                view.transform.SetPositionAndRotation(at, Quaternion.LookRotation(target + new Vector3(0, 0, 10) - at));
                yield return new WaitForSecondsRealtime(.4f);
                yield return Shot($"20-{i + 1}c-{part.stages[i].id}-above");
                Destroy(view.gameObject);
                if (ex.objectives.Count > 0 && ex.objectives[0] is HeartAltar altar)
                {
                    Teleport(p1, altar.transform.position + new Vector3(0, 0, -18));
                    p1.SetLook(0, 6);
                    yield return new WaitForSecondsRealtime(3f);
                    yield return Shot("21-tidemother");
                    if (altar.Boss != null) altar.Boss.Die(new DamageInfo { source = p1 });
                    Teleport(p1, altar.transform.position + new Vector3(0, 0, -6));
                    p1.SetLook(0, 12);
                    yield return new WaitForSecondsRealtime(2f);
                    yield return Shot("22-the-heart-and-the-hollow");
                    StoryCard.Ending(gm.Campaign.Ending("spiral"), null);
                    yield return new WaitForSecondsRealtime(1.6f);
                    yield return Shot("23-crest-and-mark");
                    yield return new WaitForSecondsRealtime(3.4f);
                    yield return Shot("24-the-spiral");
                    StoryCard.Close();
                }
            }
        }

        /// <summary>Host an online game for a client test: one virtual player shooting, some enemies.</summary>
        IEnumerator Host()
        {
            yield return new WaitForSecondsRealtime(1f);
            var gm = GameManager.I;
            // With a room password, so the check also covers the challenge-response handshake
            if (relay)
            {
                bool done = false; string relayError = null;
                Net.StartRelayHost("Host Bot", DemoPassword, (host, why) => { relayError = why; done = true; });
                for (float t = 0; !done && t < 30; t += Time.unscaledDeltaTime) yield return null;
                if (!Net.IsHost) { Debug.LogError($"TIDEWALL_HOST_FAILED {relayError ?? "relay timed out"}"); Application.Quit(1); yield break; }
                Debug.Log($"TIDEWALL_JOIN_CODE {Net.Host.JoinCode}");
            }
            else if (Net.StartHost(NetProtocol.DefaultPort, "Host Bot", DemoPassword, out var error) == null) { Debug.LogError($"TIDEWALL_HOST_FAILED {error}"); Application.Quit(1); yield break; }
            if (hostStage >= 0)
            {
                // A Part II stage: go there (clients that join later load it on connecting)
                var part = gm.Campaign.parts.Find(x => x.kind == PartKind.Assault);
                StoryCard.Disabled = true;
                GameManager.Load(part.stages[hostStage].scene, new RunRequest { save = Expedition.NewSave(gm, 0, hostStage) });
                yield return new WaitForSecondsRealtime(1.5f);
                gm = GameManager.I;
            }
            else gm.StartRun(null, 0);
            pad1 = InputSystem.AddDevice<Gamepad>();
            var p = gm.Players.Join(new InputDevice[] { pad1 });
            Debug.Log($"TIDEWALL_HOST_READY {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
            if (!gm.Assault) gm.Phases.Advance();   // evening: a fight to watch
            float end = Time.time + hostSeconds;
            while (Time.time < end)
            {
                if (GameManager.Enemies.Count < 6)
                    foreach (var id in new[] { "crab", "drowner", "shark", "croc" })
                        gm.Waves.SpawnExtra(gm.DB.Enemy(id), p.transform.position + new Vector3(UnityEngine.Random.Range(-8f, 8f), 0, UnityEngine.Random.Range(12f, 18f)));
                fire = Time.time % 3 < 1;
                yield return new WaitForSecondsRealtime(.5f);
            }
            Debug.Log($"TIDEWALL_HOST_DONE clients={Net.Host.ClientCount} players={gm.Players.Count}");
            Application.Quit();
        }

        /// <summary>Join a host as a client and photograph the mirrored world.</summary>
        IEnumerator Join()
        {
            Directory.CreateDirectory(dir);
            yield return new WaitForSecondsRealtime(1.5f);
            var gm = GameManager.I;
            pad1 = InputSystem.AddDevice<Gamepad>();
            if (joinCode != null) Net.StartRelayClient(joinCode, "Client Bot", DemoPassword, new InputDevice[] { pad1 });
            else Net.StartClient(join, NetProtocol.DefaultPort, "Client Bot", DemoPassword, new InputDevice[] { pad1 });
            float give = Time.realtimeSinceStartup + 30, logT = 0;
            while (Time.realtimeSinceStartup < give && (gm == null || gm.Players.Count < 2 || !gm.Players.All.Exists(x => x.Kind == PlayerKind.LocalProxy)))
            {
                yield return new WaitForSecondsRealtime(.25f);
                gm = GameManager.I;
                if ((logT += .25f) >= 2) { logT = 0; Debug.Log($"TIDEWALL_CLIENT_WAIT status={(Net.IsClient ? Net.Client.Status.ToString() : "none")} players={gm?.Players.Count} state={gm?.State} timeScale={Time.timeScale}"); }
            }
            if (gm == null || !Net.IsClient || gm.Players.Count < 2) { Debug.LogError($"TIDEWALL_CLIENT_FAILED {(Net.IsClient ? Net.Client.Error : "not connected")}"); Application.Quit(1); yield break; }
            yield return new WaitForSecondsRealtime(5f);
            yield return Shot("net-01-client-first-person");
            var me = gm.Players.All.Find(x => x.Kind == PlayerKind.LocalProxy);
            me.SwitchView();
            yield return new WaitForSecondsRealtime(2.5f);
            yield return Shot("net-02-client-top-down");
            int enemies = 0;
            foreach (var e in FindObjectsByType<Enemy>()) if (e.Proxy) enemies++;
            Debug.Log($"TIDEWALL_CLIENT_OK players={gm.Players.Count} enemyProxies={enemies} sandDollars={gm.SandDollars} phase={gm.Phases.Current?.id}");
            Net.Shutdown(null);
            Application.Quit();
        }

        const string DemoPassword = "tide";

        static void Teleport(Player p, Vector3 at) => p.Place(at);

        IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, name + ".png"));
            yield return null;
        }
    }
}

// TideWall 3D — screenshots of The Last Light: `TideWall.exe -tidewall-capture-light <folder>` (add -tidewall-caller for
// Tide Caller's). Everything runs in a
// temporary folder, so it never touches the player's settings or record (R17). Quits when it's done.
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace TideWall
{
    public class LightCapture : MonoBehaviour
    {
        string folder;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-tidewall-capture-light");
            if (i < 0 || i + 1 >= args.Length) return;
            var go = new GameObject("LightCapture");
            DontDestroyOnLoad(go);
            go.AddComponent<LightCapture>().folder = args[i + 1];
        }

        IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, name + ".png"));
            yield return new WaitForSecondsRealtime(.3f);
        }

        /// <summary>Tide Caller: the conch, the view from above with a fight going on, and driving a creature.</summary>
        IEnumerator Caller()
        {
            GameManager.Pending = new RunRequest { save = new SaveData { slot = 0, part = Lighthouse.PartId, versus = true } };
            SceneManager.LoadScene(Lighthouse.SceneName);
            yield return null; yield return null;
            var gm = GameManager.I;
            var level = gm.Lighthouse;
            var p1 = gm.Players.Join(new InputDevice[] { InputSystem.AddDevice<Gamepad>() });
            var p2 = gm.Players.Join(new InputDevice[] { InputSystem.AddDevice<Gamepad>() });
            yield return new WaitForSecondsRealtime(1.5f);
            var conch = FindAnyObjectByType<ConchShell>().transform.position;
            p1.Place(conch + new Vector3(-3.5f, .1f, -2));
            var look = Quaternion.LookRotation(conch + Vector3.up * .8f - p1.EyePos).eulerAngles;
            p1.SetLook(look.y, look.x > 180 ? look.x - 360 : look.x);
            yield return new WaitForSecondsRealtime(.5f);
            yield return Shot("C1-the-conch");

            level.SetCaller(p2);
            p1.Place(level.SpawnPoint(0));
            level.StartFloodForTests();
            var at = p2.transform.position;
            for (int i = 0; i < 9; i++)
            {
                var c = at + Quaternion.Euler(0, i * 40, 0) * Vector3.forward * 5;
                level.CyclePick(i % 3 == 0 ? 1 : 0);
                level.TryCall(c, false);
                gm.Waves.SpawnExtra(gm.DB.Enemy(i % 2 == 0 ? "crab" : "drowner"), new Vector3(c.x, Arena.GroundAt(c), c.z) * .6f);
            }
            p2.SetLook(p2.transform.position.x > 0 ? 270 : 90, 30);
            yield return new WaitForSecondsRealtime(2.5f);
            yield return Shot("C2-isometric");
            p2.SwitchView();
            yield return new WaitForSecondsRealtime(1f);
            yield return Shot("C2b-close-view");
            p2.SwitchView();

            p2.Place(GameManager.Enemies[0].transform.position);
            yield return null;
            level.TryRide(p2.transform.position);
            var d = -level.Ridden.transform.position; d.y = 0;
            p2.SetLook(Quaternion.LookRotation(d).eulerAngles.y, 16);
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Shot("C3-driving-a-creature");
            Debug.Log("TIDEWALL_CALLER_CAPTURE_DONE");
            yield return new WaitForSecondsRealtime(.5f);
            Application.Quit();
        }

        IEnumerator Start()
        {
            Directory.CreateDirectory(folder);
            var temp = Path.Combine(Application.temporaryCachePath, "tidewall-capture-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);
            Storage.Root = temp;
            Settings.Reload();
            StoryCard.Disabled = true;
            yield return null;
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-tidewall-caller") >= 0) { yield return Caller(); yield break; }
            SceneManager.LoadScene(Lighthouse.SceneName);
            yield return null; yield return null;
            var gm = GameManager.I;
            var level = gm.Lighthouse;
            var pad1 = InputSystem.AddDevice<Gamepad>();
            var pad2 = InputSystem.AddDevice<Gamepad>();
            var p1 = gm.Players.Join(new InputDevice[] { pad1 });
            var p2 = gm.Players.Join(new InputDevice[] { pad2 });
            yield return new WaitForSecondsRealtime(1.5f);

            // The overview, from the air
            var cam = new GameObject("Overview").AddComponent<Camera>();
            cam.depth = 50; cam.fieldOfView = 55;
            Level.SetupCameraSky(cam, gm.Phases.SkyColor);
            cam.transform.SetPositionAndRotation(new Vector3(-45, 34, -45), Quaternion.LookRotation(new Vector3(0, 2, 0) - new Vector3(-45, 34, -45)));
            yield return Shot("L1-island-start");
            cam.enabled = false;

            p1.SetLook(20, 8);
            yield return Shot("L2-on-the-hill");

            // A fight: the first flood, one sailor at the lamp
            level.StartFloodForTests();
            foreach (var id in new[] { "crab", "crab", "drowner", "urchin", "hermit", "drowner" })
            {
                var at = Quaternion.Euler(0, Random.Range(0, 360f), 0) * Vector3.forward * Random.Range(16f, 26f);
                at.y = Arena.GroundAt(at);
                gm.Waves.SpawnExtra(gm.DB.Enemy(id), at);
            }
            level.SetKeeper(p2);
            p2.SetLook(35, 26);
            yield return new WaitForSecondsRealtime(3f);
            yield return Shot("L3-flood-and-the-lamp");
            level.SetKeeper(null);

            // The sea up the hill
            cam.enabled = true;
            Arena.WaterLevel = 2.2f;
            yield return new WaitForSecondsRealtime(.3f);
            yield return Shot("L4-high-water");
            cam.enabled = false;

            // Fishing, on the reel
            foreach (var e in GameManager.Enemies.ToArray()) if (e != null && !e.Dead) e.Die(default);
            Arena.WaterLevel = 1;
            var spot = FishingSpot.Spawn(p1.transform.position.normalized);
            spot.radius = 90;
            var shore = spot.transform.position - spot.transform.position.normalized * 3;
            shore.y = Arena.GroundAt(shore) + .1f;
            p1.Place(shore);
            yield return null;
            var look = Quaternion.LookRotation(spot.transform.position - p1.EyePos).eulerAngles;
            p1.SetLook(look.y, look.x > 180 ? look.x - 360 : look.x);
            p1.StartReeling(gm.DB.fish[1], spot);
            yield return new WaitForSecondsRealtime(1f);
            yield return Shot("L5-fishing-on-the-reel");
            p1.DebugLandFish();

            // Low tide: the wrecks
            level.StartLowTideForTests();
            Arena.WaterLevel = gm.DB.lastLight.lowTideWater;
            cam.enabled = true;
            cam.transform.SetPositionAndRotation(new Vector3(-58, 14, -58), Quaternion.LookRotation(new Vector3(-10, 0, -10) - new Vector3(-58, 14, -58)));
            yield return new WaitForSecondsRealtime(.5f);
            yield return Shot("L6-low-tide-salvage");
            cam.enabled = false;

            // The Keeper's Log
            p1.OpenMenu(LastLightMenus.KeepersLog(null));
            yield return new WaitForSecondsRealtime(.5f);
            yield return Shot("L7-keepers-log");
            Debug.Log("TIDEWALL_LIGHT_CAPTURE_DONE");
            yield return new WaitForSecondsRealtime(.5f);
            Application.Quit();
        }
    }
}

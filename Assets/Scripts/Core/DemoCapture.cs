// TideWall 3D — automated screenshot tour for checking visuals without a human at the controls.
// Only runs when the game is launched with:  TideWall.exe -tidewall-capture <output folder>
// It joins virtual gamepads, walks through the phases, starts a fight and saves PNGs, then quits.
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
        string dir;
        Gamepad pad1, pad2;
        Vector2 aim1 = new(0, 1);
        bool fire;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Check()
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-tidewall-capture");
            if (i < 0) return;
            var c = new GameObject("DemoCapture").AddComponent<DemoCapture>();
            c.dir = i + 1 < args.Length ? args[i + 1] : "Captures";
        }

        void Update()
        {
            // Hold the virtual sticks/triggers every frame (state events are one-shot).
            if (pad1 != null) InputSystem.QueueStateEvent(pad1, new GamepadState { rightStick = aim1, rightTrigger = fire ? 1 : 0 });
        }

        IEnumerator Start()
        {
            Directory.CreateDirectory(dir);
            yield return new WaitForSeconds(2.5f);
            var gm = GameManager.I;
            yield return Shot("01-lobby");

            pad1 = InputSystem.AddDevice<Gamepad>();
            pad2 = InputSystem.AddDevice<Gamepad>();
            var p1 = gm.Players.Join(new InputDevice[] { pad1 });
            var p2 = gm.Players.Join(new InputDevice[] { pad2 });
            yield return new WaitForSeconds(3f);
            yield return Shot("02-morning-two-players");

            p1.OpenMenu(Menus.Hotdogs(p1));
            p2.OpenMenu(Menus.Shop(p2));
            yield return new WaitForSeconds(.5f);
            yield return Shot("03-hotdog-and-shop-menus");
            p1.CloseMenu(); p2.CloseMenu();

            gm.Phases.Advance();   // evening
            Teleport(p1, new Vector3(-3, 0, 4)); Teleport(p2, new Vector3(4, 0, -2));
            foreach (var id in new[] { "crab", "crab", "drowner", "hermit", "jelly", "urchin", "gull" })
                gm.Waves.SpawnExtra(gm.DB.Enemy(id), new Vector3(UnityEngine.Random.Range(-8f, 8f), 0, UnityEngine.Random.Range(10f, 16f)));
            gm.Waves.SpawnExtra(gm.DB.Enemy("shark"), new Vector3(0, -1, 26));
            yield return new WaitForSeconds(4f);
            fire = true;
            yield return new WaitForSeconds(1.2f);
            yield return Shot("04-evening-fight");

            gm.Phases.Advance();   // night (enemies still alive keep fighting)
            foreach (var id in new[] { "croc", "octopus", "lobster", "turtle", "puffer", "eel", "drowner", "drowner" })
                gm.Waves.SpawnExtra(gm.DB.Enemy(id), new Vector3(UnityEngine.Random.Range(-10f, 10f), 0, UnityEngine.Random.Range(9f, 18f)));
            yield return new WaitForSeconds(5f);
            yield return Shot("05-night-fight");

            p2.Statuses.Add(StatusType.UpsideDown, 10);
            p1.Statuses.Add(StatusType.Giant, 10);
            yield return new WaitForSeconds(1.5f);
            yield return Shot("06-sauce-upside-down-and-giant");
            p2.Statuses.Clear(StatusType.UpsideDown);
            p2.Statuses.Add(StatusType.FirstPerson, 10);
            yield return new WaitForSeconds(1f);
            yield return Shot("07-sauce-first-person");

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
            yield return new WaitForSeconds(.5f);
            yield return Shot("08-bestiary");
            var boss = Enemy.Spawn(gm.DB.Enemy("glizzy"), new Vector3(0, Arena.GroundY(12), 12), 5, -1);   // on the sand, not under the sea
            boss.enabled = false;
            cam.transform.SetPositionAndRotation(new Vector3(0, 7, 0), Quaternion.Euler(18, 0, 0));
            yield return new WaitForSeconds(.5f);
            yield return Shot("09-glizzy-boss");
            Destroy(cam.gameObject);
            gm.Players.All.ForEach(p => p.gameObject.SetActive(true));

            var pad3 = InputSystem.AddDevice<Gamepad>(); var pad4 = InputSystem.AddDevice<Gamepad>();
            gm.Players.Join(new InputDevice[] { pad3 }); gm.Players.Join(new InputDevice[] { pad4 });
            yield return new WaitForSeconds(2f);
            yield return Shot("10-four-players");
            Debug.Log("TIDEWALL_CAPTURE_DONE");
            Application.Quit();
        }

        static void Teleport(Player p, Vector3 at)
        {
            var cc = p.GetComponent<CharacterController>();
            cc.enabled = false; p.transform.position = at; cc.enabled = true;
        }

        IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, name + ".png"));
            yield return null;
        }
    }
}

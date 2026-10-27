// TideWall 3D — playtesting tools: the switches the debug menu (DebugMenu.cs) flips, F1 to open it, collider
// outlines and a stats overlay. Offline and on the host only (a client's world is the host's). Settings → Debug tools
// turns all of it off. The switches are static, so they survive moving between Part II stages.
using UnityEngine;
using UnityEngine.InputSystem;

namespace TideWall
{
    public class DebugTools : MonoBehaviour
    {
        public static bool God, InfiniteAmmo, Noclip, Freeze, Hitboxes, Overlay, CasinoAlwaysOpen;
        public static float TimeScale = 1;
        public static bool AnyOn => God || InfiniteAmmo || Noclip || Freeze || Hitboxes || Overlay || CasinoAlwaysOpen || TimeScale != 1;

        /// <summary>Can this player open the debug menu? (Settings allows it, on this machine, not as an online client.)</summary>
        public static bool Available(Player p) => Settings.Current.debugTools && !Net.IsClient && p != null && p.Kind == PlayerKind.Local;

        public static void ResetAll()
        {
            God = InfiniteAmmo = Noclip = Freeze = Hitboxes = Overlay = CasinoAlwaysOpen = false;
            TimeScale = 1;
        }

        GameManager gm;
        Mesh box;
        float fps, fpsAcc; int fpsFrames;
        GUIStyle overlayStyle;

        void Awake() => gm = GetComponent<GameManager>();

        void Update()
        {
            fpsAcc += Time.unscaledDeltaTime; fpsFrames++;
            if (fpsAcc >= .5f) { fps = fpsFrames / fpsAcc; fpsAcc = 0; fpsFrames = 0; }
            // F1: the debug menu for whoever plays on the keyboard (or the first player here)
            var kb = Keyboard.current;
            if (kb == null || !kb.f1Key.wasPressedThisFrame || gm.State != GameState.Playing) return;
            var p = gm.Players.All.Find(x => x.Kind == PlayerKind.Local && x.UsesMouse) ?? gm.Players.All.Find(x => x.Kind == PlayerKind.Local);
            if (!Available(p)) return;
            if (p.OpenedMenu != null && p.OpenedMenu.Title.StartsWith(DebugMenu.Tag)) p.CloseMenu();
            else p.OpenMenu(DebugMenu.Main(p));
        }

        // ------------------------------------------------------------------ collider outlines
        static readonly Color EnemyCol = new(1, .3f, .3f), WeakCol = new(1, .9f, .2f), PlayerCol = new(.3f, 1, .4f), BuildCol = new(.4f, .7f, 1), OtherCol = new(.9f, .6f, 1);

        void LateUpdate()
        {
            if (!Hitboxes) return;
            box ??= LineCube();
            foreach (var e in GameManager.Enemies)
            {
                if (e == null) continue;
                foreach (var c in e.GetComponentsInChildren<Collider>())
                {
                    var limb = c.GetComponentInParent<Limb>();
                    Draw(c, limb != null && (limb.Kind == "Head" || limb.Kind == "Eyes") ? WeakCol : EnemyCol);
                }
            }
            foreach (var p in gm.Players.All) if (p != null) foreach (var c in p.GetComponentsInChildren<Collider>()) Draw(c, PlayerCol);
            foreach (var s in Structure.All) if (s != null) foreach (var c in s.GetComponentsInChildren<Collider>()) Draw(c, BuildCol);
            foreach (var i in FindObjectsByType<Interactable>()) foreach (var c in i.GetComponentsInChildren<Collider>()) Draw(c, OtherCol);
        }

        void Draw(Collider c, Color col)
        {
            if (c == null || !c.enabled) return;
            Matrix4x4 m = c is BoxCollider b
                ? c.transform.localToWorldMatrix * Matrix4x4.TRS(b.center, Quaternion.identity, b.size)
                : Matrix4x4.TRS(c.bounds.center, Quaternion.identity, c.bounds.size);
            Graphics.DrawMesh(box, m, Mats.Unlit(col), 0);
        }

        /// <summary>A unit cube's twelve edges, as lines.</summary>
        static Mesh LineCube()
        {
            var v = new Vector3[8];
            for (int i = 0; i < 8; i++) v[i] = new Vector3((i & 1) - .5f, ((i >> 1) & 1) - .5f, ((i >> 2) & 1) - .5f);
            var idx = new[] { 0, 1, 2, 3, 4, 5, 6, 7, 0, 2, 1, 3, 4, 6, 5, 7, 0, 4, 1, 5, 2, 6, 3, 7 };
            var mesh = new Mesh { name = "DebugLineCube", vertices = v };
            mesh.SetIndices(idx, MeshTopology.Lines, 0);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one);
            return mesh;
        }

        // ------------------------------------------------------------------ stats overlay
        void OnGUI()
        {
            if (!Overlay || gm.State == GameState.MainMenu) return;
            float k = Screen.height / 1080f;
            overlayStyle ??= new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, richText = true };
            overlayStyle.fontSize = Mathf.RoundToInt(15 * k);
            string where = gm.Assault ? $"{gm.Expedition.Stage.title} · tide {gm.Expedition.TideLeft:0}s"
                                      : $"Day {gm.Phases.Day} · {gm.Phases.Current?.displayName} · wave {gm.Waves.Wave}/{gm.Waves.WaveCount}";
            string flags = (God ? "GOD " : "") + (InfiniteAmmo ? "AMMO " : "") + (Noclip ? "NOCLIP " : "") + (Freeze ? "FROZEN " : "") + (CasinoAlwaysOpen ? "CASINO " : "");
            var text = $"<b>{fps:0} fps</b>  ·  {Time.timeScale:0.##}× time\n{where}\n{GameManager.Enemies.Count} creatures · {gm.SandDollars} SD · {gm.Pearls} pearls · casino {gm.Casino.Net:+0;-0;0}" +
                       (flags.Length > 0 ? $"\n<color=#fde68a>{flags}</color>" : "");
            // Bottom right, clear of the HUD's health bars and weapon lines
            float w = 470 * k, h = (flags.Length > 0 ? 92 : 72) * k;
            GUI.Box(new Rect(Screen.width - w - 10 * k, Screen.height - h - 10 * k, w, h), text, overlayStyle);
        }
    }
}

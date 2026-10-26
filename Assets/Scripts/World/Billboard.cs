// TideWall 3D — world text that turns to face whichever camera is drawing it (stall signs, name tags, vendor speech).
// Split screen draws the scene once per player, so the turn happens per camera, just before it renders: every player
// reads every sign the right way round, from any side.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TideWall
{
    public class Billboard : MonoBehaviour
    {
        static readonly List<Billboard> all = new();
        static bool hooked;

        /// <summary>Upright: only turn around the vertical axis (signs); otherwise face the camera fully (name tags).</summary>
        public bool upright;

        public static Billboard Add(GameObject go, bool upright = false)
        {
            var b = go.AddComponent<Billboard>();
            b.upright = upright;
            return b;
        }

        void OnEnable()
        {
            all.Add(this);
            if (!hooked) { RenderPipelineManager.beginCameraRendering += Face; hooked = true; }
        }

        void OnDisable() => all.Remove(this);

        static void Face(ScriptableRenderContext ctx, Camera cam)
        {
            var at = cam.transform.position;
            foreach (var b in all)
            {
                var d = b.transform.position - at;
                if (b.upright) d.y = 0;
                if (d.sqrMagnitude < 1e-4f) continue;
                b.transform.rotation = b.upright ? Quaternion.LookRotation(d) : Quaternion.LookRotation(d, cam.transform.up);
            }
        }
    }
}

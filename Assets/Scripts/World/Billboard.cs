// TideWall 3D — world text that turns to face whichever camera is drawing it (stall signs, name tags, vendor speech).
// Split screen draws the scene once per player, so the turn happens per camera, just before it renders: every player
// reads every sign the right way round, from any side.
using System.Collections.Generic;
using TMPro;
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
        /// <summary>Fade out when a camera is closer than this (0 = never): a stall's sign isn't needed once you're at
        /// the stall, and up close it would fill the view.</summary>
        public float fadeNear;
        TMP_Text text;
        float baseAlpha = 1;

        public static Billboard Add(GameObject go, bool upright = false, float fadeNear = 0)
        {
            var b = go.AddComponent<Billboard>();
            b.upright = upright;
            b.fadeNear = fadeNear;
            return b;
        }

        void Awake()
        {
            text = GetComponent<TMP_Text>();
            if (text != null) baseAlpha = text.color.a;
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
                if (b.fadeNear > 0 && b.text != null)
                {
                    float a = b.baseAlpha * Mathf.Clamp01((d.magnitude - b.fadeNear) / 3f);
                    if (!Mathf.Approximately(b.text.alpha, a)) b.text.alpha = a;
                }
                if (d.sqrMagnitude < 1e-4f) continue;
                b.transform.rotation = b.upright ? Quaternion.LookRotation(d) : Quaternion.LookRotation(d, cam.transform.up);
            }
        }
    }
}

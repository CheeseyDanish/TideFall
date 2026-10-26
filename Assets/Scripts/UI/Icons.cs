// TideWall 3D — pictures of things for menus (the shop's weapon, food and defence cards): each model is rendered once
// by an off-screen camera to a small texture and cached. Nothing to maintain: any ModelFactory model has an icon.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TideWall
{
    public static class Icons
    {
        const int Size = 160, Layer = 30;
        static readonly Dictionary<string, Sprite> cache = new();
        static Camera cam;
        static Light key;

        /// <summary>The picture of a model (null if it can't be drawn, e.g. without a graphics device).</summary>
        public static Sprite For(string model)
        {
            if (string.IsNullOrEmpty(model)) return null;
            if (cache.TryGetValue(model, out var s)) return s;
            cache[model] = s = Render(model);
            return s;
        }

        static Sprite Render(string model)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return null;
            var stage = new Vector3(0, -400, 0);
            GameObject go = null;
            RenderTexture rt = null;
            bool fog = RenderSettings.fog;
            try
            {
                if (cam == null)
                {
                    cam = new GameObject("Icon Camera").AddComponent<Camera>();
                    Object.DontDestroyOnLoad(cam.gameObject);
                    cam.enabled = false;
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = new Color(0, 0, 0, 0);
                    cam.cullingMask = 1 << Layer;
                    cam.fieldOfView = 26;
                    cam.GetUniversalAdditionalCameraData().renderPostProcessing = false;
                    key = new GameObject("Icon Light").AddComponent<Light>();
                    Object.DontDestroyOnLoad(key.gameObject);
                    key.type = LightType.Directional; key.intensity = 1.3f; key.cullingMask = 1 << Layer;
                    key.transform.rotation = Quaternion.Euler(35, -40, 0);
                    key.enabled = false;
                }
                var mi = ModelFactory.Spawn(model, null, stage);
                go = mi.root;
                foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layer;
                go.transform.rotation = Quaternion.Euler(0, 210, 0);   // three-quarter view
                var b = ModelFactory.Bounds(go);
                float dist = b.extents.magnitude / Mathf.Tan(cam.fieldOfView * .5f * Mathf.Deg2Rad) * 1.05f;
                cam.transform.position = b.center + new Vector3(0, .35f, -1).normalized * dist;
                cam.transform.LookAt(b.center);
                cam.nearClipPlane = .01f; cam.farClipPlane = dist * 3;
                rt = RenderTexture.GetTemporary(Size, Size, 24, RenderTextureFormat.ARGB32);
                cam.targetTexture = rt;
                key.enabled = true;
                RenderSettings.fog = false;
                cam.Render();
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                tex.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                return Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(.5f, .5f));
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"TideWall: couldn't draw an icon for {model} ({e.Message})");
                return null;
            }
            finally
            {
                RenderSettings.fog = fog;
                if (key != null) key.enabled = false;
                if (cam != null) cam.targetTexture = null;
                if (rt != null) RenderTexture.ReleaseTemporary(rt);
                if (go != null) Object.Destroy(go);
            }
        }
    }
}

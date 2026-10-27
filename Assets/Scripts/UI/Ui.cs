// TideWall 3D — small helpers for building the HUD and menus (UGUI + TextMeshPro) and labels in the world.
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TideWall
{
    public static class Ui
    {
        static Sprite white;
        static Material shadowed;

        /// <summary>TextMeshPro's default font (LiberationSans SDF): sharp at any size and resolution.</summary>
        public static TMP_FontAsset Font => TMP_Settings.defaultFontAsset;

        /// <summary>The font with a soft drop shadow underneath, so HUD text reads over sand, sea and sky alike.</summary>
        static Material Shadowed
        {
            get
            {
                if (shadowed != null) return shadowed;
                shadowed = new Material(Font.material) { name = "Font Shadowed" };
                shadowed.EnableKeyword("UNDERLAY_ON");
                shadowed.SetColor("_UnderlayColor", new Color(0, 0, 0, .75f));
                shadowed.SetFloat("_UnderlayOffsetX", .6f);
                shadowed.SetFloat("_UnderlayOffsetY", -.6f);
                shadowed.SetFloat("_UnderlaySoftness", .25f);
                return shadowed;
            }
        }

        public static TextAlignmentOptions Align(TextAnchor a) => a switch
        {
            TextAnchor.UpperLeft => TextAlignmentOptions.TopLeft,
            TextAnchor.UpperCenter => TextAlignmentOptions.Top,
            TextAnchor.UpperRight => TextAlignmentOptions.TopRight,
            TextAnchor.MiddleLeft => TextAlignmentOptions.Left,
            TextAnchor.MiddleRight => TextAlignmentOptions.Right,
            TextAnchor.LowerLeft => TextAlignmentOptions.BottomLeft,
            TextAnchor.LowerCenter => TextAlignmentOptions.Bottom,
            TextAnchor.LowerRight => TextAlignmentOptions.BottomRight,
            _ => TextAlignmentOptions.Center,
        };

        /// <summary>Set text only when it changed (TextMeshPro rebuilds its mesh on every assignment).</summary>
        public static void Set(TMP_Text t, string value) { if (t.text != value) t.text = value; }

        /// <summary>A plain white sprite, so Images can be tinted to any colour.</summary>
        public static Sprite White
        {
            get
            {
                if (white != null) return white;
                var tex = new Texture2D(4, 4) { filterMode = FilterMode.Bilinear };
                var px = new Color[16];
                for (int i = 0; i < 16; i++) px[i] = Color.white;
                tex.SetPixels(px); tex.Apply();
                return white = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(.5f, .5f));
            }
        }

        /// <summary>A canvas drawn by one camera: in split screen each player's HUD sits inside their own viewport.</summary>
        public static Canvas CanvasFor(Camera cam, string name, int order = 0)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler));
            var c = go.GetComponent<Canvas>();
            if (cam != null) { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = cam; c.planeDistance = .5f; }
            else c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingLayerName = "HUD";   // after everything in the world (TideWall → Setup Project adds the layer)
            c.sortingOrder = order;
            var s = go.GetComponent<CanvasScaler>();
            s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            s.referenceResolution = new Vector2(1280, 720);
            s.matchWidthOrHeight = .5f;
            return c;
        }

        public static RectTransform Rect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = pivot;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            return rt;
        }

        public static TextMeshProUGUI Text(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, int fontSize, TextAnchor align, Color color, bool shadow = true)
        {
            var rt = Rect(parent, name, anchor, anchor, anchor, pos, size);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = Font;
            if (shadow) t.fontSharedMaterial = Shadowed;
            t.fontSize = fontSize; t.alignment = Align(align); t.color = color;
            t.textWrappingMode = TextWrappingModes.Normal; t.overflowMode = TextOverflowModes.Overflow;
            t.richText = true; t.raycastTarget = false;
            return t;
        }

        /// <summary>Text standing in the world (signs, name tags, speech, damage numbers). TextMeshPro is depth-tested,
        /// so walls hide it; pair it with a Billboard so it faces each camera. `height` is the letters' size in metres.</summary>
        public static TextMeshPro Label(Transform parent, string name, Vector3 localPos, float height, Color color, TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var t = go.AddComponent<TextMeshPro>();
            t.font = Font;
            t.fontSize = height * 10;   // a 3D TextMeshPro at size 10 stands about a metre tall
            t.color = color; t.alignment = align;
            t.textWrappingMode = TextWrappingModes.NoWrap; t.overflowMode = TextOverflowModes.Overflow;
            t.rectTransform.sizeDelta = new Vector2(10, 2);
            t.sortingOrder = -100;   // drawn before the players' HUD canvases, so a nearby sign never covers the HUD
            t.rectTransform.pivot = align == TextAlignmentOptions.Bottom ? new Vector2(.5f, 0) : new Vector2(.5f, .5f);
            return t;
        }

        public static Image Image(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Color color)
        {
            var rt = Rect(parent, name, anchor, anchor, anchor, pos, size);
            var i = rt.gameObject.AddComponent<Image>();
            i.sprite = White; i.color = color; i.raycastTarget = false;
            return i;
        }

        /// <summary>A stretched full-panel image (for overlays like ink or tunnel vision).</summary>
        public static Image Fill(Transform parent, string name, Color color)
        {
            var rt = Rect(parent, name, Vector2.zero, Vector2.one, new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
            var i = rt.gameObject.AddComponent<Image>();
            i.sprite = White; i.color = color; i.raycastTarget = false;
            return i;
        }

        /// <summary>A horizontal bar: returns the fill image (set fillAmount 0..1).</summary>
        public static Image Bar(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Color fill)
        {
            var bg = Image(parent, name, anchor, pos, size, new Color(0, 0, 0, .6f));
            var f = Image(bg.transform, "Fill", new Vector2(0, .5f), new Vector2(2, 0), size - new Vector2(4, 4), fill);
            f.rectTransform.pivot = new Vector2(0, .5f);
            f.type = UnityEngine.UI.Image.Type.Filled; f.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal; f.fillOrigin = 0;
            return f;
        }

        static Sprite sandDollar;

        /// <summary>The money icon: a sand dollar (pale disc, five-petal flower, a little hole in the middle). Drawn in code.</summary>
        public static Sprite SandDollarIcon
        {
            get
            {
                if (sandDollar != null) return sandDollar;
                const int n = 64;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                var px = new Color[n * n];
                Color body = new(.97f, .91f, .76f), rim = new(.82f, .7f, .5f), petal = new(.74f, .6f, .4f);
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        var d = new Vector2(x - n / 2f + .5f, y - n / 2f + .5f) / (n / 2f);
                        float r = d.magnitude;
                        if (r > 1) { px[y * n + x] = Color.clear; continue; }
                        Color c = r > .86f ? rim : body;
                        // Five petals: narrow ellipses pointing out from the centre
                        float a = Mathf.Atan2(d.y, d.x) + Mathf.PI / 2;
                        for (int k = 0; k < 5; k++)
                        {
                            float pa = k * Mathf.PI * 2 / 5;
                            var dir = new Vector2(Mathf.Cos(pa + Mathf.PI / 2), Mathf.Sin(pa + Mathf.PI / 2));
                            float along = Vector2.Dot(d, dir), across = Mathf.Abs(d.x * dir.y - d.y * dir.x);
                            if (along > .16f && along < .62f && across < .085f * Mathf.Sin(Mathf.PI * (along - .16f) / .46f) + .01f) c = petal;
                        }
                        if (r < .08f) c = petal;
                        c.a = Mathf.Clamp01((1 - r) * n / 3f);   // soft edge
                        px[y * n + x] = c;
                    }
                tex.SetPixels(px); tex.Apply();
                return sandDollar = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(.5f, .5f));
            }
        }

        static Sprite ring, dot;

        /// <summary>A thin circle outline, and a solid disc (the reload widget's beat ring and pips).</summary>
        public static Sprite Ring => ring ??= Circle(.78f);
        public static Sprite Dot => dot ??= Circle(0);

        static Sprite Circle(float inner, int size = 64)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[size * size];
            float px1 = 2f / size;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(size / 2f, size / 2f)) / (size / 2f);
                    float a = Mathf.Clamp01((1 - d) / px1) * (inner > 0 ? Mathf.Clamp01((d - inner) / px1) : 1);
                    px[y * size + x] = new Color(1, 1, 1, a);
                }
            tex.SetPixels(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(.5f, .5f));
        }

        public static Sprite Radial(bool hole, int size = 128)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(size / 2f, size / 2f)) / (size / 2f);
                    float a = hole ? Mathf.SmoothStep(0, 1, (d - .35f) / .55f) : 1 - Mathf.SmoothStep(.55f, 1, d);
                    px[y * size + x] = new Color(1, 1, 1, a);
                }
            tex.SetPixels(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(.5f, .5f));
        }
    }
}

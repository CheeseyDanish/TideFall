// TideWall 3D — graphics settings applied to the screen, the render pipeline and every camera.
// Pipeline-level options (render scale, MSAA, shadow distance/cascades) change a runtime copy of the URP asset, and
// only in a built game: in the editor the project's assets are never touched (CLAUDE.md R17), so those options
// show their effect in builds. Camera-level options (anti-aliasing mode, post-processing, draw distance) apply
// everywhere through ConfigureCamera, which every camera calls each frame.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TideWall
{
    public static class GraphicsApply
    {
        public static readonly string[] AntiAliasingNames = { "Off", "FXAA", "SMAA", "MSAA 2x", "MSAA 4x", "MSAA 8x" };
        public static readonly string[] ShadowNames = { "Off", "Low", "Medium", "High" };
        public static readonly string[] DisplayModeNames = { "Fullscreen", "Exclusive fullscreen", "Windowed" };
        public static readonly int[] FpsCaps = { 0, 30, 60, 120, 144, 165, 240 };
        static UniversalRenderPipelineAsset runtimeAsset;
        static bool menuBoost;

        /// <summary>
        /// The main menu is one still view, so it can afford more: while it's shown the game renders at (at least) 2x
        /// resolution with MSAA 8x, then goes back to the player's settings.
        /// </summary>
        public static bool MenuBoost
        {
            get => menuBoost;
            set { if (menuBoost == value) return; menuBoost = value; Apply(Settings.Current); }
        }

        /// <summary>Screen resolutions to choose from (largest first, duplicates removed). -1 in Settings = the display's own.</summary>
        public static List<Vector2Int> Resolutions()
        {
            var list = new List<Vector2Int>();
            foreach (var r in Screen.resolutions)
            {
                var v = new Vector2Int(r.width, r.height);
                if (!list.Contains(v)) list.Add(v);
            }
            list.Sort((a, b) => (b.x * b.y).CompareTo(a.x * a.y));
            return list;
        }

        public static string ResolutionName(int index)
        {
            var list = Resolutions();
            if (index < 0 || index >= list.Count) return $"Native ({Display.main.systemWidth}x{Display.main.systemHeight})";
            return $"{list[index].x}x{list[index].y}";
        }

        public static void Apply(Settings s)
        {
            QualitySettings.vSyncCount = s.vsync ? 1 : 0;
            Application.targetFrameRate = s.vsync ? -1 : s.fpsCap > 0 ? s.fpsCap : -1;
            if (!Application.isEditor && !Application.isBatchMode)
            {
                var mode = s.displayMode switch { 1 => FullScreenMode.ExclusiveFullScreen, 2 => FullScreenMode.Windowed, _ => FullScreenMode.FullScreenWindow };
                var list = Resolutions();
                int w = s.resolution >= 0 && s.resolution < list.Count ? list[s.resolution].x : Display.main.systemWidth;
                int h = s.resolution >= 0 && s.resolution < list.Count ? list[s.resolution].y : Display.main.systemHeight;
                if (mode == FullScreenMode.Windowed && s.resolution < 0) { w = Mathf.RoundToInt(w * .8f); h = Mathf.RoundToInt(h * .8f); }
                if (Screen.width != w || Screen.height != h || Screen.fullScreenMode != mode) Screen.SetResolution(w, h, mode);
                ApplyPipeline(s);
            }
            ApplyVolume(s);
            var sun = GameManager.I != null && GameManager.I.World != null ? GameManager.I.World.sun : null;
            if (sun != null) sun.shadows = s.shadows == 0 ? LightShadows.None : s.shadows == 1 ? LightShadows.Hard : LightShadows.Soft;
        }

        /// <summary>Built game only: work on a copy of the pipeline asset so nothing on disk changes.</summary>
        static void ApplyPipeline(Settings s)
        {
            if (runtimeAsset == null)
            {
                // Start from the best quality level's pipeline (the PC asset): these settings decide the rest
                var best = QualitySettings.GetRenderPipelineAssetAt(QualitySettings.names.Length - 1) as UniversalRenderPipelineAsset;
                var current = best != null ? best : GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                if (current == null) return;
                runtimeAsset = Object.Instantiate(current);
                runtimeAsset.name = current.name + " (runtime)";
            }
            // Every time: changing the quality preset swaps the level's own asset back in
            if (QualitySettings.renderPipeline != runtimeAsset) QualitySettings.renderPipeline = runtimeAsset;
            runtimeAsset.renderScale = menuBoost ? Mathf.Max(s.renderScale, 2f) : Mathf.Clamp(s.renderScale, .5f, 2f);
            runtimeAsset.msaaSampleCount = menuBoost ? 8 : s.antiAliasing switch { 3 => 2, 4 => 4, 5 => 8, _ => 1 };
            runtimeAsset.shadowDistance = s.shadows switch { 0 => 0, 1 => 35, 2 => 60, _ => 90 };
            runtimeAsset.shadowCascadeCount = s.shadows >= 3 ? 4 : s.shadows == 2 ? 2 : 1;
        }

        /// <summary>Bloom and the rest of the post-processing (the scene's Volume; a runtime copy of its profile).</summary>
        static void ApplyVolume(Settings s)
        {
            var vol = Object.FindAnyObjectByType<Volume>();
            if (vol == null) return;
            var profile = vol.profile;   // instantiates a runtime copy: the asset stays as it is
            if (profile.TryGet<Bloom>(out var bloom)) bloom.active = s.bloom;
            vol.weight = s.postProcessing ? 1 : 0;
        }

        /// <summary>Per camera, every frame (cheap): anti-aliasing mode, post-processing, draw distance.</summary>
        public static void ConfigureCamera(Camera c)
        {
            var s = Settings.Current;
            var data = c.GetUniversalAdditionalCameraData();
            data.antialiasing = s.antiAliasing switch { 1 => AntialiasingMode.FastApproximateAntialiasing, 2 => AntialiasingMode.SubpixelMorphologicalAntiAliasing, _ => AntialiasingMode.None };
            data.antialiasingQuality = AntialiasingQuality.High;
            data.renderPostProcessing = s.postProcessing;
            c.allowMSAA = s.antiAliasing >= 3 || menuBoost;
            if (menuBoost && s.antiAliasing < 3) data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            c.farClipPlane = Mathf.Clamp(s.drawDistance, 80, 500);
        }
    }
}

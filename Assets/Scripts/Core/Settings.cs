// TideWall 3D — player settings (audio, game, controls). Stored as JSON in the user's data folder
// (Application.persistentDataPath/settings.json); tests point Storage.Root at a temp folder so they never
// touch the real file. Changing a value and calling Settings.Changed() applies it everywhere.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TideWall
{
    /// <summary>Where settings and saves live. Tests redirect this (never touch the player's real files).</summary>
    public static class Storage
    {
        static string root;
        public static string Root
        {
            get => root ??= Application.persistentDataPath;
            set => root = value;
        }

        public static string Read(string file)
        {
            try { var p = Path.Combine(Root, file); return File.Exists(p) ? File.ReadAllText(p) : null; }
            catch (Exception e) { Debug.LogWarning($"TideWall: couldn't read {file}: {e.Message}"); return null; }
        }

        public static bool Write(string file, string text)
        {
            try
            {
                var p = Path.Combine(Root, file);
                Directory.CreateDirectory(Path.GetDirectoryName(p));
                File.WriteAllText(p + ".tmp", text);         // write then swap, so a crash can't leave half a file
                if (File.Exists(p)) File.Delete(p);
                File.Move(p + ".tmp", p);
                return true;
            }
            catch (Exception e) { Debug.LogWarning($"TideWall: couldn't write {file}: {e.Message}"); return false; }
        }

        public static void Delete(string file)
        {
            try { var p = Path.Combine(Root, file); if (File.Exists(p)) File.Delete(p); }
            catch (Exception e) { Debug.LogWarning($"TideWall: couldn't delete {file}: {e.Message}"); }
        }
    }

    [Serializable]
    public class BindingOverride
    {
        public string key;    // "Action/Group/Part", e.g. "Fire/Keyboard/" or "Move/Keyboard/up"
        public string path;   // control path, e.g. "<Keyboard>/k"
    }

    [Serializable]
    public class Settings
    {
        // Audio (0..1)
        public float master = .8f, effects = 1f, interfaceVolume = .8f, ambience = .5f;
        public bool startMuted = true;  // the game opens silent; unmute in Settings (this session) or turn this off
        /// <summary>All sound off for this session. Starts as startMuted; not saved.</summary>
        public static bool Muted { get => muted ??= Current.startMuted; set => muted = value; }
        static bool? muted;
        // Game
        public bool startFirstPerson = true;
        public float fov = 75, mouseSensitivity = 1, stickSensitivity = 1, screenShake = 1;
        public bool invertY, aimAssist = true, showFps, damageNumbers = true;
        // Graphics
        public int displayMode;         // 0 fullscreen (borderless), 1 exclusive fullscreen, 2 windowed
        public int resolution = -1;     // index into GraphicsApply.Resolutions(); -1 = the display's native resolution
        public bool vsync = true;
        public int fpsCap;              // when vsync is off: 0 = unlimited
        public float renderScale = 1;   // 0.5..2 (above 1 = supersampling: sharper, slower)
        public int antiAliasing = 4;    // GraphicsApply.AntiAliasingNames: MSAA 4x
        public int shadows = 3;         // off, low, medium, high
        public bool bloom = true, postProcessing = true;
        public float drawDistance = 260, particleDensity = 1;
        public int quality = -1;        // Unity quality level; -1 = leave the project's default
        public int version;             // settings format: older files had a low quality preset that hid the graphics options
        // Online
        public string playerName = "Sailor";
        public string lastAddress = "127.0.0.1";
        public int port = 7777;
        // Controls: overrides on top of the default bindings (PlayerControls)
        public List<BindingOverride> bindings = new();

        const string File = "settings.json";
        const int Version = 2;
        static Settings current;
        public static Settings Current => current ??= Load();
        /// <summary>Fired after any change is applied (cameras re-read FOV, players re-apply bindings…).</summary>
        public static event Action OnChanged;

        public static Settings Load()
        {
            var json = Storage.Read(File);
            Settings s = null;
            if (!string.IsNullOrEmpty(json)) try { s = JsonUtility.FromJson<Settings>(json); } catch { s = null; }
            s ??= new Settings { version = Version };
            if (s.version < Version)
            {
                // Before the Graphics menu, "Graphics quality" picked Unity's Mobile level (blurry, low resolution)
                s.quality = -1;
                s.version = Version;
            }
            return s;
        }

        /// <summary>Forget the cached copy (tests switch Storage.Root and reload).</summary>
        public static void Reload() { current = Load(); Changed(save: false); }

        public static void Changed(bool save = true)
        {
            var s = Current;
            s.master = Mathf.Clamp01(s.master); s.effects = Mathf.Clamp01(s.effects);
            s.interfaceVolume = Mathf.Clamp01(s.interfaceVolume); s.ambience = Mathf.Clamp01(s.ambience);
            s.fov = Mathf.Clamp(s.fov, 55, 110);
            s.mouseSensitivity = Mathf.Clamp(s.mouseSensitivity, .1f, 4); s.stickSensitivity = Mathf.Clamp(s.stickSensitivity, .1f, 4);
            s.screenShake = Mathf.Clamp(s.screenShake, 0, 2);
            AudioListener.volume = Muted ? 0 : s.master;
            s.renderScale = Mathf.Clamp(s.renderScale, .5f, 2); s.particleDensity = Mathf.Clamp(s.particleDensity, .25f, 1);
            s.drawDistance = Mathf.Clamp(s.drawDistance, 80, 500);
            s.antiAliasing = Mathf.Clamp(s.antiAliasing, 0, GraphicsApply.AntiAliasingNames.Length - 1); s.shadows = Mathf.Clamp(s.shadows, 0, 3);
            s.displayMode = Mathf.Clamp(s.displayMode, 0, 2);
            if (s.quality >= 0 && s.quality < QualitySettings.names.Length && QualitySettings.GetQualityLevel() != s.quality)
                QualitySettings.SetQualityLevel(s.quality, true);
            GraphicsApply.Apply(s);
            if (save) Storage.Write(File, JsonUtility.ToJson(s, true));
            OnChanged?.Invoke();
        }

        public string Override(string key) => bindings.Find(b => b.key == key)?.path;

        public void SetOverride(string key, string path)
        {
            bindings.RemoveAll(b => b.key == key);
            if (!string.IsNullOrEmpty(path)) bindings.Add(new BindingOverride { key = key, path = path });
        }
    }
}

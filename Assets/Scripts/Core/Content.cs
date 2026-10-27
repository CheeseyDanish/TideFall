// TideWall 3D — where the game finds its assets. One ContentLibrary asset (Assets/Content/ContentLibrary.asset)
// references the data, base materials, every model and every sound; the GameManager in the scene references the
// library. No Resources folder: everything that ships is reachable from the scene, so builds only include what's used
// and anything can be found, swapped or inspected in the editor.
//
// TideWall → Setup Project fills the library (models from Assets/Content/Models, sounds from Assets/Content/Sounds).
using System;
using System.Collections.Generic;
using UnityEngine;

namespace TideWall
{
    public static class Content
    {
        public static ContentLibrary Library { get; private set; }
        static readonly Dictionary<string, GameObject> models = new();
        static readonly Dictionary<string, AudioClip> sounds = new();
        static readonly Dictionary<string, Texture2D> glyphs = new();

        /// <summary>The GameManager hands over its library when it wakes.</summary>
        public static void Use(ContentLibrary library)
        {
            Library = library;
            models.Clear(); sounds.Clear(); glyphs.Clear();
            if (library == null) return;
            foreach (var m in library.models) if (m.prefab != null) models[m.name] = m.prefab;
            foreach (var s in library.sounds) if (s.clip != null) sounds[s.name] = s.clip;
            foreach (var g in library.glyphs) if (g.texture != null) glyphs[g.name] = g.texture;
        }

        static void Ensure()
        {
#if UNITY_EDITOR
            // Editor tools run without a scene's GameManager: read the library straight from the project
            if (Library == null) Use(UnityEditor.AssetDatabase.LoadAssetAtPath<ContentLibrary>("Assets/Content/ContentLibrary.asset"));
#endif
        }

        public static GameObject Model(string name)
        {
            Ensure();
            if (models.TryGetValue(name, out var p)) return p;
            Debug.LogWarning($"TideWall: model '{name}' isn't in the content library (TideWall → Setup Project adds new ones)");
            models[name] = null;
            return null;
        }

        public static IReadOnlyDictionary<string, AudioClip> Sounds { get { Ensure(); return sounds; } }

        /// <summary>A symbol's texture from Assets/Content/Glyphs, or null (Glyphs draws it then).</summary>
        public static Texture2D Glyph(string name) { Ensure(); return glyphs.TryGetValue(name, out var t) ? t : null; }

        public static Material Material(string name)
        {
            Ensure();
            var l = Library;
            if (l == null) return null;
            return name switch
            {
                "Lit" => l.lit, "LitTransparent" => l.litTransparent, "Unlit" => l.unlit,
                "Particle" => l.particle, "ParticleAdditive" => l.particleAdditive, "Sky" => l.sky, _ => null,
            };
        }

        public static GameDatabase Database { get { Ensure(); return Library != null ? Library.database : null; } }
        public static PhaseCycle Phases { get { Ensure(); return Library != null ? Library.phases : null; } }
        public static Campaign Campaign { get { Ensure(); return Library != null ? Library.campaign : null; } }
    }
}

// TideWall 3D — the content library asset (Assets/Content/ContentLibrary.asset): the data, base materials, and every
// model and sound by name. The scene's GameManager references it; TideWall → Setup Project fills it.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace TideWall
{
    [CreateAssetMenu(menuName = "TideWall/Content Library", fileName = "ContentLibrary")]
    public class ContentLibrary : ScriptableObject
    {
        [Serializable] public struct NamedModel { public string name; public GameObject prefab; }
        [Serializable] public struct NamedSound { public string name; public AudioClip clip; }
        [Serializable] public struct NamedGlyph { public string name; public Texture2D texture; }

        [Header("Data")]
        public GameDatabase database;
        public PhaseCycle phases;
        public Campaign campaign;

        [Header("Base materials (runtime colours are copies of these)")]
        public Material lit;
        public Material litTransparent, unlit, particle, particleAdditive, sky;

        [Header("Models and sounds, by name")]
        public List<NamedModel> models = new();
        public List<NamedSound> sounds = new();
        [Tooltip("The hidden story's symbols (docs/STORY.md), from Assets/Content/Glyphs")] public List<NamedGlyph> glyphs = new();
    }
}

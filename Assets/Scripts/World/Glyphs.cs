// TideWall 3D — putting the hidden story's symbols (docs/STORY.md) into the world and the UI: a glyph as a sprite,
// a material, or a decal (a flat quad laid on a wall, a sail, a shell, a stone). Textures come from
// Assets/Content/Glyphs (made by setup from GlyphArt; repaintable), drawn by GlyphArt if missing.
using System.Collections.Generic;
using UnityEngine;

namespace TideWall
{
    public static class Glyphs
    {
        static readonly Dictionary<string, Texture2D> drawn = new();
        static readonly Dictionary<string, Sprite> sprites = new();
        static readonly Dictionary<(string, Color, bool), Material> materials = new();

        public static Texture2D Texture(string name)
        {
            var t = Content.Glyph(name);
            if (t != null) return t;
            if (!drawn.TryGetValue(name, out t) || t == null) drawn[name] = t = GlyphArt.Texture(name);
            return t;
        }

        public static Sprite Sprite(string name)
        {
            if (sprites.TryGetValue(name, out var s) && s != null) return s;
            var t = Texture(name);
            return sprites[name] = UnityEngine.Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(.5f, .5f));
        }

        /// <summary>An unlit, see-through material showing the glyph in this colour. Glowing ones add light (embers,
        /// shards, the Heart); plain ones are carved, painted or stained.</summary>
        public static Material Material(string name, Color color, bool glow = false)
        {
            var key = (name, color, glow);
            if (materials.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(glow ? Mats.ParticleAdditive : Mats.Particle) { name = $"Glyph {name}{(glow ? " (glow)" : "")}" };
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", Texture(name));
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", Texture(name));
            Mats.SetColor(m, color);
            return materials[key] = m;
        }

        /// <summary>A glyph laid flat on something: a quad whose face looks along its local -Z, so turn -Z out of the
        /// surface and set it a hair in front of it (DecalOn does both).</summary>
        public static GameObject Decal(Transform parent, string name, Vector3 localPos, Quaternion localRot, float size, Color color, bool glow = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = $"Glyph {name}";
            Util.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.transform.localScale = Vector3.one * size;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = Material(name, color, glow);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        /// <summary>A decal on a surface facing `normal` (world space), centred at `point`.</summary>
        public static GameObject DecalOn(Transform parent, string name, Vector3 point, Vector3 normal, float size, Color color, bool glow = false, float spin = 0)
        {
            // A quad faces -Z: point its -Z out of the surface
            var rot = Quaternion.LookRotation(-normal) * Quaternion.Euler(0, 0, spin);
            var go = Decal(parent, name, Vector3.zero, Quaternion.identity, size, color, glow);
            go.transform.SetPositionAndRotation(point + normal * .02f, rot);
            return go;
        }
    }
}

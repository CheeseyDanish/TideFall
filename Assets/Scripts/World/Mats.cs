// TideWall 3D — runtime materials. Base materials live in Assets/Content/Materials (made by the editor setup,
// TideWall → Setup Project) and are referenced by the content library, so their URP shaders are always in builds;
// runtime colours are clones of those. Falls back to Shader.Find in the editor if the assets are missing.
using System.Collections.Generic;
using UnityEngine;

namespace TideWall
{
    public static class Mats
    {
        static readonly Dictionary<string, Material> bases = new();
        static readonly Dictionary<(string, Color), Material> tinted = new();

        static Material Base(string name, string shader)
        {
            if (bases.TryGetValue(name, out var m) && m != null) return m;
            m = Content.Material(name);
            if (m == null)
            {
                var s = Shader.Find(shader) ?? Shader.Find("Universal Render Pipeline/Unlit");
                m = new Material(s) { name = name };
            }
            return bases[name] = m;
        }

        public static Material Lit(Color c) => Get("Lit", "Universal Render Pipeline/Lit", c);
        public static Material Unlit(Color c) => Get("Unlit", "Universal Render Pipeline/Unlit", c);
        public static Material Transparent(Color c) => Get("LitTransparent", "Universal Render Pipeline/Lit", c);
        public static Material Particle => Base("Particle", "Universal Render Pipeline/Particles/Unlit");
        public static Material ParticleAdditive => Base("ParticleAdditive", "Universal Render Pipeline/Particles/Unlit");

        static Material Get(string baseName, string shader, Color c)
        {
            if (tinted.TryGetValue((baseName, c), out var m) && m != null) return m;
            m = new Material(Base(baseName, shader)) { name = baseName + "_" + ColorUtility.ToHtmlStringRGBA(c) };
            SetColor(m, c);
            return tinted[(baseName, c)] = m;
        }

        public static void SetColor(Material m, Color c)
        {
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        }

        public static void SetEmission(Material m, Color c)
        {
            if (!m.HasProperty("_EmissionColor")) return;
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            m.SetColor("_EmissionColor", c);
        }
    }
}

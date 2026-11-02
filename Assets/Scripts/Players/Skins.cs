// TideWall 3D — weapon skins: a copy of the model's materials using the TideWall/Skin shader, which draws the skin's
// pattern procedurally (no textures, like every other model; R4). Wood, gold and black parts count as trim and take the
// skin's third colour, so a skinned gun still reads as a gun. The attachments keep their own colours.
// What's unlocked is in the Legacy record: skins outlive runs.
using System.Collections.Generic;
using UnityEngine;

namespace TideWall
{
    public static class Skins
    {
        static readonly Dictionary<(string, bool), Material> cache = new();

        static bool IsTrim(string material)
        {
            foreach (var t in new[] { "wood", "brown", "gold", "black" }) if (material.StartsWith(t)) return true;
            return false;
        }

        static Material For(SkinDef s, bool trim)
        {
            if (cache.TryGetValue((s.id, trim), out var m) && m != null) return m;
            var main = trim ? s.colorC : s.colorA;
            var second = trim ? s.colorC * .62f : s.colorB;
            m = new Material(Mats.Skin) { name = $"{s.id}{(trim ? " trim" : "")}" };
            m.SetColor("_BaseColor", main);
            m.SetColor("_ColorB", new Color(second.r, second.g, second.b, 1));
            m.SetColor("_Glow", s.glow);
            m.SetFloat("_Pattern", (int)s.pattern);
            m.SetFloat("_Scale", s.patternScale);
            m.SetFloat("_Pulse", s.pulse);
            m.SetFloat("_Metallic", s.metallic);
            m.SetFloat("_Smoothness", s.smoothness);
            return cache[(s.id, trim)] = m;
        }

        /// <summary>Dress a freshly spawned model. Call before attachments go on. Null or factory: leave it as it is.</summary>
        public static void Apply(Model model, SkinDef skin)
        {
            if (model == null || skin == null || skin.pattern == SkinPattern.Factory) return;
            foreach (var r in model.Renderers)
            {
                if (r == null) continue;
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) if (mats[i] != null) mats[i] = For(skin, IsTrim(mats[i].name));
                r.sharedMaterials = mats;
            }
        }

        public static bool Unlocked(SkinDef s, Legacy legacy) => (s.pearls <= 0 && s.kills <= 0) || legacy.skins.Contains(s.id) || (s.kills > 0 && legacy.kills >= s.kills);
    }
}

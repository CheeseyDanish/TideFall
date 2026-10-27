// TideWall 3D — the Blender models (Assets/Content/Models, made by Blender/generate_models.py) and the component
// that sits on every spawned one. A model's moving parts are named in Blender: "Limb_*" (creature limbs that can be
// shot off), "Socket_*" (attachment points: muzzles, hands, lights), "Part_*" (jointed people), and "Body".
using System.Collections.Generic;
using UnityEngine;

namespace TideWall
{
    public static class ModelFactory
    {
        /// <summary>How models are copied. The editor's world builder swaps in one that keeps the scene's copies linked
        /// to their model assets (so a re-exported model updates the scene).</summary>
        public static System.Func<GameObject, Transform, GameObject> Instantiate = (prefab, parent) => Object.Instantiate(prefab, parent);

        /// <summary>Spawn a model by name. colliders: add a BoxCollider per mesh part (triggers by default: for shots and melee).</summary>
        public static Model Spawn(string name, Transform parent, Vector3 localPos, float scale = 1f, bool colliders = false, bool triggers = true)
        {
            var prefab = Content.Model(name);
            GameObject go;
            if (prefab != null) go = Instantiate(prefab, parent);
            else { go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.transform.SetParent(parent, false); }
            go.name = name;
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one * scale;
            if (colliders)
                foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
                {
                    var ls = mf.transform.lossyScale;
                    if (mf.sharedMesh == null || ls.x < 0 || ls.y < 0 || ls.z < 0) continue;   // mirrored decals: box colliders can't be negative
                    var bc = mf.gameObject.AddComponent<BoxCollider>();
                    bc.center = mf.sharedMesh.bounds.center;
                    bc.size = Vector3.Max(mf.sharedMesh.bounds.size, Vector3.one * .12f);
                    bc.isTrigger = triggers;
                }
            var model = go.AddComponent<Model>();
            foreach (var r in model.Renderers) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return model;
        }

        /// <summary>World-space bounds of everything renderable under a transform.</summary>
        public static Bounds Bounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one);
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        /// <summary>Give a model's materials a new colour wherever the material name contains `match` (e.g. "coat").</summary>
        public static void Recolor(Model mi, string match, Color color)
        {
            foreach (var r in mi.Renderers)
            {
                var mats = r.materials;   // instantiates per-renderer copies, intended here
                foreach (var m in mats)
                    if (m.name.ToLowerInvariant().Contains(match)) Mats.SetColor(m, color);
                r.materials = mats;
            }
        }
    }
}

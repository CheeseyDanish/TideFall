// TideWall 3D — instantiates the Blender models from Assets/Resources/Models (see Blender/generate_models.py).
using System.Collections.Generic;
using UnityEngine;

namespace TideWall
{
    public class ModelInstance
    {
        public GameObject root;
        public Transform body;
        public readonly Dictionary<string, Transform> limbs = new();     // key: name without "Limb_"
        public readonly Dictionary<string, Transform> sockets = new();   // key: name without "Socket_"
        public Renderer[] renderers;
    }

    public static class ModelFactory
    {
        static readonly Dictionary<string, GameObject> cache = new();

        public static GameObject Prefab(string name)
        {
            if (!cache.TryGetValue(name, out var p))
            {
                p = Resources.Load<GameObject>("Models/" + name);
                if (p == null) Debug.LogWarning($"TideWall: model '{name}' not found in Resources/Models");
                cache[name] = p;
            }
            return p;
        }

        /// <summary>Spawn a model. colliders: add a trigger BoxCollider per mesh part (for shots and melee).</summary>
        public static ModelInstance Spawn(string name, Transform parent, Vector3 localPos, float scale = 1f, bool colliders = false, bool triggers = true)
        {
            var mi = new ModelInstance();
            var prefab = Prefab(name);
            mi.root = prefab != null ? Object.Instantiate(prefab, parent) : GameObject.CreatePrimitive(PrimitiveType.Cube);
            if (prefab == null) mi.root.transform.SetParent(parent, false);
            mi.root.name = name;
            mi.root.transform.localPosition = localPos;
            mi.root.transform.localRotation = Quaternion.identity;
            mi.root.transform.localScale = Vector3.one * scale;
            foreach (var t in mi.root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.StartsWith("Limb_")) mi.limbs[t.name.Substring(5)] = t;
                else if (t.name.StartsWith("Socket_")) mi.sockets[t.name.Substring(7)] = t;
                else if (t.name == "Body") mi.body = t;
                if (colliders && t.TryGetComponent<MeshFilter>(out var mf) && mf.sharedMesh != null)
                {
                    var bc = t.gameObject.AddComponent<BoxCollider>();
                    bc.center = mf.sharedMesh.bounds.center;
                    bc.size = Vector3.Max(mf.sharedMesh.bounds.size, Vector3.one * .12f);
                    bc.isTrigger = triggers;
                }
            }
            if (mi.body == null) mi.body = mi.root.transform;
            mi.renderers = mi.root.GetComponentsInChildren<Renderer>(true);
            foreach (var r in mi.renderers) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return mi;
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
        public static void Recolor(ModelInstance mi, string match, Color color)
        {
            foreach (var r in mi.renderers)
            {
                var mats = r.materials;   // instantiates per-renderer copies, intended here
                foreach (var m in mats)
                    if (m.name.ToLowerInvariant().Contains(match)) Mats.SetColor(m, color);
                r.materials = mats;
            }
        }
    }
}

// TideWall 3D — the component on every spawned model. Finds the parts named in Blender ("Limb_*", "Socket_*",
// "Body") when it wakes, so it works the same for models spawned in play and models saved in a scene or prefab.
using System.Collections.Generic;
using UnityEngine;

namespace TideWall
{
    /// <summary>A spawned model. Finds its limbs and sockets by name when it wakes, so it works the same whether it was
    /// spawned in play or saved in a scene or prefab.</summary>
    public class Model : MonoBehaviour
    {
        Dictionary<string, Transform> limbs, sockets;
        Transform body;
        Renderer[] renderers;

        public GameObject root => gameObject;
        /// <summary>Key: the name without "Limb_".</summary>
        public Dictionary<string, Transform> Limbs { get { Index(); return limbs; } }
        /// <summary>Key: the name without "Socket_".</summary>
        public Dictionary<string, Transform> Sockets { get { Index(); return sockets; } }
        public Transform Body { get { Index(); return body; } }
        public Renderer[] Renderers { get { Index(); return renderers; } }

        void Awake() => Index();

        void Index()
        {
            if (limbs != null) return;
            limbs = new Dictionary<string, Transform>(); sockets = new Dictionary<string, Transform>();
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name.StartsWith("Limb_")) limbs[t.name.Substring(5)] = t;
                else if (t.name.StartsWith("Socket_")) sockets[t.name.Substring(7)] = t;
                else if (t.name == "Body") body = t;
            }
            if (body == null) body = transform;
            renderers = GetComponentsInChildren<Renderer>(true);
        }
    }
}

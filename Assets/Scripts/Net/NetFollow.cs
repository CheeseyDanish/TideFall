// TideWall 3D — on a client, smoothly follows the position the host reports for a replicated thing.
using System.Collections.Generic;
using UnityEngine;

namespace TideWall
{
    /// <summary>Client: a replicated thing that isn't a living enemy (corpse, gas can, food) glides to the host's pose.</summary>
    public class NetFollow : MonoBehaviour
    {
        public Vector3 pos;
        public Quaternion rot = Quaternion.identity;
        bool started;

        public void Set(Vector3 p, Quaternion r)
        {
            pos = p; rot = r;
            if (!started) { transform.SetPositionAndRotation(p, r); started = true; }
        }

        void LateUpdate()
        {
            if (!started) return;
            float k = 1 - Mathf.Exp(-Time.deltaTime * 14);
            transform.position = Vector3.Distance(transform.position, pos) > 8 ? pos : Vector3.Lerp(transform.position, pos, k);
            transform.rotation = Quaternion.Slerp(transform.rotation, rot, k);
        }
    }
}

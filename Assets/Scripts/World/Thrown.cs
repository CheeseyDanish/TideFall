// TideWall 3D — a carried thing in flight after a throw: lands, and sells itself if it lands in the fishmonger's cart.
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace TideWall
{
    /// <summary>Simple ballistic flight for thrown carryables (no rigidbody needed).</summary>
    public class Thrown : MonoBehaviour
    {
        public Vector3 velocity;
        public System.Action onLand;
        float spin;

        void Update()
        {
            float dt = Time.deltaTime;
            velocity += Physics.gravity * dt;
            var p = transform.position + velocity * dt;
            spin += 540 * dt;
            transform.rotation = Quaternion.Euler(spin, transform.eulerAngles.y, 0);
            float ground = Arena.GroundY(p.z);
            if (p.y <= ground)
            {
                p.y = ground;
                transform.position = p;
                transform.rotation = Quaternion.Euler(0, transform.eulerAngles.y, 0);
                if (Arena.InWater(p)) Fx.Splash(p, 8); else Fx.Smoke(p, 3, .3f, new Color(.9f, .82f, .6f, .6f));
                Destroy(this);
                onLand?.Invoke();
                return;
            }
            transform.position = p;
        }
    }
}

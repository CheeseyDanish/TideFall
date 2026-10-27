// TideWall 3D — a lobbed shell (the pearl cannon): arcs to its target and explodes.
using System.Collections.Generic;
using UnityEngine;

namespace TideWall
{
    /// <summary>A pearl cannon's shot: an arc, then a bang.</summary>
    public class Lob : MonoBehaviour
    {
        Vector3 from, to;
        float t, time, damage, radius;

        public void Launch(Vector3 a, Vector3 b, float dmg, float r)
        {
            from = a; to = b; damage = dmg; radius = r;
            time = Mathf.Clamp(Vector3.Distance(a, b) / 18, .4f, 1.4f);
            var m = ModelFactory.Spawn("Pearl", transform, Vector3.zero, 1.6f);
            foreach (var rd in m.Renderers) rd.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            transform.position = a;
        }

        void Update()
        {
            t += Time.deltaTime / time;
            var p = Vector3.Lerp(from, to, t) + Vector3.up * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t)) * 5;
            transform.position = p;
            if (t >= 1)
            {
                Combat.Explode(to, radius, damage, Element.None, null);
                Destroy(gameObject);
            }
        }
    }
}

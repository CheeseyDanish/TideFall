// TideWall 3D — The Last Light: a corpse flying into the try-pot.
using UnityEngine;

namespace TideWall
{
    /// <summary>A corpse on its way into the try-pot.</summary>
    public class CorpseVacuum : MonoBehaviour
    {
        const float Seconds = .8f;
        Corpse corpse; Vector3 from, to; float t;

        public static void Begin(Corpse c, Vector3 to, float delay)
        {
            var v = c.gameObject.AddComponent<CorpseVacuum>();
            v.corpse = c; v.from = c.transform.position; v.to = to; v.t = -delay;
            if (c.TryGetComponent<DeathFlop>(out var flop)) Destroy(flop);
        }

        void Update()
        {
            if (corpse == null || corpse.Holder != null) { Destroy(this); return; }
            t += Time.deltaTime / Seconds;
            if (t <= 0) return;
            float k = t * t;   // slowly, then all at once
            var at = Vector3.Lerp(from, to, k);
            at.y += Mathf.Sin(k * Mathf.PI) * 2.5f;
            transform.position = at;
            transform.localScale = Vector3.one * Mathf.Lerp(1, .3f, k);
            if (t >= 1) Fishmonger.Sell(corpse);
        }
    }
}

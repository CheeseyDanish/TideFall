// TideWall 3D — the way deeper at the end of a Part II stage: a stone arch that wakes when the stage's work is done.
// Stand under it for a moment and the expedition moves on.
using UnityEngine;

namespace TideWall
{
    public class ExitGate : MonoBehaviour
    {
        [SerializeField] Light glow;
        [SerializeField] Renderer glyph;
        public bool Open { get; private set; }
        float standT;

        public static ExitGate Create(Transform parent, Vector3 pos)
        {
            var go = ModelFactory.Spawn("ExitGate", parent, pos, 1, colliders: true, triggers: false).root;
            var g = go.AddComponent<ExitGate>();
            g.glow = new GameObject("Glow").AddComponent<Light>();
            g.glow.transform.SetParent(go.transform, false);
            g.glow.transform.localPosition = new Vector3(0, 3, 0);
            g.glow.type = LightType.Point; g.glow.range = 14; g.glow.color = new Color(.5f, 1f, .8f); g.glow.intensity = 0; g.glow.shadows = LightShadows.None;
            g.glyph = Glyphs.Decal(go.transform, "wave", new Vector3(0, 5.3f, -.62f), Quaternion.identity, 1.6f, new Color(.5f, 1f, .8f), glow: true).GetComponent<Renderer>();
            g.glyph.enabled = false;
            // The opening itself is walkable: only the pillars block
            foreach (var c in go.GetComponentsInChildren<Collider>()) if (c.bounds.size.x > 3) c.enabled = false;
            return g;
        }

        public void SetOpen()
        {
            if (Open) return;
            Open = true;
            if (glyph) glyph.enabled = true;
            Sfx.Play("bell", .7f);
        }

        void Update()
        {
            if (glow) glow.intensity = Mathf.MoveTowards(glow.intensity, Open ? 8 : 0, Time.deltaTime * 6);
            if (!Open || Net.IsClient) return;
            var gm = GameManager.I;
            if (gm == null || gm.State != GameState.Playing) return;
            bool someone = false;
            foreach (var p in gm.Players.All)
            {
                if (p.IsOut || p.IsDowned) continue;
                var d = p.transform.position - transform.position; d.y = 0;
                if (d.magnitude < 3.5f) someone = true;
            }
            standT = someone ? standT + Time.deltaTime : 0;
            if (standT > 1.2f) { standT = -999; gm.Expedition.NextStage(); }
        }
    }
}

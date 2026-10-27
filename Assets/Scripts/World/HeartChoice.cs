// TideWall 3D — one of the two answers at the Heart, as a place to stand and press Interact. It only shows once the
// Tidemother has fallen, and the second only with all five Spiral Shards. The words are the campaign's (EndingDef.choice).
using UnityEngine;

namespace TideWall
{
    public class HeartChoice : Interactable
    {
        [SerializeField] HeartAltar altar;
        [SerializeField] string ending;
        [SerializeField] Renderer glyph;
        int shards;

        void Start() => shards = Legacy.Load().ShardCount;

        public static HeartChoice Create(Transform parent, Vector3 localPos, HeartAltar altar, string ending, string glyphName)
        {
            var go = new GameObject($"Choice: {ending}");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var c = go.AddComponent<HeartChoice>();
            c.altar = altar; c.ending = ending; c.radius = 2.4f;
            c.glyph = Glyphs.Decal(go.transform, glyphName, new Vector3(0, .05f, 0), Quaternion.Euler(90, 0, 0), 1.8f, new Color(1f, .9f, .6f, .85f), glow: true).GetComponent<Renderer>();
            c.glyph.enabled = false;
            return c;
        }

        public override string Prompt(Player p) => altar != null && altar.Offers(ending) ? GameManager.I.Campaign.Ending(ending)?.choice : null;

        public override void Interact(Player p) => altar.Choose(ending);

        void Update()
        {
            // (A client can't ask the host's record, so it shows the second answer by its own shards)
            if (glyph) glyph.enabled = altar != null && altar.State == HeartAltar.Phase.Choosing && (Net.IsClient ? ending == "strike" || shards >= GameManager.I.Campaign.shardCount : altar.Offers(ending));
        }
    }
}

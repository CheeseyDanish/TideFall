// TideWall 3D — The Last Light: a wreck the low tide has uncovered, and the sea chest lying in it. First to it opens it.
// The sea takes back whatever's left when the tide turns.
using UnityEngine;

namespace TideWall
{
    public class SalvageCrate : Interactable
    {
        bool proxy;

        static SalvageCrate Make(Vector3 at, int id)
        {
            var go = new GameObject("Salvage");
            go.transform.position = at;
            var c = go.AddComponent<SalvageCrate>();
            c.radius = 3;
            var rng = new System.Random(Mathf.RoundToInt(at.x * 13 + at.z * 7));
            var hull = ModelFactory.Spawn("WreckHull", go.transform, new Vector3(1.5f, 0, 1.2f), .55f);
            hull.root.transform.localRotation = Quaternion.Euler((float)rng.NextDouble() * 24 - 12, (float)rng.NextDouble() * 360, 14 + (float)rng.NextDouble() * 30);
            ModelFactory.Spawn("SalvageCrate", go.transform, Vector3.zero);
            var label = Ui.Label(go.transform, "Sign", new Vector3(0, 2.2f, 0), .4f, new Color(1, .9f, .55f));
            label.text = "SALVAGE";
            Billboard.Add(label.gameObject, upright: true, fadeNear: 4);
            NetId.Tag(go, NetKind.Salvage, 0, id);
            return c;
        }

        public static SalvageCrate Spawn(Vector3 at) => Make(at, 0);
        public static SalvageCrate SpawnProxy(int id, Vector3 at) { var c = Make(at, id); c.proxy = true; return c; }

        public override string Prompt(Player p) => proxy ? null : "Open the sea chest";

        public override void Interact(Player p)
        {
            if (proxy || GameManager.I.Level is not Lighthouse l) return;
            l.Loot(p, transform.position);
            Fx.Debris(transform.position + Vector3.up * .5f, new Color(.55f, .4f, .25f), 12);
            Sfx.Play("coin");
            Destroy(gameObject);
        }
    }
}

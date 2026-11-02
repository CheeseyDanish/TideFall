// TideWall 3D — a patch of bubbles where the fish are biting. On Part I's shore in the mornings; in The Last Light it
// stays at the waterline as the sea rises and falls. A few catches and it's fished out.
using UnityEngine;

namespace TideWall
{
    public class FishingSpot : Interactable
    {
        int catches = 2;
        float bubbleT;
        bool proxy;
        Vector3 around;

        /// <summary>Part I: at this point on the shore. The Last Light: `dir` is the direction from the island's centre.</summary>
        public static FishingSpot Spawn(Vector3 dirOrPoint)
        {
            var s = Make(dirOrPoint, 0);
            return s;
        }

        public static FishingSpot SpawnProxy(int id, Vector3 pos)
        {
            var s = Make(pos, id);
            s.proxy = true;
            return s;
        }

        static FishingSpot Make(Vector3 v, int id)
        {
            var go = new GameObject("Fishing Spot");
            var s = go.AddComponent<FishingSpot>();
            s.radius = 6;
            s.around = v;
            go.transform.position = s.Where();
            ModelFactory.Spawn("FishSpot", go.transform, Vector3.zero);
            NetId.Tag(go, NetKind.FishSpot, 0, id);
            return s;
        }

        Vector3 Where()
        {
            if (!Arena.Island) return new Vector3(around.x, Arena.WaterLevel + .03f, around.z);
            if (proxy) return new Vector3(around.x, Arena.WaterLevel + .03f, around.z);
            var d = new Vector3(around.x, 0, around.z).normalized;
            var at = d * (Arena.ShoreRadius() + 2.5f);
            at.y = Arena.WaterLevel + .03f;
            return at;
        }

        public override string Prompt(Player p) => proxy || p.CarryCount > 0 ? null : "Fish here";
        public override void Interact(Player p) { if (!proxy) p.StartFishing(this); }

        /// <summary>A fish was landed here. Fished out after a couple.</summary>
        public void Caught()
        {
            if (--catches <= 0) { Fx.Splash(transform.position, 6); Destroy(gameObject); }
        }

        void Update()
        {
            transform.position = Where();
            if ((bubbleT -= Time.deltaTime) > 0) return;
            bubbleT = Random.Range(.2f, .5f);
            Fx.BubblesLocal(transform.position + new Vector3(Random.Range(-.8f, .8f), .05f, Random.Range(-.8f, .8f)));
        }
    }
}

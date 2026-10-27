// TideWall 3D — the expedition's supply cache at the start of each Part II stage: the shop, carried down the seabed.
using UnityEngine;

namespace TideWall
{
    public class SupplyCache : Interactable
    {
        public static SupplyCache Create(Transform parent, Vector3 pos, float yaw)
        {
            var go = ModelFactory.Spawn("SupplyCache", parent, pos).root;
            go.transform.rotation = Quaternion.Euler(0, yaw, 0);
            var col = go.AddComponent<BoxCollider>(); col.center = new Vector3(0, .6f, 0); col.size = new Vector3(2.2f, 1.2f, 1.4f);
            var c = Add<SupplyCache>(go, "SUPPLIES", 3f);
            Glyphs.Decal(go.transform, "crest", new Vector3(0, .75f, -.71f), Quaternion.identity, .9f, new Color(.95f, .85f, .6f, .9f));
            return c;
        }

        public override string Prompt(Player p) => "Supplies";

        public override void Interact(Player p) => p.OpenMenu(Menus.Shop(p, Menus.ShopTab.Guns, transform.position));
    }
}

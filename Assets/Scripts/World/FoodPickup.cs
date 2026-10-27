// TideWall 3D — food lying on the beach: walk up and eat it.
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace TideWall
{
    public class FoodPickup : Interactable
    {
        public FoodDef Food;
        float baseY;

        public static FoodPickup Spawn(FoodDef food, Vector3 pos, int id = 0)
        {
            var go = new GameObject(food.displayName);
            go.transform.position = pos;
            var f = go.AddComponent<FoodPickup>();
            f.Food = food; f.radius = 1.6f; f.baseY = .35f;
            var mi = ModelFactory.Spawn(food.model, go.transform, Vector3.zero, 1.6f);
            foreach (var r in mi.Renderers) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            NetId.Tag(go, NetKind.Food, GameManager.I.DB.foods.IndexOf(food), id);
            return f;
        }

        public override string Prompt(Player p) => $"Eat {Food.displayName} (+{Food.heal:0} HP)";
        public override void Interact(Player p) { p.Eat(Food, null); Destroy(gameObject); }

        void Update()
        {
            var t = transform;
            t.rotation = Quaternion.Euler(0, Time.time * 60, 0);
            t.position = new Vector3(t.position.x, baseY + Mathf.Sin(Time.time * 2.5f) * .08f, t.position.z);
        }
    }
}

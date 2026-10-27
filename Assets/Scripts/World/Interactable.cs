// TideWall 3D — the base for things players walk up to and press Interact on (ShopKiosk, Fishmonger, HotdogStand,
// ReadyBell, FoodPickup, and every Carryable: corpses and gas cans), and finding the nearest one.
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace TideWall
{
    public abstract class Interactable : MonoBehaviour
    {
        public static readonly List<Interactable> All = new();
        public float radius = 2.2f;

        protected virtual void OnEnable() => All.Add(this);
        protected virtual void OnDisable() => All.Remove(this);

        /// <summary>The button prompt shown to this player, or null if there's nothing to do here.</summary>
        public abstract string Prompt(Player p);
        public abstract void Interact(Player p);
        public virtual Vector3 Point => transform.position;

        public static Interactable Nearest(Player p)
        {
            Interactable best = null; float bd = float.MaxValue;
            var pos = p.transform.position;
            foreach (var it in All)
            {
                if (it == null || !it.isActiveAndEnabled) continue;
                var d = it.Point - pos; d.y = 0;
                float dist = d.magnitude;
                if (dist > it.radius || dist >= bd || it.Prompt(p) == null) continue;
                best = it; bd = dist;
            }
            return best;
        }

        /// <summary>Attach an interactable to a world prop, with a floating sign.</summary>
        public static T Add<T>(GameObject go, string sign, float radius) where T : Interactable
        {
            var it = go.AddComponent<T>();
            it.radius = radius;
            var label = Ui.Label(go.transform, "Sign", new Vector3(0, 3.6f, 0), .8f, new Color(1, .95f, .8f));
            label.text = sign;
            label.fontStyle = FontStyles.Bold;
            Billboard.Add(label.gameObject, upright: true, fadeNear: 6);
            return it;
        }
    }
}

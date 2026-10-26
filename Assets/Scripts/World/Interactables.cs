// TideWall 3D — things players walk up to and press Interact on: the shop, the fishmonger, the hotdog stand,
// the ready bell, food on the beach, and anything that can be picked up and thrown (corpses, gas cans).
using System.Collections.Generic;
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
            var label = new GameObject("Sign").AddComponent<TextMesh>();
            label.transform.SetParent(go.transform, false);
            label.transform.localPosition = new Vector3(0, 3.6f, 0);
            label.transform.rotation = Quaternion.Euler(50, 0, 0);
            label.text = sign; label.font = Ui.Font; label.fontSize = 128; label.characterSize = .034f;
            label.anchor = TextAnchor.MiddleCenter; label.color = new Color(1, .95f, .8f);
            label.GetComponent<MeshRenderer>().sharedMaterial = Ui.Font.material;
            return it;
        }
    }

    public class ShopKiosk : Interactable
    {
        public override string Prompt(Player p) => GameManager.I.Phases.ShopOpen ? "Shop" : "Shop (closed until morning)";

        public override void Interact(Player p)
        {
            if (!GameManager.I.Phases.ShopOpen) { Sfx.Play("deny"); p.Toast("The shopkeeper is hiding until morning."); return; }
            p.OpenMenu(Menus.Shop(p, Menus.ShopTab.Guns, transform.position));
        }
    }

    public class Fishmonger : Interactable
    {
        public override string Prompt(Player p)
        {
            int n = 0, v = 0;
            foreach (var c in p.Carrying) if (c is Corpse corpse) { n++; v += corpse.Value; }
            if (!GameManager.I.Phases.ShopOpen) return n > 0 ? "Fishmonger (buys in the morning)" : null;
            return n > 0 ? $"Sell {n} {(n == 1 ? "corpse" : "corpses")} (+{v})" : "Fishmonger: bring corpses (or throw them in!)";
        }

        public override void Interact(Player p)
        {
            if (!GameManager.I.Phases.ShopOpen) { Sfx.Play("deny"); GameManager.I.World.fishmonger?.Say("Come back in the morning!"); return; }
            int sold = 0;
            for (int i = p.Carrying.Count - 1; i >= 0; i--)
                if (p.Carrying[i] is Corpse c) { p.Carrying.RemoveAt(i); Sell(c); sold++; }
            if (sold > 0) GameManager.I.World.fishmonger?.Say(sold > 2 ? "What a haul!" : null);
        }

        public static void Sell(Corpse c)
        {
            GameManager.I.AddSandDollars(c.Value, c.transform.position, "sold " + c.Def.displayName);
            Fx.Splash(c.transform.position, 6);
            Destroy(c.gameObject);
        }

        /// <summary>A corpse landed: if it's in the cart and the shop is open, it sells.</summary>
        public static bool TryCatch(Corpse c)
        {
            if (!GameManager.I.Phases.ShopOpen) return false;
            foreach (var it in All)
                if (it is Fishmonger f && Vector3.Distance(Flat(f.transform.position), Flat(c.transform.position)) < 2.6f) { Sell(c); return true; }
            return false;
        }

        static Vector3 Flat(Vector3 v) => new(v.x, 0, v.z);
    }

    public class HotdogStand : Interactable
    {
        public override string Prompt(Player p) => "Hotdog stand";
        public override void Interact(Player p)
        {
            p.OpenMenu(Menus.Hotdogs(p, transform.position));
            GameManager.I.World.chef?.Say();
        }
    }

    public class ReadyBell : Interactable
    {
        public override string Prompt(Player p)
        {
            var ph = GameManager.I.Phases.Current;
            if (ph == null || !ph.endWhenAllReady || GameManager.I.State != GameState.Playing) return null;
            return p.Ready ? "Not ready yet" : "Ring the bell (ready)";
        }

        public override void Interact(Player p)
        {
            p.Ready = !p.Ready;
            Sfx.Play("bell", p.Ready ? 1 : .5f, .2f);
            var pm = GameManager.I.Players;
            GameManager.I.Banner(p.Ready ? $"{p.Name} is ready" : $"{p.Name} isn't ready", $"{pm.ReadyCount}/{pm.Count} ready");
        }
    }

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
            foreach (var r in mi.renderers) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
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

    /// <summary>Anything a player can pick up, carry (stacked, up to weight 3) and throw.</summary>
    public abstract class Carryable : Interactable
    {
        public float weight = 1;
        public Player Holder { get; set; }
        public abstract string DisplayName { get; }

        public override string Prompt(Player p)
        {
            if (Holder != null || p.IsDowned) return null;
            return p.CarryWeight + weight <= Player.MaxCarry ? $"Pick up {DisplayName}" : null;
        }

        public override void Interact(Player p) => p.PickUp(this);

        /// <summary>Launch from the player's hands. Lands via Thrown, then OnLanded.</summary>
        public void Throw(Vector3 from, Vector3 velocity)
        {
            Holder = null;
            transform.SetParent(null, true);
            transform.position = from;
            var th = gameObject.AddComponent<Thrown>();
            th.velocity = velocity;
            th.onLand = OnLanded;
        }

        public virtual void OnLanded() { }
    }

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

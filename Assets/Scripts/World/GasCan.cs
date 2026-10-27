// TideWall 3D — a gas can: carry it, throw it, shoot it to blow it up (it leaks fire when hit).
using UnityEngine;

namespace TideWall
{
    /// <summary>A jerry can of fuel. Shoot it, hit it, set it alight or throw it… it may explode.</summary>
    public class GasCan : Carryable
    {
        bool leaking, exploded;
        float leakT;

        public override string DisplayName => leaking ? "leaking gas can" : "gas can";

        /// <summary>Client: a mirror of the host's gas can (it only looks; the host decides when it blows).</summary>
        public static GasCan SpawnProxy(int id, Vector3 pos, Quaternion rot)
        {
            var go = new GameObject("GasCan");
            go.transform.SetPositionAndRotation(pos, rot);
            var g = go.AddComponent<GasCan>();
            g.radius = 1.6f; g.weight = 1; g.proxy = true;
            ModelFactory.Spawn("GasCan", go.transform, Vector3.zero, 1f, colliders: true);
            NetId.Tag(go, NetKind.GasCan, 0, id);
            go.AddComponent<NetFollow>().Set(pos, rot);
            return g;
        }

        bool proxy;
        public bool Leaking { get => leaking; set => leaking = value; }

        public static GasCan Spawn(Vector3 pos)
        {
            var go = new GameObject("GasCan");
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, Random.Range(0, 360f), 0));
            var g = go.AddComponent<GasCan>();
            g.radius = 1.6f; g.weight = 1;
            ModelFactory.Spawn("GasCan", go.transform, Vector3.zero, 1f, colliders: true);
            NetId.Tag(go, NetKind.GasCan, 0);
            return g;
        }

        /// <summary>Something hit the can. Fire always sets it off; otherwise it's a gamble.</summary>
        public void Hit(Element element, Player source)
        {
            if (exploded || proxy) return;
            if (element == Element.Fire || leaking || Random.value < GameManager.I.DB.gasCanBulletChance) Explode(source);
            else
            {
                leaking = true;
                Sfx.Play("hit");
                Fx.Sparks(transform.position + Vector3.up * .4f, new Color(1, .8f, .3f), 6);
            }
        }

        public override void OnLanded()
        {
            if (proxy) return;
            if (Random.value < GameManager.I.DB.gasCanThrowChance) Explode(null);
            else { leaking = true; Sfx.Play("hit"); }
        }

        public void Explode(Player source)
        {
            if (exploded) return;
            exploded = true;
            if (Holder != null) Holder.Drop(this);
            var db = GameManager.I.DB;
            Combat.Explode(transform.position + Vector3.up * .4f, db.gasCanRadius, db.gasCanDamage, Element.Fire, source, hurtsPlayers: true);
            Destroy(gameObject);
        }

        void Update()
        {
            if (!leaking) return;
            leakT -= Time.deltaTime;
            if (leakT <= 0) { leakT = .15f; Fx.Drip(transform.position + Vector3.up * .3f); }
        }
    }
}

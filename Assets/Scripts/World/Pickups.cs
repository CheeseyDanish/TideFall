// TideWall 3D — corpses (sold to the fishmonger) and gas cans (may explode).
using UnityEngine;

namespace TideWall
{
    /// <summary>What's left of a sea creature. Worth less for every limb it's missing. Keeps dripping while it dries.</summary>
    public class Corpse : Carryable
    {
        public EnemyDef Def;
        public int Value;
        public float Wet;
        Bounds bounds;
        float dripAcc, boundsT;

        public override string DisplayName => $"{Def.displayName} corpse ({Value})";

        public void Init(EnemyDef def, int value, float wet)
        {
            Def = def; Value = value; Wet = wet;
            radius = 1.8f;
            weight = def.cost >= 6 ? 3 : def.cost >= 3 ? 2 : 1;   // a shark fills your arms; crabs stack
            if (def.behaviour == EnemyBehaviour.Boss) weight = 3;
        }

        public override string Prompt(Player p)
        {
            if (Def.behaviour == EnemyBehaviour.Boss && Holder == null && !p.IsDowned)
                return p.Carrying.Count == 0 ? $"Drag {DisplayName}" : null;
            return base.Prompt(p);
        }

        public override void OnLanded()
        {
            if (Fishmonger.TryCatch(this)) Sfx.Play("coin");
        }

        void Update()
        {
            if (Arena.InWater(transform.position)) Wet = 1;
            else Wet = Mathf.Max(0, Wet - GameManager.I.DB.wetDrainPerSecond * .5f * Time.deltaTime);
            if ((boundsT -= Time.deltaTime) <= 0) { bounds = ModelFactory.Bounds(gameObject); boundsT = .5f; }
            dripAcc += Wet * GameManager.I.DB.maxDripsPerSecond * .6f * Time.deltaTime;
            while (dripAcc >= 1)
            {
                dripAcc -= 1;
                Fx.Drip(new Vector3(Random.Range(bounds.min.x, bounds.max.x), bounds.min.y + bounds.size.y * Random.Range(.2f, .8f), Random.Range(bounds.min.z, bounds.max.z)));
            }
        }
    }

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

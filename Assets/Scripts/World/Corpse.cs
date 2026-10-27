// TideWall 3D — a dead creature: carried to the fishmonger and sold (worth less for every limb shot off).
using UnityEngine;

namespace TideWall
{
    /// <summary>What's left of a sea creature. Worth less for every limb it's missing. Keeps dripping while it dries.</summary>
    public class Corpse : Carryable
    {
        /// <summary>Every corpse on the beach (so nothing has to search the scene for them every frame).</summary>
        public static readonly System.Collections.Generic.List<Corpse> Live = new();
        public EnemyDef Def;
        public int Value;
        /// <summary>A thieving gull has it in its beak.</summary>
        public Enemy Thief;
        public float Wet;
        Bounds bounds;
        float dripAcc, boundsT;

        // Also an Interactable: the base registers it for Interact, this list is just the corpses
        protected override void OnEnable() { base.OnEnable(); Live.Add(this); }
        protected override void OnDisable() { base.OnDisable(); Live.Remove(this); }

        public override string DisplayName => $"{Def.displayName} corpse ({Value})";

        public void Init(EnemyDef def, int value, float wet)
        {
            Def = def; Value = value; Wet = wet;
            radius = 2.6f;   // generous: corpses are awkward to stand right next to
            weight = def.cost >= 6 ? 3 : def.cost >= 3 ? 2 : 1;   // a shark fills your arms; crabs stack
            if (def.behaviour == EnemyBehaviour.Boss) weight = 3;
        }

        public override Vector3 Point => ModelFactory.Bounds(gameObject).center;

        public override string Prompt(Player p)
        {
            if (Thief != null) return null;
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
}

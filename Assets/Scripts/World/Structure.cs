// TideWall 3D — buildable defences (StructureDef): sandbag barricades that block, urchin spikes that hurt, kelp
// snares that slow, bait buckets that lure, harpoon turrets and pearl cannons that shoot. Players build them from
// the Build menu (sand dollars, golden pearls) on the beach or in town; creatures chew through the ones in their way.
// The host simulates them; online clients see mirrors (NetKind.Structure).
using System.Collections.Generic;
using UnityEngine;

namespace TideWall
{
    public class Structure : MonoBehaviour
    {
        public static readonly List<Structure> All = new();

        public StructureDef Def { get; private set; }
        public float Hp { get; private set; }
        public bool Dead { get; private set; }
        Model model;
        Transform headPart, muzzle;
        float cooldown, flashT;
        bool proxy;
        MaterialPropertyBlock mpb;

        public static Structure Build(StructureDef def, Vector3 pos, float yaw, float hp = -1)
        {
            var s = Make(def, pos, yaw);
            s.Hp = hp > 0 ? Mathf.Min(hp, def.hp) : def.hp;
            NetId.Tag(s.gameObject, NetKind.Structure, GameManager.I.DB.structures.IndexOf(def));
            return s;
        }

        /// <summary>Client: a mirror of the host's defence.</summary>
        public static Structure SpawnProxy(StructureDef def, int id, Vector3 pos, Quaternion rot)
        {
            var s = Make(def, pos, rot.eulerAngles.y);
            s.proxy = true;
            s.Hp = def.hp;
            NetId.Tag(s.gameObject, NetKind.Structure, GameManager.I.DB.structures.IndexOf(def), id);
            return s;
        }

        static Structure Make(StructureDef def, Vector3 pos, float yaw)
        {
            var go = new GameObject(def.displayName);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            var s = go.AddComponent<Structure>();
            s.Def = def;
            s.mpb = new MaterialPropertyBlock();
            s.model = ModelFactory.Spawn(def.model, go.transform, Vector3.zero);
            foreach (var t in s.model.root.GetComponentsInChildren<Transform>(true)) if (t.name == "Part_Head") s.headPart = t;
            s.model.Sockets.TryGetValue("Muzzle", out s.muzzle);
            // Solid ones block players too; traps on the floor don't
            if (def.kind is StructureKind.Barricade or StructureKind.Turret or StructureKind.Cannon or StructureKind.Bait)
            {
                var b = ModelFactory.Bounds(go);
                var bc = go.AddComponent<BoxCollider>();
                bc.center = go.transform.InverseTransformPoint(b.center);
                bc.size = new Vector3(b.size.x, b.size.y, b.size.z);
            }
            All.Add(s);
            return s;
        }

        void OnDestroy() => All.Remove(this);

        /// <summary>Why a structure can't go here (null = it can).</summary>
        public static string WhyNot(StructureDef def, Vector3 spot)
        {
            if (def == null) return "Nothing selected";
            // The level's own rules first (Part I: not in the sea, on the wall or at the powerplant)
            if (GameManager.I != null && GameManager.I.Level.BuildRule(spot, def.radius) is { } why) return why;
            foreach (var s in All)
            {
                if (s == null || s.Dead) continue;
                var d = s.transform.position - spot; d.y = 0;
                if (d.magnitude < s.Def.radius + def.radius) return "Something's already there";
            }
            foreach (var it in Interactable.All)
            {
                if (it == null || it is Carryable || it is FoodPickup) continue;
                var d = it.transform.position - spot; d.y = 0;
                if (d.magnitude < def.radius + 2.2f) return "Too close to a stall";
            }
            if (def.max > 0 && All.FindAll(s => s != null && !s.Dead && s.Def == def).Count >= def.max) return $"At most {def.max} of these";
            return null;
        }

        /// <summary>The nearest bait bucket luring creatures at this position, if any.</summary>
        public static Structure BaitFor(Vector3 pos)
        {
            Structure best = null; float bd = float.MaxValue;
            foreach (var s in All)
            {
                if (s == null || s.Dead || s.Def.kind != StructureKind.Bait) continue;
                float d = Vector3.Distance(s.transform.position, pos);
                if (d < s.Def.range && d < bd) { bd = d; best = s; }
            }
            return best;
        }

        public void Damage(float amount)
        {
            if (Dead || proxy || amount <= 0) return;
            Hp -= amount;
            flashT = .1f;
            if (Random.value < .5f) Fx.Debris(transform.position + Vector3.up * .5f, new Color(.85f, .78f, .6f), 2);
            if (Hp <= 0) Break();
        }

        void Break()
        {
            Dead = true;
            Fx.Debris(transform.position + Vector3.up * .6f, new Color(.85f, .78f, .6f), 20);
            Fx.Smoke(transform.position + Vector3.up * .4f, 8, .6f, new Color(.8f, .75f, .62f, .6f));
            Sfx.Play("boom", .4f, .1f);
            GameManager.I.Banner($"{Def.displayName.ToUpperInvariant()} DESTROYED", "Build another from the Build menu.");
            Destroy(gameObject);
        }

        /// <summary>Client: the host's HP for this one (it breaks when the host removes it).</summary>
        public void ApplyNet(float hp, Quaternion head)
        {
            if (hp < Hp - .5f) flashT = .1f;
            Hp = hp;
            if (headPart != null) headPart.rotation = head;
        }

        public Quaternion HeadRotation => headPart != null ? headPart.rotation : transform.rotation;

        void Update()
        {
            float dt = Time.deltaTime;
            if (flashT > 0) flashT -= dt;
            Look();
            if (proxy || Dead) return;
            var gm = GameManager.I;
            if (gm == null || gm.State != GameState.Playing) return;
            switch (Def.kind)
            {
                case StructureKind.Spikes:
                    foreach (var e in GameManager.Enemies.ToArray())
                        if (e != null && !e.Dead && !e.Riding && Near(e, Def.radius) && e.Def.behaviour != EnemyBehaviour.Flyer)
                        {
                            e.TakeHit(new DamageInfo { amount = Def.damage * dt, ignoreArmor = true, point = e.transform.position }, null);
                            Hp -= dt * 4;   // the spikes wear down
                            if (Random.value < dt * 6) Fx.Gore(e.transform.position + Vector3.up * .2f, new Color(.6f, .05f, .05f), 2);
                        }
                    if (Hp <= 0) Break();
                    break;
                case StructureKind.Snare:
                    foreach (var e in GameManager.Enemies)
                        if (e != null && !e.Dead && Near(e, Def.radius)) e.Snare(Def.slow, .3f);
                    break;
                case StructureKind.Turret:
                case StructureKind.Cannon:
                    Shoot(dt);
                    break;
            }
        }

        bool Near(Enemy e, float r)
        {
            var d = e.transform.position - transform.position; d.y = 0;
            return d.magnitude < r + e.Radius * .5f;
        }

        void Shoot(float dt)
        {
            cooldown -= dt;
            Enemy target = null; float bd = Def.range;
            foreach (var e in GameManager.Enemies)
            {
                if (e == null || e.Dead || e.Riding) continue;
                float d = Vector3.Distance(e.transform.position, transform.position);
                if (d < bd) { bd = d; target = e; }
            }
            if (target == null || headPart == null) return;
            var aimAt = ModelFactory.Bounds(target.gameObject).center;
            var dir = aimAt - headPart.position;
            var want = Quaternion.LookRotation(Def.kind == StructureKind.Cannon ? new Vector3(dir.x, dir.magnitude * .6f, dir.z) : dir);
            headPart.rotation = Quaternion.RotateTowards(headPart.rotation, want, 240 * dt);
            if (cooldown > 0 || Quaternion.Angle(headPart.rotation, want) > 8) return;
            cooldown = 1 / Mathf.Max(.05f, Def.rate);
            var from = muzzle != null ? muzzle.position : headPart.position;
            Fx.Flash(from, new Color(1, .8f, .4f), 4, 3, .08f);
            if (Def.kind == StructureKind.Turret)
            {
                Fx.Tracer(from, aimAt, new Color(.85f, .9f, 1f), .08f, .12f);
                target.TakeHit(new DamageInfo { amount = Def.damage, ignoreArmor = true, point = aimAt, dir = dir.normalized, knockback = 3 }, null);
                Sfx.Play("harpoon", .6f, .1f);
            }
            else
            {
                var shell = new GameObject("Pearl Shot").AddComponent<Lob>();
                shell.Launch(from, aimAt, Def.damage, Def.splash);
                Sfx.Play("shotgun", .7f, .1f);
            }
        }

        void Look()
        {
            if (model == null) return;
            foreach (var r in model.Renderers)
            {
                if (r == null) continue;
                if (flashT > 0) { mpb.Clear(); mpb.SetColor("_BaseColor", Color.white); r.SetPropertyBlock(mpb); }
                else r.SetPropertyBlock(null);
            }
        }
    }
}

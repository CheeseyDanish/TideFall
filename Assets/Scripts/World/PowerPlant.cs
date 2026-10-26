// TideWall 3D — the Nuclear Sand Powerplant: what the team defends. Creatures that get through a breach in the
// wall (or fly over it) attack it; at 0 HP it melts down and the run is over. Steam from the cooling towers, a
// green core that glows hotter (and flickers red) as it takes damage, and an alarm when it's in danger.
using UnityEngine;

namespace TideWall
{
    public class PowerPlant : MonoBehaviour
    {
        public float Hp { get; private set; }
        public float MaxHp { get; private set; }
        public float Fraction => MaxHp > 0 ? Hp / MaxHp : 0;
        ModelInstance model;
        Light core;
        Transform steamL, steamR;
        float steamT, flashT, alarmT;
        bool warned;

        public static PowerPlant Build(Transform parent, Vector3 pos, float hp)
        {
            var mi = ModelFactory.Spawn("Powerplant", parent, pos);
            mi.root.transform.rotation = Quaternion.identity;   // the reactor's front (sign, glowing hatch) faces the sea
            var bc = mi.root.AddComponent<BoxCollider>();
            bc.center = new Vector3(0, 2.5f, 0); bc.size = new Vector3(12, 5, 8);
            var p = mi.root.AddComponent<PowerPlant>();
            p.model = mi;
            p.MaxHp = p.Hp = hp;
            foreach (var r in mi.renderers)
                foreach (var m in r.materials)
                    if (m.name.ToLowerInvariant().StartsWith("core")) Mats.SetEmission(m, new Color(.3f, 1f, .4f) * 2.5f);
            mi.sockets.TryGetValue("SteamL", out p.steamL);
            mi.sockets.TryGetValue("SteamR", out p.steamR);
            p.core = new GameObject("Core Light").AddComponent<Light>();
            p.core.transform.SetParent(mi.sockets.TryGetValue("Core", out var c) ? c : mi.root.transform, false);
            p.core.transform.localPosition = new Vector3(0, 0, 3.5f);   // in front of the dome, toward the sea
            p.core.type = LightType.Point; p.core.range = 14; p.core.shadows = LightShadows.None;
            return p;
        }

        /// <summary>The point on the plant's edge a creature at `from` should walk to.</summary>
        public Vector3 Approach(Vector3 from, float radius)
        {
            var d = from - transform.position; d.y = 0;
            if (d.sqrMagnitude < .01f) d = Vector3.forward;
            return transform.position + d.normalized * (Arena.PlantRadius + radius * .6f);
        }

        public bool InReach(Vector3 from, float range)
        {
            var d = from - transform.position; d.y = 0;
            return d.magnitude <= Arena.PlantRadius + range + .3f;
        }

        public void Damage(float amount, Vector3 where)
        {
            var gm = GameManager.I;
            if (gm.State != GameState.Playing || amount <= 0 || Net.IsClient) return;
            Hp = Mathf.Max(0, Hp - amount);
            flashT = .15f;
            Fx.Sparks(where + Vector3.up * 1.2f, new Color(.4f, 1f, .5f), 6);
            Sfx.Play("zap", .5f, .1f);
            if (!warned && Fraction < .3f)
            {
                warned = true;
                Sfx.Play("horn");
                gm.Banner("CORE CRITICAL", "The powerplant is overheating: get the creatures off it!");
            }
            if (Hp <= 0)
            {
                Fx.Explosion(transform.position + Vector3.up * 3, 9);
                Fx.Smoke(transform.position + Vector3.up * 5, 40, 4, new Color(.55f, .9f, .5f, .8f));
                gm.GameOver("MELTDOWN: the Nuclear Sand Powerplant melted down.");
            }
        }

        public void Repair(float fraction) { Hp = Mathf.Min(MaxHp, Hp + MaxHp * fraction); if (Fraction >= .3f) warned = false; }
        public void Grow(float mult) { MaxHp *= mult; Hp *= mult; }

        /// <summary>Loading a save, or a client mirroring the host.</summary>
        public void Restore(float hp, float max)
        {
            if (hp < Hp - .5f) flashT = .15f;
            MaxHp = Mathf.Max(1, max); Hp = Mathf.Clamp(hp, 0, MaxHp);
        }

        void Update()
        {
            float t = Time.time;
            float danger = 1 - Fraction;
            // Steam from the cooling towers (made locally on every machine: it's only scenery)
            steamT -= Time.deltaTime;
            if (steamT <= 0)
            {
                steamT = .35f;
                if (steamL) Fx.Steam(steamL.position, danger);
                if (steamR) Fx.Steam(steamR.position, danger);
            }
            if (flashT > 0) flashT -= Time.deltaTime;
            bool critical = Fraction < .3f;
            float pulse = .5f + .5f * Mathf.Sin(t * (critical ? 9 : 2));
            core.color = critical && pulse > .5f ? new Color(1, .35f, .25f) : new Color(.35f, 1f, .45f);
            core.intensity = (2.5f + danger * 5) * (.7f + .3f * pulse) + (flashT > 0 ? 6 : 0);
            if (critical && GameManager.I.State == GameState.Playing)
            {
                alarmT -= Time.deltaTime;
                if (alarmT <= 0) { alarmT = 2.5f; if (!Net.IsClient) Sfx.Play("deny", .6f, .5f); }
            }
        }
    }
}

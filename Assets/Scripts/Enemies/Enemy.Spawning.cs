// TideWall 3D — Enemy: creating creatures (and their online proxies) from their data.
// (One part of the Enemy class; Enemy.cs has the overview, the fields and the frame loop.)
using System.Collections.Generic;
using UnityEngine;

namespace TideWall
{
    public partial class Enemy
    {
        // ------------------------------------------------------------------ spawning
        public static Enemy Spawn(EnemyDef def, Vector3 pos, int day, int wave)
        {
            var go = new GameObject(def.displayName);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, 180, 0));   // face the shore (-Z)
            var e = go.AddComponent<Enemy>();
            e.Init(def, day, wave);
            NetId.Tag(go, NetKind.Enemy, GameManager.I.DB.enemies.IndexOf(def));
            return e;
        }

        /// <summary>Client: a mirror of the host's enemy with this network id.</summary>
        public static Enemy SpawnProxy(EnemyDef def, int id, Vector3 pos, float yaw)
        {
            var go = new GameObject(def.displayName);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            var e = go.AddComponent<Enemy>();
            e.Proxy = true;
            e.Init(def, 1, -1);
            e.netPos = pos; e.netRot = go.transform.rotation;
            NetId.Tag(go, NetKind.Enemy, GameManager.I.DB.enemies.IndexOf(def), id);
            return e;
        }

        void Init(EnemyDef def, int day, int wave)
        {
            gm = GameManager.I;
            Def = def; Wave = wave;
            MaxHp = Hp = def.hp * gm.DB.HpScale(day) * (Proxy ? 1 : gm.Cards.EnemyHp);
            damageScale = gm.DB.DamageScale(day);
            armor = def.armor;
            Model = ModelFactory.Spawn(def.model, transform, Vector3.zero, def.scale, colliders: true);
            mpb = new MaterialPropertyBlock();
            bounds = ModelFactory.Bounds(gameObject);
            Radius = Mathf.Clamp(Mathf.Max(bounds.extents.x, bounds.extents.z) * .8f, .35f, 3f);
            foreach (var ld in def.limbs)
            {
                if (!Model.Limbs.TryGetValue(ld.part, out var t)) { Debug.LogWarning($"{def.model} has no Limb_{ld.part}"); continue; }
                var limb = t.gameObject.AddComponent<Limb>();
                limb.Owner = this; limb.Def = ld; limb.Hp = MaxHp * ld.hpFraction; limb.BaseRotation = t.localRotation;
                limb.Kind = KindOf(ld.part);
                limb.Side = ld.part.EndsWith("L") ? -1 : ld.part.EndsWith("R") ? 1 : 0;
                limb.Phase = Random.value * 6.28f;
                Limbs.Add(limb);
            }
            if (def.mark)
            {
                // The Mark, painted on its back where only someone looking down (a defender on the wall) would see it
                Glyphs.Decal(transform, "mark", new Vector3(0, bounds.max.y - transform.position.y + .01f, 0), Quaternion.Euler(90, 0, 0),
                             Mathf.Clamp(Radius * 1.1f, .4f, 3f), new Color(.55f, .1f, .12f, .85f));
                // The Tidemother's flanks are scratched with tallies: she has done this before
                if (def.id == "tidemother")
                    foreach (float side in new[] { -1f, 1f })
                        for (int k = 0; k < 3; k++)
                            Glyphs.Decal(transform, "tally", new Vector3(side * 1.83f, 1.7f + (k % 2) * .35f, -.9f + k * .9f), Quaternion.Euler(0, -90 * side, (k - 1) * 8),
                                         .75f, new Color(.95f, .9f, .8f, .7f));
            }
            if (def.behaviour == EnemyBehaviour.Flyer) { flyHeight = Random.Range(4f, 6f); Wet = 0; }
            lungeCd = Random.Range(1f, 3f);
            specialT = Random.Range(3f, 6f);
            summonT = 10;
            lastPos = transform.position;
            GameManager.Enemies.Add(this);
        }

        static string KindOf(string part)
        {
            foreach (var k in new[] { "Leg", "Arm", "Wing", "Tent", "Claw", "Tail", "Head", "Eyes", "Fin", "Shell", "Lid" })
                if (part.StartsWith(k)) return k;
            return "Other";
        }
    }
}

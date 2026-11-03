// TideWall 3D — the loose half of a creature's ragdoll (DeathFlop throws the body).
using UnityEngine;

namespace TideWall
{
    /// <summary>A dead creature's limbs, loose: each swings on its joint under gravity, within limits, and slowly stops.</summary>
    public class LimbRag : MonoBehaviour
    {
        const float MaxSwing = 75, SettleSeconds = 3.5f;
        struct Limb { public Transform t; public Quaternion rest; public Vector3 tip, spin; }
        Limb[] limbs;
        Corpse corpse;
        float quietIn;

        public static void Begin(Enemy e, Vector3 push)
        {
            var r = e.gameObject.AddComponent<LimbRag>();
            r.corpse = e.GetComponent<Corpse>();
            r.quietIn = SettleSeconds;
            var list = new System.Collections.Generic.List<Limb>();
            foreach (var kv in e.Model.Limbs)
            {
                var t = kv.Value;
                if (t == null || t == e.Model.root.transform) continue;
                // Which way it points: from the joint to the middle of what's on it
                var rs = t.GetComponentsInChildren<Renderer>();
                if (rs.Length == 0) continue;
                var b = rs[0].bounds;
                foreach (var x in rs) b.Encapsulate(x.bounds);
                var tip = t.InverseTransformDirection(b.center - t.position);
                if (tip.sqrMagnitude < .0001f) tip = Vector3.down;
                list.Add(new Limb { t = t, rest = t.localRotation, tip = tip.normalized, spin = Random.onUnitSphere * (260 + push.magnitude * 25) });
            }
            r.limbs = list.ToArray();
        }

        /// <summary>It hit the ground: everything jumps.</summary>
        public static void Kick(Enemy e, float hard)
        {
            if (!e.TryGetComponent<LimbRag>(out var r)) return;
            for (int i = 0; i < r.limbs.Length; i++) r.limbs[i].spin += Random.onUnitSphere * hard * 40;
            r.quietIn = SettleSeconds;
        }

        void Update()
        {
            bool carried = corpse != null && corpse.Holder != null;
            if (carried) quietIn = 1;   // still swinging
            else if ((quietIn -= Time.deltaTime) <= 0) return;
            float dt = Mathf.Min(Time.deltaTime, .04f);
            for (int i = 0; i < limbs.Length; i++)
            {
                ref var l = ref limbs[i];
                if (l.t == null || l.t.lossyScale.sqrMagnitude < .0001f) continue;
                // Gravity turns it toward hanging straight down
                var worldTip = l.t.TransformDirection(l.tip);
                var torque = Vector3.Cross(worldTip, Vector3.down) * 900;
                var parent = l.t.parent != null ? l.t.parent : l.t;
                l.spin += parent.InverseTransformDirection(torque) * dt;
                l.spin *= Mathf.Exp(-dt * 4.5f);
                var q = Quaternion.AngleAxis(l.spin.magnitude * dt, l.spin.normalized) * l.t.localRotation;
                // A joint only goes so far; at the limit it stops dead
                float off = Quaternion.Angle(l.rest, q);
                if (off > MaxSwing) { q = Quaternion.RotateTowards(l.rest, q, MaxSwing); l.spin *= .3f; }
                l.t.localRotation = q;
            }
        }
    }
}

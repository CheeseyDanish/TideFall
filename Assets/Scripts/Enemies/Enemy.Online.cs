// TideWall 3D — Enemy: what replicates online, and how a client's proxy follows the host.
// (One part of the Enemy class; Enemy.cs has the overview, the fields and the frame loop.)
using System.Collections.Generic;
using UnityEngine;

namespace TideWall
{
    public partial class Enemy
    {
        // ------------------------------------------------------------------ online
        Vector3 netPos, netRootPos; Quaternion netRot = Quaternion.identity, netRootRot = Quaternion.identity; float netRootScale = 1;

        /// <summary>Host: everything a client needs to draw this enemy (or its corpse).</summary>
        public void WriteNet(ref NetEntityState s)
        {
            s.pos = transform.position; s.rot = transform.rotation;
            var r = Model.root.transform;
            s.rootPos = r.localPosition; s.rootRot = r.localRotation; s.rootScale = r.localScale.x / Mathf.Max(.0001f, Def.scale);
            var c = Dead ? GetComponent<Corpse>() : null;
            s.wet = c != null ? c.Wet : Wet;
            s.flags = (byte)((Dead ? 1 : 0) | (flashT > 0 ? 2 : 0) | (burnT > 0 ? 4 : 0) | (grounded ? 8 : 0) | (swingT > 0 ? 16 : 0) | (ride != null ? 32 : 0));
            uint mask = 0;
            for (int i = 0; i < Limbs.Count && i < 32; i++) if (Limbs[i] == null || Limbs[i].Severed) mask |= 1u << i;
            s.severed = mask;
            s.value = c != null ? c.Value : 0;
        }

        /// <summary>Client: follow the host's state for this enemy.</summary>
        public void ApplyNet(in NetEntityState s)
        {
            netPos = s.pos; netRot = s.rot;
            netRootPos = s.rootPos; netRootRot = s.rootRot; netRootScale = s.rootScale;
            Wet = s.wet;
            flashT = (s.flags & 2) != 0 ? .08f : flashT;
            burnT = (s.flags & 4) != 0 ? 1 : 0;
            grounded = (s.flags & 8) != 0;
            if ((s.flags & 16) != 0) swingT = .25f;
            bool surfing = (s.flags & 32) != 0;
            if (surfing != (board != null)) ShowBoard(surfing);
            for (int i = 0; i < Limbs.Count && i < 32; i++)
                if ((s.severed & (1u << i)) != 0 && Limbs[i] != null && !Limbs[i].Severed) SeverVisual(Limbs[i]);
            if ((s.flags & 1) != 0 && !Dead) DieVisual(s.value);
            if (Dead && TryGetComponent<Corpse>(out var c)) { c.Value = s.value; c.Wet = s.wet; }
        }

        void ProxyUpdate(float dt)
        {
            animT += dt;
            float k = 1 - Mathf.Exp(-dt * 12);
            transform.position = Vector3.Distance(transform.position, netPos) > 6 ? netPos : Vector3.Lerp(transform.position, netPos, k);
            transform.rotation = Quaternion.Slerp(transform.rotation, netRot, k);
            var r = Model.root.transform;
            r.localPosition = Vector3.Lerp(r.localPosition, netRootPos, k);
            r.localRotation = Quaternion.Slerp(r.localRotation, netRootRot, k);
            r.localScale = Vector3.one * Def.scale * Mathf.Lerp(r.localScale.x / Mathf.Max(.0001f, Def.scale), netRootScale, k);
            if (flashT > 0) flashT -= dt;
            if ((boundsT -= dt) <= 0) { bounds = ModelFactory.Bounds(gameObject); boundsT = .4f; }
            dripAcc += Wet * gm.DB.maxDripsPerSecond * Mathf.Clamp(Radius, .6f, 2f) * dt;
            while (dripAcc >= 1)
            {
                dripAcc -= 1;
                Fx.Drip(new Vector3(Random.Range(bounds.min.x, bounds.max.x), Mathf.Lerp(bounds.min.y, bounds.max.y, Random.Range(.15f, .7f)), Random.Range(bounds.min.z, bounds.max.z)));
            }
            AnimateLimbs(dt, Gait(dt));
            ApplyLook();
            lastPos = transform.position;
        }

        /// <summary>Client: the host severed this limb. Same physics fly-off; the gore and sound came from the host.</summary>
        void SeverVisual(Limb limb)
        {
            limb.Severed = true;
            var t = limb.transform;
            t.SetParent(null, true);
            foreach (var c in t.GetComponentsInChildren<Collider>()) c.isTrigger = false;
            var rb = t.gameObject.AddComponent<Rigidbody>();
            rb.mass = .4f;
            rb.linearVelocity = (t.position - transform.position).normalized * 4 + Vector3.up * 3.5f + Random.insideUnitSphere;
            rb.angularVelocity = Random.insideUnitSphere * 12;
            Destroy(limb);
            Destroy(t.gameObject, 8);
        }

        /// <summary>Client: the host says it died. Turn into a corpse that follows the host (it can be carried and thrown).</summary>
        void DieVisual(int value)
        {
            Dead = true;
            GameManager.Enemies.Remove(this);
            var c = gameObject.AddComponent<Corpse>();
            c.Init(Def, value, Wet);
            foreach (var r in Model.Renderers) if (r) r.SetPropertyBlock(null);
            foreach (var l in Limbs) if (l != null) Destroy(l);
            var f = gameObject.AddComponent<NetFollow>();
            f.Set(transform.position, transform.rotation);
            f.pos = netPos; f.rot = netRot;
            Model.root.transform.localPosition = netRootPos; Model.root.transform.localRotation = netRootRot;
            enabled = false;
        }

        /// <summary>Client: a corpse's pose (it moves when carried or thrown on the host).</summary>
        public void ApplyCorpsePose(in NetEntityState s)
        {
            if (TryGetComponent<NetFollow>(out var f)) f.Set(s.pos, s.rot);
            Model.root.transform.localPosition = s.rootPos; Model.root.transform.localRotation = s.rootRot;
        }

        /// <summary>A tipped-over corpse can swing below the sand: lift its model so the lowest point sits on the ground
        /// (otherwise it looks sunk and is hard to pick up).</summary>
        void LiftCorpse()
        {
            var b = ModelFactory.Bounds(Model.root);
            float ground = Arena.GroundY(transform.position.z);
            float lift = ground + .02f - b.min.y;
            if (lift > 0) Model.root.transform.position += Vector3.up * lift;
        }

        void OnDestroy() => GameManager.Enemies.Remove(this);
    }
}

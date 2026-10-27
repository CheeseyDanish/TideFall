// TideWall 3D — Player: build mode: the placement ghost, turning, placing defences.
// (One part of the Player class; Player.cs has the overview, the fields and the frame loop.)
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace TideWall
{
    public partial class Player
    {
        // ------------------------------------------------------------------ building
        public void StartBuilding(StructureDef def)
        {
            Building = def;
            BuildYaw = Mathf.Round(Yaw / 45) * 45;
            CloseMenu();
            Toast($"Building {def.displayName}: {Glyph(Btn.Interact)} place · {Glyph(Btn.Reload)} turn · {Glyph(Btn.Back)} cancel", new Color(.7f, .9f, 1));
        }

        public void StopBuilding() { Building = null; UpdateGhost(); }

        /// <summary>Where the current build would go (on the ground under your aim, within reach).</summary>
        public Vector3 BuildSpot()
        {
            var to = AimPoint - transform.position; to.y = 0;
            if (to.magnitude > 9) to = to.normalized * 9;
            if (to.magnitude < 2) to = AimDir * 2.5f;
            var p = transform.position + to;
            return new Vector3(p.x, Arena.GroundY(p.z), p.z);
        }

        void UpdateBuild(float dt)
        {
            CombatTimers(dt);
            var spot = BuildSpot();
            string why = Structure.WhyNot(Building, spot);
            BuildValid = why == null && gm.SandDollars >= Building.sandDollars && gm.Pearls >= Building.pearls;
            if (Pressed(Btn.Reload)) BuildYaw += 45;
            if (Pressed(Btn.Back) || Pressed(Btn.Build)) { StopBuilding(); return; }
            if (Pressed(Btn.Interact) || Pressed(Btn.Fire))
            {
                if (why != null) { Sfx.Play("deny"); Toast(why, new Color(1, .6f, .6f)); return; }
                if (!gm.TrySpend(Building.sandDollars, Building.pearls)) { Toast("Not enough sand dollars or pearls", new Color(1, .6f, .6f)); return; }
                Structure.Build(Building, spot, BuildYaw);
                Sfx.Play("reload");
                Fx.Debris(spot + Vector3.up * .3f, new Color(.9f, .82f, .6f), 8);
                if (gm.SandDollars < Building.sandDollars || gm.Pearls < Building.pearls) StopBuilding();   // can't afford another
            }
            UpdateGhost();
        }

        /// <summary>The see-through preview of what you're placing (only your own camera sees it).</summary>
        void UpdateGhost()
        {
            if (!HasView) return;
            if (Building == null) { if (ghost != null) { Destroy(ghost); ghost = null; ghostId = null; } return; }
            if (ghost == null || ghostId != Building.id)
            {
                if (ghost != null) Destroy(ghost);
                var mi = ModelFactory.Spawn(Building.model, null, Vector3.zero);
                ghost = mi.root; ghostId = Building.id;
                foreach (var t in ghost.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = ViewLayer + Index;
                foreach (var r in mi.Renderers) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.sharedMaterial = Mats.Transparent(new Color(.4f, 1f, .5f, .45f)); }
            }
            var spot = BuildSpot();
            bool ok = Kind == PlayerKind.LocalProxy ? Structure.WhyNot(Building, spot) == null && gm.SandDollars >= Building.sandDollars && gm.Pearls >= Building.pearls : BuildValid;
            ghost.transform.SetPositionAndRotation(spot, Quaternion.Euler(0, BuildYaw, 0));
            var col = ok ? new Color(.4f, 1f, .5f, .45f) : new Color(1f, .35f, .3f, .45f);
            foreach (var r in ghost.GetComponentsInChildren<Renderer>()) r.sharedMaterial = Mats.Transparent(col);
        }

        void UpdateNameTag()
        {
            if (nameTag == null) return;
            nameTag.text = string.IsNullOrEmpty(Nick) ? Name : $"{Name} · {Nick}";
            var cam = gm.Players.FirstViewCamera(this);
            nameTag.gameObject.SetActive(cam != null && gm.Players.Count > 1);
        }

        public float HurtFlash => hurtFlash;

        public void Shake(float amount)
        {
            if (Cam != null) Cam.AddShake(amount);
            else if (Kind == PlayerKind.Remote && Net.IsHost) Net.Host.Shake(this, amount);
        }

        public void Toast(string msg, Color? color = null)
        {
            if (Hud != null) Hud.Toast(msg, color ?? Color.white);
            else if (Kind == PlayerKind.Remote && Net.IsHost) Net.Host.Toast(this, msg, color ?? Color.white);
        }
    }
}

// TideWall 3D — Player: gun attachments and ammo bought in the shop. Each gun has two slots: one attachment (a sight,
// barrel, magazine or grip part) and one ammo modifier (incendiary, shock…). Their numbers are folded into EffGun by
// RebuildWeapons, and the attachment's little model is fitted to the gun in your hands (and the first-person view model).
// (One part of the Player class; Player.cs has the overview, the fields and the frame loop.)
using System.Collections.Generic;
using UnityEngine;

namespace TideWall
{
    public partial class Player
    {
        public IEnumerable<AttachmentDef> AttachmentsOn(string gunId)
        {
            if (!Attachments.TryGetValue(gunId, out var ids)) yield break;
            foreach (var id in ids) if (db.Attachment(id) is { } a) yield return a;
        }

        /// <summary>The attachment in this slot of this gun, or null.</summary>
        public AttachmentDef AttachmentIn(string gunId, AttachmentSlot slot)
        {
            foreach (var a in AttachmentsOn(gunId)) if (a.slot == slot) return a;
            return null;
        }

        public bool HasAttachment(string gunId, string attachmentId) => Attachments.TryGetValue(gunId, out var ids) && ids.Contains(attachmentId);

        /// <summary>Fit an attachment to a gun, replacing whatever was in its slot.</summary>
        void Fit(string gunId, AttachmentDef a)
        {
            if (!Attachments.TryGetValue(gunId, out var ids)) Attachments[gunId] = ids = new List<string>();
            ids.RemoveAll(id => db.Attachment(id) is { } old && old.slot == a.slot);
            ids.Add(a.id);
        }

        /// <summary>Shop: fit this to the gun in your hands (paid for by the caller).</summary>
        public void FitAttachment(AttachmentDef a)
        {
            if (Gun == null || a == null) return;
            Fit(Gun.id, a);
            int mag = MagSize;
            RebuildWeapons();
            if (Ammo > MagSize || MagSize > mag) ammo[Gun.id] = MagSize;   // a new drum comes loaded
            if (gunModel != null) { DressGun(gunModel, Gun.id); SetLayer(gunModel.root.transform, BodyLayer + Index); }
            if (Cam != null) Cam.View.Redress();
            Sfx.Play("reload");
        }

        /// <summary>Online client: a proxy's attachments, as the host's snapshot lists them (for the models).</summary>
        void ApplyNetAttachments(string gunId, string list)
        {
            var now = Attachments.TryGetValue(gunId, out var ids) ? string.Join(",", ids) : "";
            if (now == list) return;
            Attachments[gunId] = new List<string>(list.Length > 0 ? list.Split(',') : new string[0]);
            if (gunModel != null && Gun != null && Gun.id == gunId) { DressGun(gunModel, gunId); SetLayer(gunModel.root.transform, BodyLayer + Index); }
            if (Cam != null) Cam.View.Redress();
        }

        public string AttachmentList(string gunId) => Attachments.TryGetValue(gunId, out var ids) ? string.Join(",", ids) : "";

        /// <summary>Put the attachment's model on a gun model by where it mounts: sight on top, barrel piece at the
        /// muzzle, magazine under the middle, grip or stock at the back (ammo shows no part). Placed from the gun's muzzle socket (guns point along +Z).</summary>
        public static void DressGun(Model gun, IEnumerable<AttachmentDef> fitted)
        {
            var old = gun.root.transform.Find("Attachments");
            if (old != null) Object.Destroy(old.gameObject);
            var holder = new GameObject("Attachments").transform;
            holder.SetParent(gun.root.transform, false);
            var muzzle = gun.Sockets.TryGetValue("Muzzle", out var mz) ? gun.root.transform.InverseTransformPoint(mz.position) : new Vector3(0, .05f, .4f);
            float len = Mathf.Max(.25f, muzzle.z);
            foreach (var a in fitted)
            {
                if (string.IsNullOrEmpty(a.model) || a.mount == AttachmentMount.None) continue;
                var at = a.mount switch
                {
                    AttachmentMount.Sight => new Vector3(0, muzzle.y + .07f, len * .35f),
                    AttachmentMount.Barrel => muzzle,
                    AttachmentMount.Magazine => new Vector3(0, muzzle.y - .1f, len * .3f),
                    _ => new Vector3(0, muzzle.y - .02f, -.12f),
                };
                var part = ModelFactory.Spawn(a.model, holder, at);
                if (part == null) continue;
                foreach (var c in part.root.GetComponentsInChildren<Collider>()) Object.Destroy(c);
                if (a.mount == AttachmentMount.Barrel) part.root.transform.localPosition = muzzle;
            }
        }

        void DressGun(Model gun, string gunId) => DressGun(gun, new List<AttachmentDef>(AttachmentsOn(gunId)));
    }
}

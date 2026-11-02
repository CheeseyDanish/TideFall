// TideWall 3D — Player: gun attachments bought in the shop. Each gun has five slots: scope, barrel, laser sight, stock
// and ammo. A part you've bought is yours for the run and fits any gun, free, from the Inventory. Their numbers are folded
// into EffGun by RebuildWeapons, and each part's little model is fitted to the gun in your hands (and the view model).
// Also the laser's beam and aiming down the sights (the fitted scope sets how far it zooms).
// (One part of the Player class; Player.cs has the overview, the fields and the frame loop.)
using System.Collections.Generic;
using UnityEngine;

namespace TideWall
{
    public partial class Player
    {
        /// <summary>Parts this sailor has bought: they fit any gun, from the Inventory.</summary>
        public readonly HashSet<string> OwnedParts = new();
        LineRenderer laser;
        Transform laserDot;
        MaterialPropertyBlock laserBlock;

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
        public bool OwnsPart(string attachmentId) => OwnedParts.Contains(attachmentId);

        /// <summary>Fit an attachment to a gun, replacing whatever was in its slot.</summary>
        void Fit(string gunId, AttachmentDef a)
        {
            if (!Attachments.TryGetValue(gunId, out var ids)) Attachments[gunId] = ids = new List<string>();
            ids.RemoveAll(id => db.Attachment(id) is { } old && old.slot == a.slot);
            ids.Add(a.id);
            OwnedParts.Add(a.id);
        }

        /// <summary>Shop: fit this to the gun in your hands (paid for by the caller).</summary>
        public void FitAttachment(AttachmentDef a) { if (Gun != null) FitTo(Gun.id, a); }

        /// <summary>Inventory: fit a part to any gun you own.</summary>
        public void FitTo(string gunId, AttachmentDef a)
        {
            if (a == null) return;
            int mag = MagSize;
            Fit(gunId, a);
            Refit(gunId, mag);
        }

        /// <summary>Inventory: take the part out of this slot (you keep it).</summary>
        public void Unfit(string gunId, AttachmentSlot slot)
        {
            if (!Attachments.TryGetValue(gunId, out var ids)) return;
            int mag = MagSize;
            ids.RemoveAll(id => db.Attachment(id) is { } old && old.slot == slot);
            Refit(gunId, mag);
        }

        void Refit(string gunId, int magBefore)
        {
            Sfx.Play("reload");
            if (Gun == null || Gun.id != gunId) return;   // that gun's numbers and models are worked out when it's next in your hands
            RebuildWeapons();
            if (Ammo > MagSize || MagSize > magBefore) ammo[Gun.id] = MagSize;   // a new drum comes loaded
            if (gunModel != null) { DressGun(gunModel, Gun.id); SetLayer(gunModel.root.transform, BodyLayer + Index); }
            if (Cam != null) Cam.View.Redress();
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

        /// <summary>Put the attachment's model on a gun model by where it mounts: sight on top, barrel piece at the muzzle,
        /// laser under the barrel, magazine under the middle, stock at the back (ammo with no mount shows no part).
        /// Placed from the gun's muzzle socket (guns point along +Z).</summary>
        public static void DressGun(Model gun, IEnumerable<AttachmentDef> fitted)
        {
            var old = gun.root.transform.Find("Attachments");
            if (old != null) { old.name = "Old"; old.gameObject.SetActive(false); Object.Destroy(old.gameObject); }
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
                    AttachmentMount.Laser => new Vector3(.055f, muzzle.y - .05f, len * .62f),
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

        // ------------------------------------------------------------------ aiming down the sights, and the laser
        /// <summary>Holding the sights up (never with a menu open, arms full, or mid-reload).</summary>
        public bool Aiming => Fish.stage == FishStage.None && !IsDowned && !IsOut && ShownMenu == null && Building == null && !GunsLocked && CarryCount == 0 && ReloadProgress < 0 && Held(Btn.Sight);
        /// <summary>0 → 1 as the sights come up.</summary>
        public float AimAmount { get; private set; }
        /// <summary>The view's FOV multiplier right now (a scope zooms further than iron sights). Also slows the look.</summary>
        public float AimZoom
        {
            get
            {
                var scope = Gun != null ? AttachmentIn(Gun.id, AttachmentSlot.Scope) : null;
                return Mathf.Lerp(1, scope != null ? scope.zoom : db.aimZoom, AimAmount);
            }
        }

        /// <summary>Looking down a long scope (first person only): the view model gives way to the scope's ring.</summary>
        public bool ScopedIn
        {
            get
            {
                if (!FirstPerson || AimAmount < .85f || Gun == null) return false;
                var scope = AttachmentIn(Gun.id, AttachmentSlot.Scope);
                return scope != null && scope.zoom <= .5f;
            }
        }

        void LateUpdate()
        {
            AimAmount = Mathf.MoveTowards(AimAmount, Aiming ? 1 : 0, Time.unscaledDeltaTime * 7);
            UpdateLaser();
            UpdateFishLine();
        }

        /// <summary>A fitted laser draws a thin beam from the gun to the point under the crosshair, and a dot where it lands.
        /// Everyone can see it, so teammates can tell what you're aiming at.</summary>
        void UpdateLaser()
        {
            var a = Gun != null ? AttachmentIn(Gun.id, AttachmentSlot.Laser) : null;
            bool on = a != null && !GunsLocked && !IsDowned && !IsOut && CarryCount == 0 && gunModel != null;
            if (!on) { if (laser != null) { laser.enabled = false; laserDot.gameObject.SetActive(false); } return; }
            if (laser == null)
            {
                laser = new GameObject("Laser").AddComponent<LineRenderer>();
                laser.material = Mats.ParticleAdditive; laser.positionCount = 2; laser.useWorldSpace = true;
                laser.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(dot.GetComponent<Collider>());
                dot.name = "LaserDot";
                var r = dot.GetComponent<MeshRenderer>();
                r.sharedMaterial = Mats.ParticleAdditive; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                laserDot = dot.transform;
                laserBlock = new MaterialPropertyBlock();
            }
            var from = MuzzlePosition();
            laser.enabled = true;
            laser.SetPosition(0, from); laser.SetPosition(1, AimPoint);
            laser.startWidth = .004f; laser.endWidth = .018f;
            laser.startColor = a.laserColor; laser.endColor = new Color(a.laserColor.r, a.laserColor.g, a.laserColor.b, .25f);
            laserDot.gameObject.SetActive(true);
            laserDot.position = AimPoint;
            laserDot.localScale = Vector3.one * Mathf.Clamp(Vector3.Distance(from, AimPoint) * .006f, .05f, .22f);
            laserBlock.SetColor("_BaseColor", a.laserColor);
            laserDot.GetComponent<MeshRenderer>().SetPropertyBlock(laserBlock);
        }
    }
}

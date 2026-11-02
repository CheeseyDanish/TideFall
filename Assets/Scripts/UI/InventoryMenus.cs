// TideWall 3D — the Inventory (its own button, or the pause menu): the guns you carry and the ones in your stash, each
// gun's five attachment slots, and every weapon's skin. Runs on the host for online players, like the other menus.
using System.Collections.Generic;
using UnityEngine;

namespace TideWall
{
    public static class InventoryMenus
    {
        public const string Title = "INVENTORY";

        static string SlotName(AttachmentSlot s) => s == AttachmentSlot.Laser ? "Laser sight" : s.ToString();

        public static Menu Main(Player p)
        {
            var gm = GameManager.I;
            var m = new Menu { Title = Title, Rebuild = () => Main(p) };
            m.Header($"CARRIED  {p.Loadout.Count}/{gm.DB.gunSlots}");
            for (int i = 0; i < p.Loadout.Count; i++)
            {
                var g = gm.DB.Gun(p.Loadout[i]);
                if (g == null) continue;
                m.Add($"{i + 1}  {g.displayName}", g == p.Gun ? "in hands" : ">", GunSummary(p, g), open: () => GunPage(p, g));
            }
            var stash = new List<GunDef>();
            foreach (var g in p.OwnedGuns()) if (!p.Carries(g.id)) stash.Add(g);
            if (stash.Count > 0)
            {
                m.Header("STASH");
                foreach (var g in stash) m.Add(g.displayName, ">", GunSummary(p, g), open: () => GunPage(p, g));
            }
            m.Header("MELEE");
            foreach (var w in gm.DB.melee)
            {
                if (!p.OwnsMelee(w.id)) continue;
                var weapon = w;
                m.Add(weapon.displayName, p.SkinOn(weapon.id)?.displayName ?? "Factory", "Pick a skin.", open: () => SkinPage(p, weapon.id, weapon.model, weapon.displayName, () => Main(p)));
            }
            m.FirstSelectable();
            return m;
        }

        static string GunSummary(Player p, GunDef g)
        {
            var parts = new List<string>();
            foreach (var a in p.AttachmentsOn(g.id)) parts.Add(a.displayName);
            return parts.Count == 0 ? $"{g.description} No attachments." : string.Join(" · ", parts);
        }

        public static Menu GunPage(Player p, GunDef g)
        {
            var gm = GameManager.I;
            var m = new Menu { Title = g.displayName.ToUpperInvariant(), Rebuild = () => GunPage(p, g), Parent = () => Main(p) };
            if (p.Gun != g)
                m.Add(p.Carries(g.id) ? "Take in hand" : "Carry this", null, p.Carries(g.id) || p.Loadout.Count < gm.DB.gunSlots ? "Into your hands." : $"Swaps with your {p.Gun.displayName}, which goes to the stash.",
                      pick: () => { p.GiveGun(g.id); Sfx.Play("swap"); });
            m.Header("ATTACHMENTS");
            foreach (AttachmentSlot s in System.Enum.GetValues(typeof(AttachmentSlot)))
            {
                var slot = s;
                var now = p.AttachmentIn(g.id, slot);
                m.Add(SlotName(slot), now != null ? now.displayName : "empty", now != null ? Menus.AttachmentEffects(now) : "Nothing fitted.", open: () => PartPage(p, g, slot));
            }
            m.Header("LOOK");
            m.Add("Skin", p.SkinOn(g.id)?.displayName ?? "Factory", "How this gun looks.", open: () => SkinPage(p, g.id, g.model, g.displayName, () => GunPage(p, g)));
            if (p.Carries(g.id) && p.Loadout.Count > 1)
                m.Add("Put in stash", null, "Frees up a slot. You keep the gun and its parts.", pick: () => p.Stash(g.id));
            m.FirstSelectable();
            return m;
        }

        public static Menu PartPage(Player p, GunDef g, AttachmentSlot slot)
        {
            var gm = GameManager.I;
            var m = new Menu { Title = SlotName(slot).ToUpperInvariant(), Layout = MenuLayout.Grid, Rebuild = () => PartPage(p, g, slot), Parent = () => GunPage(p, g) };
            var now = p.AttachmentIn(g.id, slot);
            m.Items.Add(new MenuItem
            {
                Label = "Nothing", Detail = "Leave this slot empty.", Equipped = now == null, Right = now == null ? "Fitted" : "Remove",
                Blocked = () => now == null ? "This slot's already empty" : null, OnPick = () => p.Unfit(g.id, slot),
            });
            foreach (var at in gm.DB.attachments)
            {
                if (at.slot != slot) continue;
                var a = at;
                bool fitted = now == a, owned = p.OwnsPart(a.id);
                m.Items.Add(new MenuItem
                {
                    Label = a.displayName, Icon = a.model, Detail = a.description, Info = Menus.AttachmentEffects(a), Owned = owned, Equipped = fitted,
                    Right = fitted ? "Fitted" : owned ? "Fit" : $"{a.price} SD at the shop",
                    Blocked = () => fitted ? "Already fitted" : !owned ? "Buy it at the shop first" : null,
                    OnPick = () => p.FitTo(g.id, a),
                });
            }
            m.Selected = 0;
            return m;
        }

        public static Menu SkinPage(Player p, string weaponId, string model, string weaponName, System.Func<Menu> parent)
        {
            var gm = GameManager.I;
            var m = new Menu { Title = $"{weaponName.ToUpperInvariant()} · SKINS", Layout = MenuLayout.Grid, Rebuild = () => SkinPage(p, weaponId, model, weaponName, parent), Parent = parent };
            var legacy = Legacy.Load();
            var now = p.SkinOn(weaponId);
            foreach (var sk in gm.DB.skins)
            {
                var s = sk;
                bool factory = s.id == SkinDef.Factory, worn = factory ? now == null : now == s, open = factory || Skins.Unlocked(s, legacy);
                string left = s.kills > 0 ? $"{Mathf.Max(0, s.kills - legacy.kills)} more kills, or " : "";
                m.Items.Add(new MenuItem
                {
                    Label = s.displayName, Icon = factory ? model : $"{model}#{s.id}", Detail = s.description, Owned = open, Equipped = worn,
                    Info = open ? "Unlocked" : $"{left}{s.pearls} golden pearl{(s.pearls == 1 ? "" : "s")}",
                    Right = worn ? "Equipped" : open ? "Equip" : $"{s.pearls} pearl{(s.pearls == 1 ? "" : "s")}",
                    Blocked = () => worn ? "Already on" : !open && gm.Pearls < s.pearls ? "Not enough golden pearls (giant clams drop them)" : null,
                    OnPick = () =>
                    {
                        if (!open) { if (!gm.TrySpend(0, s.pearls)) return; Legacy.UnlockSkin(s.id); Sfx.Play("bell", .5f, .2f); }
                        p.SetSkin(weaponId, s);
                    },
                });
            }
            m.Selected = 0;
            return m;
        }
    }
}

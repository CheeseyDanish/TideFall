// TideWall 3D — Player: the inventory. You own any number of guns, but carry at most GameDatabase.gunSlots (three):
// Swap cycles them, the slot buttons pick one, and the rest wait in your stash (Inventory button). Skins are per weapon.
// (One part of the Player class; Player.cs has the overview, the fields and the frame loop.)
using System.Collections.Generic;
using UnityEngine;

namespace TideWall
{
    public partial class Player
    {
        /// <summary>The guns you carry, in slot order.</summary>
        public readonly List<string> Loadout = new();
        /// <summary>Skin on each weapon (weapon id → skin id; none = the factory finish).</summary>
        public readonly Dictionary<string, string> WeaponSkins = new();
        /// <summary>Goes up whenever the loadout changes (so the HUD only rebuilds its line then).</summary>
        public int LoadoutVersion { get; private set; }

        public void OwnGun(string id) { if (db.Gun(id) != null) ownedGuns.Add(id); }
        public IEnumerable<GunDef> OwnedGuns() { foreach (var g in db.guns) if (ownedGuns.Contains(g.id)) yield return g; }
        public bool Carries(string id) => Loadout.Contains(id);

        /// <summary>Into a free slot; with none free, it takes the place of the gun in your hands (which goes to the stash).</summary>
        void Carry(string id)
        {
            if (Loadout.Contains(id)) return;
            if (Loadout.Count < db.gunSlots) Loadout.Add(id);
            else
            {
                int i = Gun != null ? Loadout.IndexOf(Gun.id) : -1;
                Loadout[i < 0 ? 0 : i] = id;
            }
            LoadoutVersion++;
        }

        /// <summary>Inventory: back to the stash (you always keep one gun in your hands).</summary>
        public void Stash(string id)
        {
            if (Loadout.Count <= 1 || !Loadout.Remove(id)) return;
            LoadoutVersion++;
            if (Gun != null && Gun.id == id) GiveGun(Loadout[0]);
            Sfx.Play("swap");
        }

        /// <summary>Slot buttons: the gun in this slot.</summary>
        public void SelectSlot(int slot)
        {
            if (slot < 0 || slot >= Loadout.Count || (Gun != null && Gun.id == Loadout[slot])) return;
            GiveGun(Loadout[slot]);
            fireCd = Mathf.Max(fireCd, db.swapSeconds);
            Sfx.Play("swap");
        }

        void CycleGun(int step)
        {
            if (Loadout.Count < 2) return;
            SelectSlot((Loadout.IndexOf(Gun.id) + step + Loadout.Count) % Loadout.Count);
        }

        public SkinDef SkinOn(string weaponId) => WeaponSkins.TryGetValue(weaponId, out var id) ? db.Skin(id) : null;

        /// <summary>Inventory: put this skin on a weapon (null = the factory finish). Its models are redrawn.</summary>
        public void SetSkin(string weaponId, SkinDef skin)
        {
            if (skin == null || skin.id == SkinDef.Factory) WeaponSkins.Remove(weaponId); else WeaponSkins[weaponId] = skin.id;
            if (Gun != null && Gun.id == weaponId) SpawnGunModel();
            if (Melee != null && Melee.id == weaponId) SpawnMeleeModel();
            Sfx.Play("swap");
        }

        void SpawnGunModel()
        {
            if (gunModel != null) { gunModel.root.SetActive(false); Destroy(gunModel.root); }
            gunModel = ModelFactory.Spawn(Gun.model, Socket("Gun"), Vector3.zero);
            Skins.Apply(gunModel, SkinOn(Gun.id));
            DressGun(gunModel, Gun.id);
            SetLayer(gunModel.root.transform, BodyLayer + Index);
            if (Cam != null) Cam.View.Rebuild(true);
        }

        void SpawnMeleeModel()
        {
            if (meleeModel != null) { meleeModel.root.SetActive(false); Destroy(meleeModel.root); }
            meleeModel = ModelFactory.Spawn(Melee.model, Socket("Melee"), Vector3.zero);
            Skins.Apply(meleeModel, SkinOn(Melee.id));
            meleeModel.root.transform.localRotation = Quaternion.Euler(-60, 0, 0);   // held up, ready to swing
            SetLayer(meleeModel.root.transform, BodyLayer + Index);
            if (Cam != null) Cam.View.Rebuild(true);
        }

        /// <summary>Online client: the loadout and the skins on the weapons in hand, as the host's snapshot lists them.</summary>
        void ApplyNetInventory(string loadout, string gunSkin, string meleeSkin)
        {
            if (string.Join(",", Loadout) != loadout)
            {
                Loadout.Clear();
                if (loadout.Length > 0) Loadout.AddRange(loadout.Split(','));
                LoadoutVersion++;
            }
            if (Gun != null && NetSkinChanged(Gun.id, gunSkin)) SpawnGunModel();
            if (Melee != null && NetSkinChanged(Melee.id, meleeSkin)) SpawnMeleeModel();
        }

        bool NetSkinChanged(string weapon, string skin)
        {
            WeaponSkins.TryGetValue(weapon, out var now);
            if ((now ?? "") == skin) return false;
            if (skin.Length == 0) WeaponSkins.Remove(weapon); else WeaponSkins[weapon] = skin;
            return true;
        }

        void ToggleInventory()
        {
            if (OpenedMenu != null && OpenedMenu.Title == InventoryMenus.Title) { CloseMenu(); return; }
            OpenMenu(InventoryMenus.Main(this));
        }
    }
}

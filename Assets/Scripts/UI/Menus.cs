// TideWall 3D — the per-player menus (shop and hotdog stand). Each player navigates their own copy in their
// own viewport; purchases come out of the shared team purse.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace TideWall
{
    public class MenuItem
    {
        public string Label, Detail, Right;
        public bool Header;
        /// <summary>Returns why this can't be picked right now, or null.</summary>
        public Func<string> Blocked;
        public Action OnPick;
    }

    public class Menu
    {
        public string Title;
        public readonly List<MenuItem> Items = new();
        public int Selected;
        /// <summary>Rebuilds the menu after a purchase (prices and "owned" labels change).</summary>
        public Func<Menu> Rebuild;

        public void Move(int dir)
        {
            if (Items.Count == 0) return;
            for (int i = 0; i < Items.Count; i++)
            {
                Selected = (Selected + dir + Items.Count) % Items.Count;
                if (!Items[Selected].Header) return;
            }
        }

        public void FirstSelectable() { Selected = -1; Move(1); }
    }

    public static class Menus
    {
        static MenuItem Header(string text) => new() { Label = text, Header = true };

        public static Menu Shop(Player p)
        {
            var gm = GameManager.I;
            var m = new Menu { Title = "SHOP", Rebuild = () => Shop(p) };
            m.Items.Add(Header("GUNS (yours)"));
            foreach (var g in gm.DB.guns)
            {
                var gun = g;
                bool owned = p.OwnsGun(gun.id), equipped = p.Gun == gun;
                m.Items.Add(new MenuItem
                {
                    Label = gun.displayName, Detail = $"{gun.description}  ({gun.damage}{(gun.pellets > 1 ? "×" + gun.pellets : "")} dmg, {gun.magazine} rounds)",
                    Right = equipped ? "Equipped" : owned ? "Equip" : $"{gun.price}c",
                    Blocked = () => equipped ? "Already equipped" : null,
                    OnPick = () => { if (owned || gm.TrySpend(gun.price)) { p.GiveGun(gun.id); Sfx.Play("reload"); } },
                });
            }
            m.Items.Add(Header("MELEE (yours)"));
            foreach (var w in gm.DB.melee)
            {
                var mel = w;
                bool equipped = p.Melee == mel;
                m.Items.Add(new MenuItem
                {
                    Label = mel.displayName, Detail = mel.description, Right = equipped ? "Equipped" : p.OwnsMelee(mel.id) ? "Equip" : $"{mel.price}c",
                    Blocked = () => equipped ? "Already equipped" : null,
                    OnPick = () => { if (p.OwnsMelee(mel.id) || gm.TrySpend(mel.price)) { p.GiveMelee(mel.id); Sfx.Play("swing"); } },
                });
            }
            m.Items.Add(Header("UPGRADES (whole team)"));
            foreach (var u in gm.DB.upgrades)
            {
                var up = u;
                int lvl = gm.Team.Level(up.id), price = gm.Team.Price(up);
                m.Items.Add(new MenuItem
                {
                    Label = up.maxLevel > 0 ? $"{up.displayName}  {lvl}/{up.maxLevel}" : up.displayName, Detail = up.description,
                    Right = gm.Team.Maxed(up) ? "Max" : $"{price}c",
                    Blocked = () => gm.Team.Blocked(up),
                    OnPick = () => { if (gm.TrySpend(price)) { gm.Team.Apply(up); Sfx.Play("bell", .5f, .2f); } },
                });
            }
            m.FirstSelectable();
            return m;
        }

        public static Menu Hotdogs(Player p)
        {
            var gm = GameManager.I;
            var m = new Menu { Title = "HOTDOG STAND", Rebuild = () => Hotdogs(p) };
            var hotdog = gm.DB.Food("hotdog");
            if (hotdog != null)
            {
                m.Items.Add(Header("HOTDOGS (heal " + hotdog.heal + ")"));
                m.Items.Add(Food(p, hotdog, null));
                foreach (var c in gm.DB.condiments) m.Items.Add(Food(p, hotdog, c));
            }
            m.Items.Add(Header("SIDES"));
            foreach (var f in gm.DB.foods) if (f.soldAtStand && f != hotdog) m.Items.Add(Food(p, f, null));
            m.FirstSelectable();
            return m;
        }

        static MenuItem Food(Player p, FoodDef food, CondimentDef sauce)
        {
            var gm = GameManager.I;
            int price = food.price + (sauce?.price ?? 0);
            return new MenuItem
            {
                Label = sauce == null ? food.displayName : $"{food.displayName} + {sauce.displayName}",
                Detail = sauce == null ? food.description : sauce.description,
                Right = $"{price}c",
                Blocked = () => p.IsDowned ? "You're down" : null,
                OnPick = () => { if (gm.TrySpend(price)) p.Eat(food, sauce); },
            };
        }
    }
}

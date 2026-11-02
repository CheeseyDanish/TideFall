// TideWall 3D — the Keeper's Log: The Last Light's permanent upgrades, bought with lamp oil brought home from past runs.
using UnityEngine;

namespace TideWall
{
    public static class LastLightMenus
    {
        /// <summary>The upgrades. Parent: where Back goes (the main menu's page).</summary>
        public static Menu KeepersLog(System.Func<Menu> parent)
        {
            var db = GameDatabase.Load();
            var log = Legacy.Load();
            var m = new Menu { Title = "KEEPER'S LOG", Layout = MenuLayout.Grid, Parent = parent, Rebuild = () => KeepersLog(parent) };
            foreach (var u in db.lastLight.upgrades)
            {
                var up = u;
                int lvl = log.KeeperLevel(up.id), price = up.PriceAt(lvl);
                bool maxed = lvl >= up.maxLevel;
                m.Items.Add(new MenuItem
                {
                    Label = up.displayName, Detail = $"{up.description} You have {log.lampOil} lamp oil.", Owned = maxed,
                    Info = $"Level {lvl}/{up.maxLevel}", Right = maxed ? "Max" : $"{price} oil",
                    Blocked = () => maxed ? "That's as far as it goes" : Legacy.Load().lampOil < price ? "Not enough lamp oil" : null,
                    OnPick = () => { if (Legacy.BuyKeeper(up.id, price)) Sfx.Play("bell", .5f, .2f); },
                });
            }
            if (m.Items.Count > 0) m.Selected = 0;
            return m;
        }
    }
}

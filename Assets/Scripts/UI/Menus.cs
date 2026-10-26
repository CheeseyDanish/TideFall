// TideWall 3D — menus as data: rows with labels, values and actions. Players navigate the in-game ones (shop,
// hotdog stand, pause, settings) in their own viewport; the main menu draws the same Menu objects full screen.
// Purchases come out of the shared team purse.
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
        /// <summary>Picking opens another menu (submenus).</summary>
        public Func<Menu> Open;
        /// <summary>Left/right on this row (sliders and toggles). Argument: -1 or +1.</summary>
        public Action<int> OnAdjust;
    }

    public class Menu
    {
        public string Title;
        public readonly List<MenuItem> Items = new();
        public int Selected;
        /// <summary>Rebuilds the menu after a purchase or a change (prices, labels and values change).</summary>
        public Func<Menu> Rebuild;
        /// <summary>Back goes here instead of closing (submenus).</summary>
        public Func<Menu> Parent;
        /// <summary>Pause/settings menus: they pause an offline game, and Melee doesn't close them.</summary>
        public bool System;

        public MenuItem Current => Selected >= 0 && Selected < Items.Count && !Items[Selected].Header ? Items[Selected] : null;

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

        public MenuItem Header(string text) { var it = new MenuItem { Label = text, Header = true }; Items.Add(it); return it; }

        public MenuItem Add(string label, string right = null, string detail = null, Action pick = null, Func<Menu> open = null,
                            Action<int> adjust = null, Func<string> blocked = null)
        {
            var it = new MenuItem { Label = label, Right = right, Detail = detail, OnPick = pick, Open = open, OnAdjust = adjust, Blocked = blocked };
            Items.Add(it);
            return it;
        }
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

        // ------------------------------------------------------------------ pause
        public static Menu Pause(Player p)
        {
            var gm = GameManager.I;
            var m = new Menu { Title = "PAUSED", System = true, Rebuild = () => Pause(p) };
            m.Add("Resume", pick: p.CloseMenu);
            m.Add(p.PrefersFirstPerson ? "Switch to top-down view" : "Switch to first person", Glyph(p, Btn.View), "You can also switch any time with the View button.",
                  pick: () => { p.CloseMenu(); p.SwitchView(); });
            m.Add("Settings", ">", "Audio, game and controls.", open: () => Settings(() => Pause(p), p));
            if (!Net.IsClient && gm.SaveSlot > 0)
                m.Add("Save game", $"slot {gm.SaveSlot}", "Saves the team's coins, upgrades, wall and weapons.",
                      pick: () => { if (gm.SaveNow()) { p.Toast("Game saved", new Color(.7f, 1, .7f)); Sfx.Play("bell", .4f, .2f); } },
                      blocked: () => gm.CanSaveNow ? null : $"You can save during {SavePhaseName()} (it also autosaves then)");
            if (Net.IsHost) m.Add("Hosting", $"{Net.Host.ClientCount} online", Net.Host.Addresses);
            m.Add(Net.IsClient ? "Leave game" : "Quit to main menu", null,
                  Net.IsClient ? "Disconnect from the host." : gm.CanSaveNow ? "Saves first." :
                  gm.SaveSlot > 0 ? $"Progress since the start of {SavePhaseName()} is lost." : "This run isn't being saved.",
                  pick: gm.QuitToMenu);
            m.Add("Quit to desktop", null, "Close TideWall.", pick: Application.Quit);
            m.FirstSelectable();
            return m;
        }

        public static string SavePhaseName()
        {
            var ph = GameManager.I.Cycle.phases.Find(x => x.saveOnStart);
            return ph != null ? ph.displayName : "a safe phase";
        }

        static string Glyph(Player p, Btn b) => p != null && p.Controls != null ? p.Controls.Glyph(b) : "";

        // ------------------------------------------------------------------ settings
        static string Pct(float v) => $"< {Mathf.RoundToInt(v * 100)}% >";
        static string OnOff(bool v) => v ? "< On >" : "< Off >";
        static void Save() => TideWall.Settings.Changed();

        /// <summary>The settings menu. `back` builds the menu to return to; p is the player using it (null in the main menu).</summary>
        public static Menu Settings(Func<Menu> back, Player p)
        {
            var s = TideWall.Settings.Current;
            var m = new Menu { Title = "SETTINGS", System = true, Parent = back };
            m.Rebuild = () => Settings(back, p);

            m.Header("AUDIO");
            Slider(m, "Master volume", () => s.master, v => s.master = v, "Everything.");
            Slider(m, "Effects", () => s.effects, v => s.effects = v, "Guns, hits, creatures, explosions.");
            Slider(m, "Interface", () => s.interfaceVolume, v => s.interfaceVolume = v, "Menu clicks, bells and coins.");
            Slider(m, "Ambience", () => s.ambience, v => s.ambience = v, "Surf and wind.");

            m.Header("GAME");
            Toggle(m, "Start in first person", () => s.startFirstPerson, v => s.startFirstPerson = v, "The view new players start with. Switch any time with the View button.");
            Step(m, "Field of view", () => $"< {s.fov:0} >", d => s.fov += d * 5, "First-person field of view, in degrees.");
            Step(m, "Mouse sensitivity", () => $"< {s.mouseSensitivity:0.0} >", d => s.mouseSensitivity = Mathf.Round((s.mouseSensitivity + d * .1f) * 10) / 10, "First-person mouse look.");
            Step(m, "Stick sensitivity", () => $"< {s.stickSensitivity:0.0} >", d => s.stickSensitivity = Mathf.Round((s.stickSensitivity + d * .1f) * 10) / 10, "First-person right-stick look.");
            Toggle(m, "Invert look up/down", () => s.invertY, v => s.invertY = v, "Mouse and stick.");
            Toggle(m, "Aim assist (controllers)", () => s.aimAssist, v => s.aimAssist = v, "Slows the stick over enemies and nudges shots onto bodies.");
            Step(m, "Screen shake", () => Pct(s.screenShake), d => s.screenShake += d * .25f, "Explosions, hits, the boss.");
            Toggle(m, "Fullscreen", () => s.fullscreen, v => s.fullscreen = v, "Windowed or fullscreen.");
            Step(m, "Graphics quality", () => $"< {QualityName(s.quality)} >", d =>
            {
                int n = Mathf.Max(1, QualitySettings.names.Length);
                int q = s.quality < 0 ? QualitySettings.GetQualityLevel() : s.quality;
                s.quality = ((q + d) % n + n) % n;
            }, "Lower it if the game stutters with four players.");
            Toggle(m, "Show frame rate", () => s.showFps, v => s.showFps = v, "A small FPS counter in the corner.");

            m.Header("CONTROLS");
            m.Add("Keyboard & mouse", ">", "Rebind keys and mouse buttons.", open: () => Controls(PlayerControls.KeyboardGroup, () => Settings(back, p)));
            m.Add("Controller", ">", "Rebind gamepad buttons (Xbox, PlayStation, Switch Pro and others).", open: () => Controls(PlayerControls.GamepadGroup, () => Settings(back, p)));
            m.Add("Reset all controls", null, "Back to the default keys and buttons.", pick: () => { Rebinder.ResetAll(); Sfx.Play("bell", .4f, .2f); });
            if (back != null) m.Add("Back", null, null, open: back);
            m.FirstSelectable();
            return m;
        }

        static string QualityName(int q)
        {
            var names = QualitySettings.names;
            if (names.Length == 0) return "Default";
            if (q < 0 || q >= names.Length) q = QualitySettings.GetQualityLevel();
            return names[q];
        }

        static void Slider(Menu m, string label, Func<float> get, Action<float> set, string detail)
        {
            var it = m.Add(label, Pct(get()), detail);
            it.OnAdjust = d => { set(Mathf.Clamp01(Mathf.Round((get() + d * .1f) * 10) / 10)); Save(); it.Right = Pct(get()); };
        }

        static void Toggle(Menu m, string label, Func<bool> get, Action<bool> set, string detail)
        {
            var it = m.Add(label, OnOff(get()), detail);
            void Flip() { set(!get()); Save(); it.Right = OnOff(get()); }
            it.OnPick = Flip;
            it.OnAdjust = _ => Flip();
        }

        static void Step(Menu m, string label, Func<string> show, Action<int> change, string detail)
        {
            var it = m.Add(label, show(), detail);
            it.OnAdjust = d => { change(d); Save(); it.Right = show(); };
            it.OnPick = () => { change(1); Save(); it.Right = show(); };
        }

        /// <summary>Rebinding list for one device group. Picking a row listens for the new key or button.</summary>
        public static Menu Controls(string group, Func<Menu> back)
        {
            bool kb = group == PlayerControls.KeyboardGroup;
            var m = new Menu { Title = kb ? "KEYBOARD & MOUSE" : "CONTROLLER", System = true, Parent = back };
            m.Rebuild = () => Controls(group, back);
            foreach (var (action, part, label) in PlayerControls.Rebindable)
            {
                if (!kb && action == "Move") continue;   // the left stick moves
                string a = action, pa = part, l = label;
                m.Add(l, Rebinder.Current(a, group, pa), kb ? "Pick, then press the new key or mouse button (Esc cancels)." : "Pick, then press the new button.",
                      pick: () => Rebinder.Start(a, group, pa, l));
            }
            m.Header(kb ? "Mouse look and the cursor aren't rebindable." : "Left stick moves; right stick looks or aims.");
            m.Add("Back", null, null, open: back);
            m.FirstSelectable();
            return m;
        }
    }
}

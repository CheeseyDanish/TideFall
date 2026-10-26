// TideWall 3D — menus as data: items with labels, values and actions, drawn as a list, a grid of item cards (the
// shop, like the 2D game's) or a row of big cards (tidal cards). Players navigate the in-game ones in their own
// viewport with the d-pad / arrow keys while still walking; the main menu draws the same Menu objects full screen.
// Purchases come out of the shared team purse (sand dollars, golden pearls).
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
        /// <summary>Grid and card layouts: a model shown as a picture (ModelFactory name), a stat line, a colour, a state.</summary>
        public string Icon, Info;
        public Color Tint = Color.clear;
        public bool Owned, Equipped;
    }

    public enum MenuLayout { List, Grid, Cards }

    public class Menu
    {
        public string Title;
        public readonly List<MenuItem> Items = new();
        public int Selected;
        /// <summary>Rebuilds the menu after a purchase or a change (prices, labels and values change).</summary>
        public Func<Menu> Rebuild;
        /// <summary>Back goes here instead of closing (submenus).</summary>
        public Func<Menu> Parent;
        /// <summary>Pause/settings menus: they pause an offline game.</summary>
        public bool System;
        /// <summary>How it's drawn: a list, a grid of item cards (shop), or a row of big cards (tidal cards).</summary>
        public MenuLayout Layout;
        public int Columns => Layout == MenuLayout.Grid ? 3 : 0;
        public bool Horizontal => Layout == MenuLayout.Cards;
        /// <summary>Tabs across the top (Swap / Reload switch them); OnTab builds the menu for tab Tab ± 1.</summary>
        public string[] Tabs;
        public int Tab;
        public Func<int, Menu> OnTab;
        /// <summary>A stall's menu closes when you walk this far from it.</summary>
        public Vector3? Anchor;
        public float AnchorRange = 6.5f;

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

        /// <summary>Grid navigation: a step left/right, or a row up/down.</summary>
        public void MoveGrid(int dx, int dy)
        {
            if (Items.Count == 0) return;
            int c = Mathf.Max(1, Columns);
            int next = Selected + dx + dy * c;
            if (dx != 0) next = (next % Items.Count + Items.Count) % Items.Count;
            else if (next < 0 || next >= Items.Count) next = dy > 0 ? Mathf.Min(Items.Count - 1, (Selected % c)) : Items.Count - 1;
            Selected = next;
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

        // ------------------------------------------------------------------ the shop (like the 2D game's: tabs of item cards)
        public enum ShopTab { Guns, Melee, Upgrades, Defences }
        static readonly string[] ShopTabs = { "Guns", "Melee", "Upgrades", "Defences" };

        /// <summary>The shop: a tab of item cards with pictures, prices and stats. Swap / Reload change tabs.</summary>
        public static Menu Shop(Player p, ShopTab tab, Vector3 stall)
        {
            var gm = GameManager.I;
            var m = new Menu { Title = "SHOP", Layout = MenuLayout.Grid, Tabs = ShopTabs, Tab = (int)tab, Anchor = stall, Rebuild = () => Shop(p, tab, stall) };
            m.OnTab = d => Shop(p, (ShopTab)(((int)tab + d + ShopTabs.Length) % ShopTabs.Length), stall);
            switch (tab)
            {
                case ShopTab.Guns:
                    foreach (var g in gm.DB.guns)
                    {
                        var gun = g;
                        bool owned = p.OwnsGun(gun.id), equipped = p.Gun == gun;
                        m.Items.Add(new MenuItem
                        {
                            Label = gun.displayName, Icon = gun.model, Detail = gun.description, Owned = owned, Equipped = equipped,
                            Info = $"{gun.damage:0}{(gun.pellets > 1 ? "×" + gun.pellets : "")} dmg · {gun.fireRate:0.#}/s · {gun.magazine} rounds",
                            Right = equipped ? "Equipped" : owned ? "Equip" : $"{gun.price} SD",
                            Blocked = () => equipped ? "Already equipped" : null,
                            OnPick = () => { if (owned || gm.TrySpend(gun.price)) { p.GiveGun(gun.id); Sfx.Play("reload"); } },
                        });
                    }
                    break;
                case ShopTab.Melee:
                    foreach (var w in gm.DB.melee)
                    {
                        var mel = w;
                        bool owned = p.OwnsMelee(mel.id), equipped = p.Melee == mel;
                        m.Items.Add(new MenuItem
                        {
                            Label = mel.displayName, Icon = mel.model, Detail = mel.description, Owned = owned, Equipped = equipped,
                            Info = $"{mel.damage:0} dmg · {mel.arcDegrees:0}° · {mel.range:0.#} m · {mel.style}",
                            Right = equipped ? "Equipped" : owned ? "Equip" : $"{mel.price} SD",
                            Blocked = () => equipped ? "Already equipped" : null,
                            OnPick = () => { if (owned || gm.TrySpend(mel.price)) { p.GiveMelee(mel.id); Sfx.Play("swing"); } },
                        });
                    }
                    break;
                case ShopTab.Upgrades:
                    foreach (var u in gm.DB.upgrades)
                    {
                        var up = u;
                        int lvl = gm.Team.Level(up.id), price = gm.Team.Price(up);
                        m.Items.Add(new MenuItem
                        {
                            Label = up.displayName, Icon = UpgradeIcon(up.kind), Detail = up.description,
                            Info = up.maxLevel > 0 ? $"Level {lvl}/{up.maxLevel}" : lvl > 0 ? $"Bought {lvl}×" : "Whole team",
                            Right = gm.Team.Maxed(up) ? "Max" : $"{price} SD", Owned = gm.Team.Maxed(up),
                            Blocked = () => gm.Team.Blocked(up),
                            OnPick = () => { if (gm.TrySpend(price)) { gm.Team.Apply(up); Sfx.Play("bell", .5f, .2f); } },
                        });
                    }
                    break;
                default:
                    AddDefences(m, p);
                    break;
            }
            if (m.Items.Count > 0) m.Selected = 0;
            return m;
        }

        static string UpgradeIcon(UpgradeKind k) => k switch
        {
            UpgradeKind.RepairWall or UpgradeKind.WallMaxHp => "WallSegment",
            UpgradeKind.PlantRepair or UpgradeKind.PlantMaxHp => "Powerplant",
            UpgradeKind.PlayerMaxHp => "Sailor",
            UpgradeKind.GunDamage or UpgradeKind.Magazine => "Revolver",
            UpgradeKind.MeleeDamage => "Cutlass",
            UpgradeKind.GasCans => "GasCan",
            _ => "Crate",
        };

        static void AddDefences(Menu m, Player p)
        {
            var gm = GameManager.I;
            foreach (var st in gm.DB.structures)
            {
                var def = st;
                int built = Structure.All.FindAll(s => s != null && !s.Dead && s.Def == def).Count;
                m.Items.Add(new MenuItem
                {
                    Label = def.displayName, Icon = def.model, Detail = def.description,
                    Info = (def.max > 0 ? $"{built}/{def.max} built · " : built > 0 ? $"{built} built · " : "") + $"{def.hp:0} HP",
                    Right = def.pearls > 0 ? $"{def.sandDollars} SD + {def.pearls} pearl{(def.pearls > 1 ? "s" : "")}" : $"{def.sandDollars} SD",
                    Blocked = () => gm.SandDollars < def.sandDollars ? "Not enough sand dollars" : gm.Pearls < def.pearls ? "Not enough golden pearls (giant clams drop them)"
                                  : def.max > 0 && built >= def.max ? $"At most {def.max}" : null,
                    OnPick = () => p.StartBuilding(def),
                });
            }
        }

        /// <summary>Build menu (Build button, any time): pick a defence, then place it where you aim.</summary>
        public static Menu Build(Player p)
        {
            var m = new Menu { Title = "BUILD", Layout = MenuLayout.Grid, Rebuild = () => Build(p) };
            AddDefences(m, p);
            if (m.Items.Count > 0) m.Selected = 0;
            return m;
        }

        public static Menu Hotdogs(Player p, Vector3 stall)
        {
            var gm = GameManager.I;
            var m = new Menu { Title = "HOTDOG STAND", Layout = MenuLayout.Grid, Anchor = stall, Rebuild = () => Hotdogs(p, stall) };
            var hotdog = gm.DB.Food("hotdog");
            if (hotdog != null)
            {
                m.Items.Add(Food(p, hotdog, null));
                foreach (var c in gm.DB.condiments) m.Items.Add(Food(p, hotdog, c));
            }
            foreach (var f in gm.DB.foods) if (f.soldAtStand && f != hotdog) m.Items.Add(Food(p, f, null));
            if (m.Items.Count > 0) m.Selected = 0;
            return m;
        }

        static MenuItem Food(Player p, FoodDef food, CondimentDef sauce)
        {
            var gm = GameManager.I;
            int price = food.price + (sauce?.price ?? 0);
            return new MenuItem
            {
                Label = sauce == null ? food.displayName : $"{food.displayName} + {sauce.displayName}",
                Icon = food.model, Tint = sauce != null ? sauce.color : Color.clear,
                Detail = sauce == null ? food.description : sauce.description,
                Info = $"+{food.heal:0} HP",
                Right = $"{price} SD",
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
            m.Add("Stats & mods", p.StatPoints > 0 || p.PendingMods > 0 ? "!" : ">", "Spend stat points, choose weapon mods, respec.",
                  pick: () => { if (p.Kind == PlayerKind.LocalProxy) p.RequestStats(); else p.OpenMenu(Stats(p)); });
            m.Add("Settings", ">", "Audio, game and controls.", open: () => Settings(() => Pause(p), p));
            if (!Net.IsClient && gm.SaveSlot > 0)
                m.Add("Save game", $"slot {gm.SaveSlot}", "Saves the team's sand dollars, upgrades, wall and weapons.",
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
            Slider(m, "Interface", () => s.interfaceVolume, v => s.interfaceVolume = v, "Menu clicks, bells and sand dollars.");
            Slider(m, "Ambience", () => s.ambience, v => s.ambience = v, "Surf and wind.");

            m.Header("GAME");
            Toggle(m, "Start in first person", () => s.startFirstPerson, v => s.startFirstPerson = v, "The view new players start with. Switch any time with the View button.");
            Step(m, "Field of view", () => $"< {s.fov:0} >", d => s.fov += d * 5, "First-person field of view, in degrees.");
            Step(m, "Mouse sensitivity", () => $"< {s.mouseSensitivity:0.0} >", d => s.mouseSensitivity = Mathf.Round((s.mouseSensitivity + d * .1f) * 10) / 10, "First-person mouse look.");
            Step(m, "Stick sensitivity", () => $"< {s.stickSensitivity:0.0} >", d => s.stickSensitivity = Mathf.Round((s.stickSensitivity + d * .1f) * 10) / 10, "First-person right-stick look.");
            Toggle(m, "Invert look up/down", () => s.invertY, v => s.invertY = v, "Mouse and stick.");
            Toggle(m, "Aim assist (controllers)", () => s.aimAssist, v => s.aimAssist = v, "Slows the stick over enemies and nudges shots onto bodies.");
            Step(m, "Screen shake", () => Pct(s.screenShake), d => s.screenShake += d * .25f, "Explosions, hits, the boss.");
            Toggle(m, "Damage numbers", () => s.damageNumbers, v => s.damageNumbers = v, "Numbers pop off creatures you hit (yellow = weak point).");
            Toggle(m, "Show frame rate", () => s.showFps, v => s.showFps = v, "A small FPS counter in the corner.");

            m.Header("GRAPHICS");
            Step(m, "Display mode", () => $"< {GraphicsApply.DisplayModeNames[s.displayMode]} >", d => s.displayMode = ((s.displayMode + d) % 3 + 3) % 3,
                 "Borderless fullscreen, exclusive fullscreen, or a window.");
            Step(m, "Resolution", () => $"< {GraphicsApply.ResolutionName(s.resolution)} >", d =>
            {
                int n = GraphicsApply.Resolutions().Count + 1;   // + native
                s.resolution = ((s.resolution + 1 + d) % n + n) % n - 1;
            }, "Native uses your display's full resolution.");
            Toggle(m, "VSync", () => s.vsync, v => s.vsync = v, "Match the monitor's refresh rate (no tearing).");
            Step(m, "Frame rate limit", () => $"< {(s.fpsCap > 0 ? s.fpsCap + " fps" : "Unlimited")} >", d =>
            {
                int i = System.Array.IndexOf(GraphicsApply.FpsCaps, s.fpsCap); if (i < 0) i = 0;
                int n = GraphicsApply.FpsCaps.Length;
                s.fpsCap = GraphicsApply.FpsCaps[((i + d) % n + n) % n];
            }, "Only when VSync is off.");
            Step(m, "Render scale", () => Pct(s.renderScale), d => s.renderScale = Mathf.Round((s.renderScale + d * .1f) * 10) / 10,
                 "Internal resolution. Above 100% is supersampling: sharper but slower. (Applies in the built game.)");
            Step(m, "Anti-aliasing", () => $"< {GraphicsApply.AntiAliasingNames[s.antiAliasing]} >", d =>
            {
                int n = GraphicsApply.AntiAliasingNames.Length;
                s.antiAliasing = ((s.antiAliasing + d) % n + n) % n;
            }, "Smooths jagged edges. MSAA is the sharpest; FXAA the cheapest.");
            Step(m, "Shadows", () => $"< {GraphicsApply.ShadowNames[s.shadows]} >", d => s.shadows = ((s.shadows + d) % 4 + 4) % 4,
                 "Shadow quality and distance.");
            Toggle(m, "Bloom", () => s.bloom, v => s.bloom = v, "Glow around bright things (torches, the reactor core).");
            Toggle(m, "Post-processing", () => s.postProcessing, v => s.postProcessing = v, "Colour grading, bloom and vignette.");
            Step(m, "Draw distance", () => $"< {s.drawDistance:0} m >", d => s.drawDistance += d * 20, "How far you can see out to sea.");
            Step(m, "Particle density", () => Pct(s.particleDensity), d => s.particleDensity = Mathf.Round((s.particleDensity + d * .25f) * 4) / 4,
                 "Drips, gore, sparks and smoke. Lower it on slower computers.");
            Step(m, "Quality preset", () => $"< {QualityName(s.quality)} >", d =>
            {
                int n = Mathf.Max(1, QualitySettings.names.Length);
                int q = s.quality < 0 ? QualitySettings.GetQualityLevel() : s.quality;
                s.quality = ((q + d) % n + n) % n;
            }, "Texture and detail level. Resolution, anti-aliasing and shadows are the options above.");

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
        // ------------------------------------------------------------------ stats, mods, respec
        public const string StatsTitle = "STATS & MODS";

        /// <summary>Level, stat points to spend, weapon mods to choose, and a respec. Runs on the host for online players.</summary>
        public static Menu Stats(Player p)
        {
            var gm = GameManager.I;
            var m = new Menu { Title = StatsTitle, Rebuild = () => Stats(p) };
            m.Header($"LEVEL {p.Level}   XP {p.Xp}/{p.XpNext}   ·   {p.StatPoints} point{(p.StatPoints == 1 ? "" : "s")} to spend");
            foreach (var st in gm.DB.stats)
            {
                var stat = st;
                int pts = p.Points(stat.id);
                string bonus = stat.kind == StatKind.MaxHp ? $"+{pts * stat.perPoint:0} HP" : $"+{pts * stat.perPoint * 100:0}%";
                m.Add($"{stat.displayName}  {pts}/{stat.maxPoints}", p.StatPoints > 0 && pts < stat.maxPoints ? "+1" : bonus,
                      $"{stat.description} Now: {bonus}.", pick: () => p.SpendPoint(stat.id), blocked: () => p.WhyCantSpend(stat.id));
            }
            if (p.ModOffer != null)
            {
                m.Header($"CHOOSE A WEAPON MOD{(p.PendingMods > 1 ? $" ({p.PendingMods} waiting)" : "")}");
                foreach (var mod in p.ModOffer)
                {
                    var md = mod;
                    m.Add(md.displayName, md.gun ? p.Gun.displayName : p.Melee.displayName, md.description, pick: () => p.TakeMod(md));
                }
            }
            else m.Header($"<size=13>Next weapon mod at level {NextModLevel(p, gm)}</size>");
            m.Header("YOUR MODS");
            var gunMods = string.Join(", ", ModNames(p, p.Gun.id, gm));
            var meleeMods = string.Join(", ", ModNames(p, p.Melee.id, gm));
            m.Header($"<size=13>{p.Gun.displayName}: {(gunMods.Length > 0 ? gunMods : "none")}</size>");
            m.Header($"<size=13>{p.Melee.displayName}: {(meleeMods.Length > 0 ? meleeMods : "none")}</size>");
            m.Add("Respec", $"{p.RespecPrice} SD", "Take back every stat point to spend again. Costs more each time (paid from the team's sand dollars).",
                  pick: () => p.Respec(), blocked: () => p.SpentPoints == 0 ? "No points to take back" : gm.SandDollars < p.RespecPrice ? "Not enough sand dollars" : null);
            m.Add("Close", null, null, pick: p.CloseMenu);
            m.FirstSelectable();
            return m;
        }

        static int NextModLevel(Player p, GameManager gm)
        {
            int every = Mathf.Max(1, gm.DB.modEveryLevels);
            return (p.Level / every + 1) * every;
        }

        static IEnumerable<string> ModNames(Player p, string weapon, GameManager gm)
        {
            foreach (var id in p.ModsOn(weapon)) if (gm.DB.Mod(id) is { } md) yield return md.displayName;
        }

        // ------------------------------------------------------------------ tidal cards
        /// <summary>The draft vote: pick one of the offered cards. Everyone votes; ties and no-shows are decided at random.</summary>
        public static Menu CardVote(Player p)
        {
            var cards = GameManager.I.Cards;
            var m = new Menu { Title = TidalCards.MenuTag, Layout = MenuLayout.Cards, Rebuild = () => CardVote(p) };
            if (cards.Draft == null) { m.Add("No vote right now", null, "Tidal cards are drafted at the start of each fight.", pick: p.CloseMenu); m.Selected = 0; return m; }
            int n = cards.Draft.Count;
            for (int i = 0; i < n + (cards.RerollOffered ? 2 : 1); i++)
            {
                int choice = i;
                int votes = 0;
                foreach (var v in cards.Votes.Values) if (v == choice) votes++;
                bool mine = cards.Votes.TryGetValue(p.Index, out var my) && my == choice;
                string right = (mine ? "Your vote · " : "") + $"{votes} vote{(votes == 1 ? "" : "s")}";
                if (i < n)
                {
                    var c = cards.Draft[i];
                    string tag = c.exclusive == CardExclusive.Online ? "ONLINE ONLY" : c.exclusive == CardExclusive.SplitScreen ? "SPLIT SCREEN ONLY" : "";
                    m.Items.Add(new MenuItem { Label = c.displayName, Detail = c.description, Info = $"{c.waves} waves" + (tag.Length > 0 ? $"\n{tag}" : ""),
                                               Tint = c.color, Right = right, Equipped = mine, OnPick = () => cards.Vote(p, choice) });
                }
                else if (i == n)
                    m.Items.Add(new MenuItem { Label = "???", Detail = "A random tidal card. Could be anything that can appear right now, good or bad.",
                                               Info = "Random", Tint = new Color(.25f, .25f, .3f), Right = right, Equipped = mine, OnPick = () => cards.Vote(p, choice) });
                else
                    m.Items.Add(new MenuItem { Label = "Re-roll", Detail = "Rare! Throw these back and draw three new cards.",
                                               Info = "New cards", Tint = new Color(.95f, .8f, .3f), Right = right, Equipped = mine, OnPick = () => cards.Vote(p, choice) });
            }
            m.Selected = cards.Votes.TryGetValue(p.Index, out var mv) ? Mathf.Clamp(mv, 0, m.Items.Count - 1) : 0;
            return m;
        }
    }
}

// TideWall 3D — the two big menu layouts, drawn inside a player's viewport:
//   GridView  — the shop: tabs and the purse on top, four rows of four compact tiles (picture, name, numbers, price)
//               that scroll down from the top, and the selected item's description below.
//   CardsView — the tidal card vote: a row of big cards (colour, name, waves, description, votes), a face-down
//               random card, and sometimes a re-roll.
//   TableView — the casino: a felt table in a brass rail (the menu's Board, redrawn every frame so cards can turn
//               over) above the list of actions (bet, deal, hit…), the purse, the selected action's description, and
//               a win's celebration (the rail flares, sand dollars rain).
//   SlotsView — the slot machine on that felt: reel windows of pictures, the lever, the paytable, the tide meter.
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TideWall
{
    public class GridView
    {
        const int Cols = Menu.GridColumns, Rows = 4;
        const float TileW = 196, TileH = 86, StepX = 202, StepY = 92, Left = -310, Top = 160, BarX = 405, BarH = 362;
        static readonly Color Idle = new(.11f, .15f, .25f, .96f), Picked = new(.2f, .26f, .43f, .98f), Worn = new(.11f, .26f, .19f, .96f), Gold = new(1, .82f, .3f);
        readonly RectTransform root;
        readonly TMP_Text title, tabs, purse, detail, help;
        readonly Image bar, thumb;
        readonly List<(Image bg, Image icon, TMP_Text name, TMP_Text info, TMP_Text price, Image frame)> tiles = new();
        int scroll;

        public GridView(Transform canvas)
        {
            var panel = Ui.Image(canvas, "Shop", new Vector2(.5f, .5f), new Vector2(0, -10), new Vector2(836, 498), new Color(.05f, .07f, .13f, 1));
            root = panel.rectTransform;
            var header = Ui.Image(root, "Header", new Vector2(.5f, 1), Vector2.zero, new Vector2(836, 40), new Color(.08f, .11f, .21f, 1));
            title = Ui.Text(header.transform, "Title", new Vector2(0, .5f), new Vector2(14, 0), new Vector2(170, 34), 16, TextAnchor.MiddleLeft, Gold);
            title.rectTransform.pivot = new Vector2(0, .5f);
            tabs = Ui.Text(header.transform, "Tabs", new Vector2(.5f, .5f), new Vector2(-10, 0), new Vector2(470, 34), 14, TextAnchor.MiddleCenter, Color.white);
            purse = Ui.Text(header.transform, "Purse", new Vector2(1, .5f), new Vector2(-12, 0), new Vector2(150, 34), 14, TextAnchor.MiddleRight, new Color(1f, .93f, .75f));
            purse.rectTransform.pivot = new Vector2(1, .5f);
            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Cols; c++)
                {
                    var pos = new Vector2(Left + c * StepX, Top - r * StepY);
                    var frame = Ui.Image(root, "Frame", new Vector2(.5f, .5f), pos, new Vector2(TileW + 4, TileH + 4), Gold);
                    var bg = Ui.Image(root, "Tile", new Vector2(.5f, .5f), pos, new Vector2(TileW, TileH), Idle);
                    var icon = Ui.Image(bg.transform, "Icon", new Vector2(0, .5f), new Vector2(4, 0), new Vector2(60, 60), Color.white);
                    icon.preserveAspect = true;
                    var name = Words(bg.transform, "Name", new Vector2(68, -5), new Vector2(124, 18), 13, Color.white, true);
                    var info = Words(bg.transform, "Info", new Vector2(68, -24), new Vector2(124, 40), 11, new Color(.72f, .8f, .93f), false);
                    var price = Ui.Text(bg.transform, "Price", new Vector2(1, 0), new Vector2(-6, 4), new Vector2(130, 18), 13, TextAnchor.LowerRight, Gold);
                    price.rectTransform.pivot = new Vector2(1, 0);
                    tiles.Add((bg, icon, name, info, price, frame));
                }
            bar = Ui.Image(root, "Scroll", new Vector2(.5f, .5f), new Vector2(BarX, Top - 1.5f * StepY), new Vector2(5, BarH), new Color(1, 1, 1, .08f));
            thumb = Ui.Image(root, "Thumb", new Vector2(.5f, .5f), new Vector2(BarX, 0), new Vector2(5, 40), Gold);
            detail = Ui.Text(root, "Detail", new Vector2(.5f, 0), new Vector2(-70, 34), new Vector2(660, 56), 14, TextAnchor.MiddleLeft, new Color(.88f, .92f, 1));
            help = Ui.Text(root, "Help", new Vector2(1, 0), new Vector2(-12, 34), new Vector2(150, 44), 13, TextAnchor.MiddleRight, new Color(.65f, .7f, .85f));
            help.rectTransform.pivot = new Vector2(1, .5f);
            root.gameObject.SetActive(false);
        }

        /// <summary>A line of a tile, cut off rather than spilling out of it (the whole thing is in the line below the grid).</summary>
        static TMP_Text Words(Transform parent, string name, Vector2 pos, Vector2 size, int fontSize, Color c, bool oneLine)
        {
            var t = Ui.Text(parent, name, new Vector2(0, 1), pos, size, fontSize, TextAnchor.UpperLeft, c);
            t.rectTransform.pivot = new Vector2(0, 1);
            t.textWrappingMode = oneLine ? TextWrappingModes.NoWrap : TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Ellipsis;
            return t;
        }

        public void Hide() => root.gameObject.SetActive(false);

        /// <summary>Which item (or tab, as TabBase + index) is under this screen point; -1 for neither.</summary>
        public int Hit(Menu m, Vector2 screen, Camera cam)
        {
            if (m.Tabs != null)
            {
                int link = TMP_TextUtilities.FindIntersectingLink(tabs, screen, cam);
                if (link >= 0 && int.TryParse(tabs.textInfo.linkInfo[link].GetLinkID(), out int tab)) return Menu.TabBase + tab;
            }
            for (int i = 0; i < tiles.Count; i++)
            {
                int index = scroll * Cols + i;
                if (index < m.Items.Count && RectTransformUtility.RectangleContainsScreenPoint(tiles[i].bg.rectTransform, screen, cam)) return index;
            }
            return -1;
        }

        /// <summary>Tests: the middle of an item's card on screen.</summary>
        public Vector2 ScreenOf(int index, Camera cam) => RectTransformUtility.WorldToScreenPoint(cam, tiles[Mathf.Clamp(index - scroll * Cols, 0, tiles.Count - 1)].bg.rectTransform.TransformPoint(tiles[0].bg.rectTransform.rect.center));

        public void Show(Menu m, Player p, GameManager gm)
        {
            if (!root.gameObject.activeSelf) root.SetAsLastSibling();   // over the rest of the HUD
            root.gameObject.SetActive(true);
            title.text = $"<b>{m.Title}</b>";
            if (m.Tabs != null)
            {
                var sb = new System.Text.StringBuilder($"<color=#64748b>{p.Glyph(Btn.Swap)}</color>   ");
                for (int i = 0; i < m.Tabs.Length; i++)
                    sb.Append(i == m.Tab ? $"<link=\"{i}\"><color=#fde68a><b><u>{m.Tabs[i]}</u></b></color></link>   " : $"<link=\"{i}\"><color=#94a3b8>{m.Tabs[i]}</color></link>   ");
                tabs.text = sb.Append($"<color=#64748b>{p.Glyph(Btn.Reload)}</color>").ToString();
            }
            else tabs.text = "";
            purse.text = $"<color=#fde68a>{gm.SandDollars} SD</color>" + (gm.Pearls > 0 ? $"  <color=#fcd34d>{gm.Pearls} pearls</color>" : "");

            int rows = (m.Items.Count + Cols - 1) / Cols;
            int row = Mathf.Max(0, m.Selected) / Cols;
            if (row < scroll) scroll = row;
            if (row >= scroll + Rows) scroll = row - Rows + 1;
            scroll = Mathf.Clamp(scroll, 0, Mathf.Max(0, rows - Rows));
            for (int i = 0; i < tiles.Count; i++)
            {
                int index = scroll * Cols + i;
                var t = tiles[i];
                bool on = index < m.Items.Count;
                t.bg.gameObject.SetActive(on);
                t.frame.gameObject.SetActive(on && index == m.Selected);
                if (!on) continue;
                var it = m.Items[index];
                bool blocked = it.Blocked?.Invoke() != null;
                t.bg.color = index == m.Selected ? Picked : it.Equipped ? Worn : Idle;
                var sprite = Icons.For(it.Icon);
                t.icon.sprite = sprite;
                t.icon.enabled = sprite != null;
                bool cant = blocked && !it.Owned && !it.Equipped;
                var tint = it.Tint.a > 0 ? Color.Lerp(Color.white, it.Tint, .35f) : Color.white;
                t.icon.color = cant ? new Color(tint.r, tint.g, tint.b, .45f) : tint;
                t.name.text = $"<b>{it.Label}</b>";
                t.info.text = it.Info ?? "";
                t.price.text = it.Equipped || it.Owned ? $"<color=#86efac>{it.Right}</color>" : cant ? $"<color=#f87171>{it.Right}</color>" : it.Right;
            }
            bool more = rows > Rows;
            bar.enabled = thumb.enabled = more;
            if (more)
            {
                float h = BarH * Rows / rows, travel = BarH - h;
                thumb.rectTransform.sizeDelta = new Vector2(5, h);
                thumb.rectTransform.anchoredPosition = new Vector2(BarX, bar.rectTransform.anchoredPosition.y + travel / 2 - travel * scroll / Mathf.Max(1, rows - Rows));
            }
            var cur = m.Current;
            string why = cur?.Blocked?.Invoke();
            detail.text = cur == null ? "" : cur.Detail + (why != null && !cur.Equipped ? $"\n<color=#fca5a5>{why}</color>" : "");
            string word = m.Tabs == null ? "Pick" : cur?.Open != null && cur.OnPick == null ? "Open" : cur != null && cur.Owned ? "Use" : "Buy";
            help.text = $"{p.Glyph(Btn.Interact)} {word}\n{p.Glyph(Btn.Back)} Close";
        }
    }

    public class CardsView
    {
        readonly RectTransform root;
        readonly TMP_Text title, help;
        readonly List<(RectTransform rt, Image bg, Image band, TMP_Text name, TMP_Text info, TMP_Text desc, TMP_Text votes, TMP_Text big)> cards = new();

        public CardsView(Transform canvas)
        {
            root = Ui.Rect(canvas, "TidalCards", new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, -30), new Vector2(1000, 400));
            Ui.Fill(root, "Shade", new Color(.02f, .04f, .1f, .55f));
            title = Ui.Text(root, "Title", new Vector2(.5f, 1), new Vector2(0, -6), new Vector2(900, 34), 24, TextAnchor.UpperCenter, new Color(.6f, .95f, 1));
            title.rectTransform.pivot = new Vector2(.5f, 1);
            for (int i = 0; i < 5; i++)
            {
                var bg = Ui.Image(root, "Card", new Vector2(.5f, .5f), Vector2.zero, new Vector2(180, 270), new Color(.07f, .1f, .18f, .95f));
                var band = Ui.Image(bg.transform, "Band", new Vector2(.5f, 1), Vector2.zero, new Vector2(180, 64), Color.white);   // pivots at the top edge
                var name = Ui.Text(band.transform, "Name", new Vector2(.5f, .5f), Vector2.zero, new Vector2(170, 56), 18, TextAnchor.MiddleCenter, Color.white);
                var info = Ui.Text(bg.transform, "Info", new Vector2(.5f, 1), new Vector2(0, -70), new Vector2(170, 36), 13, TextAnchor.UpperCenter, new Color(1, .9f, .6f));
                var desc = Ui.Text(bg.transform, "Desc", new Vector2(.5f, .5f), new Vector2(0, -36), new Vector2(160, 110), 13, TextAnchor.UpperCenter, new Color(.9f, .93f, 1));
                var votes = Ui.Text(bg.transform, "Votes", new Vector2(.5f, 0), new Vector2(0, 16), new Vector2(170, 22), 13, TextAnchor.MiddleCenter, new Color(.8f, .85f, 1));
                var big = Ui.Text(bg.transform, "Big", new Vector2(.5f, .5f), new Vector2(0, -10), new Vector2(170, 120), 90, TextAnchor.MiddleCenter, new Color(1, 1, 1, .85f));
                cards.Add((bg.rectTransform, bg, band, name, info, desc, votes, big));
            }
            help = Ui.Text(root, "Help", new Vector2(.5f, 0), new Vector2(0, -6), new Vector2(900, 22), 13, TextAnchor.LowerCenter, new Color(.7f, .75f, .9f));
            root.gameObject.SetActive(false);
        }

        public void Hide() => root.gameObject.SetActive(false);

        public int Hit(Menu m, Vector2 screen, Camera cam)
        {
            for (int i = 0; i < cards.Count && i < m.Items.Count; i++)
                if (RectTransformUtility.RectangleContainsScreenPoint(cards[i].rt, screen, cam)) return i;
            return -1;
        }

        public void Show(Menu m, Player p, GameManager gm)
        {
            root.gameObject.SetActive(true);
            title.text = gm.Cards.Draft != null ? $"<b>TIDAL CARDS</b>   <size=16>Vote for the next tide · {Mathf.CeilToInt(gm.Cards.DraftLeft)}s · {gm.Cards.Votes.Count}/{gm.Players.Count} voted</size>" : "<b>TIDAL CARDS</b>";
            int n = m.Items.Count;
            float spacing = 196;
            for (int i = 0; i < cards.Count; i++)
            {
                var c = cards[i];
                bool on = i < n;
                c.rt.gameObject.SetActive(on);
                if (!on) continue;
                var it = m.Items[i];
                bool sel = i == m.Selected;
                bool faceDown = it.Label == "???";
                c.rt.anchoredPosition = new Vector2((i - (n - 1) / 2f) * spacing, sel ? 8 : 0);
                c.rt.localScale = Vector3.one * (sel ? 1.07f : 1f);
                c.bg.color = sel ? new Color(.13f, .18f, .32f, .98f) : new Color(.07f, .1f, .18f, .95f);
                c.band.color = it.Tint.a > 0 ? it.Tint : new Color(.3f, .5f, .8f);
                c.name.text = faceDown ? "<b>?</b>" : $"<b>{it.Label}</b>";
                c.info.text = it.Info ?? "";
                c.desc.text = faceDown ? "" : it.Detail ?? "";
                c.big.text = faceDown ? "?" : "";
                c.votes.text = it.Equipped ? $"<color=#86efac>{it.Right}</color>" : it.Right;
            }
            help.text = $"{(p.UsesMouse ? "Arrow keys" : "D-pad")} choose · {p.Glyph(Btn.Interact)} vote · {p.Glyph(Btn.Back)} close (vote later with {p.Glyph(Btn.Stats)})";
        }
    }

    public class TableView
    {
        readonly RectTransform root, feltRt;
        readonly TMP_Text title, purse, board, actions, detail, help;
        readonly Image rail, glow;
        readonly List<Image> coins = new();
        readonly SlotsView slots;

        public TableView(Transform canvas)
        {
            var panel = Ui.Image(canvas, "Table", new Vector2(.5f, .5f), new Vector2(0, -6), new Vector2(880, 520), new Color(.04f, .06f, .11f, .95f));
            root = panel.rectTransform;
            // Header: maroon with a gold trim, a suit flourish, and the purse with its sand dollar
            var header = Ui.Image(root, "Header", new Vector2(.5f, 1), Vector2.zero, new Vector2(880, 48), new Color(.36f, .07f, .1f, .98f));
            Ui.Image(header.transform, "Trim", new Vector2(.5f, 0), Vector2.zero, new Vector2(880, 3), new Color(.9f, .72f, .3f, 1));
            title = Ui.Text(header.transform, "Title", new Vector2(0, .5f), new Vector2(18, 0), new Vector2(560, 40), 22, TextAnchor.MiddleLeft, new Color(1, .85f, .4f));
            title.rectTransform.pivot = new Vector2(0, .5f);
            purse = Ui.Text(header.transform, "Purse", new Vector2(1, .5f), new Vector2(-14, 0), new Vector2(260, 40), 18, TextAnchor.MiddleRight, new Color(1f, .93f, .75f));
            purse.rectTransform.pivot = new Vector2(1, .5f);
            var coin = Ui.Image(header.transform, "Coin", new Vector2(1, .5f), new Vector2(-280, 0), new Vector2(26, 26), Color.white);
            coin.sprite = Ui.SandDollarIcon; coin.rectTransform.pivot = new Vector2(1, .5f);
            // The felt: green baize inside a brass rail, darker toward the edges
            rail = Ui.Image(root, "Rail", new Vector2(.5f, 1), new Vector2(0, -54), new Vector2(852, 282), new Color(.55f, .4f, .16f, 1));
            var felt = Ui.Image(rail.transform, "Felt", new Vector2(.5f, 1), new Vector2(0, -6), new Vector2(840, 270), new Color(.06f, .33f, .22f, 1));
            feltRt = felt.rectTransform;
            felt.gameObject.AddComponent<RectMask2D>();
            var vignette = Ui.Image(felt.transform, "Vignette", new Vector2(.5f, .5f), Vector2.zero, new Vector2(900, 330), new Color(0, .05f, .02f, .55f));
            vignette.sprite = Ui.Radial(true);
            glow = Ui.Image(felt.transform, "Glow", new Vector2(.5f, .5f), Vector2.zero, new Vector2(760, 300), new Color(1, .85f, .35f, 0));
            glow.sprite = Ui.Radial(false);
            slots = new SlotsView(felt.transform);
            board = Ui.Text(felt.transform, "Board", new Vector2(.5f, 1), new Vector2(0, -6), new Vector2(816, 258), 19, TextAnchor.MiddleCenter, Color.white);
            board.richText = true;
            // Sand dollars that rain on the felt when you win
            for (int i = 0; i < 18; i++)
            {
                var c = Ui.Image(felt.transform, "Coin", new Vector2(.5f, 1), Vector2.zero, new Vector2(30, 30), Color.white);
                c.sprite = Ui.SandDollarIcon;
                c.gameObject.SetActive(false);
                coins.Add(c);
            }
            actions = Ui.Text(root, "Actions", new Vector2(.5f, 0), new Vector2(0, 58), new Vector2(820, 124), 15, TextAnchor.UpperLeft, Color.white);
            detail = Ui.Text(root, "Detail", new Vector2(.5f, 0), new Vector2(0, 28), new Vector2(820, 30), 13, TextAnchor.MiddleCenter, new Color(.85f, .9f, 1));
            help = Ui.Text(root, "Help", new Vector2(.5f, 0), new Vector2(0, 6), new Vector2(820, 20), 12, TextAnchor.MiddleCenter, new Color(.6f, .65f, .8f));
            root.gameObject.SetActive(false);
        }

        public void Hide() => root.gameObject.SetActive(false);

        public void Show(Menu m, Player p, GameManager gm)
        {
            root.gameObject.SetActive(true);
            title.text = $"<color=#fca5a5>♥</color> <b>{m.Title}</b> <color=#e5e7eb>♠</color>";
            purse.text = $"<b>{gm.SandDollars}</b> SD";
            var reels = m.Reels?.Invoke();
            slots.Show(reels);
            // With reels on the table, the words go underneath them
            var bt = board.rectTransform;
            if (reels != null) { bt.anchoredPosition = new Vector2(0, -206); bt.sizeDelta = new Vector2(560, 58); }
            else { bt.anchoredPosition = new Vector2(0, -6); bt.sizeDelta = new Vector2(816, 258); }
            board.text = m.Board?.Invoke() ?? "";

            // A win: the rail flares gold, a glow swells on the felt, and sand dollars rain down
            float cheer = m.Celebrate?.Invoke() ?? 0;
            float t = Time.unscaledTime;
            rail.color = Color.Lerp(new Color(.55f, .4f, .16f), new Color(1, .85f, .35f), cheer * (.6f + .4f * Mathf.Sin(t * 14)));
            glow.color = new Color(1, .85f, .35f, cheer * .35f);
            int n = Mathf.RoundToInt(coins.Count * cheer);
            for (int i = 0; i < coins.Count; i++)
            {
                var c = coins[i];
                bool on = i < n;
                c.gameObject.SetActive(on);
                if (!on) continue;
                float seed = i * 0.6180339f % 1;
                float fall = (t * (140 + seed * 120) + seed * 300) % 330;
                c.rectTransform.anchoredPosition = new Vector2(-400 + seed * 800 + Mathf.Sin(t * 3 + i) * 18, 30 - fall);
                c.rectTransform.localRotation = Quaternion.Euler(0, (t * 300 + i * 40) % 360, 0);
                c.color = new Color(1, 1, 1, Mathf.Clamp01(cheer * 1.5f));
            }

            actions.text = Rows(m, 6);
            var cur = m.Current;
            string why = cur?.Blocked?.Invoke();
            detail.text = cur == null ? "" : (cur.Detail ?? "") + (!string.IsNullOrEmpty(why) ? $"  <color=#fca5a5>{why}</color>" : "");
            string adj = cur?.OnAdjust != null ? " · left/right change" : m.OnSide != null || m.QuickLabel != null ? " · left/right bet" : "";
            string ql = m.QuickLabel?.Invoke();
            string quick = !string.IsNullOrEmpty(ql) ? $" · <color=#fde68a>{p.Glyph(Btn.Reload)} {ql}</color>" : "";
            help.text = $"{(p.UsesMouse ? "Up/down" : "D-pad")} choose{adj} · {p.Glyph(Btn.Interact)} pick{quick} · {p.Glyph(Btn.Back)} {(m.Parent != null ? "back" : "leave")}";
        }

        public int Hit(Menu m, Vector2 screen, Camera cam)
        {
            int line = TMP_TextUtilities.FindIntersectingLine(actions, screen, cam);
            return line < 0 ? -1 : m.First + line;
        }

        /// <summary>The actions as rows; the selected one sits on a soft gold band.</summary>
        static string Rows(Menu m, int rows)
        {
            var sb = new System.Text.StringBuilder();
            int first = m.Window(rows);
            for (int i = first; i < Mathf.Min(m.Items.Count, first + rows); i++)
            {
                var it = m.Items[i];
                if (it.Header) { sb.Append($"<color=#93c5fd><b>{it.Label}</b></color>\n"); continue; }
                bool sel = i == m.Selected;
                bool blocked = it.Blocked?.Invoke() != null;
                string colour = blocked ? "#94a3b8" : sel ? "#fde68a" : "#ffffff";
                string line = $"{(sel ? "► " : "   ")}{it.Label}<pos=58%>{it.Right}";
                sb.Append(sel ? $"<mark=#fde68a1f><color={colour}><b>{line}</b>      </color></mark>\n" : $"<color={colour}>{line}</color>\n");
            }
            return sb.ToString();
        }
    }

    /// <summary>The Tide Machine on the felt: three reel windows of pictures scrolling past a gold line, the lever, the
    /// paytable (pictures and multipliers), and the tide meter. Driven by Menu.Reels (Slots.Show).</summary>
    public class SlotsView
    {
        const float Spacing = 84, IconSize = 92, Elevation = 1.4f;
        readonly RectTransform root, stick, ball;
        readonly Image[] frames = new Image[3];
        readonly Image[][] icons = new Image[3][];
        readonly Image tideFill, tideGlow;
        readonly TMP_Text tideLabel;
        bool pictures;

        public SlotsView(Transform felt)
        {
            root = Ui.Rect(felt, "Slots", new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(.5f, 1), Vector2.zero, new Vector2(840, 270));
            // The cabinet's face: a maroon housing with a gold edge around the windows
            var edge = Ui.Image(root, "Edge", new Vector2(.5f, 1), new Vector2(0, -12), new Vector2(404, 184), new Color(.9f, .72f, .3f, 1));
            Ui.Image(edge.transform, "Housing", new Vector2(.5f, .5f), Vector2.zero, new Vector2(396, 176), new Color(.4f, .06f, .1f, 1));
            for (int r = 0; r < 3; r++)
            {
                var x = (r - 1) * 126f;
                frames[r] = Ui.Image(edge.transform, $"Frame{r}", new Vector2(.5f, .5f), new Vector2(x, 0), new Vector2(122, 166), new Color(.25f, .04f, .07f, 1));
                var window = Ui.Image(frames[r].transform, "Window", new Vector2(.5f, .5f), Vector2.zero, new Vector2(114, 158), new Color(.98f, .95f, .86f, 1));
                window.gameObject.AddComponent<RectMask2D>();
                icons[r] = new Image[4];
                for (int k = 0; k < 4; k++)
                {
                    var ic = Ui.Image(window.transform, $"Symbol{k}", new Vector2(.5f, .5f), Vector2.zero, new Vector2(IconSize, IconSize), Color.white);
                    ic.preserveAspect = true;
                    icons[r][k] = ic;
                }
                // Shading at the top and bottom of the window, so the reel looks round
                var top = Ui.Image(window.transform, "ShadeTop", new Vector2(.5f, 1), Vector2.zero, new Vector2(114, 30), new Color(0, 0, 0, .28f));
                var bottom = Ui.Image(window.transform, "ShadeBottom", new Vector2(.5f, 0), Vector2.zero, new Vector2(114, 30), new Color(0, 0, 0, .28f));
            }
            Ui.Image(edge.transform, "Payline", new Vector2(.5f, .5f), Vector2.zero, new Vector2(392, 3), new Color(1, .82f, .3f, .9f));

            // The lever: a chrome stick and a red ball, pulled down as the reels go
            var leverBase = Ui.Image(root, "LeverBase", new Vector2(.5f, 1), new Vector2(232, -96), new Vector2(26, 26), new Color(.7f, .72f, .78f));
            leverBase.sprite = Ui.Dot;
            stick = Ui.Image(leverBase.transform, "Stick", new Vector2(.5f, .5f), Vector2.zero, new Vector2(8, 80), new Color(.78f, .8f, .86f)).rectTransform;
            stick.pivot = new Vector2(.5f, 0);
            var b = Ui.Image(leverBase.transform, "Ball", new Vector2(.5f, .5f), new Vector2(0, 80), new Vector2(30, 30), new Color(.9f, .15f, .15f));
            b.sprite = Ui.Dot;
            ball = b.rectTransform;

            // The tide meter: a tall glass of sea that fills with each losing spin
            var tube = Ui.Image(root, "Tide", new Vector2(.5f, 1), new Vector2(300, -24), new Vector2(34, 160), new Color(.05f, .1f, .2f, 1));
            tideGlow = Ui.Image(tube.transform, "Glow", new Vector2(.5f, .5f), Vector2.zero, new Vector2(110, 230), new Color(.4f, .8f, 1, 0));
            tideGlow.sprite = Ui.Radial(false);
            tideFill = Ui.Image(tube.transform, "Sea", new Vector2(.5f, .5f), Vector2.zero, new Vector2(28, 154), new Color(.2f, .6f, .95f, 1));
            tideFill.sprite = Ui.White; tideFill.type = Image.Type.Filled; tideFill.fillMethod = Image.FillMethod.Vertical; tideFill.fillOrigin = 0;
            tideLabel = Ui.Text(root, "TideLabel", new Vector2(.5f, 1), new Vector2(300, -188), new Vector2(110, 40), 13, TextAnchor.UpperCenter, new Color(.7f, .9f, 1));

            // The paytable: three little pictures and what they pay
            for (int s = Slots.Symbols.Length - 1, row = 0; s >= 0; s--, row++)
            {
                float y = -22 - row * 27;
                for (int k = 0; k < 3; k++)
                {
                    var ic = Ui.Image(root, $"Pay{s}{k}", new Vector2(.5f, 1), new Vector2(-394 + k * 30, y), new Vector2(29, 29), Color.white);
                    ic.preserveAspect = true;
                    ic.name = $"Pay{s}";
                }
                Ui.Text(root, $"PayText{s}", new Vector2(.5f, 1), new Vector2(-268, y), new Vector2(60, 24), 15, TextAnchor.MiddleLeft, s == 5 ? new Color(1, .85f, .4f) : Color.white)
                  .text = $"<b>×{Slots.Three[s]}</b>";
            }
            for (int k = 0; k < 2; k++)
            {
                var ic = Ui.Image(root, "PayShells", new Vector2(.5f, 1), new Vector2(-379 + k * 30, -22 - 6 * 27), new Vector2(29, 29), Color.white);
                ic.preserveAspect = true;
            }
            Ui.Text(root, "PayShellsText", new Vector2(.5f, 1), new Vector2(-268, -22 - 6 * 27), new Vector2(60, 24), 15, TextAnchor.MiddleLeft, Color.white).text = $"<b>×{Slots.TwoShells}</b>";
            root.gameObject.SetActive(false);
        }

        /// <summary>Pictures of the symbols (rendered from their models), filled in once.</summary>
        void Pictures()
        {
            if (pictures) return;
            pictures = true;
            foreach (var img in root.GetComponentsInChildren<Image>(true))
            {
                if (img.name.StartsWith("Pay") && img.name.Length == 4 && int.TryParse(img.name.Substring(3), out int s)) img.sprite = Icons.For(Slots.Models[s], Elevation);
                else if (img.name == "PayShells") img.sprite = Icons.For(Slots.Models[0], Elevation);
            }
        }

        public void Show(float[] state)
        {
            bool on = state != null && state.Length >= 7;
            root.gameObject.SetActive(on);
            if (!on) return;
            Pictures();
            float t = Time.unscaledTime;
            for (int r = 0; r < 3; r++)
            {
                float p = state[r];
                int c = Mathf.FloorToInt(p);
                float frac = p - c;
                for (int k = 0; k < 4; k++)
                {
                    int j = k - 1;   // -1 below the line … 2 above
                    int index = ((c + j) % Slots.StripLength + Slots.StripLength) % Slots.StripLength;
                    var ic = icons[r][k];
                    ic.sprite = Icons.For(Slots.Models[Slots.Strips[r][index]], Elevation);
                    ic.rectTransform.anchoredPosition = new Vector2(0, (j - frac) * Spacing);
                }
                // The reel holding its breath glows
                bool suspense = Mathf.RoundToInt(state[4]) == r;
                frames[r].color = suspense ? Color.Lerp(new Color(1, .75f, .2f), new Color(1, .95f, .6f), .5f + .5f * Mathf.Sin(t * 18)) : new Color(.25f, .04f, .07f, 1);
            }
            float pull = state[3];
            stick.localScale = new Vector3(1, 1 - 1.8f * pull, 1);
            ball.anchoredPosition = new Vector2(0, 80 * (1 - 1.8f * pull));
            float tide = state[5];
            bool high = state[6] > .5f;
            tideFill.fillAmount = tide;
            tideFill.color = high ? Color.Lerp(new Color(.3f, .85f, 1), new Color(1, .9f, .5f), .5f + .5f * Mathf.Sin(t * 6)) : new Color(.2f, .6f, .95f, 1);
            tideGlow.color = new Color(.4f, .8f, 1, high ? .45f + .2f * Mathf.Sin(t * 6) : 0);
            tideLabel.text = high ? "<b><color=#fde68a>HIGH\nTIDE!</color></b>" : $"TIDE\n{Mathf.RoundToInt(tide * Slots.TideFill)}/{Slots.TideFill}";
        }
    }
}

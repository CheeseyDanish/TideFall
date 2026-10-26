// TideWall 3D — the two big menu layouts, drawn inside a player's viewport:
//   GridView  — the shop (like the 2D game's): a header with tabs and the purse, a grid of item cards (picture,
//               name, stat line, price, owned/equipped), and the selected item's description.
//   CardsView — the tidal card vote: a row of big cards (colour, name, waves, description, votes), a face-down
//               random card, and sometimes a re-roll.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TideWall
{
    public class GridView
    {
        const int Cols = 3, Rows = 2;
        readonly RectTransform root;
        readonly Text title, tabs, purse, detail, help;
        readonly List<(Image bg, Image icon, Text name, Text info, Text price, Image border)> tiles = new();
        int scroll;

        public GridView(Transform canvas)
        {
            var panel = Ui.Image(canvas, "Shop", new Vector2(.5f, .5f), new Vector2(0, -10), new Vector2(780, 470), new Color(.06f, .09f, .17f, .9f));
            root = panel.rectTransform;
            var header = Ui.Image(root, "Header", new Vector2(.5f, 1), new Vector2(0, -26), new Vector2(780, 52), new Color(.08f, .12f, .24f, .95f));
            title = Ui.Text(header.transform, "Title", new Vector2(0, .5f), new Vector2(18, 0), new Vector2(220, 40), 22, TextAnchor.MiddleLeft, new Color(1, .85f, .4f));
            title.rectTransform.pivot = new Vector2(0, .5f);
            tabs = Ui.Text(header.transform, "Tabs", new Vector2(.5f, .5f), new Vector2(10, 0), new Vector2(380, 40), 16, TextAnchor.MiddleCenter, Color.white);
            purse = Ui.Text(header.transform, "Purse", new Vector2(1, .5f), new Vector2(-14, 0), new Vector2(200, 40), 16, TextAnchor.MiddleRight, new Color(1f, .93f, .75f));
            purse.rectTransform.pivot = new Vector2(1, .5f);
            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Cols; c++)
                {
                    var pos = new Vector2(-252 + c * 252, 96 - r * 162);
                    var bg = Ui.Image(root, "Tile", new Vector2(.5f, .5f), pos, new Vector2(240, 150), new Color(.12f, .16f, .28f, .95f));
                    // Selection: a gold bar along the bottom of the card
                    var border = Ui.Image(bg.transform, "Border", new Vector2(.5f, 0), new Vector2(0, 2), new Vector2(240, 4), new Color(1, .85f, .3f, 0));
                    // Picture on the left (Ui.Image pivots at its anchor: x is the left edge), words on the right
                    var icon = Ui.Image(bg.transform, "Icon", new Vector2(0, .5f), new Vector2(6, 6), new Vector2(100, 100), Color.white);
                    icon.preserveAspect = true;
                    var name = Ui.Text(bg.transform, "Name", new Vector2(0, 1), new Vector2(112, -10), new Vector2(122, 42), 15, TextAnchor.UpperLeft, Color.white);
                    var info = Ui.Text(bg.transform, "Info", new Vector2(0, 1), new Vector2(112, -54), new Vector2(122, 56), 11, TextAnchor.UpperLeft, new Color(.75f, .82f, .95f));
                    var price = Ui.Text(bg.transform, "Price", new Vector2(1, 0), new Vector2(-10, 10), new Vector2(150, 22), 14, TextAnchor.LowerRight, new Color(1, .85f, .4f));
                    tiles.Add((bg, icon, name, info, price, border));
                }
            detail = Ui.Text(root, "Detail", new Vector2(.5f, 0), new Vector2(0, 40), new Vector2(740, 44), 14, TextAnchor.MiddleCenter, new Color(.85f, .9f, 1));
            help = Ui.Text(root, "Help", new Vector2(.5f, 0), new Vector2(0, 12), new Vector2(740, 20), 12, TextAnchor.MiddleCenter, new Color(.6f, .65f, .8f));
            root.gameObject.SetActive(false);
        }

        public void Hide() => root.gameObject.SetActive(false);

        public void Show(Menu m, Player p, GameManager gm)
        {
            root.gameObject.SetActive(true);
            title.text = m.Title;
            if (m.Tabs != null)
            {
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < m.Tabs.Length; i++)
                    sb.Append(i == m.Tab ? $"<color=#fde68a><b>[ {m.Tabs[i]} ]</b></color>  " : $"<color=#94a3b8>{m.Tabs[i]}</color>  ");
                tabs.text = sb.ToString();
            }
            else tabs.text = "";
            purse.text = $"<color=#fde68a>{gm.SandDollars} SD</color>   <color=#fcd34d>◉ {gm.Pearls} pearls</color>";
            int row = Mathf.Max(0, m.Selected) / Cols;
            if (row < scroll) scroll = row;
            if (row >= scroll + Rows) scroll = row - Rows + 1;
            for (int i = 0; i < tiles.Count; i++)
            {
                int index = scroll * Cols + i;
                var t = tiles[i];
                bool on = index < m.Items.Count;
                t.bg.gameObject.SetActive(on);
                if (!on) continue;
                var it = m.Items[index];
                bool sel = index == m.Selected;
                bool blocked = it.Blocked?.Invoke() != null;
                t.border.color = sel ? new Color(1, .85f, .3f, 1) : new Color(1, .85f, .3f, 0);
                t.bg.color = it.Equipped ? new Color(.12f, .28f, .2f, .95f) : sel ? new Color(.18f, .23f, .4f, .97f) : new Color(.12f, .16f, .28f, .95f);
                var sprite = Icons.For(it.Icon);
                t.icon.sprite = sprite;
                t.icon.enabled = sprite != null;
                t.icon.color = it.Tint.a > 0 ? Color.Lerp(Color.white, it.Tint, .35f) : Color.white;
                t.name.text = $"<b>{it.Label}</b>";
                t.info.text = it.Info ?? "";
                t.price.text = it.Equipped ? "<color=#86efac>Equipped</color>" : blocked && !it.Owned ? $"<color=#94a3b8>{it.Right}</color>" : it.Right;
            }
            var cur = m.Current;
            string why = cur?.Blocked?.Invoke();
            detail.text = cur == null ? "" : cur.Detail + (why != null && !cur.Equipped ? $"\n<color=#fca5a5>{why}</color>" : "");
            help.text = $"{(p.UsesMouse ? "Arrows / wheel" : "D-pad")} choose · {p.Glyph(Btn.Interact)} buy · " +
                        (m.Tabs != null ? $"{p.Glyph(Btn.Swap)} / {p.Glyph(Btn.Reload)} tabs · " : "") + $"{p.Glyph(Btn.Back)} close · you can keep walking";
        }
    }

    public class CardsView
    {
        readonly RectTransform root;
        readonly Text title, help;
        readonly List<(RectTransform rt, Image bg, Image band, Text name, Text info, Text desc, Text votes, Text big)> cards = new();

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
}

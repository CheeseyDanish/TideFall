// TideWall 3D — everything drawn around a player's reticle: the reload minigame widget (under it), the ammo hint when
// you're running low, the Perfect-reload tag, and the kill combo counter (to the right). Follows the reticle: the screen
// centre in first person, the aim point from above. Draws from Player state only, so it works the same online.
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TideWall
{
    public class ReticleHud
    {
        const float W = 130, H = 9;
        readonly RectTransform root;
        // Reload widget
        readonly RectTransform widget;
        readonly Image barBg, zone1, zone2, fill, danger, marker, timer, beatDot, beatRing;
        readonly List<Image> pips = new();
        readonly TMP_Text caption, word, ammo;
        // Combo
        readonly RectTransform comboRoot;
        readonly TMP_Text comboCount, comboLabel;
        readonly Image comboBar;
        int lastCombo;
        float comboPop;

        public ReticleHud(Transform canvas)
        {
            root = Ui.Rect(canvas, "Reticle", new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(10, 10));

            widget = Ui.Rect(root, "Reload", new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, -48), new Vector2(W, 40));
            barBg = Ui.Image(widget, "Bar", new Vector2(.5f, .5f), new Vector2(0, 8), new Vector2(W + 4, H + 4), new Color(0, 0, 0, .7f));
            var bar = barBg.rectTransform;
            zone1 = Seg(bar, "Zone1", new Color(.53f, .94f, .67f, .9f));
            zone2 = Seg(bar, "Zone2", new Color(.53f, .94f, .67f, .4f));
            danger = Seg(bar, "Danger", new Color(.94f, .27f, .27f, .85f));
            fill = Seg(bar, "Fill", new Color(.98f, .57f, .24f, .55f));
            marker = Ui.Image(bar, "Marker", new Vector2(0, .5f), Vector2.zero, new Vector2(3, H + 8), Color.white);
            timer = Ui.Image(bar, "Timer", new Vector2(0, 0), new Vector2(2, -6), new Vector2(0, 2), new Color(.58f, .64f, .72f, .8f));
            beatRing = Ui.Image(widget, "BeatRing", new Vector2(.5f, .5f), new Vector2(0, 8), new Vector2(30, 30), new Color(.99f, .88f, .28f, .8f));
            beatRing.sprite = Ui.Ring;
            beatDot = Ui.Image(widget, "Beat", new Vector2(.5f, .5f), new Vector2(0, 8), new Vector2(12, 12), new Color(.99f, .88f, .28f, 1));
            beatDot.sprite = Ui.Dot;
            for (int i = 0; i < 12; i++)
            {
                var pip = Ui.Image(widget, "Pip", new Vector2(.5f, .5f), Vector2.zero, new Vector2(8, 8), Color.white);
                pip.sprite = Ui.Dot;
                pips.Add(pip);
            }
            caption = Ui.Text(widget, "Caption", new Vector2(.5f, .5f), new Vector2(0, -28), new Vector2(320, 18), 12, TextAnchor.MiddleCenter, new Color(.9f, .91f, .92f, .85f));
            word = Ui.Text(root, "Word", new Vector2(.5f, .5f), new Vector2(0, -26), new Vector2(300, 26), 18, TextAnchor.MiddleCenter, Color.white);
            ammo = Ui.Text(root, "Ammo", new Vector2(.5f, .5f), new Vector2(0, -34), new Vector2(300, 20), 13, TextAnchor.MiddleCenter, Color.white);

            comboRoot = Ui.Rect(root, "Combo", new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, .5f), new Vector2(46, 18), new Vector2(150, 50));
            comboCount = Ui.Text(comboRoot, "Count", new Vector2(0, .5f), new Vector2(0, 6), new Vector2(150, 34), 28, TextAnchor.MiddleLeft, new Color(1, .85f, .3f));
            comboCount.rectTransform.pivot = new Vector2(0, .5f);
            comboLabel = Ui.Text(comboRoot, "Label", new Vector2(0, .5f), new Vector2(2, -16), new Vector2(150, 16), 11, TextAnchor.MiddleLeft, new Color(1, .93f, .75f));
            comboLabel.rectTransform.pivot = new Vector2(0, .5f);
            comboBar = Ui.Image(comboRoot, "Bar", new Vector2(0, .5f), new Vector2(2, -26), new Vector2(60, 3), new Color(1, .85f, .3f, .9f));
        }

        static Image Seg(RectTransform bar, string name, Color c)
        {
            // Inside the bar's 2px border; Ui.Image pivots at its anchor, so x is the left edge
            return Ui.Image(bar, name, new Vector2(0, .5f), new Vector2(2, 0), new Vector2(0, H), c);
        }

        static void Span(Image img, float lo, float hi)
        {
            img.enabled = hi > lo;
            img.rectTransform.anchoredPosition = new Vector2(2 + W * lo, img.rectTransform.anchoredPosition.y);
            img.rectTransform.sizeDelta = new Vector2(W * Mathf.Max(0, hi - lo), img.rectTransform.sizeDelta.y);
        }

        public void Update(Player p, GameManager gm, Vector2 reticle, bool show, float dt)
        {
            root.gameObject.SetActive(show);
            if (!show) return;
            root.anchoredPosition = reticle;
            DrawReload(p, gm);
            DrawCombo(p, gm, dt);
        }

        void DrawReload(Player p, GameManager gm)
        {
            var r = p.ReloadView;
            bool running = r != null && !r.Done;
            widget.gameObject.SetActive(running);

            // The result ("PERFECT!", "JAMMED") pops over the bar
            float wt = r != null ? Mathf.Clamp01(r.WordT / .9f) : 0;
            word.text = r != null && wt > 0 ? $"<b>{r.Word}</b>" : "";
            word.color = r != null && r.WordGood ? new Color(1, .85f, .3f, wt) : new Color(.97f, .45f, .45f, wt);
            word.rectTransform.anchoredPosition = new Vector2(0, -26 + (1 - wt) * 10);

            // Not reloading: a hint when you're low, and the Perfect bonus while it lasts
            if (!running)
            {
                string hint = "";
                if (!p.GunsLocked && p.CarryCount == 0 && p.ShownMenu == null)
                {
                    if (p.Ammo <= 0) hint = $"<color=#fca5a5><b>RELOAD</b> {p.Glyph(Btn.Reload)}</color>";
                    else if (p.Ammo <= Mathf.Max(1, p.MagSize / 4)) hint = $"<color=#fde68a>{p.Ammo}/{p.MagSize}</color>";
                    if (p.PerfectBonus > 0) hint += (hint.Length > 0 ? "  " : "") + $"<color=#fcd34d>PERFECT +{p.PerfectBonus * 100:0}%</color>";
                }
                ammo.text = wt > 0 ? "" : hint;
                return;
            }
            ammo.text = "";

            var R = r.R;
            string btn = p.Glyph(Btn.Reload);
            caption.text = $"<b>{R.displayName.ToUpper()}</b> · {R.how.Replace("{r}", btn)}";
            zone1.color = r.Style == ReloadStyle.Heavy ? new Color(.98f, .8f, .08f, .9f) : new Color(.53f, .94f, .67f, .9f);
            marker.color = r.Jammed ? new Color(.94f, .27f, .27f) : Color.white;
            barBg.enabled = true;
            marker.enabled = true;
            Span(zone2, 0, 0); Span(danger, 0, 0); Span(fill, 0, 0);
            timer.enabled = false;
            beatDot.enabled = beatRing.enabled = false;
            float mk = r.Marker;

            switch (r.Style)
            {
                case ReloadStyle.Zone:
                case ReloadStyle.Drop:
                case ReloadStyle.Heavy:
                    if (r.Tried) Span(zone1, 0, 0); else Span(zone1, r.Zone.x, r.Zone.y);
                    break;
                case ReloadStyle.Bolt:
                    if (r.Tried) { Span(zone1, 0, 0); break; }
                    if (r.Step == 0) { Span(zone1, r.Zone.x, r.Zone.y); Span(zone2, r.Zone2.x, r.Zone2.y); }
                    else Span(zone1, r.Zone2.x, r.Zone2.y);
                    caption.text = $"<b>BOLT · {(r.Step == 0 ? "OPEN" : "CLOSE")}</b> · {R.how.Replace("{r}", btn)}";
                    break;
                case ReloadStyle.Mash:
                    Span(zone1, 0, 0);
                    fill.color = new Color(.05f, .65f, .91f, .9f);
                    Span(fill, 0, Mathf.Clamp01(r.T / r.Need));
                    marker.enabled = false;
                    caption.color = Mathf.Sin(Time.unscaledTime * 16) > 0 ? new Color(.65f, .95f, .99f) : new Color(.88f, .95f, 1);
                    break;
                case ReloadStyle.Pressure:
                    Span(danger, .94f, 1);
                    if (r.Tried) Span(zone1, 0, 0); else Span(zone1, r.Zone.x, r.Zone.y);
                    fill.color = new Color(.98f, .57f, .24f, .55f);
                    Span(fill, 0, r.Needle);
                    mk = r.Needle;
                    // thin line underneath: the plain reload timer
                    timer.enabled = true;
                    timer.rectTransform.sizeDelta = new Vector2(W * Mathf.Clamp01(r.T / r.Need), 2);
                    if (r.Tried) caption.text = "<b>VENTED</b> · wait it out";
                    break;
                case ReloadStyle.Shells:
                    Span(zone1, r.Zone.x, r.Zone.y);
                    mk = r.RoundFraction;
                    break;
                case ReloadStyle.Rhythm:
                    // An approach ring closing on the beat light instead of a bar
                    barBg.enabled = false;
                    marker.enabled = false;
                    Span(zone1, 0, 0);
                    beatDot.enabled = beatRing.enabled = true;
                    bool fumbling = r.Fumble > 0;
                    beatRing.rectTransform.sizeDelta = Vector2.one * (12 + 26 * r.ToNextBeat);
                    beatRing.color = fumbling ? new Color(.97f, .44f, .44f, .8f) : new Color(.99f, .88f, .28f, .8f);
                    beatDot.color = fumbling ? new Color(.94f, .27f, .27f) : new Color(.99f, .88f, .28f, .25f + .75f * Mathf.Pow(Mathf.Max(0, r.NearBeat), 3));
                    break;
            }
            if (r.Style != ReloadStyle.Mash) caption.color = new Color(.9f, .91f, .92f, .85f);
            if (marker.enabled) marker.rectTransform.anchoredPosition = new Vector2(2 + W * Mathf.Clamp01(mk) - 1.5f, 0);

            // Rounds in the magazine for one-at-a-time reloads
            int n = r.PerRound ? Mathf.Min(p.MagSize, pips.Count) : 0;
            float gap = n > 0 ? Mathf.Min(13, 110f / n) : 0, x0 = -(n - 1) * gap / 2;
            for (int i = 0; i < pips.Count; i++)
            {
                bool on = i < n;
                pips[i].enabled = on;
                if (!on) continue;
                pips[i].rectTransform.anchoredPosition = new Vector2(x0 + i * gap, -10);
                pips[i].color = i < p.Ammo ? new Color(.98f, .75f, .14f) : new Color(.9f, .91f, .92f, .3f);
            }
            caption.rectTransform.anchoredPosition = new Vector2(0, r.PerRound ? -26 : -12);
        }

        void DrawCombo(Player p, GameManager gm, float dt)
        {
            int c = p.Combo;
            if (c > lastCombo && c >= 2) { comboPop = 1; }
            lastCombo = c;
            comboPop = Mathf.MoveTowards(comboPop, 0, dt * 4);
            bool on = c >= 2 && p.ComboLeft > 0;
            comboRoot.gameObject.SetActive(on);
            if (!on) return;
            float left = Mathf.Clamp01(p.ComboLeft / Mathf.Max(.1f, gm.DB.comboWindow));
            var hot = Color.Lerp(new Color(1, .85f, .3f), new Color(1, .35f, .2f), Mathf.Clamp01((c - 2) / 10f));
            comboCount.text = $"<b>x{c}</b>";
            comboCount.color = new Color(hot.r, hot.g, hot.b, .4f + .6f * left);
            comboCount.rectTransform.localScale = Vector3.one * (1 + comboPop * .5f);
            int step = Mathf.Min(c, gm.DB.comboMax) - 1;
            comboLabel.text = c >= 10 ? "UNSTOPPABLE" : c >= 6 ? "RAMPAGE" : c >= 4 ? "FRENZY" : "COMBO";
            comboLabel.text += gm.DB.comboSandDollars > 0 ? $"  +{step * gm.DB.comboSandDollars} SD" : "";
            comboBar.rectTransform.sizeDelta = new Vector2(60 * left, 3);
            comboBar.color = hot;
        }
    }
}

// TideWall 3D — one player's HUD, drawn by their camera so it sits inside their split-screen viewport.
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace TideWall
{
    public class PlayerHud : MonoBehaviour
    {
        Player p;
        GameManager gm;
        Image hpFill, wallFill, reloadFill, hurt, tunnel, hitMarker, menuPanel;
        Text nameText, hpText, coinText, phaseText, wallText, gunText, promptText, bannerTitle, bannerSub, statusText, toastText, menuTitle, menuBody, menuDetail, centerText;
        readonly List<Image> inkBlots = new();
        float bannerT, toastT, hitT;
        Sprite blob, hole;

        public static PlayerHud Create(Player player)
        {
            var canvas = Ui.CanvasFor(player.Cam.Camera, $"HUD P{player.Index + 1}", player.Index);
            var hud = canvas.gameObject.AddComponent<PlayerHud>();
            hud.p = player; hud.gm = GameManager.I;
            hud.Build(canvas.transform);
            hud.gm.OnBanner += hud.ShowBanner;
            return hud;
        }

        void OnDestroy() { if (gm != null) gm.OnBanner -= ShowBanner; }

        void Build(Transform c)
        {
            var col = p.Color;
            blob = Ui.Radial(false); hole = Ui.Radial(true);
            // Overlays first (drawn underneath the HUD text)
            hurt = Ui.Fill(c, "Hurt", new Color(.8f, 0, 0, 0));
            tunnel = Ui.Fill(c, "Tunnel", Color.clear);
            tunnel.sprite = hole; tunnel.color = new Color(0, 0, 0, 0);
            for (int i = 0; i < 7; i++)
            {
                var b = Ui.Image(c, "Ink", new Vector2(.5f, .5f), Vector2.zero, new Vector2(300, 300), new Color(.06f, .02f, .08f, 0));
                b.sprite = blob; inkBlots.Add(b);
            }

            // Top-left: who you are and your health
            var tl = new Vector2(0, 1);
            Ui.Image(c, "Swatch", tl, new Vector2(18, -18), new Vector2(14, 14), col).rectTransform.pivot = tl;
            nameText = Ui.Text(c, "Name", tl, new Vector2(40, -12), new Vector2(300, 24), 18, TextAnchor.UpperLeft, Color.white);
            nameText.rectTransform.pivot = tl;
            hpFill = Ui.Bar(c, "HP", tl, new Vector2(18, -38), new Vector2(240, 16), new Color(.35f, .85f, .4f));
            hpFill.transform.parent.GetComponent<RectTransform>().pivot = tl;
            hpText = Ui.Text(c, "HPText", tl, new Vector2(266, -36), new Vector2(120, 22), 14, TextAnchor.UpperLeft, Color.white);
            hpText.rectTransform.pivot = tl;
            statusText = Ui.Text(c, "Status", tl, new Vector2(18, -60), new Vector2(420, 60), 14, TextAnchor.UpperLeft, Color.white);
            statusText.rectTransform.pivot = tl;

            // Top-centre: phase, waves, wall
            var tc = new Vector2(.5f, 1);
            phaseText = Ui.Text(c, "Phase", tc, new Vector2(0, -10), new Vector2(560, 26), 19, TextAnchor.UpperCenter, Color.white);
            phaseText.rectTransform.pivot = tc;
            wallFill = Ui.Bar(c, "Wall", tc, new Vector2(-130, -40), new Vector2(260, 12), new Color(.75f, .72f, .65f));
            wallFill.transform.parent.GetComponent<RectTransform>().pivot = tc;
            wallFill.transform.parent.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -40);
            wallText = Ui.Text(c, "WallText", tc, new Vector2(0, -54), new Vector2(300, 20), 13, TextAnchor.UpperCenter, new Color(.9f, .88f, .8f));
            wallText.rectTransform.pivot = tc;

            // Top-right: the team purse
            var tr = new Vector2(1, 1);
            coinText = Ui.Text(c, "Coins", tr, new Vector2(-18, -12), new Vector2(260, 30), 22, TextAnchor.UpperRight, new Color(1, .85f, .3f));
            coinText.rectTransform.pivot = tr;

            // Bottom-left: weapons
            var bl = new Vector2(0, 0);
            gunText = Ui.Text(c, "Gun", bl, new Vector2(18, 16), new Vector2(460, 70), 16, TextAnchor.LowerLeft, Color.white);
            gunText.rectTransform.pivot = bl;
            reloadFill = Ui.Bar(c, "Reload", bl, new Vector2(18, 90), new Vector2(160, 8), new Color(.6f, .8f, 1));
            reloadFill.transform.parent.GetComponent<RectTransform>().pivot = bl;

            // Bottom-centre: what Interact does here
            promptText = Ui.Text(c, "Prompt", new Vector2(.5f, 0), new Vector2(0, 24), new Vector2(700, 30), 18, TextAnchor.LowerCenter, new Color(1, .95f, .75f));
            promptText.rectTransform.pivot = new Vector2(.5f, 0);

            // Centre: banners, toasts, big state messages
            bannerTitle = Ui.Text(c, "BannerTitle", new Vector2(.5f, .72f), Vector2.zero, new Vector2(1000, 60), 40, TextAnchor.MiddleCenter, Color.white);
            bannerSub = Ui.Text(c, "BannerSub", new Vector2(.5f, .64f), Vector2.zero, new Vector2(900, 40), 18, TextAnchor.MiddleCenter, new Color(.9f, .92f, 1));
            toastText = Ui.Text(c, "Toast", new Vector2(.5f, .33f), Vector2.zero, new Vector2(800, 30), 17, TextAnchor.MiddleCenter, Color.white);
            centerText = Ui.Text(c, "Center", new Vector2(.5f, .5f), Vector2.zero, new Vector2(900, 200), 26, TextAnchor.MiddleCenter, Color.white);
            hitMarker = Ui.Image(c, "Hit", new Vector2(.5f, .5f), Vector2.zero, new Vector2(18, 18), new Color(1, 1, 1, 0));
            hitMarker.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);

            // Menu panel
            menuPanel = Ui.Image(c, "Menu", new Vector2(.5f, .5f), Vector2.zero, new Vector2(620, 470), new Color(.05f, .08f, .15f, .93f));
            menuTitle = Ui.Text(menuPanel.transform, "Title", new Vector2(.5f, 1), new Vector2(0, -22), new Vector2(580, 34), 24, TextAnchor.MiddleCenter, new Color(1, .85f, .4f));
            menuBody = Ui.Text(menuPanel.transform, "Body", new Vector2(.5f, .5f), new Vector2(0, 8), new Vector2(580, 340), 16, TextAnchor.MiddleLeft, Color.white);
            menuDetail = Ui.Text(menuPanel.transform, "Detail", new Vector2(.5f, 0), new Vector2(0, 44), new Vector2(580, 50), 14, TextAnchor.MiddleCenter, new Color(.8f, .85f, 1));
            Ui.Text(menuPanel.transform, "Help", new Vector2(.5f, 0), new Vector2(0, 14), new Vector2(580, 22), 13, TextAnchor.MiddleCenter, new Color(.6f, .65f, .8f))
                .text = p.Controls.UsesMouse ? "W/S choose · E or click buy · Tab/Esc close" : "Stick choose · [A] buy · [B] close";
        }

        public void ShowBanner(string title, string sub) { bannerTitle.text = title; bannerSub.text = sub; bannerT = 3.2f; }

        public void Toast(string msg, Color color) { toastText.text = msg; toastText.color = color; toastT = 2.6f; }

        public void HitMarker(bool crit) { hitT = .15f; hitMarker.color = crit ? new Color(1, .85f, .2f, 1) : new Color(1, 1, 1, .9f); }

        void Update()
        {
            if (p == null) { Destroy(gameObject); return; }
            float dt = Time.unscaledDeltaTime;
            var phases = gm.Phases;
            var ph = phases.Current;

            nameText.text = $"<b>{p.Name}</b>  <size=12><color=#cbd5e1>{p.Controls.DeviceName}</color></size>";
            hpFill.fillAmount = Mathf.Clamp01(p.Hp / p.MaxHp);
            hpFill.color = p.Hp / p.MaxHp > .5f ? new Color(.35f, .85f, .4f) : p.Hp / p.MaxHp > .25f ? new Color(.95f, .8f, .2f) : new Color(.95f, .3f, .25f);
            hpText.text = $"{Mathf.CeilToInt(p.Hp)}/{Mathf.RoundToInt(p.MaxHp)}";
            var sb = new StringBuilder();
            foreach (var kv in p.Statuses.Active)
            {
                int mood = StatusEffects.Mood(kv.Key);
                sb.Append($"<color={(mood > 0 ? "#86efac" : mood < 0 ? "#fca5a5" : "#d4d4d8")}>{StatusEffects.Label(kv.Key)} {kv.Value:0}s</color>  ");
            }
            if (p.Wet > .2f) sb.Append("<color=#93c5fd>WET</color>");
            statusText.text = sb.ToString();
            coinText.text = $"{gm.Coins} coins";

            if (ph != null && gm.State != GameState.Lobby)
            {
                string timing = phases.TimeLeft >= 0 ? $" · {Mathf.CeilToInt(phases.TimeLeft)}s" : "";
                string waves = gm.Waves.WaveCount > 0 ? $" · Wave {Mathf.Min(gm.Waves.Wave + 1, gm.Waves.WaveCount)}/{gm.Waves.WaveCount} · {GameManager.Enemies.Count} left" : "";
                phaseText.text = $"<b>Day {phases.Day} · {ph.displayName}</b>{timing}{waves}";
            }
            wallFill.fillAmount = gm.Wall.Hp / gm.Wall.MaxHp;
            wallText.text = $"Wall {Mathf.CeilToInt(gm.Wall.Hp)}/{Mathf.RoundToInt(gm.Wall.MaxHp)}";

            gunText.text = $"<b>{p.Gun.displayName}</b>  {(p.ReloadProgress >= 0 ? "reloading…" : $"{p.Ammo}/{p.MagSize}")}\n" +
                           $"<color=#cbd5e1>{p.Melee.displayName}</color>" +
                           (p.Carrying.Count > 0 ? $"\n<color=#fde68a>Carrying {p.Carrying.Count} (weight {p.CarryWeight:0}/{Player.MaxCarry:0}) · Fire to throw</color>" : "");
            reloadFill.transform.parent.gameObject.SetActive(p.ReloadProgress >= 0);
            if (p.ReloadProgress >= 0) reloadFill.fillAmount = p.ReloadProgress;

            promptText.text = Prompt();

            bannerT -= dt; toastT -= dt; hitT -= dt;
            SetAlpha(bannerTitle, Mathf.Clamp01(bannerT)); SetAlpha(bannerSub, Mathf.Clamp01(bannerT));
            SetAlpha(toastText, Mathf.Clamp01(toastT));
            var hc = hitMarker.color; hc.a = Mathf.Clamp01(hitT / .15f); hitMarker.color = hc;
            if (hitT > 0 && p.Cam.Camera != null)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, p.AimScreen, p.Cam.Camera, out var local);
                hitMarker.rectTransform.anchoredPosition = local;
            }

            hurt.color = new Color(.8f, 0, 0, Mathf.Clamp01(p.HurtFlash) * .6f + (p.IsDowned ? .25f : 0));
            tunnel.color = new Color(0, 0, 0, p.Statuses.Has(StatusType.Tunnel) ? .97f : 0);
            UpdateInk();
            UpdateMenu();
            centerText.text = CenterMessage();
        }

        string Prompt()
        {
            if (p.IsDowned || p.IsOut || p.OpenedMenu != null || gm.State != GameState.Playing) return "";
            string btn = p.Controls.UsesMouse ? "[E]" : "[A]";
            foreach (var other in gm.Players.All)
                if (other != p && other.IsDowned && Vector3.Distance(other.transform.position, p.transform.position) < 1.9f)
                    return $"Hold {btn} to revive {other.Name} ({other.ReviveProgress * 100:0}%)";
            if (p.Focus != null) return $"{btn} {p.Focus.Prompt(p)}";
            if (p.Carrying.Count > 0) return $"{btn} Drop";
            var ph = gm.Phases.Current;
            if (ph != null && ph.endWhenAllReady) return $"Ring the bell when you're ready · {gm.Players.ReadyCount}/{gm.Players.Count} ready";
            return "";
        }

        string CenterMessage()
        {
            if (gm.State == GameState.GameOver)
                return $"<size=44><b>THE WALL HAS FALLEN</b></size>\n{gm.GameOverReason}\nYou held out until day {gm.Phases.Day}.\n\n<size=20>Press {(p.Controls.UsesMouse ? "E" : "[A]")} to try again</size>";
            if (gm.Paused) return $"<size=40><b>PAUSED</b></size>\n<size=18>{(p.Controls.UsesMouse ? "Esc" : "Start")} to resume</size>";
            if (!p.Controls.Connected) return "<b>Controller disconnected</b>\nReconnect it to keep playing";
            if (p.IsOut) return "<b>You bled out</b>\nYou'll be back next phase";
            if (p.IsDowned) return $"<b>You're down!</b>\nA teammate can hold Interact to revive you\n<size=20>{p.BleedLeft:0}s</size>";
            return "";
        }

        void UpdateInk()
        {
            float ink = p.Statuses.Remaining(StatusType.InkVision);
            for (int i = 0; i < inkBlots.Count; i++)
            {
                var b = inkBlots[i];
                if (ink > 0 && b.color.a < .05f)
                {
                    b.rectTransform.anchoredPosition = new Vector2(Random.Range(-420f, 420f), Random.Range(-220f, 220f));
                    b.rectTransform.sizeDelta = Vector2.one * Random.Range(220f, 420f);
                }
                b.color = new Color(.06f, .02f, .08f, Mathf.Clamp01(ink) * .93f);
            }
        }

        void UpdateMenu()
        {
            var m = p.OpenedMenu;
            menuPanel.gameObject.SetActive(m != null);
            if (m == null) return;
            menuTitle.text = $"{m.Title}   <size=16><color=#fde68a>{gm.Coins} coins</color></size>";
            var sb = new StringBuilder();
            int first = Mathf.Clamp(m.Selected - 6, 0, Mathf.Max(0, m.Items.Count - 14));
            for (int i = first; i < Mathf.Min(m.Items.Count, first + 14); i++)
            {
                var it = m.Items[i];
                if (it.Header) { sb.Append($"<color=#93c5fd><b>{it.Label}</b></color>\n"); continue; }
                bool sel = i == m.Selected;
                bool blocked = it.Blocked?.Invoke() != null;
                string label = it.Label.PadRight(34).Substring(0, 34);
                string colour = blocked ? "#94a3b8" : sel ? "#fde68a" : "#ffffff";
                sb.Append($"<color={colour}>{(sel ? "► " : "   ")}{label}  {it.Right}</color>\n");
            }
            menuBody.text = sb.ToString();
            menuDetail.text = m.Selected >= 0 && m.Selected < m.Items.Count ? m.Items[m.Selected].Detail : "";
        }

        static void SetAlpha(Graphic g, float a) { var c = g.color; c.a = a; g.color = c; }
    }
}

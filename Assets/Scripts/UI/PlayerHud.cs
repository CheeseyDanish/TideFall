// TideWall 3D — one player's HUD, drawn by their camera so it sits inside their split-screen viewport:
// health, level/XP, the powerplant and wall, phase, sand dollars, tidal cards, weapons, prompts (with the
// player's own key glyphs), crosshair (first person), menus.
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TideWall
{
    public class PlayerHud : MonoBehaviour
    {
        Player p;
        GameManager gm;
        Image hpFill, wallFill, hurt, tunnel, hitMarker, menuPanel, crossH, crossV, xpFill, coinIcon;
        TMP_Text nameText, hpText, coinText, phaseText, wallText, gunText, promptText, bannerTitle, bannerSub, statusText, toastText, menuTitle, menuBody, menuDetail, menuHelp, centerText, fpsText, levelText, cardsText, voteText, previewText;
        readonly List<Image> inkBlots = new();
        GridView grid;
        CardsView cardsView;
        TableView table;
        Image killMarker;
        ReticleHud reticle;
        float statusT;
        HudLine nameLine, hpLine, statusLine, coinLine, levelLine, cardsLine, voteLine, phaseLine, gunLine;

        /// <summary>Remembers what a line of text last showed (as a hash of its inputs), so it's rebuilt only on change.</summary>
        struct HudLine
        {
            int key; bool set;
            public bool Changed(int k) { if (set && k == key) return false; key = k; set = true; return true; }
        }
        float killT;
        float bannerT, toastT, hitT, fpsT, fpsAcc; int fpsFrames;
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
            xpFill = Ui.Bar(c, "XP", tl, new Vector2(18, -58), new Vector2(240, 6), new Color(1f, .82f, .3f));
            xpFill.transform.parent.GetComponent<RectTransform>().pivot = tl;
            levelText = Ui.Text(c, "Level", tl, new Vector2(266, -53), new Vector2(220, 18), 13, TextAnchor.UpperLeft, new Color(1f, .9f, .55f));
            levelText.rectTransform.pivot = tl;
            statusText = Ui.Text(c, "Status", tl, new Vector2(18, -72), new Vector2(420, 60), 14, TextAnchor.UpperLeft, Color.white);
            statusText.rectTransform.pivot = tl;

            // Top-centre: phase, waves, wall
            var tc = new Vector2(.5f, 1);
            phaseText = Ui.Text(c, "Phase", tc, new Vector2(0, -10), new Vector2(560, 26), 19, TextAnchor.UpperCenter, Color.white);
            phaseText.rectTransform.pivot = tc;
            wallFill = Ui.Bar(c, "Plant", tc, new Vector2(-130, -40), new Vector2(260, 12), new Color(.4f, .95f, .45f));
            wallFill.transform.parent.GetComponent<RectTransform>().pivot = tc;
            wallFill.transform.parent.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -40);
            wallText = Ui.Text(c, "WallText", tc, new Vector2(0, -54), new Vector2(300, 20), 13, TextAnchor.UpperCenter, new Color(.9f, .88f, .8f));
            wallText.rectTransform.pivot = tc;

            // Top-right: the team purse
            var tr = new Vector2(1, 1);
            coinText = Ui.Text(c, "SandDollars", tr, new Vector2(-18, -12), new Vector2(200, 30), 22, TextAnchor.UpperRight, new Color(1f, .93f, .75f));
            coinText.rectTransform.pivot = tr;
            coinIcon = Ui.Image(c, "SandDollarIcon", tr, new Vector2(-18, -12), new Vector2(26, 26), Color.white);
            coinIcon.sprite = Ui.SandDollarIcon; coinIcon.rectTransform.pivot = tr;
            cardsText = Ui.Text(c, "Cards", tr, new Vector2(-18, -44), new Vector2(300, 120), 13, TextAnchor.UpperRight, Color.white);
            cardsText.rectTransform.pivot = tr;
            voteText = Ui.Text(c, "Vote", new Vector2(.5f, 1), new Vector2(0, -74), new Vector2(700, 24), 16, TextAnchor.UpperCenter, new Color(.6f, .95f, 1f));
            voteText.rectTransform.pivot = new Vector2(.5f, 1);
            previewText = Ui.Text(c, "Preview", new Vector2(.5f, 1), new Vector2(0, -96), new Vector2(900, 22), 13, TextAnchor.UpperCenter, new Color(.85f, .9f, 1f, .9f));
            previewText.rectTransform.pivot = new Vector2(.5f, 1);

            // Bottom-left: weapons
            var bl = new Vector2(0, 0);
            gunText = Ui.Text(c, "Gun", bl, new Vector2(18, 16), new Vector2(460, 70), 16, TextAnchor.LowerLeft, Color.white);
            gunText.rectTransform.pivot = bl;

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
            // First-person crosshair: a small plus in the player's colour
            crossH = Ui.Image(c, "CrossH", new Vector2(.5f, .5f), Vector2.zero, new Vector2(14, 2), Color.Lerp(col, Color.white, .5f));
            crossV = Ui.Image(c, "CrossV", new Vector2(.5f, .5f), Vector2.zero, new Vector2(2, 14), Color.Lerp(col, Color.white, .5f));
            fpsText = Ui.Text(c, "Fps", new Vector2(1, 0), new Vector2(-12, 8), new Vector2(120, 20), 12, TextAnchor.LowerRight, new Color(.8f, .9f, 1));
            fpsText.rectTransform.pivot = new Vector2(1, 0);

            // Menu panel
            // List menus sit to the right so you can see where you're walking
            menuPanel = Ui.Image(c, "Menu", new Vector2(1, .5f), new Vector2(-300, -10), new Vector2(560, 440), new Color(.05f, .08f, .15f, .88f));
            menuTitle = Ui.Text(menuPanel.transform, "Title", new Vector2(.5f, 1), new Vector2(0, -22), new Vector2(520, 34), 22, TextAnchor.MiddleCenter, new Color(1, .85f, .4f));
            menuBody = Ui.Text(menuPanel.transform, "Body", new Vector2(.5f, .5f), new Vector2(0, 8), new Vector2(520, 320), 15, TextAnchor.MiddleLeft, Color.white);
            menuDetail = Ui.Text(menuPanel.transform, "Detail", new Vector2(.5f, 0), new Vector2(0, 44), new Vector2(520, 50), 13, TextAnchor.MiddleCenter, new Color(.8f, .85f, 1));
            menuHelp = Ui.Text(menuPanel.transform, "Help", new Vector2(.5f, 0), new Vector2(0, 14), new Vector2(520, 22), 12, TextAnchor.MiddleCenter, new Color(.6f, .65f, .8f));
            grid = new GridView(c);
            cardsView = new CardsView(c);
            table = new TableView(c);
            reticle = new ReticleHud(c);
            killMarker = Ui.Image(c, "Kill", new Vector2(.5f, .5f), Vector2.zero, new Vector2(30, 30), new Color(1, .25f, .2f, 0));
            killMarker.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
        }

        public void ShowBanner(string title, string sub) { bannerTitle.text = title; bannerSub.text = sub; bannerT = 3.2f; }

        public void Toast(string msg, Color color) { toastText.text = msg; toastText.color = color; toastT = 2.6f; }

        /// <summary>A red X when your hit kills.</summary>
        public void KillMarker() { killT = .35f; }

        public void HitMarker(bool crit) { hitT = .15f; hitMarker.color = crit ? new Color(1, .85f, .2f, 1) : new Color(1, 1, 1, .9f); }

        void Update()
        {
            if (p == null) { Destroy(gameObject); return; }
            float dt = Time.unscaledDeltaTime;
            var phases = gm.Phases;
            var ph = phases.Current;

            // Each line is rebuilt only when what it shows changes (building strings every frame churns memory and
            // makes TextMeshPro regenerate its mesh); bars and colours are cheap and update every frame.
            if (nameLine.Changed(System.HashCode.Combine(p.Name, p.Nick, p.DeviceName)))
                nameText.text = $"<b>{p.Name}</b>{(string.IsNullOrEmpty(p.Nick) ? "" : " · " + p.Nick)}  <size=12><color=#cbd5e1>{p.DeviceName}</color></size>";
            hpFill.fillAmount = Mathf.Clamp01(p.Hp / p.MaxHp);
            hpFill.color = p.Hp / p.MaxHp > .5f ? new Color(.35f, .85f, .4f) : p.Hp / p.MaxHp > .25f ? new Color(.95f, .8f, .2f) : new Color(.95f, .3f, .25f);
            int hp = Mathf.CeilToInt(p.Hp), maxHp = Mathf.RoundToInt(p.MaxHp);
            if (hpLine.Changed(System.HashCode.Combine(hp, maxHp))) hpText.text = $"{hp}/{maxHp}";

            int statusKey = p.Wet > .2f ? 1 : 0;
            foreach (var kv in p.Statuses.Active) statusKey = System.HashCode.Combine(statusKey, kv.Key, Mathf.CeilToInt(kv.Value));
            if (statusLine.Changed(statusKey))
            {
                var sb = new StringBuilder();
                foreach (var kv in p.Statuses.Active)
                {
                    int mood = StatusEffects.Mood(kv.Key);
                    sb.Append($"<color={(mood > 0 ? "#86efac" : mood < 0 ? "#fca5a5" : "#d4d4d8")}>{StatusEffects.Label(kv.Key)} {kv.Value:0}s</color>  ");
                }
                if (p.Wet > .2f) sb.Append("<color=#93c5fd>WET</color>");
                statusText.text = sb.ToString();
            }
            if (coinLine.Changed(gm.SandDollars))
            {
                coinText.text = gm.SandDollars.ToString();
                coinText.ForceMeshUpdate();
                coinIcon.rectTransform.anchoredPosition = new Vector2(-24 - coinText.preferredWidth, -14);   // just left of the number
            }
            xpFill.fillAmount = Mathf.Clamp01(p.Xp / (float)Mathf.Max(1, p.XpNext));
            if (levelLine.Changed(System.HashCode.Combine(p.Level, p.StatPoints, p.PendingMods, p.Glyph(Btn.Stats))))
                levelText.text = $"Lv {p.Level}" + (p.StatPoints > 0 ? $"  <color=#fde68a>+{p.StatPoints} pt {p.Glyph(Btn.Stats)}</color>" : "") +
                                 (p.PendingMods > 0 ? "  <color=#86efac>MOD!</color>" : "");

            int cardsKey = 0;
            foreach (var a in gm.Cards.Actives) cardsKey = System.HashCode.Combine(cardsKey, a.def.id, a.wavesLeft);
            if (cardsLine.Changed(cardsKey))
            {
                var cards = new StringBuilder();
                foreach (var a in gm.Cards.Actives)
                    cards.Append($"<color=#{ColorUtility.ToHtmlStringRGB(Color.Lerp(a.def.color, Color.white, .35f))}>{a.def.displayName}</color> <color=#cbd5e1>{a.wavesLeft} wave{(a.wavesLeft == 1 ? "" : "s")}</color>\n");
                cardsText.text = cards.ToString();
            }
            bool voting = gm.Cards.Draft != null && gm.State == GameState.Playing;
            bool voted = gm.Cards.Votes.ContainsKey(p.Index);
            int draftLeft = Mathf.CeilToInt(gm.Cards.DraftLeft);
            if (voteLine.Changed(System.HashCode.Combine(voting, voted, gm.Cards.Votes.Count, gm.Players.Count, draftLeft, p.Glyph(Btn.Stats))))
                voteText.text = !voting ? "" : voted
                    ? $"Tidal card vote: {gm.Cards.Votes.Count}/{gm.Players.Count} voted · {draftLeft}s"
                    : $"TIDAL CARD VOTE · {draftLeft}s · {p.Glyph(Btn.Stats)} to vote";

            if (gm.Assault && gm.State != GameState.MainMenu)
            {
                var ex = gm.Expedition;
                if (phaseLine.Changed(System.HashCode.Combine(ex.StageIndex, gm.EnemiesLeft)))
                    phaseText.text = $"<b>Part {ex.Part.numeral} · {ex.Stage.title}</b> · {gm.EnemiesLeft} creatures";
            }
            else if (ph != null && gm.State != GameState.MainMenu)
            {
                int timeLeft = phases.TimeLeft >= 0 ? Mathf.CeilToInt(phases.TimeLeft) : -1;
                if (phaseLine.Changed(System.HashCode.Combine(phases.Day, ph.displayName, timeLeft, gm.Waves.Wave, gm.Waves.WaveCount, gm.EnemiesLeft)))
                {
                    string timing = timeLeft >= 0 ? $" · {timeLeft}s" : "";
                    string waves = gm.Waves.WaveCount > 0 ? $" · Wave {Mathf.Min(gm.Waves.Wave + 1, gm.Waves.WaveCount)}/{gm.Waves.WaveCount} · {gm.EnemiesLeft} left" : "";
                    phaseText.text = $"<b>Day {phases.Day} · {ph.displayName}</b>{timing}{waves}";
                }
            }
            // The bar and line under it: Part I the powerplant and the wall, Part II the objective and the tide clock
            float status = gm.Level.StatusFraction();
            wallFill.fillAmount = status;
            wallFill.color = gm.Assault ? Color.Lerp(new Color(1f, .3f, .25f), new Color(.45f, .85f, 1f), Mathf.Clamp01(status * 3))
                           : status > .5f ? new Color(.4f, .95f, .45f) : status > .3f ? new Color(.95f, .8f, .2f) : new Color(1f, .3f, .25f);
            // (Built as words by the level, so it's refreshed a few times a second rather than every frame)
            statusT -= dt;
            if (statusT <= 0) { statusT = .2f; Ui.Set(wallText, gm.Level.StatusLine()); }

            bool reloading = p.ReloadProgress >= 0;
            int perfect = Mathf.RoundToInt(p.PerfectBonus * 100), weight = Mathf.RoundToInt(p.CarryWeight);
            int gunKey = System.HashCode.Combine(p.GunsLocked, p.Gun.id, reloading, p.Ammo, p.MagSize, perfect, p.Melee.id, System.HashCode.Combine(p.CarryCount, weight, p.Glyph(Btn.Fire)));
            if (gunLine.Changed(gunKey))
                gunText.text = (p.GunsLocked ? $"<color=#94a3b8><b>{p.Gun.displayName}</b>  holstered until wave {gm.DB.gunsUnlockWave}</color>\n"
                                             : $"<b>{p.Gun.displayName}</b>  {(reloading ? "reloading…" : $"{p.Ammo}/{p.MagSize}")}" +
                                               (perfect > 0 ? $"  <color=#fcd34d>PERFECT +{perfect}%</color>" : "") + "\n") +
                               $"<color=#cbd5e1>{p.Melee.displayName}</color>" +
                               (p.CarryCount > 0 ? $"\n<color=#fde68a>Carrying {p.CarryCount} (weight {weight}/{Player.MaxCarry:0}) · {p.Glyph(Btn.Fire)} throw</color>" : "");

            Ui.Set(promptText, Prompt());

            bannerT -= dt; toastT -= dt; hitT -= dt;
            SetAlpha(bannerTitle, Mathf.Clamp01(bannerT)); SetAlpha(bannerSub, Mathf.Clamp01(bannerT));
            SetAlpha(toastText, Mathf.Clamp01(toastT));
            var hc = hitMarker.color; hc.a = Mathf.Clamp01(hitT / .15f); hitMarker.color = hc;
            killT -= dt;
            killMarker.color = new Color(1, .25f, .2f, Mathf.Clamp01(killT / .35f));
            killMarker.rectTransform.localScale = Vector3.one * (1 + Mathf.Clamp01(killT / .35f) * .6f);
            // The crosshair spreads as you fire, then settles
            float spread = 14 + p.FireHeat * 16;
            crossH.rectTransform.sizeDelta = new Vector2(spread, 2); crossV.rectTransform.sizeDelta = new Vector2(2, spread);
            bool cardsOpen = p.ShownMenu != null && p.ShownMenu.Layout == MenuLayout.Cards;
            Ui.Set(previewText, cardsOpen ? "" : gm.Waves.Preview);
            if (cardsOpen) { voteText.text = ""; voteLine = default; SetAlpha(bannerTitle, 0); SetAlpha(bannerSub, 0); }   // the cards have the screen
            bool cross = p.FirstPerson && !p.IsOut && p.ShownMenu == null && gm.State == GameState.Playing;
            crossH.enabled = crossV.enabled = cross;
            fpsText.enabled = Settings.Current.showFps;
            if (fpsText.enabled)
            {
                fpsFrames++; fpsAcc += dt; fpsT -= dt;
                if (fpsT <= 0) { fpsText.text = $"{fpsFrames / Mathf.Max(.001f, fpsAcc):0} fps"; fpsT = .5f; fpsFrames = 0; fpsAcc = 0; }
            }
            // The reticle: the screen centre in first person, the aim point from above
            var reticlePos = Vector2.zero;
            if (!p.FirstPerson && p.Cam.Camera != null)
                RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, p.AimScreen, p.Cam.Camera, out reticlePos);
            if (hitT > 0) hitMarker.rectTransform.anchoredPosition = reticlePos;
            killMarker.rectTransform.anchoredPosition = reticlePos;
            reticle.Update(p, gm, reticlePos, !p.IsOut && !p.IsDowned && gm.State == GameState.Playing && (p.ShownMenu == null || p.ShownMenu.Layout == MenuLayout.List), dt);

            hurt.color = new Color(.8f, 0, 0, Mathf.Clamp01(p.HurtFlash) * .6f + (p.IsDowned ? .25f : 0));
            tunnel.color = new Color(0, 0, 0, p.Statuses.Has(StatusType.Tunnel) ? .97f : 0);
            UpdateInk();
            UpdateMenu();
            Ui.Set(centerText, CenterMessage());
        }

        string Prompt()
        {
            if (p.ShownMenu != null || gm.State != GameState.Playing) return "";
            return p.PromptText().Replace("{btn}", p.Glyph(Btn.Interact));
        }

        string CenterMessage()
        {
            if (Rebinder.Listening != null && p.ShownMenu != null)
                return $"<b>Press the new key or button</b>\n{Rebinder.Listening}\n<size=16>Esc cancels</size>";
            if (gm.State == GameState.GameOver)
            {
                string retry = Net.IsClient ? "Waiting for the host to start a new run" : $"{p.Glyph(Btn.Interact)} New run";
                var rec = gm.RunRecord;
                string best = rec != null ? $"\n<size=16>Runs: {rec.runs} · best day {rec.bestDay} · best level {rec.bestLevel}</size>" : "";
                string reached = gm.Assault ? $"The expedition reached {gm.Expedition.Stage.title}" : $"You held out until day {gm.Phases.Day}";
                return $"<size=44><b>RUN OVER</b></size>\n{gm.GameOverReason}\n{reached} (level {p.Level}).{best}\n\n" +
                       $"<size=20>{retry} · {p.Glyph(Btn.Back)} {(Net.IsClient ? "Leave" : "Main menu")}</size>";
            }
            if (Net.IsClient && !Net.Client.Connected) return "<b>Connection lost</b>\nReturning to the main menu…";
            if (!p.Connected) return "<b>Controller disconnected</b>\nReconnect it to keep playing";
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
            var m = p.ShownMenu;
            menuPanel.gameObject.SetActive(m != null && m.Layout == MenuLayout.List);
            if (m != null && m.Layout == MenuLayout.Grid) grid.Show(m, p, gm); else grid.Hide();
            if (m != null && m.Layout == MenuLayout.Cards) cardsView.Show(m, p, gm); else cardsView.Hide();
            if (m != null && m.Layout == MenuLayout.Table) table.Show(m, p, gm); else table.Hide();
            if (m == null || m.Layout != MenuLayout.List) return;
            menuTitle.text = m.System ? m.Title : $"{m.Title}   <size=16><color=#fde68a>{gm.SandDollars} sand dollars</color></size>";
            menuBody.text = Render(m, 14);
            var cur = m.Current;
            string why = cur?.Blocked?.Invoke();
            menuDetail.text = cur == null ? "" : m.System && why != null ? $"<color=#fca5a5>{why}</color>" : cur.Detail;
            string nav = p.UsesMouse ? "Arrows / wheel" : "D-pad";
            string adj = cur?.OnAdjust != null ? " · left/right change" : "";
            menuHelp.text = $"{nav} choose{adj} · {p.Glyph(Btn.Interact)} pick · {p.Glyph(Btn.Back)} {(m.Parent != null ? "back" : "close")}";
        }

        /// <summary>A menu as rich text rows (also used by the main menu): headers, the selection, values on the right.</summary>
        public static string Render(Menu m, int rows, int labelWidth = 34)
        {
            var sb = new StringBuilder();
            int first = Mathf.Clamp(m.Selected - rows / 2, 0, Mathf.Max(0, m.Items.Count - rows));
            for (int i = first; i < Mathf.Min(m.Items.Count, first + rows); i++)
            {
                var it = m.Items[i];
                if (it.Header) { sb.Append($"<color=#93c5fd><b>{it.Label}</b></color>\n"); continue; }
                bool sel = i == m.Selected;
                bool blocked = it.Blocked?.Invoke() != null;
                string label = it.Label.Length > labelWidth ? it.Label.Substring(0, labelWidth) : it.Label;
                string colour = blocked ? "#94a3b8" : sel ? "#fde68a" : "#ffffff";
                // <pos> is a TextMeshPro tab stop: the values line up in a column whatever the labels' widths
                sb.Append($"<color={colour}>{(sel ? "► " : "   ")}{label}<pos=58%>{it.Right}</color>\n");
            }
            return sb.ToString();
        }

        static void SetAlpha(Graphic g, float a) { var c = g.color; c.a = a; g.color = c; }
    }
}

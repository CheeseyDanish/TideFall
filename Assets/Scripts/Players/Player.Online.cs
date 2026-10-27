// TideWall 3D — Player: the client's mirror of the host's state for this player (snapshots, menus).
// (One part of the Player class; Player.cs has the overview, the fields and the frame loop.)
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace TideWall
{
    public partial class Player
    {
        // ------------------------------------------------------------------ online mirror (client side)
        /// <summary>Client: the host's view of this player (from a snapshot).</summary>
        public void ApplyNet(NetPlayerState s)
        {
            netPos = s.pos; netYaw = s.yaw; netFirstPerson = s.firstPerson;
            if (Kind == PlayerKind.Proxy) { Pitch = s.pitch; PrefersFirstPerson = s.firstPerson; }
            Hp = s.hp; netMaxHp = s.maxHp;
            IsDowned = s.downed; IsOut = s.isOut; Ready = s.ready;
            BleedLeft = s.bleedLeft; ReviveProgress = s.revive; Wet = s.wet;
            if (s.ammo < netAmmo && Kind == PlayerKind.Proxy) FireHeat = 1;   // they fired (before netAmmo updates)
            netAmmo = s.ammo; netMag = Mathf.Max(1, s.mag); netReload = s.reload;
            netReloadGame = s.reloadGame; netPerfect = s.perfect; netCombo = s.combo; netComboT = s.comboLeft;
            netCarryCount = s.carryCount; netCarryWeight = s.carryWeight;
            if (s.swing > swingT) swingT = s.swing;
            if (Kind == PlayerKind.Proxy) AimPoint = s.aim;
            if (Gun == null || Gun.id != s.gun) GiveGun(s.gun);
            ApplyNetAttachments(s.gun, s.attach);
            if (Melee == null || Melee.id != s.melee) GiveMelee(s.melee);
            Statuses.ClearAll();
            foreach (var (type, left) in s.statuses) Statuses.Add(type, left);
            Kills = s.kills;
            Level = s.level; Xp = s.xp; netXpNext = s.xpNext; StatPoints = s.statPoints; PendingMods = s.pendingMods; netSpeedBonus = s.speedBonus;
        }

        /// <summary>Client: the host's menu for this player (rows only; picking happens on the host).</summary>
        public void ApplyNetMenu(Menu m, string prompt, string building, float buildYaw)
        {
            OpenedMenu = m;
            NetPrompt = prompt;
            Building = string.IsNullOrEmpty(building) ? null : db.Structure(building);
            BuildYaw = buildYaw;
        }

        public void NetHurt() { hurtFlash = .25f; }

        /// <summary>Client: other players glide to where the host says they are.</summary>
        void ProxyUpdate(float dt)
        {
            Statuses.Tick(dt);
            if (hurtFlash > 0) hurtFlash -= dt;
            float k = 1 - Mathf.Exp(-dt * 14);
            cc.enabled = false;
            transform.position = Vector3.Distance(transform.position, netPos) > 6 ? netPos : Vector3.Lerp(transform.position, netPos, k);
            cc.enabled = true;
            swingT -= dt;
            visual.gameObject.SetActive(!IsOut);
            visual.rotation = Quaternion.Slerp(visual.rotation, Quaternion.Euler(0, netYaw, 0), k);
            if (!IsOut) Drip(dt);
            UpdateReticle(!IsDowned && !netFirstPerson && gm.State == GameState.Playing);
            UpdateVisual(dt);
        }

        /// <summary>Client: this machine's own player. Movement and looking are local; the rest mirrors the host.</summary>
        void OwnProxyUpdate(float dt)
        {
            Statuses.Tick(dt);
            if (hurtFlash > 0) hurtFlash -= dt;
            swingT -= dt;
            float udt = Time.unscaledDeltaTime;
            if (!Rebinder.Busy)
            {
                if (Pressed(Btn.Pause)) { if (LocalMenu != null) LocalMenu = null; else OpenMenu(Menus.Pause(this)); }
                if (gm.State == GameState.GameOver && Pressed(Btn.Back) && LocalMenu == null) gm.QuitToMenu();
                if (LocalMenu == null && OpenedMenu == null && Pressed(Btn.View)) SwitchView();
            }
            RenderOffset = Vector3.Lerp(RenderOffset, Vector3.zero, 1 - Mathf.Exp(-dt * 10));
            bool playing = !IsOut && gm.State == GameState.Playing;
            if (LocalMenu != null) { UpdateMenu(udt); PredictMove(dt, playing); UpdateVisual(dt); UpdateReticle(false); return; }
            visual.gameObject.SetActive(!IsOut);
            if (!playing) { PredictMove(dt, false); UpdateReticle(false); return; }
            TickLook(dt);
            ResolveAim(LocalAim());
            PredictMove(dt, true);
            if (IsDowned) UpdateReticle(false);
            else FaceAim(dt);
            UpdateGhost();
            Drip(dt);
            UpdateVisual(dt);
        }

        void OnDestroy()
        {
            Controls?.Dispose();
            if (reticle) Destroy(reticle.gameObject);
            if (Cam) Destroy(Cam.gameObject);
            if (Hud) Destroy(Hud.gameObject);
            if (gm != null && gm.Level != null) gm.Level.lanterns.Remove(lantern);
            foreach (var c in Carrying) if (c) { c.Holder = null; c.transform.SetParent(null, true); SetLayer(c.transform, 0); }
        }
    }
}

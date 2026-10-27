// TideWall 3D — Player: guns and the reload minigames, melee swings, perfect reloads, kill combos.
// (One part of the Player class; Player.cs has the overview, the fields and the frame loop.)
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace TideWall
{
    public partial class Player
    {
        // ------------------------------------------------------------------ combat
        float DamageMult(bool melee)
        {
            float m = melee ? gm.Team.MeleeDamage : gm.Team.GunDamage;
            m *= 1 + StatBonus(melee ? StatKind.MeleeDamage : StatKind.GunDamage);
            m *= gm.Cards.PlayerDamage;
            if (gm.Cards.Has("screenswap") && gm.Cards.SharesScreen(this)) m *= 1.4f;
            if (Statuses.Has(StatusType.Rage)) m *= 1.5f;
            if (Statuses.Has(StatusType.DoubleDamage)) m *= 2;
            if (Statuses.Has(StatusType.Giant)) m *= 1.25f;
            if (Statuses.Has(StatusType.Tiny)) m *= .7f;
            return m;
        }

        /// <summary>Timers that keep running whatever you're doing (reloads finish, swings land, even with a menu open).</summary>
        void CombatTimers(float dt)
        {
            fireCd -= dt; meleeCd -= dt; swingT -= dt; fireBuffer -= dt; lockToastT -= dt;
            FireHeat = Mathf.MoveTowards(FireHeat, 0, dt * 3);
            comboT -= dt;
            if (comboT <= 0) combo = 0;
            lastReload?.Update(dt, ref dummyAmmo, 0);
            if (reload != null)
            {
                int a = Ammo;
                bool done = reload.Update(dt, ref a, MagSize);
                ammo[Gun.id] = a;
                if (done) FinishReload();
            }
            if (strikeT > 0)
            {
                strikeT -= dt;
                if (strikeT <= 0) Strike();
            }
        }

        /// <summary>Start a melee swing; the hit lands when the wind-up ends.</summary>
        void StartSwing()
        {
            meleeCd = EffMelee.cooldown; swingT = Melee.SwingTime; strikeT = Mathf.Max(.01f, Melee.windup);
            Sfx.Play("swing");
        }

        void Strike()
        {
            int hit = Combat.Melee(this, EffMelee, transform.position + Vector3.up * .9f + AimDir * .3f, AimDir, DamageMult(true));
            if (hit > 0) { Sfx.Play("hit"); Shake(.08f + (Melee.style == MeleeStyle.Slam ? .2f : 0)); }
            if (Melee.style == MeleeStyle.Slam) Fx.Debris(transform.position + AimDir * 1.6f, new Color(.9f, .82f, .6f), 10);
        }

        void UpdateCombat(float dt)
        {
            CombatTimers(dt);
            if (grabbedBy != null)
            {
                // Mash to break free
                if (Pressed(Btn.Melee) || Pressed(Btn.Interact) || Pressed(Btn.Fire)) grabbedBy.Struggle();
                return;
            }
            if (!GunsLocked && Pressed(Btn.Swap)) CycleGun();
            if (!GunsLocked && Pressed(Btn.Reload))
            {
                if (reload != null) { int a = Ammo; reload.Press(ref a, MagSize); ammo[Gun.id] = a; if (reload.Done) FinishReload(); }
                else StartReload();
            }
            bool rHeld = Held(Btn.Reload);
            if (reloadHeld && !rHeld && reload != null) { reload.Release(); if (reload.Done) FinishReload(); }
            reloadHeld = rHeld;
            // One-at-a-time reloads stop when you fire with rounds in
            if (reload != null && reload.PerRound && Ammo > 0 && Pressed(Btn.Fire)) { lastReload = null; reload = null; }

            if (Pressed(Btn.Fire)) fireBuffer = .25f;
            if (Carrying.Count > 0)
            {
                if (fireBuffer > 0) { fireBuffer = 0; ThrowTop(); }
            }
            else if (GunsLocked)
            {
                // The first waves are melee only: the trigger swings your melee weapon
                if (fireBuffer > 0 && meleeCd <= 0) { fireBuffer = 0; StartSwing(); }
                if (Pressed(Btn.Fire) && lockToastT <= 0)
                {
                    lockToastT = 6;
                    Toast($"Guns unlock at wave {db.gunsUnlockWave}: melee only until then!", new Color(1, .8f, .5f));
                }
            }
            else if ((Held(Btn.Fire) || fireBuffer > 0) && fireCd <= 0 && reload == null)
            {
                if (Ammo <= 0) StartReload();
                else Shoot();
            }
            if (Pressed(Btn.Melee) && meleeCd <= 0) StartSwing();
        }

        /// <summary>Where bullets leave from: the first-person gun (bottom right of your view) or the model's muzzle.</summary>
        public Vector3 MuzzlePosition()
        {
            if (FirstPerson) return EyePos + LookRotation * new Vector3(.26f, -.2f, .7f);
            return gunModel.Sockets.TryGetValue("Muzzle", out var mz) ? mz.position : transform.position + Vector3.up * 1.2f + AimDir * .6f;
        }

        void Shoot()
        {
            fireBuffer = 0;
            float rate = EffGun.fireRate * (1 + StatBonus(StatKind.Handling) * .5f) * (Statuses.Has(StatusType.SugarRush) ? 1.25f : 1);
            fireCd = 1 / rate;
            ammo[Gun.id] = Ammo - 1;
            var origin = MuzzlePosition();
            Combat.FireGun(this, EffGun, origin, AimPoint, DamageMult(false) * (1 + perfectBonus));
            Fx.Flash(origin, new Color(1, .8f, .4f), 5, 3, .06f);
            Fx.Smoke(origin, 1, .05f);
            Sfx.Play(Gun.id switch { "scattergun" => "shotgun", "harpoon" => "harpoon", "flare" => "flare", _ => "shot" });
            Shake(EffGun.recoil * .025f);
            FireHeat = 1;
            Pitch = Mathf.Clamp(Pitch - EffGun.recoil * .25f, -80, 80);   // the barrel climbs a little (the camera kick is bigger, and recovers)
            if (Ammo <= 0) { perfectBonus = 0; StartReload(); }
        }

        int dummyAmmo;

        /// <summary>Start the gun's reload minigame (CLAUDE.md R6b). With no input it finishes in the normal reload time.</summary>
        void StartReload()
        {
            if (reload != null || Ammo >= MagSize) return;
            float time = EffGun.reload / (1 + StatBonus(StatKind.Handling));
            reload = ReloadGame.Begin(db.Reload(Gun.reloadStyle), time, MagSize);
            lastReload = null;
            Sfx.Play("reload");
        }

        /// <summary>Tour and tests: empty the gun and start its reload.</summary>
        public void EmptyAndReload() { ammo[Gun.id] = 0; reload = null; StartReload(); }

        void FinishReload()
        {
            if (!reload.PerRound) ammo[Gun.id] = MagSize;
            if (reload.Perfect)
            {
                perfectBonus = reload.R.bonus > 0 ? reload.R.bonus : db.perfectReloadBonus;
                Toast($"Perfect reload: +{perfectBonus * 100:0}% damage this magazine", new Color(1, .85f, .3f));
            }
            Sfx.Play("reload");
            lastReload = reload;
            reload = null;
        }

        void CycleGun()
        {
            var list = db.guns.FindAll(g => ownedGuns.Contains(g.id));
            if (list.Count < 2) return;
            int i = list.IndexOf(Gun);
            GiveGun(list[(i + 1) % list.Count].id);
            Sfx.Play("ui");
        }

        public void OnHitEnemy(Enemy e, float dmg, bool crit, bool melee = false)
        {
            float steal = melee ? EffMelee.lifesteal : EffGun.lifesteal;
            if (steal > 0) Heal(dmg * steal);
            if (Hud != null) Hud.HitMarker(crit);
            else if (Kind == PlayerKind.Remote && Net.IsHost) Net.Host.HitMarker(this, crit);
        }
        public void OnSever(Enemy e, LimbDef limb) { Severs++; Toast($"{e.Def.displayName}: {limb.part} off!", new Color(1, .85f, .5f)); }
        public void OnKill(Enemy e)
        {
            Kills++;
            if (Hud != null) Hud.KillMarker();
            Sfx.Play("kill", .7f, .05f);
            // Kill combos: kills within the window chain; each one past the first pays a little more
            combo = comboT > 0 ? combo + 1 : 1;
            comboT = db.comboWindow;
            BestCombo = Mathf.Max(BestCombo, combo);
            if (combo >= 2)
            {
                int step = Mathf.Min(combo, db.comboMax) - 1;
                if (db.comboSandDollars > 0) gm.AddSandDollars(step * db.comboSandDollars);
                if (db.comboXp > 0) GainXp(step * db.comboXp);
                Sfx.Play("combo", .5f, .05f);
            }
        }
    }
}

// TideWall 3D — Player: XP and levels, stat points and respec, weapons owned and equipped, weapon mods.
// (One part of the Player class; Player.cs has the overview, the fields and the frame loop.)
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace TideWall
{
    public partial class Player
    {
        // ------------------------------------------------------------------ progression
        /// <summary>The total bonus from points in stats of this kind (+HP for MaxHp, a fraction for the rest).</summary>
        public float StatBonus(StatKind kind)
        {
            float b = 0;
            foreach (var kv in Allocated)
                if (db.Stat(kv.Key) is { } st && st.kind == kind) b += kv.Value * st.perPoint;
            return b;
        }

        public int Points(string statId) => Allocated.TryGetValue(statId, out var n) ? n : 0;

        List<string> ModsFor(string weaponId)
        {
            if (!Mods.TryGetValue(weaponId, out var list)) Mods[weaponId] = list = new List<string>();
            return list;
        }

        /// <summary>Copy the equipped weapons and apply this player's mods to the copies.</summary>
        void RebuildWeapons()
        {
            if (Gun != null)
            {
                var g = JsonUtility.FromJson<GunDef>(JsonUtility.ToJson(Gun));
                foreach (var id in ModsFor(Gun.id))
                {
                    var m = db.Mod(id); if (m == null) continue;
                    g.damage *= m.damage; g.fireRate *= m.fireRate; g.reload *= m.reload; g.range *= m.range;
                    g.magazine = Mathf.Max(1, Mathf.RoundToInt(g.magazine * m.magazine));
                    g.spreadDegrees += m.spread; g.splash += m.splash; g.knockback += m.knockback;
                    g.pierce += m.pierce; g.pellets += m.pellets; g.lifesteal += m.lifesteal; g.limbBonus += m.limbBonus;
                    if (m.setElement) g.element = m.element;
                }
                foreach (var a in AttachmentsOn(Gun.id))
                {
                    g.damage *= a.damage; g.fireRate *= a.fireRate; g.reload *= a.reload; g.range *= a.range;
                    g.magazine = Mathf.Max(1, Mathf.RoundToInt(g.magazine * a.magazine));
                    g.spreadDegrees *= a.spread; g.recoil *= a.recoil;
                }
                EffGun = g;
            }
            if (Melee != null)
            {
                var w = JsonUtility.FromJson<MeleeDef>(JsonUtility.ToJson(Melee));
                foreach (var id in ModsFor(Melee.id))
                {
                    var m = db.Mod(id); if (m == null) continue;
                    w.damage *= m.damage; w.cooldown *= m.cooldown; w.range *= m.range; w.arcDegrees = Mathf.Min(300, w.arcDegrees * m.arc);
                    w.knockback += m.knockback; w.stun += m.stun; w.lifesteal += m.lifesteal; w.limbBonus += m.limbBonus;
                    if (m.setElement) w.element = m.element;
                }
                EffMelee = w;
            }
        }

        /// <summary>Part II: a newcomer to the expedition starts at the part's level, with the stat points (and any mod)
        /// those levels would have earned.</summary>
        public void StartAtLevel(int level)
        {
            if (!Simulated || Level >= level) return;
            for (int l = Level + 1; l <= level; l++)
            {
                StatPoints += db.statPointsPerLevel;
                if (db.modEveryLevels > 0 && l % db.modEveryLevels == 0) PendingMods++;
            }
            Level = level;
            Xp = 0;
            if (PendingMods > 0 && ModOffer == null) RollModOffer();
            Toast($"Level {Level}: {StatPoints} stat points to spend ({Glyph(Btn.Stats)} Stats)", new Color(1, .9f, .4f));
        }

        /// <summary>XP from a kill (cards and Fortune scale it). Level-ups give stat points; every few levels, a mod.</summary>
        public void GainXp(float amount)
        {
            if (!Simulated || amount <= 0) return;
            Xp += Mathf.Max(1, Mathf.RoundToInt(amount * gm.Cards.Xp * (1 + StatBonus(StatKind.Fortune) * .5f)));
            while (Xp >= db.XpForLevel(Level))
            {
                Xp -= db.XpForLevel(Level);
                Level++;
                StatPoints += db.statPointsPerLevel;
                Heal(MaxHp * .15f);
                Fx.Sparks(transform.position + Vector3.up * 1.2f, new Color(1, .9f, .4f), 14);
                Sfx.Play("jackpot", .5f, .3f);
                bool mod = db.modEveryLevels > 0 && Level % db.modEveryLevels == 0;
                if (mod) { PendingMods++; if (ModOffer == null) RollModOffer(); }
                Toast(mod ? $"LEVEL {Level}! A weapon mod is ready: {Glyph(Btn.Stats)} Stats"
                          : $"LEVEL {Level}! +{db.statPointsPerLevel} stat point: {Glyph(Btn.Stats)} Stats", new Color(1, .9f, .4f));
            }
        }

        public string WhyCantSpend(string statId)
        {
            var st = db.Stat(statId);
            if (st == null) return "?";
            if (StatPoints <= 0) return "No stat points (level up to earn more)";
            if (Points(statId) >= st.maxPoints) return "Maxed";
            return null;
        }

        public void SpendPoint(string statId)
        {
            if (WhyCantSpend(statId) != null) return;
            Allocated[statId] = Points(statId) + 1;
            StatPoints--;
            if (db.Stat(statId).kind == StatKind.MaxHp) Heal(db.Stat(statId).perPoint);
            RefreshMaxHp();
            Sfx.Play("bell", .4f, .1f);
        }

        public int SpentPoints { get { int n = 0; foreach (var v in Allocated.Values) n += v; return n; } }
        public int RespecPrice => db.respecPerLevel * Level * (1 + Respecs);

        /// <summary>Refund every stat point (for sand dollars from the team purse).</summary>
        public bool Respec()
        {
            if (SpentPoints == 0 || !gm.TrySpend(RespecPrice)) return false;
            StatPoints += SpentPoints;
            Allocated.Clear();
            Respecs++;
            RefreshMaxHp();
            Sfx.Play("swing");
            Toast("Stats refunded: spend your points again", new Color(.8f, .9f, 1));
            return true;
        }

        /// <summary>Offer a few mods for the weapons in hand that they don't have yet.</summary>
        void RollModOffer()
        {
            var pool = db.mods.FindAll(m => m.gun ? !ModsFor(Gun.id).Contains(m.id) : !ModsFor(Melee.id).Contains(m.id));
            ModOffer = new List<ModDef>();
            while (ModOffer.Count < db.modChoices && pool.Count > 0)
            {
                var m = pool[Random.Range(0, pool.Count)];
                pool.Remove(m);
                ModOffer.Add(m);
            }
            if (ModOffer.Count == 0) ModOffer = null;
        }

        public void TakeMod(ModDef m)
        {
            if (ModOffer == null || !ModOffer.Contains(m)) return;
            ModsFor(m.gun ? Gun.id : Melee.id).Add(m.id);
            PendingMods = Mathf.Max(0, PendingMods - 1);
            ModOffer = null;
            if (PendingMods > 0) RollModOffer();
            RebuildWeapons();
            Sfx.Play("reload");
            Toast($"{m.displayName} fitted to your {(m.gun ? Gun.displayName : Melee.displayName)}", new Color(.7f, 1, .8f));
        }

        public IEnumerable<string> ModsOn(string weaponId) => ModsFor(weaponId);

        /// <summary>Stats button: the Stats menu (or the tidal card vote while one is running).</summary>
        void ToggleStats()
        {
            if (OpenedMenu != null && (OpenedMenu.Title == Menus.StatsTitle || OpenedMenu.Title == TidalCards.MenuTag)) { CloseMenu(); return; }
            if (OpenedMenu != null && OpenedMenu.System) return;
            OpenMenu(gm.Cards.Draft != null && !gm.Cards.Votes.ContainsKey(Index) ? Menus.CardVote(this) : Menus.Stats(this));
        }

        /// <summary>Client: the pause menu wants the host's Stats menu.</summary>
        public void RequestStats() { statsWanted = true; LocalMenu = null; }
    }
}

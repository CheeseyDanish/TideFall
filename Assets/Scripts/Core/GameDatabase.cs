// TideWall 3D — all content and balance numbers (CLAUDE.md R1).
// Lives as Assets/Resources/Data/GameDatabase.asset (editable in the Inspector). If the asset is missing,
// the game builds the same defaults in memory, so code and asset can never disagree about what exists.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace TideWall
{
    public enum EnemyBehaviour { Walker, Flyer, Puffer, Leaper, Shark, Croc, Urchin, Jelly, Octopus, Boss }

    /// <summary>What happens to an enemy when one of its limbs is shot or cut off.</summary>
    public enum LimbEffect
    {
        None,
        Kill,           // head / eyes: severing it kills (and is a weak point)
        WeakenAttack,   // claw / arm: attack damage halves per limb lost
        Slow,           // leg / fin / tail / tentacle: move speed drops
        Ground,         // wing: a flyer falls out of the sky and crawls
        NoTail,         // croc tail: no more tail swipe
        ShellOff,       // hermit shell: loses its armour, panics and speeds up
        WeakenSting,    // jellyfish tentacle: sting damage drops
    }

    public enum Element { None, Fire, Shock }

    public enum StatusType
    {
        None,
        // condiments and food
        Rage, Speed, SugarRush,
        // ??? sauce: good
        Giant, Invincible, DoubleDamage,
        // ??? sauce: bad
        UpsideDown, ReverseControls, Tiny, Fisheye, Slow, FirstPerson, InkVision, Tunnel,
        // ??? sauce: neutral
        Rainbow, DutchTilt, Bubbles,
        // combat
        Burning, Stunned, Grabbed,
    }

    public enum UpgradeKind { RepairWall, WallMaxHp, PlayerMaxHp, GunDamage, MeleeDamage, Magazine, GasCans }

    [Serializable]
    public class LimbDef
    {
        [Tooltip("Model child name without the 'Limb_' prefix, e.g. ClawL")] public string part;
        [Tooltip("Limb HP as a fraction of the enemy's max HP")] public float hpFraction = .35f;
        public LimbEffect effect;
        [Tooltip("Damage multiplier for hits on this limb (weak points > 1)")] public float critMultiplier = 1f;

        public LimbDef() { }
        public LimbDef(string part, LimbEffect effect, float hpFraction = .35f, float crit = 1f)
        { this.part = part; this.effect = effect; this.hpFraction = hpFraction; critMultiplier = crit; }
    }

    [Serializable]
    public class EnemyDef
    {
        public string id, displayName, model;
        public EnemyBehaviour behaviour;
        public float scale = 1f;
        [Header("Stats (day 1; HP scales with the day)")]
        public float hp = 30, speed = 2, damage = 5, attackRate = 1, attackRange = 1.1f, armor;
        [Tooltip("Speed multiplier while in the sea")] public float waterSpeedMult = .8f;
        [Tooltip("Chases players within this many metres instead of attacking the wall")] public float aggroRange = 6f;
        [Tooltip("Damage dealt back to anyone who melees it (urchin spines)")] public float thorns;
        [Header("Economy & spawning")]
        public int bounty = 2, corpseValue = 8, cost = 1, minDay = 1;
        public List<LimbDef> limbs = new();
    }

    [Serializable]
    public class GunDef
    {
        public string id, displayName, model, description;
        public int price;
        public float damage = 20, fireRate = 3, spreadDegrees = 2, range = 30, reload = 1.4f, knockback, splash;
        public int pellets = 1, magazine = 6, pierce;
        public Element element;
    }

    [Serializable]
    public class MeleeDef
    {
        public string id, displayName, model, description;
        public int price;
        public float damage = 18, arcDegrees = 100, range = 1.8f, cooldown = .45f, knockback = 2, stun, pull;
        public Element element;
    }

    [Serializable]
    public class FoodDef
    {
        public string id, displayName, model, description;
        public int price;
        public float heal;
        public StatusType status;
        public float statusSeconds;
        [Tooltip("Clears burning, ink and slows")] public bool cures;
        public bool soldAtStand = true, spawnsOnBeach = true;
    }

    [Serializable]
    public class CondimentDef
    {
        public string id, displayName, description;
        public int price;
        public StatusType status;       // None = the ??? sauce gamble
        public float statusSeconds = 20;
        public Color color = Color.red;
    }

    [Serializable]
    public class UpgradeDef
    {
        public string id, displayName, description;
        public UpgradeKind kind;
        public int price;
        [Tooltip("Each purchase multiplies the price by this")] public float priceGrowth = 1.5f;
        [Tooltip("0 = unlimited")] public int maxLevel;
        public float amount;
    }

    [CreateAssetMenu(menuName = "TideWall/Game Database")]
    public class GameDatabase : ScriptableObject
    {
        public List<EnemyDef> enemies = new();
        public List<GunDef> guns = new();
        public List<MeleeDef> melee = new();
        public List<FoodDef> foods = new();
        public List<CondimentDef> condiments = new();
        public List<UpgradeDef> upgrades = new();

        [Header("Players")]
        public float playerMaxHp = 100, playerSpeed = 5.5f, playerWaterSpeedMult = .6f, carrySpeedMult = .8f;
        public float dashSpeed = 14, dashSeconds = .18f, dashCooldown = .8f;
        public float bleedOutSeconds = 30, reviveSeconds = 2f, reviveHpFraction = .4f;
        public string startGun = "revolver", startMelee = "spade";
        public int startCoins = 40;

        [Header("Wall")]
        public float wallMaxHp = 600;

        [Header("Wet / elements")]
        [Tooltip("Wetness lost per second on dry land (1 = soaked)")] public float wetDrainPerSecond = .12f;
        [Tooltip("Drips per second when fully soaked")] public float maxDripsPerSecond = 28;
        public float shockWetMultiplier = 1.5f, burnDps = 8, burnSeconds = 4;

        [Header("Waves (d = day)")]
        [Tooltip("HP multiplier = 1 + hpPerDay * (d-1)")] public float hpPerDay = .18f;
        [Tooltip("Damage multiplier = 1 + damagePerDay * (d-1)")] public float damagePerDay = .05f;
        [Tooltip("Most enemies alive at once (keeps big nights smooth)")] public int maxAlive = 70;
        [Tooltip("Spawn budget = a + b*d + c*d*d, times the phase's multiplier")] public Vector3 budget = new(10, 7, .6f);
        public float waveRushFraction = .5f, waveRushDelay = 3f, waveGap = 8f;

        [Header("??? Sauce")]
        [Tooltip("Chance of the jackpot, as a fraction (0.0001 = 0.01%)")] public float sauceJackpotChance = .0001f;
        public int sauceJackpotCoins = 10000;

        [Header("Gas cans")]
        [Tooltip("Chance a bullet hit makes a gas can blow (otherwise it starts leaking; the next hit always blows)")]
        public float gasCanBulletChance = .6f;
        public float gasCanThrowChance = .7f, gasCanDamage = 90, gasCanRadius = 4.5f;
        public int baseGasCans = 3;

        public EnemyDef Enemy(string id) => enemies.Find(e => e.id == id);
        public GunDef Gun(string id) => guns.Find(g => g.id == id);
        public MeleeDef Melee(string id) => melee.Find(m => m.id == id);
        public FoodDef Food(string id) => foods.Find(f => f.id == id);
        public float HpScale(int day) => 1 + hpPerDay * (day - 1);
        public float DamageScale(int day) => 1 + damagePerDay * (day - 1);
        public float Budget(int day) => budget.x + budget.y * day + budget.z * day * day;

        public static GameDatabase Load()
        {
            var db = Resources.Load<GameDatabase>("Data/GameDatabase");
            if (db == null || db.enemies.Count == 0) { db = CreateInstance<GameDatabase>(); db.FillDefaults(); }
            return db;
        }

        /// <summary>The shipped content. The editor setup writes this into the asset; edit the asset to tune.</summary>
        public void FillDefaults()
        {
            LimbDef L(string p, LimbEffect e, float hp = .35f, float crit = 1f) => new(p, e, hp, crit);
            enemies = new List<EnemyDef>
            {
                new() { id = "crab", displayName = "Crabling", model = "Crab", behaviour = EnemyBehaviour.Walker, hp = 30, speed = 2.4f, damage = 4, aggroRange = 5,
                        bounty = 2, corpseValue = 8, cost = 1, limbs = { L("ClawL", LimbEffect.WeakenAttack), L("ClawR", LimbEffect.WeakenAttack), L("Eyes", LimbEffect.Kill, .25f, 2f) } },
                new() { id = "drowner", displayName = "Drowner", model = "Drowner", behaviour = EnemyBehaviour.Walker, hp = 60, speed = 1.6f, damage = 8, aggroRange = 7,
                        bounty = 4, corpseValue = 14, cost = 2, limbs = { L("ArmL", LimbEffect.WeakenAttack, .3f), L("ArmR", LimbEffect.WeakenAttack, .3f),
                        L("LegL", LimbEffect.Slow, .3f), L("LegR", LimbEffect.Slow, .3f), L("Head", LimbEffect.Kill, .4f, 2f) } },
                new() { id = "gull", displayName = "Gull", model = "Gull", behaviour = EnemyBehaviour.Flyer, hp = 20, speed = 6, damage = 5, aggroRange = 14,
                        bounty = 3, corpseValue = 6, cost = 2, limbs = { L("WingL", LimbEffect.Ground, .3f), L("WingR", LimbEffect.Ground, .3f) } },
                new() { id = "hermit", displayName = "Hermit Crab", model = "HermitCrab", behaviour = EnemyBehaviour.Walker, hp = 40, speed = 1.8f, damage = 5, armor = 4,
                        bounty = 3, corpseValue = 15, cost = 2, minDay = 2, limbs = { L("Shell", LimbEffect.ShellOff, .6f), L("ClawL", LimbEffect.WeakenAttack),
                        L("ClawR", LimbEffect.WeakenAttack), L("Eyes", LimbEffect.Kill, .25f, 2f) } },
                new() { id = "puffer", displayName = "Puffer", model = "Puffer", behaviour = EnemyBehaviour.Puffer, hp = 30, speed = 2, damage = 35,
                        bounty = 3, corpseValue = 10, cost = 3, minDay = 2 },
                new() { id = "urchin", displayName = "Sea Urchin", model = "Urchin", behaviour = EnemyBehaviour.Urchin, hp = 50, speed = 1f, damage = 10, thorns = 8,
                        bounty = 3, corpseValue = 12, cost = 2, minDay = 2 },
                new() { id = "jelly", displayName = "Jellyfish", model = "Jellyfish", behaviour = EnemyBehaviour.Jelly, hp = 35, speed = 1.2f, damage = 6, attackRange = 1.8f,
                        bounty = 3, corpseValue = 10, cost = 2, minDay = 2, limbs = { L("Tent1", LimbEffect.WeakenSting, .2f), L("Tent2", LimbEffect.WeakenSting, .2f),
                        L("Tent3", LimbEffect.WeakenSting, .2f), L("Tent4", LimbEffect.WeakenSting, .2f) } },
                new() { id = "shark", displayName = "Shark", model = "Shark", behaviour = EnemyBehaviour.Shark, hp = 120, speed = 6, waterSpeedMult = 1, damage = 25,
                        attackRange = 1.8f, aggroRange = 12, bounty = 8, corpseValue = 45, cost = 6, minDay = 2,
                        limbs = { L("Fin", LimbEffect.Slow, .3f), L("Tail", LimbEffect.Slow, .35f) } },
                new() { id = "eel", displayName = "Eel", model = "Eel", behaviour = EnemyBehaviour.Leaper, hp = 35, speed = 3.2f, damage = 6, aggroRange = 8,
                        bounty = 3, corpseValue = 12, cost = 2, minDay = 3, limbs = { L("Head", LimbEffect.Kill, .35f, 2f), L("Tail", LimbEffect.Slow, .35f) } },
                new() { id = "turtle", displayName = "Shellback", model = "Turtle", behaviour = EnemyBehaviour.Walker, hp = 160, speed = .9f, damage = 14, attackRate = .7f, armor = 6,
                        attackRange = 1.5f, bounty = 6, corpseValue = 25, cost = 5, minDay = 3, limbs = { L("Head", LimbEffect.Kill, .3f, 2.5f) } },
                new() { id = "croc", displayName = "Saltwater Croc", model = "Croc", behaviour = EnemyBehaviour.Croc, hp = 220, speed = 2.2f, waterSpeedMult = 1.6f, damage = 20,
                        attackRange = 2.2f, armor = 5, aggroRange = 8, bounty = 10, corpseValue = 60, cost = 8, minDay = 4,
                        limbs = { L("Head", LimbEffect.Kill, .35f, 2f), L("Tail", LimbEffect.NoTail, .3f) } },
                new() { id = "octopus", displayName = "Octopus", model = "Octopus", behaviour = EnemyBehaviour.Octopus, hp = 140, speed = 1.6f, damage = 10, attackRange = 3.5f,
                        aggroRange = 9, bounty = 8, corpseValue = 35, cost = 6, minDay = 4,
                        limbs = { L("Tent1", LimbEffect.Slow, .18f), L("Tent2", LimbEffect.Slow, .18f), L("Tent3", LimbEffect.Slow, .18f),
                                  L("Tent4", LimbEffect.Slow, .18f), L("Tent5", LimbEffect.Slow, .18f), L("Tent6", LimbEffect.Slow, .18f) } },
                new() { id = "lobster", displayName = "Clawback", model = "Lobster", behaviour = EnemyBehaviour.Walker, hp = 300, speed = 1.2f, damage = 22, attackRate = .6f,
                        armor = 4, attackRange = 2f, bounty = 12, corpseValue = 40, cost = 10, minDay = 5,
                        limbs = { L("ClawL", LimbEffect.WeakenAttack, .3f), L("ClawR", LimbEffect.WeakenAttack, .3f), L("Head", LimbEffect.Kill, .3f, 2f) } },
                new() { id = "glizzy", displayName = "Glizzy Monster", model = "GlizzyMonster", behaviour = EnemyBehaviour.Boss, hp = 2500, speed = 1f, damage = 40,
                        attackRate = .5f, attackRange = 3f, armor = 3, aggroRange = 10, bounty = 150, corpseValue = 300, cost = 0, minDay = 5 },
            };

            guns = new List<GunDef>
            {
                new() { id = "revolver", displayName = "Revolver", model = "Revolver", price = 0, damage = 22, fireRate = 3, magazine = 6, reload = 1.4f, spreadDegrees = 2,
                        description = "Six shots, good on weak points." },
                new() { id = "scattergun", displayName = "Scattergun", model = "Scattergun", price = 300, damage = 9, pellets = 7, fireRate = 1.2f, magazine = 6, reload = 2f,
                        spreadDegrees = 14, range = 14, description = "Seven pellets. Clears crabs and claws, weak at range." },
                new() { id = "harpoon", displayName = "Harpoon Gun", model = "HarpoonGun", price = 600, damage = 60, fireRate = .9f, magazine = 3, reload = 1.6f, spreadDegrees = 0,
                        pierce = 99, knockback = 6, description = "Skewers a whole line and shoves it back into the surf." },
                new() { id = "flare", displayName = "Flare Gun", model = "FlareGun", price = 450, damage = 15, fireRate = 1.2f, magazine = 4, reload = 1.8f, spreadDegrees = 1,
                        splash = 2f, element = Element.Fire, description = "Sets dry targets alight. Wet ones just steam." },
            };

            melee = new List<MeleeDef>
            {
                new() { id = "spade", displayName = "Beach Spade", model = "Spade", price = 0, damage = 18, arcDegrees = 100, range = 1.8f, cooldown = .45f, knockback = 2,
                        description = "Everyone starts somewhere." },
                new() { id = "cutlass", displayName = "Cutlass", model = "Cutlass", price = 250, damage = 24, arcDegrees = 90, range = 2f, cooldown = .32f, knockback = 1.5f,
                        description = "Fast slashes. Best for lopping off limbs." },
                new() { id = "oar", displayName = "Oar", model = "Oar", price = 300, damage = 20, arcDegrees = 160, range = 2.6f, cooldown = .7f, knockback = 5,
                        description = "Wide sweep that knocks a crowd back." },
                new() { id = "boathook", displayName = "Boat Hook", model = "BoatHook", price = 400, damage = 22, arcDegrees = 30, range = 3.4f, cooldown = .6f, knockback = 0,
                        pull = 3, element = Element.Shock, description = "Long reach. Hooks a target toward you with a jolt (great on wet ones)." },
                new() { id = "anchor", displayName = "Anchor", model = "Anchor", price = 700, damage = 70, arcDegrees = 120, range = 2.4f, cooldown = 1.3f, knockback = 8,
                        stun = 1f, description = "Slow, crushing, stuns. Cracks shells." },
            };

            foods = new List<FoodDef>
            {
                new() { id = "hotdog", displayName = "Hotdog", model = "Hotdog", price = 10, heal = 35, spawnsOnBeach = false, description = "Heals 35. Add a condiment!" },
                new() { id = "burger", displayName = "Burger", model = "Burger", price = 16, heal = 60, description = "Heals 60." },
                new() { id = "fries", displayName = "Fries", model = "Fries", price = 8, heal = 20, status = StatusType.Speed, statusSeconds = 8, description = "Heals 20, quick feet for 8s." },
                new() { id = "drink", displayName = "Soda", model = "Cup", price = 6, heal = 10, cures = true, description = "Heals 10 and washes off burns and ink." },
                new() { id = "donut", displayName = "Donut", model = "Donut", price = 8, heal = 25, status = StatusType.SugarRush, statusSeconds = 15,
                        description = "Heals 25, sugar rush: +25% fire rate for 15s." },
            };

            condiments = new List<CondimentDef>
            {
                new() { id = "ketchup", displayName = "Ketchup", price = 4, status = StatusType.Rage, statusSeconds = 20, color = new Color(.85f, .1f, .1f),
                        description = "RAGE: +50% damage for 20s." },
                new() { id = "mustard", displayName = "Mustard", price = 4, status = StatusType.Speed, statusSeconds = 20, color = new Color(.95f, .8f, .1f),
                        description = "Speed: +40% move speed for 20s." },
                new() { id = "mystery", displayName = "??? Sauce", price = 6, status = StatusType.None, color = new Color(.55f, .2f, .7f),
                        description = "Who knows. Could be great. Could be terrible. Could be money." },
            };

            upgrades = new List<UpgradeDef>
            {
                new() { id = "repair", displayName = "Repair Wall", kind = UpgradeKind.RepairWall, price = 40, priceGrowth = 1, amount = .3f, description = "Restore 30% of the wall." },
                new() { id = "reinforce", displayName = "Reinforce Wall", kind = UpgradeKind.WallMaxHp, price = 80, maxLevel = 8, amount = .2f, description = "+20% wall max HP." },
                new() { id = "vitality", displayName = "Sea Legs", kind = UpgradeKind.PlayerMaxHp, price = 70, priceGrowth = 1.4f, maxLevel = 6, amount = 25, description = "+25 max HP for every player." },
                new() { id = "gundmg", displayName = "Heavier Rounds", kind = UpgradeKind.GunDamage, price = 90, maxLevel = 8, amount = .15f, description = "+15% gun damage." },
                new() { id = "meleedmg", displayName = "Whetstone", kind = UpgradeKind.MeleeDamage, price = 70, maxLevel = 8, amount = .15f, description = "+15% melee damage." },
                new() { id = "pockets", displayName = "Deep Pockets", kind = UpgradeKind.Magazine, price = 80, maxLevel = 4, amount = .3f, description = "+30% magazine size." },
                new() { id = "gascans", displayName = "Gas Delivery", kind = UpgradeKind.GasCans, price = 50, priceGrowth = 1.6f, maxLevel = 4, amount = 2,
                        description = "+2 gas cans dropped on the beach each morning." },
            };
        }
    }
}

// TideWall 3D — all content and balance numbers (CLAUDE.md R1).
// Lives as Assets/Content/Data/GameDatabase.asset (editable in the Inspector). If the asset is missing,
// the game builds the same defaults in memory, so code and asset can never disagree about what exists.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace TideWall
{
    public enum EnemyBehaviour { Walker, Flyer, Puffer, Leaper, Shark, Croc, Urchin, Jelly, Octopus, Boss, Clam }

    /// <summary>What a buildable defence does (StructureDef.kind).</summary>
    public enum StructureKind { Barricade, Spikes, Snare, Turret, Cannon, Bait }

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

    /// <summary>A gun's reload minigame (the 2D game's styles). Every one is optional: doing nothing reloads in the gun's reload time.</summary>
    public enum ReloadStyle { Zone, Rhythm, Shells, Bolt, Heavy, Drop, Mash, Pressure }

    /// <summary>The numbers for one reload minigame. Zones are fractions of the reload bar.</summary>
    [Serializable]
    public class ReloadDef
    {
        public ReloadStyle style;
        public string displayName, how;
        [Tooltip("The green zone (Zone, Heavy, Shells, Pressure)")] public Vector2 zone = new(.42f, .6f);
        [Tooltip("Bolt: the second zone")] public Vector2 zone2;
        [Tooltip("Seconds added by a miss")] public float jam = .6f;
        [Tooltip("Perfect damage bonus (0 = the database default)")] public float bonus;
        [Tooltip("Rounds load one at a time; firing with rounds in cancels the reload")] public bool perRound;
        [Tooltip("Rhythm: seconds per beat, the hit window either side, and the stall after a fumble")] public float beat = .2f, window = .07f, fumble = .35f;
        [Tooltip("Shells: seconds lost when you tap outside the zone")] public float slip = .25f;
        [Tooltip("Drop: width of the zone that moves every reload")] public float width = .16f;
        [Tooltip("Mash: share of the reload each tap skips; Perfect if done within this share of the normal time")] public float perTap = .09f, perfectUnder = .55f;
        [Tooltip("Pressure: seconds for the needle to fill (it vents at the top)")] public float fill = .9f;
    }

    /// <summary>How a melee weapon swings (animation, and when the hit lands).</summary>
    public enum MeleeStyle { Chop, Slash, Sweep, Thrust, Slam }

    public enum StatusType
    {
        None,
        // condiments and food
        Rage, Speed, SugarRush,
        // ??? sauce: good
        Giant, Invincible, DoubleDamage,
        // ??? sauce: bad
        UpsideDown, ReverseControls, Tiny, Fisheye, Slow, SwapView, InkVision, Tunnel,
        // ??? sauce: neutral
        Rainbow, DutchTilt, Bubbles,
        // combat
        Burning, Stunned, Grabbed,
    }

    public enum UpgradeKind { RepairWall, WallMaxHp, PlayerMaxHp, GunDamage, MeleeDamage, Magazine, GasCans, PlantRepair, PlantMaxHp }

    /// <summary>What a stat point improves (amount per point is StatDef.perPoint).</summary>
    public enum StatKind { MaxHp, MeleeDamage, GunDamage, MoveSpeed, Handling, Fortune }

    /// <summary>Tidal cards that only exist in some kinds of game. Exclusive cards have effects nothing else has.</summary>
    public enum CardExclusive { None, Online, SplitScreen }

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
        [Tooltip("Sand dollars it drops when killed")] public int bounty = 2;
        public int corpseValue = 8, cost = 1, minDay = 1;
        [Tooltip("XP for the killer (0 = 10 + 12 x cost)")] public int xp;
        [Tooltip("Doesn't appear before this wave of the run (1 = from the start)")] public int minWave = 1;
        [Tooltip("How often it's picked compared with others (1 = normal, 0.15 = rare)")] public float spawnWeight = 1;
        [Tooltip("Golden pearls it drops when killed")] public int pearls;
        public int Xp => xp > 0 ? xp : 10 + 12 * cost;
        [Tooltip("Carries the Mark (the inner half of the Spiral) on its back: the sea's own (docs/STORY.md)")] public bool mark;
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
        [Tooltip("Kick per shot, in degrees (camera and view model)")] public float recoil = 3;
        [Tooltip("Which reload minigame this gun uses")] public ReloadStyle reloadStyle;
        [Tooltip("Set by weapon mods: share of damage healed, extra limb damage")] public float lifesteal, limbBonus;
    }

    [Serializable]
    public class MeleeDef
    {
        public string id, displayName, model, description;
        public int price;
        public float damage = 18, arcDegrees = 100, range = 1.8f, cooldown = .45f, knockback = 2, stun, pull;
        public Element element;
        [Header("Swing (the hit lands at the end of the wind-up)")]
        public MeleeStyle style;
        public float windup = .12f, strike = .1f, recover = .18f;
        public float SwingTime => windup + strike + recover;
        [Tooltip("Set by weapon mods: share of damage healed, extra limb damage")] public float lifesteal, limbBonus;
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

    /// <summary>A stat players put level-up points into (Stats menu).</summary>
    [Serializable]
    public class StatDef
    {
        public string id, displayName, description;
        public StatKind kind;
        [Tooltip("Effect per point: +HP for MaxHp, a fraction (0.08 = +8%) for the others")] public float perPoint;
        public int maxPoints = 10;
    }

    /// <summary>A weapon mod, offered every few levels. Applies to the gun or melee weapon it was taken on.</summary>
    [Serializable]
    public class ModDef
    {
        public string id, displayName, description;
        [Tooltip("Gun mod (else a melee mod)")] public bool gun = true;
        [Header("Multipliers (1 = no change)")]
        public float damage = 1, fireRate = 1, reload = 1, magazine = 1, range = 1, arc = 1, cooldown = 1;
        [Header("Additions")]
        public float spread, splash, knockback, stun;
        public int pierce, pellets;
        [Tooltip("Fraction of damage dealt returned as HP")] public float lifesteal;
        [Tooltip("Extra damage to limbs (0.6 = +60%)")] public float limbBonus;
        public bool setElement;
        public Element element;
    }

    /// <summary>A tidal card: a modifier the team drafts, lasting a number of waves.</summary>
    [Serializable]
    public class TidalCardDef
    {
        public string id, displayName;
        [TextArea] public string description;
        [Tooltip("How many waves it lasts (it can carry over into the next phase)")] public int waves = 3;
        public CardExclusive exclusive;
        public float weight = 1;
        public Color color = new(.3f, .7f, .9f);
        [Header("Multipliers (1 = no change)")]
        public float enemyHp = 1, enemySpeed = 1, enemyDamage = 1, budget = 1, sandDollars = 1, xp = 1, playerDamage = 1, playerSpeed = 1, playerMaxHp = 1, corpseValue = 1;
        [Header("Other")]
        public bool alwaysWet;
        public float fogDensity;
        [Tooltip("How far up the beach breakers carry enemies (metres past the shoreline)")] public float breakerReach;
        [Tooltip("Sand dollars wash away after this many seconds (0 = normal)")] public float sandDollarLifetime;
        [Tooltip("Exclusive behaviour handled in TidalCards.cs: buddybeam, bottle, screenswap, snacks")] public string special;
    }

    /// <summary>A defence players build on the beach (Build menu), paid in sand dollars and golden pearls.</summary>
    [Serializable]
    public class StructureDef
    {
        public string id, displayName, model, description;
        public StructureKind kind;
        public int sandDollars, pearls;
        public float hp = 100, radius = 1.2f;
        [Header("Effect")]
        public float damage, rate = 1, range, slow, splash;
        [Tooltip("How many can stand at once (0 = unlimited)")] public int max;
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
        public List<StatDef> stats = new();
        public List<ModDef> mods = new();
        public List<TidalCardDef> cards = new();
        public List<StructureDef> structures = new();
        public List<ReloadDef> reloads = new();
        [Header("Vendors (what they shout)")]
        [TextArea] public List<string> chefLines = new(), fishmongerLines = new();

        [Header("Players")]
        public float playerMaxHp = 100, playerSpeed = 5.5f, playerWaterSpeedMult = .6f, carrySpeedMult = .8f;
        public float dashSpeed = 14, dashSeconds = .18f, dashCooldown = .8f;
        public float bleedOutSeconds = 30, reviveSeconds = 2f, reviveHpFraction = .4f;
        public string startGun = "revolver", startMelee = "spade";
        public int startSandDollars = 40;
        [Tooltip("Guns can't be fired before this wave of the run: the first waves are melee only")] public int gunsUnlockWave = 4;
        [Tooltip("Damage bonus after a Perfect reload, until the magazine is empty")] public float perfectReloadBonus = .25f;

        [Header("Kill combos")]
        [Tooltip("Kills closer together than this (seconds) chain into a combo")] public float comboWindow = 3f;
        [Tooltip("Bonus sand dollars per kill past the first in a combo")] public int comboSandDollars = 1;
        [Tooltip("Bonus XP per kill past the first in a combo")] public int comboXp = 2;
        [Tooltip("Combo count where the bonuses stop growing")] public int comboMax = 20;

        [Header("Levels, stats and mods")]
        [Tooltip("XP for the next level = xpBase x level^xpGrowth")] public float xpBase = 60, xpGrowth = 1.35f;
        public int statPointsPerLevel = 1;
        [Tooltip("A weapon mod is offered every this many levels")] public int modEveryLevels = 5;
        public int modChoices = 3;
        [Tooltip("Teammates get this share of a kill's XP")] public float sharedXp = .5f;
        [Tooltip("Respec price = this x the player's level")] public int respecPerLevel = 15;

        [Header("Sand dollars on the beach")]
        public float sandDollarLifetime = 30, sandDollarMagnet = 4.5f;
        [Tooltip("Sand dollars fly to the killer at this speed; anyone they pass within this distance catches them")]
        public float lootFlySpeed = 9, lootIntercept = 1.6f;

        [Header("Defences")]
        [Tooltip("HP of each wall section; a section at 0 is breached")] public float wallSegmentHp = 140;
        [Tooltip("The Nuclear Sand Powerplant: at 0 it melts down and the run ends")] public float plantMaxHp = 1000;

        [Header("Breakers (enemies arrive riding waves)")]
        public float breakerSpeed = 9, breakerGroupSeconds = 1.5f, breakerStartZ = 100;
        [Tooltip("Chance a creature wipes out (falls off its surfboard) before the shore")] public float breakerWipeoutChance = .17f;

        [Header("Tidal cards")]
        public int cardChoices = 3;
        public float cardVoteSeconds = 15;
        [Tooltip("When exclusive cards are possible, chance one of the choices is exclusive")] public float exclusiveCardChance = .6f;
        [Tooltip("Chance the vote also offers a re-roll (three new cards)")] public float cardRerollChance = .15f;

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
        public int sauceJackpotSandDollars = 10000;

        [Header("Gas cans")]
        [Tooltip("Chance a bullet hit makes a gas can blow (otherwise it starts leaking; the next hit always blows)")]
        public float gasCanBulletChance = .6f;
        public float gasCanThrowChance = .7f, gasCanDamage = 90, gasCanRadius = 4.5f;
        public int baseGasCans = 3;

        public EnemyDef Enemy(string id) => enemies.Find(e => e.id == id);
        public GunDef Gun(string id) => guns.Find(g => g.id == id);
        public ReloadDef Reload(ReloadStyle s) => reloads.Find(r => r.style == s) ?? new ReloadDef { style = s, displayName = s.ToString(), how = "" };
        public MeleeDef Melee(string id) => melee.Find(m => m.id == id);
        public FoodDef Food(string id) => foods.Find(f => f.id == id);
        public StatDef Stat(string id) => stats.Find(s => s.id == id);
        public ModDef Mod(string id) => mods.Find(m => m.id == id);
        public TidalCardDef Card(string id) => cards.Find(c => c.id == id);
        public StructureDef Structure(string id) => structures.Find(c => c.id == id);
        public int XpForLevel(int level) => Mathf.RoundToInt(xpBase * Mathf.Pow(Mathf.Max(1, level), xpGrowth));
        public float HpScale(int day) => 1 + hpPerDay * (day - 1);
        public float DamageScale(int day) => 1 + damagePerDay * (day - 1);
        public float Budget(int day) => budget.x + budget.y * day + budget.z * day * day;

        public static GameDatabase Load()
        {
            var db = Content.Database;
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
                        bounty = 3, corpseValue = 6, cost = 2, minWave = 5, limbs = { L("WingL", LimbEffect.Ground, .3f), L("WingR", LimbEffect.Ground, .3f) } },
                new() { id = "hermit", displayName = "Hermit Crab", model = "HermitCrab", behaviour = EnemyBehaviour.Walker, hp = 40, speed = 1.8f, damage = 5, armor = 4,
                        bounty = 3, corpseValue = 15, cost = 2, minDay = 2, mark = true, limbs = { L("Shell", LimbEffect.ShellOff, .6f), L("ClawL", LimbEffect.WeakenAttack),
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
                        attackRange = 1.5f, bounty = 6, corpseValue = 25, cost = 5, minDay = 3, mark = true, limbs = { L("Head", LimbEffect.Kill, .3f, 2.5f) } },
                new() { id = "croc", displayName = "Saltwater Croc", model = "Croc", behaviour = EnemyBehaviour.Croc, hp = 220, speed = 2.2f, waterSpeedMult = 1.6f, damage = 20,
                        attackRange = 2.2f, armor = 5, aggroRange = 8, bounty = 10, corpseValue = 60, cost = 8, minDay = 4,
                        mark = true, limbs = { L("Head", LimbEffect.Kill, .35f, 2f), L("Tail", LimbEffect.NoTail, .3f) } },
                new() { id = "octopus", displayName = "Octopus", model = "Octopus", behaviour = EnemyBehaviour.Octopus, hp = 140, speed = 1.6f, damage = 10, attackRange = 3.5f,
                        aggroRange = 9, bounty = 8, corpseValue = 35, cost = 6, minDay = 4,
                        limbs = { L("Tent1", LimbEffect.Slow, .18f), L("Tent2", LimbEffect.Slow, .18f), L("Tent3", LimbEffect.Slow, .18f),
                                  L("Tent4", LimbEffect.Slow, .18f), L("Tent5", LimbEffect.Slow, .18f), L("Tent6", LimbEffect.Slow, .18f) } },
                new() { id = "lobster", displayName = "Clawback", model = "Lobster", behaviour = EnemyBehaviour.Walker, hp = 300, speed = 1.2f, damage = 22, attackRate = .6f,
                        armor = 4, attackRange = 2f, bounty = 12, corpseValue = 40, cost = 10, minDay = 5,
                        mark = true, limbs = { L("ClawL", LimbEffect.WeakenAttack, .3f), L("ClawR", LimbEffect.WeakenAttack, .3f), L("Head", LimbEffect.Kill, .3f, 2f) } },
                new() { id = "clam", displayName = "Giant Clam", model = "Clam", behaviour = EnemyBehaviour.Clam, hp = 90, speed = .9f, damage = 14, attackRange = 1.6f,
                        armor = 10, aggroRange = 6, bounty = 6, corpseValue = 30, cost = 3, spawnWeight = .15f, pearls = 2,
                        limbs = { L("Lid", LimbEffect.ShellOff, .5f) } },
                new() { id = "glizzy", displayName = "Glizzy Monster", model = "GlizzyMonster", behaviour = EnemyBehaviour.Boss, hp = 2500, speed = 1f, damage = 40,
                        attackRate = .5f, attackRange = 3f, armor = 3, aggroRange = 10, bounty = 150, corpseValue = 300, cost = 0, minDay = 5 },
                // Part II's last stage: the Heart's keeper. Never sent by the wave director (cost 0, Boss)
                new() { id = "tidemother", displayName = "The Tidemother", model = "Tidemother", behaviour = EnemyBehaviour.Boss, hp = 6000, speed = .8f, damage = 45,
                        attackRate = .45f, attackRange = 3.6f, armor = 5, aggroRange = 60, bounty = 400, corpseValue = 600, cost = 0, minDay = 99, pearls = 10, mark = true,
                        limbs = { L("ClawL", LimbEffect.WeakenAttack, .2f), L("ClawR", LimbEffect.WeakenAttack, .2f), L("Eye", LimbEffect.Slow, .15f, 2f) } },
            };

            guns = new List<GunDef>
            {
                new() { id = "revolver", displayName = "Revolver", model = "Revolver", price = 0, damage = 22, fireRate = 3, magazine = 6, reload = 1.4f, spreadDegrees = 2, recoil = 3.5f,
                        reloadStyle = ReloadStyle.Rhythm, description = "Six shots, good on weak points. Reload: tap on the beat." },
                new() { id = "smg", displayName = "Rivet SMG", model = "RivetSMG", price = 400, damage = 9, fireRate = 10, magazine = 24, reload = 1.6f, spreadDegrees = 5, recoil = 1.6f,
                        range = 24, reloadStyle = ReloadStyle.Zone, description = "Sprays rivets. Reload: tap in the green." },
                new() { id = "scattergun", displayName = "Scattergun", model = "Scattergun", price = 300, damage = 9, pellets = 7, fireRate = 1.2f, magazine = 6, reload = 2f, recoil = 8,
                        spreadDegrees = 14, range = 14, reloadStyle = ReloadStyle.Shells, description = "Seven pellets. Clears crabs and claws, weak at range. Reload: shell by shell." },
                new() { id = "harpoon", displayName = "Harpoon Gun", model = "HarpoonGun", price = 600, damage = 60, fireRate = .9f, magazine = 3, reload = 1.6f, spreadDegrees = 0, recoil = 6,
                        pierce = 99, knockback = 6, reloadStyle = ReloadStyle.Heavy, description = "Skewers a whole line and shoves it back into the surf. Reload: one narrow chance." },
                new() { id = "longrifle", displayName = "Longrifle", model = "Longrifle", price = 750, damage = 75, fireRate = .8f, magazine = 5, reload = 2f, spreadDegrees = 0, recoil = 7,
                        range = 60, pierce = 1, reloadStyle = ReloadStyle.Bolt, description = "Long range, goes through one. Reload: work the bolt." },
                new() { id = "flare", displayName = "Flare Gun", model = "FlareGun", price = 450, damage = 15, fireRate = 1.2f, magazine = 4, reload = 1.8f, spreadDegrees = 1, recoil = 4.5f,
                        splash = 2f, element = Element.Fire, reloadStyle = ReloadStyle.Drop, description = "Sets dry targets alight. Wet ones just steam. Reload: catch the moving zone." },
                new() { id = "eelcaster", displayName = "Eel Caster", model = "EelCaster", price = 850, damage = 18, fireRate = 4, magazine = 10, reload = 2.2f, spreadDegrees = 2, recoil = 2.5f,
                        range = 22, splash = 1.2f, element = Element.Shock, reloadStyle = ReloadStyle.Mash, description = "Shocking bolts that crackle over a crowd. Reload: mash to charge." },
                new() { id = "blowtorch", displayName = "Blowtorch", model = "Blowtorch", price = 700, damage = 5, pellets = 4, fireRate = 9, magazine = 40, reload = 2f, spreadDegrees = 9, recoil = .8f,
                        range = 8, element = Element.Fire, reloadStyle = ReloadStyle.Pressure, description = "Short, hot and hungry. Reload: hold, release in the green." },
            };

            melee = new List<MeleeDef>
            {
                new() { id = "spade", displayName = "Beach Spade", model = "Spade", price = 0, damage = 18, arcDegrees = 100, range = 1.8f, cooldown = .45f, knockback = 2,
                        style = MeleeStyle.Chop, windup = .12f, strike = .1f, recover = .2f, description = "Everyone starts somewhere." },
                new() { id = "cutlass", displayName = "Cutlass", model = "Cutlass", price = 250, damage = 24, arcDegrees = 90, range = 2f, cooldown = .32f, knockback = 1.5f,
                        style = MeleeStyle.Slash, windup = .06f, strike = .08f, recover = .14f, description = "Fast slashes. Best for lopping off limbs." },
                new() { id = "oar", displayName = "Oar", model = "Oar", price = 300, damage = 20, arcDegrees = 160, range = 2.6f, cooldown = .7f, knockback = 5,
                        style = MeleeStyle.Sweep, windup = .16f, strike = .14f, recover = .24f, description = "Wide sweep that knocks a crowd back." },
                new() { id = "boathook", displayName = "Boat Hook", model = "BoatHook", price = 400, damage = 22, arcDegrees = 30, range = 3.4f, cooldown = .6f, knockback = 0,
                        pull = 3, element = Element.Shock, style = MeleeStyle.Thrust, windup = .08f, strike = .1f, recover = .22f, description = "Long reach. Hooks a target toward you with a jolt (great on wet ones)." },
                new() { id = "anchor", displayName = "Anchor", model = "Anchor", price = 700, damage = 70, arcDegrees = 120, range = 2.4f, cooldown = 1.3f, knockback = 8,
                        stun = 1f, style = MeleeStyle.Slam, windup = .38f, strike = .14f, recover = .38f, description = "Slow, crushing, stuns. Cracks shells." },
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
                new() { id = "repair", displayName = "Repair Wall", kind = UpgradeKind.RepairWall, price = 40, priceGrowth = 1, amount = .4f,
                        description = "Restore 40% of every wall section, and rebuild breached ones." },
                new() { id = "reinforce", displayName = "Reinforce Wall", kind = UpgradeKind.WallMaxHp, price = 80, maxLevel = 8, amount = .2f, description = "+20% HP for every wall section." },
                new() { id = "coolcore", displayName = "Cool the Core", kind = UpgradeKind.PlantRepair, price = 60, priceGrowth = 1.15f, amount = .25f,
                        description = "Pour sea water on the reactor: +25% powerplant HP." },
                new() { id = "leadlining", displayName = "Lead Lining", kind = UpgradeKind.PlantMaxHp, price = 120, maxLevel = 6, amount = .2f, description = "+20% powerplant max HP." },
                new() { id = "vitality", displayName = "Sea Legs", kind = UpgradeKind.PlayerMaxHp, price = 70, priceGrowth = 1.4f, maxLevel = 6, amount = 25, description = "+25 max HP for every player." },
                new() { id = "gundmg", displayName = "Heavier Rounds", kind = UpgradeKind.GunDamage, price = 90, maxLevel = 8, amount = .15f, description = "+15% gun damage." },
                new() { id = "meleedmg", displayName = "Whetstone", kind = UpgradeKind.MeleeDamage, price = 70, maxLevel = 8, amount = .15f, description = "+15% melee damage." },
                new() { id = "pockets", displayName = "Deep Pockets", kind = UpgradeKind.Magazine, price = 80, maxLevel = 4, amount = .3f, description = "+30% magazine size." },
                new() { id = "gascans", displayName = "Gas Delivery", kind = UpgradeKind.GasCans, price = 50, priceGrowth = 1.6f, maxLevel = 4, amount = 2,
                        description = "+2 gas cans dropped on the beach each morning." },
            };

            stats = new List<StatDef>
            {
                new() { id = "vigor", displayName = "Vigor", kind = StatKind.MaxHp, perPoint = 12, description = "+12 max HP per point." },
                new() { id = "brawn", displayName = "Brawn", kind = StatKind.MeleeDamage, perPoint = .08f, description = "+8% melee damage per point." },
                new() { id = "aim", displayName = "Aim", kind = StatKind.GunDamage, perPoint = .06f, description = "+6% gun damage per point." },
                new() { id = "swift", displayName = "Swiftness", kind = StatKind.MoveSpeed, perPoint = .04f, description = "+4% move speed and faster dashes per point." },
                new() { id = "hands", displayName = "Quick Hands", kind = StatKind.Handling, perPoint = .07f, description = "+7% reload speed and +3.5% fire rate per point." },
                new() { id = "fortune", displayName = "Fortune", kind = StatKind.Fortune, perPoint = .08f, description = "+8% sand dollars you pick up and +4% XP per point." },
            };

            mods = new List<ModDef>
            {
                // guns
                new() { id = "extmag", displayName = "Extended Mag", gun = true, magazine = 1.5f, description = "+50% magazine." },
                new() { id = "hairtrigger", displayName = "Hair Trigger", gun = true, fireRate = 1.25f, description = "+25% fire rate." },
                new() { id = "speedloader", displayName = "Speed Loader", gun = true, reload = .7f, description = "Reloads 30% faster." },
                new() { id = "barbed", displayName = "Barbed Rounds", gun = true, pierce = 1, description = "Shots pierce one more creature." },
                new() { id = "incendiary", displayName = "Incendiary Rounds", gun = true, setElement = true, element = Element.Fire, description = "Shots set dry targets alight." },
                new() { id = "livewire", displayName = "Live Wire", gun = true, setElement = true, element = Element.Shock, description = "Shots shock (x1.5 and chain on wet targets)." },
                new() { id = "blastcaps", displayName = "Blast Caps", gun = true, splash = 1.5f, description = "Shots burst in a small splash." },
                new() { id = "slugs", displayName = "Heavy Slugs", gun = true, damage = 1.3f, fireRate = .85f, description = "+30% damage, -15% fire rate." },
                new() { id = "leech", displayName = "Leech Rounds", gun = true, lifesteal = .08f, description = "Heal 8% of the damage you deal." },
                new() { id = "buckshot", displayName = "Buckshot", gun = true, pellets = 2, spread = 4, description = "+2 pellets per shot, a little wider." },
                // melee
                new() { id = "serrated", displayName = "Serrated Edge", gun = false, limbBonus = .6f, description = "+60% damage to limbs." },
                new() { id = "barnacles", displayName = "Barnacle-Crusted", gun = false, stun = .5f, description = "Hits stun for half a second." },
                new() { id = "riptide", displayName = "Riptide Edge", gun = false, knockback = 4, description = "Hits knock creatures back hard." },
                new() { id = "longhandle", displayName = "Long Handle", gun = false, range = 1.3f, description = "+30% reach." },
                new() { id = "wideswing", displayName = "Wide Swing", gun = false, arc = 1.35f, description = "+35% swing arc." },
                new() { id = "heavyhead", displayName = "Heavy Head", gun = false, damage = 1.35f, cooldown = 1.2f, description = "+35% damage, swings 20% slower." },
                new() { id = "eelgrip", displayName = "Eel-Skin Grip", gun = false, cooldown = .75f, description = "Swings 25% faster." },
                new() { id = "jellycoil", displayName = "Jellyfish Coil", gun = false, setElement = true, element = Element.Shock, description = "Swings shock (great on wet creatures)." },
                new() { id = "vampsquid", displayName = "Vampire Squid", gun = false, lifesteal = .12f, description = "Heal 12% of the melee damage you deal." },
            };

            // The 2D game's reload minigames (its RELOADS table). {r} is the player's reload button.
            reloads = new List<ReloadDef>
            {
                new() { style = ReloadStyle.Zone, displayName = "Active", how = "Tap {r} in the green", zone = new(.42f, .6f), jam = .6f },
                new() { style = ReloadStyle.Rhythm, displayName = "Rhythm", how = "Tap {r} on the beat", perRound = true, beat = .2f, window = .07f, fumble = .35f },
                new() { style = ReloadStyle.Shells, displayName = "Shell by shell", how = "Tap {r} in the green · fire to stop", perRound = true, zone = new(.55f, .85f), slip = .25f },
                new() { style = ReloadStyle.Bolt, displayName = "Bolt", how = "Tap {r} in each zone", zone = new(.22f, .36f), zone2 = new(.62f, .76f), jam = .5f },
                new() { style = ReloadStyle.Heavy, displayName = "Heavy", how = "One chance · {r} in the gold", zone = new(.3f, .4f), jam = 1.4f, bonus = .4f },
                new() { style = ReloadStyle.Drop, displayName = "Drop", how = "Tap {r} in the green", width = .16f, jam = .6f },
                new() { style = ReloadStyle.Mash, displayName = "Capacitor", how = "Mash {r}", perTap = .09f, perfectUnder = .55f },
                new() { style = ReloadStyle.Pressure, displayName = "Pressure", how = "Hold {r} · release in the green", zone = new(.62f, .82f), fill = .9f, jam = .8f },
            };

            structures = new List<StructureDef>
            {
                new() { id = "barricade", displayName = "Sandbag Barricade", model = "Barricade", kind = StructureKind.Barricade, sandDollars = 40, hp = 260, radius = 1.6f,
                        description = "Blocks creatures. They have to chew through it." },
                new() { id = "spikes", displayName = "Urchin Spikes", model = "Spikes", kind = StructureKind.Spikes, sandDollars = 60, hp = 160, radius = 1.2f, damage = 14,
                        description = "Hurts anything that walks over it (wears down as it does)." },
                new() { id = "snare", displayName = "Kelp Snare", model = "KelpSnare", kind = StructureKind.Snare, sandDollars = 50, hp = 200, radius = 1.4f, slow = .5f,
                        description = "Tangles creatures: half speed while they cross it." },
                new() { id = "bait", displayName = "Bait Bucket", model = "BaitBucket", kind = StructureKind.Bait, sandDollars = 30, hp = 90, radius = .6f, range = 11,
                        description = "Creatures nearby go for the fish heads instead of you or the wall." },
                new() { id = "turret", displayName = "Harpoon Turret", model = "HarpoonTurret", kind = StructureKind.Turret, sandDollars = 120, pearls = 1, hp = 180, radius = .8f,
                        damage = 26, rate = .8f, range = 20, max = 4, description = "Fires harpoons at the nearest creature." },
                new() { id = "cannon", displayName = "Pearl Cannon", model = "PearlCannon", kind = StructureKind.Cannon, sandDollars = 200, pearls = 3, hp = 240, radius = 1.0f,
                        damage = 70, rate = .3f, range = 28, splash = 3.5f, max = 2, description = "Lobs exploding pearls into crowds." },
            };

            chefLines = new List<string> { "Hot dogs! Get 'em while the core's cool!", "Ketchup for rage, mustard for speed!", "??? sauce? Your funeral, pal.",
                                            "Nothing beats a glizzy before a big night.", "No refunds if you get turned upside down." };
            fishmongerLines = new List<string> { "Fresh catch! Bring me those corpses!", "Intact ones pay better, you know.", "Throw 'em in the cart, I'll count 'em!",
                                                  "Is that a clam? Bring me a clam!", "Smells like a good night's work." };

            cards = new List<TidalCardDef>
            {
                new() { id = "redtide", displayName = "Red Tide", waves = 3, enemyHp = 1.4f, sandDollars = 1.6f, color = new Color(.85f, .2f, .2f),
                        description = "Creatures +40% HP. Sand dollars +60%." },
                new() { id = "kingtide", displayName = "King Tide", waves = 2, breakerReach = 14, xp = 1.5f, color = new Color(.2f, .5f, .95f),
                        description = "Breakers carry creatures far up the beach. +50% XP." },
                new() { id = "riptide", displayName = "Riptide", waves = 3, enemySpeed = 1.25f, playerSpeed = 1.15f, color = new Color(.2f, .75f, .8f),
                        description = "Creatures 25% faster. You're 15% faster too." },
                new() { id = "downpour", displayName = "Downpour", waves = 3, alwaysWet = true, sandDollars = 1.3f, fogDensity = .004f, color = new Color(.45f, .55f, .7f),
                        description = "Everything stays soaked: shock hits hard, fire fizzles. Sand dollars +30%." },
                new() { id = "fogbank", displayName = "Fog Bank", waves = 2, fogDensity = .02f, enemyHp = .8f, color = new Color(.7f, .72f, .75f),
                        description = "Thick fog rolls in. Creatures -20% HP." },
                new() { id = "frenzy", displayName = "Feeding Frenzy", waves = 3, budget = 1.3f, corpseValue = 1.5f, color = new Color(.6f, .1f, .15f),
                        description = "30% more creatures. Corpses sell for 50% more." },
                new() { id = "calm", displayName = "Calm Waters", waves = 2, budget = .7f, sandDollars = .7f, xp = .7f, color = new Color(.5f, .85f, .7f),
                        description = "30% fewer creatures, 30% less reward. The safe pick." },
                new() { id = "glasscannon", displayName = "Glass Cannon", waves = 3, playerDamage = 1.5f, playerMaxHp = .6f, color = new Color(.95f, .6f, .2f),
                        description = "+50% damage. -40% max HP." },
                new() { id = "goldrush", displayName = "Gold Rush", waves = 2, sandDollars = 2, sandDollarLifetime = 8, color = new Color(.95f, .8f, .2f),
                        description = "Double sand dollars, but they wash away after 8 seconds." },
                new() { id = "bloom", displayName = "Plankton Bloom", waves = 3, xp = 2, enemyDamage = 1.2f, color = new Color(.4f, .95f, .6f),
                        description = "Double XP. Creatures hit 20% harder." },
                // Online only
                new() { id = "buddybeam", displayName = "Buddy Beam", waves = 3, exclusive = CardExclusive.Online, budget = 1.2f, special = "buddybeam", color = new Color(.3f, 1f, 1f),
                        description = "ONLINE ONLY. A laser links teammates playing on different computers. Creatures that cross it burn. 20% more creatures." },
                new() { id = "bottle", displayName = "Message in a Bottle", waves = 3, exclusive = CardExclusive.Online, special = "bottle", color = new Color(.6f, .9f, .5f),
                        description = "ONLINE ONLY. Every wave, each player gets a condiment boost sent by a teammate on another computer." },
                // Split screen only
                new() { id = "screenswap", displayName = "Screen Swap", waves = 2, exclusive = CardExclusive.SplitScreen, special = "screenswap", color = new Color(.95f, .4f, .9f),
                        description = "SPLIT SCREEN ONLY. Every 20 seconds your viewports shuffle. +40% damage for everyone sharing a screen." },
                new() { id = "snacks", displayName = "Shared Snacks", waves = 3, exclusive = CardExclusive.SplitScreen, special = "snacks", color = new Color(1f, .7f, .4f),
                        description = "SPLIT SCREEN ONLY. Food heals everyone on your screen, and you revive each other twice as fast." },
            };
        }
    }
}

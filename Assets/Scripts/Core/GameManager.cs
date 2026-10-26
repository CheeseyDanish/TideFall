// TideWall 3D — the root object. Boots itself into any scene (no scene setup required), builds the world,
// and owns the shared state: coins, team upgrades, game state, pause.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TideWall
{
    public enum GameState { Lobby, Playing, GameOver }

    public class GameManager : MonoBehaviour
    {
        public static GameManager I { get; private set; }
        /// <summary>Every living enemy (dead ones become corpses and leave this list).</summary>
        public static readonly List<Enemy> Enemies = new();

        public GameDatabase DB { get; private set; }
        public PhaseCycle Cycle { get; private set; }
        public GameState State { get; private set; } = GameState.Lobby;
        public bool Paused { get; private set; }
        public int Coins { get; private set; }
        public readonly TeamStats Team = new();
        public World World { get; private set; }
        public Wall Wall => World.wall;
        public PhaseManager Phases { get; private set; }
        public WaveDirector Waves { get; private set; }
        public PlayerManager Players { get; private set; }
        public string GameOverReason { get; private set; }

        /// <summary>(title, subtitle): big centre-screen message for every player.</summary>
        public event Action<string, string> OnBanner;
        /// <summary>(delta, where or null, reason): coin changes, for floating "+12" popups.</summary>
        public event Action<int, Vector3?, string> OnCoins;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindAnyObjectByType<GameManager>() == null) new GameObject("TideWall").AddComponent<GameManager>();
        }

        void Awake()
        {
            if (I != null && I != this) { Destroy(gameObject); return; }
            I = this;
            Enemies.Clear();
            Interactable.All.Clear();
            Time.timeScale = 1;
            DB = GameDatabase.Load();
            Cycle = PhaseCycle.Load();
            Coins = DB.startCoins;
            // The scene's own camera/sun are replaced by ours (players get their own cameras).
            foreach (var c in FindObjectsByType<Camera>(FindObjectsInactive.Include)) Destroy(c.gameObject);
            foreach (var l in FindObjectsByType<Light>(FindObjectsInactive.Include))
                if (l.type == LightType.Directional) Destroy(l.gameObject);
            gameObject.AddComponent<AudioListener>();
            Fx.Init(transform);
            Sfx.Init(gameObject);
            World = WorldBuilder.Build(transform);
            Phases = gameObject.AddComponent<PhaseManager>();
            Waves = gameObject.AddComponent<WaveDirector>();
            Players = gameObject.AddComponent<PlayerManager>();
        }

        void OnDestroy() { if (I == this) I = null; }

        void Update() => Fx.Tick(Time.deltaTime);

        /// <summary>Called when the first player joins.</summary>
        public void StartGame()
        {
            if (State != GameState.Lobby) return;
            State = GameState.Playing;
            Phases.Begin();
        }

        public void Banner(string title, string subtitle) => OnBanner?.Invoke(title, subtitle);

        public void AddCoins(int amount, Vector3? where = null, string reason = null)
        {
            if (amount == 0) return;
            Coins += amount;
            OnCoins?.Invoke(amount, where, reason);
            if (amount > 0) Sfx.Play("coin", .6f, .06f);
        }

        public bool TrySpend(int amount)
        {
            if (Coins < amount) { Sfx.Play("deny"); return false; }
            Coins -= amount;
            OnCoins?.Invoke(-amount, null, null);
            return true;
        }

        public void TogglePause()
        {
            if (State != GameState.Playing) return;
            Paused = !Paused;
            Time.timeScale = Paused ? 0 : 1;
        }

        public void GameOver(string reason)
        {
            if (State != GameState.Playing) return;
            State = GameState.GameOver;
            GameOverReason = reason;
            Paused = false;
            Time.timeScale = .25f;   // slow-motion fall; HUDs show the game-over panel
            Sfx.Play("horn");
            Banner("THE WALL HAS FALLEN", reason);
        }

        public void Restart()
        {
            Time.timeScale = 1;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }

    /// <summary>Team-wide upgrade levels bought at the shop, and the multipliers they give.</summary>
    public class TeamStats
    {
        readonly Dictionary<string, int> levels = new();
        public float GunDamage = 1, MeleeDamage = 1, Magazine = 1, PlayerHpBonus;
        public int ExtraGasCans;

        public int Level(string id) => levels.TryGetValue(id, out var l) ? l : 0;
        public int Price(UpgradeDef u) => Mathf.RoundToInt(u.price * Mathf.Pow(u.priceGrowth, Level(u.id)));
        public bool Maxed(UpgradeDef u) => u.maxLevel > 0 && Level(u.id) >= u.maxLevel;

        /// <summary>Why this upgrade can't be bought right now, or null if it can.</summary>
        public string Blocked(UpgradeDef u)
        {
            if (Maxed(u)) return "Maxed";
            if (u.kind == UpgradeKind.RepairWall && GameManager.I.Wall.Hp >= GameManager.I.Wall.MaxHp) return "Wall is full";
            return null;
        }

        public void Apply(UpgradeDef u)
        {
            levels[u.id] = Level(u.id) + 1;
            var gm = GameManager.I;
            switch (u.kind)
            {
                case UpgradeKind.RepairWall: gm.Wall.Heal(gm.Wall.MaxHp * u.amount); break;
                case UpgradeKind.WallMaxHp: gm.Wall.Grow(1 + u.amount); break;
                case UpgradeKind.PlayerMaxHp: PlayerHpBonus += u.amount; foreach (var p in gm.Players.All) p.RefreshMaxHp(); break;
                case UpgradeKind.GunDamage: GunDamage += u.amount; break;
                case UpgradeKind.MeleeDamage: MeleeDamage += u.amount; break;
                case UpgradeKind.Magazine: Magazine += u.amount; break;
                case UpgradeKind.GasCans: ExtraGasCans += Mathf.RoundToInt(u.amount); break;
            }
        }
    }
}

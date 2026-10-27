// TideWall 3D — the root object. Boots itself into any scene (no scene setup required), builds the world,
// and owns the shared state: sand dollars (the money), team upgrades, game state, pause, the current save slot.
// It boots into the main menu. A run starts from the menu (new run, continue, host) or when a player joins
// directly (tests, capture tour). Runs are roguelike: when the powerplant melts down (or everyone falls), the run
// is over and its save is deleted; the Legacy record keeps the best. Online clients mirror the host (see Net/).
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace TideWall
{
    public enum GameState { MainMenu, Playing, GameOver }

    /// <summary>A run to start right after the scene (re)loads: "retry from save", or a host restarting for everyone.</summary>
    public class RunRequest
    {
        public int slot;
        public SaveData save;
        public List<InputDevice[]> devices = new();   // local players to re-join, in order
    }

    public class GameManager : MonoBehaviour
    {
        public static GameManager I { get; private set; }
        /// <summary>Every living enemy (dead ones become corpses and leave this list).</summary>
        public static readonly List<Enemy> Enemies = new();
        public static RunRequest Pending;

        public GameDatabase DB { get; private set; }
        public PhaseCycle Cycle { get; private set; }
        public Campaign Campaign { get; private set; }
        public GameState State { get; private set; } = GameState.MainMenu;
        public bool Paused { get; private set; }
        public int SandDollars { get; private set; }
        /// <summary>Golden pearls: rare (giant clams drop them), spent on the best defences.</summary>
        public int Pearls { get; private set; }
        public readonly TeamStats Team = new();
        /// <summary>This scene's level: Part I's World (the town and beach) or a Part II Expedition stage.</summary>
        public Level Level { get; private set; }
        public World World => Level as World;
        public Expedition Expedition => Level as Expedition;
        /// <summary>Part II: attacking on the seabed rather than defending the wall.</summary>
        public bool Assault => Level is Expedition;
        public Wall Wall => World != null ? World.wall : null;
        public PowerPlant Plant => World != null ? World.plant : null;
        /// <summary>The scene with the main menu (Part I's town).</summary>
        public const string MenuScene = "TideWall";
        public TidalCards Cards { get; private set; }
        /// <summary>The record this run's game over produced (null until then).</summary>
        public Legacy RunRecord { get; private set; }
        public PhaseManager Phases { get; private set; }
        public WaveDirector Waves { get; private set; }
        public PlayerManager Players { get; private set; }
        public MainMenu Menu { get; private set; }
        public string GameOverReason { get; private set; }
        /// <summary>Save slot of this run (0 = not saving, e.g. tests and online clients).</summary>
        public int SaveSlot { get; private set; }
        /// <summary>The save this run started from (player loadouts are restored from it as players join).</summary>
        public SaveData Loaded { get; private set; }
        /// <summary>Enemies left this phase (a client reads the host's count).</summary>
        public int EnemiesLeft => Net.IsClient ? netEnemiesLeft : Enemies.Count;
        int netEnemiesLeft;

        /// <summary>(title, subtitle): big centre-screen message for every player.</summary>
        public event Action<string, string> OnBanner;
        /// <summary>(delta, where or null, reason): sand dollar changes, for floating "+12" popups.</summary>
        public event Action<int, Vector3?, string> OnSandDollars;

        /// <summary>Everything the game loads (data, materials, models, sounds). Set in the TideWall scene.</summary>
        [SerializeField] ContentLibrary content;

        void Awake()
        {
            if (I != null && I != this) { Destroy(gameObject); return; }
            I = this;
            Enemies.Clear();
            NetId.Reset();
            Content.Use(content);
            Time.timeScale = 1;
            DB = GameDatabase.Load();
            Cycle = PhaseCycle.Load();
            Campaign = Campaign.Load();
            SandDollars = DB.startSandDollars;
            gameObject.AddComponent<AudioListener>();
            Settings.Changed(save: false);
            Fx.Init(transform);
            Sfx.Init(gameObject);
            Cards = gameObject.AddComponent<TidalCards>();
            // The level is saved in the scene (TideWall → Rebuild World In Scene / Rebuild Stages regenerate them)
            Level = FindAnyObjectByType<Level>();
            if (Level == null)
            {
                Debug.LogError("TideWall: this scene has no level. Building Part I's for now; open the TideWall scene or use TideWall → Rebuild World In Scene.");
                Level = WorldBuilder.Build(null);
            }
            Arena.Seabed = Assault;
            Level.Init(DB);
            Phases = gameObject.AddComponent<PhaseManager>();
            Waves = gameObject.AddComponent<WaveDirector>();
            Players = gameObject.AddComponent<PlayerManager>();
            if (World != null) Menu = gameObject.AddComponent<MainMenu>();   // the main menu lives in Part I's keep
        }

        void Start()
        {
            // After a reload: a client keeps mirroring its host; a host or a "retry" starts its run straight away.
            if (Net.IsClient) { BeginClient(); return; }
            var req = Pending; Pending = null;
            // A Part II stage opened on its own (the editor, tests): an unsaved expedition starting here
            if (req == null && Assault) req = new RunRequest { save = Expedition.NewSave(this, 0, Expedition.StageIndex) };
            if (req == null) return;
            StartRun(req.save, req.slot);
            foreach (var devs in req.devices) if (Array.TrueForAll(devs, d => d != null && d.added)) Players.Join(devs, quiet: true);   // the same sailors, carried over
            if (Net.IsHost) Net.Host.Rebind(this);
        }

        void OnDestroy() { if (I == this) I = null; }

        void Update()
        {
            Fx.Tick(Time.deltaTime);
            // Pause: offline, any local player with the pause menu open stops time. Online the world keeps going.
            bool pause = State == GameState.Playing && !Net.Online && Players.AnyPauseMenuOpen;
            if (pause != Paused) { Paused = pause; Time.timeScale = pause ? 0 : 1; }
        }

        // ------------------------------------------------------------------ runs
        /// <summary>Start a run (offline or as host). save = null starts a new game; slot 0 = don't save.</summary>
        public void StartRun(SaveData save, int slot)
        {
            if (State != GameState.MainMenu) return;
            SaveSlot = slot;
            Loaded = save;
            State = GameState.Playing;
            Menu?.Hide();
            if (Assault)
            {
                // Part II: the team, its money and its upgrades come with the save from the last stage
                if (save != null) { SandDollars = save.sandDollars; Pearls = save.pearls; Team.Restore(save); }
                Expedition.Begin(save);
                return;
            }
            if (save != null && save.part != "wall")
            {
                // A Part II save opened from the main menu: go to its stage
                Load(Expedition.SceneFor(Campaign, save), new RunRequest { save = save, slot = slot });
                return;
            }
            if (save != null)
            {
                SandDollars = save.sandDollars;
                Pearls = save.pearls;
                Waves.RunWave = save.runWave;
                foreach (var st in save.structures) if (DB.Structure(st.id) is { } def) Structure.Build(def, new Vector3(st.x, 0, st.z), st.yaw, st.hp);
                Team.Restore(save);
                Wall.Restore(save.wallSections, save.wallSectionMax);
                if (save.plantMaxHp > 0) Plant.Restore(save.plantHp, save.plantMaxHp);
                Cards.Restore(save.cards);
                Phases.SetDay(save.day);
                Phases.Enter(save.phaseIndex);
            }
            else
            {
                Phases.Begin();
                // The very first run: Part I's opening words
                if (Legacy.Load().runs == 0) StoryCard.Part(Campaign.parts.Find(p => p.kind == PartKind.Defense));
            }
        }

        /// <summary>A client mirrors the host: no simulation, state arrives in snapshots.</summary>
        public void BeginClient()
        {
            State = GameState.Playing;
            SaveSlot = 0;
            Menu?.Hide();
        }

        /// <summary>Called by the first player to join while the main menu is up (tests, capture): a new, unsaved run.</summary>
        public void StartGame() { if (State == GameState.MainMenu) StartRun(null, 0); }

        /// <summary>Write the save for this run now (offline or host only).</summary>
        public bool SaveNow()
        {
            if (SaveSlot <= 0 || Net.IsClient || State != GameState.Playing) return false;
            var d = SaveSystem.Capture(this);
            Loaded = d;
            return SaveSystem.Write(d);
        }

        public bool CanSaveNow => SaveSlot > 0 && !Net.IsClient && State == GameState.Playing && !Assault && Phases.Current != null && Phases.Current.saveOnStart;

        public void Banner(string title, string subtitle)
        {
            OnBanner?.Invoke(title, subtitle);
            if (Net.IsHost) Net.Host.Banner(title, subtitle);
        }

        public void AddSandDollars(int amount, Vector3? where = null, string reason = null)
        {
            if (amount == 0) return;
            SandDollars += amount;
            OnSandDollars?.Invoke(amount, where, reason);
            if (amount > 0) Sfx.Play("coin", .6f, .06f);
        }

        public void AddPearls(int amount, Vector3? where = null)
        {
            if (amount == 0) return;
            Pearls += amount;
            if (amount > 0) Sfx.Play("jackpot", .45f, .1f);
        }

        /// <summary>Spend sand dollars and golden pearls together (both or neither).</summary>
        public bool TrySpend(int sandDollars, int pearls)
        {
            if (SandDollars < sandDollars || Pearls < pearls) { Sfx.Play("deny"); return false; }
            Pearls -= pearls;
            return TrySpend(sandDollars);
        }

        public bool TrySpend(int amount)
        {
            if (SandDollars < amount) { Sfx.Play("deny"); return false; }
            SandDollars -= amount;
            OnSandDollars?.Invoke(-amount, null, null);
            return true;
        }

        public void GameOver(string reason)
        {
            if (State != GameState.Playing) return;
            State = GameState.GameOver;
            GameOverReason = reason;
            Time.timeScale = .25f;   // slow-motion fall; HUDs show the game-over panel
            Sfx.Play("horn");
            Banner("RUN OVER", reason);
            // Roguelike: the run's save goes with it; the legacy record keeps the best
            if (SaveSlot > 0) SaveSystem.Delete(SaveSlot);
            int level = 0, kills = 0;
            foreach (var p in Players.All) { level = Mathf.Max(level, p.Level); kills += p.Kills; }
            RunRecord = Assault ? Legacy.RecordExpedition(Expedition.StageIndex, level, kills) : Legacy.Record(Phases.Day, level, kills);
            if (Net.IsHost) Net.Host.SendGameOver(reason);
        }

        /// <summary>Client: the host says the run is over.</summary>
        public void NetGameOver(string reason)
        {
            if (State != GameState.Playing) return;
            State = GameState.GameOver; GameOverReason = reason;
        }

        /// <summary>Game over → a fresh run in the same slot, with the same players (and the same online group).</summary>
        public void NewRun()
        {
            if (Net.IsClient) return;   // the host decides
            var req = new RunRequest { slot = SaveSlot, save = null };
            foreach (var p in Players.All) if (p.Controls != null) req.devices.Add(p.Controls.Devices);
            // Part II starts over from its first stage
            string scene = SceneManager.GetActiveScene().name;
            if (Assault) { req.save = Expedition.NewSave(this, SaveSlot, 0); scene = Expedition.SceneFor(Campaign, req.save); }
            Load(scene, req);
        }

        /// <summary>Back to the main menu (saving first if this phase allows it). Leaves any online session.</summary>
        public void QuitToMenu()
        {
            if (CanSaveNow) SaveNow();
            Net.Shutdown(Net.IsHost ? "The host closed the game." : null);
            Pending = null;
            Load(MenuScene, null);
        }

        /// <summary>Go to a scene, starting a run there with `req` (the players and the save to bring). An online host
        /// takes its clients along.</summary>
        public static void Load(string scene, RunRequest req)
        {
            Pending = req;
            if (Net.IsHost) Net.Host.SendReload(scene);
            Time.timeScale = 1;
            SceneManager.LoadScene(scene);
        }

        /// <summary>The players on this machine, to bring into the next scene.</summary>
        public RunRequest Carry(SaveData save, int slot)
        {
            var req = new RunRequest { slot = slot, save = save };
            foreach (var p in Players.All) if (p.Controls != null) req.devices.Add(p.Controls.Devices);
            return req;
        }

        public static void Reload()
        {
            Time.timeScale = 1;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        /// <summary>Client: the host went to another scene; follow it.</summary>
        public static void ReloadTo(string scene)
        {
            Time.timeScale = 1;
            if (string.IsNullOrEmpty(scene) || !Application.CanStreamedLevelBeLoaded(scene)) { Reload(); return; }
            SceneManager.LoadScene(scene);
        }

        // ------------------------------------------------------------------ client mirror
        public void ApplyNet(int sandDollars, int pearls, int enemiesLeft)
        {
            Pearls = pearls;
            if (sandDollars != SandDollars) OnSandDollars?.Invoke(sandDollars - SandDollars, null, null);
            SandDollars = sandDollars;
            netEnemiesLeft = enemiesLeft;
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
            bool home = u.kind is UpgradeKind.RepairWall or UpgradeKind.WallMaxHp or UpgradeKind.PlantRepair or UpgradeKind.PlantMaxHp;
            if (home && GameManager.I.Assault) return "The wall is far behind you";
            if (u.kind == UpgradeKind.RepairWall && !GameManager.I.Wall.Damaged) return "The wall is in one piece";
            if (u.kind == UpgradeKind.PlantRepair && GameManager.I.Plant.Hp >= GameManager.I.Plant.MaxHp - .5f) return "The core is cool";
            return null;
        }

        public void Apply(UpgradeDef u)
        {
            levels[u.id] = Level(u.id) + 1;
            var gm = GameManager.I;
            switch (u.kind)
            {
                case UpgradeKind.RepairWall: gm.Wall.Repair(u.amount); break;
                case UpgradeKind.PlantRepair: gm.Plant.Repair(u.amount); break;
                case UpgradeKind.PlantMaxHp: gm.Plant.Grow(1 + u.amount); break;
                case UpgradeKind.WallMaxHp: gm.Wall.Grow(1 + u.amount); break;
                case UpgradeKind.PlayerMaxHp: PlayerHpBonus += u.amount; foreach (var p in gm.Players.All) p.Heal(u.amount); break;
                case UpgradeKind.GunDamage: GunDamage += u.amount; break;
                case UpgradeKind.MeleeDamage: MeleeDamage += u.amount; break;
                case UpgradeKind.Magazine: Magazine += u.amount; break;
                case UpgradeKind.GasCans: ExtraGasCans += Mathf.RoundToInt(u.amount); break;
            }
        }

        /// <summary>Put back the levels and multipliers from a save (the wall is restored separately).</summary>
        public void Restore(SaveData d)
        {
            levels.Clear();
            foreach (var u in d.upgrades) levels[u.id] = u.level;
            GunDamage = d.gunDamage; MeleeDamage = d.meleeDamage; Magazine = d.magazine;
            PlayerHpBonus = d.playerHpBonus; ExtraGasCans = d.extraGasCans;
        }
    }
}

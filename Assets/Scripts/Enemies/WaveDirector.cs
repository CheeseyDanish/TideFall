// TideWall 3D — turns a phase's wave settings into spawns. The day's budget (GameDatabase.Budget) is spent on
// packs of enemies, split into waves. Same rule as the 2D game: once half of the current wave is dead, the next
// wave arrives a few seconds later instead of at its planned time.
// Creatures arrive riding breakers: spawns due within a moment of each other share one wave rolling in from sea.
// Active tidal cards scale the numbers (budget) as spawns come due, and count down as waves finish.
// RunWave counts waves across the whole run: some creatures (gulls) and guns only arrive after a few waves.
// Preview tells the HUD what's coming; in calm phases it's what the next fight's tide may bring.
using System.Collections.Generic;
using UnityEngine;

namespace TideWall
{
    public class WaveDirector : MonoBehaviour
    {
        class Entry { public EnemyDef def; public float t; public int wave; public bool boss; }

        readonly List<Entry> queue = new();
        int qi, wave, waveCount, day;
        float waveClock, nextWaveIn, slice;
        int[] totals = new int[0], killed = new int[0];
        bool[] rushed = new bool[0];
        public int Alive { get; private set; }
        public int Wave => wave;
        public int WaveCount => waveCount;
        public float Progress => queue.Count == 0 ? 1 : qi / (float)queue.Count;
        /// <summary>Waves started this run (1 = the first wave). Saved with the run.</summary>
        public int RunWave { get; set; }
        /// <summary>How tough creatures spawned now are (Part I: the current day; Part II: the stage's).</summary>
        public int Day { get => day; set => day = Mathf.Max(1, value); }
        /// <summary>What's coming, for the HUD ("Next: 4 Crabling, 2 Drowner"). Sent to online clients.</summary>
        public string Preview { get; private set; } = "";
        int runWaveAtPhaseStart;
        float previewT;
        /// <summary>True once every planned enemy has spawned and died.</summary>
        public bool Cleared => qi >= queue.Count && Alive <= 0;

        GameManager gm;
        void Awake() => gm = GetComponent<GameManager>();

        /// <summary>Client: show the host's wave counter.</summary>
        public void ApplyNet(int currentWave, int count, string preview) { wave = currentWave; waveCount = count; Preview = preview ?? ""; }

        public void StartPhase(PhaseDefinition p, int day)
        {
            this.day = day;
            queue.Clear(); qi = 0; wave = 0; waveClock = 0; Alive = 0;
            waveCount = Mathf.Max(0, p.waves);
            totals = new int[waveCount]; killed = new int[waveCount]; rushed = new bool[waveCount];
            runWaveAtPhaseStart = RunWave;
            if (p.corpseThieves > 0) SendThieves(p.corpseThieves);
            if (waveCount == 0) { UpdatePreview(); return; }
            RunWave++;   // the first wave of this phase starts now
            var db = gm.DB;
            slice = Mathf.Min(40, 18 + 2 * day);
            nextWaveIn = slice + db.waveGap;
            var pool = db.enemies.FindAll(e => e.cost > 0 && e.minDay <= day && e.behaviour != EnemyBehaviour.Boss &&
                                              (p.enemyPool.Count == 0 || p.enemyPool.Contains(e.id)));
            if (pool.Count == 0) pool = db.enemies.FindAll(e => e.cost > 0 && e.behaviour != EnemyBehaviour.Boss);
            float budget = db.Budget(day) * p.budgetMultiplier;
            int guard = 0;
            while (budget >= 1 && guard++ < 400)
            {
                int w = Random.Range(0, waveCount);
                int runWave = runWaveAtPhaseStart + w + 1;
                var allowed = pool.FindAll(e => e.minWave <= runWave);   // e.g. no gulls before the run's 5th wave
                if (allowed.Count == 0) allowed = pool;
                var def = WeightedPick(allowed);
                if (def.cost > budget) { if (allowed.TrueForAll(e => e.cost > budget)) break; continue; }
                int pack = def.spawnWeight < .5f ? 1 : def.cost <= 1 ? Random.Range(2, 5) : def.cost <= 3 ? Random.Range(1, 3) : 1;
                float t = Random.Range(.5f, slice);
                for (int k = 0; k < pack && budget >= def.cost; k++, budget -= def.cost)
                {
                    queue.Add(new Entry { def = def, t = t + k * .6f, wave = w });
                    totals[w]++;
                }
            }
            if (p.bossEveryDays > 0 && day % p.bossEveryDays == 0 && db.Enemy(p.bossId) is { } boss)
                queue.Add(new Entry { def = boss, t = 3, wave = waveCount - 1, boss = true });
            queue.Sort((a, b) => a.wave != b.wave ? a.wave.CompareTo(b.wave) : a.t.CompareTo(b.t));
            UpdatePreview();
        }

        /// <summary>The HUD's "what's coming" line: the rest of this wave and the next one, or what the next fight may bring.</summary>
        void UpdatePreview()
        {
            if (waveCount == 0)
            {
                // A calm phase: name the creatures the next fighting phase can send
                var cycle = gm.Cycle.phases;
                PhaseDefinition next = null;
                for (int i = 1; i <= cycle.Count && next == null; i++) { var ph = cycle[(gm.Phases.Index + i) % cycle.Count]; if (ph.waves > 0) next = ph; }
                if (next == null) { Preview = ""; return; }
                int nextDay = gm.Phases.Day + (gm.Phases.Current != null && gm.Phases.Current.advancesDay ? 1 : 0);
                var names = new List<string>();
                foreach (var e in gm.DB.enemies)
                    if (e.cost > 0 && e.minDay <= nextDay && e.minWave <= RunWave + next.waves && (next.enemyPool.Count == 0 || next.enemyPool.Contains(e.id)))
                        names.Add(e.displayName);
                Preview = names.Count > 0 ? $"{next.displayName} may bring: {string.Join(", ", names)}" : "";
                return;
            }
            Preview = $"{Describe(wave, qi)}" + (wave + 1 < waveCount ? $"   ·   Wave {wave + 2}: {Describe(wave + 1, 0)}" : "");
        }

        string Describe(int w, int from)
        {
            var counts = new Dictionary<string, int>();
            var order = new List<string>();
            for (int i = from; i < queue.Count; i++)
            {
                if (queue[i].wave != w) continue;
                var n = queue[i].boss ? $"<color=#fca5a5>{queue[i].def.displayName}</color>" : queue[i].def.displayName;
                if (!counts.ContainsKey(n)) { counts[n] = 0; order.Add(n); }
                counts[n]++;
            }
            if (order.Count == 0) return w == wave ? "all ashore" : "";
            var parts = new List<string>();
            foreach (var n in order) parts.Add($"{counts[n]} {n}");
            return (w == wave ? "Incoming: " : "") + string.Join(", ", parts);
        }

        /// <summary>Morning thieves: a few gulls fly in to snatch the night's corpses before you can sell them.</summary>
        void SendThieves(int count)
        {
            var gull = gm.DB.enemies.Find(e => e.behaviour == EnemyBehaviour.Flyer);
            if (gull == null) return;
            int corpses = Corpse.Live.Count;
            for (int i = 0; i < Mathf.Min(count, corpses); i++)
            {
                var g = Enemy.Spawn(gull, new Vector3(Random.Range(-40f, 40f), 12, Arena.ShoreZ + 25 + i * 4), day, -1);
                g.MakeThief();
            }
        }

        static EnemyDef WeightedPick(List<EnemyDef> pool)
        {
            // Cheap enemies are common, expensive ones rare.
            float total = 0;
            foreach (var e in pool) total += 3f / (1 + e.cost * .5f) * e.spawnWeight;
            float r = Random.value * total;
            foreach (var e in pool) { r -= 3f / (1 + e.cost * .5f) * e.spawnWeight; if (r <= 0) return e; }
            return pool[0];
        }

        void Update()
        {
            if (gm.State != GameState.Playing || waveCount == 0 || Net.IsClient) return;   // clients only show the host's counter
            float dt = Time.deltaTime;
            waveClock += dt;
            if ((previewT -= dt) <= 0) { previewT = .5f; UpdatePreview(); }
            while (qi < queue.Count && Alive < gm.DB.maxAlive &&
                   (queue[qi].wave < wave || (queue[qi].wave == wave && queue[qi].t <= waveClock)))
                Spawn(queue[qi++]);

            if (wave < waveCount - 1)
            {
                if (!rushed[wave] && totals[wave] > 0 && killed[wave] >= totals[wave] * gm.DB.waveRushFraction)
                {
                    rushed[wave] = true;
                    nextWaveIn = Mathf.Min(nextWaveIn, gm.DB.waveRushDelay);
                }
                nextWaveIn -= dt;
                if (nextWaveIn <= 0)
                {
                    bool wasRushed = rushed[wave];
                    wave++; waveClock = 0; nextWaveIn = slice + gm.DB.waveGap;
                    RunWave++;
                    gm.Cards.WaveFinished();
                    gm.Banner($"WAVE {wave + 1}/{waveCount}", wasRushed ? "Half the last wave fell. The next one rushes in!" : "The tide brings more.");
                    Sfx.Play("horn");
                }
            }
        }

        Breaker forming;

        void Spawn(Entry e)
        {
            // Tidal cards: fewer creatures (some never come) or more (some bring a friend)
            float budget = gm.Cards.Budget;
            if (!e.boss && budget < 1 && Random.value > budget) { if (e.wave >= 0 && e.wave < killed.Length) killed[e.wave]++; return; }
            SpawnOne(e.def, e.boss, e.wave);
            if (!e.boss && budget > 1 && Random.value < budget - 1)
            {
                if (e.wave >= 0 && e.wave < totals.Length) totals[e.wave]++;
                SpawnOne(e.def, false, e.wave);
            }
        }

        void SpawnOne(EnemyDef def, bool boss, int waveIndex)
        {
            var db = gm.DB;
            if (def.behaviour == EnemyBehaviour.Flyer)
            {
                // Gulls come in low over the water on their own
                Enemy.Spawn(def, new Vector3(Random.Range(-Arena.WallHalfLength, Arena.WallHalfLength), 9, Arena.ShoreZ + 30), day, waveIndex);
                Alive++;
                return;
            }
            // Everyone else rides a breaker in. Join the one forming, or start a new one.
            float releaseZ = Arena.ShoreZ + 1 - gm.Cards.BreakerReach;
            if (boss || forming == null || forming.Broken || Time.time - forming.BornAt > db.breakerGroupSeconds || forming.Riders >= 8)
            {
                float width = boss ? 20 : Random.Range(12f, 20f);
                float x = Random.Range(-Arena.WallHalfLength + width / 2, Arena.WallHalfLength - width / 2);
                forming = Breaker.Launch(boss ? 0 : x, width, db.breakerStartZ, releaseZ, db.breakerSpeed);
            }
            float dx = Random.Range(-forming.Width / 2 + 1, forming.Width / 2 - 1), dz = Random.Range(-.3f, 1.2f);
            var en = Enemy.Spawn(def, forming.RiderPosition(dx, dz), day, boss ? -1 : waveIndex);
            forming.Carry(en, dx, dz);
            Alive++;
            if (boss) { gm.Banner(def.displayName.ToUpperInvariant() + " RISES", "It's riding in on a monster wave. Everyone to the wall!"); forming = null; }
        }

        /// <summary>Spawn an extra enemy that isn't part of the plan (boss summons, tests). It still has to die to clear the phase.</summary>
        public Enemy SpawnExtra(EnemyDef def, Vector3 pos)
        {
            Alive++;
            return Enemy.Spawn(def, pos, day, -1);
        }

        public void OnEnemyDied(Enemy e)
        {
            Alive = Mathf.Max(0, Alive - 1);
            if (e.Wave >= 0 && e.Wave < killed.Length) killed[e.Wave]++;
        }
    }
}

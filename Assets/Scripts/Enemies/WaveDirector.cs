// TideWall 3D — turns a phase's wave settings into spawns. The day's budget (GameDatabase.Budget) is spent on
// packs of enemies, split into waves. Same rule as the 2D game: once half of the current wave is dead, the next
// wave arrives a few seconds later instead of at its planned time.
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
        /// <summary>True once every planned enemy has spawned and died.</summary>
        public bool Cleared => qi >= queue.Count && Alive <= 0;

        GameManager gm;
        void Awake() => gm = GetComponent<GameManager>();

        public void StartPhase(PhaseDefinition p, int day)
        {
            this.day = day;
            queue.Clear(); qi = 0; wave = 0; waveClock = 0; Alive = 0;
            waveCount = Mathf.Max(0, p.waves);
            totals = new int[waveCount]; killed = new int[waveCount]; rushed = new bool[waveCount];
            if (waveCount == 0) return;
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
                var def = WeightedPick(pool);
                if (def.cost > budget) { if (pool.TrueForAll(e => e.cost > budget)) break; continue; }
                int pack = def.cost <= 1 ? Random.Range(2, 5) : def.cost <= 3 ? Random.Range(1, 3) : 1;
                int w = Random.Range(0, waveCount);
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
        }

        static EnemyDef WeightedPick(List<EnemyDef> pool)
        {
            // Cheap enemies are common, expensive ones rare.
            float total = 0;
            foreach (var e in pool) total += 3f / (1 + e.cost * .5f);
            float r = Random.value * total;
            foreach (var e in pool) { r -= 3f / (1 + e.cost * .5f); if (r <= 0) return e; }
            return pool[0];
        }

        void Update()
        {
            if (gm.State != GameState.Playing || waveCount == 0) return;
            float dt = Time.deltaTime;
            waveClock += dt;
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
                    gm.Banner($"WAVE {wave + 1}/{waveCount}", wasRushed ? "Half the last wave fell. The next one rushes in!" : "The tide brings more.");
                    Sfx.Play("horn");
                }
            }
        }

        void Spawn(Entry e)
        {
            var def = e.def;
            float x = Random.Range(-Arena.WallHalfLength + 2, Arena.WallHalfLength - 2);
            float z = Random.Range(Arena.SpawnMinZ, Arena.SpawnMaxZ);
            var pos = def.behaviour == EnemyBehaviour.Flyer ? new Vector3(x, 8, 36) : new Vector3(x, Arena.GroundY(z), z);
            if (e.boss) pos = new Vector3(0, Arena.GroundY(40), 40);
            var en = Enemy.Spawn(def, pos, day, e.boss ? -1 : e.wave);
            Alive++;
            if (e.boss) gm.Banner(def.displayName.ToUpperInvariant() + " RISES", "Everyone on the wall!");
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

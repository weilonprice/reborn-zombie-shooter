using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// Drives escalating waves and recycles zombies through a pool so mid-wave spawning
    /// never allocates. A wave ends when every spawned zombie is dead.
    /// </summary>
    public class WaveManager : MonoBehaviour
    {
        [Header("Spawning")]
        [Tooltip("Everything that can spawn, and when. Entry 0 is the horde baseline and is " +
                 "used as the fallback wherever a prefab is missing.")]
        [SerializeField] SpawnEntry[] spawnTable = new SpawnEntry[0];
        [SerializeField] Transform player;
        [Tooltip("Zombies appear on a ring this far from the arena centre.")]
        [SerializeField] float spawnRadius = 22f;
        [Tooltip("Never spawn closer than this to the player, so nothing pops in on top of them.")]
        [SerializeField] float minDistanceFromPlayer = 12f;
        [Tooltip("Height zombies spawn at. Their capsule is centred on the pivot, so spawning " +
                 "at ground level would bury the lower half and make them pop upward.")]
        [SerializeField] float spawnHeight = 1f;

        [Header("Pacing")]
        [SerializeField] int firstWaveCount = 5;
        [Tooltip("Extra zombies added per wave.")]
        [SerializeField] float countGrowth = 2.5f;
        [SerializeField] int maxAliveAtOnce = 60;
        [SerializeField] float timeBetweenSpawns = 0.45f;

        [Tooltip("Wave that ends the run. 0 or less means endless.")]
        [SerializeField] int finalWave = 15;

        [Header("Boss")]
        [Tooltip("Arrives during the final wave. With one assigned, killing it is the win " +
                 "rather than clearing the wave.")]
        [SerializeField] ZombieAI bossPrefab;
        [Tooltip("How far into the final wave the boss arrives, as a fraction of its spawns. " +
                 "Part-way rather than immediately, so the wave establishes itself first.")]
        [SerializeField, Range(0f, 1f)] float bossArrivesAt = 0.4f;

        readonly Dictionary<ZombieAI, Queue<ZombieAI>> pools = new();
        readonly List<ZombieAI> alive = new();

        Transform poolRoot;
        Coroutine loop;

        public static WaveManager Instance { get; private set; }

        public int WaveNumber { get; private set; }
        public int Remaining { get; private set; }
        public bool OnBreak { get; private set; }
        public int FinalWave => finalWave;
        public bool WaitingForPlayerReady { get; private set; }

        public event Action<int> WaveStarted;
        public event Action<int> RemainingChanged;
        public event Action<int> WaveCompleted;
        public event Action BreakStarted;
        public event Action BreakEnded;

        void Awake()
        {
            Instance = this;
            poolRoot = new GameObject("ZombiePool").transform;
            poolRoot.SetParent(transform, false);

            for (int i = 0; i < spawnTable.Length; i++)
            {
                var prefab = spawnTable[i]?.prefab;
                if (prefab != null && !pools.ContainsKey(prefab))
                    pools[prefab] = new Queue<ZombieAI>();
            }
        }

        void Start()
        {
            if (BaselinePrefab == null)
            {
                Debug.LogError($"{nameof(WaveManager)}: no zombie prefab assigned.", this);
                enabled = false;
                return;
            }

            if (player == null)
            {
                var go = GameObject.FindGameObjectWithTag("Player");
                if (go != null) player = go.transform;
            }

            loop = StartCoroutine(RunWaves());
        }

        IEnumerator RunWaves()
        {
            while (true)
            {
                if (GameManager.Instance != null && GameManager.Instance.State != GameState.Playing)
                    yield break;

                WaveNumber++;
                int count = Mathf.RoundToInt(firstWaveCount + countGrowth * (WaveNumber - 1));

                Remaining = count;
                WaveStarted?.Invoke(WaveNumber);
                RemainingChanged?.Invoke(Remaining);

                bool isFinalWave = finalWave > 0 && WaveNumber >= finalWave;
                int bossArrivalIndex = Mathf.RoundToInt(count * bossArrivesAt);
                bool bossSpawned = false;

                // Counts bodies rather than iterations, because one roll of the table can
                // put a whole pack on the field.
                int spawned = 0;
                while (spawned < count)
                {
                    // Hold back if the arena is already saturated.
                    while (alive.Count >= maxAliveAtOnce)
                        yield return null;

                    spawned += Spawn(count - spawned);

                    if (isFinalWave && bossPrefab != null && !bossSpawned && spawned >= bossArrivalIndex)
                    {
                        SpawnBoss();
                        bossSpawned = true;
                    }

                    yield return new WaitForSeconds(timeBetweenSpawns);
                }

                while (alive.Count > 0)
                {
                    // Killing the boss ends the run mid-wave, so stop spinning here rather
                    // than waiting for a horde that has already stopped moving.
                    if (GameManager.Instance != null && GameManager.Instance.State != GameState.Playing)
                        yield break;

                    yield return null;
                }

                WaveCompleted?.Invoke(WaveNumber);

                // The run ends here rather than opening another Armory break. With a boss,
                // the win already came from its death; without one, clearing the final wave
                // is itself the win.
                if (isFinalWave)
                {
                    if (bossPrefab == null) GameManager.Instance?.Win();
                    yield break;
                }

                OnBreak = true;
                WaitingForPlayerReady = true;
                BreakStarted?.Invoke();

                while (WaitingForPlayerReady)
                {
                    if (GameManager.Instance != null && GameManager.Instance.State != GameState.Playing)
                        yield break;
                    yield return null;
                }

                BreakEnded?.Invoke();
                OnBreak = false;
            }
        }

        /// <summary>
        /// Drops the wave cap and resumes the loop after a win. WaveNumber is a field and
        /// survives the coroutine ending, so the next wave is 16 rather than 1 - endless
        /// continues the run instead of restarting it.
        /// </summary>
        public void ContinueEndless()
        {
            if (GameManager.Instance == null || GameManager.Instance.State != GameState.Victory) return;

            finalWave = 0;
            GameManager.Instance.ResumeForEndless();

            // The previous run may still be parked in its wait loop; two live wave loops
            // would spawn everything twice.
            if (loop != null) StopCoroutine(loop);
            loop = StartCoroutine(RunWaves());
        }

        public void ReadyNextWave()
        {
            WaitingForPlayerReady = false;
        }

        void Update()
        {
            if (OnBreak && WaitingForPlayerReady && InputReader.RestartPressed && Time.timeScale > 0f)
            {
                ReadyNextWave();
            }

            if (GameManager.Instance != null
                && GameManager.Instance.State == GameState.Victory
                && InputReader.EndlessPressed)
            {
                ContinueEndless();
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void SpawnBoss()
        {
            var boss = Rent(bossPrefab);
            boss.transform.SetPositionAndRotation(PickSpawnPoint(), Quaternion.identity);
            boss.gameObject.SetActive(true);
            boss.SetTarget(player);
            alive.Add(boss);

            // The horde is loud by wave 15; the arrival has to cut through it.
            CameraShake.Instance?.AddTrauma(0.9f);
        }

        /// <summary>
        /// Spawns one roll of the table and returns how many bodies it put on the field.
        /// <para>
        /// Some archetypes arrive as a pack. The group is clamped to the wave's remaining
        /// budget so a cluster near the end cannot overshoot the count the HUD already
        /// announced - Remaining is set once, up front, and has to stay true.
        /// </para>
        /// </summary>
        int Spawn(int budget)
        {
            var entry = PickEntryForWave();
            var prefab = entry?.prefab != null ? entry.prefab : BaselinePrefab;

            int group = Mathf.Clamp(entry != null ? entry.groupSize : 1, 1, Mathf.Max(1, budget));
            var anchor = PickSpawnPoint();

            for (int i = 0; i < group; i++)
            {
                var zombie = Rent(prefab);

                // A pack lands scattered around one point rather than stacked on it, so the
                // separation pass is not asked to untangle three bodies at the same position.
                var offset = group == 1
                    ? Vector3.zero
                    : new Vector3(UnityEngine.Random.Range(-1.4f, 1.4f), 0f,
                                  UnityEngine.Random.Range(-1.4f, 1.4f));

                zombie.transform.SetPositionAndRotation(anchor + offset, Quaternion.identity);
                zombie.gameObject.SetActive(true);
                zombie.SetTarget(player);
                alive.Add(zombie);
            }

            return group;
        }

        /// <summary>
        /// Rolls one enemy for this wave. Weights come from the table rather than a branch
        /// per archetype: the old version needed a serialized field, a start-wave int and
        /// three lines of formula for every type, which does not survive a roster of twelve.
        /// </summary>
        SpawnEntry PickEntryForWave()
        {
            float total = 0f;

            for (int i = 0; i < spawnTable.Length; i++)
                total += WeightOf(spawnTable[i]);

            if (total <= 0f) return null;

            float roll = UnityEngine.Random.value * total;

            for (int i = 0; i < spawnTable.Length; i++)
            {
                float weight = WeightOf(spawnTable[i]);
                if (weight <= 0f) continue;

                if (roll < weight) return spawnTable[i];
                roll -= weight;
            }

            return null;
        }

        float WeightOf(SpawnEntry entry)
        {
            if (entry == null || entry.prefab == null) return 0f;
            if (WaveNumber < entry.startWave) return 0f;

            return Mathf.Min(
                entry.weightAtStart + (WaveNumber - entry.startWave) * entry.weightGrowthPerWave,
                entry.weightCap);
        }

        /// <summary>The horde baseline, and the fallback when anything else is missing.</summary>
        ZombieAI BaselinePrefab =>
            spawnTable != null && spawnTable.Length > 0 ? spawnTable[0]?.prefab : null;

        static readonly Collider[] SpawnProbe = new Collider[4];

        Vector3 PickSpawnPoint()
        {
            var centre = transform.position;

            // A handful of tries is plenty; the last candidate is accepted regardless so this
            // always terminates. Candidates are rejected for being on top of the player OR
            // inside cover - the spawn ring crosses the outer blocks in several layouts, and
            // a zombie born inside a slab has to shove its way out before it can chase.
            Vector3 point = centre + Vector3.up * spawnHeight;
            for (int attempt = 0; attempt < 12; attempt++)
            {
                float angle = UnityEngine.Random.value * Mathf.PI * 2f;
                point = centre + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * spawnRadius;
                point.y = centre.y + spawnHeight;

                if (BlockedAt(point)) continue;
                if (player == null) break;

                if ((point - player.position).sqrMagnitude >= minDistanceFromPlayer * minDistanceFromPlayer)
                    break;
            }

            return point;
        }

        /// <summary>
        /// Whether solid level geometry occupies a spawn point. Damageables are ignored - a
        /// barricade or another zombie standing there is fine, walls are not.
        /// </summary>
        static bool BlockedAt(Vector3 point)
        {
            int count = Physics.OverlapSphereNonAlloc(point, 0.6f, SpawnProbe, ~0,
                                                      QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                if (SpawnProbe[i] == null) continue;
                if (SpawnProbe[i].GetComponentInParent<IDamageable>() != null) continue;

                return true;
            }

            return false;
        }

        ZombieAI Rent(ZombieAI prefab)
        {
            if (prefab == null) prefab = BaselinePrefab;

            if (pools.TryGetValue(prefab, out var queue) && queue.Count > 0)
                return queue.Dequeue();

            var zombie = Instantiate(prefab, poolRoot);
            zombie.gameObject.SetActive(false);
            zombie.PrefabSource = prefab;
            zombie.Died += Recycle;   // subscribed once, for the object's whole lifetime
            return zombie;
        }

        void Recycle(ZombieAI zombie)
        {
            if (!alive.Remove(zombie)) return;

            Remaining = Mathf.Max(0, Remaining - 1);
            RemainingChanged?.Invoke(Remaining);

            zombie.gameObject.SetActive(false);
            zombie.transform.SetParent(poolRoot, false);

            var source = zombie.PrefabSource ?? BaselinePrefab;
            if (source != null)
            {
                if (!pools.TryGetValue(source, out var queue))
                {
                    queue = new Queue<ZombieAI>();
                    pools[source] = queue;
                }
                queue.Enqueue(zombie);
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, spawnRadius);
        }
    }
}

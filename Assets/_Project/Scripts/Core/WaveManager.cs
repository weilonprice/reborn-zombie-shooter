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
        [SerializeField] ZombieAI zombiePrefab;
        [SerializeField] ZombieAI brutePrefab;
        [SerializeField] ZombieAI runnerPrefab;
        [SerializeField] ZombieAI rangedPrefab;
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

        [Tooltip("Wave that ends the run. Clearing it wins. 0 or less means endless.")]
        [SerializeField] int finalWave = 15;

        [Header("Archetypes")]
        [Tooltip("Wave index at which brutes begin spawning (1-indexed).")]
        [SerializeField] int bruteStartWave = 2;
        [Tooltip("Wave index at which runners begin spawning (1-indexed).")]
        [SerializeField] int runnerStartWave = 3;
        [Tooltip("Wave index at which ranged zombies begin spawning (1-indexed).")]
        [SerializeField] int rangedStartWave = 4;

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

            if (zombiePrefab != null) pools[zombiePrefab] = new Queue<ZombieAI>();
            if (brutePrefab != null) pools[brutePrefab] = new Queue<ZombieAI>();
            if (runnerPrefab != null) pools[runnerPrefab] = new Queue<ZombieAI>();
            if (rangedPrefab != null) pools[rangedPrefab] = new Queue<ZombieAI>();
        }

        void Start()
        {
            if (zombiePrefab == null)
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

                for (int i = 0; i < count; i++)
                {
                    // Hold back if the arena is already saturated.
                    while (alive.Count >= maxAliveAtOnce)
                        yield return null;

                    Spawn();
                    yield return new WaitForSeconds(timeBetweenSpawns);
                }

                while (alive.Count > 0)
                    yield return null;

                WaveCompleted?.Invoke(WaveNumber);

                // The run ends here rather than opening another Armory break: clearing the
                // final wave is the win, so there is nothing left to shop for.
                if (finalWave > 0 && WaveNumber >= finalWave)
                {
                    GameManager.Instance?.Win();
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
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Spawn()
        {
            var prefab = PickPrefabForWave();
            var zombie = Rent(prefab);
            zombie.transform.SetPositionAndRotation(PickSpawnPoint(), Quaternion.identity);
            zombie.gameObject.SetActive(true);
            zombie.SetTarget(player);
            alive.Add(zombie);
        }

        ZombieAI PickPrefabForWave()
        {
            // Standard zombie serves as the horde baseline
            float standardWeight = 10f;

            float bruteWeight = (brutePrefab != null && WaveNumber >= bruteStartWave)
                ? Mathf.Min(1.2f + (WaveNumber - bruteStartWave) * 0.7f, 4.5f)
                : 0f;

            float runnerWeight = (runnerPrefab != null && WaveNumber >= runnerStartWave)
                ? Mathf.Min(1.8f + (WaveNumber - runnerStartWave) * 0.8f, 5.0f)
                : 0f;

            float rangedWeight = (rangedPrefab != null && WaveNumber >= rangedStartWave)
                ? Mathf.Min(1.4f + (WaveNumber - rangedStartWave) * 0.6f, 4.0f)
                : 0f;

            float total = standardWeight + bruteWeight + runnerWeight + rangedWeight;
            float roll = UnityEngine.Random.value * total;

            if (roll < bruteWeight) return brutePrefab;
            roll -= bruteWeight;

            if (roll < runnerWeight) return runnerPrefab;
            roll -= runnerWeight;

            if (roll < rangedWeight) return rangedPrefab;

            return zombiePrefab;
        }

        Vector3 PickSpawnPoint()
        {
            var centre = transform.position;

            // A handful of tries is plenty to find a point away from the player;
            // the last candidate is accepted regardless so this always terminates.
            Vector3 point = centre + Vector3.up * spawnHeight;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                float angle = UnityEngine.Random.value * Mathf.PI * 2f;
                point = centre + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * spawnRadius;
                point.y = centre.y + spawnHeight;

                if (player == null) break;
                if ((point - player.position).sqrMagnitude >= minDistanceFromPlayer * minDistanceFromPlayer)
                    break;
            }

            return point;
        }

        ZombieAI Rent(ZombieAI prefab)
        {
            if (prefab == null) prefab = zombiePrefab;

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

            var source = zombie.PrefabSource ?? zombiePrefab;
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

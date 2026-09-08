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
        [SerializeField] float timeBetweenWaves = 5f;

        readonly Queue<ZombieAI> pool = new();
        readonly List<ZombieAI> alive = new();

        Transform poolRoot;
        Coroutine loop;

        public int WaveNumber { get; private set; }
        public int Remaining { get; private set; }
        public float BreakTimeLeft { get; private set; }
        public bool OnBreak { get; private set; }

        public event Action<int> WaveStarted;
        public event Action<int> RemainingChanged;

        void Awake()
        {
            poolRoot = new GameObject("ZombiePool").transform;
            poolRoot.SetParent(transform, false);
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

                OnBreak = true;
                for (BreakTimeLeft = timeBetweenWaves; BreakTimeLeft > 0f; BreakTimeLeft -= Time.deltaTime)
                    yield return null;

                BreakTimeLeft = 0f;
                OnBreak = false;
            }
        }

        void Spawn()
        {
            var zombie = Rent();
            zombie.transform.SetPositionAndRotation(PickSpawnPoint(), Quaternion.identity);
            zombie.gameObject.SetActive(true);
            zombie.SetTarget(player);
            alive.Add(zombie);
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

        ZombieAI Rent()
        {
            if (pool.Count > 0) return pool.Dequeue();

            var zombie = Instantiate(zombiePrefab, poolRoot);
            zombie.gameObject.SetActive(false);
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
            pool.Enqueue(zombie);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, spawnRadius);
        }
    }
}

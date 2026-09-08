using System;
using UnityEngine;

namespace ZombieShooter
{
    public enum GameState { Playing, GameOver }

    /// <summary>
    /// Owns global run state: score, current wave and win/lose. Deliberately thin —
    /// it reacts to events from other systems instead of driving them.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [SerializeField] Health playerHealth;

        public GameState State { get; private set; } = GameState.Playing;
        public int Score { get; private set; }
        public int Kills { get; private set; }

        public event Action<int> ScoreChanged;
        public event Action StateChanged;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        void Start()
        {
            if (playerHealth == null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) playerHealth = player.GetComponent<Health>();
            }

            if (playerHealth != null)
                playerHealth.Died += OnPlayerDied;
            else
                Debug.LogWarning($"{nameof(GameManager)}: no player Health found; game over will never trigger.", this);
        }

        void OnDestroy()
        {
            if (playerHealth != null)
                playerHealth.Died -= OnPlayerDied;

            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (State == GameState.GameOver && InputReader.RestartPressed)
                Restart();
        }

        public void AddScore(int amount)
        {
            if (State != GameState.Playing) return;

            Score += amount;
            Kills++;
            ScoreChanged?.Invoke(Score);
        }

        void OnPlayerDied(Health _)
        {
            if (State == GameState.GameOver) return;

            State = GameState.GameOver;
            StateChanged?.Invoke();
        }

        public void Restart()
        {
            // Reloading the scene is the cheapest correct reset for a prototype:
            // every system rebuilds its own state in Awake/OnEnable.
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            UnityEngine.SceneManagement.SceneManager.LoadScene(scene.buildIndex);
        }
    }
}

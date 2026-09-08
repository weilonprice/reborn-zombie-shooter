using System;
using UnityEngine;

namespace ZombieShooter
{
    public enum GameState
    {
        Playing,
        GameOver,
        /// <summary>Final wave cleared. A run now has an ending it can reach.</summary>
        Victory,
    }

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
        public int Gold { get; private set; }

        public event Action<int> ScoreChanged;
        public event Action<int> GoldChanged;
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

        /// <summary>True once the run has ended, whether won or lost.</summary>
        public bool IsRunOver => State != GameState.Playing;

        void Update()
        {
            if (IsRunOver && InputReader.RestartPressed)
                Restart();
        }

        public void AddScore(int amount)
        {
            if (State != GameState.Playing) return;

            Score += amount;
            Kills++;
            ScoreChanged?.Invoke(Score);
        }

        public void AddGold(int amount)
        {
            if (State != GameState.Playing || amount <= 0) return;

            Gold += amount;
            GoldChanged?.Invoke(Gold);
        }

        public bool TrySpendGold(int amount)
        {
            if (amount < 0 || Gold < amount) return false;

            Gold -= amount;
            GoldChanged?.Invoke(Gold);
            return true;
        }

        void OnPlayerDied(Health _)
        {
            if (IsRunOver) return;

            State = GameState.GameOver;
            StateChanged?.Invoke();
        }

        /// <summary>
        /// Called by WaveManager when the final wave is cleared. Every system already gates
        /// on State == Playing, so setting this stops the arena without any further wiring.
        /// </summary>
        public void Win()
        {
            if (IsRunOver) return;

            // A kill freeze may still be running from the last zombie of the last wave.
            Time.timeScale = 1f;

            State = GameState.Victory;
            StateChanged?.Invoke();
        }

        public void Restart()
        {
            // timeScale survives a scene load, so a restart during a hit-stop freeze would
            // otherwise come back to a permanently frozen game.
            Time.timeScale = 1f;

            // Reloading the scene is the cheapest correct reset for a prototype:
            // every system rebuilds its own state in Awake/OnEnable.
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            UnityEngine.SceneManagement.SceneManager.LoadScene(scene.buildIndex);
        }
    }
}

using UnityEngine;
using UnityEngine.UI;

namespace ZombieShooter
{
    /// <summary>
    /// Placeholder greybox HUD built on legacy uGUI Text so it needs no TextMeshPro
    /// font import. Swap for TMP (or UI Toolkit) once the art direction is settled.
    /// </summary>
    public class HUD : MonoBehaviour
    {
        [SerializeField] Health playerHealth;
        [SerializeField] Weapon weapon;
        [SerializeField] WaveManager waves;
        [SerializeField] WeaponLoadout loadout;

        [Header("Widgets")]
        [SerializeField] Image healthFill;
        [SerializeField] Text healthLabel;
        [SerializeField] Text ammoLabel;
        [SerializeField] Text weaponLabel;
        [SerializeField] Text waveLabel;
        [SerializeField] Text scoreLabel;
        [SerializeField] Text centreLabel;

        bool lastReloading;

        // Subscribe in Start, not OnEnable: GameManager.Instance is assigned in Awake, and
        // Unity gives no ordering guarantee between one object's Awake and another's OnEnable.
        void Start()
        {
            if (playerHealth != null)
            {
                playerHealth.Changed += OnHealthChanged;
                OnHealthChanged(playerHealth.Current, playerHealth.Max);
            }

            if (weapon != null)
            {
                weapon.AmmoChanged += OnAmmoChanged;
                OnAmmoChanged(weapon.Ammo, weapon.MagazineSize);
            }

            if (waves != null)
            {
                waves.WaveStarted += OnWaveStarted;
                waves.RemainingChanged += OnRemainingChanged;
            }

            if (loadout != null)
            {
                loadout.WeaponChanged += OnWeaponChanged;
                // Covers either execution order: if the loadout already picked a weapon we
                // read it, and if it has not, the event above delivers it.
                OnWeaponChanged(loadout.CurrentDefinition);
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.ScoreChanged += OnScoreChanged;
                GameManager.Instance.StateChanged += RefreshCentreLabel;
                OnScoreChanged(GameManager.Instance.Score);
            }

            RefreshWaveLabel();
            RefreshCentreLabel();
        }

        void OnDestroy()
        {
            if (playerHealth != null) playerHealth.Changed -= OnHealthChanged;
            if (weapon != null) weapon.AmmoChanged -= OnAmmoChanged;

            if (waves != null)
            {
                waves.WaveStarted -= OnWaveStarted;
                waves.RemainingChanged -= OnRemainingChanged;
            }

            if (loadout != null) loadout.WeaponChanged -= OnWeaponChanged;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.ScoreChanged -= OnScoreChanged;
                GameManager.Instance.StateChanged -= RefreshCentreLabel;
            }
        }

        void Update()
        {
            // Two things aren't event-driven: the between-wave countdown and reload state.
            if (waves != null && waves.OnBreak) RefreshCentreLabel();

            if (weapon != null && weapon.IsReloading != lastReloading)
            {
                lastReloading = weapon.IsReloading;
                OnAmmoChanged(weapon.Ammo, weapon.MagazineSize);
            }
        }

        void OnHealthChanged(float current, float max)
        {
            if (healthFill != null) healthFill.fillAmount = max <= 0f ? 0f : current / max;
            if (healthLabel != null) healthLabel.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
        }

        void OnAmmoChanged(int ammo, int magazine)
        {
            if (ammoLabel == null) return;

            ammoLabel.text = weapon != null && weapon.IsReloading
                ? "RELOADING..."
                : $"{ammo} / {magazine}";
        }

        void OnWeaponChanged(WeaponDefinition weaponDefinition)
        {
            if (weaponLabel == null) return;

            weaponLabel.text = weaponDefinition != null
                ? weaponDefinition.DisplayName.ToUpperInvariant()
                : string.Empty;
        }

        void OnWaveStarted(int wave)
        {
            RefreshWaveLabel();
            RefreshCentreLabel();
        }

        void OnRemainingChanged(int remaining) => RefreshWaveLabel();

        void OnScoreChanged(int score)
        {
            if (scoreLabel != null) scoreLabel.text = $"SCORE {score}";
        }

        void RefreshWaveLabel()
        {
            if (waveLabel == null || waves == null) return;
            waveLabel.text = $"WAVE {waves.WaveNumber}    LEFT {waves.Remaining}";
        }

        void RefreshCentreLabel()
        {
            if (centreLabel == null) return;

            if (GameManager.Instance != null && GameManager.Instance.State == GameState.GameOver)
            {
                int score = GameManager.Instance.Score;
                int wave = waves != null ? waves.WaveNumber : 0;
                centreLabel.text = $"YOU DIED\nWave {wave}  ·  Score {score}\n\nPress SPACE to restart";
                return;
            }

            if (waves != null && waves.OnBreak)
            {
                centreLabel.text = $"WAVE {waves.WaveNumber + 1} IN {Mathf.CeilToInt(waves.BreakTimeLeft)}";
                return;
            }

            centreLabel.text = string.Empty;
        }
    }
}

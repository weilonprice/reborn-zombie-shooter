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
        [SerializeField] DeployablePlacer placer;
        [SerializeField] UltimateAbility ultimate;

        [Header("Widgets")]
        [SerializeField] Image healthFill;
        [SerializeField] Text healthLabel;
        [SerializeField] Text ammoLabel;
        [SerializeField] Text weaponLabel;

        [Header("Boss")]
        [SerializeField] GameObject bossBarRoot;
        [SerializeField] Image bossFill;
        [SerializeField] Text bossLabel;
        [SerializeField] Text waveLabel;
        [SerializeField] Text scoreLabel;
        [SerializeField] Text goldLabel;
        [SerializeField] Text barricadeLabel;
        [SerializeField] Text ultimateLabel;
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
                waves.BreakStarted += RefreshCentreLabel;
                waves.BreakEnded += RefreshCentreLabel;
            }

            if (loadout != null)
            {
                loadout.WeaponChanged += OnWeaponChanged;
                loadout.ReserveAmmoChanged += OnReserveAmmoChanged;
                // Covers either execution order: if the loadout already picked a weapon we
                // read it, and if it has not, the event above delivers it.
                OnWeaponChanged(loadout.CurrentDefinition);
            }

            if (ultimate != null)
            {
                ultimate.Changed += RefreshUltimateLabel;
                RefreshUltimateLabel();
            }

            if (placer != null)
            {
                placer.StockChanged += OnDeployableStockChanged;
                placer.SelectionChanged += OnDeployableSelectionChanged;
                OnDeployableSelectionChanged(placer.Selected);
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.ScoreChanged += OnScoreChanged;
                GameManager.Instance.GoldChanged += OnGoldChanged;
                GameManager.Instance.StateChanged += RefreshCentreLabel;
                OnScoreChanged(GameManager.Instance.Score);
                OnGoldChanged(GameManager.Instance.Gold);
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
                waves.BreakStarted -= RefreshCentreLabel;
                waves.BreakEnded -= RefreshCentreLabel;
            }

            if (loadout != null)
            {
                loadout.WeaponChanged -= OnWeaponChanged;
                loadout.ReserveAmmoChanged -= OnReserveAmmoChanged;
            }

            if (ultimate != null) ultimate.Changed -= RefreshUltimateLabel;

            if (placer != null)
            {
                placer.StockChanged -= OnDeployableStockChanged;
                placer.SelectionChanged -= OnDeployableSelectionChanged;
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.ScoreChanged -= OnScoreChanged;
                GameManager.Instance.GoldChanged -= OnGoldChanged;
                GameManager.Instance.StateChanged -= RefreshCentreLabel;
            }
        }

        void Update()
        {
            RefreshBossBar();

            // Two things aren't event-driven: the between-wave countdown and reload state.
            if (waves != null && waves.OnBreak) RefreshCentreLabel();

            if (weapon != null && weapon.IsReloading != lastReloading)
            {
                lastReloading = weapon.IsReloading;
                OnAmmoChanged(weapon.Ammo, weapon.MagazineSize);
            }
        }

        /// <summary>
        /// Polled rather than event-driven: the boss is pooled, so it comes and goes without
        /// the HUD being rewired, and a bar that lingered after its death would be worse than
        /// one frame of latency appearing.
        /// </summary>
        void RefreshBossBar()
        {
            if (bossBarRoot == null) return;

            var boss = BossController.Active;
            bool show = boss != null && boss.Health != null && boss.Health.IsAlive;

            if (bossBarRoot.activeSelf != show) bossBarRoot.SetActive(show);
            if (!show) return;

            if (bossFill != null) bossFill.fillAmount = boss.Health.Normalized;

            if (bossLabel != null)
            {
                bossLabel.text = boss.Phase >= 2
                    ? "THE BUTCHER   ·   ENRAGED"
                    : "THE BUTCHER";
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

            string reserveText = "∞";
            if (loadout != null)
            {
                int res = loadout.CurrentReserveAmmo;
                if (res >= 0) reserveText = res.ToString();
            }

            ammoLabel.text = weapon != null && weapon.IsReloading
                ? $"RELOADING...  [{reserveText}]"
                : $"{ammo} / {magazine}  [{reserveText}]";
        }

        void OnReserveAmmoChanged(int slot, int reserve)
        {
            if (weapon != null)
                OnAmmoChanged(weapon.Ammo, weapon.MagazineSize);
        }

        void OnWeaponChanged(WeaponDefinition weaponDefinition)
        {
            if (weaponLabel == null) return;

            weaponLabel.text = weaponDefinition != null
                ? weaponDefinition.DisplayName.ToUpperInvariant()
                : string.Empty;

            if (weapon != null)
                OnAmmoChanged(weapon.Ammo, weapon.MagazineSize);
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

        void OnGoldChanged(int gold)
        {
            if (goldLabel != null) goldLabel.text = $"GOLD ${gold}";
        }

        // Both the count and the selection feed one label, so either changing redraws it.
        void OnDeployableStockChanged(int index, int count) => RefreshDeployableLabel();
        void OnDeployableSelectionChanged(DeployableDefinition definition) => RefreshDeployableLabel();

        void RefreshDeployableLabel()
        {
            if (barricadeLabel == null) return;

            if (placer == null || placer.Selected == null)
            {
                barricadeLabel.text = string.Empty;
                return;
            }

            string name = placer.Selected.DisplayName.ToUpperInvariant();
            barricadeLabel.text = placer.Count > 1
                ? $"[Q] {name}  x{placer.SelectedStock}"
                : $"{name}  x{placer.SelectedStock}";
        }

        void RefreshWaveLabel()
        {
            if (waveLabel == null || waves == null) return;
            // Showing the total is what makes a run read as an arc rather than a treadmill.
            waveLabel.text = waves.FinalWave > 0
                ? $"WAVE {waves.WaveNumber} / {waves.FinalWave}    LEFT {waves.Remaining}"
                : $"WAVE {waves.WaveNumber}    LEFT {waves.Remaining}";
        }

        /// <summary>
        /// Blank until the tier is bought, so a player who never takes it never sees a
        /// widget for an ability they do not have.
        /// </summary>
        void RefreshUltimateLabel()
        {
            if (ultimateLabel == null) return;

            if (ultimate == null || !ultimate.Available)
            {
                ultimateLabel.text = string.Empty;
                return;
            }

            if (ultimate.Active)
            {
                ultimateLabel.text = "LEGEND OF THE WEST";
                ultimateLabel.color = new Color(1f, 0.45f, 0.15f);
                return;
            }

            if (ultimate.Charged)
            {
                ultimateLabel.text = "[V]  ULTIMATE READY";
                ultimateLabel.color = new Color(1f, 0.85f, 0.25f);
                return;
            }

            ultimateLabel.text = $"ULT  {ultimate.Kills} / {ultimate.Required}";
            ultimateLabel.color = new Color(0.62f, 0.64f, 0.68f);
        }

        void RefreshCentreLabel()
        {
            if (centreLabel == null) return;

            if (GameManager.Instance != null && GameManager.Instance.State == GameState.Victory)
            {
                int wonScore = GameManager.Instance.Score;
                int wonGold = GameManager.Instance.Gold;
                int lastWave = waves != null ? waves.WaveNumber : 0;
                centreLabel.text = $"YOU SURVIVED\nAll {lastWave} waves cleared  ·  Score {wonScore}  ·  Gold ${wonGold}\n\n[E] CONTINUE ENDLESS   ·   [SPACE] PLAY AGAIN";
                return;
            }

            if (GameManager.Instance != null && GameManager.Instance.State == GameState.GameOver)
            {
                int score = GameManager.Instance.Score;
                int gold = GameManager.Instance.Gold;
                int wave = waves != null ? waves.WaveNumber : 0;
                centreLabel.text = $"YOU DIED\nWave {wave}  ·  Score {score}  ·  Gold ${gold}\n\nPress SPACE to restart";
                return;
            }

            if (waves != null && waves.OnBreak)
            {
                centreLabel.text = $"WAVE {waves.WaveNumber} CLEARED!\n[B] ARMORY SHOP   ·   [F] PLACE FORTIFICATIONS   ·   [SPACE] NEXT WAVE";
                return;
            }

            centreLabel.text = string.Empty;
        }
    }
}

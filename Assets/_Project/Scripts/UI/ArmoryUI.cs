using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace ZombieShooter
{
    /// <summary>
    /// Interactive pause/break menu for purchasing weapons, ammo crates,
    /// and deployables with accumulated gold.
    /// Host GameObject stays active to receive input and wave events.
    /// </summary>
    public class ArmoryUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] GameObject panel;
        [SerializeField] Text titleLabel;
        [SerializeField] Text goldLabel;
        [SerializeField] WeaponLoadout loadout;

        [Header("Weapon Buttons & Labels")]
        [SerializeField] Button shotgunButton;
        [SerializeField] Text shotgunBtnText;
        [SerializeField] Button arButton;
        [SerializeField] Text arBtnText;
        [SerializeField] Button sniperButton;
        [SerializeField] Text sniperBtnText;

        [Header("Ammo Button")]
        [SerializeField] Button ammoButton;
        [SerializeField] Text ammoBtnText;

        [Header("Deployable Fortifications")]
        [SerializeField] Button barricadeButton;
        [SerializeField] Text barricadeBtnText;
        [SerializeField] Button barrelButton;
        [SerializeField] Text barrelBtnText;
        [SerializeField] Button claymoreButton;
        [SerializeField] Text claymoreBtnText;
        [SerializeField] DeployablePlacer placer;

        [Header("Actions")]
        [SerializeField] Button deployButton;
        [SerializeField] Text deployBtnText;
        [SerializeField] Button closeButton;

        public bool IsOpen => panel != null && panel.activeSelf;

        void Awake()
        {
            // Wire action buttons
            if (deployButton != null) deployButton.onClick.AddListener(OnDeployOrResumeClicked);
            if (closeButton != null) closeButton.onClick.AddListener(Close);

            // Wire weapon buttons
            if (shotgunButton != null)
                shotgunButton.onClick.AddListener(() => ArmoryManager.Instance?.TryBuyWeapon(1, ArmoryManager.CostShotgun, loadout));
            if (arButton != null)
                arButton.onClick.AddListener(() => ArmoryManager.Instance?.TryBuyWeapon(2, ArmoryManager.CostAssaultRifle, loadout));
            if (sniperButton != null)
                sniperButton.onClick.AddListener(() => ArmoryManager.Instance?.TryBuyWeapon(3, ArmoryManager.CostSniper, loadout));

            // Wire ammo button
            if (ammoButton != null)
                ammoButton.onClick.AddListener(() => ArmoryManager.Instance?.TryBuyAmmoCrate(loadout));

            // Wire barricade button
            if (barricadeButton != null)
                barricadeButton.onClick.AddListener(() => ArmoryManager.Instance?.TryBuyDeployable(placer, 0));
            if (barrelButton != null)
                barrelButton.onClick.AddListener(() => ArmoryManager.Instance?.TryBuyDeployable(placer, 1));
            if (claymoreButton != null)
                claymoreButton.onClick.AddListener(() => ArmoryManager.Instance?.TryBuyDeployable(placer, 2));
            // Hide the visual panel while keeping this host GameObject active
            if (panel != null && panel != gameObject)
                panel.SetActive(false);
        }

        void Start()
        {
            if (loadout == null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) loadout = player.GetComponent<WeaponLoadout>();
            }

            if (placer == null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) placer = player.GetComponent<DeployablePlacer>();
            }

            if (WaveManager.Instance != null)
                WaveManager.Instance.BreakStarted += Open;

            if (ArmoryManager.Instance != null)
                ArmoryManager.Instance.Changed += RefreshUI;

            if (placer != null)
                placer.StockChanged += (_, __) => RefreshUI();

            if (GameManager.Instance != null)
                GameManager.Instance.GoldChanged += _ => RefreshUI();
        }

        void OnDestroy()
        {
            if (WaveManager.Instance != null)
                WaveManager.Instance.BreakStarted -= Open;

            if (ArmoryManager.Instance != null)
                ArmoryManager.Instance.Changed -= RefreshUI;
        }

        void Update()
        {
            if (IsOpen)
            {
                if (InputReader.RestartPressed)
                {
                    OnDeployOrResumeClicked();
                    return;
                }

                if (InputReader.ArmoryPressed || (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame))
                {
                    Close();
                    return;
                }
            }
            else
            {
                if (InputReader.ArmoryPressed)
                {
                    Open();
                }
            }
        }

        public void Open()
        {
            if (panel == null) return;

            panel.SetActive(true);
            Time.timeScale = 0f;
            RefreshUI();
        }

        public void Close()
        {
            if (panel == null) return;

            panel.SetActive(false);
            Time.timeScale = 1f;
        }

        public void OnDeployOrResumeClicked()
        {
            bool onBreak = WaveManager.Instance != null && WaveManager.Instance.OnBreak;
            Close();
            if (onBreak)
            {
                WaveManager.Instance?.ReadyNextWave();
            }
        }

        public void RefreshUI()
        {
            int gold = GameManager.Instance != null ? GameManager.Instance.Gold : 0;
            int wave = WaveManager.Instance != null ? WaveManager.Instance.WaveNumber : 1;
            bool onBreak = WaveManager.Instance != null && WaveManager.Instance.OnBreak;

            if (titleLabel != null)
            {
                titleLabel.text = onBreak
                    ? $"ARMORY SHOP  -  WAVE {wave} CLEARED"
                    : $"ARMORY SHOP  -  WAVE {wave} IN PROGRESS";
            }

            if (deployBtnText != null)
            {
                deployBtnText.text = onBreak
                    ? $"DEPLOY / WAVE {wave + 1} [SPACE]"
                    : "RESUME GAME [SPACE]";
            }

            if (goldLabel != null)
                goldLabel.text = $"GOLD: ${gold}";

            // Weapons
            UpdateButton(shotgunButton, shotgunBtnText,
                loadout != null && loadout.IsSlotUnlocked(1),
                gold >= ArmoryManager.CostShotgun,
                $"${ArmoryManager.CostShotgun} BUY");

            UpdateButton(arButton, arBtnText,
                loadout != null && loadout.IsSlotUnlocked(2),
                gold >= ArmoryManager.CostAssaultRifle,
                $"${ArmoryManager.CostAssaultRifle} BUY");

            UpdateButton(sniperButton, sniperBtnText,
                loadout != null && loadout.IsSlotUnlocked(3),
                gold >= ArmoryManager.CostSniper,
                $"${ArmoryManager.CostSniper} BUY");

            // Ammo
            if (ammoButton != null)
            {
                bool canAffordAmmo = gold >= ArmoryManager.CostAmmoCrate;
                ammoButton.interactable = canAffordAmmo;
                if (ammoBtnText != null)
                    ammoBtnText.text = $"${ArmoryManager.CostAmmoCrate} REFILL ALL";
            }

            // Deployables. Price and stock both come from the catalogue, so a fourth one
            // needs a button here and nothing else - no constant, no branch.
            RefreshDeployableButton(barricadeButton, barricadeBtnText, 0, gold);
            RefreshDeployableButton(barrelButton, barrelBtnText, 1, gold);
            RefreshDeployableButton(claymoreButton, claymoreBtnText, 2, gold);
        }

        /// <summary>
        /// Draws one deployable's buy button from its definition. Hides the button entirely
        /// if the catalogue has no entry at that index, so an unfinished slot cannot be
        /// bought from.
        /// </summary>
        void RefreshDeployableButton(Button button, Text label, int index, int gold)
        {
            if (button == null) return;

            var definition = placer != null ? placer.DefinitionAt(index) : null;
            if (definition == null)
            {
                button.gameObject.SetActive(false);
                return;
            }

            if (!button.gameObject.activeSelf) button.gameObject.SetActive(true);

            button.interactable = gold >= definition.Cost;
            if (label != null)
                label.text = $"${definition.Cost} BUY  [x{placer.StockOf(index)}]";
        }

        static void UpdateButton(Button btn, Text label, bool isOwned, bool canAfford, string buyText)
        {
            if (btn == null) return;

            if (isOwned)
            {
                btn.interactable = false;
                if (label != null) label.text = "INSTALLED";
            }
            else
            {
                btn.interactable = canAfford;
                if (label != null) label.text = buyText;
            }
        }
    }
}

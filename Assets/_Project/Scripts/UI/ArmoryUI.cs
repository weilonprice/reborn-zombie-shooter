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

        [Header("Weapon Buttons")]
        [Tooltip("One button per catalogue entry, in the same order. Built by ArenaBuilder " +
                 "in a loop, so a tenth weapon is a catalogue entry and nothing else.")]
        [SerializeField] Button[] weaponButtons;
        [SerializeField] Text[] weaponBtnTexts;
        [SerializeField] Text loadoutLabel;

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

            // Wire weapon buttons. The index is captured per button rather than read from a
            // loop variable, which would have every button buying the last weapon.
            if (weaponButtons != null)
            {
                for (int i = 0; i < weaponButtons.Length; i++)
                {
                    if (weaponButtons[i] == null) continue;

                    int index = i;
                    weaponButtons[i].onClick.AddListener(() => BuyWeapon(index));
                }
            }

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
            RefreshWeaponButtons(gold);

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

        void BuyWeapon(int catalogueIndex)
        {
            var definition = WeaponAt(catalogueIndex);
            if (definition != null) ArmoryManager.Instance?.TryBuyWeapon(definition, loadout);
        }

        WeaponDefinition WeaponAt(int index)
        {
            var catalogue = loadout != null ? loadout.Catalogue : null;
            return catalogue != null && index >= 0 && index < catalogue.Count
                ? catalogue[index]
                : null;
        }

        /// <summary>
        /// Draws every weapon in the catalogue. Buttons past the end of it hide themselves,
        /// so the shop can be built with room to grow without showing empty rows.
        /// </summary>
        void RefreshWeaponButtons(int gold)
        {
            if (weaponButtons == null) return;

            bool full = loadout != null && !loadout.HasFreeSlot;

            if (loadoutLabel != null && loadout != null)
            {
                int held = 0;
                for (int i = 0; i < loadout.CarryCapacity; i++)
                    if (loadout.CarriedAt(i) != null) held++;

                loadoutLabel.text = full
                    ? $"LOADOUT FULL  {held} / {loadout.CarryCapacity}  -  this run is committed"
                    : $"LOADOUT  {held} / {loadout.CarryCapacity}";
                loadoutLabel.color = full
                    ? new Color(0.85f, 0.45f, 0.35f)
                    : new Color(0.62f, 0.64f, 0.68f);
            }

            for (int i = 0; i < weaponButtons.Length; i++)
            {
                var button = weaponButtons[i];
                if (button == null) continue;

                var definition = WeaponAt(i);
                if (definition == null)
                {
                    button.gameObject.SetActive(false);
                    continue;
                }

                if (!button.gameObject.activeSelf) button.gameObject.SetActive(true);

                var label = weaponBtnTexts != null && i < weaponBtnTexts.Length
                    ? weaponBtnTexts[i]
                    : null;

                bool owned = loadout != null && loadout.Owns(definition);

                // "No room left" and "cannot afford it" are different answers and the player
                // should be able to tell which one they are looking at.
                if (owned)
                {
                    button.interactable = false;
                    if (label != null) label.text = "CARRIED";
                }
                else if (full)
                {
                    button.interactable = false;
                    if (label != null) label.text = "NO SLOT";
                }
                else
                {
                    button.interactable = gold >= definition.Cost;
                    if (label != null) label.text = $"${definition.Cost} BUY";
                }
            }
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
    }
}

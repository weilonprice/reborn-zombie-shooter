using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace ZombieShooter
{
    /// <summary>
    /// Interactive pause/break menu for purchasing weapons, ammo crates,
    /// and tangible weapon mod cores with accumulated gold.
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
        [SerializeField] BarricadePlacer placer;

        [Header("Mod Buttons & Labels")]
        [SerializeField] Button drumMagsButton;
        [SerializeField] Text drumMagsBtnText;
        [SerializeField] Button overclockButton;
        [SerializeField] Text overclockBtnText;
        [SerializeField] Button boreButton;
        [SerializeField] Text boreBtnText;
        [SerializeField] Button slugButton;
        [SerializeField] Text slugBtnText;
        [SerializeField] Button dragonsBreathButton;
        [SerializeField] Text dragonsBreathBtnText;

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
                barricadeButton.onClick.AddListener(() => ArmoryManager.Instance?.TryBuyBarricade(placer));

            // Wire mod buttons
            if (drumMagsButton != null)
                drumMagsButton.onClick.AddListener(() => ArmoryManager.Instance?.TryBuyMod(ModCoreType.ExtendedDrumMags, ArmoryManager.CostExtendedDrumMags));
            if (overclockButton != null)
                overclockButton.onClick.AddListener(() => ArmoryManager.Instance?.TryBuyMod(ModCoreType.OverclockedReceiver, ArmoryManager.CostOverclockedReceiver));
            if (boreButton != null)
                boreButton.onClick.AddListener(() => ArmoryManager.Instance?.TryBuyMod(ModCoreType.BorePiercing, ArmoryManager.CostBorePiercing));
            if (slugButton != null)
                slugButton.onClick.AddListener(() => ArmoryManager.Instance?.TryBuyMod(ModCoreType.HeavySlug, ArmoryManager.CostHeavySlug));
            if (dragonsBreathButton != null)
                dragonsBreathButton.onClick.AddListener(() => ArmoryManager.Instance?.TryBuyMod(ModCoreType.DragonsBreath, ArmoryManager.CostDragonsBreath));

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
                if (player != null) placer = player.GetComponent<BarricadePlacer>();
            }

            if (WaveManager.Instance != null)
                WaveManager.Instance.BreakStarted += Open;

            if (ArmoryManager.Instance != null)
                ArmoryManager.Instance.Changed += RefreshUI;

            if (placer != null)
                placer.BarricadesChanged += _ => RefreshUI();

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

            var armory = ArmoryManager.Instance;

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

            // Barricades
            if (barricadeButton != null)
            {
                bool canAfford = gold >= ArmoryManager.CostBarricade;
                barricadeButton.interactable = canAfford;
                int count = placer != null ? placer.BarricadesInStock : 0;
                if (barricadeBtnText != null)
                    barricadeBtnText.text = $"${ArmoryManager.CostBarricade} BUY  [x{count}]";
            }

            // Mod Cores
            if (armory != null)
            {
                UpdateButton(drumMagsButton, drumMagsBtnText,
                    armory.HasMod(ModCoreType.ExtendedDrumMags),
                    gold >= ArmoryManager.CostExtendedDrumMags,
                    $"${ArmoryManager.CostExtendedDrumMags} INSTALL");

                UpdateButton(overclockButton, overclockBtnText,
                    armory.HasMod(ModCoreType.OverclockedReceiver),
                    gold >= ArmoryManager.CostOverclockedReceiver,
                    $"${ArmoryManager.CostOverclockedReceiver} INSTALL");

                UpdateButton(boreButton, boreBtnText,
                    armory.HasMod(ModCoreType.BorePiercing),
                    gold >= ArmoryManager.CostBorePiercing,
                    $"${ArmoryManager.CostBorePiercing} INSTALL");

                UpdateButton(slugButton, slugBtnText,
                    armory.HasMod(ModCoreType.HeavySlug),
                    gold >= ArmoryManager.CostHeavySlug,
                    $"${ArmoryManager.CostHeavySlug} INSTALL");

                UpdateButton(dragonsBreathButton, dragonsBreathBtnText,
                    armory.HasMod(ModCoreType.DragonsBreath),
                    gold >= ArmoryManager.CostDragonsBreath,
                    $"${ArmoryManager.CostDragonsBreath} INSTALL");
            }
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

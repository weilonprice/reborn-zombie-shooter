using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieShooter
{
    public enum ModCoreType
    {
        DragonsBreath,       // Shotgun: Incendiary fire pellets (DoT)
        HeavySlug,           // Shotgun: Single 120-damage high-knockback slug
        BorePiercing,        // AR & Sniper: +2 pierce count, 100% damage retention
        ExtendedDrumMags,    // Universal: +50% magazine capacity
        OverclockedReceiver  // Pistol & AR: Full-auto pistol + 50% fire rate; AR +35% fire rate
    }

    /// <summary>
    /// Manages purchases of weapons, ammo crates, and tangible weapon mod cores.
    /// Deducts gold through GameManager and coordinates with WeaponLoadout.
    /// </summary>
    public class ArmoryManager : MonoBehaviour
    {
        public static ArmoryManager Instance { get; private set; }

        readonly HashSet<ModCoreType> installedMods = new();

        public const int CostShotgun = 150;
        public const int CostAssaultRifle = 250;
        public const int CostSniper = 350;
        public const int CostAmmoCrate = 50;
        public const int CostBarricade = 40;

        public const int CostDragonsBreath = 200;
        public const int CostHeavySlug = 200;
        public const int CostBorePiercing = 220;
        public const int CostExtendedDrumMags = 180;
        public const int CostOverclockedReceiver = 180;

        public event Action Changed;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public bool HasMod(ModCoreType mod) => installedMods.Contains(mod);

        /// <summary>
        /// Magazine capacity for a definition with installed mods applied.
        /// <para>
        /// Single source of truth, and it has to stay that way: Weapon and WeaponLoadout
        /// both ask here, so a magazine can never be refilled to a different size than the
        /// weapon can actually hold. Computing it in two places is exactly how the Armory
        /// ended up topping an upgraded rifle to 30 rounds while it held 45.
        /// </para>
        /// Safe before the Armory exists - with no instance, nothing is installed.
        /// </summary>
        public static int EffectiveMagazineSize(WeaponDefinition definition)
        {
            if (definition == null) return 0;

            float mult = Instance != null && Instance.HasMod(ModCoreType.ExtendedDrumMags) ? 1.5f : 1f;
            return Mathf.RoundToInt(definition.MagazineSize * mult);
        }

        public bool CanAfford(int cost) =>
            GameManager.Instance != null && GameManager.Instance.Gold >= cost;

        public bool TryBuyWeapon(int slot, int cost, WeaponLoadout loadout)
        {
            if (loadout == null || loadout.IsSlotUnlocked(slot)) return false;
            if (GameManager.Instance == null || !GameManager.Instance.TrySpendGold(cost)) return false;

            loadout.UnlockSlot(slot);
            loadout.Select(slot, force: true);
            Changed?.Invoke();
            return true;
        }

        public bool TryBuyAmmoCrate(WeaponLoadout loadout)
        {
            if (loadout == null) return false;
            if (GameManager.Instance == null || !GameManager.Instance.TrySpendGold(CostAmmoCrate)) return false;

            loadout.RefillAllReserves();
            Changed?.Invoke();
            return true;
        }

        public bool TryBuyBarricade(BarricadePlacer placer)
        {
            if (placer == null) return false;
            if (GameManager.Instance == null || !GameManager.Instance.TrySpendGold(CostBarricade)) return false;

            placer.AddBarricades(1);
            Changed?.Invoke();
            return true;
        }

        public bool TryBuyMod(ModCoreType mod, int cost)
        {
            if (installedMods.Contains(mod)) return false;
            if (GameManager.Instance == null || !GameManager.Instance.TrySpendGold(cost)) return false;

            installedMods.Add(mod);
            Changed?.Invoke();
            return true;
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// Manages purchases of weapons, ammo crates and deployables. Deducts gold through
    /// GameManager and coordinates with WeaponLoadout.
    /// <para>
    /// Mod cores used to live here too - five global upgrades bought once and applied by
    /// weapon tag. They were removed once weapon upgrade trees landed: two systems making a
    /// weapon stronger, neither aware of the other, with untested stacking between them.
    /// Trees replace them, so this is deletion rather than migration.
    /// </para>
    /// </summary>
    public class ArmoryManager : MonoBehaviour
    {
        public static ArmoryManager Instance { get; private set; }

        [SerializeField] ArmoryPrices prices;

        // Static passthroughs so every existing call site is unchanged, but the numbers now
        // come from an asset. The literals are only a fallback for an unwired manager.
        static ArmoryPrices P => Instance != null ? Instance.prices : null;

        public static int CostAmmoCrate => P != null ? P.AmmoCrate : 50;

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

            return UpgradeManager.Resolve(definition).MagazineSize;
        }

        public bool CanAfford(int cost) =>
            GameManager.Instance != null && GameManager.Instance.Gold >= cost;

        /// <summary>
        /// Buys a weapon into a free carried slot. Price comes from the definition, so a new
        /// weapon needs no new method, constant or branch here.
        /// </summary>
        public bool TryBuyWeapon(WeaponDefinition definition, WeaponLoadout loadout)
        {
            if (definition == null || loadout == null) return false;
            if (loadout.Owns(definition) || !loadout.HasFreeSlot) return false;
            if (GameManager.Instance == null || !GameManager.Instance.TrySpendGold(definition.Cost)) return false;

            loadout.TryCarry(definition);
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

        /// <summary>
        /// Buys one of whatever deployable sits at that catalogue index. Price comes from the
        /// definition, so a new deployable needs no new purchase method here.
        /// </summary>
        public bool TryBuyDeployable(DeployablePlacer placer, int index)
        {
            if (placer == null) return false;

            var definition = placer.DefinitionAt(index);
            if (definition == null) return false;
            if (GameManager.Instance == null || !GameManager.Instance.TrySpendGold(definition.Cost)) return false;

            placer.AddStock(index, 1);
            Changed?.Invoke();
            return true;
        }
    }
}

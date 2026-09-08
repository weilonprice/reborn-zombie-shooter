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

        [SerializeField] ArmoryPrices prices;

        readonly HashSet<ModCoreType> installedMods = new();

        // Static passthroughs so every existing call site is unchanged, but the numbers now
        // come from an asset. The literals are only a fallback for an unwired manager.
        static ArmoryPrices P => Instance != null ? Instance.prices : null;

        public static int CostShotgun => P != null ? P.Shotgun : 150;
        public static int CostAssaultRifle => P != null ? P.AssaultRifle : 250;
        public static int CostSniper => P != null ? P.Sniper : 350;
        public static int CostAmmoCrate => P != null ? P.AmmoCrate : 50;
        public static int CostBarricade => P != null ? P.Barricade : 40;

        public static int CostDragonsBreath => P != null ? P.DragonsBreath : 200;
        public static int CostHeavySlug => P != null ? P.HeavySlug : 200;
        public static int CostBorePiercing => P != null ? P.BorePiercing : 220;
        public static int CostExtendedDrumMags => P != null ? P.ExtendedDrumMags : 180;
        public static int CostOverclockedReceiver => P != null ? P.OverclockedReceiver : 180;

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

        /// <summary>
        /// Whether an installed mod actually affects a given weapon.
        /// <para>
        /// Applicability is decided by the weapon's tags, never by its position in the
        /// loadout. The previous slot-index checks meant reordering slots would have
        /// silently reassigned every mod, and a fifth weapon would have needed edits to
        /// Weapon.cs - which is exactly what moving weapons into ScriptableObjects was
        /// supposed to prevent.
        /// </para>
        /// </summary>
        public static bool ModAppliesTo(ModCoreType mod, WeaponDefinition definition)
        {
            if (definition == null || Instance == null || !Instance.HasMod(mod)) return false;

            return mod switch
            {
                ModCoreType.ExtendedDrumMags => true,
                ModCoreType.DragonsBreath => definition.HasAnyTag(WeaponTags.Shotgun),
                ModCoreType.HeavySlug => definition.HasAnyTag(WeaponTags.Shotgun),
                ModCoreType.BorePiercing => definition.HasAnyTag(WeaponTags.Rifle | WeaponTags.Precision),
                // The weapon declares its own response, so "does this apply" is simply
                // "does this weapon react to it".
                ModCoreType.OverclockedReceiver =>
                    definition.OverclockedConvertsToAuto || definition.OverclockedFireRateMultiplier > 1f,
                _ => false,
            };
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

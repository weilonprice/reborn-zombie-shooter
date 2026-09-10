using System;
using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// One purchasable step on an upgrade path.
    /// <para>
    /// Stat fields use -1 to mean "leave unchanged", so a tier only states what it actually
    /// alters. Behaviour flags are a deliberately fixed vocabulary rather than a general
    /// scripting system: adding a <em>new kind</em> of effect still needs code, but adding
    /// new tiers and paths from the existing vocabulary is pure data. That is the honest
    /// improvement over one hardcoded branch per mod.
    /// </para>
    /// </summary>
    [Serializable]
    public class UpgradeTier
    {
        public string title = "Tier";
        [TextArea(2, 3)] public string description = "";
        public int cost = 60;

        [Header("Stat overrides (-1 leaves the stat alone)")]
        public float damage = -1f;
        public float fireRate = -1f;
        public int magazineSize = -1;
        public float reloadTime = -1f;
        public int pierceCount = -1;
        public float knockbackMultiplier = -1f;
        [Tooltip("Pellets per trigger pull. -1 leaves it alone.")]
        public int pelletCount = -1;
        [Tooltip("Base spread in degrees. -1 leaves it alone.")]
        public float spread = -1f;
        [Tooltip("Damage retained per body pierced. Above 1 the round gains. -1 leaves it alone.")]
        public float penetrationFalloff = -1f;
        [Tooltip("Effective range in metres. -1 leaves it alone.")]
        public float range = -1f;

        [Header("Delivery overrides (-1 keeps the delivery asset's own value)")]
        [Tooltip("Cone half-angle in degrees, for flame weapons.")]
        public float coneHalfAngle = -1f;
        [Tooltip("How many bodies a chain hops to after the first.")]
        public int chainBounces = -1;
        [Tooltip("How far a chain reaches for its next target.")]
        public float chainHopRange = -1f;
        [Tooltip("Damage multiplier per chain hop. Above 1 the arc grows down the chain.")]
        public float chainDamagePerHop = -1f;
        [Tooltip("Explosion radius for projectile weapons. 0 makes them single-target.")]
        public float blastRadius = -1f;

        [Header("Behaviour")]
        public bool convertToFullAuto;
        [Tooltip("Refills the magazine over time while another weapon is equipped.")]
        public bool holsteredReload;
        [Tooltip("Each kill loads one round straight into the magazine.")]
        public bool killsReloadMagazine;
        [Tooltip("Kills with this weapon pay double gold.")]
        public bool doubleGoldOnKill;
        [Tooltip("Reserve rounds refunded per kill. 0 for none.")]
        public int reserveRefundPerKill;

        [Header("Akimbo")]
        [Tooltip("Draws a second gun. Shots alternate hands; the magazine and fire rate are " +
                 "raised by this tier's own stat fields, not implicitly.")]
        public bool dualWield;
        [Tooltip("How many enemies the off-hand picks for itself, ignoring the crosshair. 0 " +
                 "keeps both guns pointed where you aim.")]
        public int offHandTargets;

        [Header("Gunslinger")]
        [Tooltip("Chance any given shot crits, 0-1. -1 leaves it alone.")]
        public float critChance = -1f;
        [Tooltip("Damage multiplier on a crit. -1 leaves it alone.")]
        public float critMultiplier = -1f;
        [Tooltip("The first shot after a reload always crits.")]
        public bool guaranteedCritAfterReload;
        [Tooltip("Rate of fire while the trigger is held down. 0 keeps the weapon on clicks.")]
        public float fanFireRate = -1f;
        [Tooltip("Spread the weapon degrades to while held. -1 leaves it alone.")]
        public float fanMaxSpread = -1f;
        [Tooltip("Seconds of holding to reach full spread. -1 leaves it alone.")]
        public float fanSpreadRamp = -1f;
        [Tooltip("Reserve ammunition stops depleting. Reloads still happen.")]
        public bool infiniteReserve;
        [Tooltip("Kills with this weapon needed to charge the ultimate. 0 for no ultimate.")]
        public int ultimateKills;
        [Tooltip("Crit chance while the ultimate is running, 0-1. -1 leaves it alone.")]
        public float ultimateCritChance = -1f;
        [Tooltip("Rate of fire while the ultimate is running. -1 leaves it alone.")]
        public float ultimateFireRate = -1f;

        [Header("Marksman")]
        [Tooltip("Damage added per consecutive hit on the same target, as a fraction. " +
                 "Resets when you hit something else. -1 leaves it alone.")]
        public float focusBonusPerHit = -1f;
        [Tooltip("Ceiling on the focus bonus, as a fraction. -1 leaves it alone.")]
        public float focusMaxBonus = -1f;
        [Tooltip("Spread the weapon tightens to while the trigger is held - the inverse of " +
                 "fanMaxSpread. -1 leaves it alone.")]
        public float sustainedSpreadMin = -1f;

        [Header("Adrenaline")]
        [Tooltip("Move speed added per kill, as a fraction. Lapses if you stop killing.")]
        public float killSpeedBonus = -1f;
        [Tooltip("Fire rate added per kill, as a fraction.")]
        public float killFireRateBonus = -1f;
        [Tooltip("Ceiling on stacked kills.")]
        public int killStackMax = -1;
        [Tooltip("Seconds a stack survives without another kill.")]
        public float killStackSeconds = -1f;

        [Header("Lawman")]
        [Tooltip("Health restored to your own barricade when you shoot it. 0 for none.")]
        public float barricadeRepair = -1f;
        [Tooltip("Seconds a hit enemy is pinned in place. 0 for none.")]
        public float pinSeconds = -1f;
        [Tooltip("Barricades you place mount a turret firing this weapon.")]
        public bool grantsBarricadeTurret;

        [Header("Reaper")]
        [Tooltip("Health restored per kill. 0 for none.")]
        public float lifestealPerKill = -1f;
        [Tooltip("Extra damage at 1 HP, as a fraction, scaling with health lost.")]
        public float missingHealthDamageBonus = -1f;
        [Tooltip("Times a wave that a lethal hit leaves you at 1 HP instead.")]
        public int revivesPerWave = -1;

        [Header("Executioner")]
        [Tooltip("Fraction of max health at or below which a hit is simply lethal. -1 for none.")]
        public float executeThreshold = -1f;
    }

    [Serializable]
    public class WeaponUpgradePath
    {
        public string title = "Path";
        [TextArea(1, 2)] public string summary = "";
        public UpgradeTier[] tiers = new UpgradeTier[5];
    }

    /// <summary>
    /// The three upgrade paths available for one weapon.
    /// </summary>
    [CreateAssetMenu(menuName = "Zombie Shooter/Weapon Upgrade Tree", fileName = "UPG_NewTree")]
    public class WeaponUpgradeTree : ScriptableObject
    {
        [SerializeField] WeaponDefinition weapon;
        [SerializeField] WeaponUpgradePath[] paths = new WeaponUpgradePath[3];

        public WeaponDefinition Weapon => weapon;
        public int PathCount => paths != null ? paths.Length : 0;

        public WeaponUpgradePath PathAt(int index) =>
            paths != null && index >= 0 && index < paths.Length ? paths[index] : null;

#if UNITY_EDITOR
        /// <summary>
        /// Editor-only authoring hook for ArenaBuilder. Building fifteen nested tiers through
        /// SerializedProperty would be far more code than constructing them directly, and this
        /// is compiled out of player builds.
        /// </summary>
        public void EditorInitialise(WeaponDefinition owner, WeaponUpgradePath[] authored)
        {
            weapon = owner;
            paths = authored;
        }
#endif

        public UpgradeTier TierAt(int pathIndex, int tierIndex)
        {
            var path = PathAt(pathIndex);
            if (path == null || path.tiers == null) return null;
            return tierIndex >= 0 && tierIndex < path.tiers.Length ? path.tiers[tierIndex] : null;
        }
    }
}

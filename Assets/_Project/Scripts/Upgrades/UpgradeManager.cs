using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// Owns which upgrade tiers the player has bought, enforces the 5-3-0 rule, and resolves
    /// a weapon's effective stats.
    /// <para>
    /// The restriction is the design, not a limitation: without it every run buys everything
    /// and every weapon converges on the same build. One path to 5, a second to 3, the third
    /// locked at 0 is what makes a maxed Magnum a different weapon from a maxed Machine
    /// Pistol rather than a later version of it.
    /// </para>
    /// </summary>
    public class UpgradeManager : MonoBehaviour
    {
        public const int MaxTier = 5;
        /// <summary>Highest tier the second path may reach.</summary>
        public const int SecondaryMaxTier = 3;
        /// <summary>How many paths may be taken above tier 0 at all.</summary>
        public const int MaxOpenPaths = 2;

        public static UpgradeManager Instance { get; private set; }

        [SerializeField] WeaponUpgradeTree[] trees;

        readonly Dictionary<WeaponDefinition, int[]> purchased = new();
        readonly Dictionary<WeaponDefinition, WeaponStats> resolved = new();
        readonly Dictionary<WeaponDefinition, WeaponUpgradeTree> treeByWeapon = new();

        public event Action Changed;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            if (trees == null) return;
            foreach (var tree in trees)
            {
                if (tree == null || tree.Weapon == null) continue;
                treeByWeapon[tree.Weapon] = tree;
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public WeaponUpgradeTree TreeFor(WeaponDefinition weapon) =>
            weapon != null && treeByWeapon.TryGetValue(weapon, out var tree) ? tree : null;

        /// <summary>Tiers bought per path. Never null for a weapon with a tree.</summary>
        public int[] TiersFor(WeaponDefinition weapon)
        {
            if (weapon == null) return Array.Empty<int>();

            if (!purchased.TryGetValue(weapon, out var tiers))
            {
                var tree = TreeFor(weapon);
                tiers = new int[tree != null ? tree.PathCount : 3];
                purchased[weapon] = tiers;
            }
            return tiers;
        }

        public int TierIn(WeaponDefinition weapon, int path)
        {
            var tiers = TiersFor(weapon);
            return path >= 0 && path < tiers.Length ? tiers[path] : 0;
        }

        /// <summary>
        /// Whether the next tier on a path could be bought, ignoring cost. Separated from
        /// affordability so the UI can show "locked by the 5-3-0 rule" differently from
        /// "you cannot afford it yet".
        /// </summary>
        public bool IsNextTierAllowed(WeaponDefinition weapon, int path)
        {
            var tree = TreeFor(weapon);
            if (tree == null) return false;

            var tiers = TiersFor(weapon);
            if (path < 0 || path >= tiers.Length) return false;

            int next = tiers[path] + 1;
            if (next > MaxTier) return false;
            if (tree.TierAt(path, next - 1) == null) return false;

            // Evaluate the configuration this purchase would produce, rather than the one
            // it starts from - the rule is about the result.
            int open = 0, aboveSecondary = 0;
            for (int i = 0; i < tiers.Length; i++)
            {
                int value = i == path ? next : tiers[i];
                if (value > 0) open++;
                if (value > SecondaryMaxTier) aboveSecondary++;
            }

            return open <= MaxOpenPaths && aboveSecondary <= 1;
        }

        public UpgradeTier NextTier(WeaponDefinition weapon, int path)
        {
            var tree = TreeFor(weapon);
            if (tree == null) return null;

            int next = TierIn(weapon, path) + 1;
            return next > MaxTier ? null : tree.TierAt(path, next - 1);
        }

        public bool TryBuyNextTier(WeaponDefinition weapon, int path)
        {
            if (!IsNextTierAllowed(weapon, path)) return false;

            var tier = NextTier(weapon, path);
            if (tier == null) return false;
            if (GameManager.Instance == null || !GameManager.Instance.TrySpendGold(tier.cost)) return false;

            TiersFor(weapon)[path]++;
            resolved.Remove(weapon);

            Changed?.Invoke();
            return true;
        }

        /// <summary>Effective stats with purchased tiers applied. Cached until a purchase.</summary>
        public WeaponStats StatsFor(WeaponDefinition weapon)
        {
            if (weapon == null) return default;
            if (resolved.TryGetValue(weapon, out var cached)) return cached;

            var stats = WeaponStats.FromDefinition(weapon);
            var tree = TreeFor(weapon);

            if (tree != null)
            {
                var tiers = TiersFor(weapon);
                for (int path = 0; path < tiers.Length; path++)
                {
                    for (int t = 0; t < tiers[path]; t++)
                    {
                        var tier = tree.TierAt(path, t);
                        if (tier != null) Apply(ref stats, tier);
                    }
                }
            }

            resolved[weapon] = stats;
            return stats;
        }

        static void Apply(ref WeaponStats stats, UpgradeTier tier)
        {
            // -1 means the tier does not speak to that stat, so later tiers on the same path
            // can raise damage without silently resetting everything else.
            if (tier.damage >= 0f) stats.Damage = tier.damage;
            if (tier.fireRate >= 0f) stats.FireRate = tier.fireRate;
            if (tier.magazineSize >= 0) stats.MagazineSize = tier.magazineSize;
            if (tier.reloadTime >= 0f) stats.ReloadTime = tier.reloadTime;
            if (tier.pierceCount >= 0) stats.PierceCount = tier.pierceCount;
            if (tier.knockbackMultiplier >= 0f) stats.KnockbackMultiplier = tier.knockbackMultiplier;
            if (tier.pelletCount >= 0) stats.PelletCount = tier.pelletCount;
            if (tier.spread >= 0f) stats.Spread = tier.spread;
            if (tier.penetrationFalloff >= 0f) stats.PenetrationFalloff = tier.penetrationFalloff;
            if (tier.range >= 0f) stats.Range = tier.range;

            if (tier.coneHalfAngle >= 0f) stats.ConeHalfAngle = tier.coneHalfAngle;
            if (tier.chainBounces >= 0) stats.ChainBounces = tier.chainBounces;
            if (tier.chainHopRange >= 0f) stats.ChainHopRange = tier.chainHopRange;
            if (tier.chainDamagePerHop >= 0f) stats.ChainDamagePerHop = tier.chainDamagePerHop;
            if (tier.blastRadius >= 0f) stats.BlastRadius = tier.blastRadius;

            if (tier.convertToFullAuto) stats.FullAuto = true;
            if (tier.holsteredReload) stats.HolsteredReload = true;
            if (tier.killsReloadMagazine) stats.KillsReloadMagazine = true;
            if (tier.doubleGoldOnKill) stats.DoubleGoldOnKill = true;
            if (tier.reserveRefundPerKill > 0)
                stats.ReserveRefundPerKill = Mathf.Max(stats.ReserveRefundPerKill, tier.reserveRefundPerKill);

            if (tier.dualWield) stats.DualWield = true;
            if (tier.offHandTargets > 0)
                stats.OffHandTargets = Mathf.Max(stats.OffHandTargets, tier.offHandTargets);

            if (tier.critChance >= 0f) stats.CritChance = tier.critChance;
            if (tier.critMultiplier >= 0f) stats.CritMultiplier = tier.critMultiplier;
            if (tier.guaranteedCritAfterReload) stats.GuaranteedCritAfterReload = true;

            if (tier.fanFireRate >= 0f) stats.FanFireRate = tier.fanFireRate;
            if (tier.fanMaxSpread >= 0f) stats.FanMaxSpread = tier.fanMaxSpread;
            if (tier.fanSpreadRamp >= 0f) stats.FanSpreadRamp = tier.fanSpreadRamp;

            if (tier.infiniteReserve) stats.InfiniteReserve = true;

            if (tier.ultimateKills > 0) stats.UltimateKills = tier.ultimateKills;
            if (tier.ultimateCritChance >= 0f) stats.UltimateCritChance = tier.ultimateCritChance;
            if (tier.ultimateFireRate >= 0f) stats.UltimateFireRate = tier.ultimateFireRate;

            if (tier.focusBonusPerHit >= 0f) stats.FocusBonusPerHit = tier.focusBonusPerHit;
            if (tier.focusMaxBonus >= 0f) stats.FocusMaxBonus = tier.focusMaxBonus;
            if (tier.sustainedSpreadMin >= 0f) stats.SustainedSpreadMin = tier.sustainedSpreadMin;
            if (tier.executeThreshold >= 0f) stats.ExecuteThreshold = tier.executeThreshold;

            if (tier.killSpeedBonus >= 0f) stats.KillSpeedBonus = tier.killSpeedBonus;
            if (tier.killFireRateBonus >= 0f) stats.KillFireRateBonus = tier.killFireRateBonus;
            if (tier.killStackMax >= 0) stats.KillStackMax = tier.killStackMax;
            if (tier.killStackSeconds >= 0f) stats.KillStackSeconds = tier.killStackSeconds;

            if (tier.barricadeRepair >= 0f) stats.BarricadeRepair = tier.barricadeRepair;
            if (tier.pinSeconds >= 0f) stats.PinSeconds = tier.pinSeconds;
            if (tier.grantsBarricadeTurret) stats.GrantsBarricadeTurret = true;

            if (tier.lifestealPerKill >= 0f) stats.LifestealPerKill = tier.lifestealPerKill;
            if (tier.missingHealthDamageBonus >= 0f)
                stats.MissingHealthDamageBonus = tier.missingHealthDamageBonus;
            if (tier.revivesPerWave >= 0) stats.RevivesPerWave = tier.revivesPerWave;
        }

        /// <summary>Convenience for callers that may run before the manager exists.</summary>
        public static WeaponStats Resolve(WeaponDefinition weapon)
        {
            if (weapon == null) return default;
            return Instance != null ? Instance.StatsFor(weapon) : WeaponStats.FromDefinition(weapon);
        }
    }
}

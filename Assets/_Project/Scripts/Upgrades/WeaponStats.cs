namespace ZombieShooter
{
    /// <summary>
    /// A weapon's stats after its purchased upgrades are applied. Resolved once per purchase
    /// and cached, so the firing path reads a plain struct rather than walking tiers per shot.
    /// </summary>
    public struct WeaponStats
    {
        public float Damage;
        public float FireRate;
        public float ReloadTime;
        public float KnockbackMultiplier;
        public int MagazineSize;
        public int PierceCount;
        public int PelletCount;
        public float Spread;
        public float PenetrationFalloff;

        public bool FullAuto;
        public bool HolsteredReload;
        public bool KillsReloadMagazine;
        public bool DoubleGoldOnKill;
        public int ReserveRefundPerKill;

        public bool DualWield;
        public int OffHandTargets;

        public float CritChance;
        public float CritMultiplier;
        public bool GuaranteedCritAfterReload;

        /// <summary>0 means the trigger is not a fan - the weapon still fires per click.</summary>
        public float FanFireRate;
        public float FanMaxSpread;
        public float FanSpreadRamp;

        public bool InfiniteReserve;

        /// <summary>0 means this weapon has no ultimate.</summary>
        public int UltimateKills;
        public float UltimateCritChance;
        public float UltimateFireRate;

        public float FocusBonusPerHit;
        public float FocusMaxBonus;
        /// <summary>Negative means the weapon does not tighten while held.</summary>
        public float SustainedSpreadMin;

        /// <summary>0 disables executions; otherwise a fraction of the target's max health.</summary>
        public float ExecuteThreshold;

        public static WeaponStats FromDefinition(WeaponDefinition d) => new()
        {
            Damage = d.Damage,
            FireRate = d.FireRate,
            ReloadTime = d.ReloadTime,
            KnockbackMultiplier = d.KnockbackMultiplier,
            MagazineSize = d.MagazineSize,
            PierceCount = d.PierceCount,
            PelletCount = d.PelletsPerShot,
            Spread = d.Spread,
            PenetrationFalloff = d.PenetrationFalloff,
            FullAuto = d.Mode == FireMode.Automatic,

            // Defaults behind the behaviour flags, so a tier can switch an effect on without
            // restating every number it runs on.
            CritMultiplier = 2f,
            FanSpreadRamp = 1f,
            SustainedSpreadMin = -1f,
        };
    }
}

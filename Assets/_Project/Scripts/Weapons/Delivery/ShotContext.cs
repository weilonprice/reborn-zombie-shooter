using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// Everything a <see cref="WeaponDelivery"/> needs to put one shot into the world.
    /// <para>
    /// The split is deliberate. <see cref="Weapon"/> owns whatever is true of every firearm -
    /// ammo, rate of fire, which hand fired, whether the shot crit, and which direction the
    /// ultimate picked. A delivery owns only how the shot travels. Anything a delivery needs
    /// to know belongs in here rather than reached for through the weapon, so a delivery can
    /// be reasoned about without reading Weapon.cs.
    /// </para>
    /// </summary>
    public readonly struct ShotContext
    {
        public readonly Weapon Weapon;
        public readonly WeaponDefinition Definition;
        public readonly WeaponStats Stats;

        /// <summary>Where hit tests start - the player's centre, never the muzzle.</summary>
        public readonly Vector3 Origin;
        public readonly Vector3 Direction;
        /// <summary>Where tracers and effects come from, which is the muzzle.</summary>
        public readonly Vector3 MuzzlePosition;

        /// <summary>Degrees of cone. Zero for auto-aimed shots, which must not miss.</summary>
        public readonly float Spread;
        public readonly bool Crit;
        public readonly LayerMask HitMask;

        public ShotContext(Weapon weapon, WeaponDefinition definition, in WeaponStats stats,
                           Vector3 origin, Vector3 direction, Vector3 muzzlePosition,
                           float spread, bool crit, LayerMask hitMask)
        {
            Weapon = weapon;
            Definition = definition;
            Stats = stats;
            Origin = origin;
            Direction = direction;
            MuzzlePosition = muzzlePosition;
            Spread = spread;
            Crit = crit;
            HitMask = hitMask;
        }

        /// <summary>Base damage for one projectile of this shot, crit already applied.</summary>
        public float Damage => Crit ? Stats.Damage * Stats.CritMultiplier : Stats.Damage;

        public int PelletCount => Stats.PelletCount > 0 ? Stats.PelletCount : 1;

        const float VerticalSpreadScale = 0.15f;

        /// <summary>
        /// One direction inside the spread cone. Horizontal-dominant: the camera looks down,
        /// so vertical scatter is nearly invisible and only costs accuracy the player cannot
        /// see themselves losing.
        /// </summary>
        public Vector3 SpreadDirection()
        {
            if (Spread <= 0f) return Direction;

            float yaw = Random.Range(-Spread, Spread);
            float pitch = Random.Range(-Spread, Spread) * VerticalSpreadScale;

            return Quaternion.Euler(pitch, yaw, 0f) * Direction;
        }
    }
}

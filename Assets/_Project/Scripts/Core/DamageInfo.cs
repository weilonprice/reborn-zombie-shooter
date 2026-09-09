using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// Everything a receiver needs to know about one instance of damage.
    /// <para>
    /// Replaces the old three loose arguments so weapons can carry their own punch without
    /// the signature growing again for crits, damage types or status effects later.
    /// </para>
    /// </summary>
    public readonly struct DamageInfo
    {
        public readonly float Amount;
        public readonly Vector3 Point;
        /// <summary>Surface normal at the hit, pointing back toward whatever caused it.</summary>
        public readonly Vector3 Normal;
        /// <summary>
        /// Scales the receiver's own knockback, rather than setting it outright. A shotgun
        /// says "shove hard"; how far a given body actually moves stays that body's business,
        /// so a heavy enemy can shrug off the same hit that flings a normal one.
        /// </summary>
        public readonly float KnockbackMultiplier;
        public readonly GameObject Source;
        /// <summary>
        /// The weapon responsible, when there is one. Source is the shooter, which is the
        /// player for every gun - so anything keyed to "kills with this specific weapon"
        /// needs this instead.
        /// </summary>
        public readonly WeaponDefinition SourceWeapon;

        public DamageInfo(float amount, Vector3 point, Vector3 normal,
                          float knockbackMultiplier = 1f, GameObject source = null,
                          WeaponDefinition sourceWeapon = null)
        {
            Amount = amount;
            Point = point;
            Normal = normal;
            KnockbackMultiplier = knockbackMultiplier;
            Source = source;
            SourceWeapon = sourceWeapon;
        }
    }
}

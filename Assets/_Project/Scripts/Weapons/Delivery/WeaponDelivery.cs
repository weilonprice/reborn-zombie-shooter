using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// How a weapon's shot reaches the world: a hitscan line, a cone of flame, a chain that
    /// hops between bodies, a projectile with travel time.
    /// <para>
    /// A ScriptableObject rather than an enum with a branch per mode, for the same reason
    /// deployables became definitions and mod cores were deleted: an enum means every new
    /// kind of weapon edits the same file, and six of the ten planned weapons need a mode
    /// the original hitscan path could not express. Build it once here, and a new mode is a
    /// new class plus an asset.
    /// </para>
    /// </summary>
    public abstract class WeaponDelivery : ScriptableObject
    {
        /// <summary>
        /// Delivers one shot. Returns whether anything was hit, and where the first impact
        /// landed so the weapon can place its impact sound.
        /// </summary>
        public abstract bool Deliver(in ShotContext shot, out Vector3 firstImpact);
    }
}

using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// Lets an archetype walk somewhere other than at the player.
    /// <para>
    /// Steering only. Attacks still resolve against whatever the enemy is actually pressed
    /// against, so an enemy that walks at a barricade attacks it through the ordinary
    /// blocking check rather than needing an attack path of its own.
    /// </para>
    /// </summary>
    public interface IEnemySteerOverride
    {
        /// <summary>Where to walk, or null to chase the player as usual.</summary>
        Transform SteerTarget();
    }
}

using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// Gets a say before an enemy actually dies.
    /// <para>
    /// Asked BEFORE the death sequence runs, not after. By the time Health.Died has been
    /// handled the kill has already paid out score and gold, disabled the collider and queued
    /// a despawn - reviving from there means unpicking all of it. Refusing the death up front
    /// means none of it happened.
    /// </para>
    /// </summary>
    public interface IDeathInterceptor
    {
        /// <summary>True if the enemy should survive this killing blow.</summary>
        bool TryPreventDeath();
    }
}

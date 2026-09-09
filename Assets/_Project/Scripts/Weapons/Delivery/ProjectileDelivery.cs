using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// Fires a round with travel time instead of an instant line - the grenade launcher's
    /// lob and the nail gun's bolt.
    /// <para>
    /// Reports no hit, ever. Nothing has been struck at the moment of firing, and claiming
    /// otherwise would play an impact sound at the muzzle for a grenade still in the air.
    /// The projectile owns its own impact.
    /// </para>
    /// </summary>
    [CreateAssetMenu(menuName = "Zombie Shooter/Delivery/Projectile", fileName = "DLV_Projectile")]
    public class ProjectileDelivery : WeaponDelivery
    {
        [SerializeField] WeaponProjectile projectilePrefab;
        [Tooltip("Radius of the explosion on impact. 0 makes it a single-target round.")]
        [SerializeField] float blastRadius = 4.5f;
        [Tooltip("Metres in front of the muzzle to spawn, so the round clears the player.")]
        [SerializeField] float spawnOffset = 0.6f;

        public override bool Deliver(in ShotContext shot, out Vector3 firstImpact)
        {
            firstImpact = Vector3.zero;

            if (projectilePrefab == null)
            {
                Debug.LogError($"{name}: no projectile prefab assigned - this weapon cannot fire.");
                return false;
            }

            int rounds = shot.PelletCount;

            for (int i = 0; i < rounds; i++)
            {
                var direction = shot.SpreadDirection();

                WeaponProjectile.Spawn(
                    projectilePrefab,
                    shot.MuzzlePosition + direction * spawnOffset,
                    direction,
                    shot.Damage,
                    shot.Stats.KnockbackMultiplier,
                    blastRadius,
                    shot.Weapon.gameObject,
                    shot.Definition);
            }

            return false;
        }
    }
}

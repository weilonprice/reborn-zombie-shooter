using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// A short cone that damages everything inside it, for the flamethrower.
    /// <para>
    /// Continuous fire is modelled as a very high rate of very small shots rather than as a
    /// separate "beam" concept. That keeps ammo, reload, rate of fire, crits and upgrades
    /// working exactly as they do for every other weapon - a fuel tank is a magazine, and a
    /// tick of flame is a round. The alternative was a second firing lifecycle running
    /// beside the first, with its own bugs.
    /// </para>
    /// </summary>
    [CreateAssetMenu(menuName = "Zombie Shooter/Delivery/Cone", fileName = "DLV_Cone")]
    public class ConeDelivery : WeaponDelivery
    {
        [Tooltip("Half-angle of the cone in degrees. 30 is a wide gout; 12 is a jet.")]
        [SerializeField] float halfAngle = 26f;
        [Tooltip("Damage falls off toward the edge of the cone and the end of its reach.")]
        [SerializeField] bool falloffWithDistance = true;
        [Tooltip("Damage retained at maximum range when falloff is on.")]
        [SerializeField, Range(0f, 1f)] float minimumFalloff = 0.45f;
        [Tooltip("Level geometry stops the flame. Barricades and other damageables do not - " +
                 "bullets already pass through your own walls, and flame that did not would " +
                 "be inconsistent with the rest of the arsenal.")]
        [SerializeField] bool blockedByGeometry = true;

        static readonly RaycastHit[] SightBuffer = new RaycastHit[12];

        public override bool Deliver(in ShotContext shot, out Vector3 firstImpact)
        {
            firstImpact = Vector3.zero;

            var zombies = ZombieAI.ActiveZombies;
            if (zombies == null) return false;

            float range = shot.Range;
            float sqrRange = range * range;

            // Upgrades widen the cone; the asset only supplies the starting angle.
            float angle = shot.Stats.ConeHalfAngle >= 0f ? shot.Stats.ConeHalfAngle : halfAngle;
            float cosLimit = Mathf.Cos(angle * Mathf.Deg2Rad);
            float damage = shot.Damage;

            bool anyHit = false;

            // Backwards: a tick can kill, and a death only unregisters on despawn, but that
            // is ZombieAI's lifecycle rather than a guarantee worth leaning on.
            for (int i = zombies.Count - 1; i >= 0; i--)
            {
                var zombie = zombies[i];
                if (zombie == null) continue;

                var health = zombie.Health;
                if (health == null || !health.IsAlive) continue;

                var offset = zombie.transform.position - shot.Origin;
                float sqrDistance = offset.sqrMagnitude;
                if (sqrDistance > sqrRange || sqrDistance < 0.0001f) continue;

                float distance = Mathf.Sqrt(sqrDistance);
                var toTarget = offset / distance;
                if (Vector3.Dot(shot.Direction, toTarget) < cosLimit) continue;

                // Tested only for enemies already inside the cone, which is a handful even
                // in a horde - not once per live zombie.
                if (blockedByGeometry && WallBetween(shot, toTarget, distance)) continue;

                float scaled = damage;
                if (falloffWithDistance)
                    scaled *= Mathf.Lerp(1f, minimumFalloff, distance / range);

                var point = zombie.transform.position;
                shot.Weapon.ApplyShot(health, health, point, -toTarget,
                                      scaled, shot.Stats.KnockbackMultiplier);

                if (!anyHit)
                {
                    anyHit = true;
                    firstImpact = point;
                }
            }

            // One puff per tick regardless of how many it caught, or a wide cone into a
            // horde would spawn a particle burst per enemy per frame.
            ImpactEffects.Instance?.PlayImpact(
                shot.MuzzlePosition + shot.Direction * (range * 0.5f), -shot.Direction);

            return anyHit;
        }

        static bool WallBetween(in ShotContext shot, Vector3 direction, float distance)
        {
            int count = Physics.RaycastNonAlloc(shot.Origin, direction, SightBuffer, distance,
                                                shot.HitMask, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                // Anything damageable is a body or a barricade, and flame goes past both.
                if (SightBuffer[i].collider.GetComponentInParent<IDamageable>() == null)
                    return true;
            }

            return false;
        }
    }
}

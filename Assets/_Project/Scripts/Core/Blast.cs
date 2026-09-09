using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// Radius damage from a player source. Hits enemies and sets off explosive barrels, and
    /// deliberately touches neither the player nor their own barricades.
    /// <para>
    /// Enemies come from <see cref="ZombieAI.ActiveZombies"/> rather than a physics query.
    /// Nothing here uses layers, so an OverlapSphere on the default layer would also return
    /// the floor, the walls, the player and their fortifications, and would then need a
    /// component lookup per collider to reject them. Barrels get their own small query
    /// because they are the one thing a blast SHOULD reach out and touch.
    /// </para>
    /// </summary>
    public static class Blast
    {
        static readonly Collider[] BarrelBuffer = new Collider[16];

        /// <summary>Returns how many enemies this blast killed.</summary>
        public static int Damage(Vector3 centre, float radius, float damage,
                                 GameObject source, WeaponDefinition weapon)
        {
            int killed = 0;

            var zombies = ZombieAI.ActiveZombies;
            if (zombies != null && damage > 0f)
            {
                float sqrRadius = radius * radius;

                // Backwards: a blast kills, and a death only unregisters on despawn, but
                // that is ZombieAI's lifecycle rather than a guarantee worth leaning on.
                for (int i = zombies.Count - 1; i >= 0; i--)
                {
                    var zombie = zombies[i];
                    if (zombie == null) continue;

                    var health = zombie.Health;
                    if (health == null || !health.IsAlive) continue;

                    var offset = zombie.transform.position - centre;
                    if (offset.sqrMagnitude > sqrRadius) continue;

                    var normal = offset.sqrMagnitude < 0.0001f ? Vector3.up : offset.normalized;
                    health.TakeDamage(new DamageInfo(damage, zombie.transform.position, normal,
                                                     1f, source, weapon));

                    if (!health.IsAlive) killed++;
                }
            }

            ChainBarrels(centre, radius);
            ImpactEffects.Instance?.PlayImpact(centre, Vector3.up);

            return killed;
        }

        /// <summary>
        /// Sets off any barrel caught in the blast. Barrels chain rather than take damage so
        /// one with more health than the blast deals still goes up with the line.
        /// </summary>
        static void ChainBarrels(Vector3 centre, float radius)
        {
            int count = Physics.OverlapSphereNonAlloc(centre, radius, BarrelBuffer, ~0,
                                                      QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                if (BarrelBuffer[i] == null) continue;

                var barrel = BarrelBuffer[i].GetComponentInParent<ExplosiveBarrel>();
                if (barrel != null && barrel.IsAlive) barrel.DetonateAfter(0.05f);
            }
        }
    }
}

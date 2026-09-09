using System.Collections.Generic;
using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// Picks live enemies for effects that choose their own targets: ricochet chains and the
    /// akimbo off-hand.
    /// <para>
    /// Callers pass in their own lists so a per-shot search allocates nothing. Results come
    /// back nearest first.
    /// </para>
    /// </summary>
    public static class TargetFinder
    {
        /// <summary>
        /// Fills <paramref name="results"/> with up to <paramref name="max"/> live enemies
        /// within <paramref name="radius"/>, nearest first, skipping anything in
        /// <paramref name="exclude"/>.
        /// </summary>
        public static void Gather(Vector3 origin, float radius, int max,
                                  List<Health> exclude, List<Health> results)
        {
            results.Clear();
            if (max <= 0) return;

            var zombies = ZombieAI.ActiveZombies;
            if (zombies == null) return;

            float sqrRadius = radius * radius;

            for (int i = 0; i < zombies.Count; i++)
            {
                var zombie = zombies[i];
                if (zombie == null) continue;

                var health = zombie.Health;
                if (health == null || !health.IsAlive) continue;
                if (exclude != null && exclude.Contains(health)) continue;

                float sqrDistance = (zombie.transform.position - origin).sqrMagnitude;
                if (sqrDistance > sqrRadius) continue;

                // Insertion sort into a list that is at most a handful long. A full sort of
                // every live zombie would be far more work than picking the best few.
                int slot = results.Count;
                while (slot > 0 &&
                       (results[slot - 1].transform.position - origin).sqrMagnitude > sqrDistance)
                    slot--;

                if (slot >= max) continue;

                results.Insert(slot, health);
                if (results.Count > max) results.RemoveAt(results.Count - 1);
            }
        }

        public static Health Nearest(Vector3 origin, float radius, List<Health> exclude, List<Health> scratch)
        {
            Gather(origin, radius, 1, exclude, scratch);
            return scratch.Count > 0 ? scratch[0] : null;
        }

        /// <summary>
        /// Where an auto-aimed shot should be sent. Every enemy's CharacterController is
        /// centred on its pivot, so the pivot already is centre mass - no vertical offset.
        /// </summary>
        public static Vector3 AimPoint(Health target) => target.transform.position;
    }
}

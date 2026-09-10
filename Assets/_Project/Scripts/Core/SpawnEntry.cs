using System;
using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// One archetype's place in the wave schedule: when it starts appearing and how common
    /// it becomes.
    /// <para>
    /// Weight is relative, not a percentage. The baseline zombie sits at 10 and never grows,
    /// so an archetype at 4.5 is roughly a third of spawns once it has ramped. Growth is what
    /// makes a wave feel different from the one before it without changing the count.
    /// </para>
    /// </summary>
    [Serializable]
    public class SpawnEntry
    {
        public ZombieAI prefab;

        [Tooltip("First wave this can appear on, 1-indexed.")]
        public int startWave = 1;
        [Tooltip("Relative weight on its first wave.")]
        public float weightAtStart = 1.2f;
        [Tooltip("Added to the weight for each wave since it started appearing.")]
        public float weightGrowthPerWave = 0.7f;
        [Tooltip("Ceiling, so a late wave cannot become entirely one archetype.")]
        public float weightCap = 4.5f;

        [Tooltip("How many arrive together when this entry is rolled. Above 1 the archetype " +
                 "reads as a pack rather than as a stream, and it counts as that many toward " +
                 "the wave's total.")]
        public int groupSize = 1;
    }
}

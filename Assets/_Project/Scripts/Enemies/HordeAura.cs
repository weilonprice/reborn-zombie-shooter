using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// Drives nearby zombies faster while it lives, for the Screamer.
    /// <para>
    /// The point is to create a priority target. Everything else in the horde is answered by
    /// shooting whatever is closest; this one has to be picked out of a crowd and killed
    /// first, which is a different decision from any other archetype makes you take.
    /// </para>
    /// <para>
    /// The buff is applied every tick and never explicitly removed. ZombieAI resets its own
    /// multiplier on spawn, and anything that leaves the radius simply stops being refreshed
    /// - so a dead screamer needs no cleanup pass and cannot leave the horde permanently
    /// enraged if it is despawned mid-frame.
    /// </para>
    /// </summary>
    public class HordeAura : MonoBehaviour
    {
        [SerializeField] float radius = 12f;
        [SerializeField] float speedMultiplier = 1.5f;
        [Tooltip("Seconds between refreshes. The buff lasts until the next one lapses, so " +
                 "this also decides how long it lingers after the screamer dies.")]
        [SerializeField] float tickInterval = 0.25f;

        float nextTick;

        void OnEnable() => nextTick = 0f;

        void OnDisable() => Release();

        void Update()
        {
            if (Time.time < nextTick) return;
            nextTick = Time.time + tickInterval;

            var zombies = ZombieAI.ActiveZombies;
            if (zombies == null) return;

            float sqrRadius = radius * radius;

            for (int i = 0; i < zombies.Count; i++)
            {
                var zombie = zombies[i];
                if (zombie == null || zombie == GetComponent<ZombieAI>()) continue;
                if ((zombie.transform.position - transform.position).sqrMagnitude > sqrRadius) continue;

                zombie.SpeedMultiplier = speedMultiplier;
            }
        }

        /// <summary>Hands the horde its own speed back the moment the screamer goes down.</summary>
        void Release()
        {
            var zombies = ZombieAI.ActiveZombies;
            if (zombies == null) return;

            float sqrRadius = radius * radius;

            for (int i = 0; i < zombies.Count; i++)
            {
                var zombie = zombies[i];
                if (zombie == null) continue;
                if ((zombie.transform.position - transform.position).sqrMagnitude > sqrRadius) continue;

                zombie.SpeedMultiplier = 1f;
            }
        }
    }
}

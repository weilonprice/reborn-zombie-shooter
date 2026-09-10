using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// Refuses one killing blow a wave, while a weapon granting it is carried.
    /// <para>
    /// Implemented as a death interceptor rather than a revive, so no death ever happens -
    /// the run-over screen, the score tally and the wave loop never see it. Reviving after
    /// the fact would mean undoing all three.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class LastStand : MonoBehaviour, IDeathInterceptor
    {
        [Tooltip("Health left after refusing the blow. One is the readable number.")]
        [SerializeField] float survivingHealth = 1f;
        [SerializeField] float slowMotionSeconds = 0.9f;
        [SerializeField, Range(0.05f, 1f)] float slowMotionScale = 0.3f;

        Health health;
        WeaponLoadout loadout;
        int usedThisWave;
        int lastWave = -1;

        void Awake()
        {
            health = GetComponent<Health>();
            loadout = GetComponent<WeaponLoadout>();
        }

        public bool TryPreventDeath()
        {
            int allowance = Allowance();
            if (allowance <= 0) return false;

            // Charges reset per wave rather than per run, and the wave number is read here
            // rather than tracked, so a wave lost to a restart cannot leave a stale count.
            int wave = WaveManager.Instance != null ? WaveManager.Instance.WaveNumber : 0;
            if (wave != lastWave)
            {
                lastWave = wave;
                usedThisWave = 0;
            }

            if (usedThisWave >= allowance) return false;
            usedThisWave++;

            health.Heal(survivingHealth);

            HitStop.Instance?.SlowMotion(slowMotionSeconds, slowMotionScale);
            CameraShake.Instance?.AddTrauma(0.8f);
            return true;
        }

        /// <summary>Highest allowance among carried weapons, resolved live.</summary>
        int Allowance()
        {
            if (loadout == null) return 0;

            int best = 0;
            for (int i = 0; i < loadout.CarryCapacity; i++)
            {
                var definition = loadout.CarriedAt(i);
                if (definition == null) continue;

                best = Mathf.Max(best, UpgradeManager.Resolve(definition).RevivesPerWave);
            }

            return best;
        }
    }
}

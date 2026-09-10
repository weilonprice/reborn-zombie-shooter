using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// Gets back up once.
    /// <para>
    /// Punishes the habit every other archetype teaches: drop a body and immediately look
    /// somewhere else. It refuses the killing blow rather than reviving after it, so no score
    /// or gold is paid twice and there is no corpse to un-despawn.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class Revenant : MonoBehaviour, IDeathInterceptor
    {
        [Tooltip("Fraction of maximum health it stands back up with.")]
        [SerializeField, Range(0.05f, 1f)] float reviveFraction = 0.45f;
        [Tooltip("How many times per spawn. One is the readable number.")]
        [SerializeField] int revivesPerLife = 1;
        [SerializeField] Color revivedTint = new(0.85f, 0.25f, 0.75f);

        Health health;
        HitFlash flash;
        int used;

        void Awake()
        {
            health = GetComponent<Health>();
            flash = GetComponent<HitFlash>();
        }

        // Pooled: it has to be able to come back again next time it spawns.
        void OnEnable() => used = 0;

        public bool TryPreventDeath()
        {
            if (used >= revivesPerLife) return false;
            used++;

            health.Heal(health.Max * reviveFraction);

            // Marked for the rest of its life, so the player can tell which bodies in a pack
            // are still owed a second kill.
            if (flash != null) flash.SetRestTint(revivedTint, 0.55f);

            CameraShake.Instance?.AddTrauma(0.18f);
            ImpactEffects.Instance?.PlayImpact(transform.position, Vector3.up);
            return true;
        }
    }
}

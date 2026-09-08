using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// One world-space particle system reused for every bullet impact: move it to the hit
    /// point and emit.
    /// <para>
    /// Because it simulates in world space, sparks already in flight stay where they were
    /// when the system is repositioned - so a single system covers an entire firefight
    /// with no pooling and no per-shot allocation.
    /// </para>
    /// </summary>
    public class ImpactEffects : MonoBehaviour
    {
        public static ImpactEffects Instance { get; private set; }

        [SerializeField] ParticleSystem sparks;
        [SerializeField] int particlesPerHit = 7;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            if (sparks == null) sparks = GetComponentInChildren<ParticleSystem>();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void PlayImpact(Vector3 point, Vector3 normal)
        {
            if (sparks == null) return;

            // The normal points back toward the shooter, so aiming the cone along it
            // sprays sparks back out of the surface.
            var facing = normal.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(normal)
                : Quaternion.identity;

            sparks.transform.SetPositionAndRotation(point, facing);
            sparks.Emit(particlesPerHit);
        }
    }
}

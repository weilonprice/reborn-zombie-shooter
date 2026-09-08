using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// Shakes the camera when its owner takes damage, scaled by how big a bite the hit took
    /// out of the health bar rather than by a flat amount. A chip of damage nudges; something
    /// that takes a third of your health hits hard.
    /// <para>
    /// Trauma accumulates, so being swarmed by several zombies landing blows together shakes
    /// far harder than any single hit - which is exactly the read you want when surrounded.
    /// </para>
    /// </summary>
    public class DamageShake : MonoBehaviour
    {
        [SerializeField] Health health;
        [Tooltip("Trauma a single hit would add if it removed the entire health bar. " +
                 "Actual trauma scales from this by the fraction of max health lost.")]
        [SerializeField] float traumaAtFullHealthLoss = 4.6f;
        [Tooltip("Floor, so even a scratch is felt.")]
        [SerializeField] float minTrauma = 0.45f;
        [Tooltip("Ceiling, so one huge hit cannot max out the shake on its own.")]
        [SerializeField] float maxTrauma = 0.85f;

        void Awake()
        {
            if (health == null) health = GetComponentInParent<Health>();
        }

        void OnEnable()
        {
            if (health != null) health.Damaged += OnDamaged;
        }

        void OnDisable()
        {
            if (health != null) health.Damaged -= OnDamaged;
        }

        void OnDamaged(float amount, Vector3 hitPoint, Vector3 hitNormal)
        {
            if (CameraShake.Instance == null) return;

            float fraction = health != null && health.Max > 0f ? amount / health.Max : 0f;
            float trauma = Mathf.Clamp(fraction * traumaAtFullHealthLoss, minTrauma, maxTrauma);

            CameraShake.Instance.AddTrauma(trauma);
        }
    }
}

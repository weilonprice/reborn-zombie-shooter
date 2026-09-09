using System.Collections;
using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// A barrel that detonates when destroyed, damaging everything in a radius.
    /// <para>
    /// Chain reactions are the point: barrels caught in a blast detonate a frame later rather
    /// than immediately, which both prevents unbounded recursion within one frame and reads
    /// far better - the player sees the chain travel instead of the whole line vanishing at
    /// once.
    /// </para>
    /// </summary>
    public class ExplosiveBarrel : MonoBehaviour, IDamageable
    {
        [Header("Durability")]
        [SerializeField] float maxHealth = 30f;

        [Header("Blast")]
        [SerializeField] float blastRadius = 6f;
        [SerializeField] float centreDamage = 220f;
        [Tooltip("Damage at the very edge of the blast, as a fraction of centre damage.")]
        [SerializeField, Range(0f, 1f)] float edgeDamageFraction = 0.25f;
        [SerializeField] float knockbackMultiplier = 4f;
        [Tooltip("Barrels hurt the player too. This is what makes placing one a decision " +
                 "rather than free damage.")]
        [SerializeField] bool damagesPlayer = true;
        [Tooltip("Whether the blast also destroys your own barricades.")]
        [SerializeField] bool damagesStructures;
        [SerializeField] float chainDelay = 0.08f;

        [Header("Feedback")]
        [SerializeField] GameObject visualRoot;
        [SerializeField] ParticleSystem blastParticles;
        [SerializeField] AudioClip explodeClip;
        [SerializeField, Range(0f, 1f)] float explodeVolume = 0.85f;
        [SerializeField] float blastTrauma = 0.7f;
        [SerializeField] float despawnDelay = 1.4f;

        static readonly Collider[] BlastHits = new Collider[64];

        float currentHealth;
        bool detonated;

        public bool IsAlive => currentHealth > 0f && !detonated;

        void Awake() => currentHealth = maxHealth;

        void OnEnable()
        {
            currentHealth = maxHealth;
            detonated = false;
            if (visualRoot != null) visualRoot.SetActive(true);
        }

        public void TakeDamage(in DamageInfo info)
        {
            if (!IsAlive) return;

            currentHealth -= info.Amount;
            if (currentHealth <= 0f) Detonate();
        }

        /// <summary>Used by a neighbouring blast, so a chain does not need to route damage.</summary>
        public void DetonateAfter(float delay)
        {
            if (detonated) return;
            StartCoroutine(DetonateDelayed(delay));
        }

        IEnumerator DetonateDelayed(float delay)
        {
            yield return new WaitForSeconds(delay);
            Detonate();
        }

        void Detonate()
        {
            if (detonated) return;
            detonated = true;
            currentHealth = 0f;

            int count = Physics.OverlapSphereNonAlloc(transform.position, blastRadius, BlastHits,
                                                      ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var hit = BlastHits[i];
                if (hit == null) continue;

                // Neighbours chain rather than taking damage, so a barrel with more health
                // than the blast deals still goes up with the rest of the line.
                var neighbour = hit.GetComponentInParent<ExplosiveBarrel>();
                if (neighbour != null)
                {
                    if (neighbour != this) neighbour.DetonateAfter(chainDelay);
                    continue;
                }

                if (!damagesStructures && hit.GetComponentInParent<Barricade>() != null) continue;
                if (!damagesPlayer && hit.CompareTag("Player")) continue;

                var target = hit.GetComponentInParent<IDamageable>();
                if (target == null || !target.IsAlive) continue;

                var toTarget = hit.transform.position - transform.position;
                float distance = toTarget.magnitude;
                float t = blastRadius <= 0f ? 0f : Mathf.Clamp01(distance / blastRadius);

                // Linear falloff to a floor, so the edge of a blast still means something.
                float damage = centreDamage * Mathf.Lerp(1f, edgeDamageFraction, t);
                var normal = distance > 0.001f ? -toTarget / distance : Vector3.up;

                target.TakeDamage(new DamageInfo(damage, hit.transform.position, normal,
                                                 knockbackMultiplier, gameObject));
            }

            if (blastParticles != null) blastParticles.Play();
            SfxPlayer.Instance?.PlayAt(explodeClip, transform.position, explodeVolume);
            CameraShake.Instance?.AddTrauma(blastTrauma);

            if (visualRoot != null) visualRoot.SetActive(false);
            Destroy(gameObject, despawnDelay);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.5f, 0.1f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, blastRadius);
        }
    }
}

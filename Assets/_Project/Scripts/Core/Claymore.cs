using System.Collections;
using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// A directional proximity mine. Arms shortly after placement, then fires a cone of
    /// damage the first time something living crosses in front of it, and is spent.
    /// <para>
    /// Directional rather than radial on purpose: a claymore is a commitment to a lane, which
    /// makes it a fortification decision rather than a grenade you leave on the floor. It
    /// pairs with barricades - walls choose where the horde walks, claymores punish the lane
    /// the walls chose.
    /// </para>
    /// </summary>
    public class Claymore : MonoBehaviour
    {
        [Header("Trigger")]
        [Tooltip("Seconds before it can fire. Stops one going off in the player's face as " +
                 "they place it while being chased.")]
        [SerializeField] float armTime = 0.7f;
        [SerializeField] float triggerRadius = 4.5f;
        [Tooltip("Half-angle of the kill cone, in degrees, measured from the facing.")]
        [SerializeField, Range(5f, 180f)] float coneHalfAngle = 55f;

        [Header("Damage")]
        [SerializeField] float damage = 160f;
        [SerializeField] float blastRange = 9f;
        [SerializeField] float knockbackMultiplier = 3f;
        [Tooltip("Claymores are directional and player-placed, so unlike barrels they do not " +
                 "hurt the person who set them.")]
        [SerializeField] bool damagesPlayer;

        [Header("Feedback")]
        [SerializeField] GameObject visualRoot;
        [SerializeField] Renderer armedIndicator;
        [SerializeField] Color disarmedColor = new(0.35f, 0.35f, 0.38f);
        [SerializeField] Color armedColor = new(0.95f, 0.25f, 0.20f);
        [SerializeField] ParticleSystem blastParticles;
        [SerializeField] AudioClip detonateClip;
        [SerializeField, Range(0f, 1f)] float detonateVolume = 0.8f;
        [SerializeField] float blastTrauma = 0.45f;
        [SerializeField] float despawnDelay = 1.2f;

        static readonly Collider[] Hits = new Collider[64];
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        bool armed;
        bool spent;
        Material indicatorMaterial;

        void Awake()
        {
            if (armedIndicator != null) indicatorMaterial = armedIndicator.material;
        }

        void OnEnable()
        {
            armed = false;
            spent = false;
            SetIndicator(disarmedColor);
            StartCoroutine(ArmAfterDelay());
        }

        void OnDestroy()
        {
            if (indicatorMaterial != null) Destroy(indicatorMaterial);
        }

        IEnumerator ArmAfterDelay()
        {
            yield return new WaitForSeconds(armTime);

            armed = true;
            SetIndicator(armedColor);
        }

        void Update()
        {
            if (!armed || spent) return;
            if (GameManager.Instance != null && GameManager.Instance.State != GameState.Playing) return;

            int count = Physics.OverlapSphereNonAlloc(transform.position, triggerRadius, Hits,
                                                      ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var hit = Hits[i];
                if (hit == null) continue;

                var zombie = hit.GetComponentInParent<ZombieAI>();
                if (zombie == null) continue;

                var toTarget = hit.transform.position - transform.position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude < 0.0001f) continue;

                if (Vector3.Angle(transform.forward, toTarget) > coneHalfAngle) continue;

                Detonate();
                return;
            }
        }

        void Detonate()
        {
            if (spent) return;
            spent = true;

            int count = Physics.OverlapSphereNonAlloc(transform.position, blastRange, Hits,
                                                      ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var hit = Hits[i];
                if (hit == null) continue;
                if (!damagesPlayer && hit.CompareTag("Player")) continue;
                if (hit.GetComponentInParent<Barricade>() != null) continue;

                var toTarget = hit.transform.position - transform.position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.0001f &&
                    Vector3.Angle(transform.forward, toTarget) > coneHalfAngle) continue;

                var target = hit.GetComponentInParent<IDamageable>();
                if (target == null || !target.IsAlive) continue;

                var normal = toTarget.sqrMagnitude > 0.0001f ? -toTarget.normalized : Vector3.up;
                target.TakeDamage(new DamageInfo(damage, hit.transform.position, normal,
                                                 knockbackMultiplier, gameObject));
            }

            if (blastParticles != null) blastParticles.Play();
            SfxPlayer.Instance?.PlayAt(detonateClip, transform.position, detonateVolume);
            CameraShake.Instance?.AddTrauma(blastTrauma);

            if (visualRoot != null) visualRoot.SetActive(false);
            Destroy(gameObject, despawnDelay);
        }

        void SetIndicator(Color c)
        {
            if (indicatorMaterial == null) return;

            if (indicatorMaterial.HasProperty(BaseColorId)) indicatorMaterial.SetColor(BaseColorId, c);
            else indicatorMaterial.color = c;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.25f, 0.2f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, blastRange);

            var left = Quaternion.Euler(0f, -coneHalfAngle, 0f) * transform.forward;
            var right = Quaternion.Euler(0f, coneHalfAngle, 0f) * transform.forward;
            Gizmos.DrawRay(transform.position, left * blastRange);
            Gizmos.DrawRay(transform.position, right * blastRange);
        }
    }
}

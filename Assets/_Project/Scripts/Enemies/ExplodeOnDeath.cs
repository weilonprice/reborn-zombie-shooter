using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// Bursts when killed, hurting the player and their fortifications.
    /// <para>
    /// Deliberately does NOT damage other zombies. A bloater that cleared the pack around it
    /// would reward killing one in a crowd, which is the opposite of the pressure it exists
    /// to apply: it should make close-range weapons dangerous to use on it, not better.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class ExplodeOnDeath : MonoBehaviour
    {
        [SerializeField] float radius = 4.5f;
        [SerializeField] float damage = 34f;
        [Tooltip("Damage at the very edge of the blast, as a fraction of the centre.")]
        [SerializeField, Range(0f, 1f)] float edgeFalloff = 0.35f;
        [SerializeField] float trauma = 0.5f;
        [SerializeField] AudioClip blastClip;
        [SerializeField, Range(0f, 1f)] float blastVolume = 0.7f;

        static readonly Collider[] Buffer = new Collider[24];

        Health health;
        bool exploded;

        void Awake() => health = GetComponent<Health>();

        void OnEnable()
        {
            // Pooled: an enemy that already burst has to be able to do it again next wave.
            exploded = false;
            health.Died += OnDied;
        }

        void OnDisable() => health.Died -= OnDied;

        void OnDied(Health _)
        {
            if (exploded) return;
            exploded = true;

            var centre = transform.position;

            int count = Physics.OverlapSphereNonAlloc(centre, radius, Buffer, ~0,
                                                      QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                if (Buffer[i] == null) continue;

                var target = Buffer[i].GetComponentInParent<IDamageable>();
                if (target == null || !target.IsAlive) continue;

                // Everything hostile is skipped, including itself - this is aimed at the
                // player and at whatever they have built.
                if (Buffer[i].GetComponentInParent<ZombieAI>() != null) continue;

                var offset = Buffer[i].transform.position - centre;
                float t = Mathf.Clamp01(offset.magnitude / radius);
                float scaled = damage * Mathf.Lerp(1f, edgeFalloff, t);

                var normal = offset.sqrMagnitude < 0.0001f ? Vector3.up : offset.normalized;
                target.TakeDamage(new DamageInfo(scaled, centre, normal, 1.5f, gameObject));
            }

            ImpactEffects.Instance?.PlayImpact(centre, Vector3.up);
            CameraShake.Instance?.AddTrauma(trauma);
            SfxPlayer.Instance?.PlayAt(blastClip, centre, blastVolume);
        }
    }
}

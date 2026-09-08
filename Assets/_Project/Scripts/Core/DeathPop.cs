using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// Collapse-on-death: the body flattens, spreads and spins out of existence over a
    /// couple of frames, so a kill reads as an event rather than a disappearance.
    /// <para>
    /// Chosen over a dissolve (needs a custom shader) or gibs (needs spawned debris and a
    /// pool) because it costs no assets whatsoever - it is pure transform animation.
    /// </para>
    /// </summary>
    public class DeathPop : MonoBehaviour
    {
        [SerializeField] Health health;
        [Tooltip("What to animate. Defaults to this object's own transform.")]
        [SerializeField] Transform body;
        [Tooltip("Seconds from full size to gone. Keep at or below ZombieAI.deathLinger, " +
                 "or the body is despawned before the collapse finishes.")]
        [SerializeField] float duration = 0.2f;
        [Tooltip("How far the body spreads sideways as it flattens.")]
        [SerializeField] float squash = 1.5f;
        [SerializeField] float spinDegrees = 110f;

        Vector3 restScale;
        Quaternion restRotation;
        float elapsed = -1f;   // negative means idle

        void Awake()
        {
            if (health == null) health = GetComponentInParent<Health>();
            if (body == null) body = transform;

            restScale = body.localScale;
            restRotation = body.localRotation;
        }

        void OnEnable()
        {
            if (health != null) health.Died += OnDied;
            Reset();
        }

        void OnDisable()
        {
            if (health != null) health.Died -= OnDied;
            Reset();
        }

        void OnDied(Health _) => elapsed = 0f;

        void Reset()
        {
            elapsed = -1f;
            if (body == null) return;

            body.localScale = restScale;
            body.localRotation = restRotation;
        }

        void Update()
        {
            if (elapsed < 0f || body == null) return;

            // Scaled time, so the collapse pauses with everything else during a hit-stop
            // freeze and stays in step with ZombieAI's despawn timer.
            elapsed += Time.deltaTime;
            float t = duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);

            float shrink = 1f - t;
            float spread = Mathf.Lerp(1f, squash, t);

            var s = restScale;
            s.x *= spread * shrink;
            s.z *= spread * shrink;
            s.y *= Mathf.Lerp(1f, 1f / squash, t) * shrink;
            body.localScale = s;

            body.localRotation = restRotation * Quaternion.Euler(0f, spinDegrees * t, 0f);
        }
    }
}

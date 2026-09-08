using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// Trauma-based screen shake.
    /// <para>
    /// Callers add <em>trauma</em> (0-1) rather than requesting a shake directly; the actual
    /// displacement is trauma raised to a power, which is the intensity curve. Squaring means
    /// a stream of small events barely registers while a big one still lands hard - exactly
    /// what a weapon firing eight times a second needs.
    /// </para>
    /// <para>
    /// Displacement is sampled from Perlin noise rather than random values, so the camera
    /// drifts smoothly instead of vibrating between unrelated positions frame to frame.
    /// </para>
    /// <para>
    /// Lives on the camera itself, which hangs off a follow rig, so the shake is a purely
    /// local offset. That keeps it independent of LateUpdate ordering against the rig.
    /// </para>
    /// </summary>
    public class CameraShake : MonoBehaviour
    {
        public static CameraShake Instance { get; private set; }

        [Tooltip("Maximum positional offset in world units, at full trauma.")]
        [SerializeField] float maxOffset = 0.4f;
        [Tooltip("Maximum roll in degrees, at full trauma.")]
        [SerializeField] float maxRoll = 1.84f;
        [Tooltip("Trauma bled off per second. Higher settles the camera faster.")]
        [SerializeField] float decay = 1.6f;
        [Tooltip("How fast the noise is sampled. Higher is buzzier, lower is a wobble.")]
        [SerializeField] float frequency = 22f;
        [Tooltip("Trauma is raised to this power. 2 keeps sustained fire subtle while " +
                 "leaving room for kills to read; 1 would make every shot equally loud.")]
        [SerializeField, Range(1f, 3f)] float responseCurve = 2f;

        [Header("Recoil")]
        [Tooltip("How fast the camera springs back to centre after a kick, per second.")]
        [SerializeField] float recoilDecay = 9f;
        [Tooltip("Ceiling on accumulated kick, so sustained fire cannot walk the camera away.")]
        [SerializeField] float maxRecoil = 0.25f;

        float trauma;
        float seed;
        Vector3 recoil;   // rig-local, screen plane

        Vector3 restPosition;
        Quaternion restRotation;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            // Different every run, so repeated playthroughs don't shake identically.
            seed = Random.value * 100f;
            restPosition = transform.localPosition;
            restRotation = transform.localRotation;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Adds trauma, clamped to 1. Small values for frequent events.</summary>
        public void AddTrauma(float amount)
        {
            if (amount <= 0f) return;
            trauma = Mathf.Clamp01(trauma + amount);
        }

        /// <summary>
        /// A directional shove, unlike the noise of <see cref="AddTrauma"/>. Pass the world
        /// direction the camera should be pushed - for a weapon, the opposite of the barrel,
        /// so the view is knocked back the way the gun is.
        /// </summary>
        public void AddRecoil(Vector3 worldDirection, float amount)
        {
            if (amount <= 0f || worldDirection.sqrMagnitude < 0.0001f) return;

            // Into the rig's frame, where x is screen right and y is screen up, then
            // flattened so the kick never pushes the camera along its own view axis.
            var local = transform.parent != null
                ? transform.parent.InverseTransformDirection(worldDirection)
                : worldDirection;
            local.z = 0f;
            if (local.sqrMagnitude < 0.0001f) return;

            recoil = Vector3.ClampMagnitude(recoil + local.normalized * amount, maxRecoil);
        }

        void LateUpdate()
        {
            // Runs unconditionally: recoil has to keep settling even with no trauma left.
            var offset = Vector3.zero;
            var rotation = restRotation;

            if (trauma > 0f)
            {
                float shake = Mathf.Pow(trauma, responseCurve);

                // Scaled time on purpose: during a hit-stop freeze the camera holds still,
                // which reads as a crisp frozen frame rather than a wobble in stopped time.
                float t = Time.time * frequency;

                float x = Mathf.PerlinNoise(seed, t) * 2f - 1f;
                float y = Mathf.PerlinNoise(seed + 11.3f, t) * 2f - 1f;
                float roll = Mathf.PerlinNoise(seed + 27.7f, t) * 2f - 1f;

                // Local space, so x and y are screen right and screen up regardless of the
                // rig's top-down pitch.
                offset += new Vector3(x, y, 0f) * (maxOffset * shake);
                rotation = restRotation * Quaternion.Euler(0f, 0f, roll * maxRoll * shake);

                trauma = Mathf.Max(0f, trauma - decay * Time.deltaTime);
            }

            offset += recoil;

            // Exponential settle, written frame-rate independently so the spring back feels
            // identical at 60 and 144 fps.
            recoil = Vector3.Lerp(recoil, Vector3.zero, 1f - Mathf.Exp(-recoilDecay * Time.deltaTime));

            transform.localPosition = restPosition + offset;
            transform.localRotation = rotation;
        }
    }
}

using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// Looping low drone under everything else. The three event sounds otherwise sit on
    /// silence, which is what makes an arena feel like a test scene rather than a place.
    /// <para>
    /// Non-positional and quiet by design: it should be noticed only when it stops.
    /// </para>
    /// </summary>
    public class AmbientBed : MonoBehaviour
    {
        [SerializeField] AudioClip clip;
        [SerializeField, Range(0f, 1f)] float volume = 0.35f;
        [Tooltip("Seconds to reach full volume. Starting at full is jarring on scene load.")]
        [SerializeField] float fadeInSeconds = 2.5f;

        AudioSource source;
        float elapsed;

        void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;   // 2D: the bed is everywhere, not somewhere
            source.volume = 0f;
            source.priority = 0;        // never voice-stolen by gunfire
        }

        void Start()
        {
            if (clip == null)
            {
                Debug.LogWarning($"{nameof(AmbientBed)}: no clip assigned; the arena will be silent.", this);
                enabled = false;
                return;
            }
            source.Play();
        }

        void Update()
        {
            if (elapsed >= fadeInSeconds) return;

            // Unscaled, so a hit-stop freeze in the first seconds doesn't stall the fade.
            elapsed += Time.unscaledDeltaTime;
            float t = fadeInSeconds <= 0f ? 1f : Mathf.Clamp01(elapsed / fadeInSeconds);
            source.volume = volume * t;
        }
    }
}

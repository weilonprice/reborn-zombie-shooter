using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// Round-robin pool of AudioSources for one-shot effects.
    /// <para>
    /// Avoids AudioSource.PlayClipAtPoint, which allocates and destroys a GameObject per
    /// call - at eight rounds a second plus impacts that is a lot of garbage. Every shot
    /// gets a small random pitch offset, without which repeated fire turns into an
    /// obviously looping buzz.
    /// </para>
    /// </summary>
    public class SfxPlayer : MonoBehaviour
    {
        public static SfxPlayer Instance { get; private set; }

        [Tooltip("Simultaneous sounds. Beyond this the oldest voice is stolen.")]
        [SerializeField] int voices = 14;
        [Tooltip("Full volume within this radius of the listener.")]
        [SerializeField] float minDistance = 6f;
        [SerializeField] float maxDistance = 45f;

        AudioSource[] pool;
        int next;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            pool = new AudioSource[Mathf.Max(1, voices)];
            for (int i = 0; i < pool.Length; i++)
            {
                var go = new GameObject($"Voice_{i}");
                go.transform.SetParent(transform, false);

                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.rolloffMode = AudioRolloffMode.Linear;
                src.minDistance = minDistance;
                src.maxDistance = maxDistance;
                pool[i] = src;
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Positional one-shot, attenuated by distance from the listener.</summary>
        public void PlayAt(AudioClip clip, Vector3 position, float volume = 1f, float pitchJitter = 0.08f)
            => Play(clip, position, 1f, volume, pitchJitter);

        /// <summary>Non-positional one-shot, for sounds the player makes themselves.</summary>
        public void PlayFlat(AudioClip clip, float volume = 1f, float pitchJitter = 0.08f)
            => Play(clip, Vector3.zero, 0f, volume, pitchJitter);

        void Play(AudioClip clip, Vector3 position, float spatialBlend, float volume, float pitchJitter)
        {
            if (clip == null || pool == null) return;

            var src = pool[next];
            next = (next + 1) % pool.Length;

            src.transform.position = position;
            src.clip = clip;
            src.spatialBlend = spatialBlend;
            src.volume = volume;
            src.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            src.Play();
        }
    }
}

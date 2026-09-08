using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// Round-robin pool of LineRenderers for bullet tracers.
    /// <para>
    /// A single shared LineRenderer can only draw one line, so a shotgun firing eight pellets
    /// overwrote the same one eight times and showed a single tracer - the spread was there
    /// mechanically but completely invisible. One renderer per pellet is what makes the fan
    /// readable.
    /// </para>
    /// </summary>
    public class TracerPool : MonoBehaviour
    {
        public static TracerPool Instance { get; private set; }

        [Tooltip("Concurrent tracers. Needs to comfortably exceed the highest pellet count.")]
        [SerializeField] int lines = 16;
        [SerializeField] Material material;

        LineRenderer[] pool;
        float[] hideAt;
        int next;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            pool = new LineRenderer[Mathf.Max(1, lines)];
            hideAt = new float[pool.Length];

            for (int i = 0; i < pool.Length; i++)
            {
                var go = new GameObject($"Tracer_{i}");
                go.transform.SetParent(transform, false);

                var line = go.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.positionCount = 2;
                line.numCapVertices = 2;
                line.sharedMaterial = material;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.enabled = false;

                pool[i] = line;
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Draw(Vector3 from, Vector3 to, float width, float duration)
        {
            if (pool == null || pool.Length == 0) return;

            int index = next;
            next = (next + 1) % pool.Length;

            var line = pool[index];
            line.widthMultiplier = width;
            line.SetPosition(0, from);
            line.SetPosition(1, to);
            line.enabled = true;

            // Scaled time, so a tracer caught by a hit-stop freeze stays on screen for the
            // frozen frame instead of expiring inside it.
            hideAt[index] = Time.time + duration;
        }

        void Update()
        {
            for (int i = 0; i < pool.Length; i++)
            {
                if (pool[i].enabled && Time.time >= hideAt[i])
                    pool[i].enabled = false;
            }
        }
    }
}

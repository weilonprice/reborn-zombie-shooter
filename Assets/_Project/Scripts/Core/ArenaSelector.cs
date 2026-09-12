using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// Picks which arena a run is fought in and switches the others off.
    /// <para>
    /// Every layout is built into the scene and toggled, because ArenaBuilder generates the
    /// scene once and cannot know which arena a future run wants. Inactive cover is a handful
    /// of static primitives and costs nothing.
    /// </para>
    /// <para>
    /// Chooses in Awake, deliberately. FlowField bakes its static blocking in Start, and Unity
    /// guarantees every Awake runs before any Start - so the field always bakes against the
    /// arena that is actually standing. Choosing in Start would be a race with no error.
    /// </para>
    /// </summary>
    public class ArenaSelector : MonoBehaviour
    {
        public static ArenaSelector Instance { get; private set; }

        [Tooltip("One root per layout. Exactly one is left active for the run.")]
        [SerializeField] GameObject[] layouts;
        [Tooltip("Forces a layout by index instead of choosing at random. -1 picks randomly.")]
        [SerializeField] int forcedIndex = -1;

        /// <summary>Name of the arena this run is being fought in.</summary>
        public string CurrentName { get; private set; } = "Arena";

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            if (layouts == null || layouts.Length == 0) return;

            int chosen = forcedIndex >= 0 && forcedIndex < layouts.Length
                ? forcedIndex
                : Random.Range(0, layouts.Length);

            for (int i = 0; i < layouts.Length; i++)
            {
                if (layouts[i] == null) continue;
                layouts[i].SetActive(i == chosen);
            }

            if (layouts[chosen] != null) CurrentName = layouts[chosen].name;
            Debug.Log($"<b>Zombie Shooter</b>: arena for this run is {CurrentName}.");
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}

using System.Collections;
using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// Tints every renderer for a few frames when the owner takes damage.
    /// <para>
    /// Uses per-instance materials rather than a MaterialPropertyBlock: the URP asset runs
    /// the GPU Resident Drawer (instanced drawing), which does not honour property blocks.
    /// The cost is losing batching for these renderers, which is irrelevant at horde sizes
    /// in the dozens and far better than a flash that silently never renders.
    /// </para>
    /// </summary>
    public class HitFlash : MonoBehaviour
    {
        [SerializeField] Health health;
        [SerializeField] Color flashColor = Color.white;
        [Tooltip("Seconds the tint is held. Past ~0.1 it stops reading as an impact.")]
        [SerializeField] float duration = 0.06f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        Material[] materials;
        Color[] restColors;
        Coroutine routine;

        void Awake()
        {
            if (health == null) health = GetComponentInParent<Health>();

            var renderers = GetComponentsInChildren<Renderer>(true);
            materials = new Material[renderers.Length];
            restColors = new Color[renderers.Length];

            for (int i = 0; i < renderers.Length; i++)
            {
                // .material (not .sharedMaterial) hands back a copy owned by this object.
                materials[i] = renderers[i].material;
                restColors[i] = ReadColor(materials[i]);
            }
        }

        void OnEnable()
        {
            if (health != null) health.Damaged += OnDamaged;
            Restore();
        }

        void OnDisable()
        {
            if (health != null) health.Damaged -= OnDamaged;

            if (routine != null)
            {
                StopCoroutine(routine);
                routine = null;
            }
            Restore();
        }

        void OnDestroy()
        {
            if (materials == null) return;

            // These instances belong to us; nothing else will clean them up.
            foreach (var m in materials)
                if (m != null) Destroy(m);
        }

        void OnDamaged(float amount, Vector3 hitPoint, Vector3 hitNormal)
        {
            if (!isActiveAndEnabled) return;

            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(FlashRoutine());
        }

        IEnumerator FlashRoutine()
        {
            Apply(flashColor);

            // Realtime, so a kill freeze doesn't stretch the flash to its full duration.
            yield return new WaitForSecondsRealtime(duration);

            Restore();
            routine = null;
        }

        void Apply(Color c)
        {
            if (materials == null) return;

            foreach (var m in materials)
                if (m != null) WriteColor(m, c);
        }

        void Restore()
        {
            if (materials == null) return;

            for (int i = 0; i < materials.Length; i++)
                if (materials[i] != null) WriteColor(materials[i], restColors[i]);
        }

        static Color ReadColor(Material m) =>
            m.HasProperty(BaseColorId) ? m.GetColor(BaseColorId) : m.color;

        static void WriteColor(Material m, Color c)
        {
            if (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, c);
            else m.color = c;
        }
    }
}

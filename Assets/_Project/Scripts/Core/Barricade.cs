using System;
using System.Collections;
using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// Destructible physical barrier (Boxhead DNA).
    /// Blocks melee horde advancement, player movement, and absorbs incoming enemy projectiles.
    /// Filters out friendly fire from player bullets while letting piercing weapons punch through.
    /// </summary>
    public class Barricade : MonoBehaviour, IDamageable
    {
        [Header("Health & Defense")]
        [SerializeField] float maxHealth = 150f;
        [SerializeField] Collider obstacleCollider;

        [Header("Visuals & Feedback")]
        [SerializeField] GameObject visualRoot;
        [SerializeField] ParticleSystem splinterParticles;
        [SerializeField] Transform healthBarRoot;
        [SerializeField] Transform healthBarFill;
        [SerializeField] Color flashColor = new Color(1f, 0.9f, 0.8f, 1f);
        [SerializeField] float flashDuration = 0.06f;

        [Header("Audio")]
        [SerializeField] AudioClip hitClip;
        [SerializeField] AudioClip breakClip;
        [SerializeField, Range(0f, 1f)] float hitVolume = 0.55f;
        [SerializeField, Range(0f, 1f)] float breakVolume = 0.75f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        float currentHealth;
        Material[] materials;
        Color[] restColors;
        Coroutine flashRoutine;
        bool isDestroyed;
        Camera mainCam;

        public bool IsAlive => currentHealth > 0f && !isDestroyed;
        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;

        public event Action<Barricade> Destroyed;

        void Awake()
        {
            if (obstacleCollider == null) obstacleCollider = GetComponent<Collider>();
            currentHealth = maxHealth;

            // Cache renderers for flash feedback
            var renderers = visualRoot != null ? visualRoot.GetComponentsInChildren<Renderer>(true) : GetComponentsInChildren<Renderer>(true);
            materials = new Material[renderers.Length];
            restColors = new Color[renderers.Length];

            for (int i = 0; i < renderers.Length; i++)
            {
                materials[i] = renderers[i].material;
                restColors[i] = materials[i].HasProperty(BaseColorId) ? materials[i].GetColor(BaseColorId) : materials[i].color;
            }

            if (healthBarRoot != null) healthBarRoot.gameObject.SetActive(false);
            mainCam = Camera.main;
        }

        void LateUpdate()
        {
            // Keep the floating health bar facing the camera
            if (healthBarRoot != null && healthBarRoot.gameObject.activeSelf)
            {
                if (mainCam == null) mainCam = Camera.main;
                if (mainCam != null)
                {
                    healthBarRoot.rotation = mainCam.transform.rotation;
                }
            }
        }

        void OnDestroy()
        {
            if (materials != null)
            {
                foreach (var m in materials)
                    if (m != null) Destroy(m);
            }
        }

        public void TakeDamage(in DamageInfo info)
        {
            if (!IsAlive) return;

            // Friendly fire filter: Player bullets do 0 damage so fortifications aren't accidentally destroyed
            if (info.Source != null && info.Source.CompareTag("Player"))
            {
                if (hitClip != null)
                    SfxPlayer.Instance?.PlayAt(hitClip, info.Point != Vector3.zero ? info.Point : transform.position, hitVolume * 0.5f);
                if (splinterParticles != null)
                    splinterParticles.Emit(2);
                return;
            }

            currentHealth = Mathf.Max(0f, currentHealth - info.Amount);

            // Update floating health bar
            if (healthBarRoot != null)
            {
                healthBarRoot.gameObject.SetActive(true);
                if (healthBarFill != null)
                {
                    float ratio = Mathf.Clamp01(currentHealth / maxHealth);
                    healthBarFill.localScale = new Vector3(ratio, 1f, 1f);
                }
            }

            // Splinters & Audio
            if (splinterParticles != null)
                splinterParticles.Emit(5);

            if (hitClip != null)
                SfxPlayer.Instance?.PlayAt(hitClip, info.Point != Vector3.zero ? info.Point : transform.position, hitVolume);

            // Hit Flash
            if (flashRoutine != null) StopCoroutine(flashRoutine);
            flashRoutine = StartCoroutine(FlashRoutine());

            if (currentHealth <= 0f)
                Die();
        }

        IEnumerator FlashRoutine()
        {
            ApplyColor(flashColor);
            yield return new WaitForSecondsRealtime(flashDuration);
            RestoreColor();
            flashRoutine = null;
        }

        void ApplyColor(Color c)
        {
            if (materials == null) return;
            foreach (var m in materials)
            {
                if (m == null) continue;
                if (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, c);
                else m.color = c;
            }
        }

        void RestoreColor()
        {
            if (materials == null) return;
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] == null) continue;
                if (materials[i].HasProperty(BaseColorId)) materials[i].SetColor(BaseColorId, restColors[i]);
                else materials[i].color = restColors[i];
            }
        }

        void Die()
        {
            if (isDestroyed) return;
            isDestroyed = true;

            // Immediately disable collider so zombies path forward instantly
            if (obstacleCollider != null) obstacleCollider.enabled = false;
            if (healthBarRoot != null) healthBarRoot.gameObject.SetActive(false);

            // Large wood debris splinter burst
            if (splinterParticles != null)
            {
                splinterParticles.transform.SetParent(null, true);
                splinterParticles.Emit(28);
                Destroy(splinterParticles.gameObject, 1.5f);
            }

            if (breakClip != null)
                SfxPlayer.Instance?.PlayAt(breakClip, transform.position, breakVolume);

            CameraShake.Instance?.AddTrauma(0.18f);
            Destroyed?.Invoke(this);

            if (visualRoot != null) visualRoot.SetActive(false);
            Destroy(gameObject, 0.4f);
        }
    }
}

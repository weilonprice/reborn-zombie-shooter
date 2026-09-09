using System;
using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// Generic hit points for players and zombies alike. Raises events rather than
    /// reaching out to UI or spawners directly, so listeners stay decoupled.
    /// </summary>
    public class Health : MonoBehaviour, IDamageable
    {
        [SerializeField] float maxHealth = 100f;

        public float Max => maxHealth;
        public float Current { get; private set; }
        public bool IsAlive => Current > 0f;
        public float Normalized => maxHealth <= 0f ? 0f : Mathf.Clamp01(Current / maxHealth);

        /// <summary>(current, max)</summary>
        public event Action<float, float> Changed;
        /// <summary>Raised once, the frame health first reaches zero.</summary>
        public event Action<Health> Died;
        /// <summary>
        /// Raised only on real damage. Distinct from <see cref="Changed"/>, which also fires
        /// when a pooled object resets to full.
        /// </summary>
        public event Action<DamageInfo> Damaged;

        /// <summary>The most recent damage taken, so a death can be attributed to a weapon.</summary>
        public DamageInfo LastDamage { get; private set; }

        void Awake()
        {
            Current = maxHealth;
        }

        void OnEnable()
        {
            // Pooled objects are re-enabled rather than constructed, so reset here too.
            Current = maxHealth;
            Changed?.Invoke(Current, maxHealth);
        }

        public void TakeDamage(in DamageInfo info)
        {
            if (!IsAlive || info.Amount <= 0f) return;

            LastDamage = info;
            Current = Mathf.Max(0f, Current - info.Amount);
            Damaged?.Invoke(info);
            Changed?.Invoke(Current, maxHealth);

            if (Current <= 0f)
                Died?.Invoke(this);
        }

        public void Heal(float amount)
        {
            if (!IsAlive || amount <= 0f) return;

            Current = Mathf.Min(maxHealth, Current + amount);
            Changed?.Invoke(Current, maxHealth);
        }
    }
}

using System;
using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// Drives the boss fight by switching an ordinary <see cref="ZombieAI"/> between the two
    /// behaviours it already supports, rather than reimplementing movement or combat.
    /// <para>
    /// Phase 1 is artillery: it holds at range and lobs projectiles, forcing the player off
    /// whatever position they fortified. Phase 2 is the reversal - it charges, and smashes
    /// barricades in a single hit, so the walls built over fifteen waves buy seconds rather
    /// than safety. Splitting the two across the phase boundary is what stops the fight
    /// being a larger Brute, which the roster already has.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(Health))]
    [RequireComponent(typeof(ZombieAI))]
    public class BossController : MonoBehaviour
    {
        [Header("Phase 1 - artillery")]
        [SerializeField] float phaseOneMoveSpeed = 1.7f;
        [SerializeField] float phaseOneAttackCooldown = 2.2f;

        [Header("Phase 2 - enraged charger")]
        [Tooltip("Health fraction at which the boss switches. 0.5 is the halfway turn.")]
        [SerializeField, Range(0.05f, 0.95f)] float phaseTwoAt = 0.5f;
        [SerializeField] float phaseTwoMoveSpeed = 5f;
        [SerializeField] float phaseTwoAttackDamage = 35f;
        [SerializeField] float phaseTwoAttackCooldown = 0.9f;
        [Tooltip("Barricade damage multiplier in phase 2. Must clear a barricade's 150 HP " +
                 "in one blow for the phase to read as walls no longer helping.")]
        [SerializeField] float phaseTwoBarricadeMultiplier = 10f;

        [Header("Phase change feedback")]
        [SerializeField] AudioClip phaseChangeClip;
        [SerializeField, Range(0f, 1f)] float phaseChangeVolume = 0.8f;
        [SerializeField] float phaseChangeTrauma = 0.75f;
        [SerializeField] float phaseChangeFreeze = 0.12f;

        /// <summary>The boss currently in the arena, for the HUD to draw a health bar from.</summary>
        public static BossController Active { get; private set; }

        public Health Health { get; private set; }
        public int Phase { get; private set; } = 1;

        public event Action<int> PhaseChanged;

        ZombieAI ai;

        void Awake()
        {
            Health = GetComponent<Health>();
            ai = GetComponent<ZombieAI>();
        }

        void OnEnable()
        {
            Active = this;

            // Pooled objects are re-enabled rather than constructed, so phase 1 is applied
            // explicitly instead of relying on whatever the last fight left behind.
            EnterPhaseOne();

            Health.Died += OnDied;
        }

        void OnDisable()
        {
            Health.Died -= OnDied;
            if (Active == this) Active = null;
        }

        void Update()
        {
            if (Phase != 1 || !Health.IsAlive) return;
            if (Health.Normalized > phaseTwoAt) return;

            EnterPhaseTwo();
        }

        void EnterPhaseOne()
        {
            Phase = 1;

            ai.SetRanged(true);
            ai.SetMoveSpeed(phaseOneMoveSpeed);
            ai.SetAttackCooldown(phaseOneAttackCooldown);
            ai.SetBarricadeDamageMultiplier(1f);

            PhaseChanged?.Invoke(Phase);
        }

        void EnterPhaseTwo()
        {
            Phase = 2;

            ai.SetRanged(false);
            ai.SetMoveSpeed(phaseTwoMoveSpeed);
            ai.SetAttackDamage(phaseTwoAttackDamage);
            ai.SetAttackCooldown(phaseTwoAttackCooldown);
            ai.SetBarricadeDamageMultiplier(phaseTwoBarricadeMultiplier);

            // The turn has to be unmissable - it is the moment the player's fortifications
            // stop being an answer, and they need to feel that before they discover it.
            SfxPlayer.Instance?.PlayAt(phaseChangeClip, transform.position, phaseChangeVolume);
            CameraShake.Instance?.AddTrauma(phaseChangeTrauma);
            HitStop.Instance?.Freeze(phaseChangeFreeze);

            PhaseChanged?.Invoke(Phase);
        }

        void OnDied(Health _)
        {
            // Killing the boss is the win, not clearing the wave it arrived in.
            GameManager.Instance?.Win();
        }
    }
}

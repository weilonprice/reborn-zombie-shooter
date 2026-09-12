using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// Drives the normal zombie's imported animation clips from the existing gameplay events.
    /// Movement remains owned by ZombieAI/CharacterController; this component only selects the
    /// visual state so root motion cannot fight the horde steering.
    /// </summary>
    [RequireComponent(typeof(Health))]
    [RequireComponent(typeof(ZombieAI))]
    public class ZombieAnimator : MonoBehaviour
    {
        [SerializeField] Animator animator;
        [SerializeField] float attackDuration = 1.27f;
        [SerializeField] float getShotDuration = 0.57f;
        [SerializeField] float staggerDuration = 1.5f;
        [SerializeField] float deathPlaybackSpeed = 2.5f;

        [Header("Reactions")]
        [Tooltip("Fraction of MAX health a single hit must deal to stagger rather than flinch. " +
                 "Low values are a trap: at 0.18 a standard zombie staggered on 18 damage, " +
                 "which is nearly every weapon in the game on every shot.")]
        [SerializeField, Range(0.05f, 1f)] float heavyHealthFraction = 0.4f;
        [Tooltip("Knockback that staggers regardless of damage.")]
        [SerializeField] float heavyKnockback = 2.5f;
        [Tooltip("Minimum gap between flinches. Must exceed the flinch clip, or a zombie " +
                 "under automatic fire never shows its walk again.")]
        [SerializeField] float lightReactionInterval = 1.2f;
        [Tooltip("Minimum gap between staggers, so a stagger stays an event rather than a state.")]
        [SerializeField] float heavyReactionInterval = 4f;
        [Tooltip("Stop the zombie for the length of a stagger. Off, it slides toward the " +
                 "player at full speed while playing a stagger, which reads as a bug.")]
        [SerializeField] bool stopDuringStagger = true;

        const string Chase = "Chase";
        const string Attack = "Attack";
        const string GetShot = "GetShot";
        const string Stagger = "Stagger";
        const string Death = "Death";

        HordeAura aura;
        Revenant revenant;
        BarricadeLeaper leaper;
        float abilityUntil;

        Health health;
        ZombieAI ai;
        float returnToChaseAt;
        float nextLightAt;
        float nextHeavyAt;
        bool dead;

        void Awake()
        {
            health = GetComponent<Health>();
            ai = GetComponent<ZombieAI>();
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
            aura = GetComponent<HordeAura>();
            revenant = GetComponent<Revenant>();
            leaper = GetComponent<BarricadeLeaper>();
        }

        void OnEnable()
        {
            dead = false;
            returnToChaseAt = 0f;
            nextLightAt = 0f;
            nextHeavyAt = 0f;
            abilityUntil = 0f;

            if (health != null) health.Damaged += OnDamaged;
            if (health != null) health.Died += OnDied;
            if (ai != null) ai.Attacked += OnAttacked;
            if (aura != null) aura.Screamed += OnScream;
            if (revenant != null) revenant.Reviving += OnGetUp;
            if (leaper != null) leaper.LeapStarted += OnLeap;

            Play(Chase, 0.05f);
        }

        void OnDisable()
        {
            if (health != null) health.Damaged -= OnDamaged;
            if (health != null) health.Died -= OnDied;
            if (ai != null) ai.Attacked -= OnAttacked;
            if (aura != null) aura.Screamed -= OnScream;
            if (revenant != null) revenant.Reviving -= OnGetUp;
            if (leaper != null) leaper.LeapStarted -= OnLeap;

            if (animator != null) animator.speed = 1f;
        }

        void Update()
        {
            if (dead || animator == null) return;
            if (returnToChaseAt <= 0f || Time.time < returnToChaseAt) return;

            returnToChaseAt = 0f;
            Play(Chase, 0.12f);
        }

        void OnAttacked()
        {
            if (dead || Time.time < abilityUntil) return;

            Play(Attack, 0.08f);
            returnToChaseAt = Time.time + attackDuration;
        }

        void OnDamaged(DamageInfo info)
        {
            if (dead || animator == null || Time.time < abilityUntil) return;

            float fraction = health != null && health.Max > 0f
                ? info.Amount / health.Max
                : 0f;

            bool heavy = fraction >= heavyHealthFraction
                      || info.KnockbackMultiplier >= heavyKnockback;

            if (heavy)
            {
                // A stagger outranks anything already playing - being knocked out of a swing
                // is the point - but it cannot retrigger itself into a permanent state.
                if (Time.time < nextHeavyAt) return;

                nextHeavyAt = Time.time + heavyReactionInterval;
                nextLightAt = Time.time + lightReactionInterval;

                Play(Stagger, 0.05f);
                returnToChaseAt = Time.time + staggerDuration;

                // The animation is only half of it. ZombieAI owns movement, so without this
                // the zombie slides toward the player at full speed mid-stagger.
                if (stopDuringStagger) ai?.Slow(0f, staggerDuration);
                return;
            }

            // A flinch never interrupts a reaction that is already playing, and is spaced
            // wider than its own clip so the walk is visible between hits. Restarting a
            // 0.57s clip every twelfth of a second under an SMG was a twitch, not a flinch.
            if (Time.time < nextLightAt || Time.time < returnToChaseAt) return;

            nextLightAt = Time.time + lightReactionInterval;

            Play(GetShot, 0.05f);
            returnToChaseAt = Time.time + getShotDuration;
        }

        void OnDied(Health _)
        {
            if (dead) return;
            dead = true;
            returnToChaseAt = 0f;
            abilityUntil = 0f;

            if (animator != null)
            {
                animator.speed = Mathf.Max(0.1f, deathPlaybackSpeed);
                animator.Play(Death, 0, 0f);
            }
        }

        void OnScream(float seconds) => PlayAbility("Scream", 1.5f, seconds);
        void OnGetUp(float seconds) => PlayAbility("GetUp", 2.2f, seconds);
        void OnLeap(float seconds) => PlayAbility("Leap", 0.65f, seconds);

        void PlayAbility(string state, float clipSeconds, float seconds)
        {
            if (dead || animator == null || !animator.HasState(0, Animator.StringToHash(state))) return;

            seconds = Mathf.Max(.01f, seconds);
            abilityUntil = returnToChaseAt = Time.time + seconds;
            // These signature actions own the whole rig. Death can still interrupt; attack
            // callbacks and the triggering damage reaction cannot erase the defining pose.
            animator.speed = clipSeconds / seconds;
            animator.Play(state, 0, 0f);
        }

        void Play(string state, float fade)
        {
            if (animator == null || dead) return;

            animator.speed = 1f;
            animator.CrossFadeInFixedTime(state, fade, 0, 0f);
        }
    }
}

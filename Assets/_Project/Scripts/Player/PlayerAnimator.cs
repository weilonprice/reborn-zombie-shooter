using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// Drives the authored survivor model from the existing player, health and weapon
    /// systems. Movement remains owned by PlayerController; this component only chooses
    /// visual clips and keeps root motion disabled so the collider never fights the rig.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class PlayerAnimator : MonoBehaviour
    {
        [SerializeField] Animator animator;
        [SerializeField] Health health;
        [SerializeField] Weapon weapon;

        [Header("Clip timing")]
        [SerializeField] float fireDuration = 0.37f;
        [SerializeField] float reloadDuration = 1.90f;
        [SerializeField] float getShotDuration = 0.77f;
        [SerializeField] float staggerDuration = 1.43f;

        [Header("Reactions")]
        [SerializeField, Range(0.05f, 1f)] float heavyHealthFraction = 0.40f;
        [SerializeField] float heavyKnockback = 2.5f;

        const string Idle = "Idle";
        const string Walk = "Walk";
        const string Run = "Run";
        const string Aim = "Aim";
        const string Fire = "Fire";
        const string Reload = "Reload";
        const string GetShot = "GetShot";
        const string Stagger = "Stagger";
        const string Death = "Death";

        float lockedUntil;
        bool dead;
        string currentState;

        void Awake()
        {
            if (health == null) health = GetComponent<Health>();
            if (weapon == null) weapon = GetComponent<Weapon>();
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
        }

        void OnEnable()
        {
            dead = false;
            lockedUntil = 0f;
            currentState = null;

            if (health != null)
            {
                health.Damaged += OnDamaged;
                health.Died += OnDied;
            }

            if (animator != null)
            {
                animator.applyRootMotion = false;
                animator.speed = 1f;
                Play(Idle, 0.05f);
            }
        }

        void OnDisable()
        {
            if (health != null)
            {
                health.Damaged -= OnDamaged;
                health.Died -= OnDied;
            }

            if (animator != null) animator.speed = 1f;
        }

        void Update()
        {
            if (dead || animator == null) return;

            // Weapon owns the actual reload coroutine. Polling it keeps the animation in
            // sync even when an empty magazine starts reloading automatically after a shot.
            if (weapon != null && weapon.IsReloading)
            {
                if (currentState != Reload) Play(Reload, 0.05f);

                // Fit the clip to the actual reload rather than holding a 1.9s animation over
                // a reload that upgrades have cut to 0.45s. Without this the survivor keeps
                // fumbling with the magazine for over a second after the weapon is loaded.
                float actual = Mathf.Max(0.1f, weapon.ReloadSeconds);
                animator.speed = Mathf.Clamp(reloadDuration / actual, 0.5f, 4f);

                lockedUntil = Mathf.Max(lockedUntil, Time.time + actual);
                return;
            }

            if (!Mathf.Approximately(animator.speed, 1f)) animator.speed = 1f;

            if (Time.time < lockedUntil) return;

            if (InputReader.ReloadPressed)
            {
                Play(Reload, 0.05f);
                lockedUntil = Time.time + reloadDuration;
                return;
            }

            // FirePressed catches semi-automatic weapons; FireHeld keeps the visual punch
            // alive for full-auto weapons without requiring a second event in Weapon.cs.
            if (InputReader.FirePressed || (InputReader.FireHeld && weapon != null && !weapon.IsReloading))
            {
                Play(Fire, 0.025f);
                lockedUntil = Time.time + fireDuration;
                return;
            }

            Vector2 move = InputReader.Move;
            if (move.sqrMagnitude > 0.55f)
            {
                Play(Run, 0.10f);
            }
            else if (move.sqrMagnitude > 0.02f)
            {
                Play(Walk, 0.10f);
            }
            else
            {
                // Aim is the survivor's relaxed ready stance. It keeps both hands up for
                // the weapon model while still giving Idle a readable fallback state.
                Play(Aim, 0.12f);
            }
        }

        void OnDamaged(DamageInfo info)
        {
            if (dead || animator == null || health == null) return;

            float fraction = health.Max > 0f ? info.Amount / health.Max : 0f;
            bool heavy = fraction >= heavyHealthFraction || info.KnockbackMultiplier >= heavyKnockback;

            Play(heavy ? Stagger : GetShot, 0.05f);
            lockedUntil = Time.time + (heavy ? staggerDuration : getShotDuration);
        }

        void OnDied(Health _)
        {
            if (dead) return;
            dead = true;
            lockedUntil = 0f;

            if (animator != null)
            {
                animator.speed = 1f;
                animator.Play(Death, 0, 0f);
                currentState = Death;
            }
        }

        void Play(string state, float fade)
        {
            if (animator == null || dead || currentState == state) return;

            animator.speed = 1f;
            animator.CrossFadeInFixedTime(state, fade, 0, 0f);
            currentState = state;
        }
    }
}

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

        // Two layers. The base drives the legs full-body; the masked upper layer drives the
        // arms over the top, which is the only way a twin-stick player can run and shoot at
        // once - on one layer the firing clip wins and the survivor is never seen to move.
        const int BaseLayer = 0;
        const int UpperLayer = 1;

        static readonly int ReloadSpeed = Animator.StringToHash("ReloadSpeed");

        float upperLockedUntil;
        bool dead;
        string baseState;
        string upperState;

        void Awake()
        {
            if (health == null) health = GetComponent<Health>();
            if (weapon == null) weapon = GetComponent<Weapon>();
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
        }

        void OnEnable()
        {
            dead = false;
            upperLockedUntil = 0f;
            baseState = null;
            upperState = null;

            if (health != null)
            {
                health.Damaged += OnDamaged;
                health.Died += OnDied;
            }

            if (animator != null)
            {
                animator.applyRootMotion = false;
                animator.speed = 1f;
                // Guarded: if the mask or the layer failed to build, the controller has one
                // layer and this would be an index error every time the player spawns.
                if (animator.layerCount > UpperLayer) animator.SetLayerWeight(UpperLayer, 1f);

                Play(Idle, 0.05f, BaseLayer, ref baseState);
                Play(Aim, 0.05f, UpperLayer, ref upperState);
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

            DriveLegs();
            DriveArms();
        }

        /// <summary>Never locked. The legs answer to movement and nothing else.</summary>
        void DriveLegs()
        {
            float speed = InputReader.Move.sqrMagnitude;

            string wanted = speed > 0.55f ? Run
                          : speed > 0.02f ? Walk
                          : Idle;

            Play(wanted, 0.10f, BaseLayer, ref baseState);
        }

        void DriveArms()
        {
            // Weapon owns the reload coroutine. Polling it keeps the animation in step even
            // when an empty magazine starts reloading on its own after a shot.
            if (weapon != null && weapon.IsReloading)
            {
                float actual = Mathf.Max(0.1f, weapon.ReloadSeconds);
                animator.SetFloat(ReloadSpeed, Mathf.Clamp(reloadDuration / actual, 0.5f, 4f));

                if (upperState != Reload) Play(Reload, 0.05f, UpperLayer, ref upperState);
                upperLockedUntil = Mathf.Max(upperLockedUntil, Time.time + actual);
                return;
            }

            if (Time.time < upperLockedUntil) return;

            // FirePressed catches semi-automatic weapons; FireHeld keeps the punch alive for
            // full-auto without needing a second event out of Weapon.
            if (InputReader.FirePressed || (InputReader.FireHeld && weapon != null && !weapon.IsReloading))
            {
                Play(Fire, 0.025f, UpperLayer, ref upperState);
                upperLockedUntil = Time.time + fireDuration;
                return;
            }

            // Aim is the ready stance, and the reason the upper layer has a resting pose at
            // all: without one the arms would snap back to whatever the legs are doing.
            Play(Aim, 0.12f, UpperLayer, ref upperState);
        }

        void OnDamaged(DamageInfo info)
        {
            if (dead || animator == null || health == null) return;

            float fraction = health.Max > 0f ? info.Amount / health.Max : 0f;
            bool heavy = fraction >= heavyHealthFraction || info.KnockbackMultiplier >= heavyKnockback;

            Play(heavy ? Stagger : GetShot, 0.05f, UpperLayer, ref upperState);
            upperLockedUntil = Time.time + (heavy ? staggerDuration : getShotDuration);
        }

        void OnDied(Health _)
        {
            if (dead) return;
            dead = true;
            upperLockedUntil = 0f;

            if (animator != null)
            {
                animator.speed = 1f;

                // Death is the one clip that has to own the whole body, so the arms stop
                // being driven separately for it.
                if (animator.layerCount > UpperLayer) animator.SetLayerWeight(UpperLayer, 0f);

                animator.Play(Death, BaseLayer, 0f);
                baseState = Death;
            }
        }

        void Play(string state, float fade, int layer, ref string current)
        {
            if (animator == null || dead || current == state) return;

            animator.CrossFadeInFixedTime(state, fade, layer, 0f);
            current = state;
        }
    }
}

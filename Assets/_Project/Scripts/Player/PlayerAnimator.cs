using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// Drives the authored survivor model from the existing player, health and weapon
    /// systems. Movement remains owned by PlayerController; this component only chooses
    /// visual clips and keeps root motion disabled so the collider never fights the rig.
    /// </summary>
    [RequireComponent(typeof(Health))]
    [DefaultExecutionOrder(-10)]
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

        [Header("Stride")]
        [Tooltip("Metres per second the Walk clip was authored to cover. Measured off the " +
                 "source: 0.790m of foot travel over a 1.133s cycle, two steps per cycle.")]
        [SerializeField] float walkClipSpeed = 1.39f;
        [Tooltip("Metres per second the Run clip was authored to cover: 1.052m of foot " +
                 "travel over a 0.767s cycle. The survivor actually moves at 7.")]
        [SerializeField] float runClipSpeed = 2.74f;
        [SerializeField] Vector2 strideRange = new(0.6f, 3.2f);

        [Header("Lower body")]
        [Tooltip("How far the hips may turn away from the aim to follow the direction of " +
                 "travel. Zero restores the old behaviour: legs always point where the gun " +
                 "points, and the survivor slides sideways.")]
        [SerializeField, Range(0f, 90f)] float maxHipTurn = 90f;
        [Tooltip("Degrees per second the hips turn. Low values read as the survivor " +
                 "planting a foot; high values snap.")]
        [SerializeField] float hipTurnSpeed = 720f;

        [Tooltip("Logs every base-layer state change with a timestamp. Turn on for one run " +
                 "if the legs ever stop: the console then says exactly what they went to " +
                 "and when.")]
        [SerializeField] bool logLegStates;

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
        const string Ultimate = "Ultimate";

        // Two layers. The base drives the legs full-body; the masked upper layer drives the
        // arms over the top, which is the only way a twin-stick player can run and shoot at
        // once - on one layer the firing clip wins and the survivor is never seen to move.
        const int BaseLayer = 0;
        const int UpperLayer = 1;

        static readonly int ReloadSpeed = Animator.StringToHash("ReloadSpeed");
        static readonly int LocomotionSpeed = Animator.StringToHash("LocomotionSpeed");

        [SerializeField] PlayerController movement;
        Transform pelvis;
        Transform spine;
        float hipTurn;

        float upperLockedUntil;
        bool dead;
        string baseState;
        string upperState;
        bool ultimatePose;

        void Awake()
        {
            if (health == null) health = GetComponent<Health>();
            if (weapon == null) weapon = GetComponent<Weapon>();
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
            if (movement == null) movement = GetComponent<PlayerController>();

            if (animator != null)
            {
                pelvis = PlayerWeaponGrip.Find(animator.transform, "Pelvis");
                spine = PlayerWeaponGrip.Find(animator.transform, "Spine");
            }
        }

        /// <summary>
        /// Turns the hips toward the direction the survivor is actually travelling, and
        /// unwinds the same angle at the spine so the torso keeps facing the aim.
        /// <para>
        /// Facing and movement are independent in a twin-stick game, but there are only
        /// three locomotion clips and all three run FORWARD. Aim left while walking north
        /// and the survivor played a forward run pointed left while sliding north - which
        /// reads exactly as reported from play: the legs stop meaning anything and the
        /// character floats. Strafe clips would be the other fix; this one needs no new art
        /// and stays correct at every angle rather than at four of them.
        /// </para>
        /// <para>
        /// Legs hang off Pelvis and everything else hangs off Spine, so two bones is the
        /// whole correction. It has to happen in LateUpdate, after the Animator has written
        /// the authored pose, and before WeaponVisuals solves the grip - hence the execution
        /// order on both.
        /// </para>
        /// </summary>
        void LateUpdate()
        {
            if (pelvis == null || spine == null) return;

            float wanted = 0f;

            // Dead, mid-ultimate, or standing still: unwind to neutral rather than holding
            // a twist. A corpse with its hips cocked ninety degrees is worse than no fix.
            if (!dead && !ultimatePose && movement != null)
            {
                var travel = movement.Velocity;
                travel.y = 0f;

                if (travel.sqrMagnitude > 0.04f)
                    wanted = Mathf.Clamp(
                        Vector3.SignedAngle(transform.forward, travel.normalized, Vector3.up),
                        -maxHipTurn, maxHipTurn);
            }

            hipTurn = Mathf.MoveTowardsAngle(hipTurn, wanted, hipTurnSpeed * Time.deltaTime);
            if (Mathf.Abs(hipTurn) < 0.01f) return;

            var turn = Quaternion.AngleAxis(hipTurn, Vector3.up);
            pelvis.rotation = turn * pelvis.rotation;
            spine.rotation = Quaternion.AngleAxis(-hipTurn, Vector3.up) * spine.rotation;
        }

        void OnEnable()
        {
            dead = false;
            upperLockedUntil = 0f;
            baseState = null;
            upperState = null;
            ultimatePose = false;

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

            // Full-body flourish during the auto-aim magazine. PlayerController supplies
            // the world spin; the clip supplies the pose and pivot without double rotation.
            if (weapon != null && weapon.UltimateActive)
            {
                if (!ultimatePose)
                {
                    ultimatePose = true;
                    upperLockedUntil = 0f;
                    if (animator.layerCount > UpperLayer) animator.SetLayerWeight(UpperLayer, 0f);
                    Play(Ultimate, .06f, BaseLayer, ref baseState);
                }
                return;
            }

            if (ultimatePose)
            {
                ultimatePose = false;
                upperLockedUntil = 0f;
                upperState = null;
                if (animator.layerCount > UpperLayer) animator.SetLayerWeight(UpperLayer, 1f);
                Play(Aim, .08f, UpperLayer, ref upperState);
            }

            DriveLegs();
            DriveArms();
        }

        /// <summary>
        /// Never locked. The legs answer to movement and nothing else.
        /// <para>
        /// The clip is also fitted to the distance actually being covered. Walk and Run are
        /// authored at a fixed stride - 1.39 and 2.74 m/s, measured off the source rather
        /// than estimated - and the survivor moves at 7. Played at their authored rate the
        /// feet plant and the body glides past them, which is the walk-then-float that came
        /// back from play: the legs were animating the whole time, just far too slowly for
        /// the ground being covered.
        /// </para>
        /// </summary>
        void DriveLegs()
        {
            float input = InputReader.Move.sqrMagnitude;

            string wanted = input > 0.55f ? Run
                          : input > 0.02f ? Walk
                          : Idle;

            Play(wanted, 0.10f, BaseLayer, ref baseState);

            float travelled = movement != null
                ? new Vector2(movement.Velocity.x, movement.Velocity.z).magnitude
                : 0f;

            float authored = wanted == Run ? runClipSpeed : walkClipSpeed;
            float stride = wanted == Idle || authored <= 0.01f
                ? 1f
                : Mathf.Clamp(travelled / authored, strideRange.x, strideRange.y);

            animator.SetFloat(LocomotionSpeed, stride);
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
            if (dead || animator == null || health == null || (weapon != null && weapon.UltimateActive)) return;

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

        /// <summary>
        /// Asks the animator what it is actually playing rather than trusting a cached name.
        /// <para>
        /// The cache alone is why the legs could stop for good. `current` recorded what was
        /// last REQUESTED, so once anything knocked the layer off that state - an interrupted
        /// crossfade, a clip that ended, a stray Play - every later frame compared equal,
        /// returned early, and never asked again. The layer stayed wrong until the next time
        /// the player changed speed, and standing in one state is exactly when that does not
        /// happen. Verifying against the animator makes it self-healing within a frame.
        /// </para>
        /// </summary>
        void Play(string state, float fade, int layer, ref string current)
        {
            if (animator == null || dead) return;
            if (current == state && IsPlaying(state, layer)) return;

            animator.CrossFadeInFixedTime(state, fade, layer, 0f);
            current = state;

            if (logLegStates && layer == BaseLayer)
                Debug.Log($"PlayerAnimator: base layer -> {state} at {Time.time:F2}s");
        }

        bool IsPlaying(string state, int layer)
        {
            if (layer >= animator.layerCount) return true;

            // Mid-transition the destination is what matters; arriving there is not a reason
            // to restart the crossfade.
            if (animator.IsInTransition(layer))
                return animator.GetNextAnimatorStateInfo(layer).IsName(state);

            var info = animator.GetCurrentAnimatorStateInfo(layer);
            if (!info.IsName(state)) return false;

            // A looping clip that has been left to run is fine. A one-shot that has played
            // out is holding its last frame, which on the legs is indistinguishable from
            // the character sliding with no animation at all.
            return info.loop || info.normalizedTime < 1f;
        }
    }
}

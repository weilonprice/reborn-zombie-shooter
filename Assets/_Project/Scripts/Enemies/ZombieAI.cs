using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// Horde-friendly chaser. Steers straight at the player and pushes off neighbours instead
    /// of pathfinding — the arena is open, and this scales to far more agents than NavMesh
    /// avoidance would. Swap in a NavMeshAgent if the map ever gains real obstacles.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(Health))]
    public class ZombieAI : MonoBehaviour
    {
        /// <summary>Every live zombie, used for O(n^2) local separation at prototype scale.</summary>
        static readonly List<ZombieAI> Active = new();

        /// <summary>
        /// Read-only view of the live horde, so target-seeking effects (ricochet chains, the
        /// akimbo off-hand, blast radii) can find enemies without a physics query. Entries
        /// stay listed through the death linger, so callers must check <see cref="Health"/>.
        /// </summary>
        public static IReadOnlyList<ZombieAI> ActiveZombies => Active;

        public Health Health => health;

        [Header("Movement")]
        [SerializeField] float moveSpeed = 2.6f;
        [SerializeField] float turnSpeed = 360f;
        [SerializeField] float gravity = -20f;

        [Header("Separation")]
        [Tooltip("Zombies closer than this push each other apart so the horde spreads out.")]
        [SerializeField] float separationRadius = 1.1f;
        [SerializeField] float separationStrength = 2.2f;

        [Header("Knockback")]
        [Tooltip("Base impulse away from the bullet. Weapons scale this rather than " +
                 "replacing it, so a heavier enemy can simply lower its own value.")]
        [SerializeField] float knockbackForce = 4f;
        [Tooltip("How fast that impulse bleeds off, in units/sec^2. Higher is snappier.")]
        [SerializeField] float knockbackDecay = 14f;

        [Header("Combat")]
        [SerializeField] float attackRange = 1.6f;
        [SerializeField] float attackDamage = 12f;
        [SerializeField] float attackCooldown = 1.1f;
        [Tooltip("Scales damage dealt to barricades only. The boss raises this in its second " +
                 "phase so a wall falls in one blow.")]
        [SerializeField] float barricadeDamageMultiplier = 1f;
        [SerializeField] int scoreValue = 10;
        [SerializeField] int goldReward = 10;
        [Tooltip("Seconds the body stays up after dying, so the kill flash and hit-stop " +
                 "have something on screen to land on. Not a death animation - just enough " +
                 "frames for the impact to register.")]
        [SerializeField] float deathLinger = 0.22f;
        [SerializeField] AudioClip deathClip;
        [SerializeField] float deathVolume = 0.55f;
        [Tooltip("Trauma on death. Larger than a shot so kills punctuate sustained fire.")]
        [SerializeField] float killTrauma = 0.22f;

        [Header("Ranged Combat")]
        [SerializeField] bool isRanged;
        [SerializeField] EnemyProjectile projectilePrefab;
        [Tooltip("Distance the ranged enemy tries to maintain from the player.")]
        [SerializeField] float preferredRange = 11f;
        [SerializeField] Transform shootPoint;
        [SerializeField] AudioClip shootClip;
        [SerializeField, Range(0f, 1f)] float shootVolume = 0.45f;

        CharacterController controller;
        Health health;
        Transform target;
        float verticalVelocity;
        float nextAttackTime;
        Vector3 knockback;
        bool dying;

        /// <summary>Raised when this zombie dies, so the spawner can recycle it.</summary>
        public event Action<ZombieAI> Died;

        public int ScoreValue => scoreValue;
        public int GoldReward => goldReward;
        /// <summary>The prefab this instance was instantiated from, for multi-type pooling.</summary>
        public ZombieAI PrefabSource { get; set; }

        // Runtime setters so BossController can switch this AI between the behaviours it
        // already supports, instead of a second AI duplicating steering and separation.
        public void SetRanged(bool value) => isRanged = value;
        public void SetMoveSpeed(float value) => moveSpeed = value;
        public void SetAttackDamage(float value) => attackDamage = value;
        public void SetAttackCooldown(float value) => attackCooldown = value;
        public void SetBarricadeDamageMultiplier(float value) => barricadeDamageMultiplier = value;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            health = GetComponent<Health>();
        }

        void OnEnable()
        {
            Active.Add(this);
            health.Died += OnDied;
            health.Damaged += OnDamaged;
            verticalVelocity = 0f;
            nextAttackTime = 0f;
            knockback = Vector3.zero;
            dying = false;
            controller.enabled = true;
        }

        void OnDisable()
        {
            Active.Remove(this);
            health.Died -= OnDied;
            health.Damaged -= OnDamaged;
        }

        static readonly RaycastHit[] ObstacleHits = new RaycastHit[4];
        Barricade blockingBarricade;

        public void SetTarget(Transform t) => target = t;

        void Update()
        {
            if (!health.IsAlive) return;
            if (GameManager.Instance != null && GameManager.Instance.State != GameState.Playing) return;

            if (target == null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player == null) return;
                target = player.transform;
            }

            var toTarget = target.position - transform.position;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;

            blockingBarricade = !isRanged ? CheckForBlockingBarricade() : null;

            Steer(toTarget, distance);

            if (distance <= attackRange)
            {
                TryAttack();
            }
            else if (blockingBarricade != null && blockingBarricade.IsAlive)
            {
                TryAttackBarricade(blockingBarricade);
            }
        }

        Barricade CheckForBlockingBarricade()
        {
            var origin = transform.position + Vector3.up * 0.5f;
            var dir = transform.forward;
            int count = Physics.SphereCastNonAlloc(origin, 0.35f, dir, ObstacleHits, attackRange, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var b = ObstacleHits[i].collider.GetComponentInParent<Barricade>();
                if (b != null && b.IsAlive) return b;
            }
            return null;
        }

        void TryAttackBarricade(Barricade barricade)
        {
            if (Time.time < nextAttackTime || barricade == null || !barricade.IsAlive) return;
            nextAttackTime = Time.time + attackCooldown;

            barricade.TakeDamage(new DamageInfo(
                attackDamage * barricadeDamageMultiplier,
                transform.position, -transform.forward, 1f, gameObject));
        }

        void Steer(Vector3 toTarget, float distance)
        {
            var chase = distance > 0.001f ? toTarget / distance : Vector3.zero;
            var move = chase;

            if (isRanged)
            {
                // Ranged enemies try to stay in their preferred range window.
                if (distance <= preferredRange)
                {
                    move = Vector3.zero;
                    // If the player charges close, backpedal slightly to keep breathing room.
                    if (distance < preferredRange * 0.55f)
                        move = -chase * 0.55f;
                }
            }
            else
            {
                // Stop closing once in melee range of player or clawing a barricade,
                // but keep separating so bodies don't stack.
                if (distance <= attackRange || (blockingBarricade != null && blockingBarricade.IsAlive))
                    move = Vector3.zero;
            }

            move += Separation() * separationStrength;

            if (move.sqrMagnitude > 1f) move.Normalize();

            var horizontal = move * moveSpeed + knockback;
            knockback = Vector3.MoveTowards(knockback, Vector3.zero, knockbackDecay * Time.deltaTime);

            if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
            else verticalVelocity += gravity * Time.deltaTime;

            horizontal.y = verticalVelocity;
            controller.Move(horizontal * Time.deltaTime);

            // Always face the player, even while being shoved sideways by neighbours.
            if (chase.sqrMagnitude > 0.0001f)
            {
                var look = Quaternion.LookRotation(chase, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, look, turnSpeed * Time.deltaTime);
            }
        }

        Vector3 Separation()
        {
            var push = Vector3.zero;
            float radiusSqr = separationRadius * separationRadius;

            for (int i = 0; i < Active.Count; i++)
            {
                var other = Active[i];
                if (other == this) continue;

                var away = transform.position - other.transform.position;
                away.y = 0f;
                float sqr = away.sqrMagnitude;
                if (sqr > radiusSqr || sqr < 0.0001f) continue;

                // Weight by closeness so touching neighbours dominate.
                push += away / sqr;
            }

            return Vector3.ClampMagnitude(push, 1f);
        }

        void TryAttack()
        {
            if (Time.time < nextAttackTime) return;

            if (isRanged)
            {
                if (projectilePrefab == null) return;
                nextAttackTime = Time.time + attackCooldown;

                var origin = shootPoint != null ? shootPoint.position : transform.position + Vector3.up * 0.8f;
                var aimTarget = target != null ? target.position + Vector3.up * 0.8f : origin + transform.forward * 10f;
                var dir = (aimTarget - origin).normalized;
                var rot = dir.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(dir, Vector3.up) : transform.rotation;

                EnemyProjectile.Spawn(projectilePrefab, origin, rot, attackDamage, gameObject);

                if (shootClip != null)
                    SfxPlayer.Instance?.PlayAt(shootClip, origin, shootVolume);
            }
            else
            {
                nextAttackTime = Time.time + attackCooldown;

                var damageable = target.GetComponentInParent<IDamageable>();
                if (damageable == null || !damageable.IsAlive) return;

                damageable.TakeDamage(new DamageInfo(
                    attackDamage, transform.position, -transform.forward, 1f, gameObject));
            }
        }

        void OnDamaged(DamageInfo info)
        {
            // The surface normal points back toward the shooter, so its inverse is the
            // bullet's direction of travel. Flattened, that pushes the body away from you.
            var away = -info.Normal;
            away.y = 0f;
            if (away.sqrMagnitude < 0.0001f) return;

            knockback = away.normalized * (knockbackForce * info.KnockbackMultiplier);
        }

        void OnDied(Health _)
        {
            if (dying) return;
            dying = true;

            GameManager.Instance?.AddScore(scoreValue);
            // Bounty Hunter pays double, so the award must know which weapon landed the
            // killing blow - DamageInfo.Source is the player for every gun.
            var killer = health.LastDamage.SourceWeapon;
            bool doubled = killer != null && UpgradeManager.Resolve(killer).DoubleGoldOnKill;

            GameManager.Instance?.AddGold(doubled ? goldReward * 2 : goldReward);
            HitStop.Instance?.FreezeForKill();
            SfxPlayer.Instance?.PlayAt(deathClip, transform.position, deathVolume);
            CameraShake.Instance?.AddTrauma(killTrauma);

            // Stop the collapsing body soaking bullets or pushing its neighbours around.
            controller.enabled = false;

            StartCoroutine(DespawnAfterLinger());
        }

        IEnumerator DespawnAfterLinger()
        {
            // Scaled time, so this stays in step with DeathPop's collapse - both pause
            // together during the kill freeze and resume together after it.
            yield return new WaitForSeconds(deathLinger);

            Died?.Invoke(this);
        }
    }
}

using System;
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

        [Header("Movement")]
        [SerializeField] float moveSpeed = 2.6f;
        [SerializeField] float turnSpeed = 360f;
        [SerializeField] float gravity = -20f;

        [Header("Separation")]
        [Tooltip("Zombies closer than this push each other apart so the horde spreads out.")]
        [SerializeField] float separationRadius = 1.1f;
        [SerializeField] float separationStrength = 2.2f;

        [Header("Combat")]
        [SerializeField] float attackRange = 1.6f;
        [SerializeField] float attackDamage = 12f;
        [SerializeField] float attackCooldown = 1.1f;
        [SerializeField] int scoreValue = 10;

        CharacterController controller;
        Health health;
        Transform target;
        float verticalVelocity;
        float nextAttackTime;

        /// <summary>Raised when this zombie dies, so the spawner can recycle it.</summary>
        public event Action<ZombieAI> Died;

        public int ScoreValue => scoreValue;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            health = GetComponent<Health>();
        }

        void OnEnable()
        {
            Active.Add(this);
            health.Died += OnDied;
            verticalVelocity = 0f;
            nextAttackTime = 0f;
        }

        void OnDisable()
        {
            Active.Remove(this);
            health.Died -= OnDied;
        }

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

            Steer(toTarget, distance);

            if (distance <= attackRange)
                TryAttack();
        }

        void Steer(Vector3 toTarget, float distance)
        {
            var chase = distance > 0.001f ? toTarget / distance : Vector3.zero;
            var move = chase;

            // Stop closing once in melee range, but keep separating so bodies don't stack.
            if (distance <= attackRange) move = Vector3.zero;

            move += Separation() * separationStrength;

            if (move.sqrMagnitude > 1f) move.Normalize();

            var horizontal = move * moveSpeed;

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
            nextAttackTime = Time.time + attackCooldown;

            var damageable = target.GetComponentInParent<IDamageable>();
            if (damageable == null || !damageable.IsAlive) return;

            damageable.TakeDamage(attackDamage, transform.position, -transform.forward);
        }

        void OnDied(Health _)
        {
            GameManager.Instance?.AddScore(scoreValue);
            Died?.Invoke(this);
        }
    }
}

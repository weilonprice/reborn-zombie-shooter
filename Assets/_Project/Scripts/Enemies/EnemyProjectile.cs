using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// Linear projectile fired by ranged enemies.
    /// Uses sphere-cast sweeping to avoid tunneling through the player at high speeds,
    /// ignores fellow horde members, and recycles through an internal pool.
    /// </summary>
    public class EnemyProjectile : MonoBehaviour
    {
        [SerializeField] float speed = 12f;
        [SerializeField] float maxLifetime = 4f;
        [SerializeField] float radius = 0.25f;
        [SerializeField] LayerMask hitMask = ~0;
        [Tooltip("Optional. Left behind where the projectile lands - the spitter's acid.")]
        [SerializeField] GameObject impactSpawn;
        [SerializeField] AudioClip impactClip;
        [SerializeField, Range(0f, 1f)] float impactVolume = 0.45f;

        static readonly RaycastHit[] HitBuffer = new RaycastHit[8];
        static readonly Queue<EnemyProjectile> Pool = new();
        static Transform PoolRoot;

        float damage;
        GameObject shooter;
        float aliveTime;

        public static EnemyProjectile Spawn(EnemyProjectile prefab, Vector3 position, Quaternion rotation, float damage, GameObject shooter)
        {
            if (prefab == null) return null;

            if (PoolRoot == null)
            {
                Pool.Clear();
                var rootGo = new GameObject("EnemyProjectilePool");
                PoolRoot = rootGo.transform;
            }

            EnemyProjectile proj = null;
            while (Pool.Count > 0 && proj == null)
                proj = Pool.Dequeue();

            if (proj == null)
                proj = Instantiate(prefab, PoolRoot);

            proj.transform.SetPositionAndRotation(position, rotation);
            proj.damage = damage;
            proj.shooter = shooter;
            proj.aliveTime = 0f;
            proj.gameObject.SetActive(true);
            return proj;
        }

        public void Despawn()
        {
            if (!gameObject.activeSelf) return;
            gameObject.SetActive(false);
            if (PoolRoot != null) transform.SetParent(PoolRoot, false);
            Pool.Enqueue(this);
        }

        void Update()
        {
            aliveTime += Time.deltaTime;
            if (aliveTime >= maxLifetime)
            {
                Despawn();
                return;
            }

            float step = speed * Time.deltaTime;
            var origin = transform.position;
            var direction = transform.forward;

            int hits = Physics.SphereCastNonAlloc(origin, radius, direction, HitBuffer, step, hitMask,
                                                  QueryTriggerInteraction.Ignore);
            if (hits > 0)
            {
                Array.Sort(HitBuffer, 0, hits, HitDistanceComparer.Instance);
                for (int i = 0; i < hits; i++)
                {
                    var hit = HitBuffer[i];
                    if (hit.collider == null) continue;

                    // Ignore shooter and friendly zombies
                    if (hit.collider.gameObject == shooter) continue;
                    if (hit.collider.GetComponentInParent<ZombieAI>() != null) continue;

                    var damageable = hit.collider.GetComponentInParent<IDamageable>();
                    if (damageable != null && damageable.IsAlive)
                    {
                        damageable.TakeDamage(new DamageInfo(damage, hit.point, hit.normal, 1f, shooter));
                    }

                    ImpactEffects.Instance?.PlayImpact(hit.point, hit.normal);
                    if (impactSpawn != null)
                    {
                        // Dropped on the floor plane rather than at the hit point, or a shot
                        // that clipped a shoulder would leave a puddle in mid air.
                        var ground = new Vector3(hit.point.x, 0.03f, hit.point.z);
                        Instantiate(impactSpawn, ground, Quaternion.identity);
                    }

                    if (impactClip != null)
                        SfxPlayer.Instance?.PlayAt(impactClip, hit.point, impactVolume);

                    Despawn();
                    return;
                }
            }

            transform.position += direction * step;
        }

        sealed class HitDistanceComparer : IComparer<RaycastHit>
        {
            public static readonly HitDistanceComparer Instance = new();
            public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
        }
    }
}

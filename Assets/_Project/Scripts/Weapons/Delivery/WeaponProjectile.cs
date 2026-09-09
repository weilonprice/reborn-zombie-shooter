using System.Collections.Generic;
using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// A round with travel time, for the grenade launcher and nail gun.
    /// <para>
    /// Sweeps with a spherecast rather than testing a point, so a fast round cannot tunnel
    /// through a body between frames. Recycles through a static pool the same way
    /// <see cref="EnemyProjectile"/> does.
    /// </para>
    /// <para>
    /// It carries its damage rather than asking the weapon for it at impact. By the time this
    /// lands the player may have swapped weapons, bought a tier, or died - the shot has to be
    /// worth what it was worth when it was fired. The cost is that kills here do not route
    /// through Weapon.OnKill, so they charge no ultimate and refund no reserve.
    /// </para>
    /// </summary>
    public class WeaponProjectile : MonoBehaviour
    {
        [SerializeField] float speed = 26f;
        [Tooltip("Downward acceleration. 0 flies flat, higher values lob.")]
        [SerializeField] float gravity = 0f;
        [SerializeField] float maxLifetime = 4f;
        [SerializeField] float radius = 0.22f;
        [SerializeField] LayerMask hitMask = ~0;
        [SerializeField] AudioClip impactClip;
        [SerializeField, Range(0f, 1f)] float impactVolume = 0.5f;

        static readonly RaycastHit[] HitBuffer = new RaycastHit[8];
        static readonly Collider[] ImpactBuffer = new Collider[8];
        static readonly Queue<WeaponProjectile> Pool = new();
        static Transform poolRoot;

        float damage;
        float knockback;
        float blastRadius;
        GameObject shooter;
        WeaponDefinition sourceWeapon;

        Vector3 velocity;
        float aliveTime;

        public static void Spawn(WeaponProjectile prefab, Vector3 position, Vector3 direction,
                                 float damage, float knockback, float blastRadius,
                                 GameObject shooter, WeaponDefinition sourceWeapon)
        {
            if (prefab == null) return;

            if (poolRoot == null)
            {
                Pool.Clear();
                poolRoot = new GameObject("WeaponProjectilePool").transform;
            }

            WeaponProjectile projectile = null;
            while (Pool.Count > 0 && projectile == null)
                projectile = Pool.Dequeue();

            if (projectile == null) projectile = Instantiate(prefab, poolRoot);

            projectile.transform.SetPositionAndRotation(position, Quaternion.LookRotation(direction));
            projectile.velocity = direction.normalized * projectile.speed;
            projectile.damage = damage;
            projectile.knockback = knockback;
            projectile.blastRadius = blastRadius;
            projectile.shooter = shooter;
            projectile.sourceWeapon = sourceWeapon;
            projectile.aliveTime = 0f;
            projectile.gameObject.SetActive(true);
        }

        void Update()
        {
            if (Time.timeScale <= 0f) return;

            aliveTime += Time.deltaTime;
            if (aliveTime >= maxLifetime)
            {
                Detonate(transform.position, Vector3.up);
                return;
            }

            velocity += Vector3.down * (gravity * Time.deltaTime);

            float step = velocity.magnitude * Time.deltaTime;
            if (step <= 0f) return;

            var direction = velocity / velocity.magnitude;

            int count = Physics.SphereCastNonAlloc(transform.position, radius, direction,
                                                   HitBuffer, step, hitMask,
                                                   QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                var hit = HitBuffer[i];
                if (hit.collider == null) continue;

                // Never detonate on the player who fired it, or on their own fortifications -
                // a grenade that armed itself on your barricade line would be unusable.
                if (hit.collider.transform.IsChildOf(shooter.transform)) continue;
                if (hit.collider.GetComponentInParent<Barricade>() != null) continue;

                Detonate(hit.point, hit.normal);
                return;
            }

            transform.position += velocity * Time.deltaTime;
            transform.rotation = Quaternion.LookRotation(direction);
        }

        void Detonate(Vector3 point, Vector3 normal)
        {
            if (blastRadius > 0f)
            {
                Blast.Damage(point, blastRadius, damage, shooter, sourceWeapon);
                CameraShake.Instance?.AddTrauma(0.28f);
            }
            else
            {
                // A direct round still needs to find what it touched, since the sweep only
                // told us where it stopped.
                int count = Physics.OverlapSphereNonAlloc(point, radius + 0.15f, ImpactBuffer,
                                                          hitMask, QueryTriggerInteraction.Ignore);

                for (int i = 0; i < count; i++)
                {
                    if (ImpactBuffer[i] == null) continue;

                    var target = ImpactBuffer[i].GetComponentInParent<IDamageable>();
                    if (target == null || !target.IsAlive) continue;
                    if (ImpactBuffer[i].GetComponentInParent<Barricade>() != null) continue;

                    target.TakeDamage(new DamageInfo(damage, point, normal, knockback,
                                                     shooter, sourceWeapon));
                    break;
                }

                ImpactEffects.Instance?.PlayImpact(point, normal);
            }

            SfxPlayer.Instance?.PlayAt(impactClip, point, impactVolume);
            Despawn();
        }

        void Despawn()
        {
            if (!gameObject.activeSelf) return;

            gameObject.SetActive(false);
            if (poolRoot != null) transform.SetParent(poolRoot, false);
            Pool.Enqueue(this);
        }
    }
}

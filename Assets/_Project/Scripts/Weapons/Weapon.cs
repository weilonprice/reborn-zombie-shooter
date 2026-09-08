using System;
using System.Collections;
using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// Hitscan firearm with a magazine and reload. Fires from the muzzle along the owner's
    /// facing, which the twin-stick controller already aligns with the aim point.
    /// </summary>
    public class Weapon : MonoBehaviour
    {
        [Header("Ballistics")]
        [SerializeField] float damage = 25f;
        [Tooltip("Rounds per minute.")]
        [SerializeField] float fireRate = 480f;
        [SerializeField] float range = 60f;
        [Tooltip("Cone half-angle in degrees applied to every shot.")]
        [SerializeField] float spread = 1.5f;
        [SerializeField] int pelletsPerShot = 1;
        [SerializeField] LayerMask hitMask = ~0;

        [Header("Ammo")]
        [SerializeField] int magazineSize = 30;
        [SerializeField] float reloadTime = 1.4f;

        [Header("Feedback")]
        [SerializeField] Transform muzzle;
        [SerializeField] LineRenderer tracer;
        [SerializeField] MuzzleFlash muzzleFlash;
        [SerializeField] float tracerDuration = 0.03f;

        float nextFireTime;
        Coroutine reloadRoutine;
        Health ownerHealth;

        public int MagazineSize => magazineSize;
        public int Ammo { get; private set; }
        public bool IsReloading => reloadRoutine != null;

        /// <summary>(ammo, magazineSize)</summary>
        public event Action<int, int> AmmoChanged;

        void Awake()
        {
            if (muzzle == null) muzzle = transform;
            ownerHealth = GetComponentInParent<Health>();
            Ammo = magazineSize;
        }

        void Start()
        {
            AmmoChanged?.Invoke(Ammo, magazineSize);
        }

        void Update()
        {
            if (GameManager.Instance != null && GameManager.Instance.State != GameState.Playing)
                return;

            if (ownerHealth != null && !ownerHealth.IsAlive)
                return;

            if (InputReader.ReloadPressed) BeginReload();
            if (InputReader.FireHeld) TryFire();
        }

        public void TryFire()
        {
            if (IsReloading || Time.time < nextFireTime) return;

            if (Ammo <= 0)
            {
                BeginReload();
                return;
            }

            nextFireTime = Time.time + 60f / Mathf.Max(1f, fireRate);
            Ammo--;
            AmmoChanged?.Invoke(Ammo, magazineSize);

            if (muzzleFlash != null) muzzleFlash.Play();

            for (int i = 0; i < pelletsPerShot; i++)
                FireOnePellet();

            if (Ammo == 0) BeginReload();
        }

        void FireOnePellet()
        {
            var direction = ApplySpread(muzzle.forward);
            var origin = muzzle.position;
            var endPoint = origin + direction * range;

            if (Physics.Raycast(origin, direction, out var hit, range, hitMask, QueryTriggerInteraction.Ignore))
            {
                endPoint = hit.point;

                // GetComponentInParent so colliders on child meshes still report to the root.
                var target = hit.collider.GetComponentInParent<IDamageable>();
                target?.TakeDamage(damage, hit.point, hit.normal);

                ImpactEffects.Instance?.PlayImpact(hit.point, hit.normal);
            }

            if (tracer != null) StartCoroutine(ShowTracer(origin, endPoint));
        }

        Vector3 ApplySpread(Vector3 forward)
        {
            if (spread <= 0f) return forward;

            // Random rotation within a cone around `forward`.
            var deviation = Quaternion.Euler(
                UnityEngine.Random.Range(-spread, spread),
                UnityEngine.Random.Range(-spread, spread),
                0f);
            return deviation * forward;
        }

        IEnumerator ShowTracer(Vector3 from, Vector3 to)
        {
            tracer.enabled = true;
            tracer.positionCount = 2;
            tracer.SetPosition(0, from);
            tracer.SetPosition(1, to);

            yield return new WaitForSeconds(tracerDuration);

            tracer.enabled = false;
        }

        public void BeginReload()
        {
            if (IsReloading || Ammo == magazineSize) return;
            reloadRoutine = StartCoroutine(ReloadRoutine());
        }

        IEnumerator ReloadRoutine()
        {
            yield return new WaitForSeconds(reloadTime);

            Ammo = magazineSize;
            AmmoChanged?.Invoke(Ammo, magazineSize);
            reloadRoutine = null;
        }
    }
}

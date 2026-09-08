using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// Runtime for a firearm. Holds no stats of its own: it executes a
    /// <see cref="WeaponDefinition"/> against the scene references wired here, so a new
    /// weapon is a new asset rather than a new script or another tuned component.
    /// Switching weapons is <see cref="Equip"/>.
    /// </summary>
    public class Weapon : MonoBehaviour
    {
        [Header("Definition")]
        [SerializeField] WeaponDefinition definition;

        [Header("Scene wiring")]
        [Tooltip("Where the hit test starts. Defaults to this object, which sits at the " +
                 "player's centre. Must NOT be the muzzle: at melee range the barrel is " +
                 "inside the zombie's collider, and a ray starting inside a collider never " +
                 "reports hitting it, so point-blank shots pass straight through.")]
        [SerializeField] Transform rayOrigin;
        [SerializeField] Transform muzzle;
        [SerializeField] MuzzleFlash muzzleFlash;
        [SerializeField] ParticleSystem shellEject;
        [SerializeField] LayerMask hitMask = ~0;

        // Shared across every weapon: only one shot is ever resolved at a time.
        static readonly RaycastHit[] HitBuffer = new RaycastHit[24];

        float nextFireTime;
        Coroutine reloadRoutine;
        Health ownerHealth;

        public WeaponDefinition Definition => definition;
        public int MagazineSize => definition != null ? definition.MagazineSize : 0;
        public int Ammo { get; private set; }
        public bool IsReloading => reloadRoutine != null;

        /// <summary>(ammo, magazineSize)</summary>
        public event Action<int, int> AmmoChanged;

        void Awake()
        {
            if (muzzle == null) muzzle = transform;
            ownerHealth = GetComponentInParent<Health>();

            Equip(definition);
        }

        void Start()
        {
            AmmoChanged?.Invoke(Ammo, MagazineSize);
        }

        /// <summary>Swaps the weapon's design wholesale, resetting ammo and presentation.</summary>
        public void Equip(WeaponDefinition next)
        {
            definition = next;

            if (definition == null)
            {
                Debug.LogError($"{nameof(Weapon)}: no {nameof(WeaponDefinition)} assigned - " +
                               "this weapon cannot fire.", this);
                return;
            }

            CancelReload();
            Ammo = definition.MagazineSize;
            nextFireTime = 0f;

            AmmoChanged?.Invoke(Ammo, definition.MagazineSize);
        }

        void Update()
        {
            if (definition == null) return;
            if (GameManager.Instance != null && GameManager.Instance.State != GameState.Playing) return;
            if (ownerHealth != null && !ownerHealth.IsAlive) return;

            if (InputReader.ReloadPressed) BeginReload();

            // Semi-auto reads the press, not the hold, so holding the trigger fires once.
            bool pullingTrigger = definition.Mode == FireMode.Automatic
                ? InputReader.FireHeld
                : InputReader.FirePressed;

            if (pullingTrigger) TryFire();
        }

        public void TryFire()
        {
            if (definition == null || IsReloading || Time.time < nextFireTime) return;

            if (Ammo <= 0)
            {
                BeginReload();
                return;
            }

            nextFireTime = Time.time + definition.SecondsBetweenShots;
            Ammo--;
            AmmoChanged?.Invoke(Ammo, definition.MagazineSize);

            // Once per trigger pull, not per pellet: a shotgun flashes once and ejects one shell.
            if (muzzleFlash != null) muzzleFlash.Play();
            SfxPlayer.Instance?.PlayFlat(definition.FireClip, definition.FireVolume);
            CameraShake.Instance?.AddTrauma(definition.FireTrauma);
            CameraShake.Instance?.AddRecoil(-muzzle.forward, definition.RecoilKick);

            if (shellEject != null && definition.ShellsPerShot > 0)
                shellEject.Emit(definition.ShellsPerShot);

            bool anyHit = false;
            var impactPoint = Vector3.zero;

            for (int i = 0; i < definition.PelletsPerShot; i++)
            {
                if (FireOnePellet(out var point) && !anyHit)
                {
                    anyHit = true;
                    impactPoint = point;
                }
            }

            // One impact sound per trigger pull. A shotgun putting eight pellets through two
            // bodies each would otherwise fire sixteen voices and swamp a fourteen-voice pool.
            if (anyHit)
                SfxPlayer.Instance?.PlayAt(definition.ImpactClip, impactPoint, definition.ImpactVolume);

            if (Ammo == 0) BeginReload();
        }

        /// <summary>
        /// Resolves one pellet, passing through up to <c>PierceCount</c> further bodies.
        /// Returns whether it connected with anything, and where it first did.
        /// </summary>
        bool FireOnePellet(out Vector3 firstImpact)
        {
            firstImpact = Vector3.zero;

            var direction = ApplySpread(muzzle.forward);
            var origin = rayOrigin != null ? rayOrigin.position : transform.position;
            float range = definition.Range;
            var endPoint = origin + direction * range;

            int count = Physics.RaycastNonAlloc(origin, direction, HitBuffer, range, hitMask,
                                                QueryTriggerInteraction.Ignore);
            bool connected = false;

            if (count > 0)
            {
                // RaycastNonAlloc returns hits in arbitrary order; penetration needs them
                // resolved nearest first or damage falloff would apply down the wrong chain.
                Array.Sort(HitBuffer, 0, count, HitDistanceComparer.Instance);

                float damage = definition.Damage;
                int bodiesHit = 0;

                for (int i = 0; i < count; i++)
                {
                    var hit = HitBuffer[i];

                    // GetComponentInParent so colliders on child meshes report to the root.
                    var target = hit.collider.GetComponentInParent<IDamageable>();

                    if (target == null)
                    {
                        // World geometry. Nothing penetrates walls.
                        endPoint = hit.point;
                        Register(hit, ref connected, ref firstImpact);
                        break;
                    }

                    // A body mid-death collapse is scenery, not a target - pass through it
                    // without spending a pierce on it.
                    if (!target.IsAlive) continue;

                    Register(hit, ref connected, ref firstImpact);

                    target.TakeDamage(new DamageInfo(damage, hit.point, hit.normal,
                                                     definition.KnockbackMultiplier, gameObject));
                    bodiesHit++;

                    if (bodiesHit > definition.PierceCount)
                    {
                        endPoint = hit.point;
                        break;
                    }

                    damage *= definition.PenetrationFalloff;
                }
            }

            // Drawn from the barrel even though the hit test starts at the body, so the shot
            // still looks like it came out of the gun. Pooled, so every pellet gets its own
            // line and the spread is actually visible.
            TracerPool.Instance?.Draw(muzzle.position, endPoint,
                                      definition.TracerWidth, definition.TracerDuration);

            return connected;
        }

        static void Register(in RaycastHit hit, ref bool connected, ref Vector3 firstImpact)
        {
            ImpactEffects.Instance?.PlayImpact(hit.point, hit.normal);

            if (connected) return;
            connected = true;
            firstImpact = hit.point;
        }

        /// <summary>
        /// Spread is deliberately horizontal-dominant. This is a top-down game: a fan across
        /// the screen plane is both what the player can see and what actually crosses a
        /// zombie's silhouette. Equal vertical spread would throw pellets clean over their
        /// heads - at 7 degrees over 30 units that is nearly 4 units of vertical drift
        /// against a body less than 2 units tall.
        /// </summary>
        const float VerticalSpreadScale = 0.15f;

        Vector3 ApplySpread(Vector3 forward)
        {
            float spread = definition.Spread;
            if (spread <= 0f) return forward;

            float yaw = UnityEngine.Random.Range(-spread, spread);
            float pitch = UnityEngine.Random.Range(-spread, spread) * VerticalSpreadScale;

            return Quaternion.Euler(pitch, yaw, 0f) * forward;
        }

        /// <summary>Restores a banked magazine, used when swapping back to a weapon.</summary>
        public void SetAmmo(int amount)
        {
            if (definition == null) return;

            Ammo = Mathf.Clamp(amount, 0, definition.MagazineSize);
            AmmoChanged?.Invoke(Ammo, definition.MagazineSize);
        }

        public void BeginReload()
        {
            if (definition == null || IsReloading || Ammo == definition.MagazineSize) return;
            reloadRoutine = StartCoroutine(ReloadRoutine());
        }

        void CancelReload()
        {
            if (reloadRoutine == null) return;

            StopCoroutine(reloadRoutine);
            reloadRoutine = null;
        }

        sealed class HitDistanceComparer : IComparer<RaycastHit>
        {
            public static readonly HitDistanceComparer Instance = new();
            public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
        }

        IEnumerator ReloadRoutine()
        {
            yield return new WaitForSeconds(definition.ReloadTime);

            Ammo = definition.MagazineSize;
            AmmoChanged?.Invoke(Ammo, definition.MagazineSize);
            reloadRoutine = null;
        }
    }
}

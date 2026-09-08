using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// Runtime for a firearm. Holds no stats of its own: it executes a
    /// <see cref="WeaponDefinition"/> against scene wiring and active mod cores
    /// from <see cref="ArmoryManager"/>, drawing ammo from <see cref="WeaponLoadout"/>.
    /// </summary>
    public class Weapon : MonoBehaviour
    {
        [Header("Definition")]
        [SerializeField] WeaponDefinition definition;

        [Header("Scene wiring")]
        [Tooltip("Where the hit test starts. Defaults to this object, which sits at the " +
                 "player's centre. Must NOT be the muzzle.")]
        [SerializeField] Transform rayOrigin;
        [SerializeField] Transform muzzle;
        [SerializeField] MuzzleFlash muzzleFlash;
        [SerializeField] ParticleSystem shellEject;
        [SerializeField] LayerMask hitMask = ~0;

        static readonly RaycastHit[] HitBuffer = new RaycastHit[24];

        int slotIndex;
        float nextFireTime;
        Coroutine reloadRoutine;
        Health ownerHealth;
        WeaponLoadout loadout;

        public WeaponDefinition Definition => definition;
        public int MagazineSize => ArmoryManager.EffectiveMagazineSize(definition);

        public int Ammo { get; private set; }
        public bool IsReloading => reloadRoutine != null;

        /// <summary>(ammo, magazineSize)</summary>
        public event Action<int, int> AmmoChanged;

        void Awake()
        {
            if (muzzle == null) muzzle = transform;
            ownerHealth = GetComponentInParent<Health>();
            loadout = GetComponent<WeaponLoadout>();

            Equip(definition, 0);
        }

        void Start()
        {
            AmmoChanged?.Invoke(Ammo, MagazineSize);
        }

        public void Equip(WeaponDefinition next, int slot = 0)
        {
            definition = next;
            slotIndex = slot;

            if (definition == null)
            {
                Debug.LogError($"{nameof(Weapon)}: no {nameof(WeaponDefinition)} assigned - " +
                               "this weapon cannot fire.", this);
                return;
            }

            CancelReload();
            Ammo = MagazineSize;
            nextFireTime = 0f;

            AmmoChanged?.Invoke(Ammo, MagazineSize);
        }

        void Update()
        {
            if (Time.timeScale <= 0f) return;
            if (definition == null) return;
            if (GameManager.Instance != null && GameManager.Instance.State != GameState.Playing) return;
            if (ownerHealth != null && !ownerHealth.IsAlive) return;

            if (InputReader.ReloadPressed) BeginReload();

            FireMode effectiveMode = definition.Mode;
            if (slotIndex == 0 && ArmoryManager.Instance != null && ArmoryManager.Instance.HasMod(ModCoreType.OverclockedReceiver))
                effectiveMode = FireMode.Automatic;

            bool pullingTrigger = effectiveMode == FireMode.Automatic
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

            float fireRate = definition.FireRate;
            if (ArmoryManager.Instance != null && ArmoryManager.Instance.HasMod(ModCoreType.OverclockedReceiver))
            {
                if (slotIndex == 0) fireRate *= 1.5f;       // Pistol
                else if (slotIndex == 2) fireRate *= 1.35f;  // Assault Rifle
            }

            nextFireTime = Time.time + (60f / Mathf.Max(1f, fireRate));
            Ammo--;
            AmmoChanged?.Invoke(Ammo, MagazineSize);

            if (muzzleFlash != null) muzzleFlash.Play();
            SfxPlayer.Instance?.PlayFlat(definition.FireClip, definition.FireVolume);
            CameraShake.Instance?.AddTrauma(definition.FireTrauma);
            CameraShake.Instance?.AddRecoil(-muzzle.forward, definition.RecoilKick);

            if (shellEject != null && definition.ShellsPerShot > 0)
                shellEject.Emit(definition.ShellsPerShot);

            bool isShotgun = slotIndex == 1;
            bool heavySlug = isShotgun && ArmoryManager.Instance != null && ArmoryManager.Instance.HasMod(ModCoreType.HeavySlug);
            bool dragonsBreath = isShotgun && ArmoryManager.Instance != null && ArmoryManager.Instance.HasMod(ModCoreType.DragonsBreath);

            int pellets = heavySlug ? 1 : definition.PelletsPerShot;
            bool anyHit = false;
            var impactPoint = Vector3.zero;

            for (int i = 0; i < pellets; i++)
            {
                if (FireOnePellet(heavySlug, dragonsBreath, out var point) && !anyHit)
                {
                    anyHit = true;
                    impactPoint = point;
                }
            }

            if (anyHit)
                SfxPlayer.Instance?.PlayAt(definition.ImpactClip, impactPoint, definition.ImpactVolume);

            if (Ammo == 0) BeginReload();
        }

        bool FireOnePellet(bool heavySlug, bool dragonsBreath, out Vector3 firstImpact)
        {
            firstImpact = Vector3.zero;

            float spreadAngle = heavySlug ? 0.3f : definition.Spread;
            var direction = ApplySpread(muzzle.forward, spreadAngle);
            var origin = rayOrigin != null ? rayOrigin.position : transform.position;
            float range = definition.Range;
            var endPoint = origin + direction * range;

            int count = Physics.RaycastNonAlloc(origin, direction, HitBuffer, range, hitMask,
                                                QueryTriggerInteraction.Ignore);
            bool connected = false;

            if (count > 0)
            {
                Array.Sort(HitBuffer, 0, count, HitDistanceComparer.Instance);

                float damage = heavySlug ? 120f : definition.Damage;
                float knockback = heavySlug ? 3.5f : definition.KnockbackMultiplier;

                bool bore = (slotIndex == 2 || slotIndex == 3) && ArmoryManager.Instance != null && ArmoryManager.Instance.HasMod(ModCoreType.BorePiercing);
                int pierceLimit = definition.PierceCount + (bore ? 2 : 0);
                float falloff = bore ? 1.0f : definition.PenetrationFalloff;

                int bodiesHit = 0;

                for (int i = 0; i < count; i++)
                {
                    var hit = HitBuffer[i];
                    var target = hit.collider.GetComponentInParent<IDamageable>();

                    if (target == null)
                    {
                        endPoint = hit.point;
                        Register(hit, ref connected, ref firstImpact);
                        break;
                    }

                    if (!target.IsAlive) continue;

                    Register(hit, ref connected, ref firstImpact);
                    target.TakeDamage(new DamageInfo(damage, hit.point, hit.normal, knockback, gameObject));

                    if (dragonsBreath)
                        StartCoroutine(ApplyBurnDoT(target));

                    bodiesHit++;
                    if (bodiesHit > pierceLimit)
                    {
                        endPoint = hit.point;
                        break;
                    }

                    damage *= falloff;
                }
            }

            TracerPool.Instance?.Draw(muzzle.position, endPoint,
                                      definition.TracerWidth, definition.TracerDuration);

            return connected;
        }

        IEnumerator ApplyBurnDoT(IDamageable target)
        {
            for (int i = 0; i < 3; i++)
            {
                yield return new WaitForSeconds(0.4f);
                if (target != null && target.IsAlive)
                {
                    target.TakeDamage(new DamageInfo(8f, transform.position, Vector3.up, 0.2f, gameObject));
                }
            }
        }

        static void Register(in RaycastHit hit, ref bool connected, ref Vector3 firstImpact)
        {
            ImpactEffects.Instance?.PlayImpact(hit.point, hit.normal);

            if (connected) return;
            connected = true;
            firstImpact = hit.point;
        }

        const float VerticalSpreadScale = 0.15f;

        Vector3 ApplySpread(Vector3 forward, float spread)
        {
            if (spread <= 0f) return forward;

            float yaw = UnityEngine.Random.Range(-spread, spread);
            float pitch = UnityEngine.Random.Range(-spread, spread) * VerticalSpreadScale;

            return Quaternion.Euler(pitch, yaw, 0f) * forward;
        }

        public void SetAmmo(int amount)
        {
            if (definition == null) return;

            Ammo = Mathf.Clamp(amount, 0, MagazineSize);
            AmmoChanged?.Invoke(Ammo, MagazineSize);
        }

        public void BeginReload()
        {
            if (definition == null || IsReloading || Ammo >= MagazineSize) return;
            if (loadout != null && definition.MaxReserveAmmo >= 0 && loadout.CurrentReserveAmmo <= 0) return;

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

            int needed = MagazineSize - Ammo;
            int loaded = needed;

            if (loadout != null && definition.MaxReserveAmmo >= 0)
                loaded = loadout.ConsumeReserve(slotIndex, needed);

            Ammo += loaded;
            AmmoChanged?.Invoke(Ammo, MagazineSize);
            reloadRoutine = null;
        }
    }
}

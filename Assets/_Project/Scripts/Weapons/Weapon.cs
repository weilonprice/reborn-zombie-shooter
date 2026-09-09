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

        [Header("Akimbo rig")]
        [Tooltip("The main gun mesh. Slides to one side when a second pistol is drawn.")]
        [SerializeField] Transform mainGunVisual;
        [Tooltip("Second gun mesh, hidden until the Akimbo tier is bought.")]
        [SerializeField] GameObject offHandGun;
        [SerializeField] Transform offHandMuzzle;
        [SerializeField] MuzzleFlash offHandMuzzleFlash;

        static readonly RaycastHit[] HitBuffer = new RaycastHit[24];

        /// <summary>How far Split Focus will look for a target of its own.</summary>
        const float OffHandRange = 26f;
        const float AkimboOffset = 0.30f;

        const float DoubleTapDelay = 0.08f;
        /// <summary>How long the trigger must be held before a click becomes a fan.</summary>
        const float FanEngageDelay = 0.2f;
        const float FanCoolSeconds = 0.4f;
        const float UltimateRange = 22f;

        int slotIndex;
        float nextFireTime;
        Coroutine reloadRoutine;
        Health ownerHealth;
        WeaponStats stats;
        WeaponLoadout loadout;

        bool useOffHand;

        bool doubleTapPending;
        float doubleTapAt;
        float triggerHeldSince;
        bool fanEngaged;
        float fanHeat;
        bool pendingReloadCrit;

        // Reused every shot so target seeking allocates nothing in the firing path.
        readonly List<Health> offHandTargets = new();
        readonly List<Health> excludeScratch = new();

        bool subscribedToUpgrades;

        public WeaponDefinition Definition => definition;
        public int MagazineSize => ArmoryManager.EffectiveMagazineSize(definition);

        public int Ammo { get; private set; }
        public bool IsReloading => reloadRoutine != null;

        /// <summary>(ammo, magazineSize)</summary>
        public event Action<int, int> AmmoChanged;

        /// <summary>Raised whenever a shot from this weapon kills something.</summary>
        public event Action Killed;
        public event Action UltimateEnded;

        public bool UltimateActive { get; private set; }
        public bool HasUltimate => stats.UltimateKills > 0;
        public int UltimateKillsRequired => stats.UltimateKills;

        void Awake()
        {
            if (muzzle == null) muzzle = transform;
            ownerHealth = GetComponentInParent<Health>();
            loadout = GetComponent<WeaponLoadout>();

            Equip(definition, 0);
        }

        void Start()
        {
            // Awake order across GameObjects is undefined, so UpgradeManager may not have
            // existed when this component enabled. Without this second attempt a weapon that
            // lost the race would never hear about a purchase - the gold would leave and
            // nothing on screen would change.
            SubscribeToUpgrades();
            RefreshStats();

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

            EndUltimate();
            RefreshStats();

            CancelReload();
            Ammo = MagazineSize;
            nextFireTime = 0f;

            AmmoChanged?.Invoke(Ammo, MagazineSize);
        }

        /// <summary>Recomputed on equip and whenever a tier is bought, never per shot.</summary>
        void RefreshStats()
        {
            stats = UpgradeManager.Resolve(definition);
            ApplyAkimboVisuals();
        }

        /// <summary>
        /// Shows or hides the second pistol and spreads the pair apart. Both guns keep the
        /// player's forward, so only their origins move - aim and hit tests are untouched.
        /// </summary>
        void ApplyAkimboVisuals()
        {
            bool dual = stats.DualWield;

            if (offHandGun != null) offHandGun.SetActive(dual);
            if (!dual) useOffHand = false;

            float offset = dual ? AkimboOffset : 0f;
            SlideSideways(mainGunVisual, offset);
            SlideSideways(muzzle != null && muzzle != transform ? muzzle : null, offset);
            SlideSideways(offHandMuzzle, -AkimboOffset);
        }

        static void SlideSideways(Transform target, float x)
        {
            if (target == null) return;

            var local = target.localPosition;
            target.localPosition = new Vector3(x, local.y, local.z);
        }

        void OnEnable() => SubscribeToUpgrades();

        void OnDisable()
        {
            if (!subscribedToUpgrades || UpgradeManager.Instance == null) return;

            UpgradeManager.Instance.Changed -= RefreshStats;
            subscribedToUpgrades = false;
        }

        void SubscribeToUpgrades()
        {
            if (subscribedToUpgrades || UpgradeManager.Instance == null) return;

            UpgradeManager.Instance.Changed += RefreshStats;
            subscribedToUpgrades = true;
        }

        void Update()
        {
            if (Time.timeScale <= 0f) return;
            if (definition == null) return;
            if (GameManager.Instance != null && GameManager.Instance.State != GameState.Playing) return;
            if (ownerHealth != null && !ownerHealth.IsAlive) return;

            UpdateFanHeat();

            if (UltimateActive)
            {
                // The ability owns the trigger while it runs, and the magazine is its timer.
                if (Ammo <= 0) EndUltimate();
                else Fire();
                return;
            }

            if (InputReader.ReloadPressed) BeginReload();

            if (doubleTapPending && Time.time >= doubleTapAt)
            {
                doubleTapPending = false;
                Fire(force: true);
            }

            bool overclockedAuto = definition.OverclockedConvertsToAuto
                && ArmoryManager.ModAppliesTo(ModCoreType.OverclockedReceiver, definition);
            bool fanning = stats.FanFireRate > 0f;

            if (InputReader.FirePressed)
            {
                triggerHeldSince = Time.time;
                fanEngaged = false;

                if (Fire() && stats.DoubleTap)
                {
                    doubleTapPending = true;
                    doubleTapAt = Time.time + DoubleTapDelay;
                }
            }
            else if (InputReader.FireHeld)
            {
                if (fanning)
                {
                    // A click stays a clean double tap; holding past the delay is the fan.
                    // Without it every tap would also start the fan and the accurate shot
                    // this path is built around would not exist.
                    if (Time.time - triggerHeldSince >= FanEngageDelay)
                    {
                        fanEngaged = true;
                        Fire();
                    }
                }
                else if (stats.FullAuto || overclockedAuto)
                {
                    Fire();
                }
            }
            else
            {
                fanEngaged = false;
            }
        }

        /// <summary>Accuracy bleeds away while the fan runs and recovers when it stops.</summary>
        void UpdateFanHeat()
        {
            float toward = fanEngaged ? 1f : 0f;
            float seconds = fanEngaged ? Mathf.Max(0.05f, stats.FanSpreadRamp) : FanCoolSeconds;

            fanHeat = Mathf.MoveTowards(fanHeat, toward, Time.deltaTime / seconds);
        }

        float CurrentFireRate()
        {
            float rate = stats.FireRate;

            if (UltimateActive && stats.UltimateFireRate > 0f) rate = stats.UltimateFireRate;
            else if (fanEngaged && stats.FanFireRate > 0f) rate = stats.FanFireRate;

            if (ArmoryManager.ModAppliesTo(ModCoreType.OverclockedReceiver, definition))
                rate *= definition.OverclockedFireRateMultiplier;

            return rate;
        }

        float CurrentSpread()
        {
            float spread = definition.Spread;

            return stats.FanMaxSpread > spread && fanHeat > 0f
                ? Mathf.Lerp(spread, stats.FanMaxSpread, fanHeat)
                : spread;
        }

        /// <summary>
        /// The guaranteed post-reload crit is consumed here rather than rolled, so it cannot
        /// be lost to a random roll that would have crit anyway.
        /// </summary>
        bool RollCrit()
        {
            if (pendingReloadCrit)
            {
                pendingReloadCrit = false;
                return true;
            }

            float chance = UltimateActive && stats.UltimateCritChance > 0f
                ? stats.UltimateCritChance
                : stats.CritChance;

            return chance > 0f && UnityEngine.Random.value < chance;
        }

        // ------------------------------------------------------------- ultimate

        public void StartUltimate()
        {
            if (UltimateActive || definition == null || !HasUltimate) return;

            CancelReload();
            UltimateActive = true;

            // The flourish reload the ability opens with. It does not touch the reserve:
            // from here the magazine is the ability's duration, not an ammo cost.
            Ammo = MagazineSize;
            AmmoChanged?.Invoke(Ammo, MagazineSize);

            nextFireTime = 0f;
            fanEngaged = false;
            doubleTapPending = false;
            pendingReloadCrit = stats.GuaranteedCritAfterReload;
        }

        public void EndUltimate()
        {
            if (!UltimateActive) return;

            UltimateActive = false;
            UltimateEnded?.Invoke();
        }

        /// <summary>Kept for external callers; the input path uses <see cref="Fire"/>.</summary>
        public void TryFire() => Fire();

        /// <summary>
        /// Fires one round. <paramref name="force"/> skips the rate limiter, which the second
        /// half of a double tap needs - it lands faster than the weapon's own cycle allows.
        /// </summary>
        bool Fire(bool force = false)
        {
            if (definition == null || IsReloading) return false;
            if (!force && Time.time < nextFireTime) return false;

            if (Ammo <= 0)
            {
                // During the ultimate an empty magazine ends the ability rather than
                // starting a reload, so Update handles it instead.
                if (!UltimateActive) BeginReload();
                return false;
            }

            nextFireTime = Time.time + (60f / Mathf.Max(1f, CurrentFireRate()));
            Ammo--;
            AmmoChanged?.Invoke(Ammo, MagazineSize);

            // Akimbo alternates hands. Until Split Focus is bought the off-hand is only a
            // second origin - the shot still goes wherever you are aiming.
            bool offHand = stats.DualWield && useOffHand;
            if (stats.DualWield) useOffHand = !useOffHand;

            var firingMuzzle = offHand && offHandMuzzle != null ? offHandMuzzle : muzzle;
            var flash = offHand && offHandMuzzleFlash != null ? offHandMuzzleFlash : muzzleFlash;

            bool crit = RollCrit();

            if (flash != null) flash.Play();
            SfxPlayer.Instance?.PlayFlat(definition.FireClip, definition.FireVolume);
            CameraShake.Instance?.AddTrauma(definition.FireTrauma + (crit ? 0.09f : 0f));
            CameraShake.Instance?.AddRecoil(-firingMuzzle.forward, definition.RecoilKick);

            if (shellEject != null && definition.ShellsPerShot > 0)
                shellEject.Emit(definition.ShellsPerShot);

            bool heavySlug = ArmoryManager.ModAppliesTo(ModCoreType.HeavySlug, definition);
            bool dragonsBreath = ArmoryManager.ModAppliesTo(ModCoreType.DragonsBreath, definition);

            var origin = rayOrigin != null ? rayOrigin.position : transform.position;
            bool anyHit = false;
            var impactPoint = Vector3.zero;

            if (UltimateActive)
            {
                // Aim is gone for the duration, so the guns pick the nearest body themselves.
                TargetFinder.Gather(origin, UltimateRange, 1, null, offHandTargets);

                var direction = offHandTargets.Count > 0
                    ? (TargetFinder.AimPoint(offHandTargets[0]) - origin).normalized
                    : firingMuzzle.forward;

                // A round is spent either way: the spin is the cost, not a free search.
                anyHit = FireOnePellet(heavySlug, dragonsBreath, crit, origin, direction,
                                       firingMuzzle.position, out impactPoint);
            }
            else if (offHand && stats.OffHandTargets > 0 && GatherOffHandTargets(origin))
            {
                // Split Focus: one trigger pull, several bodies, none of them under the
                // crosshair. Deliberately still one round - the capstone is meant to feel
                // generous, and metering ammo per extra target would just make it fiddly.
                for (int i = 0; i < offHandTargets.Count; i++)
                {
                    var direction = (TargetFinder.AimPoint(offHandTargets[i]) - origin).normalized;

                    if (FireOnePellet(heavySlug, dragonsBreath, crit, origin, direction,
                                      firingMuzzle.position, out var point) && !anyHit)
                    {
                        anyHit = true;
                        impactPoint = point;
                    }
                }
            }
            else
            {
                int pellets = heavySlug ? 1 : definition.PelletsPerShot;
                float spreadAngle = heavySlug ? 0.3f : CurrentSpread();

                for (int i = 0; i < pellets; i++)
                {
                    var direction = ApplySpread(firingMuzzle.forward, spreadAngle);

                    if (FireOnePellet(heavySlug, dragonsBreath, crit, origin, direction,
                                      firingMuzzle.position, out var point) && !anyHit)
                    {
                        anyHit = true;
                        impactPoint = point;
                    }
                }
            }

            if (anyHit)
                SfxPlayer.Instance?.PlayAt(definition.ImpactClip, impactPoint, definition.ImpactVolume);

            if (Ammo == 0 && !UltimateActive) BeginReload();
            return true;
        }

        /// <summary>Picks the off-hand's own targets, skipping whatever you are aiming at.</summary>
        bool GatherOffHandTargets(Vector3 origin)
        {
            excludeScratch.Clear();

            var aimed = AimedTarget(origin);
            if (aimed != null) excludeScratch.Add(aimed);

            TargetFinder.Gather(origin, OffHandRange, stats.OffHandTargets,
                                excludeScratch, offHandTargets);

            return offHandTargets.Count > 0;
        }

        /// <summary>
        /// Whatever is under the crosshair right now. Costs one ray per off-hand shot, which
        /// buys the whole point of Split Focus: the off-hand must pick something you are
        /// <em>not</em> already killing.
        /// </summary>
        Health AimedTarget(Vector3 origin)
        {
            int count = Physics.RaycastNonAlloc(origin, muzzle.forward, HitBuffer,
                                                definition.Range, hitMask,
                                                QueryTriggerInteraction.Ignore);

            Health closest = null;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                var health = HitBuffer[i].collider.GetComponentInParent<Health>();

                if (health == null || health == ownerHealth || !health.IsAlive) continue;
                if (HitBuffer[i].distance >= bestDistance) continue;

                bestDistance = HitBuffer[i].distance;
                closest = health;
            }

            return closest;
        }

        bool FireOnePellet(bool heavySlug, bool dragonsBreath, bool crit, Vector3 origin,
                           Vector3 direction, Vector3 tracerFrom, out Vector3 firstImpact)
        {
            firstImpact = Vector3.zero;

            float range = definition.Range;
            var endPoint = origin + direction * range;

            int count = Physics.RaycastNonAlloc(origin, direction, HitBuffer, range, hitMask,
                                                QueryTriggerInteraction.Ignore);
            bool connected = false;

            if (count > 0)
            {
                Array.Sort(HitBuffer, 0, count, HitDistanceComparer.Instance);

                float damage = heavySlug ? 120f : stats.Damage;
                if (crit) damage *= stats.CritMultiplier;

                float knockback = heavySlug ? 3.5f : stats.KnockbackMultiplier;

                bool bore = ArmoryManager.ModAppliesTo(ModCoreType.BorePiercing, definition);
                int pierceLimit = stats.PierceCount + (bore ? 2 : 0);
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

                    var health = hit.collider.GetComponentInParent<Health>();
                    ApplyShot(target, health, hit.point, hit.normal, damage, knockback);

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

            // A crit that looks identical to a normal shot may as well not exist, and there
            // is no crit sound in the project yet - so the tracer carries it for now.
            TracerPool.Instance?.Draw(tracerFrom, endPoint,
                                      crit ? definition.TracerWidth * 2.4f : definition.TracerWidth,
                                      crit ? definition.TracerDuration * 1.6f : definition.TracerDuration);

            return connected;
        }

        /// <summary>
        /// Applies one hit and the kill credit that hangs off it. Returns whether this hit
        /// killed the target.
        /// </summary>
        bool ApplyShot(IDamageable target, Health health, Vector3 point, Vector3 normal,
                       float damage, float knockback)
        {
            if (target == null || !target.IsAlive) return false;

            target.TakeDamage(new DamageInfo(damage, point, normal, knockback,
                                             gameObject, definition));

            bool killed = !target.IsAlive;

            // Checked here rather than through an event: this shot caused the kill, so the
            // weapon that earned the refund is unambiguous.
            if (killed) OnKill();

            return killed;
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

        void OnKill()
        {
            if (stats.KillsReloadMagazine && Ammo < MagazineSize)
            {
                Ammo++;
                AmmoChanged?.Invoke(Ammo, MagazineSize);
            }

            if (stats.ReserveRefundPerKill > 0 && loadout != null)
                loadout.AddReserve(slotIndex, stats.ReserveRefundPerKill);

            Killed?.Invoke();
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
            if (loadout != null && definition.MaxReserveAmmo >= 0 &&
                !stats.InfiniteReserve && loadout.CurrentReserveAmmo <= 0) return;

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
            yield return new WaitForSeconds(stats.ReloadTime);

            int needed = MagazineSize - Ammo;
            int loaded = needed;

            if (loadout != null && definition.MaxReserveAmmo >= 0 && !stats.InfiniteReserve)
                loaded = loadout.ConsumeReserve(slotIndex, needed);

            Ammo += loaded;
            AmmoChanged?.Invoke(Ammo, MagazineSize);
            reloadRoutine = null;

            // Quick Draw. Armed only on a reload that actually loaded something, so dry
            // firing an empty reserve cannot hand out a free crit.
            if (stats.GuaranteedCritAfterReload && loaded > 0) pendingReloadCrit = true;
        }
    }
}

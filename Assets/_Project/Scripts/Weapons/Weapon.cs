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

        // Weapons authored before deliveries existed, and every plain firearm since, leave
        // the field empty rather than each needing an identical asset.
        static WeaponDelivery defaultDelivery;
        static WeaponDelivery DefaultDelivery =>
            defaultDelivery != null
                ? defaultDelivery
                : defaultDelivery = ScriptableObject.CreateInstance<HitscanDelivery>();

        /// <summary>How far Split Focus will look for a target of its own.</summary>
        const float OffHandRange = 26f;
        const float AkimboOffset = 0.30f;

        /// <summary>How long the trigger must be held before a click becomes a fan.</summary>
        const float FanEngageDelay = 0.2f;
        const float FanCoolSeconds = 0.4f;
        const float UltimateRange = 22f;
        /// <summary>How long a focus stack survives without a hit landing on that target.</summary>
        const float FocusMemorySeconds = 1.5f;

        int slotIndex;
        float nextFireTime;
        Coroutine reloadRoutine;
        Health ownerHealth;
        WeaponStats stats;
        WeaponLoadout loadout;

        bool useOffHand;

        float triggerHeldSince;
        bool fanEngaged;
        float triggerHeat;
        bool pendingReloadCrit;

        // Marksman focus: consecutive hits on one body. Held as the Health rather than an id
        // so a pooled enemy that despawns and returns cannot inherit the previous one's stack.
        Health focusTarget;
        int focusStacks;
        float focusExpiresAt;

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

        /// <summary>
        /// Whether the reserve is currently bottomless. Read by the HUD: infinite reserve
        /// arrives as a resolved upgrade stat, not as a negative MaxReserveAmmo on the
        /// definition, so the reserve counter simply stops moving rather than going below
        /// zero - and nothing downstream could tell the difference from a full one.
        /// </summary>
        public bool HasInfiniteReserve => stats.InfiniteReserve;

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

            UpdateTriggerHeat();

            if (UltimateActive)
            {
                // The ability owns the trigger while it runs, and the magazine is its timer.
                if (Ammo <= 0) EndUltimate();
                else Fire();
                return;
            }

            if (InputReader.ReloadPressed) BeginReload();

            bool fanning = stats.FanFireRate > 0f;

            if (InputReader.FirePressed)
            {
                triggerHeldSince = Time.time;
                fanEngaged = false;
                Fire();
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
                else if (stats.FullAuto)
                {
                    Fire();
                }
            }
            else
            {
                fanEngaged = false;
            }
        }

        /// <summary>
        /// How long the trigger has been held, normalised. One value drives both directions:
        /// the fan uses it to lose accuracy, the marksman rifle uses it to gain accuracy.
        /// </summary>
        void UpdateTriggerHeat()
        {
            bool holding = fanEngaged || (stats.FullAuto && InputReader.FireHeld);
            float toward = holding ? 1f : 0f;
            float seconds = holding ? Mathf.Max(0.05f, stats.FanSpreadRamp) : FanCoolSeconds;

            triggerHeat = Mathf.MoveTowards(triggerHeat, toward, Time.deltaTime / seconds);
        }

        float CurrentFireRate()
        {
            if (UltimateActive && stats.UltimateFireRate > 0f) return stats.UltimateFireRate;
            if (fanEngaged && stats.FanFireRate > 0f) return stats.FanFireRate;

            return stats.FireRate;
        }

        float CurrentSpread()
        {
            float basis = stats.Spread >= 0f ? stats.Spread : definition.Spread;

            // Fan the hammer widens as you hold; the marksman barrel narrows. A weapon can
            // only do one, and the fan wins if something ever sets both.
            if (stats.FanMaxSpread > basis) return Mathf.Lerp(basis, stats.FanMaxSpread, triggerHeat);

            if (stats.SustainedSpreadMin >= 0f && stats.SustainedSpreadMin < basis)
                return Mathf.Lerp(basis, stats.SustainedSpreadMin, triggerHeat);

            return basis;
        }

        /// <summary>
        /// Damage multiplier from staying on one target.
        /// <para>
        /// Counted per HIT, not per shot, which is only correct while the weapons that have
        /// focus fire one pellet. Give a multi-pellet weapon this and a single trigger pull
        /// would stack it once per pellet - split the counting out of here if that day comes.
        /// </para>
        /// </summary>
        float FocusMultiplier(Health target)
        {
            if (stats.FocusBonusPerHit <= 0f || target == null) return 1f;

            if (target != focusTarget || Time.time > focusExpiresAt)
            {
                focusTarget = target;
                focusStacks = 0;
            }

            focusStacks++;
            focusExpiresAt = Time.time + FocusMemorySeconds;

            return 1f + Mathf.Min(focusStacks * stats.FocusBonusPerHit, stats.FocusMaxBonus);
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
            focusStacks = 0;
            focusTarget = null;
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

        bool Fire()
        {
            if (definition == null || IsReloading) return false;
            if (Time.time < nextFireTime) return false;

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

            var origin = rayOrigin != null ? rayOrigin.position : transform.position;
            var delivery = definition.Delivery != null ? definition.Delivery : DefaultDelivery;

            bool anyHit = false;
            var impactPoint = Vector3.zero;

            if (UltimateActive)
            {
                // Aim is gone for the duration, so the guns pick the nearest body themselves.
                TargetFinder.Gather(origin, UltimateRange, 1, null, offHandTargets);

                var direction = offHandTargets.Count > 0
                    ? (TargetFinder.AimPoint(offHandTargets[0]) - origin).normalized
                    : firingMuzzle.forward;

                // A round is spent either way: the spin is the cost, not a free search. No
                // spread, because an auto-aimed shot that misses reads as a bug.
                anyHit = delivery.Deliver(
                    Shot(origin, direction, firingMuzzle.position, 0f, crit), out impactPoint);
            }
            else if (offHand && stats.OffHandTargets > 0 && GatherOffHandTargets(origin))
            {
                // Split Focus: one trigger pull, several bodies, none of them under the
                // crosshair. Deliberately still one round - the capstone is meant to feel
                // generous, and metering ammo per extra target would just make it fiddly.
                for (int i = 0; i < offHandTargets.Count; i++)
                {
                    var direction = (TargetFinder.AimPoint(offHandTargets[i]) - origin).normalized;

                    if (delivery.Deliver(Shot(origin, direction, firingMuzzle.position, 0f, crit),
                                         out var point) && !anyHit)
                    {
                        anyHit = true;
                        impactPoint = point;
                    }
                }
            }
            else
            {
                anyHit = delivery.Deliver(
                    Shot(origin, firingMuzzle.forward, firingMuzzle.position, CurrentSpread(), crit),
                    out impactPoint);
            }

            if (anyHit)
                SfxPlayer.Instance?.PlayAt(definition.ImpactClip, impactPoint, definition.ImpactVolume);

            if (Ammo == 0 && !UltimateActive) BeginReload();
            return true;
        }

        ShotContext Shot(Vector3 origin, Vector3 direction, Vector3 muzzlePosition,
                         float spread, bool crit) =>
            new(this, definition, stats, origin, direction, muzzlePosition, spread, crit, hitMask);

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

        /// <summary>
        /// Applies one hit and everything the weapon owns about it - focus stacking,
        /// executions and kill credit. Public because deliveries land the hits; keeping this
        /// here is what stops every new delivery having to reimplement them.
        /// </summary>
        public bool ApplyShot(IDamageable target, Health health, Vector3 point, Vector3 normal,
                              float damage, float knockback)
        {
            if (target == null || !target.IsAlive) return false;

            // Barricades and barrels are damageable too. Executing your own half-dead wall
            // because a pellet clipped it would be an infuriating way to lose one.
            bool enemy = health != null && health.GetComponent<ZombieAI>() != null;

            if (enemy) damage *= FocusMultiplier(health);

            // Under the threshold the shot is simply lethal, whatever it would have rolled.
            if (stats.ExecuteThreshold > 0f && enemy &&
                health.Normalized <= stats.ExecuteThreshold)
            {
                damage = Mathf.Max(damage, health.Current);
                ImpactEffects.Instance?.PlayImpact(point, normal);
            }

            target.TakeDamage(new DamageInfo(damage, point, normal, knockback,
                                             gameObject, definition));

            bool killed = !target.IsAlive;

            // Checked here rather than through an event: this shot caused the kill, so the
            // weapon that earned the refund is unambiguous.
            if (killed) OnKill();

            return killed;
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

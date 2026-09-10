using System.Collections.Generic;
using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// A nail gun bolted to a barricade, firing on its own.
    /// <para>
    /// Sits dormant on every barricade prefab and switches itself on only while a carried
    /// weapon's upgrades grant it. That is the opposite of the placer knowing about upgrades:
    /// a wall is placed the same way whatever the player is holding, and the turret decides
    /// for itself whether it exists.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(Barricade))]
    public class BarricadeTurret : MonoBehaviour
    {
        [SerializeField] float range = 18f;
        [Tooltip("Fraction of the granting weapon's rate of fire.")]
        [SerializeField, Range(0.05f, 1f)] float rateFraction = 0.34f;
        [Tooltip("Fraction of the granting weapon's damage.")]
        [SerializeField, Range(0.05f, 2f)] float damageFraction = 1f;
        [SerializeField] float tracerWidth = 0.05f;
        [SerializeField] float tracerDuration = 0.05f;

        static readonly List<Health> Scratch = new();

        Barricade barricade;
        WeaponLoadout loadout;
        float nextFireTime;

        void Awake() => barricade = GetComponent<Barricade>();

        void Update()
        {
            if (barricade == null || !barricade.IsAlive) return;
            if (GameManager.Instance != null && GameManager.Instance.State != GameState.Playing) return;

            // Timer first. Every standing wall runs this, so resolving ten carried weapons
            // per barricade per frame just to learn it has no turret is a cost paid by
            // players who never bought the tier.
            if (Time.time < nextFireTime) return;

            if (!TryGetGrantingStats(out var stats))
            {
                nextFireTime = Time.time + 0.5f;
                return;
            }

            float rate = Mathf.Max(1f, stats.FireRate * rateFraction);
            nextFireTime = Time.time + 60f / rate;

            var muzzle = transform.position + Vector3.up * 0.9f;
            var target = TargetFinder.Nearest(muzzle, range, null, Scratch);
            if (target == null) return;

            var point = TargetFinder.AimPoint(target);
            var normal = (muzzle - point).sqrMagnitude < 0.0001f
                ? Vector3.up
                : (muzzle - point).normalized;

            target.TakeDamage(new DamageInfo(stats.Damage * damageFraction, point, normal,
                                             stats.KnockbackMultiplier, gameObject));

            TracerPool.Instance?.Draw(muzzle, point, tracerWidth, tracerDuration);
            ImpactEffects.Instance?.PlayImpact(point, normal);
        }

        /// <summary>
        /// Finds a carried weapon whose upgrades grant turrets, and fires that weapon's
        /// numbers. Resolved per shot rather than cached, so buying the tier mid-wave arms
        /// every wall already standing.
        /// </summary>
        bool TryGetGrantingStats(out WeaponStats stats)
        {
            stats = default;

            if (loadout == null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                loadout = player != null ? player.GetComponent<WeaponLoadout>() : null;
                if (loadout == null) return false;
            }

            for (int i = 0; i < loadout.CarryCapacity; i++)
            {
                var definition = loadout.CarriedAt(i);
                if (definition == null) continue;

                var resolved = UpgradeManager.Resolve(definition);
                if (!resolved.GrantsBarricadeTurret) continue;

                stats = resolved;
                return true;
            }

            return false;
        }
    }
}

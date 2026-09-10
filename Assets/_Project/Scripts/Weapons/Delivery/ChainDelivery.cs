using System.Collections.Generic;
using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// Hits one body, then hops to nearby ones, for the Tesla Coil.
    /// <para>
    /// The first link is a real raycast so the shot can be blocked by a wall and so aiming
    /// still matters; every hop after it is a search rather than a cast, because an arc that
    /// refused to bend around a corpse would read as broken rather than as physical.
    /// </para>
    /// </summary>
    [CreateAssetMenu(menuName = "Zombie Shooter/Delivery/Chain", fileName = "DLV_Chain")]
    public class ChainDelivery : WeaponDelivery
    {
        [Tooltip("Hops after the first body.")]
        [SerializeField] int bounces = 3;
        [Tooltip("How far a hop will reach for its next target.")]
        [SerializeField] float hopRange = 8f;
        [Tooltip("Damage multiplier applied per hop. Above 1 the arc grows down the chain.")]
        [SerializeField] float damagePerHop = 0.8f;

        static readonly RaycastHit[] HitBuffer = new RaycastHit[16];
        static readonly List<Health> Visited = new();
        static readonly List<Health> Scratch = new();

        public override bool Deliver(in ShotContext shot, out Vector3 firstImpact)
        {
            firstImpact = Vector3.zero;

            var definition = shot.Definition;
            var direction = shot.SpreadDirection();
            float range = shot.Range;

            // Upgrades grow the chain; the asset only supplies where it starts.
            int hops = shot.Stats.ChainBounces >= 0 ? shot.Stats.ChainBounces : bounces;
            float reach = shot.Stats.ChainHopRange >= 0f ? shot.Stats.ChainHopRange : hopRange;
            float perHop = shot.Stats.ChainDamagePerHop >= 0f
                ? shot.Stats.ChainDamagePerHop
                : damagePerHop;
            var endPoint = shot.Origin + direction * range;

            int count = Physics.RaycastNonAlloc(shot.Origin, direction, HitBuffer, range,
                                                shot.HitMask, QueryTriggerInteraction.Ignore);

            Health seed = null;
            float bestDistance = float.MaxValue;
            bool blocked = false;

            for (int i = 0; i < count; i++)
            {
                if (HitBuffer[i].distance >= bestDistance) continue;

                var health = HitBuffer[i].collider.GetComponentInParent<Health>();
                var damageable = HitBuffer[i].collider.GetComponentInParent<IDamageable>();

                if (damageable == null)
                {
                    // Level geometry. The arc stops here unless something closer was alive.
                    bestDistance = HitBuffer[i].distance;
                    seed = null;
                    endPoint = HitBuffer[i].point;
                    blocked = true;
                    continue;
                }

                if (health == null || !health.IsAlive) continue;

                bestDistance = HitBuffer[i].distance;
                seed = health;
                endPoint = HitBuffer[i].point;
                blocked = false;
            }

            TracerPool.Instance?.Draw(shot.MuzzlePosition, endPoint,
                                      definition.TracerWidth, definition.TracerDuration);

            if (seed == null || blocked) return false;

            firstImpact = endPoint;
            float damage = shot.Damage;

            shot.Weapon.ApplyShot(seed, seed, endPoint, -direction, damage,
                                  shot.Stats.KnockbackMultiplier);

            Visited.Clear();
            Visited.Add(seed);

            var from = endPoint;

            for (int hop = 0; hop < hops; hop++)
            {
                var next = TargetFinder.Nearest(from, reach, Visited, Scratch);
                if (next == null) break;

                damage *= perHop;

                var to = TargetFinder.AimPoint(next);
                var normal = (from - to).sqrMagnitude < 0.0001f
                    ? Vector3.up
                    : (from - to).normalized;

                TracerPool.Instance?.Draw(from, to, definition.TracerWidth,
                                          definition.TracerDuration);
                ImpactEffects.Instance?.PlayImpact(to, normal);

                shot.Weapon.ApplyShot(next, next, to, normal, damage,
                                      shot.Stats.KnockbackMultiplier);

                Visited.Add(next);
                from = to;
            }

            return true;
        }
    }
}

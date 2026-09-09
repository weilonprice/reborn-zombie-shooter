using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// An instant line, optionally split into pellets, that punches through bodies until it
    /// runs out of pierce. This is what the pistol, shotgun, rifle and sniper have always
    /// done; it is a delivery now rather than the only thing Weapon knows how to do.
    /// </summary>
    [CreateAssetMenu(menuName = "Zombie Shooter/Delivery/Hitscan", fileName = "DLV_Hitscan")]
    public class HitscanDelivery : WeaponDelivery
    {
        static readonly RaycastHit[] HitBuffer = new RaycastHit[24];

        public override bool Deliver(in ShotContext shot, out Vector3 firstImpact)
        {
            firstImpact = Vector3.zero;

            bool anyHit = false;
            int pellets = shot.PelletCount;

            for (int i = 0; i < pellets; i++)
            {
                if (FirePellet(shot, shot.SpreadDirection(), out var point) && !anyHit)
                {
                    anyHit = true;
                    firstImpact = point;
                }
            }

            return anyHit;
        }

        static bool FirePellet(in ShotContext shot, Vector3 direction, out Vector3 firstImpact)
        {
            firstImpact = Vector3.zero;

            var definition = shot.Definition;
            float range = definition.Range;
            var endPoint = shot.Origin + direction * range;

            int count = Physics.RaycastNonAlloc(shot.Origin, direction, HitBuffer, range,
                                                shot.HitMask, QueryTriggerInteraction.Ignore);
            bool connected = false;

            if (count > 0)
            {
                Array.Sort(HitBuffer, 0, count, HitDistanceComparer.Instance);

                float damage = shot.Damage;
                float knockback = shot.Stats.KnockbackMultiplier;

                int pierceLimit = shot.Stats.PierceCount;
                float falloff = shot.Stats.PenetrationFalloff >= 0f
                    ? shot.Stats.PenetrationFalloff
                    : definition.PenetrationFalloff;

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
                    shot.Weapon.ApplyShot(target, health, hit.point, hit.normal, damage, knockback);

                    bodiesHit++;
                    if (bodiesHit > pierceLimit)
                    {
                        endPoint = hit.point;
                        break;
                    }

                    // Above 1 this is Overpenetration, and the round grows down the line.
                    damage *= falloff;
                }
            }

            // A crit that looks identical to a normal shot may as well not exist, and there
            // is no crit sound in the project yet - so the tracer carries it.
            TracerPool.Instance?.Draw(shot.MuzzlePosition, endPoint,
                shot.Crit ? definition.TracerWidth * 2.4f : definition.TracerWidth,
                shot.Crit ? definition.TracerDuration * 1.6f : definition.TracerDuration);

            return connected;
        }

        static void Register(in RaycastHit hit, ref bool connected, ref Vector3 firstImpact)
        {
            ImpactEffects.Instance?.PlayImpact(hit.point, hit.normal);

            if (connected) return;
            connected = true;
            firstImpact = hit.point;
        }

        sealed class HitDistanceComparer : IComparer<RaycastHit>
        {
            public static readonly HitDistanceComparer Instance = new();
            public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
        }
    }
}

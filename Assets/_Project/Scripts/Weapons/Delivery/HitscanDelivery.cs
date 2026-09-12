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
        static readonly RaycastHit[] HitBuffer = new RaycastHit[32];

        /// <summary>
        /// Bodies this pellet passed through, nearest first. Reused between shots - a pellet
        /// resolves entirely inside one call, so one buffer for the whole game is enough.
        /// </summary>
        static readonly List<BodyHit> Bodies = new(8);

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
            float range = shot.Range;
            var endPoint = shot.Origin + direction * range;

            // Collide, not Ignore: hit zones are triggers. Nothing else in the project uses
            // a trigger collider, so this widens the ray onto authored limbs and onto
            // nothing else.
            int count = Physics.RaycastNonAlloc(shot.Origin, direction, HitBuffer, range,
                                                shot.HitMask, QueryTriggerInteraction.Collide);
            bool connected = false;

            if (count > 0)
            {
                Array.Sort(HitBuffer, 0, count, HitDistanceComparer.Instance);
                Bodies.Clear();

                // First pass: fold the raw colliders into one entry per body. A zombie is
                // several overlapping volumes - the walking capsule plus its limb zones - and
                // a pellet through the chest clips more than one of them. Damage is owed once
                // per body, at the best rate the pellet earned, so a shot that passes through
                // an arm and then the torso is a torso hit rather than two hits or an arm hit.
                for (int i = 0; i < count; i++)
                {
                    var hit = HitBuffer[i];
                    var target = hit.collider.GetComponentInParent<IDamageable>();

                    if (target == null)
                    {
                        // Level geometry. Nothing behind this is reachable.
                        endPoint = hit.point;
                        Register(hit, ref connected, ref firstImpact);
                        break;
                    }

                    if (!target.IsAlive) continue;

                    float multiplier = Hitbox.MultiplierOf(hit.collider);
                    int existing = IndexOf(target);

                    if (existing >= 0)
                    {
                        var body = Bodies[existing];
                        if (multiplier > body.Multiplier)
                        {
                            body.Multiplier = multiplier;
                            Bodies[existing] = body;
                        }
                        continue;
                    }

                    Bodies.Add(new BodyHit
                    {
                        Target = target,
                        Health = hit.collider.GetComponentInParent<Health>(),
                        Point = hit.point,
                        Normal = hit.normal,
                        Multiplier = multiplier,
                    });
                }

                // Second pass: pay out in the order the pellet met them, so pierce falloff
                // still compounds front to back.
                float damage = shot.Damage;
                float knockback = shot.Stats.KnockbackMultiplier;

                int pierceLimit = shot.Stats.PierceCount;
                float falloff = shot.Stats.PenetrationFalloff >= 0f
                    ? shot.Stats.PenetrationFalloff
                    : definition.PenetrationFalloff;

                for (int i = 0; i < Bodies.Count; i++)
                {
                    var body = Bodies[i];

                    ImpactEffects.Instance?.PlayImpact(body.Point, body.Normal);
                    if (!connected)
                    {
                        connected = true;
                        firstImpact = body.Point;
                    }

                    shot.Weapon.ApplyShot(body.Target, body.Health, body.Point, body.Normal,
                                          damage * body.Multiplier, knockback);

                    if (i >= pierceLimit)
                    {
                        endPoint = body.Point;
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

        static int IndexOf(IDamageable target)
        {
            for (int i = 0; i < Bodies.Count; i++)
                if (ReferenceEquals(Bodies[i].Target, target)) return i;

            return -1;
        }

        static void Register(in RaycastHit hit, ref bool connected, ref Vector3 firstImpact)
        {
            ImpactEffects.Instance?.PlayImpact(hit.point, hit.normal);

            if (connected) return;
            connected = true;
            firstImpact = hit.point;
        }

        struct BodyHit
        {
            public IDamageable Target;
            public Health Health;
            public Vector3 Point;
            public Vector3 Normal;
            public float Multiplier;
        }

        sealed class HitDistanceComparer : IComparer<RaycastHit>
        {
            public static readonly HitDistanceComparer Instance = new();
            public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
        }
    }
}

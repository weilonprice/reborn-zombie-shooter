using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// Plated at the front, soft everywhere else.
    /// <para>
    /// The point is to break the "hold the trigger and back away" pattern that works on
    /// everything else in the game: this one has to be flanked, pierced from behind a line,
    /// or hit with something that does not care where it is facing.
    /// </para>
    /// </summary>
    public class FrontalArmor : MonoBehaviour, IDamageModifier
    {
        [Tooltip("Damage retained by a hit inside the frontal arc.")]
        [SerializeField, Range(0f, 1f)] float frontalMultiplier = 0.25f;
        [Tooltip("Half-angle of the plated arc, in degrees from forward.")]
        [SerializeField] float arcHalfAngle = 70f;

        public float Modify(in DamageInfo info, float amount)
        {
            // Blasts and other sourceless damage carry no useful direction, so they ignore
            // the plate entirely - which is exactly the intended counterplay.
            var toHit = info.Point - transform.position;
            toHit.y = 0f;

            if (toHit.sqrMagnitude < 0.0001f) return amount;

            float angle = Vector3.Angle(transform.forward, toHit.normalized);
            return angle <= arcHalfAngle ? amount * frontalMultiplier : amount;
        }
    }
}

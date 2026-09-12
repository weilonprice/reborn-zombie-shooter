using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// A trigger volume that makes part of a body shootable at its own damage rate.
    /// <para>
    /// Every enemy is hittable through its CharacterController, which is a capsule sized for
    /// walking rather than for being shot at. The authored zombie reaches its arms forward,
    /// so at the height the game fires from - 1.0m, the player's own centre - the mesh is
    /// 0.57m wide to each side while the controller is 0.42m. A quarter of the visible
    /// zombie was not there as far as physics was concerned, which is why hits felt like
    /// they needed the centre of mass.
    /// </para>
    /// <para>
    /// Hitboxes fill that gap without touching movement. They are triggers, and all seventeen
    /// of the project's other physics queries pass QueryTriggerInteraction.Ignore, so nothing
    /// else in the game can see them: not explosions, not the flow field, not spawn probes,
    /// not enemy melee. Only the deliveries that opt in do.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class Hitbox : MonoBehaviour
    {
        [Tooltip("Damage scale for shots that connect here and nowhere better. " +
                 "1 is a clean body hit; limbs sit below that.")]
        [SerializeField, Range(0.1f, 2f)] float damageMultiplier = 1f;

        public float DamageMultiplier => damageMultiplier;

        void Reset()
        {
            var collider = GetComponent<Collider>();
            if (collider != null) collider.isTrigger = true;
        }

        /// <summary>
        /// What this collider is worth. Anything that is not a hitbox - a CharacterController,
        /// a barricade, a barrel - is a full hit, which keeps every body without authored zones
        /// behaving exactly as it did before hitboxes existed.
        /// </summary>
        public static float MultiplierOf(Collider collider)
        {
            if (collider == null) return 1f;
            var hitbox = collider.GetComponent<Hitbox>();
            return hitbox != null ? hitbox.damageMultiplier : 1f;
        }
    }
}

using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// Marks a body whose hit zones are authored, so weapon fire knows it can safely ignore
    /// the movement capsule.
    /// <para>
    /// Without this the rule "never shoot a CharacterController" makes any body that has no
    /// zones - a new archetype, a rig whose bones were renamed, anything built before the
    /// zones existed - silently unhittable. The marker turns that from a bug that looks like
    /// bullets passing through enemies into a body that simply keeps its old behaviour.
    /// </para>
    /// </summary>
    public class HitZoneSet : MonoBehaviour
    {
        /// <summary>
        /// True when this collider belongs to a body that has authored hit zones AND is
        /// willing to be shot through them.
        /// <para>
        /// Checks enabled, not just presence, so unticking this component on a prefab puts
        /// that body straight back on capsule accuracy with no rebuild. That is the switch
        /// to reach for if precise zones ever go wrong mid-session: bullets that connect
        /// roughly beat bullets that do not connect at all.
        /// </para>
        /// </summary>
        public static bool Covers(Collider collider)
        {
            if (collider == null) return false;
            var set = collider.GetComponent<HitZoneSet>();
            return set != null && set.enabled;
        }
    }
}

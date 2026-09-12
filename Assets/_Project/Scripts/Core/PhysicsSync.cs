using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// Pushes pending transform changes into the physics scene, at most once a frame.
    /// <para>
    /// The project runs with <c>Physics.autoSyncTransforms</c> disabled, so moving a Transform
    /// does not move its collider until the next physics step. That is fine for everything
    /// that used to be shot at - a CharacterController syncs itself inside Move - and not fine
    /// at all for hit zones, which are colliders with no Rigidbody hanging off animated bones.
    /// PhysX treats those as static geometry and keeps testing them where they were at the
    /// last FixedUpdate, which is not where the zombie the player is aiming at now is.
    /// </para>
    /// <para>
    /// FlowField already calls SyncTransforms before its bake for the same reason. This just
    /// makes the once-per-frame version shareable: a shotgun resolves eight pellets and should
    /// pay for the sync once.
    /// </para>
    /// </summary>
    public static class PhysicsSync
    {
        static int syncedFrame = -1;

        public static void EnsureThisFrame()
        {
            if (syncedFrame == Time.frameCount) return;

            syncedFrame = Time.frameCount;
            Physics.SyncTransforms();
        }
    }
}

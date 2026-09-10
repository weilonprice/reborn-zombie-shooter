using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// Lets an archetype drive its own movement for a while - a leap, a charge, a burrow.
    /// <para>
    /// Returning true means the enemy has already moved itself this frame and ZombieAI must
    /// not also apply its own steering, separation and gravity. Anything that has to leave
    /// the ground needs this, because the ordinary path pins the controller down every frame.
    /// </para>
    /// </summary>
    public interface IEnemyMotionOverride
    {
        bool MoveSelf(float deltaTime);
    }
}

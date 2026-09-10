using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// Changes incoming damage before it is applied. Implemented by armour and resistances.
    /// <para>
    /// A hook rather than a flag on Health, so an enemy that halves frontal damage and one
    /// that shrugs off fire are two small components rather than two more booleans on a class
    /// every enemy in the game shares.
    /// </para>
    /// </summary>
    public interface IDamageModifier
    {
        float Modify(in DamageInfo info, float amount);
    }
}

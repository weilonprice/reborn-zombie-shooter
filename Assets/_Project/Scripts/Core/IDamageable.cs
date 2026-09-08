namespace ZombieShooter
{
    /// <summary>Anything that can take damage. Implemented by <see cref="Health"/>.</summary>
    public interface IDamageable
    {
        bool IsAlive { get; }
        void TakeDamage(in DamageInfo info);
    }
}

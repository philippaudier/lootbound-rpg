namespace Lootbound.Gameplay.Combat
{
    /// <summary>
    /// Interface for any object that can receive damage.
    /// </summary>
    public interface IDamageable
    {
        /// <summary>
        /// True if this entity is dead and cannot receive more damage.
        /// </summary>
        bool IsDead { get; }

        /// <summary>
        /// Apply damage to this entity.
        /// </summary>
        /// <param name="request">The damage request containing damage info.</param>
        /// <returns>Result indicating what happened.</returns>
        DamageResult TakeDamage(DamageRequest request);
    }
}

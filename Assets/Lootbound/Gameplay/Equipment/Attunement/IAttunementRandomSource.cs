namespace Lootbound.Gameplay.Equipment
{
    /// <summary>
    /// Abstraction for random number generation in attunement system.
    /// Allows deterministic testing and debug overrides.
    /// </summary>
    public interface IAttunementRandomSource
    {
        /// <summary>
        /// Get a random value between 0.0 (inclusive) and 1.0 (exclusive).
        /// </summary>
        /// <returns>Random value in [0, 1) range.</returns>
        float NextFloat();

        /// <summary>
        /// Check if an attempt succeeds given the success chance.
        /// </summary>
        /// <param name="successChance">Probability of success (0.0 to 1.0).</param>
        /// <returns>True if attempt succeeds.</returns>
        bool Roll(float successChance);
    }
}

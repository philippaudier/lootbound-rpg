namespace Lootbound.Gameplay.Combat
{
    /// <summary>
    /// Pure pursuit/territory decision rules. No Unity types beyond nothing:
    /// distances and times are provided by the caller, so leash, hysteresis,
    /// reacquisition and arrival logic are testable in EditMode.
    ///
    /// The leash is evaluated against HomePosition (the territory), not
    /// against the enemy's own position: an enemy abandons when the PLAYER is
    /// too far from its home.
    /// </summary>
    public static class EnemyPursuitRules
    {
        /// <summary>
        /// Should an ongoing chase be abandoned?
        /// </summary>
        public static bool ShouldAbandonChase(
            float targetDistanceFromHome,
            float maxChaseDistanceFromHome,
            float timeSinceTargetSeen,
            float loseSightDelay)
        {
            if (targetDistanceFromHome > maxChaseDistanceFromHome)
            {
                return true;
            }

            return timeSinceTargetSeen > loseSightDelay;
        }

        /// <summary>
        /// May a NEW pursuit start toward this target? Uses a hysteresis
        /// margin below the abandon threshold so an enemy at the territory
        /// boundary does not oscillate Chasing/ReturningHome.
        /// </summary>
        public static bool CanStartChase(
            float targetDistanceFromHome,
            float maxChaseDistanceFromHome,
            float leashHysteresis)
        {
            return targetDistanceFromHome <= maxChaseDistanceFromHome - leashHysteresis;
        }

        /// <summary>
        /// May the enemy notice the player again? False while the
        /// post-return reacquisition cooldown is running.
        /// </summary>
        public static bool CanReacquireTarget(float now, float reacquireBlockedUntil)
        {
            return now >= reacquireBlockedUntil;
        }

        /// <summary>
        /// Arrival test with tolerance (home return, wander destination).
        /// </summary>
        public static bool HasArrived(float distanceToDestination, float completionDistance)
        {
            return distanceToDestination <= completionDistance;
        }
    }
}

namespace Lootbound.Gameplay.Equipment
{
    /// <summary>
    /// Static helper for attunement calculations.
    /// Provides fallback logic when config is unavailable.
    /// </summary>
    public static class AttunementHelper
    {
        /// <summary>
        /// Calculate attunement state from level and maximum.
        /// </summary>
        public static AttunementState GetState(int level, int maximumLevel)
        {
            if (level <= 0)
            {
                return AttunementState.Unattuned;
            }

            if (level >= maximumLevel)
            {
                return AttunementState.Maximum;
            }

            return AttunementState.Attuned;
        }

        /// <summary>
        /// Calculate attunement state using default maximum.
        /// </summary>
        public static AttunementState GetState(int level)
        {
            return GetState(level, AttunementFoundationConfig.DefaultMaximumAttunementLevel);
        }

        /// <summary>
        /// Check if level indicates equipment is attuned (level > 0).
        /// </summary>
        public static bool IsAttuned(int level)
        {
            return level > 0;
        }

        /// <summary>
        /// Check if level is at maximum.
        /// </summary>
        public static bool IsAtMaximum(int level, int maximumLevel)
        {
            return level >= maximumLevel;
        }

        /// <summary>
        /// Check if level is at maximum using default.
        /// </summary>
        public static bool IsAtMaximum(int level)
        {
            return IsAtMaximum(level, AttunementFoundationConfig.DefaultMaximumAttunementLevel);
        }

        /// <summary>
        /// Clamp level to valid range.
        /// </summary>
        public static int ClampLevel(int level, int maximumLevel)
        {
            if (maximumLevel <= 0)
            {
                maximumLevel = AttunementFoundationConfig.DefaultMaximumAttunementLevel;
            }

            if (level < 0)
            {
                return 0;
            }

            if (level > maximumLevel)
            {
                return maximumLevel;
            }

            return level;
        }

        /// <summary>
        /// Clamp level using default maximum.
        /// </summary>
        public static int ClampLevel(int level)
        {
            return ClampLevel(level, AttunementFoundationConfig.DefaultMaximumAttunementLevel);
        }

        /// <summary>
        /// Format display name with attunement suffix.
        /// Returns base name without suffix if level is 0.
        /// </summary>
        public static string FormatDisplayName(string baseName, int level)
        {
            if (string.IsNullOrEmpty(baseName))
            {
                return level > 0 ? $"+{level}" : string.Empty;
            }

            if (level <= 0)
            {
                return baseName;
            }

            return $"{baseName} +{level}";
        }
    }
}

namespace Lootbound.Gameplay.Equipment
{
    /// <summary>
    /// Preview of an attunement attempt without mutation.
    /// Used to validate and display attempt details before execution.
    /// </summary>
    public readonly struct AttunementAttemptPreview
    {
        /// <summary>
        /// Whether the attempt can proceed.
        /// </summary>
        public bool CanAttempt { get; }

        /// <summary>
        /// Current attunement level of the equipment.
        /// </summary>
        public int CurrentLevel { get; }

        /// <summary>
        /// Level the equipment will reach on success.
        /// </summary>
        public int ResultingLevelOnSuccess { get; }

        /// <summary>
        /// Maximum attunement level allowed.
        /// </summary>
        public int MaximumLevel { get; }

        /// <summary>
        /// Number of stones required for this attempt.
        /// </summary>
        public int RequiredStones { get; }

        /// <summary>
        /// Number of stones available in inventory.
        /// </summary>
        public int AvailableStones { get; }

        /// <summary>
        /// Base success chance before protection (0.0 to 1.0).
        /// </summary>
        public float BaseChance { get; }

        /// <summary>
        /// Protection bonus from consecutive failures (0.0 to cap).
        /// </summary>
        public float ProtectionBonus { get; }

        /// <summary>
        /// Effective success chance including protection (0.0 to 1.0).
        /// This is the actual chance used for the roll.
        /// </summary>
        public float SuccessChance { get; }

        /// <summary>
        /// Number of consecutive failures on this equipment.
        /// </summary>
        public int ConsecutiveFailures { get; }

        /// <summary>
        /// Whether the next attempt is guaranteed due to protection.
        /// </summary>
        public bool IsGuaranteed { get; }

        /// <summary>
        /// Reason why the attempt cannot proceed (if CanAttempt is false).
        /// </summary>
        public AttunementFailureReason FailureReason { get; }

        /// <summary>
        /// Name of the equipment being attuned.
        /// </summary>
        public string EquipmentName { get; }

        /// <summary>
        /// Whether the equipment will reach maximum after this attempt.
        /// </summary>
        public bool WillReachMaximum => ResultingLevelOnSuccess >= MaximumLevel;

        /// <summary>
        /// Whether the equipment is already at maximum.
        /// </summary>
        public bool IsAtMaximum => CurrentLevel >= MaximumLevel;

        /// <summary>
        /// Whether the player has enough stones.
        /// </summary>
        public bool HasEnoughStones => AvailableStones >= RequiredStones;

        /// <summary>
        /// Whether there is any accumulated protection.
        /// </summary>
        public bool HasProtection => ProtectionBonus > 0f;

        /// <summary>
        /// Protection bonus as a percentage for UI display.
        /// </summary>
        public float ProtectionBonusPercent => ProtectionBonus * 100f;

        private AttunementAttemptPreview(
            bool canAttempt,
            int currentLevel,
            int resultingLevelOnSuccess,
            int maximumLevel,
            int requiredStones,
            int availableStones,
            float baseChance,
            float protectionBonus,
            float successChance,
            int consecutiveFailures,
            bool isGuaranteed,
            AttunementFailureReason failureReason,
            string equipmentName)
        {
            CanAttempt = canAttempt;
            CurrentLevel = currentLevel;
            ResultingLevelOnSuccess = resultingLevelOnSuccess;
            MaximumLevel = maximumLevel;
            RequiredStones = requiredStones;
            AvailableStones = availableStones;
            BaseChance = baseChance;
            ProtectionBonus = protectionBonus;
            SuccessChance = successChance;
            ConsecutiveFailures = consecutiveFailures;
            IsGuaranteed = isGuaranteed;
            FailureReason = failureReason;
            EquipmentName = equipmentName ?? string.Empty;
        }

        /// <summary>
        /// Create a preview indicating the attempt cannot proceed.
        /// </summary>
        public static AttunementAttemptPreview CannotAttempt(
            AttunementFailureReason reason,
            int currentLevel = 0,
            int maximumLevel = 10,
            int requiredStones = 0,
            int availableStones = 0,
            string equipmentName = null)
        {
            return new AttunementAttemptPreview(
                canAttempt: false,
                currentLevel: currentLevel,
                resultingLevelOnSuccess: currentLevel,
                maximumLevel: maximumLevel,
                requiredStones: requiredStones,
                availableStones: availableStones,
                baseChance: 0f,
                protectionBonus: 0f,
                successChance: 0f,
                consecutiveFailures: 0,
                isGuaranteed: false,
                failureReason: reason,
                equipmentName: equipmentName);
        }

        /// <summary>
        /// Create a preview indicating the attempt can proceed.
        /// Legacy method for V1 compatibility (100% success).
        /// </summary>
        public static AttunementAttemptPreview CanProceed(
            int currentLevel,
            int maximumLevel,
            int requiredStones,
            int availableStones,
            float successChance,
            string equipmentName)
        {
            return new AttunementAttemptPreview(
                canAttempt: true,
                currentLevel: currentLevel,
                resultingLevelOnSuccess: currentLevel + 1,
                maximumLevel: maximumLevel,
                requiredStones: requiredStones,
                availableStones: availableStones,
                baseChance: successChance,
                protectionBonus: 0f,
                successChance: successChance,
                consecutiveFailures: 0,
                isGuaranteed: successChance >= 1f,
                failureReason: AttunementFailureReason.None,
                equipmentName: equipmentName);
        }

        /// <summary>
        /// Create a preview indicating the attempt can proceed with full protection info.
        /// </summary>
        public static AttunementAttemptPreview CanProceedWithProtection(
            int currentLevel,
            int maximumLevel,
            int requiredStones,
            int availableStones,
            float baseChance,
            float protectionBonus,
            float effectiveChance,
            int consecutiveFailures,
            bool isGuaranteed,
            string equipmentName)
        {
            return new AttunementAttemptPreview(
                canAttempt: true,
                currentLevel: currentLevel,
                resultingLevelOnSuccess: currentLevel + 1,
                maximumLevel: maximumLevel,
                requiredStones: requiredStones,
                availableStones: availableStones,
                baseChance: baseChance,
                protectionBonus: protectionBonus,
                successChance: effectiveChance,
                consecutiveFailures: consecutiveFailures,
                isGuaranteed: isGuaranteed,
                failureReason: AttunementFailureReason.None,
                equipmentName: equipmentName);
        }

        public override string ToString()
        {
            if (!CanAttempt)
            {
                return $"Cannot attempt: {FailureReason}";
            }

            string protectionInfo = HasProtection
                ? $" (+{ProtectionBonusPercent:F0}% protection)"
                : "";

            string guaranteedInfo = IsGuaranteed ? " [GUARANTEED]" : "";

            return $"Attempt preview: +{CurrentLevel} → +{ResultingLevelOnSuccess}, " +
                   $"cost {RequiredStones} stone(s), {SuccessChance:P0} chance{protectionInfo}{guaranteedInfo}";
        }
    }
}

namespace Lootbound.Gameplay.Equipment
{
    /// <summary>
    /// Result of an attunement attempt operation.
    /// Used by AttunementService and debug tools.
    /// </summary>
    public readonly struct AttunementAttemptResult
    {
        /// <summary>
        /// Whether the attunement attempt succeeded.
        /// </summary>
        public bool Success { get; }

        /// <summary>
        /// Attunement level before the attempt.
        /// </summary>
        public int PreviousLevel { get; }

        /// <summary>
        /// Attunement level after the attempt.
        /// </summary>
        public int CurrentLevel { get; }

        /// <summary>
        /// Whether the equipment was already at maximum level before the attempt.
        /// </summary>
        public bool WasAtMaximum { get; }

        /// <summary>
        /// The maximum attunement level used for this attempt.
        /// </summary>
        public int MaximumLevel { get; }

        /// <summary>
        /// Number of stones required for this attempt.
        /// </summary>
        public int StonesRequired { get; }

        /// <summary>
        /// Number of stones actually consumed.
        /// </summary>
        public int StonesConsumed { get; }

        /// <summary>
        /// Reason for failure if the attempt could not proceed.
        /// </summary>
        public AttunementFailureReason FailureReason { get; }

        /// <summary>
        /// The success chance that was used for this attempt.
        /// </summary>
        public float AttemptedChance { get; }

        /// <summary>
        /// Protection gained from this failure (0 if succeeded).
        /// </summary>
        public float ProtectionGained { get; }

        /// <summary>
        /// Whether this was an RNG-based failure (stones consumed but level unchanged).
        /// </summary>
        public bool WasRngFailure { get; }

        /// <summary>
        /// Whether success was guaranteed by accumulated resonance (pity).
        /// </summary>
        public bool WasGuaranteed { get; }

        /// <summary>
        /// Whether this attempt increased the level.
        /// </summary>
        public bool LevelIncreased => CurrentLevel > PreviousLevel;

        /// <summary>
        /// Whether the equipment is now at maximum level.
        /// </summary>
        public bool IsNowAtMaximum => CurrentLevel >= MaximumLevel;

        /// <summary>
        /// Whether stones were consumed in this attempt.
        /// </summary>
        public bool StonesWereConsumed => StonesConsumed > 0;

        /// <summary>
        /// Whether this was a resolved attempt (roll actually happened).
        /// True for Success or WasRngFailure. False for technical refusals.
        /// </summary>
        public bool WasAttemptResolved => Success || WasRngFailure;

        private AttunementAttemptResult(
            bool success,
            int previousLevel,
            int currentLevel,
            bool wasAtMaximum,
            int maximumLevel,
            int stonesRequired,
            int stonesConsumed,
            AttunementFailureReason failureReason,
            float attemptedChance,
            float protectionGained,
            bool wasRngFailure,
            bool wasGuaranteed)
        {
            Success = success;
            PreviousLevel = previousLevel;
            CurrentLevel = currentLevel;
            WasAtMaximum = wasAtMaximum;
            MaximumLevel = maximumLevel;
            StonesRequired = stonesRequired;
            StonesConsumed = stonesConsumed;
            FailureReason = failureReason;
            AttemptedChance = attemptedChance;
            ProtectionGained = protectionGained;
            WasRngFailure = wasRngFailure;
            WasGuaranteed = wasGuaranteed;
        }

        /// <summary>
        /// Create a result indicating the equipment was already at maximum level.
        /// </summary>
        public static AttunementAttemptResult AlreadyMaximum(int level, int maximumLevel)
        {
            return new AttunementAttemptResult(
                success: false,
                previousLevel: level,
                currentLevel: level,
                wasAtMaximum: true,
                maximumLevel: maximumLevel,
                stonesRequired: 0,
                stonesConsumed: 0,
                failureReason: AttunementFailureReason.AlreadyAtMaximum,
                attemptedChance: 0f,
                protectionGained: 0f,
                wasRngFailure: false,
                wasGuaranteed: false
            );
        }

        /// <summary>
        /// Create a result indicating a successful attunement.
        /// </summary>
        public static AttunementAttemptResult Succeeded(int previousLevel, int newLevel, int maximumLevel, bool wasGuaranteed = false)
        {
            return new AttunementAttemptResult(
                success: true,
                previousLevel: previousLevel,
                currentLevel: newLevel,
                wasAtMaximum: false,
                maximumLevel: maximumLevel,
                stonesRequired: 0,
                stonesConsumed: 0,
                failureReason: AttunementFailureReason.None,
                attemptedChance: 1f,
                protectionGained: 0f,
                wasRngFailure: false,
                wasGuaranteed: wasGuaranteed
            );
        }

        /// <summary>
        /// Create a result indicating a successful attunement with stone consumption.
        /// </summary>
        public static AttunementAttemptResult SucceededWithStones(
            int previousLevel,
            int newLevel,
            int maximumLevel,
            int stonesRequired,
            int stonesConsumed,
            float attemptedChance = 1f,
            bool wasGuaranteed = false)
        {
            return new AttunementAttemptResult(
                success: true,
                previousLevel: previousLevel,
                currentLevel: newLevel,
                wasAtMaximum: false,
                maximumLevel: maximumLevel,
                stonesRequired: stonesRequired,
                stonesConsumed: stonesConsumed,
                failureReason: AttunementFailureReason.None,
                attemptedChance: attemptedChance,
                protectionGained: 0f,
                wasRngFailure: false,
                wasGuaranteed: wasGuaranteed
            );
        }

        /// <summary>
        /// Create a result indicating RNG-based failure (stones consumed, level unchanged).
        /// </summary>
        public static AttunementAttemptResult FailedWithStones(
            int level,
            int maximumLevel,
            int stonesRequired,
            int stonesConsumed,
            float attemptedChance,
            float protectionGained)
        {
            return new AttunementAttemptResult(
                success: false,
                previousLevel: level,
                currentLevel: level,
                wasAtMaximum: false,
                maximumLevel: maximumLevel,
                stonesRequired: stonesRequired,
                stonesConsumed: stonesConsumed,
                failureReason: AttunementFailureReason.None,
                attemptedChance: attemptedChance,
                protectionGained: protectionGained,
                wasRngFailure: true,
                wasGuaranteed: false  // Never guaranteed if failed
            );
        }

        /// <summary>
        /// Create a result indicating failure (not due to maximum level).
        /// Reserved for legacy or non-RNG failures.
        /// </summary>
        public static AttunementAttemptResult Failed(int level, int maximumLevel)
        {
            return new AttunementAttemptResult(
                success: false,
                previousLevel: level,
                currentLevel: level,
                wasAtMaximum: false,
                maximumLevel: maximumLevel,
                stonesRequired: 0,
                stonesConsumed: 0,
                failureReason: AttunementFailureReason.None,
                attemptedChance: 0f,
                protectionGained: 0f,
                wasRngFailure: false,
                wasGuaranteed: false
            );
        }

        /// <summary>
        /// Create a result indicating the attempt could not proceed.
        /// </summary>
        public static AttunementAttemptResult CannotAttempt(
            int level,
            int maximumLevel,
            int stonesRequired,
            AttunementFailureReason reason)
        {
            return new AttunementAttemptResult(
                success: false,
                previousLevel: level,
                currentLevel: level,
                wasAtMaximum: reason == AttunementFailureReason.AlreadyAtMaximum,
                maximumLevel: maximumLevel,
                stonesRequired: stonesRequired,
                stonesConsumed: 0,
                failureReason: reason,
                attemptedChance: 0f,
                protectionGained: 0f,
                wasRngFailure: false,
                wasGuaranteed: false
            );
        }

        public override string ToString()
        {
            if (FailureReason != AttunementFailureReason.None && !WasAtMaximum)
            {
                return $"[AttunementAttempt] Cannot attempt: {FailureReason}";
            }

            if (WasAtMaximum)
            {
                return $"[AttunementAttempt] Already at maximum (+{CurrentLevel}/+{MaximumLevel})";
            }

            if (Success)
            {
                string stoneInfo = StonesConsumed > 0 ? $" (used {StonesConsumed} stone(s))" : "";
                return $"[AttunementAttempt] Success: +{PreviousLevel} → +{CurrentLevel}{stoneInfo}";
            }

            if (WasRngFailure)
            {
                string protectionInfo = ProtectionGained > 0
                    ? $" (+{ProtectionGained * 100f:F0}% resonance gained)"
                    : "";
                return $"[AttunementAttempt] Failed at +{CurrentLevel} ({AttemptedChance:P0} chance){protectionInfo}";
            }

            return $"[AttunementAttempt] Failed at +{CurrentLevel}";
        }
    }
}

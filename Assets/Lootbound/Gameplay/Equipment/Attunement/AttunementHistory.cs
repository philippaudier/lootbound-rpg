using System;
using UnityEngine;

namespace Lootbound.Gameplay.Equipment
{
    /// <summary>
    /// Tracks the history of attunement attempts on a specific equipment instance.
    /// Records successes, failures, stones consumed, and memorable milestones.
    /// </summary>
    /// <remarks>
    /// This structure belongs to EquipmentHistory and is serialized with it.
    /// It does NOT duplicate the consecutive failure count (protection), which
    /// remains in EquipmentData as the gameplay source of truth.
    /// </remarks>
    [Serializable]
    public sealed class AttunementHistory
    {
        // Attempt counters
        [SerializeField] private int totalAttempts;
        [SerializeField] private int successfulAttempts;
        [SerializeField] private int failedAttempts;
        [SerializeField] private int totalStonesConsumed;

        // Milestones
        [SerializeField] private int highestAttunementLevelReached;
        [SerializeField] private int longestFailureStreak;

        // Last attempt details
        [SerializeField] private long lastAttemptTimestamp;
        [SerializeField] private string lastAttemptLocation;
        [SerializeField] private bool lastAttemptSucceeded;
        [SerializeField] private bool lastAttemptWasGuaranteed;
        [SerializeField] private int lastAttemptPreviousLevel;
        [SerializeField] private int lastAttemptResultingLevel;
        [SerializeField] private float lastAttemptEffectiveChance;
        [SerializeField] private int lastAttemptStonesConsumed;

        // Last success/failure timestamps
        [SerializeField] private long lastSuccessTimestamp;
        [SerializeField] private long lastFailureTimestamp;

        /// <summary>
        /// Total number of resolved attunement attempts (successes + RNG failures).
        /// Does not include technical refusals (no stones, at maximum, etc.).
        /// </summary>
        public int TotalAttempts => totalAttempts;

        /// <summary>
        /// Number of successful attunement attempts.
        /// </summary>
        public int SuccessfulAttempts => successfulAttempts;

        /// <summary>
        /// Number of failed attunement attempts (RNG failures only).
        /// </summary>
        public int FailedAttempts => failedAttempts;

        /// <summary>
        /// Total attunement stones consumed across all attempts.
        /// </summary>
        public int TotalStonesConsumed => totalStonesConsumed;

        /// <summary>
        /// Highest attunement level ever reached by this equipment.
        /// Never decreases, even if level were to be reduced by future mechanics.
        /// </summary>
        public int HighestAttunementLevelReached => highestAttunementLevelReached;

        /// <summary>
        /// Longest consecutive failure streak recorded.
        /// </summary>
        public int LongestFailureStreak => longestFailureStreak;

        /// <summary>
        /// Unix timestamp of the last resolved attempt.
        /// </summary>
        public long LastAttemptTimestamp => lastAttemptTimestamp;

        /// <summary>
        /// Location where the last attempt was made.
        /// </summary>
        public string LastAttemptLocation => lastAttemptLocation;

        /// <summary>
        /// Whether the last attempt succeeded.
        /// </summary>
        public bool LastAttemptSucceeded => lastAttemptSucceeded;

        /// <summary>
        /// Whether the last attempt was a guaranteed success.
        /// </summary>
        public bool LastAttemptWasGuaranteed => lastAttemptWasGuaranteed;

        /// <summary>
        /// Attunement level before the last attempt.
        /// </summary>
        public int LastAttemptPreviousLevel => lastAttemptPreviousLevel;

        /// <summary>
        /// Attunement level after the last attempt.
        /// </summary>
        public int LastAttemptResultingLevel => lastAttemptResultingLevel;

        /// <summary>
        /// Success chance used for the last attempt (0.0 to 1.0).
        /// </summary>
        public float LastAttemptEffectiveChance => lastAttemptEffectiveChance;

        /// <summary>
        /// Stones consumed in the last attempt.
        /// </summary>
        public int LastAttemptStonesConsumed => lastAttemptStonesConsumed;

        /// <summary>
        /// Unix timestamp of the last successful attempt.
        /// </summary>
        public long LastSuccessTimestamp => lastSuccessTimestamp;

        /// <summary>
        /// Unix timestamp of the last failed attempt.
        /// </summary>
        public long LastFailureTimestamp => lastFailureTimestamp;

        /// <summary>
        /// Whether any attunement attempt has been made.
        /// </summary>
        public bool HasAttemptHistory => totalAttempts > 0;

        /// <summary>
        /// Whether any failure has been recorded.
        /// </summary>
        public bool HasFailureHistory => failedAttempts > 0;

        /// <summary>
        /// Whether any success has been recorded.
        /// </summary>
        public bool HasSuccessHistory => successfulAttempts > 0;

        /// <summary>
        /// Create a new empty attunement history.
        /// </summary>
        public AttunementHistory()
        {
            // All fields default to 0/null/false
        }

        /// <summary>
        /// Create history from serialized data.
        /// </summary>
        public AttunementHistory(
            int totalAttempts,
            int successfulAttempts,
            int failedAttempts,
            int totalStonesConsumed,
            int highestLevel,
            int longestStreak,
            long lastAttemptTime,
            string lastAttemptLoc,
            bool lastSucceeded,
            bool lastWasGuaranteed,
            int lastPrevLevel,
            int lastResultLevel,
            float lastChance,
            int lastStones,
            long lastSuccessTime,
            long lastFailureTime)
        {
            this.totalAttempts = Mathf.Max(0, totalAttempts);
            this.successfulAttempts = Mathf.Max(0, successfulAttempts);
            this.failedAttempts = Mathf.Max(0, failedAttempts);
            this.totalStonesConsumed = Mathf.Max(0, totalStonesConsumed);
            this.highestAttunementLevelReached = Mathf.Max(0, highestLevel);
            this.longestFailureStreak = Mathf.Max(0, longestStreak);
            this.lastAttemptTimestamp = lastAttemptTime;
            this.lastAttemptLocation = lastAttemptLoc;
            this.lastAttemptSucceeded = lastSucceeded;
            this.lastAttemptWasGuaranteed = lastWasGuaranteed;
            this.lastAttemptPreviousLevel = Mathf.Max(0, lastPrevLevel);
            this.lastAttemptResultingLevel = Mathf.Max(0, lastResultLevel);
            this.lastAttemptEffectiveChance = Mathf.Clamp01(lastChance);
            this.lastAttemptStonesConsumed = Mathf.Max(0, lastStones);
            this.lastSuccessTimestamp = lastSuccessTime;
            this.lastFailureTimestamp = lastFailureTime;
        }

        /// <summary>
        /// Record a resolved attunement attempt.
        /// Only call this for attempts where a roll actually happened (Success or WasRngFailure).
        /// </summary>
        /// <param name="result">The attunement result.</param>
        /// <param name="location">Where the attempt was made.</param>
        /// <param name="currentConsecutiveFailures">Current failure streak from EquipmentData.</param>
        /// <param name="wasGuaranteed">Whether this was a guaranteed success.</param>
        /// <param name="timestamp">Unix timestamp (defaults to now if 0).</param>
        public void RecordAttempt(
            AttunementAttemptResult result,
            string location,
            int currentConsecutiveFailures,
            bool wasGuaranteed,
            long timestamp = 0)
        {
            // Only record resolved attempts
            if (!result.Success && !result.WasRngFailure)
            {
                return;
            }

            // Use current time if not specified
            if (timestamp <= 0)
            {
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            }

            // Update counters
            totalAttempts++;
            totalStonesConsumed += result.StonesConsumed;

            if (result.Success)
            {
                successfulAttempts++;
                lastSuccessTimestamp = timestamp;

                // Update highest level reached
                highestAttunementLevelReached = Mathf.Max(
                    highestAttunementLevelReached,
                    result.CurrentLevel);
            }
            else if (result.WasRngFailure)
            {
                failedAttempts++;
                lastFailureTimestamp = timestamp;

                // Update longest failure streak
                // The currentConsecutiveFailures has already been incremented by the service
                longestFailureStreak = Mathf.Max(longestFailureStreak, currentConsecutiveFailures);
            }

            // Record last attempt details
            lastAttemptTimestamp = timestamp;
            lastAttemptLocation = location ?? "Unknown";
            lastAttemptSucceeded = result.Success;
            lastAttemptWasGuaranteed = wasGuaranteed;
            lastAttemptPreviousLevel = result.PreviousLevel;
            lastAttemptResultingLevel = result.CurrentLevel;
            lastAttemptEffectiveChance = result.AttemptedChance;
            lastAttemptStonesConsumed = result.StonesConsumed;
        }

        /// <summary>
        /// Initialize highest level if the equipment already has attunement.
        /// Called defensively when loading old equipment data.
        /// </summary>
        /// <param name="currentLevel">Current attunement level of the equipment.</param>
        public void EnsureHighestLevel(int currentLevel)
        {
            highestAttunementLevelReached = Mathf.Max(highestAttunementLevelReached, currentLevel);
        }

        /// <summary>
        /// Create a deep copy of this history.
        /// </summary>
        public AttunementHistory Clone()
        {
            return new AttunementHistory(
                totalAttempts,
                successfulAttempts,
                failedAttempts,
                totalStonesConsumed,
                highestAttunementLevelReached,
                longestFailureStreak,
                lastAttemptTimestamp,
                lastAttemptLocation,
                lastAttemptSucceeded,
                lastAttemptWasGuaranteed,
                lastAttemptPreviousLevel,
                lastAttemptResultingLevel,
                lastAttemptEffectiveChance,
                lastAttemptStonesConsumed,
                lastSuccessTimestamp,
                lastFailureTimestamp);
        }

        /// <summary>
        /// Reset all history data (debug only).
        /// Does not affect the equipment's actual attunement level or protection.
        /// </summary>
        public void Reset()
        {
            totalAttempts = 0;
            successfulAttempts = 0;
            failedAttempts = 0;
            totalStonesConsumed = 0;
            highestAttunementLevelReached = 0;
            longestFailureStreak = 0;
            lastAttemptTimestamp = 0;
            lastAttemptLocation = null;
            lastAttemptSucceeded = false;
            lastAttemptWasGuaranteed = false;
            lastAttemptPreviousLevel = 0;
            lastAttemptResultingLevel = 0;
            lastAttemptEffectiveChance = 0f;
            lastAttemptStonesConsumed = 0;
            lastSuccessTimestamp = 0;
            lastFailureTimestamp = 0;
        }

        /// <summary>
        /// Get a summary string for UI display.
        /// </summary>
        public string GetSummary()
        {
            if (totalAttempts == 0)
            {
                return "This weapon has never undergone an Attunement attempt.";
            }

            string attemptWord = totalAttempts == 1 ? "attempt" : "attempts";
            string result = $"{totalAttempts} Attunement {attemptWord}";

            if (successfulAttempts > 0)
            {
                string successWord = successfulAttempts == 1 ? "success" : "successes";
                result += $", {successfulAttempts} {successWord}";
            }

            if (failedAttempts > 0)
            {
                string failWord = failedAttempts == 1 ? "failure" : "failures";
                result += $", {failedAttempts} {failWord}";
            }

            return result;
        }

        /// <summary>
        /// Get the last attempt result as a display string.
        /// </summary>
        public string GetLastAttemptSummary()
        {
            if (lastAttemptTimestamp == 0)
            {
                return null;
            }

            string result;
            if (lastAttemptSucceeded)
            {
                if (lastAttemptWasGuaranteed)
                {
                    result = "Guaranteed Success";
                }
                else
                {
                    result = "Success";
                }
                result += $" — +{lastAttemptPreviousLevel} → +{lastAttemptResultingLevel}";
            }
            else
            {
                result = $"Failed — remained at +{lastAttemptResultingLevel}";
            }

            if (!string.IsNullOrEmpty(lastAttemptLocation))
            {
                result += $"\n{lastAttemptLocation}";
            }

            return result;
        }

        /// <summary>
        /// Format a Unix timestamp for display.
        /// </summary>
        public static string FormatTimestamp(long timestamp)
        {
            if (timestamp <= 0)
            {
                return null;
            }

            var dateTime = DateTimeOffset.FromUnixTimeSeconds(timestamp).LocalDateTime;
            var today = DateTime.Today;
            var yesterday = today.AddDays(-1);

            if (dateTime.Date == today)
            {
                return $"Today, {dateTime:HH:mm}";
            }
            else if (dateTime.Date == yesterday)
            {
                return $"Yesterday, {dateTime:HH:mm}";
            }
            else if (dateTime.Year == today.Year)
            {
                return dateTime.ToString("d MMMM");
            }
            else
            {
                return dateTime.ToString("d MMMM yyyy");
            }
        }
    }
}

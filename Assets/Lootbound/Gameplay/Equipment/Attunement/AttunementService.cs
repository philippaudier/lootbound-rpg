using System;
using UnityEngine;
using Lootbound.Gameplay.Inventory;
using Lootbound.Core;
using Lootbound.Core.Logging;

namespace Lootbound.Gameplay.Equipment
{
    /// <summary>
    /// Service for managing attunement attempts with stone consumption.
    /// Handles validation, preview, atomic transactions, and failure/protection mechanics.
    /// </summary>
    public class AttunementService
    {
        private const string Category = "Attunement";

        private readonly AttunementCostConfig costConfig;
        private readonly AttunementCoreConfig coreConfig;
        private readonly AttunementChanceConfig chanceConfig;
        private readonly Inventory.Inventory inventory;
        private IAttunementRandomSource randomSource;

        private bool isAttemptInProgress;

        // Debug override for forcing outcomes
        private bool? forceNextOutcome;

        /// <summary>
        /// Event fired when an attunement attempt completes.
        /// </summary>
        public event Action<AttunementAttemptResult> OnAttunementCompleted;

        /// <summary>
        /// Create a new attunement service.
        /// </summary>
        /// <param name="costConfig">Configuration for stone costs.</param>
        /// <param name="coreConfig">Configuration for stat bonuses.</param>
        /// <param name="inventory">Player's inventory for stone management.</param>
        /// <param name="chanceConfig">Optional configuration for success chances. If null, uses 100% success.</param>
        /// <param name="randomSource">Optional random source. If null, uses SystemRandomSource.</param>
        public AttunementService(
            AttunementCostConfig costConfig,
            AttunementCoreConfig coreConfig,
            Inventory.Inventory inventory,
            AttunementChanceConfig chanceConfig = null,
            IAttunementRandomSource randomSource = null)
        {
            this.costConfig = costConfig;
            this.coreConfig = coreConfig;
            this.inventory = inventory;
            this.chanceConfig = chanceConfig;
            this.randomSource = randomSource ?? new SystemRandomSource();
        }

        /// <summary>
        /// Get the maximum attunement level from configuration.
        /// </summary>
        public int MaximumLevel => coreConfig != null
            ? coreConfig.MaximumLevel
            : AttunementFoundationConfig.DefaultMaximumAttunementLevel;

        /// <summary>
        /// Get the number of stones per attempt from configuration.
        /// </summary>
        public int StonesPerAttempt => costConfig != null
            ? costConfig.StonesPerAttempt
            : 1;

        /// <summary>
        /// Get the attunement stone definition.
        /// </summary>
        public ItemDefinition StoneDefinition => costConfig?.AttunementStoneDefinition;

        /// <summary>
        /// Whether chance-based failure is enabled.
        /// </summary>
        public bool HasChanceSystem => chanceConfig != null;

        /// <summary>
        /// Get the number of attunement stones in inventory.
        /// </summary>
        public int GetAvailableStones()
        {
            if (inventory == null || costConfig?.AttunementStoneDefinition == null)
            {
                return 0;
            }

            return inventory.GetItemCount(costConfig.AttunementStoneDefinition);
        }

        /// <summary>
        /// Set the random source (for testing).
        /// </summary>
        public void SetRandomSource(IAttunementRandomSource source)
        {
            randomSource = source ?? new SystemRandomSource();
        }

        /// <summary>
        /// Force the next attunement outcome (debug only).
        /// </summary>
        /// <param name="success">True to force success, false to force failure, null to reset.</param>
        public void ForceNextOutcome(bool? success)
        {
            forceNextOutcome = success;
            if (success.HasValue)
            {
                LootboundLog.Info(Category, $"DEBUG: Next outcome forced to {(success.Value ? "SUCCESS" : "FAILURE")}");
            }
        }

        /// <summary>
        /// Preview an attunement attempt without making any changes.
        /// </summary>
        public AttunementAttemptPreview PreviewAttempt(EquipmentData equipment)
        {
            // Validate equipment
            if (equipment == null || !equipment.IsValid)
            {
                return AttunementAttemptPreview.CannotAttempt(
                    AttunementFailureReason.InvalidEquipment);
            }

            // Get current state
            int currentLevel = equipment.AttunementLevel;
            int maxLevel = MaximumLevel;
            int requiredStones = StonesPerAttempt;
            int availableStones = GetAvailableStones();
            string equipmentName = equipment.CustomName ?? equipment.DefinitionId;
            int consecutiveFailures = equipment.ConsecutiveAttunementFailures;

            // Check maximum
            if (currentLevel >= maxLevel)
            {
                return AttunementAttemptPreview.CannotAttempt(
                    AttunementFailureReason.AlreadyAtMaximum,
                    currentLevel,
                    maxLevel,
                    requiredStones,
                    availableStones,
                    equipmentName);
            }

            // Check configuration
            if (costConfig == null || !costConfig.IsValid)
            {
                return AttunementAttemptPreview.CannotAttempt(
                    AttunementFailureReason.InvalidConfiguration,
                    currentLevel,
                    maxLevel,
                    requiredStones,
                    availableStones,
                    equipmentName);
            }

            // Check inventory
            if (inventory == null)
            {
                return AttunementAttemptPreview.CannotAttempt(
                    AttunementFailureReason.MissingInventory,
                    currentLevel,
                    maxLevel,
                    requiredStones,
                    availableStones,
                    equipmentName);
            }

            // Check stones
            if (availableStones == 0)
            {
                return AttunementAttemptPreview.CannotAttempt(
                    AttunementFailureReason.NoAttunementStones,
                    currentLevel,
                    maxLevel,
                    requiredStones,
                    availableStones,
                    equipmentName);
            }

            if (availableStones < requiredStones)
            {
                return AttunementAttemptPreview.CannotAttempt(
                    AttunementFailureReason.InsufficientAttunementStones,
                    currentLevel,
                    maxLevel,
                    requiredStones,
                    availableStones,
                    equipmentName);
            }

            // Calculate chances
            if (chanceConfig != null)
            {
                float baseChance = chanceConfig.GetBaseChance(currentLevel);
                float protectionBonus = chanceConfig.GetProtectionBonus(consecutiveFailures);
                float effectiveChance = chanceConfig.GetEffectiveChance(currentLevel, consecutiveFailures);
                bool isGuaranteed = chanceConfig.IsGuaranteed(consecutiveFailures);

                return AttunementAttemptPreview.CanProceedWithProtection(
                    currentLevel,
                    maxLevel,
                    requiredStones,
                    availableStones,
                    baseChance,
                    protectionBonus,
                    effectiveChance,
                    consecutiveFailures,
                    isGuaranteed,
                    equipmentName);
            }

            // No chance config: 100% success (V1 behavior)
            return AttunementAttemptPreview.CanProceed(
                currentLevel,
                maxLevel,
                requiredStones,
                availableStones,
                1f,
                equipmentName);
        }

        /// <summary>
        /// Default location for attunement attempts when not specified.
        /// </summary>
        public const string DefaultAttunementLocation = "Attunement Table";

        // Current attempt context
        private string currentAttemptLocation;

        /// <summary>
        /// Attempt to increase attunement level, consuming stones.
        /// </summary>
        /// <param name="equipment">The equipment to attune.</param>
        /// <param name="bypassCost">If true, skip stone consumption (debug only).</param>
        /// <returns>Result of the attempt.</returns>
        public AttunementAttemptResult TryAttune(EquipmentData equipment, bool bypassCost = false)
        {
            return TryAttune(equipment, DefaultAttunementLocation, bypassCost);
        }

        /// <summary>
        /// Attempt to increase attunement level, consuming stones.
        /// </summary>
        /// <param name="equipment">The equipment to attune.</param>
        /// <param name="location">Location where the attempt is taking place.</param>
        /// <param name="bypassCost">If true, skip stone consumption (debug only).</param>
        /// <returns>Result of the attempt.</returns>
        public AttunementAttemptResult TryAttune(EquipmentData equipment, string location, bool bypassCost = false)
        {
            // Prevent concurrent attempts
            if (isAttemptInProgress)
            {
                LogDebug("Attempt blocked: already in progress");
                return AttunementAttemptResult.CannotAttempt(
                    equipment?.AttunementLevel ?? 0,
                    MaximumLevel,
                    StonesPerAttempt,
                    AttunementFailureReason.AttemptInProgress);
            }

            try
            {
                isAttemptInProgress = true;
                currentAttemptLocation = location ?? DefaultAttunementLocation;
                return ExecuteAttempt(equipment, bypassCost);
            }
            finally
            {
                isAttemptInProgress = false;
                currentAttemptLocation = null;
            }
        }

        private AttunementAttemptResult ExecuteAttempt(EquipmentData equipment, bool bypassCost)
        {
            int maxLevel = MaximumLevel;
            int requiredStones = StonesPerAttempt;

            // 1. Validate equipment
            if (equipment == null || !equipment.IsValid)
            {
                LogDebug("Invalid equipment");
                return AttunementAttemptResult.CannotAttempt(0, maxLevel, requiredStones,
                    AttunementFailureReason.InvalidEquipment);
            }

            int previousLevel = equipment.AttunementLevel;
            string equipmentName = equipment.CustomName ?? equipment.DefinitionId;
            int consecutiveFailures = equipment.ConsecutiveAttunementFailures;

            // 2. Check maximum
            if (previousLevel >= maxLevel)
            {
                LogDebug($"{equipmentName} already at maximum (+{previousLevel})");
                var maxResult = AttunementAttemptResult.AlreadyMaximum(previousLevel, maxLevel);
                OnAttunementCompleted?.Invoke(maxResult);
                return maxResult;
            }

            // 3. Handle bypass cost (debug mode)
            if (bypassCost)
            {
                if (costConfig != null && !costConfig.AllowDebugFreeAttempts)
                {
                    LogDebug("Debug bypass not allowed by configuration");
                    return AttunementAttemptResult.CannotAttempt(previousLevel, maxLevel, requiredStones,
                        AttunementFailureReason.InvalidConfiguration);
                }

                LogDebug($"DEBUG: Free attunement for {equipmentName}");
                return ExecuteRoll(equipment, previousLevel, maxLevel, 0, 0);
            }

            // 4. Validate configuration
            if (costConfig == null || !costConfig.IsValid)
            {
                LogDebug("Invalid cost configuration");
                return AttunementAttemptResult.CannotAttempt(previousLevel, maxLevel, requiredStones,
                    AttunementFailureReason.InvalidConfiguration);
            }

            // 5. Validate inventory
            if (inventory == null)
            {
                LogDebug("No inventory reference");
                return AttunementAttemptResult.CannotAttempt(previousLevel, maxLevel, requiredStones,
                    AttunementFailureReason.MissingInventory);
            }

            // 6. Check available stones
            int availableStones = GetAvailableStones();
            if (availableStones == 0)
            {
                LogDebug("No attunement stones available");
                return AttunementAttemptResult.CannotAttempt(previousLevel, maxLevel, requiredStones,
                    AttunementFailureReason.NoAttunementStones);
            }

            if (availableStones < requiredStones)
            {
                LogDebug($"Insufficient stones: {availableStones}/{requiredStones}");
                return AttunementAttemptResult.CannotAttempt(previousLevel, maxLevel, requiredStones,
                    AttunementFailureReason.InsufficientAttunementStones);
            }

            // 7. ATOMIC: Remove stones first (before roll)
            int stonesConsumed = inventory.RemoveItem(costConfig.AttunementStoneDefinition, requiredStones);

            if (stonesConsumed < requiredStones)
            {
                // Partial removal - this shouldn't happen, but handle it
                LootboundLog.Warning(Category,
                    $"Stone removal mismatch: expected {requiredStones}, got {stonesConsumed}");

                if (stonesConsumed == 0)
                {
                    return AttunementAttemptResult.CannotAttempt(previousLevel, maxLevel, requiredStones,
                        AttunementFailureReason.TransactionFailed);
                }
            }

            // 8. Execute roll and attunement
            return ExecuteRoll(equipment, previousLevel, maxLevel, requiredStones, stonesConsumed);
        }

        private AttunementAttemptResult ExecuteRoll(
            EquipmentData equipment,
            int previousLevel,
            int maxLevel,
            int stonesRequired,
            int stonesConsumed)
        {
            string equipmentName = equipment.CustomName ?? equipment.DefinitionId;
            int consecutiveFailures = equipment.ConsecutiveAttunementFailures;

            // Calculate success chance
            float effectiveChance;
            float baseChance;
            bool isGuaranteed;

            if (chanceConfig != null)
            {
                baseChance = chanceConfig.GetBaseChance(previousLevel);
                effectiveChance = chanceConfig.GetEffectiveChance(previousLevel, consecutiveFailures);
                isGuaranteed = chanceConfig.IsGuaranteed(consecutiveFailures);
            }
            else
            {
                // No chance config: 100% success
                baseChance = 1f;
                effectiveChance = 1f;
                isGuaranteed = true;
            }

            // Determine outcome
            bool success;
            bool outcomeWasGuaranteed = isGuaranteed;

            if (forceNextOutcome.HasValue)
            {
                success = forceNextOutcome.Value;
                forceNextOutcome = null; // Reset after use
                LogDebug($"DEBUG: Forced outcome = {success}");
            }
            else if (isGuaranteed)
            {
                success = true;
                LogDebug($"Guaranteed success after {consecutiveFailures} failures");
            }
            else
            {
                success = randomSource.Roll(effectiveChance);
            }

            if (success)
            {
                // SUCCESS: Increase level, reset protection
                var increaseResult = equipment.TryIncreaseAttunement(maxLevel);
                equipment.ResetAttunementFailures();

                if (increaseResult.Success)
                {
                    LogDebug($"{equipmentName}: +{previousLevel} → +{increaseResult.CurrentLevel}" +
                             (stonesConsumed > 0 ? $" (consumed {stonesConsumed} stone(s))" : " (debug)") +
                             $" [{effectiveChance:P0} chance]");

                    var result = AttunementAttemptResult.SucceededWithStones(
                        previousLevel,
                        increaseResult.CurrentLevel,
                        maxLevel,
                        stonesRequired,
                        stonesConsumed,
                        effectiveChance,
                        outcomeWasGuaranteed);

                    // Record to history (reset happened so consecutiveFailures is now 0)
                    RecordAttunementHistory(equipment, result, outcomeWasGuaranteed, 0);

                    OnAttunementCompleted?.Invoke(result);
                    return result;
                }

                // This shouldn't happen, but handle gracefully
                LootboundLog.Warning(Category, $"Attunement increase failed unexpectedly");
                var failResult = AttunementAttemptResult.CannotAttempt(
                    previousLevel, maxLevel, stonesRequired,
                    AttunementFailureReason.TransactionFailed);
                OnAttunementCompleted?.Invoke(failResult);
                return failResult;
            }
            else
            {
                // FAILURE: Increment protection counter
                equipment.IncrementAttunementFailures();
                int newFailureCount = equipment.ConsecutiveAttunementFailures;

                // Calculate protection gained
                float protectionGained = chanceConfig?.ProtectionBonusPerFailure ?? 0f;

                LogDebug($"{equipmentName}: FAILED at +{previousLevel}" +
                         $" (consumed {stonesConsumed} stone(s))" +
                         $" [{effectiveChance:P0} chance]" +
                         $" - failures now: {newFailureCount}");

                var result = AttunementAttemptResult.FailedWithStones(
                    previousLevel,
                    maxLevel,
                    stonesRequired,
                    stonesConsumed,
                    effectiveChance,
                    protectionGained);

                // Record to history (use the new failure count after increment)
                RecordAttunementHistory(equipment, result, false, newFailureCount);

                OnAttunementCompleted?.Invoke(result);
                return result;
            }
        }

        /// <summary>
        /// Record an attunement attempt to the equipment's history.
        /// Only called for resolved attempts (Success or WasRngFailure).
        /// </summary>
        private void RecordAttunementHistory(
            EquipmentData equipment,
            AttunementAttemptResult result,
            bool wasGuaranteed,
            int currentConsecutiveFailures)
        {
            if (equipment?.History == null)
            {
                return;
            }

            // Only record resolved attempts
            if (!result.WasAttemptResolved)
            {
                return;
            }

            equipment.History.Attunement.RecordAttempt(
                result,
                currentAttemptLocation ?? DefaultAttunementLocation,
                currentConsecutiveFailures,
                wasGuaranteed);

            // Ensure highest level is tracked
            equipment.History.Attunement.EnsureHighestLevel(equipment.AttunementLevel);

            LogDebug($"History recorded: {(result.Success ? "Success" : "Failure")} at {currentAttemptLocation}");
        }

        /// <summary>
        /// Debug: Attempt attunement without consuming stones.
        /// Only works if AllowDebugFreeAttempts is enabled.
        /// </summary>
        public AttunementAttemptResult TryAttuneDebugWithoutCost(EquipmentData equipment)
        {
            LootboundLog.Info(Category, "DEBUG: Attempting free attunement");
            return TryAttune(equipment, bypassCost: true);
        }

        private void LogDebug(string message)
        {
            bool shouldLog = (costConfig != null && costConfig.EnableDebugLogs) ||
                             (chanceConfig != null && chanceConfig.EnableDebugLogs);

            if (shouldLog)
            {
                LootboundLog.Info(Category, message);
            }
        }
    }
}

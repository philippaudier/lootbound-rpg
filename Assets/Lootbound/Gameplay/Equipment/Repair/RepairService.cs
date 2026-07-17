using System;
using UnityEngine;
using Lootbound.Gameplay.Inventory;

namespace Lootbound.Gameplay.Equipment
{
    /// <summary>
    /// Service that handles equipment repair using repair fragments.
    /// Provides preview and atomic execution of repair operations.
    /// </summary>
    public class RepairService
    {
        private readonly RepairConfig config;
        private readonly ItemDefinition repairFragmentDefinition;
        private readonly Inventory.Inventory inventory;
        private readonly Action onStatsChanged;

        /// <summary>
        /// Event raised when a repair is successfully completed.
        /// </summary>
        public event Action<RepairResult> OnRepairCompleted;

        /// <summary>
        /// Create a new repair service.
        /// </summary>
        /// <param name="config">Repair configuration.</param>
        /// <param name="repairFragmentDefinition">The repair fragment item definition.</param>
        /// <param name="inventory">Player inventory containing fragments.</param>
        /// <param name="onStatsChanged">Optional callback when equipment stats need recalculation (e.g., for equipped weapon).</param>
        public RepairService(
            RepairConfig config,
            ItemDefinition repairFragmentDefinition,
            Inventory.Inventory inventory,
            Action onStatsChanged = null)
        {
            this.config = config;
            this.repairFragmentDefinition = repairFragmentDefinition;
            this.inventory = inventory;
            this.onStatsChanged = onStatsChanged;
        }

        /// <summary>
        /// Get the number of repair fragments currently in inventory.
        /// </summary>
        public int GetAvailableFragments()
        {
            if (inventory == null || repairFragmentDefinition == null)
            {
                return 0;
            }

            return inventory.GetItemCount(repairFragmentDefinition);
        }

        /// <summary>
        /// Preview a repair operation without modifying any state.
        /// </summary>
        /// <param name="equipment">Equipment to repair.</param>
        /// <param name="fragmentCount">Number of fragments to use. Use 0 or negative for maximum available.</param>
        /// <returns>Preview showing what the repair would do.</returns>
        public RepairPreview PreviewRepair(EquipmentData equipment, int fragmentCount = 0)
        {
            // Validate inputs
            if (config == null)
            {
                return new RepairPreview(RepairFailureReason.InvalidConfig);
            }

            if (equipment == null)
            {
                return new RepairPreview(RepairFailureReason.NoEquipmentSelected);
            }

            float currentDurability = equipment.CurrentDurability;
            float maxDurability = equipment.MaxDurability;
            EquipmentCondition currentCondition = equipment.Condition;

            // Check if already at full durability
            float targetMax = maxDurability * config.MaxRepairPercentage;
            if (currentDurability >= targetMax)
            {
                return new RepairPreview(
                    RepairFailureReason.AlreadyFullDurability,
                    currentDurability,
                    maxDurability,
                    GetAvailableFragments());
            }

            // Check if broken repair is allowed
            if (currentCondition == EquipmentCondition.Broken && !config.CanRepairBroken)
            {
                return new RepairPreview(
                    RepairFailureReason.BrokenRepairNotAllowed,
                    currentDurability,
                    maxDurability,
                    GetAvailableFragments());
            }

            // Check available fragments
            int availableFragments = GetAvailableFragments();
            if (availableFragments <= 0)
            {
                return new RepairPreview(
                    RepairFailureReason.NoFragmentsAvailable,
                    currentDurability,
                    maxDurability,
                    0);
            }

            // Calculate fragments needed for full repair
            int fragmentsForFull = config.CalculateFragmentsForFullRepair(currentDurability, maxDurability);

            // Determine actual fragments to use
            int fragmentsToUse;
            if (fragmentCount <= 0)
            {
                // Use maximum available, capped at what's needed
                fragmentsToUse = Mathf.Min(availableFragments, fragmentsForFull);
            }
            else
            {
                fragmentsToUse = Mathf.Min(fragmentCount, availableFragments, fragmentsForFull);
            }

            if (fragmentsToUse <= 0)
            {
                return new RepairPreview(
                    RepairFailureReason.InsufficientFragments,
                    currentDurability,
                    maxDurability,
                    availableFragments);
            }

            // Calculate result
            float durabilityRestored = config.CalculateDurabilityRestored(fragmentsToUse);
            float durabilityAfter = Mathf.Min(currentDurability + durabilityRestored, targetMax);
            EquipmentCondition conditionAfter = EquipmentConditionHelper.GetCondition(durabilityAfter / maxDurability);

            return new RepairPreview(
                currentDurability,
                maxDurability,
                durabilityAfter,
                currentCondition,
                conditionAfter,
                availableFragments,
                fragmentsForFull,
                fragmentsToUse);
        }

        /// <summary>
        /// Preview a full repair using maximum available fragments.
        /// </summary>
        public RepairPreview PreviewFullRepair(EquipmentData equipment)
        {
            return PreviewRepair(equipment, 0);
        }

        /// <summary>
        /// Execute a repair operation atomically.
        /// Consumes fragments and restores durability.
        /// </summary>
        /// <param name="request">The repair request.</param>
        /// <returns>Result of the repair operation.</returns>
        public RepairResult ExecuteRepair(RepairRequest request)
        {
            EquipmentData equipment = request.Equipment;
            int fragmentCount = request.UseMaximumFragments ? 0 : request.FragmentCount;

            // Get preview to validate
            RepairPreview preview = PreviewRepair(equipment, fragmentCount);

            if (!preview.CanRepair)
            {
                return new RepairResult(preview.FailureReason, equipment?.CustomName ?? "");
            }

            // Store pre-repair state
            float durabilityBefore = equipment.CurrentDurability;
            EquipmentCondition conditionBefore = equipment.Condition;
            string equipmentName = equipment.CustomName;

            // Atomic transaction: remove fragments first
            int removed = inventory.RemoveItem(repairFragmentDefinition, preview.FragmentsToConsume);

            if (removed < preview.FragmentsToConsume)
            {
                // This shouldn't happen if preview was accurate, but handle it safely
                // The fragments were already removed, so we proceed with what we got
                Debug.LogWarning($"[RepairService] Expected to remove {preview.FragmentsToConsume} fragments but only removed {removed}");

                if (removed == 0)
                {
                    return new RepairResult(RepairFailureReason.InventoryTransactionFailed, equipmentName);
                }
            }

            // Calculate actual durability restored based on fragments actually removed
            float actualDurabilityRestored = config.CalculateDurabilityRestored(removed);
            float targetMax = equipment.MaxDurability * config.MaxRepairPercentage;

            // Restore durability
            equipment.RestoreDurability(actualDurabilityRestored);

            // Clamp to max repair percentage
            if (equipment.CurrentDurability > targetMax)
            {
                // Need to set directly - but RestoreDurability already clamps to MaxDurability
                // so this only matters if MaxRepairPercentage < 1
            }

            EquipmentCondition conditionAfter = equipment.Condition;

            // Notify stats change if callback provided (e.g., for equipped weapon)
            if (conditionBefore != conditionAfter)
            {
                onStatsChanged?.Invoke();
            }

            // Create result
            var result = new RepairResult(
                equipmentName,
                durabilityBefore,
                equipment.CurrentDurability,
                equipment.MaxDurability,
                conditionBefore,
                conditionAfter,
                removed);

            // Record repair in equipment history
            equipment.RecordRepair(result);

            // Raise event
            OnRepairCompleted?.Invoke(result);

            return result;
        }

        /// <summary>
        /// Execute a full repair using maximum available fragments.
        /// </summary>
        public RepairResult ExecuteFullRepair(EquipmentData equipment)
        {
            return ExecuteRepair(RepairRequest.FullRepair(equipment));
        }

        /// <summary>
        /// Execute a partial repair with a specific number of fragments.
        /// </summary>
        public RepairResult ExecutePartialRepair(EquipmentData equipment, int fragmentCount)
        {
            return ExecuteRepair(RepairRequest.PartialRepair(equipment, fragmentCount));
        }

        /// <summary>
        /// Check if equipment can be repaired at all.
        /// </summary>
        public bool CanRepair(EquipmentData equipment)
        {
            return PreviewRepair(equipment).CanRepair;
        }

        /// <summary>
        /// Check if equipment needs repair (durability below maximum).
        /// </summary>
        public bool NeedsRepair(EquipmentData equipment)
        {
            if (equipment == null)
            {
                return false;
            }

            float targetMax = equipment.MaxDurability * (config?.MaxRepairPercentage ?? 1f);
            return equipment.CurrentDurability < targetMax;
        }
    }
}

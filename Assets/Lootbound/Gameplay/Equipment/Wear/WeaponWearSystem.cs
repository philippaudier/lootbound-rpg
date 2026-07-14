using System;
using System.Collections.Generic;

namespace Lootbound.Gameplay.Equipment
{
    /// <summary>
    /// Pure C# system for applying weapon wear.
    /// Handles probabilistic wear, attack ID tracking, and condition changes.
    /// </summary>
    public class WeaponWearSystem
    {
        private readonly WeaponWearConfig config;
        private readonly HashSet<int> processedAttackIds;
        private readonly Random random;

        /// <summary>
        /// Fired when wear is successfully applied.
        /// </summary>
        public event Action<WearResult> OnWearApplied;

        /// <summary>
        /// Fired when equipment condition changes.
        /// </summary>
        public event Action<WearResult> OnConditionChanged;

        /// <summary>
        /// Create a new wear system with the given configuration.
        /// </summary>
        /// <param name="config">Wear configuration.</param>
        /// <param name="seed">Optional seed for deterministic random. Null uses system time.</param>
        public WeaponWearSystem(WeaponWearConfig config, int? seed = null)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.processedAttackIds = new HashSet<int>();
            this.random = seed.HasValue ? new Random(seed.Value) : new Random();
        }

        /// <summary>
        /// Reset tracking for a new attack cycle.
        /// Call this at the start of each new attack.
        /// </summary>
        public void ResetForNewAttack()
        {
            processedAttackIds.Clear();
        }

        /// <summary>
        /// Try to apply wear to equipment based on context.
        /// </summary>
        /// <param name="equipment">The equipment to potentially wear.</param>
        /// <param name="context">Context describing the wear event.</param>
        /// <returns>Result of the wear attempt.</returns>
        public WearResult TryApplyWear(EquipmentData equipment, WearContext context)
        {
            if (equipment == null || !equipment.IsValid)
            {
                return WearResult.NoWear(EquipmentCondition.Broken, "Invalid");
            }

            string equipmentName = equipment.CustomName ?? equipment.DefinitionId;
            EquipmentCondition currentCondition = equipment.Condition;

            // Already broken - no further wear
            if (currentCondition == EquipmentCondition.Broken)
            {
                return WearResult.NoWear(currentCondition, equipmentName);
            }

            // Check attack ID tracking for attack-based wear
            if (context.AttackId > 0 && context.Cause == WeaponWearCause.SuccessfulHit)
            {
                if (processedAttackIds.Contains(context.AttackId))
                {
                    return WearResult.Skipped(currentCondition, equipmentName);
                }
            }

            // Roll for wear
            float chance = config.GetChance(context.Cause);
            float roll = (float)random.NextDouble();

            if (roll >= chance)
            {
                return WearResult.NoWear(currentCondition, equipmentName);
            }

            // Apply wear
            float amount = config.GetAmount(context.Cause);
            EquipmentCondition conditionBefore = equipment.Condition;

            equipment.ReduceDurability(amount);

            EquipmentCondition conditionAfter = equipment.Condition;

            // Mark attack as processed for attack-based wear
            if (context.AttackId > 0 && context.Cause == WeaponWearCause.SuccessfulHit)
            {
                processedAttackIds.Add(context.AttackId);
            }

            var result = WearResult.Applied(amount, conditionBefore, conditionAfter, equipmentName);

            // Fire events
            OnWearApplied?.Invoke(result);

            if (result.ConditionChanged)
            {
                OnConditionChanged?.Invoke(result);
            }

            return result;
        }

        /// <summary>
        /// Try to apply wear for a successful hit, including heavy target check.
        /// </summary>
        /// <param name="equipment">The equipment to potentially wear.</param>
        /// <param name="attackId">Unique ID for this attack.</param>
        /// <param name="targetMaxHp">Maximum HP of the hit target.</param>
        /// <returns>Result of the wear attempt (combined if both triggers apply).</returns>
        public WearResult TryApplyHitWear(EquipmentData equipment, int attackId, float targetMaxHp)
        {
            if (equipment == null || !equipment.IsValid)
            {
                return WearResult.NoWear(EquipmentCondition.Broken, "Invalid");
            }

            string equipmentName = equipment.CustomName ?? equipment.DefinitionId;
            EquipmentCondition conditionBefore = equipment.Condition;

            // Already broken - no further wear
            if (conditionBefore == EquipmentCondition.Broken)
            {
                return WearResult.NoWear(conditionBefore, equipmentName);
            }

            // Check attack ID tracking
            if (attackId > 0 && processedAttackIds.Contains(attackId))
            {
                return WearResult.Skipped(conditionBefore, equipmentName);
            }

            float totalDurabilityLost = 0f;
            bool anyWearApplied = false;

            // Roll for successful hit wear
            float hitChance = config.GetChance(WeaponWearCause.SuccessfulHit);
            float hitRoll = (float)random.NextDouble();

            if (hitRoll < hitChance)
            {
                float hitAmount = config.GetAmount(WeaponWearCause.SuccessfulHit);
                equipment.ReduceDurability(hitAmount);
                totalDurabilityLost += hitAmount;
                anyWearApplied = true;
            }

            // Additionally roll for heavy target
            if (config.IsHeavyTarget(targetMaxHp))
            {
                float heavyChance = config.GetChance(WeaponWearCause.HeavyTargetHit);
                float heavyRoll = (float)random.NextDouble();

                if (heavyRoll < heavyChance)
                {
                    float heavyAmount = config.GetAmount(WeaponWearCause.HeavyTargetHit);
                    equipment.ReduceDurability(heavyAmount);
                    totalDurabilityLost += heavyAmount;
                    anyWearApplied = true;
                }
            }

            // Mark attack as processed
            if (attackId > 0)
            {
                processedAttackIds.Add(attackId);
            }

            if (!anyWearApplied)
            {
                return WearResult.NoWear(conditionBefore, equipmentName);
            }

            EquipmentCondition conditionAfter = equipment.Condition;
            var result = WearResult.Applied(totalDurabilityLost, conditionBefore, conditionAfter, equipmentName);

            // Fire events
            OnWearApplied?.Invoke(result);

            if (result.ConditionChanged)
            {
                OnConditionChanged?.Invoke(result);
            }

            return result;
        }

        /// <summary>
        /// Apply debug wear (always applies, no chance roll).
        /// </summary>
        public WearResult ApplyDebugWear(EquipmentData equipment)
        {
            if (equipment == null || !equipment.IsValid)
            {
                return WearResult.NoWear(EquipmentCondition.Broken, "Invalid");
            }

            string equipmentName = equipment.CustomName ?? equipment.DefinitionId;
            EquipmentCondition conditionBefore = equipment.Condition;

            if (conditionBefore == EquipmentCondition.Broken)
            {
                return WearResult.NoWear(conditionBefore, equipmentName);
            }

            float amount = config.GetAmount(WeaponWearCause.Debug);
            equipment.ReduceDurability(amount);

            EquipmentCondition conditionAfter = equipment.Condition;
            var result = WearResult.Applied(amount, conditionBefore, conditionAfter, equipmentName);

            OnWearApplied?.Invoke(result);

            if (result.ConditionChanged)
            {
                OnConditionChanged?.Invoke(result);
            }

            return result;
        }

        /// <summary>
        /// Check if an attack ID has already been processed.
        /// </summary>
        public bool IsAttackProcessed(int attackId)
        {
            return processedAttackIds.Contains(attackId);
        }

        /// <summary>
        /// Number of attacks processed since last reset.
        /// </summary>
        public int ProcessedAttackCount => processedAttackIds.Count;
    }
}

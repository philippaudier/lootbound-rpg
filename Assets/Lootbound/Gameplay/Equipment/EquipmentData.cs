using System;
using System.Collections.Generic;
using UnityEngine;
using Lootbound.Gameplay.Inventory;

namespace Lootbound.Gameplay.Equipment
{
    /// <summary>
    /// Unique instance data for a piece of equipment.
    /// Attached to ItemInstance to provide equipment identity.
    /// </summary>
    [Serializable]
    public class EquipmentData
    {
        [SerializeField] private string instanceId;
        [SerializeField] private string definitionId;
        [SerializeField] private string customName;
        [SerializeField] private ItemRarity rarity;
        [SerializeField] private List<AffixInstance> affixes;
        [SerializeField] private EquipmentHistory history;
        [SerializeField] private bool isEquipped;
        [SerializeField] private float currentDurability;
        [SerializeField] private float maxDurability;
        [SerializeField] private int attunementLevel;
        [SerializeField] private int consecutiveAttunementFailures;

        /// <summary>
        /// Unique identifier for this specific equipment instance.
        /// </summary>
        public string InstanceId => instanceId;

        /// <summary>
        /// ID of the weapon/equipment definition.
        /// </summary>
        public string DefinitionId => definitionId;

        /// <summary>
        /// Custom or generated name for this equipment.
        /// </summary>
        public string CustomName => customName;

        /// <summary>
        /// Rarity of this specific instance.
        /// </summary>
        public ItemRarity Rarity => rarity;

        /// <summary>
        /// Affixes rolled on this equipment.
        /// </summary>
        public IReadOnlyList<AffixInstance> Affixes => affixes;

        /// <summary>
        /// History of this equipment.
        /// </summary>
        public EquipmentHistory History => history;

        /// <summary>
        /// Whether this equipment is currently equipped.
        /// </summary>
        public bool IsEquipped
        {
            get => isEquipped;
            set => isEquipped = value;
        }

        /// <summary>
        /// Current durability of this equipment.
        /// </summary>
        public float CurrentDurability => currentDurability;

        /// <summary>
        /// Maximum durability of this equipment.
        /// </summary>
        public float MaxDurability => maxDurability;

        /// <summary>
        /// Normalized durability (0-1).
        /// </summary>
        public float NormalizedDurability => maxDurability > 0f ? currentDurability / maxDurability : 0f;

        /// <summary>
        /// Current condition based on durability.
        /// </summary>
        public EquipmentCondition Condition => EquipmentConditionHelper.GetCondition(NormalizedDurability);

        /// <summary>
        /// Current attunement level (0 to MaximumAttunementLevel).
        /// </summary>
        public int AttunementLevel => attunementLevel;

        /// <summary>
        /// Maximum attunement level for this equipment.
        /// Uses default value since V1 has no per-weapon variation.
        /// </summary>
        public int MaximumAttunementLevel => AttunementFoundationConfig.DefaultMaximumAttunementLevel;

        /// <summary>
        /// Current attunement state derived from level.
        /// </summary>
        public AttunementState AttunementState => AttunementHelper.GetState(attunementLevel, MaximumAttunementLevel);

        /// <summary>
        /// True if equipment has been attuned (level > 0).
        /// </summary>
        public bool IsAttuned => attunementLevel > 0;

        /// <summary>
        /// True if equipment is at maximum attunement level.
        /// </summary>
        public bool IsAtMaximumAttunement => attunementLevel >= MaximumAttunementLevel;

        /// <summary>
        /// Number of consecutive failed attunement attempts on this equipment.
        /// Used for protection (pity) system.
        /// </summary>
        public int ConsecutiveAttunementFailures => consecutiveAttunementFailures;

        /// <summary>
        /// True if this equipment has accumulated protection from failures.
        /// </summary>
        public bool HasAccumulatedResonance => consecutiveAttunementFailures > 0;

        /// <summary>
        /// Create new equipment data with a fresh GUID.
        /// </summary>
        public EquipmentData(
            string definitionId,
            string customName,
            ItemRarity rarity,
            List<AffixInstance> affixes,
            string foundLocation,
            float durability = 100f,
            int attunementLevel = 0)
        {
            instanceId = Guid.NewGuid().ToString();
            this.definitionId = definitionId;
            this.customName = customName;
            this.rarity = rarity;
            this.affixes = affixes ?? new List<AffixInstance>();
            this.history = new EquipmentHistory(foundLocation);
            this.isEquipped = false;
            this.maxDurability = Mathf.Max(1f, durability);
            this.currentDurability = this.maxDurability;
            this.attunementLevel = AttunementHelper.ClampLevel(attunementLevel);
        }

        /// <summary>
        /// Create from serialized data with existing ID.
        /// </summary>
        public EquipmentData(
            string instanceId,
            string definitionId,
            string customName,
            ItemRarity rarity,
            List<AffixInstance> affixes,
            EquipmentHistory history,
            float currentDurability = 100f,
            float maxDurability = 100f,
            int attunementLevel = 0,
            int consecutiveAttunementFailures = 0)
        {
            this.instanceId = instanceId;
            this.definitionId = definitionId;
            this.customName = customName;
            this.rarity = rarity;
            this.affixes = affixes ?? new List<AffixInstance>();
            this.history = history ?? new EquipmentHistory("Unknown");
            this.isEquipped = false;
            this.maxDurability = Mathf.Max(1f, maxDurability);
            this.currentDurability = Mathf.Clamp(currentDurability, 0f, this.maxDurability);
            this.consecutiveAttunementFailures = Mathf.Max(0, consecutiveAttunementFailures);
            this.attunementLevel = AttunementHelper.ClampLevel(attunementLevel);
        }

        /// <summary>
        /// Get display name (custom name or fallback to definition name).
        /// </summary>
        public string GetDisplayName(IEquipmentRegistry registry)
        {
            if (!string.IsNullOrEmpty(customName))
            {
                return customName;
            }

            var def = registry?.GetWeaponDefinition(definitionId);
            return def != null ? def.DisplayName : definitionId;
        }

        /// <summary>
        /// Get display name with attunement suffix (+N).
        /// Returns name without suffix if level is 0.
        /// </summary>
        public string GetAttunedDisplayName(IEquipmentRegistry registry)
        {
            string baseName = GetDisplayName(registry);
            return AttunementHelper.FormatDisplayName(baseName, attunementLevel);
        }

        /// <summary>
        /// Resolve weapon stats with affixes applied.
        /// </summary>
        public ResolvedWeaponStats ResolveStats(IEquipmentRegistry registry)
        {
            return ResolveStats(registry, null, null);
        }

        /// <summary>
        /// Resolve weapon stats with affixes and optional broken penalties applied.
        /// Resolution order: Base → Affixes → Broken penalties → Final clamp.
        /// </summary>
        /// <param name="registry">Equipment registry for definitions.</param>
        /// <param name="brokenConfig">Optional config for broken weapon penalties.</param>
        public ResolvedWeaponStats ResolveStats(IEquipmentRegistry registry, BrokenWeaponConfig brokenConfig)
        {
            return ResolveStats(registry, brokenConfig, null);
        }

        /// <summary>
        /// Resolve weapon stats with affixes, attunement bonuses, and broken penalties applied.
        /// Resolution order: Base → Affixes → Attunement → Broken penalties → Final clamp.
        /// </summary>
        /// <param name="registry">Equipment registry for definitions.</param>
        /// <param name="brokenConfig">Optional config for broken weapon penalties.</param>
        /// <param name="attunementConfig">Optional config for attunement stat bonuses.</param>
        public ResolvedWeaponStats ResolveStats(
            IEquipmentRegistry registry,
            BrokenWeaponConfig brokenConfig,
            AttunementCoreConfig attunementConfig)
        {
            var def = registry?.GetWeaponDefinition(definitionId);
            if (def == null)
            {
                return ResolvedWeaponStats.Invalid;
            }

            float damage = def.BaseDamage;
            float attackSpeed = def.BaseAttackSpeed;
            float range = def.BaseRange;
            float stagger = def.BaseStagger;

            // Accumulate percentage modifiers from affixes
            float damageBonus = 0f;
            float speedBonus = 0f;
            float rangeBonus = 0f;
            float staggerBonus = 0f;

            foreach (var affix in affixes)
            {
                if (!affix.IsValid) continue;

                float value = affix.GetEffectiveValue(registry);
                var modType = affix.GetModifierType(registry);

                switch (modType)
                {
                    case AffixModifierType.DamagePercent:
                        damageBonus += value;
                        break;
                    case AffixModifierType.AttackSpeedPercent:
                        speedBonus += value;
                        break;
                    case AffixModifierType.RangePercent:
                        rangeBonus += value;
                        break;
                    case AffixModifierType.StaggerPercent:
                        staggerBonus += value;
                        break;
                }
            }

            // Apply affix percentage modifiers
            damage *= 1f + (damageBonus / 100f);
            attackSpeed *= 1f + (speedBonus / 100f);
            range *= 1f + (rangeBonus / 100f);
            stagger *= 1f + (staggerBonus / 100f);

            // Apply attunement multipliers after affixes
            if (attunementConfig != null && attunementLevel > 0)
            {
                (damage, attackSpeed, range, stagger) = attunementConfig.ApplyMultipliers(
                    attunementLevel, damage, attackSpeed, range, stagger);
            }

            // Apply broken penalties after attunement
            if (Condition == EquipmentCondition.Broken && brokenConfig != null)
            {
                (damage, attackSpeed, range, stagger) = brokenConfig.ApplyPenalties(
                    damage, attackSpeed, range, stagger);
            }

            // Clamp values to reasonable limits
            damage = Mathf.Max(1f, damage);
            attackSpeed = Mathf.Clamp(attackSpeed, 0.3f, 3f);
            range = Mathf.Clamp(range, 0.5f, 4f);
            stagger = Mathf.Clamp01(stagger);

            return new ResolvedWeaponStats(damage, attackSpeed, range, stagger);
        }

        /// <summary>
        /// Record a kill with this equipment.
        /// </summary>
        public void RecordKill()
        {
            history?.RecordKill();
        }

        /// <summary>
        /// Record that this equipment was equipped.
        /// </summary>
        public void RecordEquip()
        {
            history?.RecordEquip();
        }

        /// <summary>
        /// Record a repair operation on this equipment.
        /// </summary>
        /// <param name="result">The repair result.</param>
        /// <param name="location">Where the repair took place.</param>
        public void RecordRepair(RepairResult result, string location = "Refuge Workbench")
        {
            history?.RecordRepair(result, location);
        }

        /// <summary>
        /// Set current durability to a specific value.
        /// Clamps to valid range (0 to MaxDurability).
        /// </summary>
        public void SetDurability(float value)
        {
            currentDurability = Mathf.Clamp(value, 0f, maxDurability);
        }

        /// <summary>
        /// Restore durability by a specified amount.
        /// </summary>
        /// <param name="amount">Amount to restore (positive value).</param>
        public void RestoreDurability(float amount)
        {
            if (amount > 0f)
            {
                currentDurability = Mathf.Min(currentDurability + amount, maxDurability);
            }
        }

        /// <summary>
        /// Reduce durability by a specified amount.
        /// </summary>
        /// <param name="amount">Amount to reduce (positive value).</param>
        public void ReduceDurability(float amount)
        {
            if (amount > 0f)
            {
                currentDurability = Mathf.Max(currentDurability - amount, 0f);
            }
        }

        /// <summary>
        /// Set attunement level to a specific value.
        /// Used for debug and future attunement attempt mechanics.
        /// </summary>
        /// <param name="newLevel">Target attunement level.</param>
        /// <param name="maximumLevel">Maximum allowed level (uses default if not specified).</param>
        /// <returns>Result describing the change.</returns>
        public AttunementLevelChangeResult SetAttunementLevel(int newLevel, int maximumLevel = -1)
        {
            if (maximumLevel <= 0)
            {
                maximumLevel = MaximumAttunementLevel;
            }

            int previousLevel = attunementLevel;
            var previousState = AttunementState;

            int clampedLevel = AttunementHelper.ClampLevel(newLevel, maximumLevel);
            bool wasClamped = clampedLevel != newLevel;

            attunementLevel = clampedLevel;
            var currentState = AttunementState;

            return new AttunementLevelChangeResult(
                previousLevel,
                attunementLevel,
                wasClamped,
                previousState,
                currentState);
        }

        /// <summary>
        /// Attempt to increase attunement level by 1.
        /// In V1, this always succeeds if not at maximum.
        /// </summary>
        /// <param name="maximumLevel">Maximum allowed level (uses default if not specified).</param>
        /// <returns>Result describing the attempt outcome.</returns>
        public AttunementAttemptResult TryIncreaseAttunement(int maximumLevel = -1)
        {
            if (maximumLevel <= 0)
            {
                maximumLevel = MaximumAttunementLevel;
            }

            // Already at maximum
            if (attunementLevel >= maximumLevel)
            {
                return AttunementAttemptResult.AlreadyMaximum(attunementLevel, maximumLevel);
            }

            // Success - increase level by 1
            int previousLevel = attunementLevel;
            attunementLevel = Mathf.Min(attunementLevel + 1, maximumLevel);

            return AttunementAttemptResult.Succeeded(previousLevel, attunementLevel, maximumLevel);
        }

        /// <summary>
        /// Increment consecutive failure count after a failed attunement attempt.
        /// </summary>
        public void IncrementAttunementFailures()
        {
            consecutiveAttunementFailures++;
        }

        /// <summary>
        /// Reset consecutive failure count after a successful attunement.
        /// </summary>
        public void ResetAttunementFailures()
        {
            consecutiveAttunementFailures = 0;
        }

        /// <summary>
        /// Set consecutive failure count directly (for debug/testing).
        /// </summary>
        /// <param name="count">Number of failures to set.</param>
        public void SetAttunementFailures(int count)
        {
            consecutiveAttunementFailures = Mathf.Max(0, count);
        }

        /// <summary>
        /// Check if this is valid equipment data.
        /// </summary>
        public bool IsValid =>
            !string.IsNullOrEmpty(instanceId) &&
            !string.IsNullOrEmpty(definitionId);

        /// <summary>
        /// Create a deep copy of this equipment data.
        /// Preserves GUID, attunement level, and all other state.
        /// For true duplication use CloneWithNewId.
        /// </summary>
        public EquipmentData Clone()
        {
            var clonedAffixes = new List<AffixInstance>();
            foreach (var affix in affixes)
            {
                clonedAffixes.Add(new AffixInstance(affix.DefinitionId, affix.RolledValue));
            }

            return new EquipmentData(
                instanceId,
                definitionId,
                customName,
                rarity,
                clonedAffixes,
                history?.Clone(),
                currentDurability,
                maxDurability,
                attunementLevel,
                consecutiveAttunementFailures);
        }

        /// <summary>
        /// Create a copy with a new instance ID.
        /// Attunement level and protection reset for new equipment.
        /// </summary>
        public EquipmentData CloneWithNewId()
        {
            var clonedAffixes = new List<AffixInstance>();
            foreach (var affix in affixes)
            {
                clonedAffixes.Add(new AffixInstance(affix.DefinitionId, affix.RolledValue));
            }

            // New equipment starts at attunement level 0
            return new EquipmentData(
                definitionId,
                customName,
                rarity,
                clonedAffixes,
                history?.FoundLocation ?? "Unknown",
                maxDurability,
                attunementLevel: 0);
        }

        public override string ToString()
        {
            return $"{customName} ({rarity}) [{instanceId[..8]}...]";
        }
    }
}

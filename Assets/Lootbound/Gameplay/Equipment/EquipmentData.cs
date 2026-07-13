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
        /// Create new equipment data with a fresh GUID.
        /// </summary>
        public EquipmentData(
            string definitionId,
            string customName,
            ItemRarity rarity,
            List<AffixInstance> affixes,
            string foundLocation)
        {
            instanceId = Guid.NewGuid().ToString();
            this.definitionId = definitionId;
            this.customName = customName;
            this.rarity = rarity;
            this.affixes = affixes ?? new List<AffixInstance>();
            this.history = new EquipmentHistory(foundLocation);
            this.isEquipped = false;
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
            EquipmentHistory history)
        {
            this.instanceId = instanceId;
            this.definitionId = definitionId;
            this.customName = customName;
            this.rarity = rarity;
            this.affixes = affixes ?? new List<AffixInstance>();
            this.history = history ?? new EquipmentHistory("Unknown");
            this.isEquipped = false;
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
        /// Resolve weapon stats with affixes applied.
        /// </summary>
        public ResolvedWeaponStats ResolveStats(IEquipmentRegistry registry)
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

            // Accumulate percentage modifiers
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

            // Apply percentage modifiers
            damage *= 1f + (damageBonus / 100f);
            attackSpeed *= 1f + (speedBonus / 100f);
            range *= 1f + (rangeBonus / 100f);
            stagger *= 1f + (staggerBonus / 100f);

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
        /// Check if this is valid equipment data.
        /// </summary>
        public bool IsValid =>
            !string.IsNullOrEmpty(instanceId) &&
            !string.IsNullOrEmpty(definitionId);

        /// <summary>
        /// Create a deep copy of this equipment data.
        /// Generates a new instance ID for the copy.
        /// </summary>
        public EquipmentData Clone()
        {
            var clonedAffixes = new List<AffixInstance>();
            foreach (var affix in affixes)
            {
                clonedAffixes.Add(new AffixInstance(affix.DefinitionId, affix.RolledValue));
            }

            // Note: Clone keeps the same GUID - for true duplication use CloneWithNewId
            return new EquipmentData(
                instanceId,
                definitionId,
                customName,
                rarity,
                clonedAffixes,
                history?.Clone());
        }

        /// <summary>
        /// Create a copy with a new instance ID.
        /// </summary>
        public EquipmentData CloneWithNewId()
        {
            var clonedAffixes = new List<AffixInstance>();
            foreach (var affix in affixes)
            {
                clonedAffixes.Add(new AffixInstance(affix.DefinitionId, affix.RolledValue));
            }

            return new EquipmentData(
                definitionId,
                customName,
                rarity,
                clonedAffixes,
                history?.FoundLocation ?? "Unknown");
        }

        public override string ToString()
        {
            return $"{customName} ({rarity}) [{instanceId[..8]}...]";
        }
    }
}

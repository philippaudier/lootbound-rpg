using System;
using UnityEngine;

namespace Lootbound.Gameplay.Equipment
{
    /// <summary>
    /// A rolled instance of an affix on a piece of equipment.
    /// Contains the definition reference and the specific rolled value.
    /// </summary>
    [Serializable]
    public class AffixInstance
    {
        [SerializeField] private string definitionId;
        [SerializeField] private float rolledValue;

        // Runtime reference - not serialized, resolved on demand
        [NonSerialized] private AffixDefinition cachedDefinition;

        /// <summary>
        /// The affix definition ID.
        /// </summary>
        public string DefinitionId => definitionId;

        /// <summary>
        /// The rolled value for this affix instance.
        /// </summary>
        public float RolledValue => rolledValue;

        /// <summary>
        /// Create a new affix instance.
        /// </summary>
        public AffixInstance(AffixDefinition definition, float value)
        {
            definitionId = definition != null ? definition.AffixId : string.Empty;
            rolledValue = value;
            cachedDefinition = definition;
        }

        /// <summary>
        /// Create from serialized data.
        /// </summary>
        public AffixInstance(string definitionId, float value)
        {
            this.definitionId = definitionId;
            this.rolledValue = value;
        }

        /// <summary>
        /// Get the affix definition, resolving from registry if needed.
        /// </summary>
        public AffixDefinition GetDefinition(IEquipmentRegistry registry)
        {
            if (cachedDefinition != null) return cachedDefinition;
            if (registry == null) return null;

            cachedDefinition = registry.GetAffixDefinition(definitionId);
            return cachedDefinition;
        }

        /// <summary>
        /// Get the modifier type from the definition.
        /// </summary>
        public AffixModifierType GetModifierType(IEquipmentRegistry registry)
        {
            var def = GetDefinition(registry);
            return def != null ? def.ModifierType : AffixModifierType.DamagePercent;
        }

        /// <summary>
        /// Get the effective value (negative if affix is a penalty).
        /// </summary>
        public float GetEffectiveValue(IEquipmentRegistry registry)
        {
            var def = GetDefinition(registry);
            if (def == null) return 0f;
            return def.IsNegative ? -Mathf.Abs(rolledValue) : rolledValue;
        }

        /// <summary>
        /// Get formatted description for display.
        /// </summary>
        public string GetFormattedDescription(IEquipmentRegistry registry)
        {
            var def = GetDefinition(registry);
            if (def == null) return $"Unknown affix ({definitionId})";
            return def.FormatDescription(rolledValue);
        }

        /// <summary>
        /// Get display name for this affix.
        /// </summary>
        public string GetDisplayName(IEquipmentRegistry registry)
        {
            var def = GetDefinition(registry);
            return def != null ? def.DisplayName : definitionId;
        }

        /// <summary>
        /// Check if this is a valid affix instance.
        /// </summary>
        public bool IsValid => !string.IsNullOrEmpty(definitionId);

        public override string ToString()
        {
            return $"{definitionId}: {rolledValue:F1}";
        }
    }
}

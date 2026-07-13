using UnityEngine;

namespace Lootbound.Gameplay.Equipment
{
    /// <summary>
    /// Definition of an equipment affix.
    /// Describes a modifier that can be applied to equipment.
    /// </summary>
    [CreateAssetMenu(fileName = "Affix_", menuName = "Lootbound/Equipment/Affix Definition")]
    public class AffixDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Unique identifier for this affix.")]
        [SerializeField] private string affixId;

        [Tooltip("Display name shown in UI.")]
        [SerializeField] private string displayName;

        [Tooltip("Description format. Use {0} for the value.")]
        [SerializeField] private string descriptionFormat = "+{0:F0}%";

        [Header("Modifier")]
        [Tooltip("Which stat this affix modifies.")]
        [SerializeField] private AffixModifierType modifierType;

        [Tooltip("Whether this modifier is negative (penalty).")]
        [SerializeField] private bool isNegative;

        [Header("Value Range")]
        [Tooltip("Minimum value that can be rolled.")]
        [SerializeField] private float minValue = 5f;

        [Tooltip("Maximum value that can be rolled.")]
        [SerializeField] private float maxValue = 15f;

        [Header("Tier")]
        [Tooltip("Tier of this affix (affects when it can appear).")]
        [SerializeField] private AffixTier tier = AffixTier.Minor;

        // Public accessors
        public string AffixId => string.IsNullOrEmpty(affixId) ? name : affixId;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
        public string DescriptionFormat => descriptionFormat;
        public AffixModifierType ModifierType => modifierType;
        public bool IsNegative => isNegative;
        public float MinValue => minValue;
        public float MaxValue => maxValue;
        public AffixTier Tier => tier;

        /// <summary>
        /// Roll a random value within the affix's range.
        /// </summary>
        public float RollValue()
        {
            return Random.Range(minValue, maxValue);
        }

        /// <summary>
        /// Roll a deterministic value using a seed.
        /// </summary>
        public float RollValue(System.Random random)
        {
            return minValue + (float)random.NextDouble() * (maxValue - minValue);
        }

        /// <summary>
        /// Format the description with the given value.
        /// </summary>
        public string FormatDescription(float value)
        {
            float displayValue = isNegative ? -Mathf.Abs(value) : value;
            return string.Format(descriptionFormat, displayValue);
        }

        private void OnValidate()
        {
            if (maxValue < minValue) maxValue = minValue;
            minValue = Mathf.Max(0f, minValue);
            maxValue = Mathf.Max(0f, maxValue);
        }
    }
}

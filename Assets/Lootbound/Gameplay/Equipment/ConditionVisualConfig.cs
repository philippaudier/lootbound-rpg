using UnityEngine;

namespace Lootbound.Gameplay.Equipment
{
    /// <summary>
    /// Configuration for equipment condition visual effects.
    /// Slice 0.7.7: Extracted from hardcoded values for easier tuning.
    /// </summary>
    [CreateAssetMenu(
        fileName = "ConditionVisualConfig",
        menuName = "Lootbound/Equipment/Condition Visual Config")]
    public class ConditionVisualConfig : ScriptableObject
    {
        [Header("Broken Effect")]
        [Tooltip("Desaturation amount when broken (0 = normal, 1 = grayscale)")]
        [SerializeField, Range(0f, 1f)]
        private float brokenDesaturation = 0.6f;

        [Tooltip("Tint color applied when broken")]
        [SerializeField]
        private Color brokenTint = new Color(0.8f, 0.3f, 0.3f, 1f);

        [Tooltip("Tint strength when broken (0 = no tint, 1 = full tint)")]
        [SerializeField, Range(0f, 1f)]
        private float brokenTintStrength = 0.3f;

        [Header("Transition")]
        [Tooltip("Duration of visual transition when condition changes")]
        [SerializeField, Range(0f, 2f)]
        private float transitionDuration = 0.3f;

        /// <summary>
        /// Desaturation level for broken equipment (0 = normal, 1 = grayscale).
        /// </summary>
        public float BrokenDesaturation => brokenDesaturation;

        /// <summary>
        /// Tint color applied to broken equipment.
        /// </summary>
        public Color BrokenTint => brokenTint;

        /// <summary>
        /// Strength of the broken tint effect (0-1).
        /// </summary>
        public float BrokenTintStrength => brokenTintStrength;

        /// <summary>
        /// Duration for visual transitions.
        /// </summary>
        public float TransitionDuration => transitionDuration;

        /// <summary>
        /// Calculate the modified color for broken equipment.
        /// </summary>
        public Color CalculateBrokenColor(Color originalColor)
        {
            // Calculate grayscale (luminance)
            float gray = originalColor.r * 0.299f + originalColor.g * 0.587f + originalColor.b * 0.114f;

            // Interpolate between original and grayscale
            Color desaturated = new Color(
                Mathf.Lerp(originalColor.r, gray, brokenDesaturation),
                Mathf.Lerp(originalColor.g, gray, brokenDesaturation),
                Mathf.Lerp(originalColor.b, gray, brokenDesaturation),
                originalColor.a
            );

            // Apply tint
            Color tinted = Color.Lerp(desaturated, brokenTint, brokenTintStrength);
            tinted.a = originalColor.a;

            return tinted;
        }
    }
}

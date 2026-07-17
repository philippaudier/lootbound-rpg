using System;
using UnityEngine;

namespace Lootbound.Gameplay.Equipment
{
    /// <summary>
    /// Configuration for attunement success chances and protection (pity) system.
    /// Defines base success rates per level and protection mechanics.
    /// </summary>
    [CreateAssetMenu(
        fileName = "AttunementChanceConfig",
        menuName = "Lootbound/Equipment/Attunement Chance Config")]
    public class AttunementChanceConfig : ScriptableObject
    {
        [Header("Base Success Chances")]
        [Tooltip("Success chance for each level transition (index 0 = +0→+1, index 9 = +9→+10)")]
        [SerializeField] private float[] baseChances = new float[]
        {
            1.00f,  // +0 → +1: 100%
            0.90f,  // +1 → +2: 90%
            0.80f,  // +2 → +3: 80%
            0.70f,  // +3 → +4: 70%
            0.60f,  // +4 → +5: 60%
            0.50f,  // +5 → +6: 50%
            0.40f,  // +6 → +7: 40%
            0.30f,  // +7 → +8: 30%
            0.24f,  // +8 → +9: 24%
            0.18f   // +9 → +10: 18%
        };

        [Header("Protection System")]
        [Tooltip("Bonus chance added per consecutive failure (0.05 = +5%)")]
        [SerializeField] private float protectionBonusPerFailure = 0.05f;

        [Tooltip("Maximum protection bonus (0.30 = +30%)")]
        [SerializeField] private float protectionCap = 0.30f;

        [Tooltip("Number of consecutive failures before guaranteed success (0 = disabled)")]
        [SerializeField] private int guaranteeAfterFailures = 6;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;

        /// <summary>
        /// Get the base success chance for a given current level.
        /// </summary>
        /// <param name="currentLevel">Current attunement level (0-9 for valid attempts).</param>
        /// <returns>Base success chance (0.0 to 1.0).</returns>
        public float GetBaseChance(int currentLevel)
        {
            if (baseChances == null || baseChances.Length == 0)
            {
                return 1f;
            }

            int index = Mathf.Clamp(currentLevel, 0, baseChances.Length - 1);
            return Mathf.Clamp01(baseChances[index]);
        }

        /// <summary>
        /// Calculate protection bonus based on consecutive failures.
        /// </summary>
        /// <param name="consecutiveFailures">Number of consecutive failures on this weapon.</param>
        /// <returns>Protection bonus (0.0 to ProtectionCap).</returns>
        public float GetProtectionBonus(int consecutiveFailures)
        {
            if (consecutiveFailures <= 0)
            {
                return 0f;
            }

            float bonus = consecutiveFailures * protectionBonusPerFailure;
            return Mathf.Min(bonus, protectionCap);
        }

        /// <summary>
        /// Calculate effective success chance including protection.
        /// </summary>
        /// <param name="currentLevel">Current attunement level.</param>
        /// <param name="consecutiveFailures">Number of consecutive failures.</param>
        /// <returns>Effective success chance (0.0 to 1.0).</returns>
        public float GetEffectiveChance(int currentLevel, int consecutiveFailures)
        {
            // Check for guarantee first
            if (IsGuaranteed(consecutiveFailures))
            {
                return 1f;
            }

            float baseChance = GetBaseChance(currentLevel);
            float protection = GetProtectionBonus(consecutiveFailures);

            return Mathf.Clamp01(baseChance + protection);
        }

        /// <summary>
        /// Check if the next attempt is guaranteed due to consecutive failures.
        /// </summary>
        /// <param name="consecutiveFailures">Number of consecutive failures.</param>
        /// <returns>True if next attempt is guaranteed success.</returns>
        public bool IsGuaranteed(int consecutiveFailures)
        {
            return guaranteeAfterFailures > 0 && consecutiveFailures >= guaranteeAfterFailures;
        }

        /// <summary>
        /// Protection bonus per consecutive failure.
        /// </summary>
        public float ProtectionBonusPerFailure => protectionBonusPerFailure;

        /// <summary>
        /// Maximum protection bonus.
        /// </summary>
        public float ProtectionCap => protectionCap;

        /// <summary>
        /// Number of failures before guaranteed success.
        /// </summary>
        public int GuaranteeAfterFailures => guaranteeAfterFailures;

        /// <summary>
        /// Whether debug logging is enabled.
        /// </summary>
        public bool EnableDebugLogs => enableDebugLogs;

        /// <summary>
        /// Number of level transitions defined.
        /// </summary>
        public int LevelCount => baseChances?.Length ?? 0;

        /// <summary>
        /// Get protection bonus as a percentage string for UI display.
        /// </summary>
        public string FormatProtectionBonus(int consecutiveFailures)
        {
            float bonus = GetProtectionBonus(consecutiveFailures);
            return $"+{bonus * 100f:F0}%";
        }

        /// <summary>
        /// Get effective chance as a percentage string for UI display.
        /// </summary>
        public string FormatEffectiveChance(int currentLevel, int consecutiveFailures)
        {
            float chance = GetEffectiveChance(currentLevel, consecutiveFailures);
            return $"{chance * 100f:F0}%";
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Ensure chances are valid
            if (baseChances != null)
            {
                for (int i = 0; i < baseChances.Length; i++)
                {
                    baseChances[i] = Mathf.Clamp01(baseChances[i]);
                }
            }

            protectionBonusPerFailure = Mathf.Clamp01(protectionBonusPerFailure);
            protectionCap = Mathf.Clamp01(protectionCap);
            guaranteeAfterFailures = Mathf.Max(0, guaranteeAfterFailures);
        }
#endif
    }
}

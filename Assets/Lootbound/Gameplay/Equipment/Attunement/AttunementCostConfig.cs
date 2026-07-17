using UnityEngine;
using Lootbound.Gameplay.Inventory;

namespace Lootbound.Gameplay.Equipment
{
    /// <summary>
    /// Configuration for attunement attempt costs.
    /// Defines the required resources and consumption rules.
    /// </summary>
    [CreateAssetMenu(fileName = "AttunementCostConfig", menuName = "Lootbound/Equipment/Attunement Cost Config")]
    public class AttunementCostConfig : ScriptableObject
    {
        [Header("Attunement Stone")]
        [Tooltip("The item definition for attunement stones.")]
        [SerializeField] private ItemDefinition attunementStoneDefinition;

        [Header("Cost")]
        [Tooltip("Number of stones required per attempt.")]
        [SerializeField, Range(1, 10)] private int stonesPerAttempt = 1;

        [Tooltip("Consume stones when attempt succeeds.")]
        [SerializeField] private bool consumeStoneOnSuccess = true;

        [Tooltip("Consume stones when attempt fails (future use).")]
        [SerializeField] private bool consumeStoneOnFailure = true;

        [Header("Debug")]
        [Tooltip("Allow debug attempts without consuming stones.")]
        [SerializeField] private bool allowDebugFreeAttempts = true;

        [Tooltip("Enable detailed debug logging.")]
        [SerializeField] private bool enableDebugLogs = false;

        /// <summary>
        /// The attunement stone item definition.
        /// </summary>
        public ItemDefinition AttunementStoneDefinition => attunementStoneDefinition;

        /// <summary>
        /// Number of stones required per attempt.
        /// </summary>
        public int StonesPerAttempt => Mathf.Max(1, stonesPerAttempt);

        /// <summary>
        /// Whether stones are consumed on successful attempts.
        /// </summary>
        public bool ConsumeStoneOnSuccess => consumeStoneOnSuccess;

        /// <summary>
        /// Whether stones are consumed on failed attempts.
        /// </summary>
        public bool ConsumeStoneOnFailure => consumeStoneOnFailure;

        /// <summary>
        /// Whether debug free attempts are allowed.
        /// </summary>
        public bool AllowDebugFreeAttempts => allowDebugFreeAttempts;

        /// <summary>
        /// Whether debug logging is enabled.
        /// </summary>
        public bool EnableDebugLogs => enableDebugLogs;

        /// <summary>
        /// Check if the configuration is valid.
        /// </summary>
        public bool IsValid => attunementStoneDefinition != null && stonesPerAttempt > 0;

        /// <summary>
        /// Get the item ID for attunement stones.
        /// </summary>
        public string AttunementStoneItemId =>
            attunementStoneDefinition != null ? attunementStoneDefinition.ItemId : string.Empty;

        private void OnValidate()
        {
            stonesPerAttempt = Mathf.Max(1, stonesPerAttempt);

            if (attunementStoneDefinition == null)
            {
                Debug.LogWarning($"[AttunementCostConfig] No attunement stone definition assigned.");
            }
        }
    }
}

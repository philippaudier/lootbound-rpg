using System;
using UnityEngine;
using Lootbound.Gameplay.Inventory;

namespace Lootbound.Gameplay.Equipment
{
    /// <summary>
    /// Player component that provides repair functionality.
    /// Connects RepairService with player's inventory and equipment.
    /// </summary>
    public class PlayerRepair : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private RepairConfig repairConfig;
        [SerializeField] private ItemDefinition repairFragmentDefinition;

        [Header("References")]
        [SerializeField] private PlayerInventory playerInventory;
        [SerializeField] private PlayerEquipment playerEquipment;

        private RepairService repairService;

        /// <summary>
        /// Event raised when a repair is completed.
        /// </summary>
        public event Action<RepairResult> OnRepairCompleted;

        /// <summary>
        /// The repair service instance.
        /// </summary>
        public RepairService RepairService => repairService;

        /// <summary>
        /// Repair configuration.
        /// </summary>
        public RepairConfig Config => repairConfig;

        /// <summary>
        /// The repair fragment item definition.
        /// </summary>
        public ItemDefinition FragmentDefinition => repairFragmentDefinition;

        private void Awake()
        {
            ValidateReferences();
        }

        private void Start()
        {
            // Initialize in Start to ensure PlayerInventory.Awake() has created the inventory
            InitializeService();
        }

        private void ValidateReferences()
        {
            if (repairConfig == null)
            {
                Debug.LogError("[PlayerRepair] RepairConfig is not assigned!");
            }

            if (repairFragmentDefinition == null)
            {
                Debug.LogError("[PlayerRepair] RepairFragmentDefinition is not assigned!");
            }

            if (playerInventory == null)
            {
                playerInventory = GetComponent<PlayerInventory>();
                if (playerInventory == null)
                {
                    Debug.LogError("[PlayerRepair] PlayerInventory not found!");
                }
            }

            if (playerEquipment == null)
            {
                playerEquipment = GetComponent<PlayerEquipment>();
                if (playerEquipment == null)
                {
                    Debug.LogError("[PlayerRepair] PlayerEquipment not found!");
                }
            }
        }

        private void InitializeService()
        {
            if (repairConfig == null || repairFragmentDefinition == null || playerInventory == null)
            {
                return;
            }

            repairService = new RepairService(
                repairConfig,
                repairFragmentDefinition,
                playerInventory.Inventory,
                OnEquipmentStatsChanged);

            repairService.OnRepairCompleted += HandleRepairCompleted;
        }

        private void OnDestroy()
        {
            if (repairService != null)
            {
                repairService.OnRepairCompleted -= HandleRepairCompleted;
            }
        }

        private void HandleRepairCompleted(RepairResult result)
        {
            OnRepairCompleted?.Invoke(result);

            if (result.Success)
            {
                Debug.Log($"[PlayerRepair] Repaired {result.EquipmentName}: " +
                    $"{result.DurabilityBefore:F0} → {result.DurabilityAfter:F0} " +
                    $"({result.FragmentsConsumed} fragments)");
            }
        }

        private void OnEquipmentStatsChanged()
        {
            // If the repaired equipment is currently equipped, recalculate stats
            if (playerEquipment != null)
            {
                playerEquipment.RecalculateStats();
            }
        }

        /// <summary>
        /// Get the number of repair fragments available.
        /// </summary>
        public int GetAvailableFragments()
        {
            return repairService?.GetAvailableFragments() ?? 0;
        }

        /// <summary>
        /// Preview repair for the currently equipped weapon.
        /// </summary>
        public RepairPreview PreviewCurrentEquipmentRepair(int fragmentCount = 0)
        {
            if (repairService == null || playerEquipment == null)
            {
                return new RepairPreview(RepairFailureReason.InvalidConfig);
            }

            return repairService.PreviewRepair(playerEquipment.CurrentEquipment, fragmentCount);
        }

        /// <summary>
        /// Preview repair for specific equipment.
        /// </summary>
        public RepairPreview PreviewRepair(EquipmentData equipment, int fragmentCount = 0)
        {
            if (repairService == null)
            {
                return new RepairPreview(RepairFailureReason.InvalidConfig);
            }

            return repairService.PreviewRepair(equipment, fragmentCount);
        }

        /// <summary>
        /// Repair the currently equipped weapon using all available fragments.
        /// </summary>
        public RepairResult RepairCurrentEquipment()
        {
            if (repairService == null || playerEquipment == null)
            {
                return new RepairResult(RepairFailureReason.InvalidConfig);
            }

            return repairService.ExecuteFullRepair(playerEquipment.CurrentEquipment);
        }

        /// <summary>
        /// Repair specific equipment using all available fragments.
        /// </summary>
        public RepairResult RepairEquipment(EquipmentData equipment)
        {
            if (repairService == null)
            {
                return new RepairResult(RepairFailureReason.InvalidConfig);
            }

            return repairService.ExecuteFullRepair(equipment);
        }

        /// <summary>
        /// Repair specific equipment with a specific number of fragments.
        /// </summary>
        public RepairResult RepairEquipment(EquipmentData equipment, int fragmentCount)
        {
            if (repairService == null)
            {
                return new RepairResult(RepairFailureReason.InvalidConfig);
            }

            return repairService.ExecutePartialRepair(equipment, fragmentCount);
        }

        /// <summary>
        /// Check if equipment can be repaired.
        /// </summary>
        public bool CanRepair(EquipmentData equipment)
        {
            return repairService?.CanRepair(equipment) ?? false;
        }

        /// <summary>
        /// Check if equipment needs repair.
        /// </summary>
        public bool NeedsRepair(EquipmentData equipment)
        {
            return repairService?.NeedsRepair(equipment) ?? false;
        }
    }
}

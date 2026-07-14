using UnityEngine;

namespace Lootbound.Gameplay.Equipment
{
    /// <summary>
    /// Configuration for the repair system.
    /// Defines how repair fragments restore equipment durability.
    /// </summary>
    [CreateAssetMenu(fileName = "RepairConfig", menuName = "Lootbound/Equipment/Repair Config")]
    public class RepairConfig : ScriptableObject
    {
        [Header("Fragment Settings")]
        [Tooltip("Amount of durability restored per repair fragment consumed.")]
        [SerializeField] private float durabilityPerFragment = 20f;

        [Header("Repair Rules")]
        [Tooltip("Can broken equipment (0 durability) be repaired?")]
        [SerializeField] private bool canRepairBroken = true;

        [Tooltip("Maximum durability percentage that can be restored. 1.0 = full repair allowed.")]
        [SerializeField, Range(0.5f, 1f)] private float maxRepairPercentage = 1f;

        /// <summary>
        /// Amount of durability restored per repair fragment consumed.
        /// </summary>
        public float DurabilityPerFragment => durabilityPerFragment;

        /// <summary>
        /// Whether broken equipment (0 durability) can be repaired.
        /// </summary>
        public bool CanRepairBroken => canRepairBroken;

        /// <summary>
        /// Maximum durability percentage that can be restored (0.5 to 1.0).
        /// </summary>
        public float MaxRepairPercentage => maxRepairPercentage;

        /// <summary>
        /// Calculate how many fragments are needed to fully repair equipment.
        /// </summary>
        public int CalculateFragmentsForFullRepair(float currentDurability, float maxDurability)
        {
            if (durabilityPerFragment <= 0f || maxDurability <= 0f)
            {
                return 0;
            }

            float targetDurability = maxDurability * maxRepairPercentage;
            float durabilityNeeded = targetDurability - currentDurability;

            if (durabilityNeeded <= 0f)
            {
                return 0;
            }

            return Mathf.CeilToInt(durabilityNeeded / durabilityPerFragment);
        }

        /// <summary>
        /// Calculate durability restored for a given number of fragments.
        /// </summary>
        public float CalculateDurabilityRestored(int fragmentCount)
        {
            if (fragmentCount <= 0 || durabilityPerFragment <= 0f)
            {
                return 0f;
            }

            return fragmentCount * durabilityPerFragment;
        }

        private void OnValidate()
        {
            durabilityPerFragment = Mathf.Max(1f, durabilityPerFragment);
        }
    }
}

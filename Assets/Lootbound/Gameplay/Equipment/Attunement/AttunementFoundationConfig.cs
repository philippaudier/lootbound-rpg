using UnityEngine;

namespace Lootbound.Gameplay.Equipment
{
    /// <summary>
    /// Configuration for the attunement foundation system.
    /// Centralizes the maximum attunement level for V1.
    /// </summary>
    [CreateAssetMenu(fileName = "AttunementFoundationConfig", menuName = "Lootbound/Equipment/Attunement Foundation Config")]
    public class AttunementFoundationConfig : ScriptableObject
    {
        /// <summary>
        /// Default maximum attunement level when no config is available.
        /// </summary>
        public const int DefaultMaximumAttunementLevel = 10;

        [Header("Attunement Limits")]
        [Tooltip("Maximum attunement level equipment can reach.")]
        [SerializeField, Range(1, 15)] private int maximumAttunementLevel = 10;

        /// <summary>
        /// Maximum attunement level equipment can reach.
        /// </summary>
        public int MaximumAttunementLevel => maximumAttunementLevel;

        /// <summary>
        /// Calculate the attunement state from a level.
        /// </summary>
        public AttunementState GetState(int level)
        {
            if (level <= 0)
            {
                return AttunementState.Unattuned;
            }

            if (level >= maximumAttunementLevel)
            {
                return AttunementState.Maximum;
            }

            return AttunementState.Attuned;
        }

        /// <summary>
        /// Check if the given level is the maximum.
        /// </summary>
        public bool IsMaximumLevel(int level)
        {
            return level >= maximumAttunementLevel;
        }

        /// <summary>
        /// Clamp a level to valid bounds.
        /// </summary>
        public int ClampLevel(int level)
        {
            return Mathf.Clamp(level, 0, maximumAttunementLevel);
        }

        private void OnValidate()
        {
            maximumAttunementLevel = Mathf.Max(1, maximumAttunementLevel);
        }
    }
}

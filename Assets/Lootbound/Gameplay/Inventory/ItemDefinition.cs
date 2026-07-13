using UnityEngine;

namespace Lootbound.Gameplay.Inventory
{
    /// <summary>
    /// Immutable definition of an item type.
    /// This is the "template" from which runtime item instances are created.
    /// </summary>
    [CreateAssetMenu(fileName = "Item_", menuName = "Lootbound/Inventory/Item Definition")]
    public class ItemDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Unique identifier for this item type.")]
        [SerializeField] private string itemId;

        [Tooltip("Display name shown in UI.")]
        [SerializeField] private string displayName;

        [Tooltip("Description shown in tooltips.")]
        [SerializeField, TextArea(2, 5)] private string description;

        [Header("Visual")]
        [Tooltip("Icon displayed in inventory slots.")]
        [SerializeField] private Sprite icon;

        [Tooltip("Item rarity for color coding.")]
        [SerializeField] private ItemRarity rarity = ItemRarity.Common;

        [Tooltip("Prefab for world representation.")]
        [SerializeField] private GameObject worldPrefab;

        [Header("Stacking")]
        [Tooltip("Can multiple instances stack in one slot?")]
        [SerializeField] private bool isStackable = true;

        [Tooltip("Maximum stack size (1 = non-stackable).")]
        [SerializeField, Range(1, 999)] private int maxStackSize = 99;

        [Header("Interaction")]
        [Tooltip("Text shown in interaction prompt.")]
        [SerializeField] private string pickupPrompt = "Pick up";

        [Tooltip("Duration to hold for pickup (0 = instant).")]
        [SerializeField, Range(0f, 5f)] private float pickupHoldDuration = 0f;

        // Public accessors
        public string ItemId => string.IsNullOrEmpty(itemId) ? name : itemId;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
        public string Description => description;
        public Sprite Icon => icon;
        public ItemRarity Rarity => rarity;
        public GameObject WorldPrefab => worldPrefab;
        public bool IsStackable => isStackable && maxStackSize > 1;
        public int MaxStackSize => isStackable ? maxStackSize : 1;
        public string PickupPrompt => pickupPrompt;
        public float PickupHoldDuration => pickupHoldDuration;

        /// <summary>
        /// Create a runtime instance of this item.
        /// </summary>
        /// <param name="quantity">Initial stack quantity.</param>
        /// <returns>New item instance.</returns>
        public ItemInstance CreateInstance(int quantity = 1)
        {
            return new ItemInstance(this, Mathf.Clamp(quantity, 1, MaxStackSize));
        }

        /// <summary>
        /// Get color associated with this item's rarity.
        /// </summary>
        public Color GetRarityColor()
        {
            return rarity switch
            {
                ItemRarity.Common => new Color(0.8f, 0.8f, 0.8f),     // Gray
                ItemRarity.Uncommon => new Color(0.2f, 0.8f, 0.2f),   // Green
                ItemRarity.Rare => new Color(0.2f, 0.4f, 1f),         // Blue
                ItemRarity.Epic => new Color(0.6f, 0.2f, 0.8f),       // Purple
                ItemRarity.Legendary => new Color(1f, 0.6f, 0.1f),    // Orange
                _ => Color.white
            };
        }

        private void OnValidate()
        {
            if (maxStackSize < 1) maxStackSize = 1;
            if (!isStackable) maxStackSize = 1;
        }
    }
}

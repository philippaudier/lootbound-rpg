using UnityEngine;
using Lootbound.Gameplay.Interaction;
using Lootbound.Gameplay.Inventory;
using Lootbound.Gameplay.Equipment;

namespace Lootbound.Gameplay.World
{
    /// <summary>
    /// Represents an item in the world that can be picked up.
    /// Implements IInteractable for the interaction system.
    /// Supports both regular items and equipment with preserved identity.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ItemWorldPickup : MonoBehaviour, IInteractable
    {
        [Header("Item")]
        [SerializeField] private ItemDefinition itemDefinition;
        [SerializeField, Range(1, 999)] private int quantity = 1;

        [Header("Interaction")]
        [SerializeField] private bool overrideHoldDuration;
        [SerializeField, Range(0f, 5f)] private float customHoldDuration = 0f;

        [Header("Visuals")]
        [SerializeField] private bool rotateOnGround = true;
        [SerializeField] private float rotationSpeed = 30f;
        [SerializeField] private bool bobUpDown = true;
        [SerializeField] private float bobAmplitude = 0.1f;
        [SerializeField] private float bobFrequency = 1f;

        private Vector3 initialPosition;
        private float bobOffset;
        private bool isPickedUp;
        private bool isInteractionInProgress;

        // For equipment items, preserve the full instance
        private ItemInstance storedInstance;

        // IInteractable implementation
        public string InteractionPrompt
        {
            get
            {
                if (itemDefinition == null) return "Pick up";

                // Use equipment name if available
                string displayName = storedInstance?.EquipmentData?.CustomName
                    ?? itemDefinition.DisplayName;

                return $"{itemDefinition.PickupPrompt} {displayName}";
            }
        }

        /// <summary>
        /// Whether this pickup contains equipment with unique identity.
        /// </summary>
        public bool HasEquipmentData => storedInstance?.HasEquipmentData ?? false;

        /// <summary>
        /// The stored item instance (for equipment).
        /// </summary>
        public ItemInstance StoredInstance => storedInstance;

        public bool CanInteract => !isPickedUp && !isInteractionInProgress && itemDefinition != null;

        public string IconId => itemDefinition?.ItemId;

        public float HoldDuration => overrideHoldDuration
            ? customHoldDuration
            : (itemDefinition?.PickupHoldDuration ?? 0f);

        public Transform InteractionTransform => transform;

        /// <summary>
        /// The item definition for this pickup.
        /// </summary>
        public ItemDefinition ItemDefinition => itemDefinition;

        /// <summary>
        /// Quantity of items in this pickup.
        /// </summary>
        public int Quantity => quantity;

        private void Start()
        {
            initialPosition = transform.position;
            bobOffset = Random.Range(0f, Mathf.PI * 2f);

            // Ensure we have a collider
            var collider = GetComponent<Collider>();
            if (collider != null)
            {
                collider.isTrigger = true;
            }
        }

        private void Update()
        {
            if (isPickedUp) return;

            // Visual effects
            if (rotateOnGround)
            {
                transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
            }

            if (bobUpDown)
            {
                float bob = Mathf.Sin((Time.time * bobFrequency * Mathf.PI * 2f) + bobOffset) * bobAmplitude;
                transform.position = initialPosition + Vector3.up * bob;
            }
        }

        public void OnInteractionStart(PlayerInteractor interactor)
        {
            // Lock to prevent concurrent interactions
            isInteractionInProgress = true;
        }

        public void OnInteractionComplete(PlayerInteractor interactor)
        {
            if (isPickedUp)
            {
                isInteractionInProgress = false;
                return;
            }

            // Find player inventory
            var playerInventory = interactor.GetComponentInParent<PlayerInventory>();
            if (playerInventory == null)
            {
                playerInventory = FindFirstObjectByType<PlayerInventory>();
            }

            if (playerInventory == null)
            {
                Debug.LogWarning("[ItemWorldPickup] No PlayerInventory found!");
                isInteractionInProgress = false;
                return;
            }

            // Handle equipment items with preserved identity
            if (storedInstance != null && storedInstance.HasEquipmentData)
            {
                // Equipment items: try to add the exact instance
                if (playerInventory.Inventory.TryAddItem(storedInstance))
                {
                    isPickedUp = true;
                    Debug.Log($"[ItemWorldPickup] Equipment picked up: {storedInstance.EquipmentData.CustomName} [{storedInstance.EquipmentData.InstanceId[..8]}]");
                    Destroy(gameObject);
                }
                else
                {
                    // Inventory full - equipment cannot be partially picked up
                    Debug.Log("[ItemWorldPickup] Inventory full, cannot pick up equipment");
                    isInteractionInProgress = false;
                }
                return;
            }

            // Handle regular items (stackable)
            int added = playerInventory.AddItem(itemDefinition, quantity);

            if (added > 0)
            {
                // If we added all items, destroy the pickup
                if (added >= quantity)
                {
                    isPickedUp = true;
                    Destroy(gameObject);
                }
                else
                {
                    // Partial pickup - reduce quantity and allow further interaction
                    quantity -= added;
                    initialPosition = transform.position; // Update bob origin
                    isInteractionInProgress = false;
                }
            }
            else
            {
                // Inventory full - allow retry
                isInteractionInProgress = false;
            }
        }

        public void OnInteractionCancel(PlayerInteractor interactor)
        {
            // Unlock interaction on cancel
            isInteractionInProgress = false;
        }

        /// <summary>
        /// Initialize this pickup with an item definition.
        /// </summary>
        public void Initialize(ItemDefinition definition, int amount = 1)
        {
            itemDefinition = definition;
            quantity = Mathf.Max(1, amount);
            storedInstance = null;
            isPickedUp = false;
            isInteractionInProgress = false;
        }

        /// <summary>
        /// Initialize this pickup with a full item instance (for equipment).
        /// </summary>
        public void Initialize(ItemInstance instance)
        {
            if (instance == null || !instance.IsValid)
            {
                Debug.LogWarning("[ItemWorldPickup] Cannot initialize with invalid instance");
                return;
            }

            itemDefinition = instance.Definition;
            quantity = instance.Quantity;
            storedInstance = instance;
            isPickedUp = false;
            isInteractionInProgress = false;
        }

        /// <summary>
        /// Spawn a pickup in the world with a definition and quantity.
        /// </summary>
        public static ItemWorldPickup SpawnPickup(ItemDefinition definition, Vector3 position, int quantity = 1)
        {
            if (definition == null) return null;

            var pickupObj = CreatePickupObject(definition, position);
            var pickup = EnsurePickupComponent(pickupObj);
            pickup.Initialize(definition, quantity);
            return pickup;
        }

        /// <summary>
        /// Spawn a pickup in the world with a full item instance (for equipment).
        /// Preserves equipment identity (GUID, affixes, history).
        /// </summary>
        public static ItemWorldPickup SpawnPickup(ItemInstance instance, Vector3 position)
        {
            if (instance == null || !instance.IsValid) return null;

            var definition = instance.Definition;
            var pickupObj = CreatePickupObject(definition, position);
            var pickup = EnsurePickupComponent(pickupObj);

            // Initialize with full instance to preserve equipment data
            pickup.Initialize(instance);

            // Log for debugging equipment drops
            if (instance.HasEquipmentData)
            {
                Debug.Log($"[ItemWorldPickup] Equipment dropped: {instance.EquipmentData.CustomName} [{instance.EquipmentData.InstanceId[..8]}]");
            }

            return pickup;
        }

        private static GameObject CreatePickupObject(ItemDefinition definition, Vector3 position)
        {
            GameObject pickupObj = null;

            // Use the item's world prefab if available
            if (definition.WorldPrefab != null)
            {
                pickupObj = Instantiate(definition.WorldPrefab, position, Quaternion.identity);
            }

            // Fallback to placeholder if no prefab or instantiation failed (missing reference)
            if (pickupObj == null)
            {
                // Create a simple placeholder
                pickupObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pickupObj.transform.position = position;
                pickupObj.transform.localScale = Vector3.one * 0.3f;

                // Make it a trigger
                var collider = pickupObj.GetComponent<Collider>();
                if (collider != null)
                {
                    collider.isTrigger = true;
                }
            }

            // Set proper name
            pickupObj.name = $"Pickup_{definition.DisplayName}";

            // Set the Interactable layer
            int interactableLayer = LayerMask.NameToLayer("Interactable");
            if (interactableLayer >= 0)
            {
                pickupObj.layer = interactableLayer;
            }
            else
            {
                Debug.LogWarning("[ItemWorldPickup] 'Interactable' layer not found. Pickup may not be detected.");
            }

            return pickupObj;
        }

        private static ItemWorldPickup EnsurePickupComponent(GameObject pickupObj)
        {
            var pickup = pickupObj.GetComponent<ItemWorldPickup>();
            if (pickup == null)
            {
                pickup = pickupObj.AddComponent<ItemWorldPickup>();
            }
            return pickup;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (quantity < 1) quantity = 1;
            if (itemDefinition != null && quantity > itemDefinition.MaxStackSize)
            {
                quantity = itemDefinition.MaxStackSize;
            }
        }

        private void OnDrawGizmosSelected()
        {
            // Use equipment rarity color if available
            Color gizmoColor = Color.yellow;
            if (storedInstance?.EquipmentData != null)
            {
                gizmoColor = GetRarityColor(storedInstance.EquipmentData.Rarity);
            }
            else if (itemDefinition != null)
            {
                gizmoColor = itemDefinition.GetRarityColor();
            }

            Gizmos.color = gizmoColor;
            Gizmos.DrawWireSphere(transform.position, 0.5f);
        }

        private static Color GetRarityColor(ItemRarity rarity)
        {
            return rarity switch
            {
                ItemRarity.Common => new Color(0.8f, 0.8f, 0.8f),
                ItemRarity.Uncommon => new Color(0.2f, 0.8f, 0.2f),
                ItemRarity.Rare => new Color(0.2f, 0.4f, 1f),
                ItemRarity.Epic => new Color(0.6f, 0.2f, 0.8f),
                ItemRarity.Legendary => new Color(1f, 0.6f, 0.1f),
                _ => Color.white
            };
        }
#endif
    }
}

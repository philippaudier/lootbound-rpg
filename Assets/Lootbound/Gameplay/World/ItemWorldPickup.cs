using UnityEngine;
using Lootbound.Gameplay.Interaction;
using Lootbound.Gameplay.Inventory;

namespace Lootbound.Gameplay.World
{
    /// <summary>
    /// Represents an item in the world that can be picked up.
    /// Implements IInteractable for the interaction system.
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

        // IInteractable implementation
        public string InteractionPrompt => itemDefinition != null
            ? $"{itemDefinition.PickupPrompt} {itemDefinition.DisplayName}"
            : "Pick up";

        public bool CanInteract => !isPickedUp && itemDefinition != null;

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
            // Visual feedback can be added here
        }

        public void OnInteractionComplete(PlayerInteractor interactor)
        {
            if (isPickedUp) return;

            // Find player inventory
            var playerInventory = interactor.GetComponentInParent<PlayerInventory>();
            if (playerInventory == null)
            {
                playerInventory = FindObjectOfType<PlayerInventory>();
            }

            if (playerInventory == null)
            {
                Debug.LogWarning("[ItemWorldPickup] No PlayerInventory found!");
                return;
            }

            // Try to add item to inventory
            int added = playerInventory.AddItem(itemDefinition, quantity);

            if (added > 0)
            {
                isPickedUp = true;

                // If we added all items, destroy the pickup
                if (added >= quantity)
                {
                    Destroy(gameObject);
                }
                else
                {
                    // Partial pickup - reduce quantity
                    quantity -= added;
                }
            }
            else
            {
                // Inventory full - could play feedback here
                Debug.Log("[ItemWorldPickup] Inventory full!");
            }
        }

        public void OnInteractionCancel(PlayerInteractor interactor)
        {
            // Reset visual state if needed
        }

        /// <summary>
        /// Initialize this pickup with an item.
        /// </summary>
        public void Initialize(ItemDefinition definition, int amount = 1)
        {
            itemDefinition = definition;
            quantity = Mathf.Max(1, amount);
            isPickedUp = false;
        }

        /// <summary>
        /// Spawn a pickup in the world.
        /// </summary>
        public static ItemWorldPickup SpawnPickup(ItemDefinition definition, Vector3 position, int quantity = 1)
        {
            if (definition == null) return null;

            GameObject pickupObj;

            // Use the item's world prefab if available
            if (definition.WorldPrefab != null)
            {
                pickupObj = Instantiate(definition.WorldPrefab, position, Quaternion.identity);
            }
            else
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

            // Add or get the pickup component
            var pickup = pickupObj.GetComponent<ItemWorldPickup>();
            if (pickup == null)
            {
                pickup = pickupObj.AddComponent<ItemWorldPickup>();
            }

            pickup.Initialize(definition, quantity);
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
            Gizmos.color = itemDefinition != null ? itemDefinition.GetRarityColor() : Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 0.5f);
        }
#endif
    }
}

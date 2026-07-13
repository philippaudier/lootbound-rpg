using UnityEngine;
using UnityEngine.UIElements;
using Lootbound.Gameplay.Inventory;
using Lootbound.Gameplay.Player;
using Lootbound.Gameplay.World;

namespace Lootbound.UI
{
    /// <summary>
    /// Inventory UI using UI Toolkit.
    /// Displays the player's inventory in a grid layout with item details and drop functionality.
    /// </summary>
    public class InventoryUI : MonoBehaviour
    {
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private PlayerInventory playerInventory;
        [SerializeField] private PlayerInputReader inputReader;
        [SerializeField] private PlayerCameraController cameraController;
        [SerializeField] private Transform dropPoint;

        [Header("Templates")]
        [SerializeField] private VisualTreeAsset slotTemplate;

        [Header("Sorting")]
        [SerializeField] private int sortOrder = 100;

        [Header("Drop Settings")]
        [SerializeField] private float dropDistance = 1.5f;
        [SerializeField] private float dropHeightOffset = 0.5f;

        private VisualElement root;
        private VisualElement inventoryPanel;
        private VisualElement slotsContainer;
        private Label titleLabel;
        private Label capacityLabel;
        private Button closeButton;

        // Item details panel
        private VisualElement itemDetails;
        private VisualElement detailsIcon;
        private Label detailsName;
        private Label detailsDescription;
        private Label detailsQuantity;
        private Button dropButton;

        private VisualElement[] slotElements;
        private int selectedSlotIndex = -1;
        private bool isOpen;

        public bool IsOpen => isOpen;

        private void Awake()
        {
            if (uiDocument == null)
            {
                Debug.LogError("[InventoryUI] UIDocument is not assigned!");
                return;
            }

            SetupUI();
        }

        private void OnEnable()
        {
            if (inputReader != null)
            {
                inputReader.OnInventoryToggled += HandleInventoryToggle;
            }

            SubscribeToInventoryEvents();
        }

        private void SubscribeToInventoryEvents()
        {
            if (playerInventory?.Inventory != null)
            {
                playerInventory.Inventory.OnSlotChanged += HandleSlotChanged;
                playerInventory.Inventory.OnInventoryChanged += HandleInventoryChanged;
                Debug.Log("[InventoryUI] Subscribed to inventory events");
            }
        }

        private void OnDisable()
        {
            if (inputReader != null)
            {
                inputReader.OnInventoryToggled -= HandleInventoryToggle;
            }

            if (playerInventory?.Inventory != null)
            {
                playerInventory.Inventory.OnSlotChanged -= HandleSlotChanged;
                playerInventory.Inventory.OnInventoryChanged -= HandleInventoryChanged;
            }
        }

        private void SetupUI()
        {
            root = uiDocument.rootVisualElement;

            // Set sort order for proper layering
            uiDocument.sortingOrder = sortOrder;

            inventoryPanel = root.Q<VisualElement>("inventory-panel");
            slotsContainer = root.Q<VisualElement>("slots-container");
            titleLabel = root.Q<Label>("inventory-title");
            capacityLabel = root.Q<Label>("capacity-label");
            closeButton = root.Q<Button>("close-button");

            // Item details panel
            itemDetails = root.Q<VisualElement>("item-details");
            detailsIcon = root.Q<VisualElement>("details-icon");
            detailsName = root.Q<Label>("details-name");
            detailsDescription = root.Q<Label>("details-description");
            detailsQuantity = root.Q<Label>("details-quantity");
            dropButton = root.Q<Button>("drop-button");

            if (closeButton != null)
            {
                closeButton.clicked += Close;
            }

            if (dropButton != null)
            {
                dropButton.clicked += HandleDropClicked;
            }

            // Hide by default
            if (inventoryPanel != null)
            {
                inventoryPanel.style.display = DisplayStyle.None;
            }

            // Ensure root can receive pointer events when visible
            root.pickingMode = PickingMode.Position;
        }

        private void Start()
        {
            // Create slot elements after inventory is initialized
            if (playerInventory != null && playerInventory.Inventory != null)
            {
                CreateSlotElements();
                RefreshAllSlots();
            }
        }

        private void CreateSlotElements()
        {
            if (slotsContainer == null || playerInventory == null) return;

            var inventory = playerInventory.Inventory;
            int slotCount = inventory.Capacity;

            slotElements = new VisualElement[slotCount];
            slotsContainer.Clear();

            // Set grid columns from config
            if (playerInventory.Config != null)
            {
                slotsContainer.style.flexWrap = Wrap.Wrap;
            }

            for (int i = 0; i < slotCount; i++)
            {
                VisualElement slotElement;

                if (slotTemplate != null)
                {
                    slotElement = slotTemplate.CloneTree();
                    slotElement = slotElement.Q<VisualElement>("slot-root") ?? slotElement;
                }
                else
                {
                    slotElement = CreateDefaultSlotElement();
                }

                slotElement.name = $"slot-{i}";
                slotElement.userData = i;

                // Add click handler
                int slotIndex = i;
                slotElement.RegisterCallback<ClickEvent>(evt => HandleSlotClicked(slotIndex));

                slotsContainer.Add(slotElement);
                slotElements[i] = slotElement;
            }
        }

        private VisualElement CreateDefaultSlotElement()
        {
            var slot = new VisualElement();
            slot.AddToClassList("inventory-slot");

            var icon = new VisualElement();
            icon.name = "slot-icon";
            icon.AddToClassList("slot-icon");
            slot.Add(icon);

            var quantity = new Label();
            quantity.name = "slot-quantity";
            quantity.AddToClassList("slot-quantity");
            slot.Add(quantity);

            return slot;
        }

        private void HandleSlotClicked(int slotIndex)
        {
            if (playerInventory?.Inventory == null) return;

            var slot = playerInventory.Inventory.GetSlot(slotIndex);

            // Deselect if clicking same slot or empty slot
            if (slotIndex == selectedSlotIndex || slot == null || slot.IsEmpty)
            {
                ClearSelection();
                return;
            }

            SelectSlot(slotIndex);
        }

        private void SelectSlot(int slotIndex)
        {
            // Clear previous selection
            if (selectedSlotIndex >= 0 && selectedSlotIndex < slotElements.Length)
            {
                slotElements[selectedSlotIndex].RemoveFromClassList("slot-selected");
            }

            selectedSlotIndex = slotIndex;

            // Add selection class
            if (slotIndex >= 0 && slotIndex < slotElements.Length)
            {
                slotElements[slotIndex].AddToClassList("slot-selected");
            }

            UpdateItemDetails();
        }

        private void ClearSelection()
        {
            if (selectedSlotIndex >= 0 && selectedSlotIndex < slotElements.Length)
            {
                slotElements[selectedSlotIndex].RemoveFromClassList("slot-selected");
            }

            selectedSlotIndex = -1;
            UpdateItemDetails();
        }

        private void UpdateItemDetails()
        {
            if (itemDetails == null) return;

            if (selectedSlotIndex < 0 || playerInventory?.Inventory == null)
            {
                itemDetails.RemoveFromClassList("has-selection");
                return;
            }

            var slot = playerInventory.Inventory.GetSlot(selectedSlotIndex);
            if (slot == null || slot.IsEmpty)
            {
                itemDetails.RemoveFromClassList("has-selection");
                return;
            }

            var item = slot.Item;
            var definition = item.Definition;

            itemDetails.AddToClassList("has-selection");

            if (detailsIcon != null && definition.Icon != null)
            {
                detailsIcon.style.backgroundImage = new StyleBackground(definition.Icon);
            }

            if (detailsName != null)
            {
                detailsName.text = definition.DisplayName;
            }

            if (detailsDescription != null)
            {
                detailsDescription.text = definition.Description;
            }

            if (detailsQuantity != null)
            {
                detailsQuantity.text = item.Quantity > 1 ? $"Quantity: {item.Quantity}" : "";
            }
        }

        private void HandleDropClicked()
        {
            if (selectedSlotIndex < 0 || playerInventory?.Inventory == null) return;

            var slot = playerInventory.Inventory.GetSlot(selectedSlotIndex);
            if (slot == null || slot.IsEmpty) return;

            var item = slot.Item;
            var definition = item.Definition;
            int quantity = item.Quantity;
            int droppedSlotIndex = selectedSlotIndex;

            // Find drop position
            Vector3 dropPosition = GetDropPosition();

            // Remove from inventory
            playerInventory.Inventory.RemoveFromSlot(selectedSlotIndex);

            // Spawn in world
            ItemWorldPickup.SpawnPickup(definition, dropPosition, quantity);

            Debug.Log($"[InventoryUI] Dropped {quantity}x {definition.DisplayName}");

            // Clear selection and refresh UI
            ClearSelection();
            RefreshSlot(droppedSlotIndex);
            UpdateCapacityLabel();
        }

        private Vector3 GetDropPosition()
        {
            Transform origin = dropPoint;
            if (origin == null && Camera.main != null)
            {
                origin = Camera.main.transform;
            }

            if (origin == null)
            {
                return transform.position + Vector3.forward * dropDistance;
            }

            // Calculate drop position in front of the player
            Vector3 forward = origin.forward;
            forward.y = 0;
            forward.Normalize();

            Vector3 dropPos = origin.position + forward * dropDistance;
            dropPos.y = origin.position.y - dropHeightOffset;

            // Simple ground check
            if (Physics.Raycast(dropPos + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 5f))
            {
                dropPos.y = hit.point.y + 0.3f;
            }

            return dropPos;
        }

        private void HandleInventoryToggle()
        {
            if (isOpen)
            {
                Close();
            }
            else
            {
                Open();
            }
        }

        public void Open()
        {
            if (inventoryPanel == null) return;
            if (isOpen) return;

            isOpen = true;
            inventoryPanel.style.display = DisplayStyle.Flex;

            // Unlock cursor for UI interaction
            if (cameraController != null)
            {
                cameraController.UnlockCursor();
            }

            // Disable gameplay input
            if (inputReader != null)
            {
                inputReader.SetInputEnabled(false);
            }

            ClearSelection();
            RefreshAllSlots();
            UpdateCapacityLabel();
        }

        public void Close()
        {
            if (inventoryPanel == null) return;
            if (!isOpen) return;

            isOpen = false;
            inventoryPanel.style.display = DisplayStyle.None;

            // Lock cursor for gameplay
            if (cameraController != null)
            {
                cameraController.LockCursor();
            }

            // Re-enable gameplay input
            if (inputReader != null)
            {
                inputReader.SetInputEnabled(true);
            }

            ClearSelection();
        }

        private void HandleSlotChanged(int slotIndex)
        {
            if (!isOpen) return;
            RefreshSlot(slotIndex);
            UpdateCapacityLabel();

            // Update details if selected slot changed
            if (slotIndex == selectedSlotIndex)
            {
                UpdateItemDetails();
            }
        }

        private void HandleInventoryChanged()
        {
            if (!isOpen) return;
            UpdateCapacityLabel();
        }

        private void RefreshAllSlots()
        {
            if (slotElements == null || playerInventory?.Inventory == null) return;

            for (int i = 0; i < slotElements.Length; i++)
            {
                RefreshSlot(i);
            }
        }

        private void RefreshSlot(int index)
        {
            if (slotElements == null || index < 0 || index >= slotElements.Length) return;
            if (playerInventory?.Inventory == null) return;

            var slotElement = slotElements[index];
            var slot = playerInventory.Inventory.GetSlot(index);

            var iconElement = slotElement.Q<VisualElement>("slot-icon");
            var quantityLabel = slotElement.Q<Label>("slot-quantity");

            if (slot == null || slot.IsEmpty)
            {
                // Empty slot
                if (iconElement != null)
                {
                    iconElement.style.backgroundImage = null;
                }
                if (quantityLabel != null)
                {
                    quantityLabel.text = "";
                }
                slotElement.RemoveFromClassList("slot-filled");
                // Reset rarity border color
                slotElement.style.borderBottomColor = StyleKeyword.Null;
            }
            else
            {
                // Filled slot
                var item = slot.Item;
                var definition = item.Definition;

                if (iconElement != null && definition.Icon != null)
                {
                    iconElement.style.backgroundImage = new StyleBackground(definition.Icon);
                }

                if (quantityLabel != null)
                {
                    quantityLabel.text = item.Quantity > 1 ? item.Quantity.ToString() : "";
                }

                slotElement.AddToClassList("slot-filled");

                // Apply rarity color
                var rarityColor = definition.GetRarityColor();
                slotElement.style.borderBottomColor = rarityColor;
            }
        }

        private void UpdateCapacityLabel()
        {
            if (capacityLabel == null || playerInventory?.Inventory == null) return;

            var inventory = playerInventory.Inventory;
            int occupied = inventory.GetOccupiedSlotCount();
            int total = inventory.Capacity;

            capacityLabel.text = $"{occupied}/{total}";
        }
    }
}

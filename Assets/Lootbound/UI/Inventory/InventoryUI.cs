using UnityEngine;
using UnityEngine.UIElements;
using Lootbound.Gameplay.Inventory;
using Lootbound.Gameplay.Player;
using Lootbound.Gameplay.World;
using Lootbound.Gameplay.Equipment;

namespace Lootbound.UI
{
    /// <summary>
    /// Inventory UI using UI Toolkit.
    /// Displays the player's inventory in a grid layout with item details and drop functionality.
    /// Supports equipment display with stats, affixes, and comparison.
    /// </summary>
    public class InventoryUI : MonoBehaviour
    {
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private PlayerInventory playerInventory;
        [SerializeField] private PlayerInputReader inputReader;
        [SerializeField] private PlayerCameraController cameraController;
        [SerializeField] private Transform dropPoint;

        [Header("Equipment")]
        [SerializeField] private PlayerEquipment playerEquipment;
        [SerializeField] private EquipmentRegistry equipmentRegistry;

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
        private Button equipButton;

        // Equipment-specific elements (created dynamically)
        private VisualElement equipmentStatsContainer;
        private VisualElement affixesContainer;
        private VisualElement historyContainer;
        private VisualElement comparisonContainer;

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

            // Try to find equip button, or create one if it doesn't exist
            equipButton = root.Q<Button>("equip-button");
            if (equipButton == null && itemDetails != null)
            {
                CreateEquipmentUI();
            }

            if (equipButton != null)
            {
                equipButton.clicked += HandleEquipClicked;
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

        private void CreateEquipmentUI()
        {
            if (itemDetails == null) return;

            // Create equipment stats container
            equipmentStatsContainer = new VisualElement();
            equipmentStatsContainer.name = "equipment-stats";
            equipmentStatsContainer.style.marginTop = 8;
            equipmentStatsContainer.style.marginBottom = 8;
            equipmentStatsContainer.style.display = DisplayStyle.None;

            // Create affixes container
            affixesContainer = new VisualElement();
            affixesContainer.name = "affixes";
            affixesContainer.style.marginBottom = 8;
            affixesContainer.style.display = DisplayStyle.None;

            // Create history container
            historyContainer = new VisualElement();
            historyContainer.name = "history";
            historyContainer.style.marginBottom = 8;
            historyContainer.style.display = DisplayStyle.None;

            // Create comparison container
            comparisonContainer = new VisualElement();
            comparisonContainer.name = "comparison";
            comparisonContainer.style.marginTop = 8;
            comparisonContainer.style.paddingTop = 8;
            comparisonContainer.style.borderTopWidth = 1;
            comparisonContainer.style.borderTopColor = new Color(0.4f, 0.4f, 0.5f, 0.5f);
            comparisonContainer.style.display = DisplayStyle.None;

            // Create equip button
            equipButton = new Button();
            equipButton.name = "equip-button";
            equipButton.text = "Equip";
            equipButton.style.height = 32;
            equipButton.style.marginTop = 4;
            equipButton.style.marginBottom = 4;
            equipButton.style.backgroundColor = new Color(0.2f, 0.5f, 0.3f, 0.8f);
            equipButton.style.borderTopLeftRadius = 4;
            equipButton.style.borderTopRightRadius = 4;
            equipButton.style.borderBottomLeftRadius = 4;
            equipButton.style.borderBottomRightRadius = 4;
            equipButton.style.color = Color.white;
            equipButton.style.display = DisplayStyle.None;

            // Insert elements before the drop button
            int dropIndex = itemDetails.IndexOf(dropButton);
            if (dropIndex >= 0)
            {
                itemDetails.Insert(dropIndex, equipmentStatsContainer);
                itemDetails.Insert(dropIndex + 1, affixesContainer);
                itemDetails.Insert(dropIndex + 2, historyContainer);
                itemDetails.Insert(dropIndex + 3, comparisonContainer);
                itemDetails.Insert(dropIndex + 4, equipButton);
            }
            else
            {
                itemDetails.Add(equipmentStatsContainer);
                itemDetails.Add(affixesContainer);
                itemDetails.Add(historyContainer);
                itemDetails.Add(comparisonContainer);
                itemDetails.Add(equipButton);
            }
        }

        private void UpdateItemDetails()
        {
            if (itemDetails == null) return;

            if (selectedSlotIndex < 0 || playerInventory?.Inventory == null)
            {
                itemDetails.RemoveFromClassList("has-selection");
                HideEquipmentUI();
                return;
            }

            var slot = playerInventory.Inventory.GetSlot(selectedSlotIndex);
            if (slot == null || slot.IsEmpty)
            {
                itemDetails.RemoveFromClassList("has-selection");
                HideEquipmentUI();
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
                // Use equipment name if available
                string displayName = item.EquipmentData?.CustomName ?? definition.DisplayName;
                detailsName.text = displayName;

                // Apply rarity color to name
                if (item.HasEquipmentData)
                {
                    detailsName.style.color = GetRarityColor(item.EquipmentData.Rarity);
                }
                else
                {
                    detailsName.style.color = Color.white;
                }
            }

            if (detailsDescription != null)
            {
                detailsDescription.text = definition.Description;
            }

            if (detailsQuantity != null)
            {
                if (item.HasEquipmentData)
                {
                    // Show rarity instead of quantity for equipment
                    detailsQuantity.text = item.EquipmentData.Rarity.ToString();
                    detailsQuantity.style.color = GetRarityColor(item.EquipmentData.Rarity);
                }
                else
                {
                    detailsQuantity.text = item.Quantity > 1 ? $"Quantity: {item.Quantity}" : "";
                    detailsQuantity.style.color = new Color(0.6f, 0.8f, 0.6f);
                }
            }

            // Handle equipment-specific UI
            if (item.HasEquipmentData)
            {
                UpdateEquipmentDetails(item);
            }
            else
            {
                HideEquipmentUI();
            }

            // Update drop button state
            UpdateDropButtonState(item);
        }

        private void UpdateEquipmentDetails(ItemInstance item)
        {
            var equipData = item.EquipmentData;
            if (equipData == null) return;

            // Update stats display
            if (equipmentStatsContainer != null)
            {
                equipmentStatsContainer.Clear();
                equipmentStatsContainer.style.display = DisplayStyle.Flex;

                var stats = equipData.ResolveStats(equipmentRegistry);
                if (stats.IsValid)
                {
                    AddStatLabel(equipmentStatsContainer, "Damage", stats.Damage.ToString("F0"));
                    AddStatLabel(equipmentStatsContainer, "Attack Speed", stats.AttackSpeed.ToString("F2"));
                    AddStatLabel(equipmentStatsContainer, "Range", $"{stats.Range:F1}m");
                }
            }

            // Update affixes display
            if (affixesContainer != null)
            {
                affixesContainer.Clear();
                if (equipData.Affixes.Count > 0)
                {
                    affixesContainer.style.display = DisplayStyle.Flex;
                    foreach (var affix in equipData.Affixes)
                    {
                        AddAffixLabel(affixesContainer, affix);
                    }
                }
                else
                {
                    affixesContainer.style.display = DisplayStyle.None;
                }
            }

            // Update history display
            if (historyContainer != null)
            {
                historyContainer.Clear();
                historyContainer.style.display = DisplayStyle.Flex;

                var historyLabel = new Label(equipData.History.GetSummary());
                historyLabel.style.fontSize = 10;
                historyLabel.style.color = new Color(0.6f, 0.6f, 0.65f);
                historyLabel.style.whiteSpace = WhiteSpace.Normal;
                historyContainer.Add(historyLabel);
            }

            // Update comparison display
            UpdateComparison(item);

            // Update equip button
            if (equipButton != null)
            {
                equipButton.style.display = DisplayStyle.Flex;
                bool isEquipped = playerEquipment?.IsSlotEquipped(selectedSlotIndex) ?? false;

                if (isEquipped)
                {
                    equipButton.text = "Unequip";
                    equipButton.style.backgroundColor = new Color(0.5f, 0.3f, 0.2f, 0.8f);
                }
                else
                {
                    equipButton.text = "Equip";
                    equipButton.style.backgroundColor = new Color(0.2f, 0.5f, 0.3f, 0.8f);
                }
            }
        }

        private void UpdateComparison(ItemInstance selectedItem)
        {
            if (comparisonContainer == null) return;

            // Only show comparison if player has a weapon equipped and selected item is different
            if (playerEquipment == null || !playerEquipment.HasWeaponEquipped)
            {
                comparisonContainer.style.display = DisplayStyle.None;
                return;
            }

            // Don't compare with self
            if (playerEquipment.IsSlotEquipped(selectedSlotIndex))
            {
                comparisonContainer.style.display = DisplayStyle.None;
                return;
            }

            comparisonContainer.Clear();
            comparisonContainer.style.display = DisplayStyle.Flex;

            var currentStats = playerEquipment.CurrentStats;
            var selectedStats = selectedItem.EquipmentData?.ResolveStats(equipmentRegistry) ?? ResolvedWeaponStats.Invalid;

            if (!selectedStats.IsValid)
            {
                comparisonContainer.style.display = DisplayStyle.None;
                return;
            }

            var headerLabel = new Label("vs Equipped");
            headerLabel.style.fontSize = 11;
            headerLabel.style.color = new Color(0.7f, 0.7f, 0.8f);
            headerLabel.style.marginBottom = 4;
            comparisonContainer.Add(headerLabel);

            AddComparisonStat(comparisonContainer, "Damage", currentStats.Damage, selectedStats.Damage);
            AddComparisonStat(comparisonContainer, "Speed", currentStats.AttackSpeed, selectedStats.AttackSpeed);
            AddComparisonStat(comparisonContainer, "Range", currentStats.Range, selectedStats.Range);
        }

        private void AddStatLabel(VisualElement container, string statName, string value)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.marginBottom = 2;

            var nameLabel = new Label(statName);
            nameLabel.style.fontSize = 11;
            nameLabel.style.color = new Color(0.7f, 0.7f, 0.75f);

            var valueLabel = new Label(value);
            valueLabel.style.fontSize = 11;
            valueLabel.style.color = Color.white;

            row.Add(nameLabel);
            row.Add(valueLabel);
            container.Add(row);
        }

        private void AddAffixLabel(VisualElement container, AffixInstance affix)
        {
            var label = new Label();
            string affixName = affix.GetDisplayName(equipmentRegistry);
            string affixDesc = affix.GetFormattedDescription(equipmentRegistry);
            label.text = $"{affixName}: {affixDesc}";
            label.style.fontSize = 11;
            label.style.color = new Color(0.5f, 0.8f, 0.5f);
            label.style.marginBottom = 2;
            container.Add(label);
        }

        private void AddComparisonStat(VisualElement container, string statName, float current, float selected)
        {
            float diff = selected - current;
            if (Mathf.Abs(diff) < 0.01f) return; // Skip if no significant difference

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginBottom = 2;

            var nameLabel = new Label($"{statName}: ");
            nameLabel.style.fontSize = 10;
            nameLabel.style.color = new Color(0.6f, 0.6f, 0.7f);

            string diffText = diff > 0 ? $"+{diff:F1}" : $"{diff:F1}";
            var diffLabel = new Label(diffText);
            diffLabel.style.fontSize = 10;
            diffLabel.style.color = diff > 0 ? new Color(0.4f, 0.8f, 0.4f) : new Color(0.8f, 0.4f, 0.4f);

            row.Add(nameLabel);
            row.Add(diffLabel);
            container.Add(row);
        }

        private void HideEquipmentUI()
        {
            if (equipmentStatsContainer != null)
                equipmentStatsContainer.style.display = DisplayStyle.None;
            if (affixesContainer != null)
                affixesContainer.style.display = DisplayStyle.None;
            if (historyContainer != null)
                historyContainer.style.display = DisplayStyle.None;
            if (comparisonContainer != null)
                comparisonContainer.style.display = DisplayStyle.None;
            if (equipButton != null)
                equipButton.style.display = DisplayStyle.None;
        }

        private void UpdateDropButtonState(ItemInstance item)
        {
            if (dropButton == null) return;

            bool isEquipped = item.HasEquipmentData && (playerEquipment?.IsSlotEquipped(selectedSlotIndex) ?? false);

            if (isEquipped)
            {
                dropButton.SetEnabled(false);
                dropButton.text = "Equipped";
            }
            else
            {
                dropButton.SetEnabled(true);
                dropButton.text = "Drop";
            }
        }

        private Color GetRarityColor(ItemRarity rarity)
        {
            return rarity switch
            {
                ItemRarity.Common => new Color(0.8f, 0.8f, 0.8f),
                ItemRarity.Uncommon => new Color(0.3f, 0.85f, 0.3f),
                ItemRarity.Rare => new Color(0.3f, 0.5f, 1f),
                ItemRarity.Epic => new Color(0.65f, 0.3f, 0.85f),
                ItemRarity.Legendary => new Color(1f, 0.65f, 0.15f),
                _ => Color.white
            };
        }

        private void HandleEquipClicked()
        {
            if (selectedSlotIndex < 0 || playerEquipment == null) return;

            bool isEquipped = playerEquipment.IsSlotEquipped(selectedSlotIndex);

            if (isEquipped)
            {
                playerEquipment.TryUnequip();
            }
            else
            {
                playerEquipment.TryEquip(selectedSlotIndex);
            }

            // Refresh UI
            UpdateItemDetails();
            RefreshAllSlots();
        }

        private void HandleDropClicked()
        {
            if (selectedSlotIndex < 0 || playerInventory?.Inventory == null) return;

            var slot = playerInventory.Inventory.GetSlot(selectedSlotIndex);
            if (slot == null || slot.IsEmpty) return;

            var item = slot.Item;

            // Cannot drop equipped items
            if (item.HasEquipmentData && (playerEquipment?.IsSlotEquipped(selectedSlotIndex) ?? false))
            {
                Debug.Log("[InventoryUI] Cannot drop equipped item");
                return;
            }

            int droppedSlotIndex = selectedSlotIndex;

            // Find drop position
            Vector3 dropPosition = GetDropPosition();

            // Remove from inventory (get the actual item, not a copy)
            var droppedItem = slot.Clear();
            playerInventory.Inventory.NotifySlotChanged(selectedSlotIndex);

            // Spawn in world - use full instance for equipment to preserve identity
            if (droppedItem.HasEquipmentData)
            {
                ItemWorldPickup.SpawnPickup(droppedItem, dropPosition);
                Debug.Log($"[InventoryUI] Dropped equipment: {droppedItem.EquipmentData.CustomName}");
            }
            else
            {
                ItemWorldPickup.SpawnPickup(droppedItem.Definition, dropPosition, droppedItem.Quantity);
                Debug.Log($"[InventoryUI] Dropped {droppedItem.Quantity}x {droppedItem.Definition.DisplayName}");
            }

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

            // Check if this slot contains equipped item
            bool isEquipped = playerEquipment?.IsSlotEquipped(index) ?? false;

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
                slotElement.RemoveFromClassList("slot-equipped");
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
                    // Show "E" for equipped items, quantity for stacks
                    if (isEquipped)
                    {
                        quantityLabel.text = "E";
                    }
                    else
                    {
                        quantityLabel.text = item.Quantity > 1 ? item.Quantity.ToString() : "";
                    }
                }

                slotElement.AddToClassList("slot-filled");

                // Apply equipped class
                if (isEquipped)
                {
                    slotElement.AddToClassList("slot-equipped");
                }
                else
                {
                    slotElement.RemoveFromClassList("slot-equipped");
                }

                // Apply rarity color - use equipment rarity if available
                Color rarityColor;
                if (item.HasEquipmentData)
                {
                    rarityColor = GetRarityColor(item.EquipmentData.Rarity);
                }
                else
                {
                    rarityColor = definition.GetRarityColor();
                }
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

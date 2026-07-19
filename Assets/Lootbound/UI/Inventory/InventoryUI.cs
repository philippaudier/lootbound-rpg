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
        [SerializeField] private PlayerRepair playerRepair;
        [SerializeField] private PlayerWeaponWear playerWeaponWear;

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
        private VisualElement conditionContainer;
        private VisualElement attunementContainer;
        private VisualElement repairContainer;
        private Button repairButton;
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
            }

            // Subscribe to equipment change events for live UI updates
            if (playerWeaponWear != null)
            {
                playerWeaponWear.OnWearApplied += HandleEquipmentChanged;
            }

            if (playerRepair != null)
            {
                playerRepair.OnRepairCompleted += HandleRepairCompleted;
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

            if (playerWeaponWear != null)
            {
                playerWeaponWear.OnWearApplied -= HandleEquipmentChanged;
            }

            if (playerRepair != null)
            {
                playerRepair.OnRepairCompleted -= HandleRepairCompleted;
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

                // The cloned TemplateContainer between the document root and
                // the panel has no definite height, so percent-based bounds
                // (max-height: 92%) never resolve and the centered panel can
                // overflow the screen. Stretch every intermediate container
                // and enforce the bound in pixels from the real root height.
                for (var element = inventoryPanel.parent; element != null && element != root; element = element.parent)
                {
                    element.style.flexGrow = 1;
                }

                root.RegisterCallback<GeometryChangedEvent>(_ => ApplyPanelHeightBound());
                ApplyPanelHeightBound();
            }

            // Start with picking disabled - will enable when opened
            root.pickingMode = PickingMode.Ignore;
        }

        /// <summary>
        /// Caps the panel height in pixels (92% of the resolved root height)
        /// so item details can never spill past the screen edge; the details
        /// column scrolls instead. Re-applied on every root geometry change
        /// (resolution or aspect switches).
        /// </summary>
        private void ApplyPanelHeightBound()
        {
            if (inventoryPanel == null || root == null) return;

            float rootHeight = root.resolvedStyle.height;
            if (float.IsNaN(rootHeight) || rootHeight <= 0f) return;

            inventoryPanel.style.maxHeight = rootHeight * 0.92f;
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

            // Create condition container (durability bar + condition label)
            conditionContainer = new VisualElement();
            conditionContainer.name = "condition";
            conditionContainer.style.marginBottom = 8;
            conditionContainer.style.display = DisplayStyle.None;

            // Create attunement container
            attunementContainer = new VisualElement();
            attunementContainer.name = "attunement";
            attunementContainer.style.marginBottom = 8;
            attunementContainer.style.display = DisplayStyle.None;

            // Create repair container
            repairContainer = new VisualElement();
            repairContainer.name = "repair";
            repairContainer.style.marginBottom = 8;
            repairContainer.style.paddingTop = 8;
            repairContainer.style.paddingBottom = 8;
            repairContainer.style.paddingLeft = 8;
            repairContainer.style.paddingRight = 8;
            repairContainer.style.backgroundColor = new Color(0.15f, 0.18f, 0.15f, 0.9f);
            repairContainer.style.borderTopLeftRadius = 4;
            repairContainer.style.borderTopRightRadius = 4;
            repairContainer.style.borderBottomLeftRadius = 4;
            repairContainer.style.borderBottomRightRadius = 4;
            repairContainer.style.display = DisplayStyle.None;

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

            // Two-column layout: the equipment info is too rich for one
            // endless column. Stats/condition/repair on the left,
            // attunement/affixes/history/comparison on the right; the
            // buttons keep the full width below the columns.
            var columns = new VisualElement();
            columns.name = "details-columns";
            columns.AddToClassList("details-columns");

            var leftColumn = new VisualElement();
            leftColumn.AddToClassList("details-column");
            var rightColumn = new VisualElement();
            rightColumn.AddToClassList("details-column");
            rightColumn.AddToClassList("details-column-right");
            columns.Add(leftColumn);
            columns.Add(rightColumn);

            leftColumn.Add(equipmentStatsContainer);
            leftColumn.Add(conditionContainer);
            leftColumn.Add(repairContainer);
            rightColumn.Add(attunementContainer);
            rightColumn.Add(affixesContainer);
            rightColumn.Add(historyContainer);
            rightColumn.Add(comparisonContainer);

            // Insert before the drop button
            int dropIndex = itemDetails.IndexOf(dropButton);
            if (dropIndex >= 0)
            {
                itemDetails.Insert(dropIndex, columns);
                itemDetails.Insert(dropIndex + 1, equipButton);
            }
            else
            {
                itemDetails.Add(columns);
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
                // Use attuned display name if equipment is attuned, otherwise custom name or definition name
                string displayName;
                if (item.HasEquipmentData && item.EquipmentData.IsAttuned)
                {
                    displayName = item.EquipmentData.GetAttunedDisplayName(equipmentRegistry);
                }
                else
                {
                    displayName = item.EquipmentData?.CustomName ?? definition.DisplayName;
                }
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

            bool isBroken = EquipmentConditionHelper.IsBroken(equipData.Condition);
            var brokenConfig = playerEquipment?.BrokenConfig;

            // Update stats display
            if (equipmentStatsContainer != null)
            {
                equipmentStatsContainer.Clear();
                equipmentStatsContainer.style.display = DisplayStyle.Flex;

                // Resolve stats with attunement bonuses and broken penalties
                var attunementConfig = playerEquipment?.AttunementConfig;
                var stats = equipData.ResolveStats(equipmentRegistry, brokenConfig, attunementConfig);
                if (stats.IsValid)
                {
                    if (isBroken && brokenConfig != null)
                    {
                        // Show stats with penalty indicators
                        AddStatLabelWithPenalty(equipmentStatsContainer, "Damage", stats.Damage, brokenConfig.GetDamagePenaltyPercent());
                        AddStatLabelWithPenalty(equipmentStatsContainer, "Attack Speed", stats.AttackSpeed, brokenConfig.GetSpeedPenaltyPercent());
                        AddStatLabelWithPenalty(equipmentStatsContainer, "Range", stats.Range, brokenConfig.GetRangePenaltyPercent(), "m");
                    }
                    else
                    {
                        AddStatLabel(equipmentStatsContainer, "Damage", stats.Damage.ToString("F0"));
                        AddStatLabel(equipmentStatsContainer, "Attack Speed", stats.AttackSpeed.ToString("F2"));
                        AddStatLabel(equipmentStatsContainer, "Range", $"{stats.Range:F1}m");
                    }
                }
            }

            // Update condition display
            if (conditionContainer != null)
            {
                conditionContainer.Clear();
                conditionContainer.style.display = DisplayStyle.Flex;

                var condition = equipData.Condition;
                var conditionColor = EquipmentConditionHelper.GetConditionColor(condition);
                var conditionTooltip = EquipmentConditionHelper.GetConditionTooltip(condition);

                // Broken warning badge
                if (isBroken)
                {
                    var brokenBadge = new VisualElement();
                    brokenBadge.style.flexDirection = FlexDirection.Row;
                    brokenBadge.style.alignItems = Align.Center;
                    brokenBadge.style.justifyContent = Justify.Center;
                    brokenBadge.style.backgroundColor = new Color(0.5f, 0.15f, 0.15f, 0.9f);
                    brokenBadge.style.paddingTop = 4;
                    brokenBadge.style.paddingBottom = 4;
                    brokenBadge.style.paddingLeft = 8;
                    brokenBadge.style.paddingRight = 8;
                    brokenBadge.style.marginBottom = 6;
                    brokenBadge.style.borderTopLeftRadius = 4;
                    brokenBadge.style.borderTopRightRadius = 4;
                    brokenBadge.style.borderBottomLeftRadius = 4;
                    brokenBadge.style.borderBottomRightRadius = 4;
                    brokenBadge.tooltip = "This weapon is broken and suffers severe combat penalties. Find a way to repair it.";

                    var brokenLabel = new Label("BROKEN - Severe Penalties");
                    brokenLabel.style.fontSize = 11;
                    brokenLabel.style.color = new Color(1f, 0.7f, 0.7f);
                    brokenLabel.style.unityFontStyleAndWeight = FontStyle.Bold;

                    brokenBadge.Add(brokenLabel);
                    conditionContainer.Add(brokenBadge);
                }

                // Condition row with label and durability text
                var conditionRow = new VisualElement();
                conditionRow.style.flexDirection = FlexDirection.Row;
                conditionRow.style.justifyContent = Justify.SpaceBetween;
                conditionRow.style.marginBottom = 4;
                conditionRow.tooltip = conditionTooltip;

                var conditionLabel = new Label("Condition");
                conditionLabel.style.fontSize = 11;
                conditionLabel.style.color = new Color(0.7f, 0.7f, 0.75f);

                var conditionValue = new Label($"{condition} ({equipData.CurrentDurability:F0}/{equipData.MaxDurability:F0})");
                conditionValue.style.fontSize = 11;
                conditionValue.style.color = conditionColor;

                conditionRow.Add(conditionLabel);
                conditionRow.Add(conditionValue);
                conditionContainer.Add(conditionRow);

                // Durability bar
                var barContainer = new VisualElement();
                barContainer.style.height = 4;
                barContainer.style.backgroundColor = new Color(0.15f, 0.15f, 0.2f);
                barContainer.style.borderTopLeftRadius = 2;
                barContainer.style.borderTopRightRadius = 2;
                barContainer.style.borderBottomLeftRadius = 2;
                barContainer.style.borderBottomRightRadius = 2;

                var barFill = new VisualElement();
                barFill.style.height = Length.Percent(100);
                barFill.style.width = Length.Percent(equipData.NormalizedDurability * 100f);
                barFill.style.backgroundColor = conditionColor;
                barFill.style.borderTopLeftRadius = 2;
                barFill.style.borderBottomLeftRadius = 2;

                barContainer.Add(barFill);
                conditionContainer.Add(barContainer);
            }

            // Update attunement display
            if (attunementContainer != null)
            {
                attunementContainer.Clear();
                attunementContainer.style.display = DisplayStyle.Flex;

                // Get max level from config if available
                int maxLevel = playerEquipment?.AttunementConfig != null
                    ? playerEquipment.AttunementConfig.MaximumLevel
                    : equipData.MaximumAttunementLevel;

                // Attunement row
                var attunementRow = new VisualElement();
                attunementRow.style.flexDirection = FlexDirection.Row;
                attunementRow.style.justifyContent = Justify.SpaceBetween;
                attunementRow.style.marginBottom = 4;

                var attunementLabel = new Label("Attunement");
                attunementLabel.style.fontSize = 11;
                attunementLabel.style.color = new Color(0.7f, 0.7f, 0.75f);

                // Show +N / +Max format
                var attunementValue = new Label($"+{equipData.AttunementLevel} / +{maxLevel}");
                attunementValue.style.fontSize = 11;
                // Color based on attunement state
                bool isAtMax = equipData.AttunementLevel >= maxLevel;
                if (isAtMax)
                {
                    attunementValue.style.color = new Color(0.9f, 0.75f, 0.4f); // Gold for max
                }
                else if (equipData.IsAttuned)
                {
                    attunementValue.style.color = new Color(0.5f, 0.7f, 1f); // Blue for attuned
                }
                else
                {
                    attunementValue.style.color = new Color(0.6f, 0.6f, 0.65f); // Gray for unattuned
                }

                attunementRow.Add(attunementLabel);
                attunementRow.Add(attunementValue);
                attunementContainer.Add(attunementRow);

                // Show damage bonus if attuned and config available
                if (equipData.IsAttuned && playerEquipment?.AttunementConfig != null)
                {
                    float bonusPercent = playerEquipment.AttunementConfig.GetDamageBonusPercent(equipData.AttunementLevel);
                    if (bonusPercent > 0)
                    {
                        var bonusRow = new VisualElement();
                        bonusRow.style.flexDirection = FlexDirection.Row;
                        bonusRow.style.justifyContent = Justify.SpaceBetween;

                        var bonusLabel = new Label("Damage Bonus");
                        bonusLabel.style.fontSize = 11;
                        bonusLabel.style.color = new Color(0.7f, 0.7f, 0.75f);

                        var bonusValue = new Label($"+{bonusPercent:F0}%");
                        bonusValue.style.fontSize = 11;
                        bonusValue.style.color = new Color(0.4f, 0.85f, 0.4f); // Green for bonus

                        bonusRow.Add(bonusLabel);
                        bonusRow.Add(bonusValue);
                        attunementContainer.Add(bonusRow);
                    }
                }
            }

            // Update repair panel
            UpdateRepairPanel(item);

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

                // Discovery and usage history
                var historyLabel = new Label(equipData.History.GetSummary());
                historyLabel.style.fontSize = 10;
                historyLabel.style.color = new Color(0.6f, 0.6f, 0.65f);
                historyLabel.style.whiteSpace = WhiteSpace.Normal;
                historyContainer.Add(historyLabel);

                // Repair history (if any)
                if (equipData.History.HasBeenRepaired)
                {
                    var repairLabel = new Label(equipData.History.GetRepairSummary());
                    repairLabel.style.fontSize = 10;
                    repairLabel.style.color = new Color(0.55f, 0.65f, 0.6f);
                    repairLabel.style.whiteSpace = WhiteSpace.Normal;
                    repairLabel.style.marginTop = 2;
                    historyContainer.Add(repairLabel);
                }

                // Attunement history (if any)
                if (equipData.History.HasAttunementHistory)
                {
                    var attunementLabel = new Label(equipData.History.GetAttunementSummary());
                    attunementLabel.style.fontSize = 10;
                    attunementLabel.style.color = new Color(0.6f, 0.55f, 0.7f);
                    attunementLabel.style.whiteSpace = WhiteSpace.Normal;
                    attunementLabel.style.marginTop = 2;
                    historyContainer.Add(attunementLabel);

                    // Show last attempt details if available
                    var attunement = equipData.History.Attunement;
                    if (attunement.LastAttemptTimestamp > 0)
                    {
                        string lastAttemptSummary = attunement.GetLastAttemptSummary();
                        if (!string.IsNullOrEmpty(lastAttemptSummary))
                        {
                            var lastAttemptLabel = new Label($"Last: {lastAttemptSummary}");
                            lastAttemptLabel.style.fontSize = 9;
                            lastAttemptLabel.style.color = new Color(0.55f, 0.5f, 0.6f);
                            lastAttemptLabel.style.whiteSpace = WhiteSpace.Normal;
                            lastAttemptLabel.style.marginTop = 1;
                            historyContainer.Add(lastAttemptLabel);
                        }
                    }
                }
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
            var brokenConfig = playerEquipment?.BrokenConfig;
            var attunementConfig = playerEquipment?.AttunementConfig;
            var selectedStats = selectedItem.EquipmentData?.ResolveStats(equipmentRegistry, brokenConfig, attunementConfig) ?? ResolvedWeaponStats.Invalid;

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

            // Compare attunement levels
            int equippedAttunement = playerEquipment.CurrentEquipment?.AttunementLevel ?? 0;
            int selectedAttunement = selectedItem.EquipmentData?.AttunementLevel ?? 0;
            AddAttunementComparison(comparisonContainer, equippedAttunement, selectedAttunement);

            AddComparisonStat(comparisonContainer, "Damage", currentStats.Damage, selectedStats.Damage);
            AddComparisonStat(comparisonContainer, "Speed", currentStats.AttackSpeed, selectedStats.AttackSpeed);
            AddComparisonStat(comparisonContainer, "Range", currentStats.Range, selectedStats.Range);
        }

        private void AddAttunementComparison(VisualElement container, int equippedLevel, int selectedLevel)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.marginBottom = 2;

            var nameLabel = new Label("Attunement");
            nameLabel.style.fontSize = 11;
            nameLabel.style.color = new Color(0.7f, 0.7f, 0.75f);

            var valueLabel = new Label($"+{selectedLevel}");
            valueLabel.style.fontSize = 11;

            // Color based on comparison
            if (selectedLevel > equippedLevel)
            {
                valueLabel.style.color = new Color(0.5f, 0.9f, 0.5f); // Green for better
            }
            else if (selectedLevel < equippedLevel)
            {
                valueLabel.style.color = new Color(0.9f, 0.5f, 0.5f); // Red for worse
            }
            else
            {
                valueLabel.style.color = new Color(0.7f, 0.7f, 0.75f); // Gray for same
            }

            row.Add(nameLabel);
            row.Add(valueLabel);
            container.Add(row);
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

        private void AddStatLabelWithPenalty(VisualElement container, string statName, float value, int penaltyPercent, string suffix = "")
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.marginBottom = 2;

            var nameLabel = new Label(statName);
            nameLabel.style.fontSize = 11;
            nameLabel.style.color = new Color(0.7f, 0.7f, 0.75f);

            // Value container with stat and penalty
            var valueContainer = new VisualElement();
            valueContainer.style.flexDirection = FlexDirection.Row;
            valueContainer.style.alignItems = Align.Center;

            string valueText = suffix == "m" ? $"{value:F1}{suffix}" : value.ToString("F0");
            var valueLabel = new Label(valueText);
            valueLabel.style.fontSize = 11;
            valueLabel.style.color = new Color(0.9f, 0.5f, 0.5f); // Red-tinted for penalty

            var penaltyLabel = new Label($" ({penaltyPercent}%)");
            penaltyLabel.style.fontSize = 10;
            penaltyLabel.style.color = new Color(0.8f, 0.4f, 0.4f);

            valueContainer.Add(valueLabel);
            valueContainer.Add(penaltyLabel);

            row.Add(nameLabel);
            row.Add(valueContainer);
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

        private void UpdateRepairPanel(ItemInstance item)
        {
            if (repairContainer == null || playerRepair == null) return;

            var equipData = item?.EquipmentData;
            if (equipData == null)
            {
                repairContainer.style.display = DisplayStyle.None;
                return;
            }

            // Get repair preview
            var preview = playerRepair.PreviewRepair(equipData);

            repairContainer.Clear();

            // Only show repair panel if equipment needs repair
            if (!playerRepair.NeedsRepair(equipData))
            {
                repairContainer.style.display = DisplayStyle.None;
                return;
            }

            repairContainer.style.display = DisplayStyle.Flex;

            // Header
            var headerLabel = new Label("Repair");
            headerLabel.style.fontSize = 12;
            headerLabel.style.color = new Color(0.7f, 0.85f, 0.7f);
            headerLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            headerLabel.style.marginBottom = 6;
            repairContainer.Add(headerLabel);

            // Fragment count row
            var fragmentRow = new VisualElement();
            fragmentRow.style.flexDirection = FlexDirection.Row;
            fragmentRow.style.justifyContent = Justify.SpaceBetween;
            fragmentRow.style.marginBottom = 4;

            var fragmentLabel = new Label("Repair Fragments");
            fragmentLabel.style.fontSize = 11;
            fragmentLabel.style.color = new Color(0.7f, 0.7f, 0.75f);

            var fragmentCount = new Label($"{preview.FragmentsAvailable}");
            fragmentCount.style.fontSize = 11;
            fragmentCount.style.color = preview.FragmentsAvailable > 0
                ? new Color(0.5f, 0.9f, 0.5f)
                : new Color(0.9f, 0.5f, 0.5f);

            fragmentRow.Add(fragmentLabel);
            fragmentRow.Add(fragmentCount);
            repairContainer.Add(fragmentRow);

            // Show preview info if repair is possible
            if (preview.CanRepair)
            {
                // Durability preview row
                var durabilityRow = new VisualElement();
                durabilityRow.style.flexDirection = FlexDirection.Row;
                durabilityRow.style.justifyContent = Justify.SpaceBetween;
                durabilityRow.style.marginBottom = 4;

                var durLabel = new Label("Durability");
                durLabel.style.fontSize = 11;
                durLabel.style.color = new Color(0.7f, 0.7f, 0.75f);

                var durValue = new Label($"{preview.CurrentDurability:F0} → {preview.DurabilityAfterRepair:F0}");
                durValue.style.fontSize = 11;
                durValue.style.color = new Color(0.5f, 0.9f, 0.5f);

                durabilityRow.Add(durLabel);
                durabilityRow.Add(durValue);
                repairContainer.Add(durabilityRow);

                // Condition preview (if changing)
                if (preview.WillChangeCondition)
                {
                    var condRow = new VisualElement();
                    condRow.style.flexDirection = FlexDirection.Row;
                    condRow.style.justifyContent = Justify.SpaceBetween;
                    condRow.style.marginBottom = 4;

                    var condLabel = new Label("Condition");
                    condLabel.style.fontSize = 11;
                    condLabel.style.color = new Color(0.7f, 0.7f, 0.75f);

                    var condAfterColor = EquipmentConditionHelper.GetConditionColor(preview.ConditionAfter);
                    var condValue = new Label($"{preview.ConditionBefore} → {preview.ConditionAfter}");
                    condValue.style.fontSize = 11;
                    condValue.style.color = condAfterColor;

                    condRow.Add(condLabel);
                    condRow.Add(condValue);
                    repairContainer.Add(condRow);
                }

                // Fragments needed row
                var neededRow = new VisualElement();
                neededRow.style.flexDirection = FlexDirection.Row;
                neededRow.style.justifyContent = Justify.SpaceBetween;
                neededRow.style.marginBottom = 8;

                var neededLabel = new Label("Fragments to use");
                neededLabel.style.fontSize = 11;
                neededLabel.style.color = new Color(0.7f, 0.7f, 0.75f);

                string fragmentsText = preview.FragmentsToConsume == preview.FragmentsForFullRepair
                    ? $"{preview.FragmentsToConsume} (full repair)"
                    : $"{preview.FragmentsToConsume} / {preview.FragmentsForFullRepair} needed";
                var neededValue = new Label(fragmentsText);
                neededValue.style.fontSize = 11;
                neededValue.style.color = Color.white;

                neededRow.Add(neededLabel);
                neededRow.Add(neededValue);
                repairContainer.Add(neededRow);

                // Repair button
                repairButton = new Button(HandleRepairClicked);
                repairButton.text = preview.IsFullRepair ? "Full Repair" : "Repair";
                repairButton.style.height = 28;
                repairButton.style.backgroundColor = new Color(0.2f, 0.5f, 0.3f, 0.9f);
                repairButton.style.borderTopLeftRadius = 4;
                repairButton.style.borderTopRightRadius = 4;
                repairButton.style.borderBottomLeftRadius = 4;
                repairButton.style.borderBottomRightRadius = 4;
                repairButton.style.color = Color.white;
                repairContainer.Add(repairButton);
            }
            else
            {
                // Show why repair cannot proceed
                string reason = GetRepairFailureMessage(preview.FailureReason);
                var reasonLabel = new Label(reason);
                reasonLabel.style.fontSize = 11;
                reasonLabel.style.color = new Color(0.9f, 0.6f, 0.4f);
                reasonLabel.style.whiteSpace = WhiteSpace.Normal;
                reasonLabel.style.marginTop = 4;
                repairContainer.Add(reasonLabel);
            }
        }

        private string GetRepairFailureMessage(RepairFailureReason reason)
        {
            return reason switch
            {
                RepairFailureReason.NoFragmentsAvailable => "No repair fragments available.",
                RepairFailureReason.InsufficientFragments => "Not enough repair fragments.",
                RepairFailureReason.AlreadyFullDurability => "Already at full durability.",
                RepairFailureReason.BrokenRepairNotAllowed => "This equipment cannot be repaired.",
                RepairFailureReason.InvalidConfig => "Repair system not configured.",
                _ => "Cannot repair."
            };
        }

        private void HandleRepairClicked()
        {
            if (selectedSlotIndex < 0 || playerRepair == null || playerInventory?.Inventory == null)
            {
                return;
            }

            var slot = playerInventory.Inventory.GetSlot(selectedSlotIndex);
            if (slot == null || slot.IsEmpty || !slot.Item.HasEquipmentData)
            {
                return;
            }

            var equipData = slot.Item.EquipmentData;
            var result = playerRepair.RepairEquipment(equipData);

            if (result.Success)
            {
                Debug.Log($"[InventoryUI] Repaired {result.EquipmentName}: " +
                    $"{result.DurabilityBefore:F0} → {result.DurabilityAfter:F0} " +
                    $"(used {result.FragmentsConsumed} fragments)");

                // Refresh the UI to show updated condition
                UpdateItemDetails();
                RefreshAllSlots();
            }
            else
            {
                Debug.LogWarning($"[InventoryUI] Repair failed: {result.FailureReason}");
            }
        }

        private void HideEquipmentUI()
        {
            if (equipmentStatsContainer != null)
                equipmentStatsContainer.style.display = DisplayStyle.None;
            if (conditionContainer != null)
                conditionContainer.style.display = DisplayStyle.None;
            if (attunementContainer != null)
                attunementContainer.style.display = DisplayStyle.None;
            if (repairContainer != null)
                repairContainer.style.display = DisplayStyle.None;
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

            // Enable pointer events on this UI
            root.pickingMode = PickingMode.Position;

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

            // Disable pointer events so other UIs can receive them
            root.pickingMode = PickingMode.Ignore;

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
            RefreshAllSlots();

            // Refresh item details to update repair panel (fragment count may have changed)
            if (selectedSlotIndex >= 0)
            {
                UpdateItemDetails();
            }
        }

        private void HandleEquipmentChanged(WearResult result)
        {
            if (!isOpen) return;

            // Refresh the slot and details when equipment durability changes
            RefreshAllSlots();
            if (selectedSlotIndex >= 0)
            {
                UpdateItemDetails();
            }
        }

        private void HandleRepairCompleted(RepairResult result)
        {
            if (!isOpen) return;

            // Refresh after repair to show updated durability and fragment count
            RefreshAllSlots();
            if (selectedSlotIndex >= 0)
            {
                UpdateItemDetails();
            }
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
                    iconElement.style.backgroundImage = StyleKeyword.None;
                    iconElement.style.display = DisplayStyle.None;
                }
                if (quantityLabel != null)
                {
                    quantityLabel.text = "";
                }
                slotElement.RemoveFromClassList("slot-filled");
                slotElement.RemoveFromClassList("slot-equipped");
                // Reset rarity border color
                slotElement.style.borderBottomColor = StyleKeyword.Null;
                // Hide attunement badge
                var attunementBadge = slotElement.Q<Label>("attunement-badge");
                if (attunementBadge != null)
                {
                    attunementBadge.style.display = DisplayStyle.None;
                }
            }
            else
            {
                // Filled slot
                var item = slot.Item;
                var definition = item.Definition;

                if (iconElement != null)
                {
                    iconElement.style.display = DisplayStyle.Flex;
                    if (definition.Icon != null)
                    {
                        iconElement.style.backgroundImage = new StyleBackground(definition.Icon);
                    }
                    iconElement.MarkDirtyRepaint();
                }
                slotElement.MarkDirtyRepaint();

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

                // Update attunement badge (only show for attuned equipment)
                UpdateSlotAttunementBadge(slotElement, item);
            }
        }

        private void UpdateSlotAttunementBadge(VisualElement slotElement, ItemInstance item)
        {
            const string badgeName = "attunement-badge";
            var badge = slotElement.Q<Label>(badgeName);

            bool showBadge = item.HasEquipmentData && item.EquipmentData.IsAttuned;

            if (showBadge)
            {
                if (badge == null)
                {
                    // Create badge
                    badge = new Label();
                    badge.name = badgeName;
                    badge.style.position = Position.Absolute;
                    badge.style.top = 2;
                    badge.style.right = 2;
                    badge.style.fontSize = 9;
                    badge.style.unityFontStyleAndWeight = FontStyle.Bold;
                    badge.style.backgroundColor = new Color(0.1f, 0.1f, 0.15f, 0.9f);
                    badge.style.paddingLeft = 3;
                    badge.style.paddingRight = 3;
                    badge.style.paddingTop = 1;
                    badge.style.paddingBottom = 1;
                    badge.style.borderTopLeftRadius = 2;
                    badge.style.borderTopRightRadius = 2;
                    badge.style.borderBottomLeftRadius = 2;
                    badge.style.borderBottomRightRadius = 2;
                    slotElement.Add(badge);
                }

                int level = item.EquipmentData.AttunementLevel;
                badge.text = $"+{level}";

                // Check max level from config if available
                int maxLevel = playerEquipment?.AttunementConfig != null
                    ? playerEquipment.AttunementConfig.MaximumLevel
                    : item.EquipmentData.MaximumAttunementLevel;
                bool isAtMax = level >= maxLevel;

                // Color based on level
                if (isAtMax)
                {
                    badge.style.color = new Color(0.9f, 0.75f, 0.4f); // Gold for max
                }
                else
                {
                    badge.style.color = new Color(0.5f, 0.7f, 1f); // Blue for attuned
                }
                badge.style.display = DisplayStyle.Flex;
            }
            else if (badge != null)
            {
                badge.style.display = DisplayStyle.None;
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

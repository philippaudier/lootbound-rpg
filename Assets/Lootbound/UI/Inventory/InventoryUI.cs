using UnityEngine;
using UnityEngine.UIElements;
using Lootbound.Gameplay.Inventory;
using Lootbound.Gameplay.Player;
using Lootbound.Gameplay.World;
using Lootbound.Gameplay.Equipment;

namespace Lootbound.UI
{
    /// <summary>
    /// Inventory UI using UI Toolkit - "expedition journal" layout.
    ///
    /// Structure (declared in Inventory.uxml):
    ///   window header (fixed) / main content / slot grid + equipment panel.
    ///   The equipment panel is header (fixed) / body (single ScrollView with
    ///   two info columns) / actions footer (fixed, never scrolled away).
    ///
    /// Hovering a slot previews its item (actions hidden, PREVIEW banner);
    /// clicking selects it. Actions always target the real selection.
    /// Window bounds are enforced in pixels from the resolved root size -
    /// never through percent heights, which do not resolve under the
    /// UIDocument TemplateContainer.
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

        private const float CompactWidthThreshold = 410f;
        private const float MaxWindowWidth = 1180f;
        private const float MaxWindowHeight = 720f;

        private VisualElement root;
        private VisualElement inventoryPanel;
        private VisualElement slotsContainer;
        private Label titleLabel;
        private Label capacityLabel;
        private Button closeButton;

        // Equipment panel (all declared in Inventory.uxml)
        private VisualElement equipmentPanel;
        private VisualElement detailsIcon;
        private Label detailsName;
        private Label detailsDescription;
        private Label detailsQuantity;
        private Label subtitleRarity;
        private Label subtitleKind;
        private Label subtitleAttunedDot;
        private Label subtitleAttuned;
        private VisualElement condMiniRoot;
        private VisualElement condFill;
        private Label condMiniLabel;
        private Button dropButton;
        private Button equipButton;

        // Section roots (info-section wrappers) and their content containers
        private VisualElement sectionStats;
        private VisualElement sectionCondition;
        private VisualElement sectionCompare;
        private VisualElement sectionAffixes;
        private VisualElement sectionHistory;
        private VisualElement sectionDetails;
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
        private int previewSlotIndex = -1;
        private bool isCompact;
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

            equipmentPanel = root.Q<VisualElement>("equipment-panel");
            detailsIcon = root.Q<VisualElement>("details-icon");
            detailsName = root.Q<Label>("details-name");
            detailsDescription = root.Q<Label>("details-description");
            detailsQuantity = root.Q<Label>("details-quantity");
            subtitleRarity = root.Q<Label>("subtitle-rarity");
            subtitleKind = root.Q<Label>("subtitle-kind");
            subtitleAttunedDot = root.Q<Label>("subtitle-attuned-dot");
            subtitleAttuned = root.Q<Label>("subtitle-attuned");
            condMiniRoot = root.Q<VisualElement>("eq-condition-mini");
            condFill = root.Q<VisualElement>("cond-fill");
            condMiniLabel = root.Q<Label>("cond-mini-label");

            sectionStats = root.Q<VisualElement>("section-stats");
            sectionCondition = root.Q<VisualElement>("section-condition");
            sectionCompare = root.Q<VisualElement>("section-compare");
            sectionAffixes = root.Q<VisualElement>("section-affixes");
            sectionHistory = root.Q<VisualElement>("section-history");
            sectionDetails = root.Q<VisualElement>("section-details");
            equipmentStatsContainer = root.Q<VisualElement>("equipment-stats");
            conditionContainer = root.Q<VisualElement>("condition");
            attunementContainer = root.Q<VisualElement>("attunement");
            repairContainer = root.Q<VisualElement>("repair");
            affixesContainer = root.Q<VisualElement>("affixes");
            historyContainer = root.Q<VisualElement>("history");
            comparisonContainer = root.Q<VisualElement>("comparison");

            dropButton = root.Q<Button>("drop-button");
            equipButton = root.Q<Button>("equip-button");

            if (closeButton != null)
            {
                closeButton.clicked += Close;
            }

            if (dropButton != null)
            {
                dropButton.clicked += HandleDropClicked;
            }

            if (equipButton != null)
            {
                equipButton.clicked += HandleEquipClicked;
            }

            // Leaving the grid always ends a hover preview.
            slotsContainer?.RegisterCallback<PointerLeaveEvent>(_ => EndPreview());

            // Compact mode: stack the two info columns when the panel gets
            // narrow. The class is only touched when the state changes.
            equipmentPanel?.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                bool shouldBeCompact = evt.newRect.width < CompactWidthThreshold;
                if (shouldBeCompact != isCompact)
                {
                    isCompact = shouldBeCompact;
                    equipmentPanel.EnableInClassList("compact", isCompact);
                }
            });

            if (inventoryPanel != null)
            {
                inventoryPanel.style.display = DisplayStyle.None;

                // The cloned TemplateContainer between the document root and
                // the overlay has no definite height, so percent-based sizes
                // never resolve. Stretch every intermediate container and
                // enforce the window bounds in pixels from the root size.
                for (var element = inventoryPanel.parent; element != null && element != root; element = element.parent)
                {
                    element.style.flexGrow = 1;
                }

                root.RegisterCallback<GeometryChangedEvent>(_ => ApplyWindowBounds());
                ApplyWindowBounds();
            }

            // Start with picking disabled - will enable when opened
            root.pickingMode = PickingMode.Ignore;
        }

        /// <summary>
        /// Caps the window size in pixels (92% of the resolved root, bounded
        /// by the design maximums) so the window can never exceed the screen
        /// at any resolution or aspect. Re-applied on root geometry changes.
        /// </summary>
        private void ApplyWindowBounds()
        {
            if (inventoryPanel == null || root == null) return;

            float rootWidth = root.resolvedStyle.width;
            float rootHeight = root.resolvedStyle.height;
            if (float.IsNaN(rootWidth) || rootWidth <= 0f) return;
            if (float.IsNaN(rootHeight) || rootHeight <= 0f) return;

            inventoryPanel.style.width = Mathf.Min(MaxWindowWidth, rootWidth * 0.92f);
            inventoryPanel.style.height = Mathf.Min(MaxWindowHeight, rootHeight * 0.92f);
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

                int slotIndex = i;
                slotElement.RegisterCallback<ClickEvent>(evt => HandleSlotClicked(slotIndex));
                slotElement.RegisterCallback<PointerEnterEvent>(_ => HandleSlotHovered(slotIndex));

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

        #region Selection and preview

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

        private void HandleSlotHovered(int slotIndex)
        {
            if (!isOpen || playerInventory?.Inventory == null) return;

            var slot = playerInventory.Inventory.GetSlot(slotIndex);
            if (slot == null || slot.IsEmpty)
            {
                // Hovering empty slots keeps the current display.
                return;
            }

            if (slotIndex == selectedSlotIndex)
            {
                EndPreview();
                return;
            }

            previewSlotIndex = slotIndex;
            equipmentPanel?.AddToClassList("preview");
            DisplayItem(slotIndex, isPreview: true);
        }

        private void EndPreview()
        {
            if (previewSlotIndex < 0) return;

            previewSlotIndex = -1;
            equipmentPanel?.RemoveFromClassList("preview");
            DisplayItem(selectedSlotIndex, isPreview: false);
        }

        /// <summary>Shows the real selection (ends any hover preview).</summary>
        private void UpdateItemDetails()
        {
            previewSlotIndex = -1;
            equipmentPanel?.RemoveFromClassList("preview");
            DisplayItem(selectedSlotIndex, isPreview: false);
        }

        #endregion

        #region Equipment panel display

        private void DisplayItem(int slotIndex, bool isPreview)
        {
            if (equipmentPanel == null) return;

            if (slotIndex < 0 || playerInventory?.Inventory == null)
            {
                equipmentPanel.AddToClassList("no-selection");
                return;
            }

            var slot = playerInventory.Inventory.GetSlot(slotIndex);
            if (slot == null || slot.IsEmpty)
            {
                equipmentPanel.AddToClassList("no-selection");
                return;
            }

            equipmentPanel.RemoveFromClassList("no-selection");

            var item = slot.Item;
            var definition = item.Definition;
            bool isEquipment = item.HasEquipmentData;

            // --- Header: icon, name, subtitle, condition mini-bar, quantity ---
            if (detailsIcon != null && definition.Icon != null)
            {
                detailsIcon.style.backgroundImage = new StyleBackground(definition.Icon);
            }

            if (detailsName != null)
            {
                string displayName;
                if (isEquipment && item.EquipmentData.IsAttuned)
                {
                    displayName = item.EquipmentData.GetAttunedDisplayName(equipmentRegistry);
                }
                else
                {
                    displayName = item.EquipmentData?.CustomName ?? definition.DisplayName;
                }
                detailsName.text = displayName;
            }

            Color rarityColor = isEquipment
                ? GetRarityColor(item.EquipmentData.Rarity)
                : definition.GetRarityColor();

            if (subtitleRarity != null)
            {
                subtitleRarity.text = (isEquipment ? item.EquipmentData.Rarity : definition.Rarity).ToString();
                subtitleRarity.style.color = rarityColor;
            }

            if (subtitleKind != null)
            {
                subtitleKind.text = isEquipment ? "Equipment" : "Item";
            }

            bool isAttuned = isEquipment && item.EquipmentData.IsAttuned;
            if (subtitleAttunedDot != null)
            {
                subtitleAttunedDot.style.display = isAttuned ? DisplayStyle.Flex : DisplayStyle.None;
            }
            if (subtitleAttuned != null)
            {
                subtitleAttuned.style.display = isAttuned ? DisplayStyle.Flex : DisplayStyle.None;
            }

            UpdateConditionMini(item);

            if (detailsQuantity != null)
            {
                bool showQuantity = !isEquipment && item.Quantity > 1;
                detailsQuantity.style.display = showQuantity ? DisplayStyle.Flex : DisplayStyle.None;
                detailsQuantity.text = showQuantity ? $"Quantity: {item.Quantity}" : "";
            }

            // --- DETAILS section (description) ---
            bool hasDescription = !string.IsNullOrEmpty(definition.Description);
            if (detailsDescription != null)
            {
                detailsDescription.text = definition.Description;
            }
            SetSectionVisible(sectionDetails, hasDescription);

            // --- Equipment sections ---
            if (isEquipment)
            {
                UpdateEquipmentDetails(item, slotIndex);
            }
            else
            {
                HideEquipmentUI();
            }

            // Actions always describe the REAL selection; during a preview
            // the footer is hidden by the .preview class, so we leave it as
            // the selection configured it.
            if (!isPreview)
            {
                UpdateActionButtons(item, slotIndex);
            }
        }

        private void UpdateConditionMini(ItemInstance item)
        {
            if (condMiniRoot == null) return;

            if (!item.HasEquipmentData)
            {
                condMiniRoot.style.display = DisplayStyle.None;
                return;
            }

            condMiniRoot.style.display = DisplayStyle.Flex;

            float normalized = item.EquipmentData.NormalizedDurability;
            if (condFill != null)
            {
                condFill.style.width = Length.Percent(Mathf.Clamp01(normalized) * 100f);
                condFill.EnableInClassList("bad", normalized < 0.4f);
                condFill.EnableInClassList("warn", normalized >= 0.4f && normalized < 0.7f);
            }

            if (condMiniLabel != null)
            {
                condMiniLabel.text = normalized >= 0.999f
                    ? "CONDITION PRISTINE"
                    : $"CONDITION {normalized * 100f:F0}%";
            }
        }

        private static void SetSectionVisible(VisualElement section, bool visible)
        {
            section?.EnableInClassList("hidden", !visible);
        }

        private void UpdateEquipmentDetails(ItemInstance item, int slotIndex)
        {
            var equipData = item.EquipmentData;
            if (equipData == null) return;

            bool isBroken = EquipmentConditionHelper.IsBroken(equipData.Condition);
            var brokenConfig = playerEquipment?.BrokenConfig;

            // --- PRIMARY STATS ---
            if (equipmentStatsContainer != null)
            {
                equipmentStatsContainer.Clear();

                var attunementConfig = playerEquipment?.AttunementConfig;
                var stats = equipData.ResolveStats(equipmentRegistry, brokenConfig, attunementConfig);
                if (stats.IsValid)
                {
                    if (isBroken && brokenConfig != null)
                    {
                        AddStatRowWithPenalty(equipmentStatsContainer, "Damage", stats.Damage, brokenConfig.GetDamagePenaltyPercent());
                        AddStatRowWithPenalty(equipmentStatsContainer, "Attack Speed", stats.AttackSpeed, brokenConfig.GetSpeedPenaltyPercent());
                        AddStatRowWithPenalty(equipmentStatsContainer, "Range", stats.Range, brokenConfig.GetRangePenaltyPercent(), "m");
                    }
                    else
                    {
                        AddStatRow(equipmentStatsContainer, "Damage", stats.Damage.ToString("F0"));
                        AddStatRow(equipmentStatsContainer, "Attack Speed", stats.AttackSpeed.ToString("F2"));
                        AddStatRow(equipmentStatsContainer, "Range", $"{stats.Range:F1}m");
                    }
                }
            }
            SetSectionVisible(sectionStats, true);

            // --- CONDITION ---
            if (conditionContainer != null)
            {
                conditionContainer.Clear();

                var condition = equipData.Condition;
                var conditionColor = EquipmentConditionHelper.GetConditionColor(condition);

                if (isBroken)
                {
                    var brokenBadge = new VisualElement();
                    brokenBadge.AddToClassList("broken-badge");
                    brokenBadge.tooltip = "This weapon is broken and suffers severe combat penalties. Find a way to repair it.";

                    var brokenLabel = new Label("BROKEN - Severe Penalties");
                    brokenLabel.AddToClassList("broken-badge-label");
                    brokenBadge.Add(brokenLabel);
                    conditionContainer.Add(brokenBadge);
                }

                var conditionRow = AddStatRow(conditionContainer, "Condition",
                    $"{condition} ({equipData.CurrentDurability:F0}/{equipData.MaxDurability:F0})", conditionColor);
                conditionRow.tooltip = EquipmentConditionHelper.GetConditionTooltip(condition);

                // Durability bar
                var barContainer = new VisualElement();
                barContainer.AddToClassList("cond-bar");

                var barFill = new VisualElement();
                barFill.AddToClassList("cond-bar-fill");
                barFill.style.width = Length.Percent(equipData.NormalizedDurability * 100f);
                barFill.style.backgroundColor = conditionColor;

                barContainer.Add(barFill);
                conditionContainer.Add(barContainer);
            }

            // --- Attunement (inside the CONDITION section) ---
            if (attunementContainer != null)
            {
                attunementContainer.Clear();

                int maxLevel = playerEquipment?.AttunementConfig != null
                    ? playerEquipment.AttunementConfig.MaximumLevel
                    : equipData.MaximumAttunementLevel;

                bool isAtMax = equipData.AttunementLevel >= maxLevel;
                Color attunementColor;
                if (isAtMax)
                {
                    attunementColor = new Color(0.9f, 0.75f, 0.4f); // Gold for max
                }
                else if (equipData.IsAttuned)
                {
                    attunementColor = new Color(0.5f, 0.7f, 1f); // Blue for attuned
                }
                else
                {
                    attunementColor = new Color(0.6f, 0.6f, 0.65f); // Gray for unattuned
                }

                AddStatRow(attunementContainer, "Attunement",
                    $"+{equipData.AttunementLevel} / +{maxLevel}", attunementColor);

                if (equipData.IsAttuned && playerEquipment?.AttunementConfig != null)
                {
                    float bonusPercent = playerEquipment.AttunementConfig.GetDamageBonusPercent(equipData.AttunementLevel);
                    if (bonusPercent > 0)
                    {
                        AddStatRow(attunementContainer, "Damage Bonus",
                            $"+{bonusPercent:F0}%", new Color(0.4f, 0.85f, 0.4f));
                    }
                }
            }

            // --- Repair (inside the CONDITION section) ---
            UpdateRepairPanel(item);
            SetSectionVisible(sectionCondition, true);

            // --- AFFIXES ---
            if (affixesContainer != null)
            {
                affixesContainer.Clear();
                foreach (var affix in equipData.Affixes)
                {
                    AddAffixEntry(affixesContainer, affix);
                }
            }
            SetSectionVisible(sectionAffixes, equipData.Affixes.Count > 0);

            // --- ITEM HISTORY (journal entries; only facts that exist) ---
            if (historyContainer != null)
            {
                historyContainer.Clear();

                AddHistoryEntry(historyContainer, equipData.History.GetSummary());

                if (equipData.History.HasBeenRepaired)
                {
                    AddHistoryEntry(historyContainer, equipData.History.GetRepairSummary());
                }

                if (equipData.History.HasAttunementHistory)
                {
                    AddHistoryEntry(historyContainer, equipData.History.GetAttunementSummary());

                    var attunement = equipData.History.Attunement;
                    if (attunement.LastAttemptTimestamp > 0)
                    {
                        string lastAttemptSummary = attunement.GetLastAttemptSummary();
                        if (!string.IsNullOrEmpty(lastAttemptSummary))
                        {
                            AddHistoryEntry(historyContainer, $"Last: {lastAttemptSummary}");
                        }
                    }
                }
            }
            SetSectionVisible(sectionHistory, true);

            // --- COMPARISON ---
            UpdateComparison(item, slotIndex);
        }

        private void UpdateComparison(ItemInstance selectedItem, int slotIndex)
        {
            if (comparisonContainer == null) return;

            // Only show comparison if a weapon is equipped and the shown item
            // is not the equipped one itself.
            if (playerEquipment == null || !playerEquipment.HasWeaponEquipped ||
                playerEquipment.IsSlotEquipped(slotIndex))
            {
                SetSectionVisible(sectionCompare, false);
                return;
            }

            var brokenConfig = playerEquipment?.BrokenConfig;
            var attunementConfig = playerEquipment?.AttunementConfig;
            var selectedStats = selectedItem.EquipmentData?.ResolveStats(equipmentRegistry, brokenConfig, attunementConfig) ?? ResolvedWeaponStats.Invalid;

            if (!selectedStats.IsValid)
            {
                SetSectionVisible(sectionCompare, false);
                return;
            }

            comparisonContainer.Clear();
            SetSectionVisible(sectionCompare, true);

            var headerLabel = new Label("vs equipped weapon");
            headerLabel.AddToClassList("compare-vs");
            comparisonContainer.Add(headerLabel);

            var currentStats = playerEquipment.CurrentStats;

            int equippedAttunement = playerEquipment.CurrentEquipment?.AttunementLevel ?? 0;
            int selectedAttunement = selectedItem.EquipmentData?.AttunementLevel ?? 0;
            AddAttunementDelta(comparisonContainer, equippedAttunement, selectedAttunement);

            AddStatDelta(comparisonContainer, "Damage", currentStats.Damage, selectedStats.Damage);
            AddStatDelta(comparisonContainer, "Speed", currentStats.AttackSpeed, selectedStats.AttackSpeed);
            AddStatDelta(comparisonContainer, "Range", currentStats.Range, selectedStats.Range);
        }

        private void HideEquipmentUI()
        {
            SetSectionVisible(sectionStats, false);
            SetSectionVisible(sectionCondition, false);
            SetSectionVisible(sectionCompare, false);
            SetSectionVisible(sectionAffixes, false);
            SetSectionVisible(sectionHistory, false);
        }

        private void UpdateActionButtons(ItemInstance item, int slotIndex)
        {
            bool isEquipment = item.HasEquipmentData;
            bool isEquipped = isEquipment && (playerEquipment?.IsSlotEquipped(slotIndex) ?? false);

            if (equipButton != null)
            {
                equipButton.style.display = isEquipment ? DisplayStyle.Flex : DisplayStyle.None;
                equipButton.text = isEquipped ? "Unequip" : "Equip";
            }

            if (dropButton != null)
            {
                dropButton.SetEnabled(!isEquipped);
                dropButton.text = isEquipped ? "Equipped" : "Drop";
            }
        }

        #endregion

        #region Row builders

        private static VisualElement AddStatRow(VisualElement container, string label, string value, Color? valueColor = null)
        {
            var row = new VisualElement();
            row.AddToClassList("stat-row");

            var nameLabel = new Label(label);
            nameLabel.AddToClassList("stat-label");

            var valueLabel = new Label(value);
            valueLabel.AddToClassList("stat-value");
            if (valueColor.HasValue)
            {
                valueLabel.style.color = valueColor.Value;
            }

            row.Add(nameLabel);
            row.Add(valueLabel);
            container.Add(row);
            return row;
        }

        private static void AddStatRowWithPenalty(VisualElement container, string label, float value, int penaltyPercent, string suffix = "")
        {
            var row = new VisualElement();
            row.AddToClassList("stat-row");

            var nameLabel = new Label(label);
            nameLabel.AddToClassList("stat-label");

            string valueText = suffix == "m" ? $"{value:F1}{suffix}" : value.ToString("F0");
            var valueLabel = new Label(valueText);
            valueLabel.AddToClassList("stat-value-penalty");

            var penaltyLabel = new Label($"({penaltyPercent}%)");
            penaltyLabel.AddToClassList("stat-penalty-note");

            row.Add(nameLabel);
            row.Add(valueLabel);
            row.Add(penaltyLabel);
            container.Add(row);
        }

        private void AddStatDelta(VisualElement container, string statName, float current, float selected)
        {
            float diff = selected - current;
            if (Mathf.Abs(diff) < 0.01f) return; // Deltas only

            var row = new VisualElement();
            row.AddToClassList("delta-row");

            var nameLabel = new Label(statName);
            nameLabel.AddToClassList("delta-label");

            var diffLabel = new Label(diff > 0 ? $"+{diff:F1}" : $"{diff:F1}");
            diffLabel.AddToClassList("delta-value");
            diffLabel.AddToClassList(diff > 0 ? "pos" : "neg");

            row.Add(nameLabel);
            row.Add(diffLabel);
            container.Add(row);
        }

        private void AddAttunementDelta(VisualElement container, int equippedLevel, int selectedLevel)
        {
            var row = new VisualElement();
            row.AddToClassList("delta-row");

            var nameLabel = new Label("Attunement");
            nameLabel.AddToClassList("delta-label");

            var valueLabel = new Label($"+{selectedLevel}");
            valueLabel.AddToClassList("delta-value");
            if (selectedLevel > equippedLevel)
            {
                valueLabel.AddToClassList("pos");
            }
            else if (selectedLevel < equippedLevel)
            {
                valueLabel.AddToClassList("neg");
            }

            row.Add(nameLabel);
            row.Add(valueLabel);
            container.Add(row);
        }

        private void AddAffixEntry(VisualElement container, AffixInstance affix)
        {
            var block = new VisualElement();
            block.AddToClassList("affix");

            var nameLabel = new Label(affix.GetDisplayName(equipmentRegistry));
            nameLabel.AddToClassList("affix-name");
            block.Add(nameLabel);

            string description = affix.GetFormattedDescription(equipmentRegistry);
            if (!string.IsNullOrEmpty(description))
            {
                var descLabel = new Label(description);
                descLabel.AddToClassList("affix-desc");
                block.Add(descLabel);
            }

            container.Add(block);
        }

        private static void AddHistoryEntry(VisualElement container, string fact)
        {
            if (string.IsNullOrEmpty(fact)) return;

            var entry = new VisualElement();
            entry.AddToClassList("history-entry");

            var factLabel = new Label(fact);
            factLabel.AddToClassList("history-fact");
            entry.Add(factLabel);

            container.Add(entry);
        }

        #endregion

        #region Repair

        private void UpdateRepairPanel(ItemInstance item)
        {
            if (repairContainer == null || playerRepair == null) return;

            var equipData = item?.EquipmentData;
            if (equipData == null)
            {
                repairContainer.style.display = DisplayStyle.None;
                return;
            }

            var preview = playerRepair.PreviewRepair(equipData);

            repairContainer.Clear();

            // Only show repair block if equipment needs repair
            if (!playerRepair.NeedsRepair(equipData))
            {
                repairContainer.style.display = DisplayStyle.None;
                return;
            }

            repairContainer.style.display = DisplayStyle.Flex;

            var headerLabel = new Label("Repair");
            headerLabel.AddToClassList("repair-header");
            repairContainer.Add(headerLabel);

            AddStatRow(repairContainer, "Repair Fragments", preview.FragmentsAvailable.ToString(),
                preview.FragmentsAvailable > 0 ? new Color(0.5f, 0.9f, 0.5f) : new Color(0.9f, 0.5f, 0.5f));

            if (preview.CanRepair)
            {
                AddStatRow(repairContainer, "Durability",
                    $"{preview.CurrentDurability:F0} → {preview.DurabilityAfterRepair:F0}",
                    new Color(0.5f, 0.9f, 0.5f));

                if (preview.WillChangeCondition)
                {
                    AddStatRow(repairContainer, "Condition",
                        $"{preview.ConditionBefore} → {preview.ConditionAfter}",
                        EquipmentConditionHelper.GetConditionColor(preview.ConditionAfter));
                }

                string fragmentsText = preview.FragmentsToConsume == preview.FragmentsForFullRepair
                    ? $"{preview.FragmentsToConsume} (full repair)"
                    : $"{preview.FragmentsToConsume} / {preview.FragmentsForFullRepair} needed";
                AddStatRow(repairContainer, "Fragments to use", fragmentsText);

                repairButton = new Button(HandleRepairClicked);
                repairButton.text = preview.IsFullRepair ? "Full Repair" : "Repair";
                repairButton.AddToClassList("action-btn");
                repairButton.AddToClassList("primary");
                repairContainer.Add(repairButton);
            }
            else
            {
                var reasonLabel = new Label(GetRepairFailureMessage(preview.FailureReason));
                reasonLabel.AddToClassList("repair-reason");
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

                UpdateItemDetails();
                RefreshAllSlots();
            }
            else
            {
                Debug.LogWarning($"[InventoryUI] Repair failed: {result.FailureReason}");
            }
        }

        #endregion

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

            // Refresh item details to update repair block (fragment count may have changed)
            if (selectedSlotIndex >= 0)
            {
                UpdateItemDetails();
            }
        }

        private void HandleEquipmentChanged(WearResult result)
        {
            if (!isOpen) return;

            RefreshAllSlots();
            if (selectedSlotIndex >= 0)
            {
                UpdateItemDetails();
            }
        }

        private void HandleRepairCompleted(RepairResult result)
        {
            if (!isOpen) return;

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

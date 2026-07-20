using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Lootbound.Gameplay.Player;
using Lootbound.Gameplay.Inventory;
using Lootbound.Gameplay.Equipment;
using Lootbound.Gameplay.World;

namespace Lootbound.UI
{
    /// <summary>
    /// Repair Station UI - the "preserve" page of the expedition journal.
    /// Left: list of equipment needing repair. Right: the selected piece,
    /// its condition now/after (with the restored gain highlighted in
    /// brass), materials, repair preview and its journey so far. The window
    /// footer (status + Cancel + Repair) never scrolls.
    ///
    /// Same family and mechanics as the inventory: shared Journal.uss
    /// tokens, pixel window bounds from the resolved root (percent heights
    /// never resolve under the UIDocument TemplateContainer), compact mode
    /// stacking the columns on narrow windows.
    /// </summary>
    public class RepairStationUI : MonoBehaviour
    {
        [Header("UI Document")]
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private int sortOrder = 110;

        [Header("Player References")]
        [SerializeField] private PlayerInputReader inputReader;
        [SerializeField] private PlayerCameraController cameraController;
        [SerializeField] private PlayerInventory playerInventory;
        [SerializeField] private PlayerEquipment playerEquipment;
        [SerializeField] private PlayerRepair playerRepair;
        [SerializeField] private EquipmentRegistry equipmentRegistry;

        [Header("Visuals")]
        [SerializeField]
        [Tooltip("Optional icon shown next to Repair Fragments in the materials list")]
        private Sprite fragmentIcon;

        private const float MaxWindowWidth = 1180f;
        private const float MaxWindowHeight = 720f;
        private const float CompactWidthThreshold = 780f;

        // Cached repair stations in scene
        private RepairStation[] cachedStations;

        // UI Elements
        private VisualElement root;
        private VisualElement stationPanel;
        private VisualElement equipmentListContainer;
        private VisualElement previewPanel;
        private Label detailEmptyLabel;
        private Button repairButton;
        private Button cancelButton;
        private Button closeButton;

        // Preview elements
        private VisualElement previewIcon;
        private Label previewName;
        private Label previewRarity;
        private Label subtitleAttunedDot;
        private Label subtitleAttuned;
        private Label previewSummary;
        private VisualElement condNowFill;
        private VisualElement afterLine;
        private VisualElement condAfterCurrent;
        private VisualElement condAfterGain;
        private Label previewCurrentDurability;
        private Label previewAfterDurability;
        private Label restoredAmount;
        private Label afterDurabilityValue;
        private VisualElement conditionChangeRow;
        private Label previewConditionBefore;
        private Label previewConditionAfter;
        private Label previewCost;
        private Label fragmentsAvailableLabel;
        private Label fragmentsNeededLabel;
        private VisualElement fragmentIconElement;
        private VisualElement sectionAffixes;
        private VisualElement affixesContainer;
        private VisualElement historyEntriesContainer;
        private Label noEquipmentLabel;
        private Label failureReasonLabel;
        private Label footerStatusLabel;

        // State
        private bool isOpen;
        private bool isCompact;
        private RepairStation currentStation;
        private EquipmentData selectedEquipment;
        private ItemInstance selectedItem;
        private int selectedSlotIndex = -1;
        private List<int> repairableSlotIndices = new List<int>();
        private List<VisualElement> equipmentElements = new List<VisualElement>();

        public bool IsOpen => isOpen;

        /// <summary>
        /// Event raised when the station UI is closed.
        /// </summary>
        public event Action OnClosed;

        private void Awake()
        {
            if (uiDocument == null)
            {
                Debug.LogError("[RepairStationUI] UIDocument is not assigned!");
                return;
            }

            SetupUI();
        }

        private void OnEnable()
        {
            if (playerRepair != null)
            {
                playerRepair.OnRepairCompleted += HandleRepairCompleted;
            }

            if (playerInventory?.Inventory != null)
            {
                playerInventory.Inventory.OnInventoryChanged += HandleInventoryChanged;
            }

            SubscribeToStations();
        }

        private void OnDisable()
        {
            if (playerRepair != null)
            {
                playerRepair.OnRepairCompleted -= HandleRepairCompleted;
            }

            if (playerInventory?.Inventory != null)
            {
                playerInventory.Inventory.OnInventoryChanged -= HandleInventoryChanged;
            }

            UnsubscribeFromStations();
        }

        private void SubscribeToStations()
        {
            // Find all repair stations in scene and subscribe to their events
            cachedStations = FindObjectsByType<RepairStation>(FindObjectsSortMode.None);
            foreach (var station in cachedStations)
            {
                station.OnInteractionRequested += HandleStationInteraction;
            }
        }

        private void UnsubscribeFromStations()
        {
            if (cachedStations == null) return;

            foreach (var station in cachedStations)
            {
                if (station != null)
                {
                    station.OnInteractionRequested -= HandleStationInteraction;
                }
            }
            cachedStations = null;
        }

        private void HandleStationInteraction(RepairStation station)
        {
            Open(station);
        }

        private void SetupUI()
        {
            root = uiDocument.rootVisualElement;
            uiDocument.sortingOrder = sortOrder;

            stationPanel = root.Q<VisualElement>("repair-station-panel");
            equipmentListContainer = root.Q<VisualElement>("equipment-list");
            previewPanel = root.Q<VisualElement>("repair-preview");
            detailEmptyLabel = root.Q<Label>("detail-empty");
            repairButton = root.Q<Button>("repair-button");
            cancelButton = root.Q<Button>("cancel-button");
            closeButton = root.Q<Button>("close-button");

            previewIcon = root.Q<VisualElement>("preview-icon");
            previewName = root.Q<Label>("preview-name");
            previewRarity = root.Q<Label>("preview-rarity");
            subtitleAttunedDot = root.Q<Label>("subtitle-attuned-dot");
            subtitleAttuned = root.Q<Label>("subtitle-attuned");
            previewSummary = root.Q<Label>("preview-summary");
            condNowFill = root.Q<VisualElement>("cond-now-fill");
            afterLine = root.Q<VisualElement>("after-line");
            condAfterCurrent = root.Q<VisualElement>("cond-after-current");
            condAfterGain = root.Q<VisualElement>("cond-after-gain");
            previewCurrentDurability = root.Q<Label>("current-durability");
            previewAfterDurability = root.Q<Label>("after-durability");
            restoredAmount = root.Q<Label>("restored-amount");
            afterDurabilityValue = root.Q<Label>("after-durability-value");
            conditionChangeRow = root.Q<VisualElement>("condition-change-row");
            previewConditionBefore = root.Q<Label>("condition-before");
            previewConditionAfter = root.Q<Label>("condition-after");
            previewCost = root.Q<Label>("repair-cost");
            fragmentsAvailableLabel = root.Q<Label>("fragments-available");
            fragmentsNeededLabel = root.Q<Label>("fragments-needed");
            fragmentIconElement = root.Q<VisualElement>("fragment-icon");
            sectionAffixes = root.Q<VisualElement>("section-affixes");
            affixesContainer = root.Q<VisualElement>("preview-affixes");
            historyEntriesContainer = root.Q<VisualElement>("history-entries");
            noEquipmentLabel = root.Q<Label>("no-equipment-label");
            failureReasonLabel = root.Q<Label>("failure-reason");
            footerStatusLabel = root.Q<Label>("footer-status");

            if (fragmentIconElement != null && fragmentIcon != null)
            {
                fragmentIconElement.style.backgroundImage = new StyleBackground(fragmentIcon);
            }

            // Button callbacks
            if (closeButton != null)
            {
                closeButton.clicked += Close;
            }

            if (cancelButton != null)
            {
                cancelButton.clicked += Close;
            }

            if (repairButton != null)
            {
                repairButton.clicked += HandleRepairClicked;
            }

            // Compact mode: stack the detail columns on narrow windows.
            stationPanel?.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                bool shouldBeCompact = evt.newRect.width < CompactWidthThreshold;
                if (shouldBeCompact != isCompact)
                {
                    isCompact = shouldBeCompact;
                    stationPanel.EnableInClassList("compact", isCompact);
                }
            });

            // Hide by default
            if (stationPanel != null)
            {
                stationPanel.style.display = DisplayStyle.None;

                // Stretch the intermediate containers and enforce the window
                // bounds in pixels (same fix as the inventory: percent sizes
                // never resolve under the TemplateContainer).
                for (var element = stationPanel.parent; element != null && element != root; element = element.parent)
                {
                    element.style.flexGrow = 1;
                }

                root.RegisterCallback<GeometryChangedEvent>(_ => ApplyWindowBounds());
                ApplyWindowBounds();
            }

            // Start with picking disabled - will enable when opened
            root.pickingMode = PickingMode.Ignore;
        }

        private void ApplyWindowBounds()
        {
            if (stationPanel == null || root == null) return;

            float rootWidth = root.resolvedStyle.width;
            float rootHeight = root.resolvedStyle.height;
            if (float.IsNaN(rootWidth) || rootWidth <= 0f) return;
            if (float.IsNaN(rootHeight) || rootHeight <= 0f) return;

            stationPanel.style.width = Mathf.Min(MaxWindowWidth, rootWidth * 0.92f);
            stationPanel.style.height = Mathf.Min(MaxWindowHeight, rootHeight * 0.92f);
        }

        /// <summary>
        /// Open the repair station UI.
        /// </summary>
        public void Open(RepairStation station)
        {
            if (stationPanel == null) return;
            if (isOpen) return;

            currentStation = station;
            isOpen = true;

            // Panel open animation
            stationPanel.AddToClassList("repair-station-panel-opening");
            stationPanel.style.display = DisplayStyle.Flex;

            // Remove opening class after a frame to trigger transition
            stationPanel.schedule.Execute(() =>
            {
                stationPanel.RemoveFromClassList("repair-station-panel-opening");
            }).ExecuteLater(10);

            // Enable pointer events on this UI
            root.pickingMode = PickingMode.Position;

            // Mark station as in use
            if (currentStation != null)
            {
                currentStation.SetInUse(true);
            }

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

            // Populate equipment list
            RefreshEquipmentList();
            ClearSelection();

            Debug.Log("[RepairStationUI] Opened");
        }

        /// <summary>
        /// Close the repair station UI.
        /// </summary>
        public void Close()
        {
            if (stationPanel == null) return;
            if (!isOpen) return;

            isOpen = false;
            stationPanel.style.display = DisplayStyle.None;

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

            // Notify station that we're closed
            currentStation?.Close();
            currentStation = null;

            ClearSelection();
            OnClosed?.Invoke();

            Debug.Log("[RepairStationUI] Closed");
        }

        private void RefreshEquipmentList()
        {
            if (equipmentListContainer == null || playerInventory?.Inventory == null) return;

            equipmentListContainer.Clear();
            equipmentElements.Clear();
            repairableSlotIndices.Clear();

            var inventory = playerInventory.Inventory;

            // Find all repairable equipment
            for (int i = 0; i < inventory.Capacity; i++)
            {
                var slot = inventory.GetSlot(i);
                if (slot == null || slot.IsEmpty) continue;

                var item = slot.Item;
                if (!item.HasEquipmentData) continue;

                var equipData = item.EquipmentData;

                // Only show equipment that needs repair
                if (!playerRepair.NeedsRepair(equipData)) continue;

                repairableSlotIndices.Add(i);
                var element = CreateEquipmentElement(item, i);
                equipmentListContainer.Add(element);
                equipmentElements.Add(element);
            }

            // Show "no equipment" message if empty
            if (noEquipmentLabel != null)
            {
                noEquipmentLabel.style.display = repairableSlotIndices.Count == 0
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            }
        }

        private VisualElement CreateEquipmentElement(ItemInstance item, int slotIndex)
        {
            var equipData = item.EquipmentData;
            var definition = item.Definition;

            var element = new VisualElement();
            element.AddToClassList("select-entry");
            element.userData = slotIndex;

            var icon = new VisualElement();
            icon.AddToClassList("select-entry-icon");
            if (definition.Icon != null)
            {
                icon.style.backgroundImage = new StyleBackground(definition.Icon);
            }
            element.Add(icon);

            var info = new VisualElement();
            info.AddToClassList("select-entry-text");

            // Name (use attuned display name for attuned equipment)
            string displayName = equipData.IsAttuned
                ? equipData.GetAttunedDisplayName(equipmentRegistry)
                : (equipData.CustomName ?? definition.DisplayName);
            var nameLabel = new Label(displayName);
            nameLabel.AddToClassList("select-entry-name");
            nameLabel.style.color = GetRarityColor(equipData.Rarity);
            info.Add(nameLabel);

            var conditionLabel = new Label(equipData.Condition.ToString());
            conditionLabel.AddToClassList("select-entry-sub");
            conditionLabel.style.color = EquipmentConditionHelper.GetConditionColor(equipData.Condition);
            info.Add(conditionLabel);

            var durabilityContainer = new VisualElement();
            durabilityContainer.AddToClassList("list-durability");

            var durabilityFill = new VisualElement();
            durabilityFill.AddToClassList("list-durability-fill");
            durabilityFill.style.width = new Length(equipData.NormalizedDurability * 100f, LengthUnit.Percent);
            durabilityFill.style.backgroundColor = EquipmentConditionHelper.GetConditionColor(equipData.Condition);
            durabilityContainer.Add(durabilityFill);

            info.Add(durabilityContainer);
            element.Add(info);

            element.RegisterCallback<ClickEvent>(evt => SelectEquipment(slotIndex));

            return element;
        }

        private void SelectEquipment(int slotIndex)
        {
            // Deselect previous
            if (selectedSlotIndex >= 0)
            {
                int elementIndex = repairableSlotIndices.IndexOf(selectedSlotIndex);
                if (elementIndex >= 0 && elementIndex < equipmentElements.Count)
                {
                    equipmentElements[elementIndex].RemoveFromClassList("selected");
                }
            }

            // Toggle selection
            if (slotIndex == selectedSlotIndex)
            {
                ClearSelection();
                return;
            }

            selectedSlotIndex = slotIndex;

            // Highlight new selection
            int newElementIndex = repairableSlotIndices.IndexOf(slotIndex);
            if (newElementIndex >= 0 && newElementIndex < equipmentElements.Count)
            {
                equipmentElements[newElementIndex].AddToClassList("selected");
            }

            // Get equipment data
            var slot = playerInventory.Inventory.GetSlot(slotIndex);
            if (slot != null && !slot.IsEmpty && slot.Item.HasEquipmentData)
            {
                selectedItem = slot.Item;
                selectedEquipment = slot.Item.EquipmentData;
                UpdatePreview();
            }
        }

        private void ClearSelection()
        {
            if (selectedSlotIndex >= 0)
            {
                int elementIndex = repairableSlotIndices.IndexOf(selectedSlotIndex);
                if (elementIndex >= 0 && elementIndex < equipmentElements.Count)
                {
                    equipmentElements[elementIndex].RemoveFromClassList("selected");
                }
            }

            selectedSlotIndex = -1;
            selectedEquipment = null;
            selectedItem = null;
            HidePreview();
        }

        private void UpdatePreview()
        {
            if (previewPanel == null || selectedEquipment == null) return;

            previewPanel.style.display = DisplayStyle.Flex;
            if (detailEmptyLabel != null)
            {
                detailEmptyLabel.style.display = DisplayStyle.None;
            }

            var preview = playerRepair.PreviewRepair(selectedEquipment);

            // --- Item preview header ---
            if (previewIcon != null && selectedItem?.Definition?.Icon != null)
            {
                previewIcon.style.backgroundImage = new StyleBackground(selectedItem.Definition.Icon);
            }

            if (previewName != null)
            {
                previewName.text = selectedEquipment.IsAttuned
                    ? selectedEquipment.GetAttunedDisplayName(equipmentRegistry)
                    : (selectedEquipment.CustomName ?? selectedItem?.Definition?.DisplayName ?? "Equipment");
            }

            if (previewRarity != null)
            {
                previewRarity.text = selectedEquipment.Rarity.ToString();
                previewRarity.style.color = GetRarityColor(selectedEquipment.Rarity);
            }

            bool isAttuned = selectedEquipment.IsAttuned;
            if (subtitleAttunedDot != null)
            {
                subtitleAttunedDot.style.display = isAttuned ? DisplayStyle.Flex : DisplayStyle.None;
            }
            if (subtitleAttuned != null)
            {
                subtitleAttuned.style.display = isAttuned ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (previewSummary != null)
            {
                string description = selectedItem?.Definition?.Description;
                previewSummary.text = description ?? "";
                previewSummary.style.display = string.IsNullOrEmpty(description)
                    ? DisplayStyle.None
                    : DisplayStyle.Flex;
            }

            // --- Condition bars: NOW / AFTER (restored gain in brass) ---
            float normalizedNow = selectedEquipment.NormalizedDurability;
            var conditionColor = EquipmentConditionHelper.GetConditionColor(selectedEquipment.Condition);

            if (condNowFill != null)
            {
                condNowFill.style.width = new Length(normalizedNow * 100f, LengthUnit.Percent);
                condNowFill.style.backgroundColor = conditionColor;
            }

            if (previewCurrentDurability != null)
            {
                previewCurrentDurability.text =
                    $"{selectedEquipment.CurrentDurability:F0} / {selectedEquipment.MaxDurability:F0}";
            }

            // --- Materials ---
            UpdateMaterials(preview);

            // --- Affixes and history ---
            UpdateAffixesDisplay();
            UpdateHistoryDisplay();

            if (preview.CanRepair)
            {
                float normalizedAfter = selectedEquipment.MaxDurability > 0f
                    ? preview.DurabilityAfterRepair / selectedEquipment.MaxDurability
                    : 0f;

                if (afterLine != null)
                {
                    afterLine.style.display = DisplayStyle.Flex;
                }
                if (condAfterCurrent != null)
                {
                    condAfterCurrent.style.width = new Length(normalizedNow * 100f, LengthUnit.Percent);
                    condAfterCurrent.style.backgroundColor = conditionColor;
                }
                if (condAfterGain != null)
                {
                    condAfterGain.style.width = new Length(
                        Mathf.Max(0f, normalizedAfter - normalizedNow) * 100f, LengthUnit.Percent);
                }

                if (previewAfterDurability != null)
                {
                    previewAfterDurability.text =
                        $"{preview.DurabilityAfterRepair:F0} / {selectedEquipment.MaxDurability:F0}";
                }

                if (restoredAmount != null)
                {
                    restoredAmount.text = $"+{preview.DurabilityAfterRepair - preview.CurrentDurability:F0}";
                }

                if (afterDurabilityValue != null)
                {
                    afterDurabilityValue.text =
                        $"{preview.DurabilityAfterRepair:F0} / {selectedEquipment.MaxDurability:F0}";
                }

                if (conditionChangeRow != null)
                {
                    conditionChangeRow.style.display = preview.WillChangeCondition
                        ? DisplayStyle.Flex
                        : DisplayStyle.None;
                    if (previewConditionBefore != null)
                    {
                        previewConditionBefore.text = preview.ConditionBefore.ToString();
                    }
                    if (previewConditionAfter != null)
                    {
                        previewConditionAfter.text = preview.ConditionAfter.ToString();
                        previewConditionAfter.style.color =
                            EquipmentConditionHelper.GetConditionColor(preview.ConditionAfter);
                    }
                }

                if (previewCost != null)
                {
                    previewCost.text = preview.FragmentsToConsume == 1
                        ? "1 Repair Fragment"
                        : $"{preview.FragmentsToConsume} Repair Fragments";
                }

                if (repairButton != null)
                {
                    repairButton.SetEnabled(true);
                    repairButton.text = preview.IsFullRepair ? "Full Repair" : "Repair";
                }

                if (failureReasonLabel != null)
                {
                    failureReasonLabel.style.display = DisplayStyle.None;
                }

                SetFooterStatus($"Ready to repair · {preview.FragmentsAvailable} fragments available");
            }
            else
            {
                if (afterLine != null)
                {
                    afterLine.style.display = DisplayStyle.None;
                }
                if (restoredAmount != null) restoredAmount.text = "—";
                if (afterDurabilityValue != null) afterDurabilityValue.text = "—";
                if (conditionChangeRow != null) conditionChangeRow.style.display = DisplayStyle.None;
                if (previewCost != null) previewCost.text = "—";

                if (repairButton != null)
                {
                    repairButton.SetEnabled(false);
                    repairButton.text = "Repair";
                }

                string reason = GetFailureReasonText(preview.FailureReason);
                if (failureReasonLabel != null)
                {
                    failureReasonLabel.text = reason;
                    failureReasonLabel.style.display = DisplayStyle.Flex;
                }

                SetFooterStatus(reason);
            }
        }

        private void UpdateMaterials(RepairPreview preview)
        {
            if (fragmentsAvailableLabel != null)
            {
                fragmentsAvailableLabel.text = preview.FragmentsAvailable.ToString();
                fragmentsAvailableLabel.EnableInClassList("short",
                    preview.FragmentsAvailable < Mathf.Max(1, preview.FragmentsToConsume));
            }

            if (fragmentsNeededLabel != null)
            {
                fragmentsNeededLabel.text = Mathf.Max(preview.FragmentsToConsume, 1).ToString();
            }
        }

        private void UpdateAffixesDisplay()
        {
            if (affixesContainer == null || selectedEquipment == null) return;

            affixesContainer.Clear();

            bool hasAffixes = selectedEquipment.Affixes != null && selectedEquipment.Affixes.Count > 0;
            sectionAffixes?.EnableInClassList("hidden", !hasAffixes);
            if (!hasAffixes) return;

            foreach (var affix in selectedEquipment.Affixes)
            {
                var affixDef = equipmentRegistry?.GetAffixDefinition(affix.DefinitionId);
                if (affixDef == null) continue;

                var block = new VisualElement();
                block.AddToClassList("affix");

                var label = new Label($"+ {affix.RolledValue:F0}% {affixDef.DisplayName}");
                label.AddToClassList("affix-name");
                block.Add(label);

                affixesContainer.Add(block);
            }
        }

        private void UpdateHistoryDisplay()
        {
            if (historyEntriesContainer == null || selectedEquipment == null) return;

            historyEntriesContainer.Clear();

            var history = selectedEquipment.History;
            if (history == null) return;

            AddHistoryEntry(history.GetSummary());

            if (history.HasBeenRepaired)
            {
                AddHistoryEntry(history.GetRepairSummary());
            }

            if (history.HasAttunementHistory)
            {
                AddHistoryEntry(history.GetAttunementSummary());
            }
        }

        private void AddHistoryEntry(string fact)
        {
            if (string.IsNullOrEmpty(fact) || historyEntriesContainer == null) return;

            var entry = new VisualElement();
            entry.AddToClassList("history-entry");

            var factLabel = new Label(fact);
            factLabel.AddToClassList("history-fact");
            entry.Add(factLabel);

            historyEntriesContainer.Add(entry);
        }

        private void HidePreview()
        {
            if (previewPanel != null)
            {
                previewPanel.style.display = DisplayStyle.None;
            }

            if (detailEmptyLabel != null)
            {
                detailEmptyLabel.style.display = DisplayStyle.Flex;
            }

            if (repairButton != null)
            {
                repairButton.SetEnabled(false);
                repairButton.text = "Repair";
            }

            SetFooterStatus(repairableSlotIndices.Count == 0
                ? "All equipment is in good condition"
                : "Select equipment to preserve");
        }

        private void SetFooterStatus(string text)
        {
            if (footerStatusLabel != null)
            {
                footerStatusLabel.text = text;
            }
        }

        private void HandleRepairClicked()
        {
            if (selectedEquipment == null || playerRepair == null) return;

            var result = playerRepair.RepairEquipment(selectedEquipment);

            if (result.Success)
            {
                Debug.Log($"[RepairStationUI] Repaired {result.EquipmentName}: " +
                    $"{result.DurabilityBefore:F0} -> {result.DurabilityAfter:F0}");

                // Visual success feedback
                ShowRepairSuccessFlash();

                // Refresh UI
                RefreshEquipmentList();

                // Check if item still needs repair
                if (!playerRepair.NeedsRepair(selectedEquipment))
                {
                    // Item fully repaired - clear selection
                    ClearSelection();
                }
                else
                {
                    // Item still needs repair - update preview
                    // Need to re-select the equipment since the list was refreshed
                    int index = repairableSlotIndices.IndexOf(selectedSlotIndex);
                    if (index >= 0)
                    {
                        int slotIndex = selectedSlotIndex;
                        selectedSlotIndex = -1;
                        SelectEquipment(slotIndex);
                    }
                    else
                    {
                        ClearSelection();
                    }
                }
            }
            else
            {
                Debug.LogWarning($"[RepairStationUI] Repair failed: {result.FailureReason}");
            }
        }

        private void HandleRepairCompleted(RepairResult result)
        {
            if (!isOpen) return;

            // Refresh the preview (fragment count changed)
            if (selectedEquipment != null)
            {
                UpdatePreview();
            }
        }

        /// <summary>
        /// Show brief visual feedback on successful repair.
        /// </summary>
        private void ShowRepairSuccessFlash()
        {
            if (previewPanel == null) return;

            previewPanel.AddToClassList("repair-success-flash");

            // Remove flash class after animation completes
            previewPanel.schedule.Execute(() =>
            {
                previewPanel.RemoveFromClassList("repair-success-flash");
            }).ExecuteLater(400);
        }

        private void HandleInventoryChanged()
        {
            if (!isOpen) return;

            // Refresh preview if we have a selection
            if (selectedEquipment != null)
            {
                UpdatePreview();
            }
        }

        private static string GetFailureReasonText(RepairFailureReason reason)
        {
            return reason switch
            {
                RepairFailureReason.NoFragmentsAvailable => "No repair fragments available",
                RepairFailureReason.InsufficientFragments => "Not enough repair fragments",
                RepairFailureReason.AlreadyFullDurability => "Already at full durability",
                RepairFailureReason.BrokenRepairNotAllowed => "Cannot repair broken equipment",
                RepairFailureReason.NoEquipmentSelected => "No equipment selected",
                RepairFailureReason.InvalidConfig => "Repair system not configured",
                RepairFailureReason.InventoryTransactionFailed => "Inventory error",
                _ => "Cannot repair"
            };
        }

        private static Color GetRarityColor(ItemRarity rarity)
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

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Auto-find references if not set
            if (playerInventory == null)
            {
                playerInventory = FindFirstObjectByType<PlayerInventory>();
            }

            if (playerEquipment == null)
            {
                playerEquipment = FindFirstObjectByType<PlayerEquipment>();
            }

            if (playerRepair == null)
            {
                playerRepair = FindFirstObjectByType<PlayerRepair>();
            }
        }
#endif
    }
}

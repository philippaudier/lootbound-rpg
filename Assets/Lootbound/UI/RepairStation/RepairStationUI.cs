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
    /// UI controller for the Repair Station interface.
    /// Displays repairable equipment and handles repair operations.
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

        // Cached repair stations in scene
        private RepairStation[] cachedStations;

        // UI Elements
        private VisualElement root;
        private VisualElement stationPanel;
        private VisualElement equipmentListContainer;
        private VisualElement previewPanel;
        private Button repairButton;
        private Button closeButton;

        // Preview elements
        private Label previewName;
        private Label previewRarity;
        private VisualElement previewDurabilityBar;
        private Label previewCurrentDurability;
        private Label previewAfterDurability;
        private Label previewCost;
        private Label previewAvailable;
        private VisualElement affixesContainer;
        private Label previewConditionBefore;
        private Label previewConditionAfter;
        private Label noEquipmentLabel;
        private Label failureReasonLabel;
        private Label repairHistoryLabel;

        // State
        private bool isOpen;
        private RepairStation currentStation;
        private EquipmentData selectedEquipment;
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
            repairButton = root.Q<Button>("repair-button");
            closeButton = root.Q<Button>("close-button");

            // Preview elements
            previewName = root.Q<Label>("preview-name");
            previewRarity = root.Q<Label>("preview-rarity");
            previewDurabilityBar = root.Q<VisualElement>("durability-bar-fill");
            previewCurrentDurability = root.Q<Label>("current-durability");
            previewAfterDurability = root.Q<Label>("after-durability");
            previewCost = root.Q<Label>("repair-cost");
            previewAvailable = root.Q<Label>("fragments-available");
            affixesContainer = root.Q<VisualElement>("preview-affixes");
            previewConditionBefore = root.Q<Label>("condition-before");
            previewConditionAfter = root.Q<Label>("condition-after");
            noEquipmentLabel = root.Q<Label>("no-equipment-label");
            failureReasonLabel = root.Q<Label>("failure-reason");

            // Create repair history label dynamically (not in UXML)
            repairHistoryLabel = new Label();
            repairHistoryLabel.style.fontSize = 10;
            repairHistoryLabel.style.color = new Color(0.55f, 0.65f, 0.6f);
            repairHistoryLabel.style.marginTop = 4;
            repairHistoryLabel.style.marginBottom = 4;
            repairHistoryLabel.style.display = DisplayStyle.None;
            if (affixesContainer != null && affixesContainer.parent != null)
            {
                // Insert after affixes container
                int index = affixesContainer.parent.IndexOf(affixesContainer);
                affixesContainer.parent.Insert(index + 1, repairHistoryLabel);
            }

            // Button callbacks
            if (closeButton != null)
            {
                closeButton.clicked += Close;
            }

            if (repairButton != null)
            {
                repairButton.clicked += HandleRepairClicked;
            }

            // Hide by default
            if (stationPanel != null)
            {
                stationPanel.style.display = DisplayStyle.None;
            }

            // Start with picking disabled - will enable when opened
            root.pickingMode = PickingMode.Ignore;
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

            // Slice 0.7.7: Panel open animation
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
            UpdateFragmentCount();

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
            element.AddToClassList("equipment-item");
            element.userData = slotIndex;

            // Icon
            var icon = new VisualElement();
            icon.AddToClassList("equipment-icon");
            if (definition.Icon != null)
            {
                icon.style.backgroundImage = new StyleBackground(definition.Icon);
            }
            element.Add(icon);

            // Info container
            var info = new VisualElement();
            info.AddToClassList("equipment-info");

            // Name (use attuned display name for attuned equipment)
            string displayName = equipData.IsAttuned
                ? equipData.GetAttunedDisplayName(equipmentRegistry)
                : (equipData.CustomName ?? definition.DisplayName);
            var nameLabel = new Label(displayName);
            nameLabel.AddToClassList("equipment-name");
            nameLabel.style.color = GetRarityColor(equipData.Rarity);
            info.Add(nameLabel);

            // Condition
            var conditionLabel = new Label(equipData.Condition.ToString());
            conditionLabel.AddToClassList("equipment-condition");
            conditionLabel.style.color = EquipmentConditionHelper.GetConditionColor(equipData.Condition);
            info.Add(conditionLabel);

            // Durability bar
            var durabilityContainer = new VisualElement();
            durabilityContainer.AddToClassList("equipment-durability-container");

            var durabilityFill = new VisualElement();
            durabilityFill.AddToClassList("equipment-durability-fill");
            durabilityFill.style.width = new Length(equipData.NormalizedDurability * 100f, LengthUnit.Percent);
            durabilityFill.style.backgroundColor = EquipmentConditionHelper.GetConditionColor(equipData.Condition);
            durabilityContainer.Add(durabilityFill);

            info.Add(durabilityContainer);
            element.Add(info);

            // Click handler
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
                    equipmentElements[elementIndex].RemoveFromClassList("equipment-selected");
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
                equipmentElements[newElementIndex].AddToClassList("equipment-selected");
            }

            // Get equipment data
            var slot = playerInventory.Inventory.GetSlot(slotIndex);
            if (slot != null && !slot.IsEmpty && slot.Item.HasEquipmentData)
            {
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
                    equipmentElements[elementIndex].RemoveFromClassList("equipment-selected");
                }
            }

            selectedSlotIndex = -1;
            selectedEquipment = null;
            HidePreview();
        }

        private void UpdatePreview()
        {
            if (previewPanel == null || selectedEquipment == null) return;

            previewPanel.style.display = DisplayStyle.Flex;

            // Get repair preview
            var preview = playerRepair.PreviewRepair(selectedEquipment);

            // Name and rarity (use attuned display name)
            if (previewName != null)
            {
                previewName.text = selectedEquipment.IsAttuned
                    ? selectedEquipment.GetAttunedDisplayName(equipmentRegistry)
                    : (selectedEquipment.CustomName ?? "Equipment");
                previewName.style.color = GetRarityColor(selectedEquipment.Rarity);
            }

            if (previewRarity != null)
            {
                previewRarity.text = selectedEquipment.Rarity.ToString();
                previewRarity.style.color = GetRarityColor(selectedEquipment.Rarity);
            }

            // Affixes
            UpdateAffixesDisplay();

            // Repair history (discrete summary)
            UpdateRepairHistoryDisplay();

            // Current durability
            if (previewCurrentDurability != null)
            {
                previewCurrentDurability.text = $"{selectedEquipment.CurrentDurability:F0} / {selectedEquipment.MaxDurability:F0}";
            }

            // Durability bar
            if (previewDurabilityBar != null)
            {
                previewDurabilityBar.style.width = new Length(selectedEquipment.NormalizedDurability * 100f, LengthUnit.Percent);
                previewDurabilityBar.style.backgroundColor = EquipmentConditionHelper.GetConditionColor(selectedEquipment.Condition);
            }

            // Condition before
            if (previewConditionBefore != null)
            {
                previewConditionBefore.text = selectedEquipment.Condition.ToString();
                previewConditionBefore.style.color = EquipmentConditionHelper.GetConditionColor(selectedEquipment.Condition);
            }

            if (preview.CanRepair)
            {
                // After repair
                if (previewAfterDurability != null)
                {
                    previewAfterDurability.text = $"{preview.DurabilityAfterRepair:F0} / {selectedEquipment.MaxDurability:F0}";
                    previewAfterDurability.style.display = DisplayStyle.Flex;
                }

                // Condition after
                if (previewConditionAfter != null)
                {
                    previewConditionAfter.text = preview.ConditionAfter.ToString();
                    previewConditionAfter.style.color = EquipmentConditionHelper.GetConditionColor(preview.ConditionAfter);
                    previewConditionAfter.style.display = DisplayStyle.Flex;
                }

                // Cost
                if (previewCost != null)
                {
                    previewCost.text = $"{preview.FragmentsToConsume} Repair Fragments";
                    previewCost.style.display = DisplayStyle.Flex;
                }

                // Repair button enabled
                if (repairButton != null)
                {
                    repairButton.SetEnabled(true);
                    repairButton.text = preview.IsFullRepair ? "Full Repair" : "Repair";
                }

                // Hide failure reason
                if (failureReasonLabel != null)
                {
                    failureReasonLabel.style.display = DisplayStyle.None;
                }
            }
            else
            {
                // Cannot repair
                if (previewAfterDurability != null)
                {
                    previewAfterDurability.style.display = DisplayStyle.None;
                }

                if (previewConditionAfter != null)
                {
                    previewConditionAfter.style.display = DisplayStyle.None;
                }

                if (previewCost != null)
                {
                    previewCost.style.display = DisplayStyle.None;
                }

                if (repairButton != null)
                {
                    repairButton.SetEnabled(false);
                    repairButton.text = "Cannot Repair";
                }

                // Show failure reason
                if (failureReasonLabel != null)
                {
                    failureReasonLabel.text = GetFailureReasonText(preview.FailureReason);
                    failureReasonLabel.style.display = DisplayStyle.Flex;
                }
            }
        }

        private void UpdateAffixesDisplay()
        {
            if (affixesContainer == null || selectedEquipment == null) return;

            affixesContainer.Clear();

            if (selectedEquipment.Affixes == null || selectedEquipment.Affixes.Count == 0)
            {
                affixesContainer.style.display = DisplayStyle.None;
                return;
            }

            affixesContainer.style.display = DisplayStyle.Flex;

            foreach (var affix in selectedEquipment.Affixes)
            {
                var affixDef = equipmentRegistry?.GetAffixDefinition(affix.DefinitionId);
                if (affixDef == null) continue;

                var label = new Label($"+ {affix.RolledValue:F0}% {affixDef.DisplayName}");
                label.AddToClassList("affix-label");
                label.style.color = new Color(0.5f, 0.8f, 0.5f);
                affixesContainer.Add(label);
            }
        }

        private void UpdateRepairHistoryDisplay()
        {
            if (repairHistoryLabel == null || selectedEquipment == null) return;

            if (selectedEquipment.History != null && selectedEquipment.History.HasBeenRepaired)
            {
                repairHistoryLabel.text = selectedEquipment.History.GetRepairSummary();
                repairHistoryLabel.style.display = DisplayStyle.Flex;
            }
            else
            {
                repairHistoryLabel.style.display = DisplayStyle.None;
            }
        }

        private void HidePreview()
        {
            if (previewPanel != null)
            {
                previewPanel.style.display = DisplayStyle.None;
            }

            if (repairButton != null)
            {
                repairButton.SetEnabled(false);
            }
        }

        private void UpdateFragmentCount()
        {
            if (previewAvailable == null || playerRepair == null) return;

            int fragments = playerRepair.GetAvailableFragments();
            previewAvailable.text = $"Available: {fragments}";
        }

        private void HandleRepairClicked()
        {
            if (selectedEquipment == null || playerRepair == null) return;

            var result = playerRepair.RepairEquipment(selectedEquipment);

            if (result.Success)
            {
                Debug.Log($"[RepairStationUI] Repaired {result.EquipmentName}: " +
                    $"{result.DurabilityBefore:F0} -> {result.DurabilityAfter:F0}");

                // Slice 0.7.7: Visual success feedback
                ShowRepairSuccessFlash();

                // Refresh UI
                UpdateFragmentCount();
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
                        SelectEquipment(selectedSlotIndex);
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

            // Refresh fragment count
            UpdateFragmentCount();
        }

        /// <summary>
        /// Slice 0.7.7: Show brief visual feedback on successful repair.
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

            UpdateFragmentCount();

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
                ItemRarity.Uncommon => new Color(0.2f, 0.8f, 0.2f),
                ItemRarity.Rare => new Color(0.2f, 0.4f, 1f),
                ItemRarity.Epic => new Color(0.6f, 0.2f, 0.8f),
                ItemRarity.Legendary => new Color(1f, 0.6f, 0.1f),
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

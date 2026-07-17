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
    /// UI controller for the Attunement Table interface.
    /// Displays attuneable equipment and handles attunement operations using stones.
    /// </summary>
    /// <remarks>
    /// The Attunement Table UI allows players to:
    /// - Select a weapon from their inventory
    /// - View current attunement level and stats
    /// - Preview the results of deepening attunement
    /// - Consume Attunement Stones to increase level
    ///
    /// Unlike the Repair Station which restores equipment,
    /// the Attunement Table reveals what equipment can become.
    /// </remarks>
    public class AttunementTableUI : MonoBehaviour
    {
        [Header("UI Document")]
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private int sortOrder = 110;

        [Header("Player References")]
        [SerializeField] private PlayerInputReader inputReader;
        [SerializeField] private PlayerCameraController cameraController;
        [SerializeField] private PlayerInventory playerInventory;
        [SerializeField] private PlayerEquipment playerEquipment;
        [SerializeField] private EquipmentRegistry equipmentRegistry;

        [Header("Ritual")]
        [SerializeField] private AttunementRitualController ritualController;

        // Cached attunement tables in scene
        private AttunementTable[] cachedTables;

        // UI Elements
        private VisualElement root;
        private VisualElement tablePanel;
        private VisualElement equipmentListContainer;
        private VisualElement previewPanel;
        private VisualElement resultPanel;
        private Button attuneButton;
        private Button closeButton;

        // Preview elements
        private Label previewName;
        private Label previewRarity;
        private VisualElement affixesContainer;
        private Label previewCondition;
        private Label previewCurrentLevel;
        private Label previewNextLevel;
        private Label previewMaxLevel;
        private Label previewSuccessChance;
        private Label previewStoneCost;
        private Label previewStonesAvailable;
        private Label previewCurrentDamage;
        private Label previewNextDamage;
        private Label noEquipmentLabel;
        private Label failureReasonLabel;
        private VisualElement statPreviewContainer;
        private Label brokenWarningLabel;
        private Label historyLabel;

        // Result panel elements
        private Label resultTitle;
        private Label resultMessage;
        private Label resultStats;
        private Button resultDismissButton;

        // State
        private bool isOpen;
        private AttunementTable currentTable;
        private EquipmentData selectedEquipment;
        private int selectedSlotIndex = -1;
        private List<int> attuneableSlotIndices = new List<int>();
        private List<VisualElement> equipmentElements = new List<VisualElement>();
        private bool isAttemptInProgress;

        // Pending ritual result (transaction resolved before ritual starts)
        private AttunementAttemptResult pendingResult;
        private float pendingPreviousDamage;
        private string pendingEquipmentName;

        public bool IsOpen => isOpen;

        /// <summary>
        /// Event raised when the table UI is closed.
        /// </summary>
        public event Action OnClosed;

        private void Awake()
        {
            if (uiDocument == null)
            {
                Debug.LogError("[AttunementTableUI] UIDocument is not assigned!");
                return;
            }

            SetupUI();
        }

        private void OnEnable()
        {
            if (playerInventory?.Inventory != null)
            {
                playerInventory.Inventory.OnInventoryChanged += HandleInventoryChanged;
            }

            if (ritualController != null)
            {
                ritualController.OnRitualComplete += HandleRitualComplete;
                ritualController.OnRitualCancelled += HandleRitualCancelled;
            }

            SubscribeToTables();
        }

        private void OnDisable()
        {
            if (playerInventory?.Inventory != null)
            {
                playerInventory.Inventory.OnInventoryChanged -= HandleInventoryChanged;
            }

            if (ritualController != null)
            {
                ritualController.OnRitualComplete -= HandleRitualComplete;
                ritualController.OnRitualCancelled -= HandleRitualCancelled;
            }

            UnsubscribeFromTables();
        }

        private void Update()
        {
            // Handle Escape key to close (blocked during ritual)
            if (isOpen && inputReader != null && inputReader.PausePressedThisFrame)
            {
                // Don't allow closing during ritual - the transaction is already resolved
                // but we want to show the result properly
                if (isAttemptInProgress)
                {
                    return;
                }
                Close();
            }
        }

        private void SubscribeToTables()
        {
            // Find all attunement tables in scene and subscribe to their events
            cachedTables = FindObjectsByType<AttunementTable>(FindObjectsSortMode.None);
            foreach (var table in cachedTables)
            {
                table.OnInteractionRequested += HandleTableInteraction;
            }
        }

        private void UnsubscribeFromTables()
        {
            if (cachedTables == null) return;

            foreach (var table in cachedTables)
            {
                if (table != null)
                {
                    table.OnInteractionRequested -= HandleTableInteraction;
                }
            }
            cachedTables = null;
        }

        private void HandleTableInteraction(AttunementTable table)
        {
            Open(table);
        }

        private void SetupUI()
        {
            root = uiDocument.rootVisualElement;
            uiDocument.sortingOrder = sortOrder;

            tablePanel = root.Q<VisualElement>("attunement-table-panel");
            equipmentListContainer = root.Q<VisualElement>("equipment-list");
            previewPanel = root.Q<VisualElement>("attunement-preview");
            resultPanel = root.Q<VisualElement>("result-panel");
            attuneButton = root.Q<Button>("attune-button");
            closeButton = root.Q<Button>("close-button");

            // Preview elements
            previewName = root.Q<Label>("preview-name");
            previewRarity = root.Q<Label>("preview-rarity");
            affixesContainer = root.Q<VisualElement>("preview-affixes");
            previewCondition = root.Q<Label>("preview-condition");
            previewCurrentLevel = root.Q<Label>("current-level");
            previewNextLevel = root.Q<Label>("next-level");
            previewMaxLevel = root.Q<Label>("max-level");
            previewSuccessChance = root.Q<Label>("success-chance");
            previewStoneCost = root.Q<Label>("stone-cost");
            previewStonesAvailable = root.Q<Label>("stones-available");
            previewCurrentDamage = root.Q<Label>("current-damage");
            previewNextDamage = root.Q<Label>("next-damage");
            noEquipmentLabel = root.Q<Label>("no-equipment-label");
            failureReasonLabel = root.Q<Label>("failure-reason");
            statPreviewContainer = root.Q<VisualElement>("stat-preview");
            brokenWarningLabel = root.Q<Label>("broken-warning");
            historyLabel = root.Q<Label>("equipment-history");

            // Result panel elements
            resultTitle = root.Q<Label>("result-title");
            resultMessage = root.Q<Label>("result-message");
            resultStats = root.Q<Label>("result-stats");
            resultDismissButton = root.Q<Button>("result-dismiss-button");

            // Button callbacks
            if (closeButton != null)
            {
                closeButton.clicked += Close;
            }

            if (attuneButton != null)
            {
                attuneButton.clicked += HandleAttuneClicked;
            }

            if (resultDismissButton != null)
            {
                resultDismissButton.clicked += DismissResult;
            }

            // Hide by default
            if (tablePanel != null)
            {
                tablePanel.style.display = DisplayStyle.None;
            }

            if (resultPanel != null)
            {
                resultPanel.style.display = DisplayStyle.None;
            }

            // Start with picking disabled
            root.pickingMode = PickingMode.Ignore;
        }

        /// <summary>
        /// Open the attunement table UI.
        /// </summary>
        public void Open(AttunementTable table)
        {
            if (tablePanel == null) return;
            if (isOpen) return;

            currentTable = table;
            isOpen = true;
            isAttemptInProgress = false;

            // Panel open animation
            tablePanel.AddToClassList("attunement-table-panel-opening");
            tablePanel.style.display = DisplayStyle.Flex;

            // Remove opening class after a frame to trigger transition
            tablePanel.schedule.Execute(() =>
            {
                tablePanel.RemoveFromClassList("attunement-table-panel-opening");
            }).ExecuteLater(10);

            // Enable pointer events on this UI
            root.pickingMode = PickingMode.Position;

            // Mark table as in use
            if (currentTable != null)
            {
                currentTable.SetInUse(true);
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
            UpdateStoneCount();
            HideResult();

            // Select default equipment
            SelectDefaultEquipment();

            Debug.Log("[AttunementTableUI] Opened");
        }

        /// <summary>
        /// Close the attunement table UI.
        /// </summary>
        public void Close()
        {
            if (tablePanel == null) return;
            if (!isOpen) return;

            // Block closing during ritual - transaction is resolved but we need to show the result
            if (isAttemptInProgress)
            {
                return;
            }

            isOpen = false;
            tablePanel.style.display = DisplayStyle.None;

            // Disable pointer events
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

            // Notify table that we're closed
            currentTable?.Close();
            currentTable = null;

            ClearSelection();
            OnClosed?.Invoke();

            Debug.Log("[AttunementTableUI] Closed");
        }

        private void RefreshEquipmentList()
        {
            if (equipmentListContainer == null || playerInventory?.Inventory == null) return;

            equipmentListContainer.Clear();
            equipmentElements.Clear();
            attuneableSlotIndices.Clear();

            var inventory = playerInventory.Inventory;

            // Build list of all equipment in inventory
            var equipmentSlots = new List<(int slotIndex, ItemInstance item, EquipmentData data)>();

            for (int i = 0; i < inventory.Capacity; i++)
            {
                var slot = inventory.GetSlot(i);
                if (slot == null || slot.IsEmpty) continue;

                var item = slot.Item;
                if (!item.HasEquipmentData) continue;

                equipmentSlots.Add((i, item, item.EquipmentData));
            }

            // Sort: equipped first, then non-maximum, then by level descending, then by name
            int equippedSlotIndex = playerEquipment?.EquippedSlotIndex ?? -1;

            equipmentSlots.Sort((a, b) =>
            {
                // Equipped weapon first
                if (a.slotIndex == equippedSlotIndex && b.slotIndex != equippedSlotIndex) return -1;
                if (b.slotIndex == equippedSlotIndex && a.slotIndex != equippedSlotIndex) return 1;

                // Non-maximum before maximum
                bool aIsMax = a.data.IsAtMaximumAttunement;
                bool bIsMax = b.data.IsAtMaximumAttunement;
                if (!aIsMax && bIsMax) return -1;
                if (aIsMax && !bIsMax) return 1;

                // Higher level first (among non-max)
                int levelCompare = b.data.AttunementLevel.CompareTo(a.data.AttunementLevel);
                if (levelCompare != 0) return levelCompare;

                // Then by name
                string aName = a.data.CustomName ?? a.item.Definition?.DisplayName ?? "";
                string bName = b.data.CustomName ?? b.item.Definition?.DisplayName ?? "";
                return string.Compare(aName, bName, StringComparison.OrdinalIgnoreCase);
            });

            // Create UI elements
            foreach (var (slotIndex, item, equipData) in equipmentSlots)
            {
                attuneableSlotIndices.Add(slotIndex);
                var element = CreateEquipmentElement(item, slotIndex, slotIndex == equippedSlotIndex);
                equipmentListContainer.Add(element);
                equipmentElements.Add(element);
            }

            // Show "no equipment" message if empty
            if (noEquipmentLabel != null)
            {
                noEquipmentLabel.style.display = attuneableSlotIndices.Count == 0
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            }
        }

        private VisualElement CreateEquipmentElement(ItemInstance item, int slotIndex, bool isEquipped)
        {
            var equipData = item.EquipmentData;
            var definition = item.Definition;

            var element = new VisualElement();
            element.AddToClassList("equipment-item");
            if (isEquipped)
            {
                element.AddToClassList("equipment-equipped");
            }
            if (equipData.IsAtMaximumAttunement)
            {
                element.AddToClassList("equipment-maximum");
            }
            element.userData = slotIndex;

            // Icon
            var icon = new VisualElement();
            icon.AddToClassList("equipment-icon");
            if (definition?.Icon != null)
            {
                icon.style.backgroundImage = new StyleBackground(definition.Icon);
            }
            element.Add(icon);

            // Info container
            var info = new VisualElement();
            info.AddToClassList("equipment-info");

            // Name (use attuned display name)
            string displayName = equipData.IsAttuned
                ? equipData.GetAttunedDisplayName(equipmentRegistry)
                : (equipData.CustomName ?? definition?.DisplayName ?? "Equipment");
            var nameLabel = new Label(displayName);
            nameLabel.AddToClassList("equipment-name");
            nameLabel.style.color = GetRarityColor(equipData.Rarity);
            info.Add(nameLabel);

            // Attunement level indicator
            var levelContainer = new VisualElement();
            levelContainer.AddToClassList("equipment-level-container");

            var levelLabel = new Label($"+{equipData.AttunementLevel}");
            levelLabel.AddToClassList("equipment-level");
            if (equipData.IsAtMaximumAttunement)
            {
                levelLabel.AddToClassList("equipment-level-maximum");
            }
            levelContainer.Add(levelLabel);

            if (isEquipped)
            {
                var equippedLabel = new Label("Equipped");
                equippedLabel.AddToClassList("equipped-badge");
                levelContainer.Add(equippedLabel);
            }

            info.Add(levelContainer);
            element.Add(info);

            // Click handler
            element.RegisterCallback<ClickEvent>(evt => SelectEquipment(slotIndex));

            return element;
        }

        private void SelectDefaultEquipment()
        {
            if (attuneableSlotIndices.Count == 0) return;

            // Try to select equipped weapon first
            int equippedSlotIndex = playerEquipment?.EquippedSlotIndex ?? -1;
            if (equippedSlotIndex >= 0 && attuneableSlotIndices.Contains(equippedSlotIndex))
            {
                SelectEquipment(equippedSlotIndex);
                return;
            }

            // Otherwise select first attuneable (non-maximum) weapon
            foreach (int slotIndex in attuneableSlotIndices)
            {
                var slot = playerInventory.Inventory.GetSlot(slotIndex);
                if (slot != null && !slot.IsEmpty && slot.Item.HasEquipmentData)
                {
                    if (!slot.Item.EquipmentData.IsAtMaximumAttunement)
                    {
                        SelectEquipment(slotIndex);
                        return;
                    }
                }
            }

            // If all are at maximum, select the first one
            if (attuneableSlotIndices.Count > 0)
            {
                SelectEquipment(attuneableSlotIndices[0]);
            }
        }

        private void SelectEquipment(int slotIndex)
        {
            // Block selection changes during ritual
            if (isAttemptInProgress)
            {
                return;
            }

            // Deselect previous
            if (selectedSlotIndex >= 0)
            {
                int elementIndex = attuneableSlotIndices.IndexOf(selectedSlotIndex);
                if (elementIndex >= 0 && elementIndex < equipmentElements.Count)
                {
                    equipmentElements[elementIndex].RemoveFromClassList("equipment-selected");
                }
            }

            // Toggle selection off if clicking the same item
            if (slotIndex == selectedSlotIndex)
            {
                ClearSelection();
                return;
            }

            selectedSlotIndex = slotIndex;

            // Highlight new selection
            int newElementIndex = attuneableSlotIndices.IndexOf(slotIndex);
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
                int elementIndex = attuneableSlotIndices.IndexOf(selectedSlotIndex);
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

            // Get attunement service and preview
            var service = playerEquipment?.GetAttunementService();
            var preview = service?.PreviewAttempt(selectedEquipment)
                ?? AttunementAttemptPreview.CannotAttempt(AttunementFailureReason.InvalidConfiguration);

            // Name and rarity
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

            // Condition
            if (previewCondition != null)
            {
                previewCondition.text = selectedEquipment.Condition.ToString();
                previewCondition.style.color = EquipmentConditionHelper.GetConditionColor(selectedEquipment.Condition);
            }

            // Broken warning
            if (brokenWarningLabel != null)
            {
                bool isBroken = selectedEquipment.Condition == EquipmentCondition.Broken;
                brokenWarningLabel.style.display = isBroken ? DisplayStyle.Flex : DisplayStyle.None;
            }

            // History
            UpdateHistoryDisplay();

            // Attunement levels
            int currentLevel = selectedEquipment.AttunementLevel;
            int maxLevel = preview.MaximumLevel;

            if (previewCurrentLevel != null)
            {
                previewCurrentLevel.text = $"+{currentLevel}";
            }

            if (previewNextLevel != null)
            {
                if (preview.IsAtMaximum)
                {
                    previewNextLevel.text = "Maximum";
                    previewNextLevel.style.color = new Color(1f, 0.8f, 0.3f);
                }
                else
                {
                    previewNextLevel.text = $"+{preview.ResultingLevelOnSuccess}";
                    previewNextLevel.style.color = new Color(0.6f, 0.9f, 0.6f);
                }
            }

            if (previewMaxLevel != null)
            {
                previewMaxLevel.text = $"/ +{maxLevel}";
            }

            // Success chance with protection breakdown
            if (previewSuccessChance != null)
            {
                if (preview.IsGuaranteed)
                {
                    previewSuccessChance.text = "GUARANTEED";
                    previewSuccessChance.style.color = new Color(0.4f, 0.9f, 1f);
                }
                else if (preview.HasProtection)
                {
                    // Show breakdown: base + protection = total
                    previewSuccessChance.text = $"{preview.SuccessChance:P0} ({preview.BaseChance:P0} + {preview.ProtectionBonusPercent:F0}%)";
                    previewSuccessChance.style.color = new Color(0.9f, 0.7f, 0.3f);
                }
                else
                {
                    previewSuccessChance.text = $"{preview.SuccessChance:P0}";
                    previewSuccessChance.style.color = preview.SuccessChance >= 1f
                        ? new Color(0.5f, 0.9f, 0.5f)
                        : new Color(1f, 0.8f, 0.4f);
                }
            }

            // Stone cost
            if (previewStoneCost != null)
            {
                previewStoneCost.text = $"{preview.RequiredStones}";
            }

            // Stones available
            UpdateStoneCount();

            // Stat preview
            UpdateStatPreview(preview);

            // Button state
            if (attuneButton != null)
            {
                bool canAttempt = preview.CanAttempt && !isAttemptInProgress;
                attuneButton.SetEnabled(canAttempt);

                if (preview.IsAtMaximum)
                {
                    attuneButton.text = "Maximum Reached";
                }
                else if (!preview.HasEnoughStones)
                {
                    attuneButton.text = "Not Enough Stones";
                }
                else
                {
                    attuneButton.text = $"Deepen Attunement - {preview.RequiredStones} Stone";
                }
            }

            // Failure reason
            if (failureReasonLabel != null)
            {
                if (!preview.CanAttempt && !preview.IsAtMaximum)
                {
                    failureReasonLabel.text = GetFailureReasonText(preview.FailureReason);
                    failureReasonLabel.style.display = DisplayStyle.Flex;
                }
                else
                {
                    failureReasonLabel.style.display = DisplayStyle.None;
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

        private void UpdateHistoryDisplay()
        {
            if (historyLabel == null || selectedEquipment == null) return;

            var history = selectedEquipment.History;
            if (history != null && !string.IsNullOrEmpty(history.FoundLocation))
            {
                var parts = new List<string>();
                parts.Add($"Found in {history.FoundLocation}");
                if (history.EnemiesDefeated > 0)
                {
                    parts.Add($"{history.EnemiesDefeated} enemies defeated");
                }

                // Add attunement history summary (discrete)
                if (history.HasAttunementHistory)
                {
                    var attunement = history.Attunement;
                    parts.Add($"{attunement.TotalAttempts} attunement attempt{(attunement.TotalAttempts == 1 ? "" : "s")}");
                    if (attunement.FailedAttempts > 0)
                    {
                        parts.Add($"{attunement.FailedAttempts} failed");
                    }
                }

                historyLabel.text = string.Join(" | ", parts);
                historyLabel.style.display = DisplayStyle.Flex;
            }
            else
            {
                historyLabel.style.display = DisplayStyle.None;
            }
        }

        private void UpdateStatPreview(AttunementAttemptPreview preview)
        {
            if (statPreviewContainer == null || selectedEquipment == null) return;

            statPreviewContainer.Clear();

            if (preview.IsAtMaximum)
            {
                statPreviewContainer.style.display = DisplayStyle.None;
                return;
            }

            statPreviewContainer.style.display = DisplayStyle.Flex;

            // Calculate current and next stats
            var attunementConfig = playerEquipment?.AttunementConfig;
            var brokenConfig = playerEquipment?.BrokenConfig;

            var currentStats = selectedEquipment.ResolveStats(equipmentRegistry, brokenConfig, attunementConfig);

            // Temporarily calculate next level stats
            int nextLevel = selectedEquipment.AttunementLevel + 1;
            float currentDamageMultiplier = attunementConfig?.GetDamageMultiplier(selectedEquipment.AttunementLevel) ?? 1f;
            float nextDamageMultiplier = attunementConfig?.GetDamageMultiplier(nextLevel) ?? 1f;

            // Calculate the damage increase
            float damageRatio = nextDamageMultiplier / currentDamageMultiplier;
            float nextDamage = currentStats.Damage * damageRatio;

            // Only show stats that change
            if (Math.Abs(nextDamage - currentStats.Damage) > 0.01f)
            {
                var damageRow = CreateStatRow("Damage", $"{currentStats.Damage:F1}", $"{nextDamage:F1}");
                statPreviewContainer.Add(damageRow);
            }

            // Update simple labels if they exist
            if (previewCurrentDamage != null)
            {
                previewCurrentDamage.text = $"{currentStats.Damage:F1}";
            }

            if (previewNextDamage != null)
            {
                previewNextDamage.text = $"{nextDamage:F1}";
            }
        }

        private VisualElement CreateStatRow(string statName, string currentValue, string nextValue)
        {
            var row = new VisualElement();
            row.AddToClassList("stat-row");

            var nameLabel = new Label(statName);
            nameLabel.AddToClassList("stat-name");
            row.Add(nameLabel);

            var currentLabel = new Label(currentValue);
            currentLabel.AddToClassList("stat-current");
            row.Add(currentLabel);

            var arrowLabel = new Label("→");
            arrowLabel.AddToClassList("stat-arrow");
            row.Add(arrowLabel);

            var nextLabel = new Label(nextValue);
            nextLabel.AddToClassList("stat-next");
            row.Add(nextLabel);

            return row;
        }

        private void HidePreview()
        {
            if (previewPanel != null)
            {
                previewPanel.style.display = DisplayStyle.None;
            }

            if (attuneButton != null)
            {
                attuneButton.SetEnabled(false);
            }
        }

        private void UpdateStoneCount()
        {
            if (previewStonesAvailable == null) return;

            int stones = playerEquipment?.GetAvailableAttunementStones() ?? 0;
            previewStonesAvailable.text = $"Available: {stones}";

            if (stones == 0)
            {
                previewStonesAvailable.style.color = new Color(0.9f, 0.5f, 0.5f);
            }
            else
            {
                previewStonesAvailable.style.color = new Color(0.6f, 0.8f, 0.6f);
            }
        }

        private void HandleAttuneClicked()
        {
            if (selectedEquipment == null || isAttemptInProgress) return;

            // Prevent double-click
            isAttemptInProgress = true;
            if (attuneButton != null)
            {
                attuneButton.SetEnabled(false);
            }

            // Re-validate before attempt
            var service = playerEquipment?.GetAttunementService();
            if (service == null)
            {
                ShowResult(false, "Configuration Error", "Attunement service is not available.", null);
                isAttemptInProgress = false;
                return;
            }

            // Verify equipment still exists in inventory
            if (!VerifyEquipmentStillExists())
            {
                ShowResult(false, "Equipment Unavailable", "The selected weapon is no longer in your inventory.", null);
                isAttemptInProgress = false;
                RefreshEquipmentList();
                ClearSelection();
                return;
            }

            // Get preview to verify we can still attempt
            var preview = service.PreviewAttempt(selectedEquipment);
            if (!preview.CanAttempt)
            {
                ShowResult(false, "Cannot Attune", GetFailureReasonText(preview.FailureReason), null);
                isAttemptInProgress = false;
                UpdatePreview();
                return;
            }

            // Store pre-attempt data
            pendingPreviousDamage = GetCurrentDamage();
            pendingEquipmentName = selectedEquipment.IsAttuned
                ? selectedEquipment.GetAttunedDisplayName(equipmentRegistry)
                : (selectedEquipment.CustomName ?? "Equipment");

            // TRANSACTION: Resolve attunement BEFORE ritual starts
            string location = currentTable?.LocationName ?? AttunementService.DefaultAttunementLocation;
            pendingResult = service.TryAttune(selectedEquipment, location);

            // If we have a ritual controller, start the ritual presentation
            if (ritualController != null && pendingResult.WasAttemptResolved)
            {
                // Get weapon prefab for visual display (optional)
                GameObject weaponPrefab = null;
                var slot = playerInventory?.Inventory?.GetSlot(selectedSlotIndex);
                if (slot?.Item?.Definition is WeaponDefinition weaponDef)
                {
                    weaponPrefab = weaponDef.FirstPersonPrefab;
                }

                // Start ritual with pre-determined result
                ritualController.StartRitual(
                    pendingResult.Success,
                    pendingResult.WasGuaranteed,
                    pendingResult.PreviousLevel,
                    pendingResult.CurrentLevel,
                    weaponPrefab
                );

                Debug.Log($"[AttunementTableUI] Ritual started: success={pendingResult.Success}, " +
                          $"+{pendingResult.PreviousLevel} → +{pendingResult.CurrentLevel}");
            }
            else
            {
                // No ritual controller or attempt not made - show result immediately
                HandleRitualComplete(pendingResult.Success, pendingResult.WasGuaranteed,
                    pendingResult.PreviousLevel, pendingResult.CurrentLevel);
            }
        }

        private void HandleRitualComplete(bool success, bool wasGuaranteed, int previousLevel, int currentLevel)
        {
            if (pendingResult.Success)
            {
                float newDamage = GetCurrentDamage();
                string statsChange = $"Damage: {pendingPreviousDamage:F1} → {newDamage:F1}";

                ShowResult(true, "Attunement Deepened",
                    $"{pendingEquipmentName} reached +{pendingResult.CurrentLevel}.", statsChange);

                Debug.Log($"[AttunementTableUI] Success: +{pendingResult.PreviousLevel} → +{pendingResult.CurrentLevel}");
            }
            else if (pendingResult.WasRngFailure)
            {
                // RNG-based failure (stones consumed but no level increase)
                int resonanceCount = selectedEquipment?.ConsecutiveAttunementFailures ?? 0;

                string resonanceInfo = resonanceCount > 0
                    ? $"Accumulated Resonance: {resonanceCount}"
                    : "";

                ShowResult(false, "Attunement Failed",
                    $"The resonance was not strong enough. {pendingEquipmentName} remains at +{pendingResult.CurrentLevel}.",
                    resonanceInfo);

                Debug.Log($"[AttunementTableUI] RNG Failed: stayed at +{pendingResult.CurrentLevel} " +
                          $"({pendingResult.AttemptedChance:P0} chance), resonance={resonanceCount}");
            }
            else
            {
                ShowResult(false, "Cannot Attune", GetFailureReasonText(pendingResult.FailureReason), null);
                Debug.Log($"[AttunementTableUI] Failed: {pendingResult.FailureReason}");
            }

            isAttemptInProgress = false;

            // Refresh UI
            RefreshEquipmentList();
            UpdateStoneCount();

            // Re-select the equipment if it still exists
            if (attuneableSlotIndices.Contains(selectedSlotIndex))
            {
                int elementIndex = attuneableSlotIndices.IndexOf(selectedSlotIndex);
                if (elementIndex >= 0 && elementIndex < equipmentElements.Count)
                {
                    equipmentElements[elementIndex].AddToClassList("equipment-selected");
                }
                UpdatePreview();
            }
            else
            {
                ClearSelection();
            }
        }

        private void HandleRitualCancelled()
        {
            // Ritual was cancelled - result is already applied, just clean up UI state
            isAttemptInProgress = false;

            // Refresh UI to show current state
            RefreshEquipmentList();
            UpdateStoneCount();

            if (attuneableSlotIndices.Contains(selectedSlotIndex))
            {
                UpdatePreview();
            }

            Debug.Log("[AttunementTableUI] Ritual cancelled");
        }

        private bool VerifyEquipmentStillExists()
        {
            if (selectedSlotIndex < 0 || playerInventory?.Inventory == null) return false;

            var slot = playerInventory.Inventory.GetSlot(selectedSlotIndex);
            if (slot == null || slot.IsEmpty || !slot.Item.HasEquipmentData) return false;

            // Verify it's the same equipment
            return slot.Item.EquipmentData == selectedEquipment;
        }

        private float GetCurrentDamage()
        {
            if (selectedEquipment == null) return 0f;

            var attunementConfig = playerEquipment?.AttunementConfig;
            var brokenConfig = playerEquipment?.BrokenConfig;
            var stats = selectedEquipment.ResolveStats(equipmentRegistry, brokenConfig, attunementConfig);
            return stats.Damage;
        }

        private void ShowResult(bool success, string title, string message, string stats)
        {
            if (resultPanel == null) return;

            resultPanel.style.display = DisplayStyle.Flex;

            if (resultTitle != null)
            {
                resultTitle.text = title;
                resultTitle.style.color = success
                    ? new Color(0.6f, 0.9f, 0.6f)
                    : new Color(0.9f, 0.6f, 0.6f);
            }

            if (resultMessage != null)
            {
                resultMessage.text = message;
            }

            if (resultStats != null)
            {
                if (!string.IsNullOrEmpty(stats))
                {
                    resultStats.text = stats;
                    resultStats.style.display = DisplayStyle.Flex;
                }
                else
                {
                    resultStats.style.display = DisplayStyle.None;
                }
            }

            // Add success animation
            if (success)
            {
                resultPanel.AddToClassList("result-success");
                resultPanel.schedule.Execute(() =>
                {
                    resultPanel.RemoveFromClassList("result-success");
                }).ExecuteLater(400);
            }
        }

        private void HideResult()
        {
            if (resultPanel != null)
            {
                resultPanel.style.display = DisplayStyle.None;
            }
        }

        private void DismissResult()
        {
            HideResult();
        }

        private void HandleInventoryChanged()
        {
            if (!isOpen) return;

            UpdateStoneCount();

            // Refresh preview if we have a selection
            if (selectedEquipment != null)
            {
                if (VerifyEquipmentStillExists())
                {
                    UpdatePreview();
                }
                else
                {
                    RefreshEquipmentList();
                    ClearSelection();
                }
            }
        }

        private static string GetFailureReasonText(AttunementFailureReason reason)
        {
            return reason switch
            {
                AttunementFailureReason.None => "",
                AttunementFailureReason.InvalidEquipment => "Invalid equipment",
                AttunementFailureReason.MissingInventory => "Inventory not available",
                AttunementFailureReason.MissingStoneDefinition => "Stone configuration missing",
                AttunementFailureReason.NoAttunementStones => "No Attunement Stones available",
                AttunementFailureReason.InsufficientAttunementStones => "Not enough Attunement Stones",
                AttunementFailureReason.AlreadyAtMaximum => "Already at maximum attunement",
                AttunementFailureReason.InvalidConfiguration => "Attunement not configured",
                AttunementFailureReason.TransactionFailed => "Transaction failed",
                AttunementFailureReason.NotEquipment => "Item is not equipment",
                AttunementFailureReason.AttemptInProgress => "Attempt already in progress",
                _ => "Cannot attune"
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
        }
#endif
    }
}

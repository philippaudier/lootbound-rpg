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
    /// Attunement Table UI - the "deepen" page of the expedition journal.
    /// Left: every piece of equipment (equipped first). Right: the selected
    /// piece, its attunement level as a brass segmented track, current
    /// bonuses, the next attunement preview, materials, the accord outlook
    /// (real chance and resonance rules) and the item's history. The window
    /// footer (status + Cancel + Attune) never scrolls.
    ///
    /// The attunement transaction resolves BEFORE the ritual presentation
    /// starts (unchanged); the result overlay reports the outcome.
    /// </summary>
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

        [Header("Visuals")]
        [SerializeField]
        [Tooltip("Optional icon shown next to Attunement Stones in the materials list")]
        private Sprite stoneIcon;

        private const float MaxWindowWidth = 1180f;
        private const float MaxWindowHeight = 720f;
        private const float CompactWidthThreshold = 780f;

        // Cached attunement tables in scene
        private AttunementTable[] cachedTables;

        // UI Elements
        private VisualElement root;
        private VisualElement tablePanel;
        private VisualElement equipmentListContainer;
        private VisualElement previewPanel;
        private Label detailEmptyLabel;
        private VisualElement resultPanel;
        private Button attuneButton;
        private Button cancelButton;
        private Button closeButton;

        // Preview elements
        private VisualElement previewIcon;
        private Label previewName;
        private Label previewRarity;
        private Label previewCondition;
        private Label brokenWarningLabel;
        private Label attuneRank;
        private VisualElement attuneTrack;
        private Label attuneProgressNote;
        private VisualElement currentBonusesContainer;
        private VisualElement sectionNext;
        private VisualElement statPreviewContainer;
        private Label previewSuccessChance;
        private VisualElement resonanceRow;
        private Label resonanceBonus;
        private Label previewStoneCost;
        private Label previewStonesAvailable;
        private VisualElement stoneIconElement;
        private VisualElement sectionAffixes;
        private VisualElement affixesContainer;
        private VisualElement historyEntriesContainer;
        private Label noEquipmentLabel;
        private Label failureReasonLabel;
        private Label footerStatusLabel;

        // Result panel elements
        private Label resultTitle;
        private Label resultMessage;
        private Label resultStats;
        private Button resultDismissButton;

        // State
        private bool isOpen;
        private bool isCompact;
        private AttunementTable currentTable;
        private EquipmentData selectedEquipment;
        private ItemInstance selectedItem;
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
            detailEmptyLabel = root.Q<Label>("detail-empty");
            resultPanel = root.Q<VisualElement>("result-panel");
            attuneButton = root.Q<Button>("attune-button");
            cancelButton = root.Q<Button>("cancel-button");
            closeButton = root.Q<Button>("close-button");

            previewIcon = root.Q<VisualElement>("preview-icon");
            previewName = root.Q<Label>("preview-name");
            previewRarity = root.Q<Label>("preview-rarity");
            previewCondition = root.Q<Label>("preview-condition");
            brokenWarningLabel = root.Q<Label>("broken-warning");
            attuneRank = root.Q<Label>("attune-rank");
            attuneTrack = root.Q<VisualElement>("attune-track");
            attuneProgressNote = root.Q<Label>("attune-progress-note");
            currentBonusesContainer = root.Q<VisualElement>("current-bonuses");
            sectionNext = root.Q<VisualElement>("section-next");
            statPreviewContainer = root.Q<VisualElement>("stat-preview");
            previewSuccessChance = root.Q<Label>("success-chance");
            resonanceRow = root.Q<VisualElement>("resonance-row");
            resonanceBonus = root.Q<Label>("resonance-bonus");
            previewStoneCost = root.Q<Label>("stone-cost");
            previewStonesAvailable = root.Q<Label>("stones-available");
            stoneIconElement = root.Q<VisualElement>("stone-icon");
            sectionAffixes = root.Q<VisualElement>("section-affixes");
            affixesContainer = root.Q<VisualElement>("preview-affixes");
            historyEntriesContainer = root.Q<VisualElement>("history-entries");
            noEquipmentLabel = root.Q<Label>("no-equipment-label");
            failureReasonLabel = root.Q<Label>("failure-reason");
            footerStatusLabel = root.Q<Label>("footer-status");

            // Result panel elements
            resultTitle = root.Q<Label>("result-title");
            resultMessage = root.Q<Label>("result-message");
            resultStats = root.Q<Label>("result-stats");
            resultDismissButton = root.Q<Button>("result-dismiss-button");

            if (stoneIconElement != null && stoneIcon != null)
            {
                stoneIconElement.style.backgroundImage = new StyleBackground(stoneIcon);
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

            if (attuneButton != null)
            {
                attuneButton.clicked += HandleAttuneClicked;
            }

            if (resultDismissButton != null)
            {
                resultDismissButton.clicked += DismissResult;
            }

            // Compact mode: stack the detail columns on narrow windows.
            tablePanel?.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                bool shouldBeCompact = evt.newRect.width < CompactWidthThreshold;
                if (shouldBeCompact != isCompact)
                {
                    isCompact = shouldBeCompact;
                    tablePanel.EnableInClassList("compact", isCompact);
                }
            });

            // Hide by default
            if (tablePanel != null)
            {
                tablePanel.style.display = DisplayStyle.None;

                // Stretch the intermediate containers and enforce the window
                // bounds in pixels (same fix as the inventory: percent sizes
                // never resolve under the TemplateContainer).
                for (var element = tablePanel.parent; element != null && element != root; element = element.parent)
                {
                    element.style.flexGrow = 1;
                }

                root.RegisterCallback<GeometryChangedEvent>(_ => ApplyWindowBounds());
                ApplyWindowBounds();
            }

            if (resultPanel != null)
            {
                resultPanel.style.display = DisplayStyle.None;
            }

            // Start with picking disabled
            root.pickingMode = PickingMode.Ignore;
        }

        private void ApplyWindowBounds()
        {
            if (tablePanel == null || root == null) return;

            float rootWidth = root.resolvedStyle.width;
            float rootHeight = root.resolvedStyle.height;
            if (float.IsNaN(rootWidth) || rootWidth <= 0f) return;
            if (float.IsNaN(rootHeight) || rootHeight <= 0f) return;

            tablePanel.style.width = Mathf.Min(MaxWindowWidth, rootWidth * 0.92f);
            tablePanel.style.height = Mathf.Min(MaxWindowHeight, rootHeight * 0.92f);
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
            element.AddToClassList("select-entry");
            element.userData = slotIndex;

            var icon = new VisualElement();
            icon.AddToClassList("select-entry-icon");
            if (definition?.Icon != null)
            {
                icon.style.backgroundImage = new StyleBackground(definition.Icon);
            }
            element.Add(icon);

            var info = new VisualElement();
            info.AddToClassList("select-entry-text");

            // Name (use attuned display name)
            string displayName = equipData.IsAttuned
                ? equipData.GetAttunedDisplayName(equipmentRegistry)
                : (equipData.CustomName ?? definition?.DisplayName ?? "Equipment");
            var nameLabel = new Label(displayName);
            nameLabel.AddToClassList("select-entry-name");
            nameLabel.style.color = GetRarityColor(equipData.Rarity);
            info.Add(nameLabel);

            if (isEquipped)
            {
                var equippedLabel = new Label("Equipped");
                equippedLabel.AddToClassList("select-entry-sub");
                info.Add(equippedLabel);
            }

            element.Add(info);

            // Attunement level badge
            var levelLabel = new Label($"+{equipData.AttunementLevel}");
            levelLabel.AddToClassList("select-entry-badge");
            if (equipData.IsAtMaximumAttunement)
            {
                levelLabel.AddToClassList("level-maximum");
            }
            element.Add(levelLabel);

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
                    equipmentElements[elementIndex].RemoveFromClassList("selected");
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
                int elementIndex = attuneableSlotIndices.IndexOf(selectedSlotIndex);
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

            // Get attunement service and preview
            var service = playerEquipment?.GetAttunementService();
            var preview = service?.PreviewAttempt(selectedEquipment)
                ?? AttunementAttemptPreview.CannotAttempt(AttunementFailureReason.InvalidConfiguration);

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

            if (previewCondition != null)
            {
                previewCondition.text = selectedEquipment.Condition.ToString();
                previewCondition.style.color = EquipmentConditionHelper.GetConditionColor(selectedEquipment.Condition);
            }

            if (brokenWarningLabel != null)
            {
                bool isBroken = selectedEquipment.Condition == EquipmentCondition.Broken;
                brokenWarningLabel.style.display = isBroken ? DisplayStyle.Flex : DisplayStyle.None;
            }

            // --- Attunement level (brass segmented track) ---
            int currentLevel = selectedEquipment.AttunementLevel;
            int maxLevel = preview.MaximumLevel;

            if (attuneRank != null)
            {
                attuneRank.text = preview.IsAtMaximum
                    ? $"+{currentLevel} · Maximum"
                    : $"+{currentLevel} → +{preview.ResultingLevelOnSuccess}";
            }

            RebuildAttunementTrack(currentLevel, maxLevel, preview.IsAtMaximum);

            if (attuneProgressNote != null)
            {
                attuneProgressNote.text = preview.IsAtMaximum
                    ? "The bond runs as deep as it can."
                    : $"+{currentLevel} of +{maxLevel}";
            }

            // --- Current bonuses (real values only) ---
            UpdateCurrentBonuses();

            // --- Next attunement ---
            UpdateStatPreview(preview);

            // --- Materials ---
            int stones = playerEquipment?.GetAvailableAttunementStones() ?? 0;
            if (previewStonesAvailable != null)
            {
                previewStonesAvailable.text = stones.ToString();
                previewStonesAvailable.EnableInClassList("short",
                    stones < Mathf.Max(1, preview.RequiredStones));
            }
            if (previewStoneCost != null)
            {
                previewStoneCost.text = Mathf.Max(preview.RequiredStones, 1).ToString();
            }

            // --- Accord outlook ---
            string chanceText = "";
            if (previewSuccessChance != null)
            {
                if (preview.IsGuaranteed)
                {
                    chanceText = "GUARANTEED";
                    previewSuccessChance.style.color = new Color(0.4f, 0.9f, 1f);
                }
                else
                {
                    chanceText = $"{preview.SuccessChance:P0}";
                    previewSuccessChance.style.color = preview.SuccessChance >= 1f
                        ? new Color(0.5f, 0.9f, 0.5f)
                        : new Color(1f, 0.8f, 0.4f);
                }
                previewSuccessChance.text = chanceText;
            }

            if (resonanceRow != null)
            {
                resonanceRow.style.display = preview.HasProtection ? DisplayStyle.Flex : DisplayStyle.None;
                if (preview.HasProtection && resonanceBonus != null)
                {
                    resonanceBonus.text = $"+{preview.ProtectionBonusPercent:F0}% (base {preview.BaseChance:P0})";
                }
            }

            // --- Affixes and history ---
            UpdateAffixesDisplay();
            UpdateHistoryDisplay();

            // --- Button and footer status ---
            if (attuneButton != null)
            {
                bool canAttempt = preview.CanAttempt && !isAttemptInProgress;
                attuneButton.SetEnabled(canAttempt);
                attuneButton.text = "Attune";
            }

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

            if (preview.IsAtMaximum)
            {
                SetFooterStatus("Maximum attunement reached");
            }
            else if (preview.CanAttempt)
            {
                SetFooterStatus(preview.IsGuaranteed
                    ? "Ready to attune · the accord is guaranteed"
                    : $"Ready to attune · {chanceText} to deepen the bond");
            }
            else
            {
                SetFooterStatus(GetFailureReasonText(preview.FailureReason));
            }
        }

        private void RebuildAttunementTrack(int currentLevel, int maxLevel, bool isAtMaximum)
        {
            if (attuneTrack == null) return;

            attuneTrack.Clear();
            if (maxLevel <= 0) return;

            for (int level = 1; level <= maxLevel; level++)
            {
                var segment = new VisualElement();
                segment.AddToClassList("attune-seg");

                if (level < currentLevel)
                {
                    segment.AddToClassList("filled");
                    segment.AddToClassList("dim");
                }
                else if (level == currentLevel)
                {
                    segment.AddToClassList("filled");
                }
                else if (level == currentLevel + 1 && !isAtMaximum)
                {
                    segment.AddToClassList("next");
                }

                attuneTrack.Add(segment);
            }
        }

        private void UpdateCurrentBonuses()
        {
            if (currentBonusesContainer == null || selectedEquipment == null) return;

            currentBonusesContainer.Clear();

            var attunementConfig = playerEquipment?.AttunementConfig;
            var brokenConfig = playerEquipment?.BrokenConfig;
            var stats = selectedEquipment.ResolveStats(equipmentRegistry, brokenConfig, attunementConfig);

            if (stats.IsValid)
            {
                AddValueRow(currentBonusesContainer, "Damage", $"{stats.Damage:F1}", null);
            }

            if (selectedEquipment.IsAttuned && attunementConfig != null)
            {
                float bonusPercent = attunementConfig.GetDamageBonusPercent(selectedEquipment.AttunementLevel);
                if (bonusPercent > 0)
                {
                    AddValueRow(currentBonusesContainer, "Attunement bonus",
                        $"+{bonusPercent:F0}%", new Color(0.4f, 0.85f, 0.4f));
                }
            }
        }

        private void UpdateStatPreview(AttunementAttemptPreview preview)
        {
            if (statPreviewContainer == null || selectedEquipment == null) return;

            statPreviewContainer.Clear();

            bool showNext = !preview.IsAtMaximum;
            sectionNext?.EnableInClassList("hidden", !showNext);
            if (!showNext) return;

            // Calculate current and next stats
            var attunementConfig = playerEquipment?.AttunementConfig;
            var brokenConfig = playerEquipment?.BrokenConfig;

            var currentStats = selectedEquipment.ResolveStats(equipmentRegistry, brokenConfig, attunementConfig);

            int nextLevel = selectedEquipment.AttunementLevel + 1;
            float currentDamageMultiplier = attunementConfig?.GetDamageMultiplier(selectedEquipment.AttunementLevel) ?? 1f;
            float nextDamageMultiplier = attunementConfig?.GetDamageMultiplier(nextLevel) ?? 1f;

            float damageRatio = currentDamageMultiplier > 0f ? nextDamageMultiplier / currentDamageMultiplier : 1f;
            float nextDamage = currentStats.Damage * damageRatio;

            AddBeforeAfterRow(statPreviewContainer, "Damage",
                $"{currentStats.Damage:F1}", $"{nextDamage:F1}",
                Math.Abs(nextDamage - currentStats.Damage) > 0.01f);
        }

        private static void AddValueRow(VisualElement container, string label, string value, Color? valueColor)
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
        }

        private static void AddBeforeAfterRow(VisualElement container, string label,
            string before, string after, bool improves)
        {
            var row = new VisualElement();
            row.AddToClassList("delta-row");

            var nameLabel = new Label(label);
            nameLabel.AddToClassList("delta-label");

            var beforeLabel = new Label(before);
            beforeLabel.AddToClassList("next-before");

            var arrowLabel = new Label("→");
            arrowLabel.AddToClassList("next-arrow");

            var afterLabel = new Label(after);
            afterLabel.AddToClassList("next-after");
            if (!improves)
            {
                afterLabel.AddToClassList("neutral");
            }

            row.Add(nameLabel);
            row.Add(beforeLabel);
            row.Add(arrowLabel);
            row.Add(afterLabel);
            container.Add(row);
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

            if (!string.IsNullOrEmpty(history.FoundLocation))
            {
                AddHistoryEntry($"Found in {history.FoundLocation}.");
            }

            if (history.EnemiesDefeated > 0)
            {
                AddHistoryEntry($"{history.EnemiesDefeated} enemies defeated together.");
            }

            if (history.HasAttunementHistory)
            {
                var attunement = history.Attunement;
                string attempts = attunement.TotalAttempts == 1
                    ? "1 attunement attempt"
                    : $"{attunement.TotalAttempts} attunement attempts";
                string failures = attunement.FailedAttempts > 0
                    ? $" ({attunement.FailedAttempts} failed)"
                    : "";
                AddHistoryEntry($"{attempts}{failures}.");
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

            if (attuneButton != null)
            {
                attuneButton.SetEnabled(false);
            }

            SetFooterStatus(attuneableSlotIndices.Count == 0
                ? "No equipment to attune"
                : "Select equipment to deepen the bond");
        }

        private void SetFooterStatus(string text)
        {
            if (footerStatusLabel != null)
            {
                footerStatusLabel.text = text;
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

            // Re-select the equipment if it still exists
            if (attuneableSlotIndices.Contains(selectedSlotIndex))
            {
                int elementIndex = attuneableSlotIndices.IndexOf(selectedSlotIndex);
                if (elementIndex >= 0 && elementIndex < equipmentElements.Count)
                {
                    equipmentElements[elementIndex].AddToClassList("selected");
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
        }
#endif
    }
}

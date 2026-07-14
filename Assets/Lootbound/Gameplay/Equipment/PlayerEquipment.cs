using System;
using UnityEngine;
using Lootbound.Core.Logging;
using Lootbound.Gameplay.Inventory;
using Lootbound.Gameplay.Combat;
using Lootbound.Gameplay.Player;

namespace Lootbound.Gameplay.Equipment
{
    /// <summary>
    /// Manages the player's equipped items and links to combat system.
    /// Handles equipping, unequipping, and weapon swapping.
    /// </summary>
    public class PlayerEquipment : MonoBehaviour
    {
        private const string Category = "PlayerEquipment";

        [Header("Dependencies")]
        [SerializeField] private PlayerInventory playerInventory;
        [SerializeField] private PlayerMeleeWeapon meleeWeapon;
        [SerializeField] private PlayerCombatController combatController;
        [SerializeField] private EquipmentRegistry equipmentRegistry;
        [SerializeField] private PlayerWeaponWear playerWeaponWear;
        [SerializeField] private BrokenWeaponConfig brokenWeaponConfig;

        [Header("Weapon View")]
        [SerializeField] private Transform weaponViewSocket;

        // Currently equipped weapon slot index in inventory (-1 if none)
        private int equippedSlotIndex = -1;

        // Cached equipment data
        private EquipmentData currentEquipment;
        private ResolvedWeaponStats currentStats;
        private GameObject currentWeaponView;
        private EquipmentCondition previousCondition;

        /// <summary>
        /// Currently equipped equipment data.
        /// </summary>
        public EquipmentData CurrentEquipment => currentEquipment;

        /// <summary>
        /// Current resolved weapon stats.
        /// </summary>
        public ResolvedWeaponStats CurrentStats => currentStats;

        /// <summary>
        /// Whether a weapon is currently equipped.
        /// </summary>
        public bool HasWeaponEquipped => currentEquipment != null && currentEquipment.IsValid;

        /// <summary>
        /// The inventory slot index of the equipped weapon (-1 if none).
        /// </summary>
        public int EquippedSlotIndex => equippedSlotIndex;

        /// <summary>
        /// Fired when equipment changes.
        /// </summary>
        public event Action<EquipmentData> OnEquipmentChanged;

        /// <summary>
        /// Fired when a weapon is equipped.
        /// </summary>
        public event Action<ItemInstance> OnWeaponEquipped;

        /// <summary>
        /// Fired when a weapon is unequipped.
        /// </summary>
        public event Action OnWeaponUnequipped;

        /// <summary>
        /// Fired when equipment stats are recalculated (e.g., due to condition change).
        /// </summary>
        public event Action<ResolvedWeaponStats> OnStatsChanged;

        /// <summary>
        /// The broken weapon config used for penalty calculations.
        /// </summary>
        public BrokenWeaponConfig BrokenConfig => brokenWeaponConfig;

        private void Awake()
        {
            // Auto-find dependencies
            if (playerInventory == null)
            {
                playerInventory = GetComponentInParent<PlayerInventory>();
            }

            if (meleeWeapon == null)
            {
                meleeWeapon = GetComponentInChildren<PlayerMeleeWeapon>();
            }

            if (combatController == null)
            {
                combatController = GetComponentInParent<PlayerCombatController>();
            }

            if (playerWeaponWear == null)
            {
                playerWeaponWear = GetComponentInParent<PlayerWeaponWear>();
            }

            // Initialize with default stats
            currentStats = ResolvedWeaponStats.Default;
        }

        private void OnEnable()
        {
            // Subscribe to inventory changes to handle removed items
            if (playerInventory?.Inventory != null)
            {
                playerInventory.Inventory.OnSlotChanged += HandleSlotChanged;
            }

            // Subscribe to combat events to track kills
            if (combatController != null)
            {
                combatController.OnHitTarget += HandleCombatHit;
            }

            // Subscribe to condition changes for stat recalculation
            if (playerWeaponWear != null)
            {
                playerWeaponWear.OnConditionChanged += HandleConditionChanged;
            }
        }

        private void OnDisable()
        {
            if (playerInventory?.Inventory != null)
            {
                playerInventory.Inventory.OnSlotChanged -= HandleSlotChanged;
            }

            if (combatController != null)
            {
                combatController.OnHitTarget -= HandleCombatHit;
            }

            if (playerWeaponWear != null)
            {
                playerWeaponWear.OnConditionChanged -= HandleConditionChanged;
            }
        }

        private void HandleCombatHit(DamageResult result)
        {
            // If the hit was fatal and we have a weapon equipped, record the kill
            if (result.WasFatal && HasWeaponEquipped)
            {
                RecordKill();
                LootboundLog.Info(Category, $"Kill recorded with {currentEquipment.CustomName}. Total: {currentEquipment.History.EnemiesDefeated}");
            }
        }

        private void HandleConditionChanged(WearResult result)
        {
            if (!HasWeaponEquipped) return;

            // Recalculate stats with broken penalties if applicable
            RecalculateStats();

            // Log condition change impact
            if (result.NowBroken)
            {
                LootboundLog.Warning(Category, $"{currentEquipment.CustomName} has broken! Combat penalties applied.");
            }
        }

        /// <summary>
        /// Recalculate and reapply stats for the currently equipped weapon.
        /// Called when condition changes or when manually requested.
        /// </summary>
        public void RecalculateStats()
        {
            if (!HasWeaponEquipped) return;

            var oldStats = currentStats;
            currentStats = currentEquipment.ResolveStats(equipmentRegistry, brokenWeaponConfig);

            // Only apply if stats actually changed
            if (!StatsEqual(oldStats, currentStats))
            {
                if (meleeWeapon != null)
                {
                    meleeWeapon.SetEquipmentStats(currentStats);
                }

                LootboundLog.Info(Category, $"Stats recalculated: {currentStats}");
                OnStatsChanged?.Invoke(currentStats);
            }
        }

        private bool StatsEqual(ResolvedWeaponStats a, ResolvedWeaponStats b)
        {
            const float epsilon = 0.001f;
            return Mathf.Abs(a.Damage - b.Damage) < epsilon &&
                   Mathf.Abs(a.AttackSpeed - b.AttackSpeed) < epsilon &&
                   Mathf.Abs(a.Range - b.Range) < epsilon &&
                   Mathf.Abs(a.Stagger - b.Stagger) < epsilon;
        }

        /// <summary>
        /// Equip an item from a specific inventory slot.
        /// </summary>
        public bool TryEquip(int slotIndex)
        {
            if (playerInventory?.Inventory == null) return false;

            var slot = playerInventory.Inventory.GetSlot(slotIndex);
            if (slot == null || slot.IsEmpty) return false;

            var item = slot.Item;
            if (!item.HasEquipmentData)
            {
                LootboundLog.Warning(Category, $"Cannot equip non-equipment item: {item.Definition?.DisplayName}");
                return false;
            }

            // Check if it's a weapon
            if (item.Definition is not WeaponDefinition weaponDef)
            {
                LootboundLog.Warning(Category, $"Cannot equip non-weapon: {item.Definition?.DisplayName}");
                return false;
            }

            // Cannot equip during attack or dodge
            if (meleeWeapon != null && meleeWeapon.IsAttacking)
            {
                LootboundLog.Info(Category, "Cannot equip during attack");
                return false;
            }

            if (combatController != null && combatController.IsDodging)
            {
                LootboundLog.Info(Category, "Cannot equip during dodge");
                return false;
            }

            // Unequip current weapon first
            if (HasWeaponEquipped)
            {
                UnequipInternal();
            }

            // Equip the new weapon
            EquipInternal(slotIndex, item, weaponDef);
            return true;
        }

        /// <summary>
        /// Unequip the current weapon.
        /// </summary>
        public bool TryUnequip()
        {
            if (!HasWeaponEquipped) return false;

            // Cannot unequip during attack or dodge
            if (meleeWeapon != null && meleeWeapon.IsAttacking)
            {
                LootboundLog.Info(Category, "Cannot unequip during attack");
                return false;
            }

            if (combatController != null && combatController.IsDodging)
            {
                LootboundLog.Info(Category, "Cannot unequip during dodge");
                return false;
            }

            UnequipInternal();
            return true;
        }

        /// <summary>
        /// Check if an inventory slot contains the currently equipped item.
        /// </summary>
        public bool IsSlotEquipped(int slotIndex)
        {
            return equippedSlotIndex == slotIndex && HasWeaponEquipped;
        }

        /// <summary>
        /// Check if an item instance is the currently equipped item.
        /// </summary>
        public bool IsEquipped(ItemInstance item)
        {
            if (item?.EquipmentData == null || currentEquipment == null) return false;
            return item.EquipmentData.InstanceId == currentEquipment.InstanceId;
        }

        /// <summary>
        /// Record a kill with the equipped weapon.
        /// </summary>
        public void RecordKill()
        {
            currentEquipment?.RecordKill();
        }

        private void EquipInternal(int slotIndex, ItemInstance item, WeaponDefinition weaponDef)
        {
            equippedSlotIndex = slotIndex;
            currentEquipment = item.EquipmentData;
            currentEquipment.IsEquipped = true;
            currentEquipment.RecordEquip();

            // Track condition for change detection
            previousCondition = currentEquipment.Condition;

            // Resolve stats (includes broken penalties if applicable)
            currentStats = currentEquipment.ResolveStats(equipmentRegistry, brokenWeaponConfig);

            // Update combat system
            ApplyStatsToCombat(weaponDef);

            // Update weapon view
            UpdateWeaponView(weaponDef);

            LootboundLog.Info(Category, $"Equipped: {currentEquipment.CustomName} ({currentStats})");

            OnWeaponEquipped?.Invoke(item);
            OnEquipmentChanged?.Invoke(currentEquipment);
        }

        private void UnequipInternal()
        {
            if (currentEquipment != null)
            {
                currentEquipment.IsEquipped = false;
            }

            var previousEquipment = currentEquipment;
            equippedSlotIndex = -1;
            currentEquipment = null;
            currentStats = ResolvedWeaponStats.Default;

            // Clear weapon view
            ClearWeaponView();

            // Reset combat to defaults
            ResetCombatToDefaults();

            LootboundLog.Info(Category, "Weapon unequipped");

            OnWeaponUnequipped?.Invoke();
            OnEquipmentChanged?.Invoke(null);
        }

        private void ApplyStatsToCombat(WeaponDefinition weaponDef)
        {
            if (meleeWeapon == null) return;

            // Apply the weapon's attack config if available
            if (weaponDef.AttackConfig != null)
            {
                meleeWeapon.SetConfig(weaponDef.AttackConfig);
            }

            // Weapon will use resolved stats via our property
            meleeWeapon.SetEquipmentStats(currentStats);
        }

        private void ResetCombatToDefaults()
        {
            if (meleeWeapon != null)
            {
                meleeWeapon.SetEquipmentStats(ResolvedWeaponStats.Default);
            }
        }

        private void UpdateWeaponView(WeaponDefinition weaponDef)
        {
            ClearWeaponView();

            if (weaponViewSocket == null || weaponDef.FirstPersonPrefab == null) return;

            currentWeaponView = Instantiate(weaponDef.FirstPersonPrefab, weaponViewSocket);
            currentWeaponView.transform.localPosition = Vector3.zero;
            currentWeaponView.transform.localRotation = Quaternion.identity;
        }

        private void ClearWeaponView()
        {
            if (currentWeaponView != null)
            {
                Destroy(currentWeaponView);
                currentWeaponView = null;
            }
        }

        private void HandleSlotChanged(int slotIndex)
        {
            // If the equipped slot was changed/cleared, update equipment state
            if (slotIndex != equippedSlotIndex) return;

            var slot = playerInventory?.Inventory?.GetSlot(slotIndex);
            if (slot == null || slot.IsEmpty)
            {
                // Item was removed from equipped slot
                LootboundLog.Info(Category, "Equipped item removed from inventory");
                UnequipInternal();
            }
            else if (slot.Item?.EquipmentData?.InstanceId != currentEquipment?.InstanceId)
            {
                // Different item now in the slot
                LootboundLog.Info(Category, "Different item now in equipped slot");
                UnequipInternal();
            }
        }

        /// <summary>
        /// Initialize with a starting weapon.
        /// </summary>
        public void InitializeWithStartingWeapon(WeaponDefinition weaponDef, string customName = null)
        {
            if (weaponDef == null || playerInventory == null) return;

            var generator = new EquipmentGenerator(equipmentRegistry);
            var item = generator.CreateSimpleWeapon(weaponDef, customName, "Starting Equipment");

            if (item == null) return;

            // Add to inventory
            if (playerInventory.Inventory.TryAddItem(item))
            {
                // Find the slot it was added to
                for (int i = 0; i < playerInventory.Inventory.Capacity; i++)
                {
                    var slot = playerInventory.Inventory.GetSlot(i);
                    if (slot?.Item?.EquipmentData?.InstanceId == item.EquipmentData?.InstanceId)
                    {
                        TryEquip(i);
                        break;
                    }
                }
            }
        }
    }
}

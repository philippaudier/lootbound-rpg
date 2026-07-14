using System;
using UnityEngine;
using Lootbound.Core.Logging;
using Lootbound.Gameplay.Combat;

namespace Lootbound.Gameplay.Equipment
{
    /// <summary>
    /// Connects the weapon wear system to Unity combat events.
    /// Handles attack ID generation and event routing.
    /// </summary>
    public class PlayerWeaponWear : MonoBehaviour
    {
        private const string Category = "PlayerWeaponWear";

        [Header("Dependencies")]
        [SerializeField] private PlayerEquipment playerEquipment;
        [SerializeField] private PlayerCombatController combatController;
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private WeaponWearConfig wearConfig;

        [Header("Debug")]
        [SerializeField] private bool logWearEvents = false;

        private WeaponWearSystem wearSystem;
        private int currentAttackId;

        /// <summary>
        /// Fired when wear is applied to the equipped weapon.
        /// </summary>
        public event Action<WearResult> OnWearApplied;

        /// <summary>
        /// Fired when the equipped weapon's condition changes.
        /// </summary>
        public event Action<WearResult> OnConditionChanged;

        /// <summary>
        /// Current attack ID (for debugging).
        /// </summary>
        public int CurrentAttackId => currentAttackId;

        /// <summary>
        /// The underlying wear system (for testing).
        /// </summary>
        public WeaponWearSystem WearSystem => wearSystem;

        private void Awake()
        {
            // Auto-find dependencies
            if (playerEquipment == null)
            {
                playerEquipment = GetComponentInParent<PlayerEquipment>();
            }

            if (combatController == null)
            {
                combatController = GetComponentInParent<PlayerCombatController>();
            }

            if (playerHealth == null)
            {
                playerHealth = GetComponentInParent<PlayerHealth>();
            }

            if (wearConfig == null)
            {
                LootboundLog.Warning(Category, "WeaponWearConfig not assigned. Wear system disabled.");
                return;
            }

            InitializeWearSystem();
        }

        private void InitializeWearSystem()
        {
            wearSystem = new WeaponWearSystem(wearConfig);
            wearSystem.OnWearApplied += HandleWearApplied;
            wearSystem.OnConditionChanged += HandleConditionChanged;
        }

        private void OnEnable()
        {
            if (combatController != null)
            {
                combatController.OnAttack += HandleAttackStarted;
                combatController.OnHitTarget += HandleHitTarget;
            }

            if (playerHealth != null)
            {
                playerHealth.OnDamaged += HandlePlayerDamaged;
            }
        }

        private void OnDisable()
        {
            if (combatController != null)
            {
                combatController.OnAttack -= HandleAttackStarted;
                combatController.OnHitTarget -= HandleHitTarget;
            }

            if (playerHealth != null)
            {
                playerHealth.OnDamaged -= HandlePlayerDamaged;
            }
        }

        private void OnDestroy()
        {
            if (wearSystem != null)
            {
                wearSystem.OnWearApplied -= HandleWearApplied;
                wearSystem.OnConditionChanged -= HandleConditionChanged;
            }
        }

        private void HandleAttackStarted()
        {
            // Generate new attack ID for this swing
            currentAttackId++;
            wearSystem?.ResetForNewAttack();

            if (logWearEvents)
            {
                LootboundLog.Info(Category, $"Attack started. ID: {currentAttackId}");
            }
        }

        private void HandleHitTarget(DamageResult damageResult)
        {
            if (wearSystem == null || !playerEquipment.HasWeaponEquipped)
            {
                return;
            }

            var equipment = playerEquipment.CurrentEquipment;
            if (equipment == null || !equipment.IsValid)
            {
                return;
            }

            // Get target max HP from the damage result source if possible
            float targetMaxHp = GetTargetMaxHp(damageResult);

            var result = wearSystem.TryApplyHitWear(equipment, currentAttackId, targetMaxHp);

            if (logWearEvents && result.WearApplied)
            {
                LootboundLog.Info(Category, $"Hit wear applied: -{result.DurabilityLost} durability");
            }
        }

        private float GetTargetMaxHp(DamageResult damageResult)
        {
            // For V1, we don't have direct access to target max HP from DamageResult
            // We can estimate based on damage dealt and whether it was fatal
            // Future versions could extend DamageResult to include this
            // For now, use a heuristic: assume non-fatal hits were against targets with at least damage dealt * 2
            if (damageResult.WasFatal)
            {
                // Fatal hit - target had <= damage dealt HP remaining
                return damageResult.DamageDealt;
            }

            // Non-fatal - we don't know, assume standard enemy HP
            return 50f;
        }

        private void HandlePlayerDamaged(DamageRequest damageRequest)
        {
            if (wearSystem == null || !playerEquipment.HasWeaponEquipped)
            {
                return;
            }

            var equipment = playerEquipment.CurrentEquipment;
            if (equipment == null || !equipment.IsValid)
            {
                return;
            }

            var context = WearContext.PlayerDamaged(damageRequest.Source);
            var result = wearSystem.TryApplyWear(equipment, context);

            if (logWearEvents && result.WearApplied)
            {
                LootboundLog.Info(Category, $"Player damage wear applied: -{result.DurabilityLost} durability");
            }
        }

        private void HandleWearApplied(WearResult result)
        {
            OnWearApplied?.Invoke(result);
        }

        private void HandleConditionChanged(WearResult result)
        {
            LootboundLog.Info(Category, $"Condition changed: {result.EquipmentName} is now {result.ConditionAfter}");
            OnConditionChanged?.Invoke(result);
        }

        /// <summary>
        /// Apply debug wear to the equipped weapon.
        /// </summary>
        public void ApplyDebugWear()
        {
            if (wearSystem == null || !playerEquipment.HasWeaponEquipped)
            {
                LootboundLog.Warning(Category, "Cannot apply debug wear: no weapon equipped or system not initialized.");
                return;
            }

            var equipment = playerEquipment.CurrentEquipment;
            var result = wearSystem.ApplyDebugWear(equipment);

            if (result.WearApplied)
            {
                LootboundLog.Info(Category, $"Debug wear applied: -{result.DurabilityLost} durability. Now: {equipment.CurrentDurability}/{equipment.MaxDurability}");
            }
        }

        /// <summary>
        /// Force break the equipped weapon (debug only).
        /// Sets durability to 0 and triggers condition change.
        /// </summary>
        public void ForceBreakWeapon()
        {
            if (!playerEquipment.HasWeaponEquipped)
            {
                LootboundLog.Warning(Category, "Cannot force break: no weapon equipped.");
                return;
            }

            var equipment = playerEquipment.CurrentEquipment;
            if (equipment.Condition == EquipmentCondition.Broken)
            {
                LootboundLog.Warning(Category, "Weapon is already broken.");
                return;
            }

            EquipmentCondition conditionBefore = equipment.Condition;
            float durabilityLost = equipment.CurrentDurability;

            equipment.SetDurability(0f);

            var result = WearResult.Applied(durabilityLost, conditionBefore, EquipmentCondition.Broken, equipment.CustomName);

            LootboundLog.Warning(Category, $"Force break applied: {equipment.CustomName} is now Broken!");

            // Fire events
            OnWearApplied?.Invoke(result);
            OnConditionChanged?.Invoke(result);
        }

        /// <summary>
        /// Restore equipped weapon to full durability (debug only).
        /// </summary>
        public void RestoreWeaponDurability()
        {
            if (!playerEquipment.HasWeaponEquipped)
            {
                LootboundLog.Warning(Category, "Cannot restore: no weapon equipped.");
                return;
            }

            var equipment = playerEquipment.CurrentEquipment;
            EquipmentCondition conditionBefore = equipment.Condition;

            equipment.SetDurability(equipment.MaxDurability);

            EquipmentCondition conditionAfter = equipment.Condition;

            if (conditionBefore != conditionAfter)
            {
                var result = WearResult.Applied(0f, conditionBefore, conditionAfter, equipment.CustomName);
                OnConditionChanged?.Invoke(result);
            }

            LootboundLog.Info(Category, $"Durability restored: {equipment.CustomName} ({equipment.CurrentDurability}/{equipment.MaxDurability})");
        }
    }
}

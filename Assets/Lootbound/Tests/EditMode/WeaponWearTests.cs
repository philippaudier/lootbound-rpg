using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;
using Lootbound.Gameplay.Equipment;
using Lootbound.Gameplay.Inventory;

namespace Lootbound.Tests.EditMode
{
    /// <summary>
    /// EditMode tests for the weapon wear system.
    /// Tests wear application, attack ID tracking, and condition changes.
    /// </summary>
    public class WeaponWearTests
    {
        #region Test Helpers

        private WeaponWearConfig CreateTestConfig(
            float successfulHitChance = 0.15f,
            float successfulHitAmount = 1f,
            float heavyTargetHpThreshold = 100f,
            float heavyTargetHitChance = 0.30f,
            float heavyTargetHitAmount = 2f,
            float playerDamagedChance = 0.10f,
            float playerDamagedAmount = 0.5f)
        {
            var config = ScriptableObject.CreateInstance<WeaponWearConfig>();
            var type = typeof(WeaponWearConfig);

            type.GetField("successfulHitChance", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(config, successfulHitChance);
            type.GetField("successfulHitAmount", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(config, successfulHitAmount);
            type.GetField("heavyTargetHpThreshold", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(config, heavyTargetHpThreshold);
            type.GetField("heavyTargetHitChance", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(config, heavyTargetHitChance);
            type.GetField("heavyTargetHitAmount", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(config, heavyTargetHitAmount);
            type.GetField("playerDamagedChance", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(config, playerDamagedChance);
            type.GetField("playerDamagedAmount", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(config, playerDamagedAmount);
            type.GetField("debugAmount", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(config, 10f);

            return config;
        }

        private EquipmentData CreateTestEquipment(float durability = 100f, string name = "Test Blade")
        {
            return new EquipmentData("weapon_test", name, ItemRarity.Common, null, "TestLocation", durability);
        }

        #endregion

        #region WeaponWearConfig Tests

        [Test]
        public void WeaponWearConfig_GetChance_ReturnsCorrectValues()
        {
            var config = CreateTestConfig(
                successfulHitChance: 0.15f,
                heavyTargetHitChance: 0.30f,
                playerDamagedChance: 0.10f);

            Assert.AreEqual(0.15f, config.GetChance(WeaponWearCause.SuccessfulHit), 0.001f);
            Assert.AreEqual(0.30f, config.GetChance(WeaponWearCause.HeavyTargetHit), 0.001f);
            Assert.AreEqual(0.10f, config.GetChance(WeaponWearCause.PlayerDamagedWhileEquipped), 0.001f);
            Assert.AreEqual(1f, config.GetChance(WeaponWearCause.Debug), 0.001f);
        }

        [Test]
        public void WeaponWearConfig_GetAmount_ReturnsCorrectValues()
        {
            var config = CreateTestConfig(
                successfulHitAmount: 1f,
                heavyTargetHitAmount: 2f,
                playerDamagedAmount: 0.5f);

            Assert.AreEqual(1f, config.GetAmount(WeaponWearCause.SuccessfulHit), 0.001f);
            Assert.AreEqual(2f, config.GetAmount(WeaponWearCause.HeavyTargetHit), 0.001f);
            Assert.AreEqual(0.5f, config.GetAmount(WeaponWearCause.PlayerDamagedWhileEquipped), 0.001f);
        }

        [Test]
        public void WeaponWearConfig_IsHeavyTarget_ChecksThreshold()
        {
            var config = CreateTestConfig(heavyTargetHpThreshold: 100f);

            Assert.IsFalse(config.IsHeavyTarget(50f));
            Assert.IsFalse(config.IsHeavyTarget(99f));
            Assert.IsTrue(config.IsHeavyTarget(100f));
            Assert.IsTrue(config.IsHeavyTarget(150f));
        }

        #endregion

        #region WearContext Tests

        [Test]
        public void WearContext_SuccessfulHit_CreatesCorrectContext()
        {
            var context = WearContext.SuccessfulHit(42, 75f);

            Assert.AreEqual(WeaponWearCause.SuccessfulHit, context.Cause);
            Assert.AreEqual(42, context.AttackId);
            Assert.AreEqual(75f, context.TargetMaxHp, 0.001f);
        }

        [Test]
        public void WearContext_PlayerDamaged_CreatesCorrectContext()
        {
            var context = WearContext.PlayerDamaged();

            Assert.AreEqual(WeaponWearCause.PlayerDamagedWhileEquipped, context.Cause);
            Assert.AreEqual(0, context.AttackId);
        }

        [Test]
        public void WearContext_Debug_CreatesCorrectContext()
        {
            var context = WearContext.Debug();

            Assert.AreEqual(WeaponWearCause.Debug, context.Cause);
            Assert.AreEqual(-1, context.AttackId);
        }

        #endregion

        #region WearResult Tests

        [Test]
        public void WearResult_NoWear_ReturnsCorrectProperties()
        {
            var result = WearResult.NoWear(EquipmentCondition.Good, "Test Blade");

            Assert.IsFalse(result.WearApplied);
            Assert.AreEqual(0f, result.DurabilityLost, 0.001f);
            Assert.AreEqual(EquipmentCondition.Good, result.ConditionBefore);
            Assert.AreEqual(EquipmentCondition.Good, result.ConditionAfter);
            Assert.IsFalse(result.ConditionChanged);
            Assert.AreEqual("Test Blade", result.EquipmentName);
        }

        [Test]
        public void WearResult_Applied_ReturnsCorrectProperties()
        {
            var result = WearResult.Applied(5f, EquipmentCondition.Excellent, EquipmentCondition.Good, "Test Blade");

            Assert.IsTrue(result.WearApplied);
            Assert.AreEqual(5f, result.DurabilityLost, 0.001f);
            Assert.AreEqual(EquipmentCondition.Excellent, result.ConditionBefore);
            Assert.AreEqual(EquipmentCondition.Good, result.ConditionAfter);
            Assert.IsTrue(result.ConditionChanged);
            Assert.AreEqual("Test Blade", result.EquipmentName);
        }

        [Test]
        public void WearResult_ConditionChanged_DetectsChange()
        {
            var noChange = WearResult.Applied(1f, EquipmentCondition.Excellent, EquipmentCondition.Excellent, "Test");
            var withChange = WearResult.Applied(5f, EquipmentCondition.Good, EquipmentCondition.Worn, "Test");

            Assert.IsFalse(noChange.ConditionChanged);
            Assert.IsTrue(withChange.ConditionChanged);
        }

        [Test]
        public void WearResult_NowBroken_DetectsBroken()
        {
            var notBroken = WearResult.Applied(5f, EquipmentCondition.Fragile, EquipmentCondition.Fragile, "Test");
            var nowBroken = WearResult.Applied(5f, EquipmentCondition.Fragile, EquipmentCondition.Broken, "Test");

            Assert.IsFalse(notBroken.NowBroken);
            Assert.IsTrue(nowBroken.NowBroken);
        }

        #endregion

        #region WeaponWearSystem Tests

        [Test]
        public void WeaponWearSystem_TryApplyWear_WithNullEquipment_ReturnsNoWear()
        {
            var config = CreateTestConfig();
            var system = new WeaponWearSystem(config, seed: 12345);

            var result = system.TryApplyWear(null, WearContext.SuccessfulHit(1, 50f));

            Assert.IsFalse(result.WearApplied);
        }

        [Test]
        public void WeaponWearSystem_TryApplyWear_WithBrokenEquipment_ReturnsNoWear()
        {
            var config = CreateTestConfig();
            var system = new WeaponWearSystem(config, seed: 12345);
            var equipment = CreateTestEquipment(durability: 100f);
            equipment.SetDurability(0f); // Make it broken

            var result = system.TryApplyWear(equipment, WearContext.SuccessfulHit(1, 50f));

            Assert.IsFalse(result.WearApplied);
            Assert.AreEqual(EquipmentCondition.Broken, result.ConditionBefore);
        }

        [Test]
        public void WeaponWearSystem_TryApplyWear_DeterministicWithSeed()
        {
            var config = CreateTestConfig(successfulHitChance: 0.5f);
            var equipment1 = CreateTestEquipment();
            var equipment2 = CreateTestEquipment();

            var system1 = new WeaponWearSystem(config, seed: 12345);
            var system2 = new WeaponWearSystem(config, seed: 12345);

            var result1 = system1.TryApplyWear(equipment1, WearContext.SuccessfulHit(1, 50f));
            var result2 = system2.TryApplyWear(equipment2, WearContext.SuccessfulHit(1, 50f));

            Assert.AreEqual(result1.WearApplied, result2.WearApplied);
        }

        [Test]
        public void WeaponWearSystem_TryApplyWear_WithZeroChance_NeverApplies()
        {
            var config = CreateTestConfig(successfulHitChance: 0f);
            var system = new WeaponWearSystem(config, seed: 12345);
            var equipment = CreateTestEquipment();

            // Try many times
            for (int i = 0; i < 100; i++)
            {
                system.ResetForNewAttack();
                var result = system.TryApplyWear(equipment, WearContext.SuccessfulHit(i + 1, 50f));
                Assert.IsFalse(result.WearApplied, $"Wear should never apply with 0% chance (attempt {i})");
            }
        }

        [Test]
        public void WeaponWearSystem_TryApplyWear_With100PercentChance_AlwaysApplies()
        {
            var config = CreateTestConfig(successfulHitChance: 1f);
            var system = new WeaponWearSystem(config, seed: 12345);
            var equipment = CreateTestEquipment(durability: 1000f); // Large durability to avoid breaking

            for (int i = 0; i < 10; i++)
            {
                system.ResetForNewAttack();
                var result = system.TryApplyWear(equipment, WearContext.SuccessfulHit(i + 1, 50f));
                Assert.IsTrue(result.WearApplied, $"Wear should always apply with 100% chance (attempt {i})");
            }
        }

        [Test]
        public void WeaponWearSystem_AttackIdTracking_PreventsDuplicateWear()
        {
            var config = CreateTestConfig(successfulHitChance: 1f);
            var system = new WeaponWearSystem(config, seed: 12345);
            var equipment = CreateTestEquipment();

            // First hit with attack ID 1
            var result1 = system.TryApplyWear(equipment, WearContext.SuccessfulHit(1, 50f));
            Assert.IsTrue(result1.WearApplied);

            // Second hit with same attack ID should be skipped
            var result2 = system.TryApplyWear(equipment, WearContext.SuccessfulHit(1, 50f));
            Assert.IsFalse(result2.WearApplied);

            // Third hit with different attack ID should apply
            system.ResetForNewAttack();
            var result3 = system.TryApplyWear(equipment, WearContext.SuccessfulHit(2, 50f));
            Assert.IsTrue(result3.WearApplied);
        }

        [Test]
        public void WeaponWearSystem_ResetForNewAttack_ClearsTracking()
        {
            var config = CreateTestConfig(successfulHitChance: 1f);
            var system = new WeaponWearSystem(config, seed: 12345);
            var equipment = CreateTestEquipment(durability: 1000f);

            // First attack
            system.TryApplyWear(equipment, WearContext.SuccessfulHit(1, 50f));
            Assert.AreEqual(1, system.ProcessedAttackCount);

            // Reset and new attack
            system.ResetForNewAttack();
            Assert.AreEqual(0, system.ProcessedAttackCount);

            // Same ID should work again after reset
            var result = system.TryApplyWear(equipment, WearContext.SuccessfulHit(1, 50f));
            Assert.IsTrue(result.WearApplied);
        }

        [Test]
        public void WeaponWearSystem_ApplyDebugWear_AlwaysApplies()
        {
            var config = CreateTestConfig();
            var system = new WeaponWearSystem(config, seed: 12345);
            var equipment = CreateTestEquipment();

            var result = system.ApplyDebugWear(equipment);

            Assert.IsTrue(result.WearApplied);
            Assert.AreEqual(10f, result.DurabilityLost, 0.001f);
        }

        [Test]
        public void WeaponWearSystem_ApplyDebugWear_DoesNotApplyToBroken()
        {
            var config = CreateTestConfig();
            var system = new WeaponWearSystem(config, seed: 12345);
            var equipment = CreateTestEquipment();
            equipment.SetDurability(0f);

            var result = system.ApplyDebugWear(equipment);

            Assert.IsFalse(result.WearApplied);
        }

        [Test]
        public void WeaponWearSystem_TryApplyHitWear_AppliesBothNormalAndHeavyWear()
        {
            // 100% chance for both
            var config = CreateTestConfig(
                successfulHitChance: 1f,
                successfulHitAmount: 1f,
                heavyTargetHpThreshold: 100f,
                heavyTargetHitChance: 1f,
                heavyTargetHitAmount: 2f);

            var system = new WeaponWearSystem(config, seed: 12345);
            var equipment = CreateTestEquipment();
            float initialDurability = equipment.CurrentDurability;

            // Hit a heavy target (150 HP > 100 threshold)
            var result = system.TryApplyHitWear(equipment, 1, 150f);

            Assert.IsTrue(result.WearApplied);
            // Should have lost both normal (1) and heavy (2) = 3 total
            Assert.AreEqual(3f, result.DurabilityLost, 0.001f);
            Assert.AreEqual(initialDurability - 3f, equipment.CurrentDurability, 0.001f);
        }

        [Test]
        public void WeaponWearSystem_TryApplyHitWear_OnlyNormalWearForLightTarget()
        {
            var config = CreateTestConfig(
                successfulHitChance: 1f,
                successfulHitAmount: 1f,
                heavyTargetHpThreshold: 100f,
                heavyTargetHitChance: 1f,
                heavyTargetHitAmount: 2f);

            var system = new WeaponWearSystem(config, seed: 12345);
            var equipment = CreateTestEquipment();

            // Hit a light target (50 HP < 100 threshold)
            var result = system.TryApplyHitWear(equipment, 1, 50f);

            Assert.IsTrue(result.WearApplied);
            Assert.AreEqual(1f, result.DurabilityLost, 0.001f);
        }

        [Test]
        public void WeaponWearSystem_OnWearApplied_FiresEvent()
        {
            var config = CreateTestConfig(successfulHitChance: 1f);
            var system = new WeaponWearSystem(config, seed: 12345);
            var equipment = CreateTestEquipment();

            WearResult? eventResult = null;
            system.OnWearApplied += result => eventResult = result;

            system.TryApplyWear(equipment, WearContext.SuccessfulHit(1, 50f));

            Assert.IsNotNull(eventResult);
            Assert.IsTrue(eventResult.Value.WearApplied);
        }

        [Test]
        public void WeaponWearSystem_OnConditionChanged_FiresOnlyWhenConditionChanges()
        {
            var config = CreateTestConfig(successfulHitChance: 1f, successfulHitAmount: 25f);
            var system = new WeaponWearSystem(config, seed: 12345);
            var equipment = CreateTestEquipment(durability: 100f);

            int conditionChangedCount = 0;
            system.OnConditionChanged += _ => conditionChangedCount++;

            // First wear: 100 -> 75 = Excellent -> Good
            system.ResetForNewAttack();
            system.TryApplyWear(equipment, WearContext.SuccessfulHit(1, 50f));
            Assert.AreEqual(1, conditionChangedCount, "Should fire for Excellent -> Good");

            // Second wear: 75 -> 50 = Good -> Worn
            system.ResetForNewAttack();
            system.TryApplyWear(equipment, WearContext.SuccessfulHit(2, 50f));
            Assert.AreEqual(2, conditionChangedCount, "Should fire for Good -> Worn");

            // Third wear: 50 -> 25 = Worn -> Fragile
            system.ResetForNewAttack();
            system.TryApplyWear(equipment, WearContext.SuccessfulHit(3, 50f));
            Assert.AreEqual(3, conditionChangedCount, "Should fire for Worn -> Fragile");
        }

        [Test]
        public void WeaponWearSystem_ConditionTransition_ExcellentToGood()
        {
            var config = CreateTestConfig(successfulHitChance: 1f, successfulHitAmount: 25f);
            var system = new WeaponWearSystem(config, seed: 12345);
            var equipment = CreateTestEquipment(durability: 100f);

            // Start at 100% = Excellent
            Assert.AreEqual(EquipmentCondition.Excellent, equipment.Condition);

            // Apply wear: 100 - 25 = 75% = Good
            var result = system.TryApplyWear(equipment, WearContext.SuccessfulHit(1, 50f));

            Assert.IsTrue(result.ConditionChanged);
            Assert.AreEqual(EquipmentCondition.Excellent, result.ConditionBefore);
            Assert.AreEqual(EquipmentCondition.Good, result.ConditionAfter);
        }

        [Test]
        public void WeaponWearSystem_ConditionTransition_FragileToBroken()
        {
            var config = CreateTestConfig(successfulHitChance: 1f, successfulHitAmount: 20f);
            var system = new WeaponWearSystem(config, seed: 12345);
            var equipment = CreateTestEquipment(durability: 100f);
            equipment.SetDurability(15f); // 15% = Fragile

            Assert.AreEqual(EquipmentCondition.Fragile, equipment.Condition);

            var result = system.TryApplyWear(equipment, WearContext.SuccessfulHit(1, 50f));

            Assert.IsTrue(result.ConditionChanged);
            Assert.AreEqual(EquipmentCondition.Fragile, result.ConditionBefore);
            Assert.AreEqual(EquipmentCondition.Broken, result.ConditionAfter);
            Assert.IsTrue(result.NowBroken);
        }

        [Test]
        public void WeaponWearSystem_PlayerDamaged_AppliesWear()
        {
            var config = CreateTestConfig(playerDamagedChance: 1f, playerDamagedAmount: 5f);
            var system = new WeaponWearSystem(config, seed: 12345);
            var equipment = CreateTestEquipment();
            float initialDurability = equipment.CurrentDurability;

            var result = system.TryApplyWear(equipment, WearContext.PlayerDamaged());

            Assert.IsTrue(result.WearApplied);
            Assert.AreEqual(5f, result.DurabilityLost, 0.001f);
            Assert.AreEqual(initialDurability - 5f, equipment.CurrentDurability, 0.001f);
        }

        [Test]
        public void WeaponWearSystem_PlayerDamaged_NotAffectedByAttackIdTracking()
        {
            var config = CreateTestConfig(playerDamagedChance: 1f, playerDamagedAmount: 1f);
            var system = new WeaponWearSystem(config, seed: 12345);
            var equipment = CreateTestEquipment(durability: 100f);

            // Multiple player damage events should all apply (no attack ID tracking)
            system.TryApplyWear(equipment, WearContext.PlayerDamaged());
            system.TryApplyWear(equipment, WearContext.PlayerDamaged());
            system.TryApplyWear(equipment, WearContext.PlayerDamaged());

            Assert.AreEqual(97f, equipment.CurrentDurability, 0.001f);
        }

        #endregion

        #region Integration Tests

        [Test]
        public void WeaponWearSystem_FullCombatScenario()
        {
            // Realistic combat scenario
            var config = CreateTestConfig(
                successfulHitChance: 0.50f,
                successfulHitAmount: 1f,
                heavyTargetHpThreshold: 100f,
                heavyTargetHitChance: 0.25f,
                heavyTargetHitAmount: 2f,
                playerDamagedChance: 0.20f,
                playerDamagedAmount: 0.5f);

            var system = new WeaponWearSystem(config, seed: 42);
            var equipment = CreateTestEquipment(durability: 100f);

            int conditionChanges = 0;
            system.OnConditionChanged += _ => conditionChanges++;

            // Simulate combat
            for (int attack = 1; attack <= 50; attack++)
            {
                system.ResetForNewAttack();

                // Hit some enemies per attack
                int hitsThisAttack = Random.Range(1, 4);
                for (int hit = 0; hit < hitsThisAttack; hit++)
                {
                    float targetHp = Random.Range(30f, 150f);
                    system.TryApplyHitWear(equipment, attack, targetHp);
                }

                // Sometimes take damage
                if (Random.value < 0.3f)
                {
                    system.TryApplyWear(equipment, WearContext.PlayerDamaged());
                }
            }

            // Equipment should have lost some durability
            Assert.Less(equipment.CurrentDurability, 100f, "Equipment should have worn down");

            // Log final state for debugging
            Debug.Log($"Final durability: {equipment.CurrentDurability}/{equipment.MaxDurability} ({equipment.Condition})");
            Debug.Log($"Condition changes: {conditionChanges}");
        }

        #endregion
    }
}

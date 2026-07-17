using NUnit.Framework;
using UnityEngine;
using Lootbound.Gameplay.Equipment;
using Lootbound.Gameplay.Inventory;

namespace Lootbound.Tests.EditMode
{
    /// <summary>
    /// EditMode tests for the repair system.
    /// Tests repair preview, repair execution, and edge cases.
    /// </summary>
    public class RepairTests
    {
        #region Test Helpers

        private RepairConfig CreateTestConfig(
            float durabilityPerFragment = 20f,
            bool canRepairBroken = true,
            float maxRepairPercentage = 1f)
        {
            var config = ScriptableObject.CreateInstance<RepairConfig>();
            var type = typeof(RepairConfig);

            type.GetField("durabilityPerFragment", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(config, durabilityPerFragment);
            type.GetField("canRepairBroken", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(config, canRepairBroken);
            type.GetField("maxRepairPercentage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(config, maxRepairPercentage);

            return config;
        }

        private EquipmentData CreateTestEquipment(float currentDurability = 100f, float maxDurability = 100f, string name = "Test Blade")
        {
            var equipment = new EquipmentData("weapon_test", name, ItemRarity.Common, null, "TestLocation", maxDurability);
            equipment.SetDurability(currentDurability);
            return equipment;
        }

        private ItemDefinition CreateTestFragmentDefinition()
        {
            var definition = ScriptableObject.CreateInstance<ItemDefinition>();
            var type = typeof(ItemDefinition);

            type.GetField("itemId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(definition, "item_repair_fragment");
            type.GetField("displayName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(definition, "Repair Fragment");
            type.GetField("isStackable", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(definition, true);
            type.GetField("maxStackSize", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(definition, 20);

            return definition;
        }

        private Inventory CreateTestInventory(int capacity = 10)
        {
            return new Inventory(capacity);
        }

        private void AddFragmentsToInventory(Inventory inventory, ItemDefinition fragmentDef, int count)
        {
            inventory.TryAddItem(new ItemInstance(fragmentDef, count));
        }

        #endregion

        #region RepairConfig Tests

        [Test]
        public void RepairConfig_CalculateFragmentsForFullRepair_ReturnsCorrectValue()
        {
            var config = CreateTestConfig(durabilityPerFragment: 20f);

            // 50 durability missing, 20 per fragment = 3 fragments (ceil of 2.5)
            int fragments = config.CalculateFragmentsForFullRepair(50f, 100f);
            Assert.AreEqual(3, fragments);
        }

        [Test]
        public void RepairConfig_CalculateFragmentsForFullRepair_ReturnsZeroForFullDurability()
        {
            var config = CreateTestConfig(durabilityPerFragment: 20f);

            int fragments = config.CalculateFragmentsForFullRepair(100f, 100f);
            Assert.AreEqual(0, fragments);
        }

        [Test]
        public void RepairConfig_CalculateFragmentsForFullRepair_RespectsMaxPercentage()
        {
            var config = CreateTestConfig(durabilityPerFragment: 20f, maxRepairPercentage: 0.8f);

            // With 0% durability and max 80%, need 80 durability = 4 fragments
            int fragments = config.CalculateFragmentsForFullRepair(0f, 100f);
            Assert.AreEqual(4, fragments);
        }

        [Test]
        public void RepairConfig_CalculateDurabilityRestored_ReturnsCorrectValue()
        {
            var config = CreateTestConfig(durabilityPerFragment: 20f);

            float restored = config.CalculateDurabilityRestored(3);
            Assert.AreEqual(60f, restored, 0.001f);
        }

        [Test]
        public void RepairConfig_CalculateDurabilityRestored_ReturnsZeroForZeroFragments()
        {
            var config = CreateTestConfig(durabilityPerFragment: 20f);

            float restored = config.CalculateDurabilityRestored(0);
            Assert.AreEqual(0f, restored, 0.001f);
        }

        #endregion

        #region RepairPreview Tests

        [Test]
        public void RepairPreview_SuccessfulPreview_HasCorrectProperties()
        {
            var preview = new RepairPreview(
                currentDurability: 50f,
                maxDurability: 100f,
                durabilityAfterRepair: 100f,
                conditionBefore: EquipmentCondition.Worn,
                conditionAfter: EquipmentCondition.Excellent,
                fragmentsAvailable: 10,
                fragmentsForFullRepair: 3,
                fragmentsToConsume: 3);

            Assert.IsTrue(preview.CanRepair);
            Assert.AreEqual(RepairFailureReason.None, preview.FailureReason);
            Assert.AreEqual(50f, preview.CurrentDurability, 0.001f);
            Assert.AreEqual(100f, preview.DurabilityAfterRepair, 0.001f);
            Assert.AreEqual(3, preview.FragmentsToConsume);
            Assert.IsTrue(preview.WillChangeCondition);
            Assert.IsTrue(preview.IsFullRepair);
            Assert.AreEqual(50f, preview.DurabilityRestored, 0.001f);
        }

        [Test]
        public void RepairPreview_FailedPreview_HasCorrectProperties()
        {
            var preview = new RepairPreview(RepairFailureReason.NoFragmentsAvailable, 50f, 100f, 0);

            Assert.IsFalse(preview.CanRepair);
            Assert.AreEqual(RepairFailureReason.NoFragmentsAvailable, preview.FailureReason);
            Assert.AreEqual(0, preview.FragmentsToConsume);
            Assert.IsFalse(preview.WillChangeCondition);
        }

        [Test]
        public void RepairPreview_WillChangeCondition_DetectsChange()
        {
            var changePreview = new RepairPreview(
                50f, 100f, 80f,
                EquipmentCondition.Worn, EquipmentCondition.Good,
                5, 3, 2);
            Assert.IsTrue(changePreview.WillChangeCondition);

            var noChangePreview = new RepairPreview(
                70f, 100f, 75f,
                EquipmentCondition.Good, EquipmentCondition.Good,
                5, 2, 1);
            Assert.IsFalse(noChangePreview.WillChangeCondition);
        }

        #endregion

        #region RepairResult Tests

        [Test]
        public void RepairResult_SuccessfulResult_HasCorrectProperties()
        {
            var result = new RepairResult(
                equipmentName: "Test Blade",
                durabilityBefore: 50f,
                durabilityAfter: 100f,
                maxDurability: 100f,
                conditionBefore: EquipmentCondition.Worn,
                conditionAfter: EquipmentCondition.Excellent,
                fragmentsConsumed: 3);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(RepairFailureReason.None, result.FailureReason);
            Assert.AreEqual("Test Blade", result.EquipmentName);
            Assert.AreEqual(50f, result.DurabilityRestored, 0.001f);
            Assert.AreEqual(3, result.FragmentsConsumed);
            Assert.IsTrue(result.ConditionChanged);
        }

        [Test]
        public void RepairResult_FailedResult_HasCorrectProperties()
        {
            var result = new RepairResult(RepairFailureReason.NoFragmentsAvailable, "Test Blade");

            Assert.IsFalse(result.Success);
            Assert.AreEqual(RepairFailureReason.NoFragmentsAvailable, result.FailureReason);
            Assert.AreEqual(0, result.FragmentsConsumed);
        }

        [Test]
        public void RepairResult_RestoredFromBroken_DetectsBrokenRepair()
        {
            var fromBroken = new RepairResult(
                "Test Blade", 0f, 20f, 100f,
                EquipmentCondition.Broken, EquipmentCondition.Fragile, 1);
            Assert.IsTrue(fromBroken.RestoredFromBroken);

            var notFromBroken = new RepairResult(
                "Test Blade", 30f, 50f, 100f,
                EquipmentCondition.Worn, EquipmentCondition.Worn, 1);
            Assert.IsFalse(notFromBroken.RestoredFromBroken);
        }

        #endregion

        #region RepairService Tests

        [Test]
        public void RepairService_PreviewRepair_WithNullEquipment_ReturnsFailure()
        {
            var config = CreateTestConfig();
            var fragmentDef = CreateTestFragmentDefinition();
            var inventory = CreateTestInventory();
            var service = new RepairService(config, fragmentDef, inventory);

            var preview = service.PreviewRepair(null);

            Assert.IsFalse(preview.CanRepair);
            Assert.AreEqual(RepairFailureReason.NoEquipmentSelected, preview.FailureReason);
        }

        [Test]
        public void RepairService_PreviewRepair_WithFullDurability_ReturnsAlreadyFull()
        {
            var config = CreateTestConfig();
            var fragmentDef = CreateTestFragmentDefinition();
            var inventory = CreateTestInventory();
            AddFragmentsToInventory(inventory, fragmentDef,10);
            var service = new RepairService(config, fragmentDef, inventory);

            var equipment = CreateTestEquipment(currentDurability: 100f, maxDurability: 100f);
            var preview = service.PreviewRepair(equipment);

            Assert.IsFalse(preview.CanRepair);
            Assert.AreEqual(RepairFailureReason.AlreadyFullDurability, preview.FailureReason);
        }

        [Test]
        public void RepairService_PreviewRepair_WithNoFragments_ReturnsNoFragments()
        {
            var config = CreateTestConfig();
            var fragmentDef = CreateTestFragmentDefinition();
            var inventory = CreateTestInventory();
            var service = new RepairService(config, fragmentDef, inventory);

            var equipment = CreateTestEquipment(currentDurability: 50f, maxDurability: 100f);
            var preview = service.PreviewRepair(equipment);

            Assert.IsFalse(preview.CanRepair);
            Assert.AreEqual(RepairFailureReason.NoFragmentsAvailable, preview.FailureReason);
        }

        [Test]
        public void RepairService_PreviewRepair_WithBrokenAndNotAllowed_ReturnsNotAllowed()
        {
            var config = CreateTestConfig(canRepairBroken: false);
            var fragmentDef = CreateTestFragmentDefinition();
            var inventory = CreateTestInventory();
            AddFragmentsToInventory(inventory, fragmentDef,10);
            var service = new RepairService(config, fragmentDef, inventory);

            var equipment = CreateTestEquipment(currentDurability: 0f, maxDurability: 100f);
            var preview = service.PreviewRepair(equipment);

            Assert.IsFalse(preview.CanRepair);
            Assert.AreEqual(RepairFailureReason.BrokenRepairNotAllowed, preview.FailureReason);
        }

        [Test]
        public void RepairService_PreviewRepair_WithValidInputs_ReturnsCorrectPreview()
        {
            var config = CreateTestConfig(durabilityPerFragment: 20f);
            var fragmentDef = CreateTestFragmentDefinition();
            var inventory = CreateTestInventory();
            AddFragmentsToInventory(inventory, fragmentDef,10);
            var service = new RepairService(config, fragmentDef, inventory);

            var equipment = CreateTestEquipment(currentDurability: 40f, maxDurability: 100f);
            var preview = service.PreviewRepair(equipment);

            Assert.IsTrue(preview.CanRepair);
            Assert.AreEqual(10, preview.FragmentsAvailable);
            Assert.AreEqual(3, preview.FragmentsForFullRepair); // 60 durability / 20 per frag = 3
            Assert.AreEqual(3, preview.FragmentsToConsume);
            Assert.AreEqual(100f, preview.DurabilityAfterRepair, 0.001f);
        }

        [Test]
        public void RepairService_PreviewRepair_WithPartialFragments_ReturnsPartialRepair()
        {
            var config = CreateTestConfig(durabilityPerFragment: 20f);
            var fragmentDef = CreateTestFragmentDefinition();
            var inventory = CreateTestInventory();
            AddFragmentsToInventory(inventory, fragmentDef,2); // Only 2 fragments, need 5 for full
            var service = new RepairService(config, fragmentDef, inventory);

            var equipment = CreateTestEquipment(currentDurability: 0f, maxDurability: 100f);
            var preview = service.PreviewRepair(equipment);

            Assert.IsTrue(preview.CanRepair);
            Assert.AreEqual(2, preview.FragmentsAvailable);
            Assert.AreEqual(5, preview.FragmentsForFullRepair);
            Assert.AreEqual(2, preview.FragmentsToConsume);
            Assert.AreEqual(40f, preview.DurabilityAfterRepair, 0.001f);
            Assert.IsFalse(preview.IsFullRepair);
        }

        [Test]
        public void RepairService_PreviewRepair_WithSpecificFragmentCount_UsesSpecifiedCount()
        {
            var config = CreateTestConfig(durabilityPerFragment: 20f);
            var fragmentDef = CreateTestFragmentDefinition();
            var inventory = CreateTestInventory();
            AddFragmentsToInventory(inventory, fragmentDef,10);
            var service = new RepairService(config, fragmentDef, inventory);

            var equipment = CreateTestEquipment(currentDurability: 0f, maxDurability: 100f);
            var preview = service.PreviewRepair(equipment, fragmentCount: 2);

            Assert.IsTrue(preview.CanRepair);
            Assert.AreEqual(2, preview.FragmentsToConsume);
            Assert.AreEqual(40f, preview.DurabilityAfterRepair, 0.001f);
        }

        [Test]
        public void RepairService_ExecuteRepair_RestoresDurabilityAndConsumesFragments()
        {
            var config = CreateTestConfig(durabilityPerFragment: 20f);
            var fragmentDef = CreateTestFragmentDefinition();
            var inventory = CreateTestInventory();
            AddFragmentsToInventory(inventory, fragmentDef,10);
            var service = new RepairService(config, fragmentDef, inventory);

            var equipment = CreateTestEquipment(currentDurability: 40f, maxDurability: 100f);
            var result = service.ExecuteFullRepair(equipment);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(3, result.FragmentsConsumed);
            Assert.AreEqual(100f, equipment.CurrentDurability, 0.001f);
            Assert.AreEqual(7, inventory.GetItemCount(fragmentDef)); // 10 - 3 = 7
        }

        [Test]
        public void RepairService_ExecuteRepair_WithPartialRepair_ConsumesCorrectFragments()
        {
            var config = CreateTestConfig(durabilityPerFragment: 20f);
            var fragmentDef = CreateTestFragmentDefinition();
            var inventory = CreateTestInventory();
            AddFragmentsToInventory(inventory, fragmentDef,10);
            var service = new RepairService(config, fragmentDef, inventory);

            var equipment = CreateTestEquipment(currentDurability: 0f, maxDurability: 100f);
            var result = service.ExecutePartialRepair(equipment, 2);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(2, result.FragmentsConsumed);
            Assert.AreEqual(40f, equipment.CurrentDurability, 0.001f);
            Assert.AreEqual(8, inventory.GetItemCount(fragmentDef));
        }

        [Test]
        public void RepairService_ExecuteRepair_UpdatesCondition()
        {
            var config = CreateTestConfig(durabilityPerFragment: 50f);
            var fragmentDef = CreateTestFragmentDefinition();
            var inventory = CreateTestInventory();
            AddFragmentsToInventory(inventory, fragmentDef,10);
            var service = new RepairService(config, fragmentDef, inventory);

            // 40% durability = Worn condition (Worn is 35-59%)
            var equipment = CreateTestEquipment(currentDurability: 40f, maxDurability: 100f);
            Assert.AreEqual(EquipmentCondition.Worn, equipment.Condition);

            var result = service.ExecuteFullRepair(equipment);

            Assert.IsTrue(result.ConditionChanged);
            Assert.AreEqual(EquipmentCondition.Worn, result.ConditionBefore);
            Assert.AreEqual(EquipmentCondition.Excellent, result.ConditionAfter);
        }

        [Test]
        public void RepairService_ExecuteRepair_RepairsBrokenEquipment()
        {
            var config = CreateTestConfig(durabilityPerFragment: 20f, canRepairBroken: true);
            var fragmentDef = CreateTestFragmentDefinition();
            var inventory = CreateTestInventory();
            AddFragmentsToInventory(inventory, fragmentDef,1);
            var service = new RepairService(config, fragmentDef, inventory);

            var equipment = CreateTestEquipment(currentDurability: 0f, maxDurability: 100f);
            Assert.AreEqual(EquipmentCondition.Broken, equipment.Condition);

            var result = service.ExecuteFullRepair(equipment);

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.RestoredFromBroken);
            Assert.AreEqual(20f, equipment.CurrentDurability, 0.001f);
            Assert.AreNotEqual(EquipmentCondition.Broken, equipment.Condition);
        }

        [Test]
        public void RepairService_ExecuteRepair_FiresEvent()
        {
            var config = CreateTestConfig(durabilityPerFragment: 20f);
            var fragmentDef = CreateTestFragmentDefinition();
            var inventory = CreateTestInventory();
            AddFragmentsToInventory(inventory, fragmentDef,10);
            var service = new RepairService(config, fragmentDef, inventory);

            RepairResult? eventResult = null;
            service.OnRepairCompleted += result => eventResult = result;

            var equipment = CreateTestEquipment(currentDurability: 50f, maxDurability: 100f);
            service.ExecuteFullRepair(equipment);

            Assert.IsNotNull(eventResult);
            Assert.IsTrue(eventResult.Value.Success);
        }

        [Test]
        public void RepairService_CanRepair_ReturnsTrueWhenPossible()
        {
            var config = CreateTestConfig();
            var fragmentDef = CreateTestFragmentDefinition();
            var inventory = CreateTestInventory();
            AddFragmentsToInventory(inventory, fragmentDef,5);
            var service = new RepairService(config, fragmentDef, inventory);

            var equipment = CreateTestEquipment(currentDurability: 50f, maxDurability: 100f);

            Assert.IsTrue(service.CanRepair(equipment));
        }

        [Test]
        public void RepairService_CanRepair_ReturnsFalseWhenNotPossible()
        {
            var config = CreateTestConfig();
            var fragmentDef = CreateTestFragmentDefinition();
            var inventory = CreateTestInventory();
            // No fragments in inventory
            var service = new RepairService(config, fragmentDef, inventory);

            var equipment = CreateTestEquipment(currentDurability: 50f, maxDurability: 100f);

            Assert.IsFalse(service.CanRepair(equipment));
        }

        [Test]
        public void RepairService_NeedsRepair_ReturnsTrueWhenDamaged()
        {
            var config = CreateTestConfig();
            var fragmentDef = CreateTestFragmentDefinition();
            var inventory = CreateTestInventory();
            var service = new RepairService(config, fragmentDef, inventory);

            var damaged = CreateTestEquipment(currentDurability: 50f, maxDurability: 100f);
            var full = CreateTestEquipment(currentDurability: 100f, maxDurability: 100f);

            Assert.IsTrue(service.NeedsRepair(damaged));
            Assert.IsFalse(service.NeedsRepair(full));
        }

        [Test]
        public void RepairService_GetAvailableFragments_ReturnsCorrectCount()
        {
            var config = CreateTestConfig();
            var fragmentDef = CreateTestFragmentDefinition();
            var inventory = CreateTestInventory();
            AddFragmentsToInventory(inventory, fragmentDef,15);
            var service = new RepairService(config, fragmentDef, inventory);

            Assert.AreEqual(15, service.GetAvailableFragments());
        }

        #endregion

        #region Repair History Tests

        [Test]
        public void EquipmentHistory_NewEquipment_HasNoRepairHistory()
        {
            var equipment = CreateTestEquipment();
            var history = equipment.History;

            Assert.AreEqual(0, history.RepairCount);
            Assert.AreEqual(0, history.RepairsFromBroken);
            Assert.AreEqual(0, history.TotalDurabilityRestored);
            Assert.AreEqual(0, history.TotalFragmentsSpent);
            Assert.IsFalse(history.HasBeenRepaired);
            Assert.AreEqual(0, history.LastRepairTimestamp);
            Assert.IsNull(history.LastRepairLocation);
        }

        [Test]
        public void EquipmentHistory_RecordRepair_IncrementsRepairCount()
        {
            var equipment = CreateTestEquipment(currentDurability: 50f, maxDurability: 100f);
            var result = new RepairResult(
                "Test Blade", 50f, 100f, 100f,
                EquipmentCondition.Worn, EquipmentCondition.Excellent, 3);

            equipment.RecordRepair(result);

            Assert.AreEqual(1, equipment.History.RepairCount);
            Assert.IsTrue(equipment.History.HasBeenRepaired);
        }

        [Test]
        public void EquipmentHistory_RecordRepair_TracksTotalDurabilityRestored()
        {
            var equipment = CreateTestEquipment(currentDurability: 50f, maxDurability: 100f);
            var result = new RepairResult(
                "Test Blade", 50f, 100f, 100f,
                EquipmentCondition.Worn, EquipmentCondition.Excellent, 3);

            equipment.RecordRepair(result);

            Assert.AreEqual(50, equipment.History.TotalDurabilityRestored);
        }

        [Test]
        public void EquipmentHistory_RecordRepair_TracksTotalFragmentsSpent()
        {
            var equipment = CreateTestEquipment(currentDurability: 50f, maxDurability: 100f);
            var result = new RepairResult(
                "Test Blade", 50f, 100f, 100f,
                EquipmentCondition.Worn, EquipmentCondition.Excellent, 3);

            equipment.RecordRepair(result);

            Assert.AreEqual(3, equipment.History.TotalFragmentsSpent);
        }

        [Test]
        public void EquipmentHistory_RecordRepair_TracksRepairsFromBroken()
        {
            var equipment = CreateTestEquipment(currentDurability: 0f, maxDurability: 100f);
            var result = new RepairResult(
                "Test Blade", 0f, 40f, 100f,
                EquipmentCondition.Broken, EquipmentCondition.Worn, 2);

            equipment.RecordRepair(result);

            Assert.AreEqual(1, equipment.History.RepairsFromBroken);
        }

        [Test]
        public void EquipmentHistory_RecordRepair_DoesNotIncrementRepairsFromBroken_WhenNotBroken()
        {
            var equipment = CreateTestEquipment(currentDurability: 50f, maxDurability: 100f);
            var result = new RepairResult(
                "Test Blade", 50f, 100f, 100f,
                EquipmentCondition.Worn, EquipmentCondition.Excellent, 3);

            equipment.RecordRepair(result);

            Assert.AreEqual(0, equipment.History.RepairsFromBroken);
        }

        [Test]
        public void EquipmentHistory_RecordRepair_StoresLastRepairDetails()
        {
            var equipment = CreateTestEquipment(currentDurability: 50f, maxDurability: 100f);
            var result = new RepairResult(
                "Test Blade", 50f, 100f, 100f,
                EquipmentCondition.Worn, EquipmentCondition.Excellent, 3);

            equipment.RecordRepair(result, "Test Workshop");

            Assert.AreEqual("Test Workshop", equipment.History.LastRepairLocation);
            Assert.AreEqual(EquipmentCondition.Worn, equipment.History.LastRepairConditionBefore);
            Assert.AreEqual(EquipmentCondition.Excellent, equipment.History.LastRepairConditionAfter);
            Assert.Greater(equipment.History.LastRepairTimestamp, 0);
        }

        [Test]
        public void EquipmentHistory_RecordRepair_AccumulatesMultipleRepairs()
        {
            var equipment = CreateTestEquipment(currentDurability: 0f, maxDurability: 100f);

            // First repair from broken
            var result1 = new RepairResult(
                "Test Blade", 0f, 40f, 100f,
                EquipmentCondition.Broken, EquipmentCondition.Worn, 2);
            equipment.RecordRepair(result1);

            // Second repair (not from broken)
            var result2 = new RepairResult(
                "Test Blade", 40f, 80f, 100f,
                EquipmentCondition.Worn, EquipmentCondition.Good, 2);
            equipment.RecordRepair(result2);

            // Third repair from worn (simulate another damage-repair cycle with broken start)
            var result3 = new RepairResult(
                "Test Blade", 0f, 60f, 100f,
                EquipmentCondition.Broken, EquipmentCondition.Good, 3);
            equipment.RecordRepair(result3);

            Assert.AreEqual(3, equipment.History.RepairCount);
            Assert.AreEqual(2, equipment.History.RepairsFromBroken); // result1 and result3
            Assert.AreEqual(40 + 40 + 60, equipment.History.TotalDurabilityRestored); // 140
            Assert.AreEqual(2 + 2 + 3, equipment.History.TotalFragmentsSpent); // 7
        }

        [Test]
        public void EquipmentHistory_RecordRepair_IgnoresFailedRepairs()
        {
            var equipment = CreateTestEquipment(currentDurability: 50f, maxDurability: 100f);
            var failedResult = new RepairResult(RepairFailureReason.NoFragmentsAvailable, "Test Blade");

            equipment.RecordRepair(failedResult);

            Assert.AreEqual(0, equipment.History.RepairCount);
            Assert.IsFalse(equipment.History.HasBeenRepaired);
        }

        [Test]
        public void EquipmentHistory_Clone_PreservesRepairHistory()
        {
            var equipment = CreateTestEquipment(currentDurability: 50f, maxDurability: 100f);
            var result = new RepairResult(
                "Test Blade", 0f, 50f, 100f,
                EquipmentCondition.Broken, EquipmentCondition.Worn, 3);
            equipment.RecordRepair(result, "Workshop");

            var clonedHistory = equipment.History.Clone();

            Assert.AreEqual(1, clonedHistory.RepairCount);
            Assert.AreEqual(1, clonedHistory.RepairsFromBroken);
            Assert.AreEqual(50, clonedHistory.TotalDurabilityRestored);
            Assert.AreEqual(3, clonedHistory.TotalFragmentsSpent);
            Assert.AreEqual("Workshop", clonedHistory.LastRepairLocation);
            Assert.AreEqual(EquipmentCondition.Broken, clonedHistory.LastRepairConditionBefore);
            Assert.AreEqual(EquipmentCondition.Worn, clonedHistory.LastRepairConditionAfter);
        }

        [Test]
        public void EquipmentHistory_GetRepairSummary_ReturnsCorrectText()
        {
            var equipment = CreateTestEquipment(currentDurability: 50f, maxDurability: 100f);

            // Before any repairs
            Assert.AreEqual("Never repaired", equipment.History.GetRepairSummary());

            // After one repair (not from broken)
            var result1 = new RepairResult(
                "Test Blade", 50f, 100f, 100f,
                EquipmentCondition.Worn, EquipmentCondition.Excellent, 3);
            equipment.RecordRepair(result1);
            Assert.AreEqual("1 repair", equipment.History.GetRepairSummary());

            // After second repair (from broken)
            var result2 = new RepairResult(
                "Test Blade", 0f, 50f, 100f,
                EquipmentCondition.Broken, EquipmentCondition.Worn, 2);
            equipment.RecordRepair(result2);
            Assert.AreEqual("2 repairs (1 time from broken)", equipment.History.GetRepairSummary());
        }

        [Test]
        public void RepairService_ExecuteRepair_RecordsInHistory()
        {
            var config = CreateTestConfig(durabilityPerFragment: 20f);
            var fragmentDef = CreateTestFragmentDefinition();
            var inventory = CreateTestInventory();
            AddFragmentsToInventory(inventory, fragmentDef, 10);
            var service = new RepairService(config, fragmentDef, inventory);

            var equipment = CreateTestEquipment(currentDurability: 40f, maxDurability: 100f);
            service.ExecuteFullRepair(equipment);

            Assert.AreEqual(1, equipment.History.RepairCount);
            Assert.IsTrue(equipment.History.HasBeenRepaired);
            Assert.AreEqual(60, equipment.History.TotalDurabilityRestored);
            Assert.AreEqual(3, equipment.History.TotalFragmentsSpent);
        }

        [Test]
        public void RepairService_ExecuteRepair_RecordsRepairFromBroken()
        {
            var config = CreateTestConfig(durabilityPerFragment: 20f, canRepairBroken: true);
            var fragmentDef = CreateTestFragmentDefinition();
            var inventory = CreateTestInventory();
            AddFragmentsToInventory(inventory, fragmentDef, 5);
            var service = new RepairService(config, fragmentDef, inventory);

            var equipment = CreateTestEquipment(currentDurability: 0f, maxDurability: 100f);
            service.ExecuteFullRepair(equipment);

            Assert.AreEqual(1, equipment.History.RepairsFromBroken);
        }

        [Test]
        public void RepairService_ExecuteRepair_DefaultLocation_IsRefugeWorkbench()
        {
            var config = CreateTestConfig(durabilityPerFragment: 20f);
            var fragmentDef = CreateTestFragmentDefinition();
            var inventory = CreateTestInventory();
            AddFragmentsToInventory(inventory, fragmentDef, 10);
            var service = new RepairService(config, fragmentDef, inventory);

            var equipment = CreateTestEquipment(currentDurability: 50f, maxDurability: 100f);
            service.ExecuteFullRepair(equipment);

            Assert.AreEqual("Refuge Workbench", equipment.History.LastRepairLocation);
        }

        #endregion

        #region Integration Tests

        [Test]
        public void RepairService_FullRepairWorkflow()
        {
            var config = CreateTestConfig(durabilityPerFragment: 20f);
            var fragmentDef = CreateTestFragmentDefinition();
            var inventory = CreateTestInventory();
            AddFragmentsToInventory(inventory, fragmentDef,10);
            var service = new RepairService(config, fragmentDef, inventory);

            var equipment = CreateTestEquipment(currentDurability: 0f, maxDurability: 100f);
            Assert.AreEqual(EquipmentCondition.Broken, equipment.Condition);

            // Preview
            var preview = service.PreviewRepair(equipment);
            Assert.IsTrue(preview.CanRepair);
            Assert.AreEqual(5, preview.FragmentsForFullRepair);

            // Partial repair (2 fragments)
            var result1 = service.ExecutePartialRepair(equipment, 2);
            Assert.IsTrue(result1.Success);
            Assert.AreEqual(40f, equipment.CurrentDurability, 0.001f);
            Assert.IsTrue(result1.RestoredFromBroken);

            // Another partial repair
            var result2 = service.ExecutePartialRepair(equipment, 2);
            Assert.IsTrue(result2.Success);
            Assert.AreEqual(80f, equipment.CurrentDurability, 0.001f);

            // Full repair with remaining fragments
            var result3 = service.ExecuteFullRepair(equipment);
            Assert.IsTrue(result3.Success);
            Assert.AreEqual(100f, equipment.CurrentDurability, 0.001f);
            Assert.AreEqual(EquipmentCondition.Excellent, equipment.Condition);

            // Verify fragments consumed
            Assert.AreEqual(5, inventory.GetItemCount(fragmentDef)); // 10 - 2 - 2 - 1 = 5
        }

        [Test]
        public void RepairService_MaxRepairPercentageLimitsRepair()
        {
            var config = CreateTestConfig(durabilityPerFragment: 20f, maxRepairPercentage: 0.8f);
            var fragmentDef = CreateTestFragmentDefinition();
            var inventory = CreateTestInventory();
            AddFragmentsToInventory(inventory, fragmentDef,10);
            var service = new RepairService(config, fragmentDef, inventory);

            var equipment = CreateTestEquipment(currentDurability: 0f, maxDurability: 100f);

            var preview = service.PreviewRepair(equipment);
            Assert.AreEqual(4, preview.FragmentsForFullRepair); // Only 80 durability needed
            Assert.AreEqual(80f, preview.DurabilityAfterRepair, 0.001f);

            var result = service.ExecuteFullRepair(equipment);
            Assert.IsTrue(result.Success);
            Assert.AreEqual(80f, equipment.CurrentDurability, 0.001f);
        }

        #endregion
    }
}

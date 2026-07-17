using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;
using Lootbound.Gameplay.Equipment;
using Lootbound.Gameplay.Inventory;

namespace Lootbound.Tests.EditMode
{
    /// <summary>
    /// EditMode tests for the attunement foundation system.
    /// Tests attunement level, state, display names, and preservation.
    /// </summary>
    public class AttunementTests
    {
        #region Test Helpers

        private WeaponDefinition CreateTestWeapon(string id, string displayName = null)
        {
            var weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
            var type = typeof(ItemDefinition);
            type.GetField("itemId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(weapon, id);
            type.GetField("displayName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(weapon, displayName ?? id);
            type.GetField("isStackable", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(weapon, false);
            type.GetField("maxStackSize", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(weapon, 1);

            var weaponType = typeof(WeaponDefinition);
            weaponType.GetField("baseDamage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(weapon, 25f);
            weaponType.GetField("baseAttackSpeed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(weapon, 1.0f);
            weaponType.GetField("baseRange", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(weapon, 2.0f);
            weaponType.GetField("baseStagger", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(weapon, 0.3f);

            return weapon;
        }

        private EquipmentRegistry CreateTestRegistry(List<WeaponDefinition> weapons = null, List<AffixDefinition> affixes = null)
        {
            var registry = ScriptableObject.CreateInstance<EquipmentRegistry>();
            var type = typeof(EquipmentRegistry);

            if (weapons != null)
            {
                type.GetField("weaponDefinitions", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.SetValue(registry, weapons);
            }

            if (affixes != null)
            {
                type.GetField("affixDefinitions", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.SetValue(registry, affixes);
            }

            registry.Reinitialize();
            return registry;
        }

        private AffixDefinition CreateTestAffix(string id, AffixModifierType modifierType)
        {
            var affix = ScriptableObject.CreateInstance<AffixDefinition>();
            var type = typeof(AffixDefinition);

            type.GetField("affixId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(affix, id);
            type.GetField("displayName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(affix, id);
            type.GetField("modifierType", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(affix, modifierType);

            return affix;
        }

        #endregion

        #region AttunementState Tests

        [Test]
        public void AttunementState_HasExpectedValues()
        {
            Assert.AreEqual(0, (int)AttunementState.Unattuned);
            Assert.AreEqual(1, (int)AttunementState.Attuned);
            Assert.AreEqual(2, (int)AttunementState.Maximum);
        }

        #endregion

        #region AttunementHelper Tests

        [Test]
        public void AttunementHelper_GetState_Level0_ReturnsUnattuned()
        {
            var state = AttunementHelper.GetState(0, 5);
            Assert.AreEqual(AttunementState.Unattuned, state);
        }

        [Test]
        public void AttunementHelper_GetState_NegativeLevel_ReturnsUnattuned()
        {
            var state = AttunementHelper.GetState(-1, 5);
            Assert.AreEqual(AttunementState.Unattuned, state);
        }

        [Test]
        public void AttunementHelper_GetState_Level1To9_ReturnsAttuned()
        {
            // With max level 10, levels 1-9 should all be Attuned
            Assert.AreEqual(AttunementState.Attuned, AttunementHelper.GetState(1, 10));
            Assert.AreEqual(AttunementState.Attuned, AttunementHelper.GetState(5, 10));
            Assert.AreEqual(AttunementState.Attuned, AttunementHelper.GetState(9, 10));
        }

        [Test]
        public void AttunementHelper_GetState_Level5_IsNotMaximum_WithMax10()
        {
            // Level 5 is NOT maximum when max is 10
            var state = AttunementHelper.GetState(5, 10);
            Assert.AreEqual(AttunementState.Attuned, state);
        }

        [Test]
        public void AttunementHelper_GetState_AtMaximum_ReturnsMaximum()
        {
            var state = AttunementHelper.GetState(10, 10);
            Assert.AreEqual(AttunementState.Maximum, state);
        }

        [Test]
        public void AttunementHelper_GetState_AboveMaximum_ReturnsMaximum()
        {
            var state = AttunementHelper.GetState(15, 10);
            Assert.AreEqual(AttunementState.Maximum, state);
        }

        [Test]
        public void AttunementHelper_GetState_DefaultMaximum_Uses10()
        {
            // AttunementHelper uses AttunementFoundationConfig.DefaultMaximumAttunementLevel (10)
            Assert.AreEqual(AttunementState.Unattuned, AttunementHelper.GetState(0));
            Assert.AreEqual(AttunementState.Attuned, AttunementHelper.GetState(3));
            Assert.AreEqual(AttunementState.Attuned, AttunementHelper.GetState(5)); // NOT Maximum with max 10
            Assert.AreEqual(AttunementState.Attuned, AttunementHelper.GetState(9));
            Assert.AreEqual(AttunementState.Maximum, AttunementHelper.GetState(10));
        }

        [Test]
        public void AttunementHelper_IsAttuned_TrueForPositiveLevel()
        {
            Assert.IsFalse(AttunementHelper.IsAttuned(0));
            Assert.IsTrue(AttunementHelper.IsAttuned(1));
            Assert.IsTrue(AttunementHelper.IsAttuned(5));
        }

        [Test]
        public void AttunementHelper_IsAtMaximum_TrueAtOrAboveMax()
        {
            Assert.IsFalse(AttunementHelper.IsAtMaximum(4, 5));
            Assert.IsTrue(AttunementHelper.IsAtMaximum(5, 5));
            Assert.IsTrue(AttunementHelper.IsAtMaximum(6, 5));
        }

        [Test]
        public void AttunementHelper_ClampLevel_ClampsToRange()
        {
            Assert.AreEqual(0, AttunementHelper.ClampLevel(-5, 10));
            Assert.AreEqual(0, AttunementHelper.ClampLevel(0, 10));
            Assert.AreEqual(5, AttunementHelper.ClampLevel(5, 10));
            Assert.AreEqual(10, AttunementHelper.ClampLevel(10, 10));
            Assert.AreEqual(10, AttunementHelper.ClampLevel(15, 10));
        }

        [Test]
        public void AttunementHelper_ClampLevel_DefaultMaximum()
        {
            // Default max in AttunementHelper uses AttunementFoundationConfig.DefaultMaximumAttunementLevel (10)
            Assert.AreEqual(0, AttunementHelper.ClampLevel(-1));
            Assert.AreEqual(10, AttunementHelper.ClampLevel(10));
            Assert.AreEqual(10, AttunementHelper.ClampLevel(15)); // Clamps to 10
        }

        [Test]
        public void AttunementHelper_ClampLevel_InvalidMaximum_UsesDefault()
        {
            Assert.AreEqual(10, AttunementHelper.ClampLevel(15, 0));
            Assert.AreEqual(10, AttunementHelper.ClampLevel(15, -1));
        }

        [Test]
        public void AttunementHelper_ClampLevel_Level11_ClampsTo10()
        {
            Assert.AreEqual(10, AttunementHelper.ClampLevel(11, 10));
            Assert.AreEqual(10, AttunementHelper.ClampLevel(99, 10));
        }

        [Test]
        public void AttunementHelper_FormatDisplayName_Level0_ReturnsBaseName()
        {
            var result = AttunementHelper.FormatDisplayName("Traveler Blade", 0);
            Assert.AreEqual("Traveler Blade", result);
        }

        [Test]
        public void AttunementHelper_FormatDisplayName_NegativeLevel_ReturnsBaseName()
        {
            var result = AttunementHelper.FormatDisplayName("Traveler Blade", -1);
            Assert.AreEqual("Traveler Blade", result);
        }

        [Test]
        public void AttunementHelper_FormatDisplayName_PositiveLevel_AddsSuffix()
        {
            Assert.AreEqual("Traveler Blade +1", AttunementHelper.FormatDisplayName("Traveler Blade", 1));
            Assert.AreEqual("Traveler Blade +3", AttunementHelper.FormatDisplayName("Traveler Blade", 3));
            Assert.AreEqual("Traveler Blade +5", AttunementHelper.FormatDisplayName("Traveler Blade", 5));
        }

        [Test]
        public void AttunementHelper_FormatDisplayName_EmptyBaseName_HandlesGracefully()
        {
            Assert.AreEqual("", AttunementHelper.FormatDisplayName("", 0));
            Assert.AreEqual("+3", AttunementHelper.FormatDisplayName("", 3));
            Assert.AreEqual("", AttunementHelper.FormatDisplayName(null, 0));
            Assert.AreEqual("+5", AttunementHelper.FormatDisplayName(null, 5));
        }

        #endregion

        #region AttunementFoundationConfig Tests

        [Test]
        public void AttunementFoundationConfig_DefaultMaximumLevel_Is10()
        {
            Assert.AreEqual(10, AttunementFoundationConfig.DefaultMaximumAttunementLevel);
        }

        [Test]
        public void AttunementFoundationConfig_GetState_ReturnsCorrectState()
        {
            var config = ScriptableObject.CreateInstance<AttunementFoundationConfig>();

            Assert.AreEqual(AttunementState.Unattuned, config.GetState(0));
            Assert.AreEqual(AttunementState.Attuned, config.GetState(3));
            Assert.AreEqual(AttunementState.Attuned, config.GetState(5)); // 5 is NOT maximum with max 10
            Assert.AreEqual(AttunementState.Attuned, config.GetState(9));
            Assert.AreEqual(AttunementState.Maximum, config.GetState(10));

            Object.DestroyImmediate(config);
        }

        [Test]
        public void AttunementFoundationConfig_IsMaximumLevel_TrueAtMax()
        {
            var config = ScriptableObject.CreateInstance<AttunementFoundationConfig>();

            Assert.IsFalse(config.IsMaximumLevel(5)); // 5 is NOT maximum with max 10
            Assert.IsFalse(config.IsMaximumLevel(9));
            Assert.IsTrue(config.IsMaximumLevel(10));
            Assert.IsTrue(config.IsMaximumLevel(15));

            Object.DestroyImmediate(config);
        }

        [Test]
        public void AttunementFoundationConfig_ClampLevel_ClampsCorrectly()
        {
            var config = ScriptableObject.CreateInstance<AttunementFoundationConfig>();

            Assert.AreEqual(0, config.ClampLevel(-5));
            Assert.AreEqual(5, config.ClampLevel(5));
            Assert.AreEqual(10, config.ClampLevel(10));
            Assert.AreEqual(10, config.ClampLevel(15)); // Clamps to max 10

            Object.DestroyImmediate(config);
        }

        #endregion

        #region AttunementLevelChangeResult Tests

        [Test]
        public void AttunementLevelChangeResult_NoChange_HasCorrectProperties()
        {
            var result = new AttunementLevelChangeResult(3, 3, false, AttunementState.Attuned, AttunementState.Attuned);

            Assert.AreEqual(3, result.PreviousLevel);
            Assert.AreEqual(3, result.CurrentLevel);
            Assert.IsFalse(result.Changed);
            Assert.IsFalse(result.WasClamped);
            Assert.AreEqual(AttunementState.Attuned, result.PreviousState);
            Assert.AreEqual(AttunementState.Attuned, result.CurrentState);
        }

        [Test]
        public void AttunementLevelChangeResult_WithChange_ChangedIsTrue()
        {
            var result = new AttunementLevelChangeResult(0, 1, false, AttunementState.Unattuned, AttunementState.Attuned);

            Assert.IsTrue(result.Changed);
            Assert.AreEqual(0, result.PreviousLevel);
            Assert.AreEqual(1, result.CurrentLevel);
        }

        [Test]
        public void AttunementLevelChangeResult_Clamped_WasClampedIsTrue()
        {
            var result = new AttunementLevelChangeResult(3, 5, true, AttunementState.Attuned, AttunementState.Maximum);

            Assert.IsTrue(result.Changed);
            Assert.IsTrue(result.WasClamped);
            Assert.AreEqual(5, result.CurrentLevel);
        }

        #endregion

        #region EquipmentData Attunement Tests

        [Test]
        public void EquipmentData_NewEquipment_AttunementLevel0()
        {
            var data = new EquipmentData("weapon1", "Test Blade", ItemRarity.Common, null, "TestLocation");

            Assert.AreEqual(0, data.AttunementLevel);
            Assert.AreEqual(AttunementState.Unattuned, data.AttunementState);
            Assert.IsFalse(data.IsAttuned);
            Assert.IsFalse(data.IsAtMaximumAttunement);
        }

        [Test]
        public void EquipmentData_WithAttunementLevel_SetsDuringConstruction()
        {
            var data = new EquipmentData("weapon1", "Test Blade", ItemRarity.Common, null, "TestLocation", 100f, 3);

            Assert.AreEqual(3, data.AttunementLevel);
            Assert.AreEqual(AttunementState.Attuned, data.AttunementState);
            Assert.IsTrue(data.IsAttuned);
            Assert.IsFalse(data.IsAtMaximumAttunement);
        }

        [Test]
        public void EquipmentData_MaximumAttunementLevel_Returns10()
        {
            var data = new EquipmentData("weapon1", "Test Blade", ItemRarity.Common, null, "TestLocation");

            Assert.AreEqual(10, data.MaximumAttunementLevel);
        }

        [Test]
        public void EquipmentData_AtLevel5_IsNotMaximum_WithMax10()
        {
            var data = new EquipmentData("weapon1", "Test Blade", ItemRarity.Common, null, "TestLocation", 100f, 5);

            Assert.AreEqual(5, data.AttunementLevel);
            Assert.AreEqual(AttunementState.Attuned, data.AttunementState); // NOT Maximum
            Assert.IsTrue(data.IsAttuned);
            Assert.IsFalse(data.IsAtMaximumAttunement); // NOT at maximum
        }

        [Test]
        public void EquipmentData_AtLevel10_IsAtMaximumAttunement()
        {
            var data = new EquipmentData("weapon1", "Test Blade", ItemRarity.Common, null, "TestLocation", 100f, 10);

            Assert.AreEqual(10, data.AttunementLevel);
            Assert.AreEqual(AttunementState.Maximum, data.AttunementState);
            Assert.IsTrue(data.IsAttuned);
            Assert.IsTrue(data.IsAtMaximumAttunement);
        }

        [Test]
        public void EquipmentData_SetAttunementLevel_ChangesLevel()
        {
            var data = new EquipmentData("weapon1", "Test Blade", ItemRarity.Common, null, "TestLocation");

            var result = data.SetAttunementLevel(3);

            Assert.AreEqual(0, result.PreviousLevel);
            Assert.AreEqual(3, result.CurrentLevel);
            Assert.IsTrue(result.Changed);
            Assert.IsFalse(result.WasClamped);
            Assert.AreEqual(AttunementState.Unattuned, result.PreviousState);
            Assert.AreEqual(AttunementState.Attuned, result.CurrentState);

            Assert.AreEqual(3, data.AttunementLevel);
        }

        [Test]
        public void EquipmentData_SetAttunementLevel_ClampsToRange()
        {
            var data = new EquipmentData("weapon1", "Test Blade", ItemRarity.Common, null, "TestLocation");

            // Test upper clamp - now clamps to 10
            var resultHigh = data.SetAttunementLevel(15);
            Assert.AreEqual(10, resultHigh.CurrentLevel);
            Assert.IsTrue(resultHigh.WasClamped);
            Assert.AreEqual(10, data.AttunementLevel);

            // Test lower clamp
            var resultLow = data.SetAttunementLevel(-5);
            Assert.AreEqual(0, resultLow.CurrentLevel);
            Assert.IsTrue(resultLow.WasClamped);
            Assert.AreEqual(0, data.AttunementLevel);
        }

        [Test]
        public void EquipmentData_SetAttunementLevel_NoChange_ReturnsFalseChanged()
        {
            var data = new EquipmentData("weapon1", "Test Blade", ItemRarity.Common, null, "TestLocation", 100f, 3);

            var result = data.SetAttunementLevel(3);

            Assert.IsFalse(result.Changed);
            Assert.AreEqual(3, result.PreviousLevel);
            Assert.AreEqual(3, result.CurrentLevel);
        }

        [Test]
        public void EquipmentData_Clone_PreservesAttunementLevel()
        {
            var original = new EquipmentData("weapon1", "Test Blade", ItemRarity.Common, null, "TestLocation", 100f, 4);
            var clone = original.Clone();

            Assert.AreEqual(4, clone.AttunementLevel);
            Assert.AreEqual(original.InstanceId, clone.InstanceId);
        }

        [Test]
        public void EquipmentData_CloneWithNewId_ResetsAttunementToZero()
        {
            var original = new EquipmentData("weapon1", "Test Blade", ItemRarity.Common, null, "TestLocation", 100f, 4);
            var clone = original.CloneWithNewId();

            Assert.AreEqual(0, clone.AttunementLevel);
            Assert.AreNotEqual(original.InstanceId, clone.InstanceId);
            Assert.AreEqual(AttunementState.Unattuned, clone.AttunementState);
        }

        [Test]
        public void EquipmentData_GetAttunedDisplayName_Level0_ReturnsBaseName()
        {
            var weapon = CreateTestWeapon("blade", "Traveler Blade");
            var registry = CreateTestRegistry(new List<WeaponDefinition> { weapon });
            var data = new EquipmentData("blade", "Traveler Blade", ItemRarity.Common, null, "TestLocation");

            var name = data.GetAttunedDisplayName(registry);

            Assert.AreEqual("Traveler Blade", name);
        }

        [Test]
        public void EquipmentData_GetAttunedDisplayName_WithLevel_AddsSuffix()
        {
            var weapon = CreateTestWeapon("blade", "Traveler Blade");
            var registry = CreateTestRegistry(new List<WeaponDefinition> { weapon });
            var data = new EquipmentData("blade", "Traveler Blade", ItemRarity.Common, null, "TestLocation", 100f, 3);

            var name = data.GetAttunedDisplayName(registry);

            Assert.AreEqual("Traveler Blade +3", name);
        }

        [Test]
        public void EquipmentData_GetAttunedDisplayName_MaxLevel_ShowsPlus10()
        {
            var weapon = CreateTestWeapon("blade", "Traveler Blade");
            var registry = CreateTestRegistry(new List<WeaponDefinition> { weapon });
            var data = new EquipmentData("blade", "Traveler Blade", ItemRarity.Common, null, "TestLocation", 100f, 10);

            var name = data.GetAttunedDisplayName(registry);

            Assert.AreEqual("Traveler Blade +10", name);
        }

        [Test]
        public void EquipmentData_GetAttunedDisplayName_UsesCustomNameIfSet()
        {
            var weapon = CreateTestWeapon("blade", "Generic Blade");
            var registry = CreateTestRegistry(new List<WeaponDefinition> { weapon });
            var data = new EquipmentData("blade", "My Named Blade", ItemRarity.Common, null, "TestLocation", 100f, 2);

            var name = data.GetAttunedDisplayName(registry);

            Assert.AreEqual("My Named Blade +2", name);
        }

        [Test]
        public void EquipmentData_GetAttunedDisplayName_FallsBackToDefinitionName()
        {
            var weapon = CreateTestWeapon("blade", "Definition Name");
            var registry = CreateTestRegistry(new List<WeaponDefinition> { weapon });
            // Pass null or empty custom name
            var data = new EquipmentData("blade", null, ItemRarity.Common, null, "TestLocation", 100f, 1);

            var name = data.GetAttunedDisplayName(registry);

            Assert.AreEqual("Definition Name +1", name);
        }

        [Test]
        public void EquipmentData_SerializationConstructor_PreservesAttunement()
        {
            var history = new EquipmentHistory("TestLocation");
            var data = new EquipmentData(
                "instance-guid",
                "blade",
                "Test Blade",
                ItemRarity.Rare,
                null,
                history,
                80f,
                100f,
                4);

            Assert.AreEqual(4, data.AttunementLevel);
            Assert.AreEqual(AttunementState.Attuned, data.AttunementState);
            Assert.IsTrue(data.IsAttuned);
        }

        [Test]
        public void EquipmentData_SerializationConstructor_ClampsInvalidLevel()
        {
            var history = new EquipmentHistory("TestLocation");
            var data = new EquipmentData(
                "instance-guid",
                "blade",
                "Test Blade",
                ItemRarity.Rare,
                null,
                history,
                100f,
                100f,
                99); // Invalid level

            Assert.AreEqual(10, data.AttunementLevel); // Clamped to max 10
        }

        #endregion

        #region Attunement Preservation Tests

        [Test]
        public void EquipmentData_DurabilityChange_PreservesAttunement()
        {
            var data = new EquipmentData("weapon1", "Test Blade", ItemRarity.Common, null, "TestLocation", 100f, 3);

            data.SetDurability(50f);
            Assert.AreEqual(3, data.AttunementLevel);

            data.ReduceDurability(30f);
            Assert.AreEqual(3, data.AttunementLevel);

            data.RestoreDurability(80f);
            Assert.AreEqual(3, data.AttunementLevel);

            // Even at broken state
            data.SetDurability(0f);
            Assert.AreEqual(EquipmentCondition.Broken, data.Condition);
            Assert.AreEqual(3, data.AttunementLevel);
        }

        [Test]
        public void EquipmentData_IsEquippedChange_PreservesAttunement()
        {
            var data = new EquipmentData("weapon1", "Test Blade", ItemRarity.Common, null, "TestLocation", 100f, 4);

            data.IsEquipped = true;
            Assert.AreEqual(4, data.AttunementLevel);

            data.IsEquipped = false;
            Assert.AreEqual(4, data.AttunementLevel);
        }

        [Test]
        public void EquipmentData_RecordKill_PreservesAttunement()
        {
            var data = new EquipmentData("weapon1", "Test Blade", ItemRarity.Common, null, "TestLocation", 100f, 2);

            data.RecordKill();
            data.RecordKill();
            data.RecordKill();

            Assert.AreEqual(2, data.AttunementLevel);
        }

        [Test]
        public void ItemInstance_Clone_PreservesAttunement()
        {
            var weapon = CreateTestWeapon("blade");
            var equipData = new EquipmentData("blade", "Test Blade", ItemRarity.Common, null, "Location", 100f, 3);
            var instance = new ItemInstance(weapon, equipData);

            var clone = instance.Clone();

            Assert.AreEqual(3, clone.EquipmentData.AttunementLevel);
            Assert.AreEqual(instance.EquipmentData.InstanceId, clone.EquipmentData.InstanceId);
        }

        [Test]
        public void ItemInstance_CloneAsNewEquipment_ResetsAttunement()
        {
            var weapon = CreateTestWeapon("blade");
            var equipData = new EquipmentData("blade", "Test Blade", ItemRarity.Common, null, "Location", 100f, 4);
            var instance = new ItemInstance(weapon, equipData);

            var clone = instance.CloneAsNewEquipment();

            Assert.AreEqual(0, clone.EquipmentData.AttunementLevel);
            Assert.AreNotEqual(instance.EquipmentData.InstanceId, clone.EquipmentData.InstanceId);
        }

        #endregion

        #region Stat Modification Tests (AttunementCoreConfig)

        private AttunementCoreConfig CreateTestAttunementConfig()
        {
            return ScriptableObject.CreateInstance<AttunementCoreConfig>();
        }

        [Test]
        public void ResolveStats_Level0_NoBonus()
        {
            var weapon = CreateTestWeapon("blade");
            var registry = CreateTestRegistry(new List<WeaponDefinition> { weapon });
            var attunementConfig = CreateTestAttunementConfig();

            var weaponType = typeof(WeaponDefinition);
            weaponType.GetField("baseDamage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(weapon, 100f);

            var data = new EquipmentData("blade", "Test Blade", ItemRarity.Common, null, "Location", 100f, 0);
            var stats = data.ResolveStats(registry, null, attunementConfig);

            // Level 0 should have no bonus (×1.00)
            Assert.AreEqual(100f, stats.Damage, 0.1f, "Level 0 should have no damage bonus");

            Object.DestroyImmediate(attunementConfig);
        }

        [Test]
        public void ResolveStats_Level1_DamageIncreasedBy4Percent()
        {
            var weapon = CreateTestWeapon("blade");
            var registry = CreateTestRegistry(new List<WeaponDefinition> { weapon });
            var attunementConfig = CreateTestAttunementConfig();

            var weaponType = typeof(WeaponDefinition);
            weaponType.GetField("baseDamage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(weapon, 100f);

            var data = new EquipmentData("blade", "Test Blade", ItemRarity.Common, null, "Location", 100f, 1);
            var stats = data.ResolveStats(registry, null, attunementConfig);

            // Level 1 should have ×1.04 damage
            Assert.AreEqual(104f, stats.Damage, 0.1f, "+1 should increase damage by 4%");

            Object.DestroyImmediate(attunementConfig);
        }

        [Test]
        public void ResolveStats_Level5_DamageIncreasedBy24Percent()
        {
            var weapon = CreateTestWeapon("blade");
            var registry = CreateTestRegistry(new List<WeaponDefinition> { weapon });
            var attunementConfig = CreateTestAttunementConfig();

            var weaponType = typeof(WeaponDefinition);
            weaponType.GetField("baseDamage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(weapon, 100f);

            var data = new EquipmentData("blade", "Test Blade", ItemRarity.Common, null, "Location", 100f, 5);
            var stats = data.ResolveStats(registry, null, attunementConfig);

            // Level 5 should have ×1.24 damage
            Assert.AreEqual(124f, stats.Damage, 0.1f, "+5 should increase damage by 24%");

            Object.DestroyImmediate(attunementConfig);
        }

        [Test]
        public void ResolveStats_Level10_DamageIncreasedBy60Percent()
        {
            var weapon = CreateTestWeapon("blade");
            var registry = CreateTestRegistry(new List<WeaponDefinition> { weapon });
            var attunementConfig = CreateTestAttunementConfig();

            var weaponType = typeof(WeaponDefinition);
            weaponType.GetField("baseDamage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(weapon, 100f);

            var data = new EquipmentData("blade", "Test Blade", ItemRarity.Common, null, "Location", 100f, 10);
            var stats = data.ResolveStats(registry, null, attunementConfig);

            // Level 10 should have ×1.60 damage
            Assert.AreEqual(160f, stats.Damage, 0.1f, "+10 should increase damage by 60%");

            Object.DestroyImmediate(attunementConfig);
        }

        [Test]
        public void ResolveStats_WithoutConfig_NoAttunementBonus()
        {
            // When no AttunementCoreConfig is provided, stats should NOT include attunement bonus
            var weapon = CreateTestWeapon("blade");
            var registry = CreateTestRegistry(new List<WeaponDefinition> { weapon });

            var weaponType = typeof(WeaponDefinition);
            weaponType.GetField("baseDamage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(weapon, 100f);

            var data = new EquipmentData("blade", "Test Blade", ItemRarity.Common, null, "Location", 100f, 5);
            var stats = data.ResolveStats(registry); // No attunement config

            // Without config, no attunement bonus is applied
            Assert.AreEqual(100f, stats.Damage, 0.1f, "Without config, attunement should not affect damage");
        }

        [Test]
        public void ResolveStats_AttunementAppliedAfterAffixes()
        {
            // Pipeline: Base → Affixes → ATTUNEMENT → Broken → Clamp
            var weapon = CreateTestWeapon("blade");
            var sharpAffix = CreateTestAffix("sharp", AffixModifierType.DamagePercent);
            var registry = CreateTestRegistry(
                new List<WeaponDefinition> { weapon },
                new List<AffixDefinition> { sharpAffix });
            var attunementConfig = CreateTestAttunementConfig();

            var weaponType = typeof(WeaponDefinition);
            weaponType.GetField("baseDamage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(weapon, 100f);

            // Add a +20% damage affix (Sharp)
            var affixes = new List<AffixInstance>
            {
                new AffixInstance("sharp", 20f) // +20% damage
            };
            var data = new EquipmentData("blade", "Test Blade", ItemRarity.Rare, affixes, "Location", 100f, 2);

            var stats = data.ResolveStats(registry, null, attunementConfig);

            // Base: 100 → Affix +20%: 120 → Attunement +2 (×1.08): 129.6
            Assert.AreEqual(129.6f, stats.Damage, 0.5f, "Attunement should be applied after affixes");

            Object.DestroyImmediate(attunementConfig);
            Object.DestroyImmediate(sharpAffix);
        }

        [Test]
        public void ResolveStats_AttunementAppliedBeforeBrokenPenalty()
        {
            // Pipeline: Base → Affixes → ATTUNEMENT → Broken → Clamp
            var weapon = CreateTestWeapon("blade");
            var registry = CreateTestRegistry(new List<WeaponDefinition> { weapon });
            var attunementConfig = CreateTestAttunementConfig();
            var brokenConfig = ScriptableObject.CreateInstance<BrokenWeaponConfig>();

            var weaponType = typeof(WeaponDefinition);
            weaponType.GetField("baseDamage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(weapon, 100f);

            // Create a broken weapon (+5 attunement)
            var data = new EquipmentData("blade", "Test Blade", ItemRarity.Common, null, "Location", 100f, 5);
            data.SetDurability(0f); // Broken

            var stats = data.ResolveStats(registry, brokenConfig, attunementConfig);

            // Base: 100 → Attunement +5 (×1.24): 124 → Broken (×0.3): 37.2
            Assert.AreEqual(37.2f, stats.Damage, 0.5f, "Broken penalty should be applied after attunement");

            Object.DestroyImmediate(attunementConfig);
            Object.DestroyImmediate(brokenConfig);
        }

        [Test]
        public void ResolveStats_AllMultipliers_CorrectOrder()
        {
            // Full pipeline test: Base → Affixes → ATTUNEMENT → Broken → Clamp
            var weapon = CreateTestWeapon("blade");
            var sharpAffix = CreateTestAffix("sharp", AffixModifierType.DamagePercent);
            var registry = CreateTestRegistry(
                new List<WeaponDefinition> { weapon },
                new List<AffixDefinition> { sharpAffix });
            var attunementConfig = CreateTestAttunementConfig();
            var brokenConfig = ScriptableObject.CreateInstance<BrokenWeaponConfig>();

            var weaponType = typeof(WeaponDefinition);
            weaponType.GetField("baseDamage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(weapon, 100f);

            // Add a +20% damage affix
            var affixes = new List<AffixInstance>
            {
                new AffixInstance("sharp", 20f)
            };
            var data = new EquipmentData("blade", "Test Blade", ItemRarity.Rare, affixes, "Location", 100f, 2);
            data.SetDurability(0f); // Broken

            var stats = data.ResolveStats(registry, brokenConfig, attunementConfig);

            // Base: 100 → Affix +20%: 120 → Attunement +2 (×1.08): 129.6 → Broken (×0.3): 38.88
            Assert.AreEqual(38.88f, stats.Damage, 0.5f, "Full pipeline should apply multipliers in correct order");

            Object.DestroyImmediate(sharpAffix);

            Object.DestroyImmediate(attunementConfig);
            Object.DestroyImmediate(brokenConfig);
        }

        #endregion

        #region Generator Tests

        [Test]
        public void EquipmentGenerator_GeneratesWithDefaultAttunement()
        {
            var weapon = CreateTestWeapon("blade");
            var registry = CreateTestRegistry(new List<WeaponDefinition> { weapon });
            var generator = new EquipmentGenerator(registry);

            var instance = generator.GenerateWeapon(weapon, "TestLocation", seed: 12345);

            Assert.IsNotNull(instance);
            Assert.AreEqual(0, instance.EquipmentData.AttunementLevel);
            Assert.AreEqual(AttunementState.Unattuned, instance.EquipmentData.AttunementState);
        }

        [Test]
        public void EquipmentGenerator_CreateSimpleWeapon_AttunementZero()
        {
            var weapon = CreateTestWeapon("blade");
            var registry = CreateTestRegistry(new List<WeaponDefinition> { weapon });
            var generator = new EquipmentGenerator(registry);

            var instance = generator.CreateSimpleWeapon(weapon, "My Blade", "Refuge");

            Assert.AreEqual(0, instance.EquipmentData.AttunementLevel);
            Assert.IsFalse(instance.EquipmentData.IsAttuned);
        }

        #endregion

        #region Two Instances Independence Tests

        [Test]
        public void TwoInstances_SameDefinition_DifferentLevels_DifferentGUIDs()
        {
            // Two Traveler Blade +0 and +2 must have different GUIDs and independent levels
            var data1 = new EquipmentData("traveler_blade", "Traveler Blade", ItemRarity.Common, null, "Location", 100f, 0);
            var data2 = new EquipmentData("traveler_blade", "Traveler Blade", ItemRarity.Common, null, "Location", 100f, 2);

            // Different GUIDs
            Assert.AreNotEqual(data1.InstanceId, data2.InstanceId);

            // Independent levels
            Assert.AreEqual(0, data1.AttunementLevel);
            Assert.AreEqual(2, data2.AttunementLevel);

            // Same definition
            Assert.AreEqual(data1.DefinitionId, data2.DefinitionId);
        }

        [Test]
        public void TwoInstances_LevelChangeOnOne_DoesNotAffectOther()
        {
            var data1 = new EquipmentData("blade", "Blade", ItemRarity.Common, null, "Location", 100f, 1);
            var data2 = new EquipmentData("blade", "Blade", ItemRarity.Common, null, "Location", 100f, 3);

            // Change level on data1
            data1.SetAttunementLevel(5);

            // data2 must remain unchanged
            Assert.AreEqual(5, data1.AttunementLevel);
            Assert.AreEqual(3, data2.AttunementLevel);
        }

        #endregion

        #region Identity Preservation Tests

        [Test]
        public void EquipmentData_SetAttunementLevel_PreservesIdentity()
        {
            var affixes = new List<AffixInstance>
            {
                new AffixInstance("sharp", 10f)
            };
            var data = new EquipmentData("blade", "Test Blade", ItemRarity.Rare, affixes, "Dark Cave", 75f, 0);

            string originalGuid = data.InstanceId;
            string originalName = data.CustomName;
            ItemRarity originalRarity = data.Rarity;
            float originalDurability = data.CurrentDurability;
            int originalAffixCount = data.Affixes.Count;
            string originalLocation = data.History.FoundLocation;

            // Change attunement level
            data.SetAttunementLevel(4);

            // All identity fields must be preserved
            Assert.AreEqual(originalGuid, data.InstanceId, "GUID must be unchanged");
            Assert.AreEqual(originalName, data.CustomName, "CustomName must be unchanged");
            Assert.AreEqual(originalRarity, data.Rarity, "Rarity must be unchanged");
            Assert.AreEqual(originalDurability, data.CurrentDurability, "Durability must be unchanged");
            Assert.AreEqual(originalAffixCount, data.Affixes.Count, "Affixes must be unchanged");
            Assert.AreEqual(originalLocation, data.History.FoundLocation, "History must be unchanged");

            // Only attunement changed
            Assert.AreEqual(4, data.AttunementLevel);
        }

        #endregion

        #region AttunementCoreConfig Tests

        [Test]
        public void AttunementCoreConfig_HasCorrectTierCount()
        {
            var config = CreateTestAttunementConfig();

            // Should have 11 tiers (0-10)
            Assert.AreEqual(11, config.TierCount);

            Object.DestroyImmediate(config);
        }

        [Test]
        public void AttunementCoreConfig_MaximumLevel_Is10()
        {
            var config = CreateTestAttunementConfig();

            Assert.AreEqual(10, config.MaximumLevel);

            Object.DestroyImmediate(config);
        }

        [Test]
        public void AttunementCoreConfig_GetTier_Level0_NoBonus()
        {
            var config = CreateTestAttunementConfig();

            var tier = config.GetTier(0);

            Assert.AreEqual(1f, tier.DamageMultiplier, 0.001f);
            Assert.IsFalse(tier.HasDamageBonus);
            Assert.IsFalse(tier.HasAnyBonus);

            Object.DestroyImmediate(config);
        }

        [Test]
        public void AttunementCoreConfig_GetTier_Level5_HasCorrectMultiplier()
        {
            var config = CreateTestAttunementConfig();

            var tier = config.GetTier(5);

            Assert.AreEqual(1.24f, tier.DamageMultiplier, 0.001f);
            Assert.IsTrue(tier.HasDamageBonus);
            Assert.AreEqual(24f, tier.DamageBonusPercent, 0.1f);

            Object.DestroyImmediate(config);
        }

        [Test]
        public void AttunementCoreConfig_GetTier_Level10_HasCorrectMultiplier()
        {
            var config = CreateTestAttunementConfig();

            var tier = config.GetTier(10);

            Assert.AreEqual(1.60f, tier.DamageMultiplier, 0.001f);
            Assert.IsTrue(tier.HasDamageBonus);
            Assert.AreEqual(60f, tier.DamageBonusPercent, 0.1f);

            Object.DestroyImmediate(config);
        }

        [Test]
        public void AttunementCoreConfig_GetTier_OutOfBounds_Clamps()
        {
            var config = CreateTestAttunementConfig();

            var tierNegative = config.GetTier(-5);
            var tierAboveMax = config.GetTier(99);

            // Negative clamps to 0
            Assert.AreEqual(1f, tierNegative.DamageMultiplier, 0.001f);

            // Above max clamps to max (10)
            Assert.AreEqual(1.60f, tierAboveMax.DamageMultiplier, 0.001f);

            Object.DestroyImmediate(config);
        }

        [Test]
        public void AttunementCoreConfig_GetDamageBonusPercent_ReturnsCorrectValues()
        {
            var config = CreateTestAttunementConfig();

            Assert.AreEqual(0f, config.GetDamageBonusPercent(0), 0.1f);
            Assert.AreEqual(4f, config.GetDamageBonusPercent(1), 0.1f);
            Assert.AreEqual(24f, config.GetDamageBonusPercent(5), 0.1f);
            Assert.AreEqual(60f, config.GetDamageBonusPercent(10), 0.1f);

            Object.DestroyImmediate(config);
        }

        [Test]
        public void AttunementCoreConfig_HasBonusAtLevel_CorrectForAllLevels()
        {
            var config = CreateTestAttunementConfig();

            Assert.IsFalse(config.HasBonusAtLevel(0), "Level 0 should have no bonus");
            Assert.IsTrue(config.HasBonusAtLevel(1), "Level 1 should have bonus");
            Assert.IsTrue(config.HasBonusAtLevel(10), "Level 10 should have bonus");

            Object.DestroyImmediate(config);
        }

        #endregion

        #region AttunementTier Tests

        [Test]
        public void AttunementTier_Default_HasNoBonus()
        {
            var tier = AttunementTier.Default;

            Assert.AreEqual(1f, tier.DamageMultiplier);
            Assert.AreEqual(1f, tier.AttackSpeedMultiplier);
            Assert.AreEqual(1f, tier.RangeMultiplier);
            Assert.AreEqual(1f, tier.StaggerMultiplier);
            Assert.IsFalse(tier.HasDamageBonus);
            Assert.IsFalse(tier.HasAnyBonus);
        }

        [Test]
        public void AttunementTier_ApplyMultipliers_CalculatesCorrectly()
        {
            var tier = new AttunementTier
            {
                DamageMultiplier = 1.24f,
                AttackSpeedMultiplier = 1f,
                RangeMultiplier = 1f,
                StaggerMultiplier = 1f
            };

            var (damage, attackSpeed, range, stagger) = tier.ApplyMultipliers(100f, 1.0f, 2.0f, 0.3f);

            Assert.AreEqual(124f, damage, 0.01f);
            Assert.AreEqual(1.0f, attackSpeed, 0.01f);
            Assert.AreEqual(2.0f, range, 0.01f);
            Assert.AreEqual(0.3f, stagger, 0.01f);
        }

        [Test]
        public void AttunementTier_DamageBonusPercent_Correct()
        {
            var tier = new AttunementTier { DamageMultiplier = 1.24f };

            Assert.AreEqual(24f, tier.DamageBonusPercent, 0.01f);
        }

        #endregion

        #region TryIncreaseAttunement Tests

        [Test]
        public void TryIncreaseAttunement_FromLevel0_Success()
        {
            var data = new EquipmentData("blade", "Test Blade", ItemRarity.Common, null, "Location", 100f, 0);

            var result = data.TryIncreaseAttunement(10);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(0, result.PreviousLevel);
            Assert.AreEqual(1, result.CurrentLevel);
            Assert.IsTrue(result.LevelIncreased);
            Assert.IsFalse(result.WasAtMaximum);
            Assert.AreEqual(1, data.AttunementLevel);
        }

        [Test]
        public void TryIncreaseAttunement_FromLevel9_Success()
        {
            var data = new EquipmentData("blade", "Test Blade", ItemRarity.Common, null, "Location", 100f, 9);

            var result = data.TryIncreaseAttunement(10);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(9, result.PreviousLevel);
            Assert.AreEqual(10, result.CurrentLevel);
            Assert.IsTrue(result.IsNowAtMaximum);
            Assert.AreEqual(10, data.AttunementLevel);
        }

        [Test]
        public void TryIncreaseAttunement_AtLevel10_Fails()
        {
            var data = new EquipmentData("blade", "Test Blade", ItemRarity.Common, null, "Location", 100f, 10);

            var result = data.TryIncreaseAttunement(10);

            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.WasAtMaximum);
            Assert.AreEqual(10, result.PreviousLevel);
            Assert.AreEqual(10, result.CurrentLevel);
            Assert.IsFalse(result.LevelIncreased);
            Assert.AreEqual(10, data.AttunementLevel);
        }

        [Test]
        public void TryIncreaseAttunement_MultipleIncreases_Works()
        {
            var data = new EquipmentData("blade", "Test Blade", ItemRarity.Common, null, "Location", 100f, 0);

            // Increase 5 times
            for (int i = 0; i < 5; i++)
            {
                var result = data.TryIncreaseAttunement(10);
                Assert.IsTrue(result.Success, $"Increase {i + 1} should succeed");
            }

            Assert.AreEqual(5, data.AttunementLevel);
        }

        [Test]
        public void TryIncreaseAttunement_UsesDefaultMaxIfNotProvided()
        {
            var data = new EquipmentData("blade", "Test Blade", ItemRarity.Common, null, "Location", 100f, 9);

            // Use -1 to trigger default max (10)
            var result = data.TryIncreaseAttunement(-1);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(10, data.AttunementLevel);
        }

        #endregion

        #region AttunementAttemptResult Tests

        [Test]
        public void AttunementAttemptResult_AlreadyMaximum_HasCorrectProperties()
        {
            var result = AttunementAttemptResult.AlreadyMaximum(10, 10);

            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.WasAtMaximum);
            Assert.AreEqual(10, result.PreviousLevel);
            Assert.AreEqual(10, result.CurrentLevel);
            Assert.AreEqual(10, result.MaximumLevel);
            Assert.IsFalse(result.LevelIncreased);
            Assert.IsTrue(result.IsNowAtMaximum);
        }

        [Test]
        public void AttunementAttemptResult_Succeeded_HasCorrectProperties()
        {
            var result = AttunementAttemptResult.Succeeded(4, 5, 10);

            Assert.IsTrue(result.Success);
            Assert.IsFalse(result.WasAtMaximum);
            Assert.AreEqual(4, result.PreviousLevel);
            Assert.AreEqual(5, result.CurrentLevel);
            Assert.AreEqual(10, result.MaximumLevel);
            Assert.IsTrue(result.LevelIncreased);
            Assert.IsFalse(result.IsNowAtMaximum);
        }

        [Test]
        public void AttunementAttemptResult_Succeeded_ToMaximum_IsNowAtMaximumTrue()
        {
            var result = AttunementAttemptResult.Succeeded(9, 10, 10);

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.IsNowAtMaximum);
        }

        [Test]
        public void AttunementAttemptResult_Failed_HasCorrectProperties()
        {
            // Reserved for future mechanics with failure chances
            var result = AttunementAttemptResult.Failed(5, 10);

            Assert.IsFalse(result.Success);
            Assert.IsFalse(result.WasAtMaximum);
            Assert.AreEqual(5, result.PreviousLevel);
            Assert.AreEqual(5, result.CurrentLevel);
            Assert.IsFalse(result.LevelIncreased);
        }

        [Test]
        public void AttunementAttemptResult_ToString_FormatsCorrectly()
        {
            var alreadyMax = AttunementAttemptResult.AlreadyMaximum(10, 10);
            var success = AttunementAttemptResult.Succeeded(4, 5, 10);
            var failed = AttunementAttemptResult.Failed(5, 10);

            Assert.IsTrue(alreadyMax.ToString().Contains("Already at maximum"));
            Assert.IsTrue(success.ToString().Contains("Success"));
            Assert.IsTrue(failed.ToString().Contains("Failed"));
        }

        #endregion

        #region Attunement Preservation Through Repair Tests

        [Test]
        public void Attunement_PreservedThroughRepair()
        {
            var data = new EquipmentData("blade", "Test Blade", ItemRarity.Common, null, "Location", 100f, 5);

            // Break the weapon
            data.SetDurability(0f);
            Assert.AreEqual(EquipmentCondition.Broken, data.Condition);
            Assert.AreEqual(5, data.AttunementLevel, "Attunement should be preserved when broken");

            // Repair the weapon
            data.RestoreDurability(100f);
            Assert.AreEqual(EquipmentCondition.Excellent, data.Condition);
            Assert.AreEqual(5, data.AttunementLevel, "Attunement should be preserved after repair");
        }

        [Test]
        public void Attunement_PreservedThroughConditionChanges()
        {
            var data = new EquipmentData("blade", "Test Blade", ItemRarity.Common, null, "Location", 100f, 7);

            // Cycle through conditions
            data.SetDurability(75f); // Good
            Assert.AreEqual(7, data.AttunementLevel);

            data.SetDurability(40f); // Worn
            Assert.AreEqual(7, data.AttunementLevel);

            data.SetDurability(15f); // Damaged
            Assert.AreEqual(7, data.AttunementLevel);

            data.SetDurability(0f); // Broken
            Assert.AreEqual(7, data.AttunementLevel);

            data.SetDurability(100f); // Back to Pristine
            Assert.AreEqual(7, data.AttunementLevel);
        }

        #endregion

        #region AttunementService Tests (Stone-Based Attunement)

        private ItemDefinition CreateTestStackableItem(string id, string displayName, int maxStack = 20)
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            var type = typeof(ItemDefinition);
            type.GetField("itemId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(item, id);
            type.GetField("displayName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(item, displayName);
            type.GetField("isStackable", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(item, true);
            type.GetField("maxStackSize", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(item, maxStack);
            return item;
        }

        private AttunementCostConfig CreateTestCostConfig(ItemDefinition stoneDefinition, int stonesPerAttempt = 1)
        {
            var config = ScriptableObject.CreateInstance<AttunementCostConfig>();
            var type = typeof(AttunementCostConfig);

            type.GetField("attunementStoneDefinition", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(config, stoneDefinition);
            type.GetField("stonesPerAttempt", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(config, stonesPerAttempt);
            type.GetField("allowDebugFreeAttempts", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(config, true);

            return config;
        }

        [Test]
        public void AttunementCostConfig_IsValid_TrueWhenStoneDefinitionSet()
        {
            var stoneDef = CreateTestStackableItem("attunement_stone", "Attunement Stone");
            var config = CreateTestCostConfig(stoneDef);

            Assert.IsTrue(config.IsValid, "Config should be valid when stone definition is set");

            Object.DestroyImmediate(config);
            Object.DestroyImmediate(stoneDef);
        }

        [Test]
        public void AttunementCostConfig_IsValid_FalseWhenNoStoneDefinition()
        {
            var config = ScriptableObject.CreateInstance<AttunementCostConfig>();

            Assert.IsFalse(config.IsValid, "Config should be invalid when no stone definition");

            Object.DestroyImmediate(config);
        }

        [Test]
        public void AttunementService_GetAvailableStones_ReturnsZero_WhenNoInventory()
        {
            var stoneDef = CreateTestStackableItem("attunement_stone", "Attunement Stone");
            var costConfig = CreateTestCostConfig(stoneDef);
            var coreConfig = CreateTestAttunementConfig();

            var service = new AttunementService(costConfig, coreConfig, null);

            Assert.AreEqual(0, service.GetAvailableStones(), "Should return 0 when no inventory");

            Object.DestroyImmediate(costConfig);
            Object.DestroyImmediate(coreConfig);
            Object.DestroyImmediate(stoneDef);
        }

        [Test]
        public void AttunementService_MaximumLevel_ReturnsConfigValue()
        {
            var stoneDef = CreateTestStackableItem("attunement_stone", "Attunement Stone");
            var costConfig = CreateTestCostConfig(stoneDef);
            var coreConfig = CreateTestAttunementConfig();

            var service = new AttunementService(costConfig, coreConfig, null);

            Assert.AreEqual(10, service.MaximumLevel, "Should return core config max level");

            Object.DestroyImmediate(costConfig);
            Object.DestroyImmediate(coreConfig);
            Object.DestroyImmediate(stoneDef);
        }

        [Test]
        public void AttunementService_StonesPerAttempt_ReturnsCostConfigValue()
        {
            var stoneDef = CreateTestStackableItem("attunement_stone", "Attunement Stone");
            var costConfig = CreateTestCostConfig(stoneDef, 3);
            var coreConfig = CreateTestAttunementConfig();

            var service = new AttunementService(costConfig, coreConfig, null);

            Assert.AreEqual(3, service.StonesPerAttempt, "Should return cost config stones per attempt");

            Object.DestroyImmediate(costConfig);
            Object.DestroyImmediate(coreConfig);
            Object.DestroyImmediate(stoneDef);
        }

        [Test]
        public void AttunementAttemptPreview_CannotAttempt_InvalidEquipment()
        {
            var stoneDef = CreateTestStackableItem("attunement_stone", "Attunement Stone");
            var costConfig = CreateTestCostConfig(stoneDef);
            var coreConfig = CreateTestAttunementConfig();

            var service = new AttunementService(costConfig, coreConfig, null);

            var preview = service.PreviewAttempt(null);

            Assert.IsFalse(preview.CanAttempt, "Should not be able to attempt with null equipment");
            Assert.AreEqual(AttunementFailureReason.InvalidEquipment, preview.FailureReason);

            Object.DestroyImmediate(costConfig);
            Object.DestroyImmediate(coreConfig);
            Object.DestroyImmediate(stoneDef);
        }

        [Test]
        public void AttunementAttemptPreview_CannotAttempt_NoInventory()
        {
            var stoneDef = CreateTestStackableItem("attunement_stone", "Attunement Stone");
            var costConfig = CreateTestCostConfig(stoneDef);
            var coreConfig = CreateTestAttunementConfig();

            var service = new AttunementService(costConfig, coreConfig, null);
            var equipment = new EquipmentData("blade", "Test Blade", ItemRarity.Common, null, "Location", 100f, 0);

            var preview = service.PreviewAttempt(equipment);

            Assert.IsFalse(preview.CanAttempt, "Should not be able to attempt without inventory");
            Assert.AreEqual(AttunementFailureReason.MissingInventory, preview.FailureReason);

            Object.DestroyImmediate(costConfig);
            Object.DestroyImmediate(coreConfig);
            Object.DestroyImmediate(stoneDef);
        }

        [Test]
        public void AttunementAttemptPreview_CannotAttempt_AlreadyAtMaximum()
        {
            var stoneDef = CreateTestStackableItem("attunement_stone", "Attunement Stone");
            var costConfig = CreateTestCostConfig(stoneDef);
            var coreConfig = CreateTestAttunementConfig();
            var inventory = new Lootbound.Gameplay.Inventory.Inventory(20);

            // Add stones to inventory
            inventory.TryAddItem(new ItemInstance(stoneDef, 10));

            var service = new AttunementService(costConfig, coreConfig, inventory);
            var equipment = new EquipmentData("blade", "Test Blade", ItemRarity.Common, null, "Location", 100f, 10); // Max level

            var preview = service.PreviewAttempt(equipment);

            Assert.IsFalse(preview.CanAttempt, "Should not be able to attempt at max level");
            Assert.AreEqual(AttunementFailureReason.AlreadyAtMaximum, preview.FailureReason);

            Object.DestroyImmediate(costConfig);
            Object.DestroyImmediate(coreConfig);
            Object.DestroyImmediate(stoneDef);
        }

        [Test]
        public void AttunementAttemptPreview_CannotAttempt_NoStones()
        {
            var stoneDef = CreateTestStackableItem("attunement_stone", "Attunement Stone");
            var costConfig = CreateTestCostConfig(stoneDef);
            var coreConfig = CreateTestAttunementConfig();
            var inventory = new Lootbound.Gameplay.Inventory.Inventory(20);
            // No stones added

            var service = new AttunementService(costConfig, coreConfig, inventory);
            var equipment = new EquipmentData("blade", "Test Blade", ItemRarity.Common, null, "Location", 100f, 0);

            var preview = service.PreviewAttempt(equipment);

            Assert.IsFalse(preview.CanAttempt, "Should not be able to attempt without stones");
            Assert.AreEqual(AttunementFailureReason.NoAttunementStones, preview.FailureReason);

            Object.DestroyImmediate(costConfig);
            Object.DestroyImmediate(coreConfig);
            Object.DestroyImmediate(stoneDef);
        }

        [Test]
        public void AttunementAttemptPreview_CannotAttempt_InsufficientStones()
        {
            var stoneDef = CreateTestStackableItem("attunement_stone", "Attunement Stone");
            var costConfig = CreateTestCostConfig(stoneDef, 5); // Need 5 stones
            var coreConfig = CreateTestAttunementConfig();
            var inventory = new Lootbound.Gameplay.Inventory.Inventory(20);

            // Add only 2 stones
            inventory.TryAddItem(new ItemInstance(stoneDef, 2));

            var service = new AttunementService(costConfig, coreConfig, inventory);
            var equipment = new EquipmentData("blade", "Test Blade", ItemRarity.Common, null, "Location", 100f, 0);

            var preview = service.PreviewAttempt(equipment);

            Assert.IsFalse(preview.CanAttempt, "Should not be able to attempt with insufficient stones");
            Assert.AreEqual(AttunementFailureReason.InsufficientAttunementStones, preview.FailureReason);
            Assert.AreEqual(5, preview.RequiredStones);
            Assert.AreEqual(2, preview.AvailableStones);

            Object.DestroyImmediate(costConfig);
            Object.DestroyImmediate(coreConfig);
            Object.DestroyImmediate(stoneDef);
        }

        [Test]
        public void AttunementAttemptPreview_CanProceed_WhenValid()
        {
            var stoneDef = CreateTestStackableItem("attunement_stone", "Attunement Stone");
            var costConfig = CreateTestCostConfig(stoneDef);
            var coreConfig = CreateTestAttunementConfig();
            var inventory = new Lootbound.Gameplay.Inventory.Inventory(20);

            inventory.TryAddItem(new ItemInstance(stoneDef, 5));

            var service = new AttunementService(costConfig, coreConfig, inventory);
            var equipment = new EquipmentData("blade", "Test Blade", ItemRarity.Common, null, "Location", 100f, 3);

            var preview = service.PreviewAttempt(equipment);

            Assert.IsTrue(preview.CanAttempt, "Should be able to attempt with valid conditions");
            Assert.AreEqual(AttunementFailureReason.None, preview.FailureReason);
            Assert.AreEqual(3, preview.CurrentLevel);
            Assert.AreEqual(4, preview.ResultingLevelOnSuccess);
            Assert.AreEqual(10, preview.MaximumLevel);
            Assert.AreEqual(1, preview.RequiredStones);
            Assert.AreEqual(5, preview.AvailableStones);
            Assert.AreEqual(1f, preview.SuccessChance, 0.001f); // V1: Always 100%

            Object.DestroyImmediate(costConfig);
            Object.DestroyImmediate(coreConfig);
            Object.DestroyImmediate(stoneDef);
        }

        [Test]
        public void AttunementService_TryAttune_Success_ConsumesStones()
        {
            var stoneDef = CreateTestStackableItem("attunement_stone", "Attunement Stone");
            var costConfig = CreateTestCostConfig(stoneDef);
            var coreConfig = CreateTestAttunementConfig();
            var inventory = new Lootbound.Gameplay.Inventory.Inventory(20);

            inventory.TryAddItem(new ItemInstance(stoneDef, 5));
            Assert.AreEqual(5, inventory.GetItemCount(stoneDef), "Should have 5 stones initially");

            var service = new AttunementService(costConfig, coreConfig, inventory);
            var equipment = new EquipmentData("blade", "Test Blade", ItemRarity.Common, null, "Location", 100f, 0);

            var result = service.TryAttune(equipment);

            Assert.IsTrue(result.Success, "Attunement should succeed");
            Assert.AreEqual(0, result.PreviousLevel);
            Assert.AreEqual(1, result.CurrentLevel);
            Assert.AreEqual(1, result.StonesConsumed);
            Assert.AreEqual(4, inventory.GetItemCount(stoneDef), "Should have 4 stones after attunement");
            Assert.AreEqual(1, equipment.AttunementLevel, "Equipment level should be increased");

            Object.DestroyImmediate(costConfig);
            Object.DestroyImmediate(coreConfig);
            Object.DestroyImmediate(stoneDef);
        }

        [Test]
        public void AttunementService_TryAttune_Fails_WhenNoStones()
        {
            var stoneDef = CreateTestStackableItem("attunement_stone", "Attunement Stone");
            var costConfig = CreateTestCostConfig(stoneDef);
            var coreConfig = CreateTestAttunementConfig();
            var inventory = new Lootbound.Gameplay.Inventory.Inventory(20);
            // No stones

            var service = new AttunementService(costConfig, coreConfig, inventory);
            var equipment = new EquipmentData("blade", "Test Blade", ItemRarity.Common, null, "Location", 100f, 0);

            var result = service.TryAttune(equipment);

            Assert.IsFalse(result.Success, "Attunement should fail without stones");
            Assert.AreEqual(AttunementFailureReason.NoAttunementStones, result.FailureReason);
            Assert.AreEqual(0, result.StonesConsumed, "No stones should be consumed");
            Assert.AreEqual(0, equipment.AttunementLevel, "Equipment level should remain unchanged");

            Object.DestroyImmediate(costConfig);
            Object.DestroyImmediate(coreConfig);
            Object.DestroyImmediate(stoneDef);
        }

        [Test]
        public void AttunementService_TryAttune_Fails_WhenAtMaximum()
        {
            var stoneDef = CreateTestStackableItem("attunement_stone", "Attunement Stone");
            var costConfig = CreateTestCostConfig(stoneDef);
            var coreConfig = CreateTestAttunementConfig();
            var inventory = new Lootbound.Gameplay.Inventory.Inventory(20);

            inventory.TryAddItem(new ItemInstance(stoneDef, 10));

            var service = new AttunementService(costConfig, coreConfig, inventory);
            var equipment = new EquipmentData("blade", "Test Blade", ItemRarity.Common, null, "Location", 100f, 10);

            var result = service.TryAttune(equipment);

            Assert.IsFalse(result.Success, "Attunement should fail at max level");
            Assert.IsTrue(result.WasAtMaximum);
            Assert.AreEqual(0, result.StonesConsumed, "No stones should be consumed at max");
            Assert.AreEqual(10, inventory.GetItemCount(stoneDef), "Stones should remain unchanged");

            Object.DestroyImmediate(costConfig);
            Object.DestroyImmediate(coreConfig);
            Object.DestroyImmediate(stoneDef);
        }

        [Test]
        public void AttunementService_TryAttuneDebugWithoutCost_Success()
        {
            var stoneDef = CreateTestStackableItem("attunement_stone", "Attunement Stone");
            var costConfig = CreateTestCostConfig(stoneDef);
            var coreConfig = CreateTestAttunementConfig();
            var inventory = new Lootbound.Gameplay.Inventory.Inventory(20);
            // No stones needed for debug bypass

            var service = new AttunementService(costConfig, coreConfig, inventory);
            var equipment = new EquipmentData("blade", "Test Blade", ItemRarity.Common, null, "Location", 100f, 0);

            var result = service.TryAttuneDebugWithoutCost(equipment);

            Assert.IsTrue(result.Success, "Debug attunement should succeed without stones");
            Assert.AreEqual(0, result.StonesConsumed, "No stones should be consumed in debug mode");
            Assert.AreEqual(1, equipment.AttunementLevel);

            Object.DestroyImmediate(costConfig);
            Object.DestroyImmediate(coreConfig);
            Object.DestroyImmediate(stoneDef);
        }

        [Test]
        public void AttunementService_TryAttune_MultipleAttempts_ConsumesCorrectStones()
        {
            var stoneDef = CreateTestStackableItem("attunement_stone", "Attunement Stone");
            var costConfig = CreateTestCostConfig(stoneDef, 2); // 2 stones per attempt
            var coreConfig = CreateTestAttunementConfig();
            var inventory = new Lootbound.Gameplay.Inventory.Inventory(20);

            inventory.TryAddItem(new ItemInstance(stoneDef, 10));

            var service = new AttunementService(costConfig, coreConfig, inventory);
            var equipment = new EquipmentData("blade", "Test Blade", ItemRarity.Common, null, "Location", 100f, 0);

            // Attempt 3 attunements
            for (int i = 0; i < 3; i++)
            {
                var result = service.TryAttune(equipment);
                Assert.IsTrue(result.Success, $"Attempt {i + 1} should succeed");
                Assert.AreEqual(2, result.StonesConsumed, $"Attempt {i + 1} should consume 2 stones");
            }

            Assert.AreEqual(3, equipment.AttunementLevel);
            Assert.AreEqual(4, inventory.GetItemCount(stoneDef), "Should have 4 stones remaining (10 - 3*2)");

            Object.DestroyImmediate(costConfig);
            Object.DestroyImmediate(coreConfig);
            Object.DestroyImmediate(stoneDef);
        }

        [Test]
        public void AttunementAttemptResult_SucceededWithStones_HasCorrectProperties()
        {
            var result = AttunementAttemptResult.SucceededWithStones(3, 4, 10, 2, 2);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(3, result.PreviousLevel);
            Assert.AreEqual(4, result.CurrentLevel);
            Assert.AreEqual(10, result.MaximumLevel);
            Assert.AreEqual(2, result.StonesRequired);
            Assert.AreEqual(2, result.StonesConsumed);
            Assert.IsTrue(result.StonesWereConsumed);
            Assert.IsTrue(result.LevelIncreased);
        }

        [Test]
        public void AttunementAttemptResult_CannotAttempt_HasCorrectProperties()
        {
            var result = AttunementAttemptResult.CannotAttempt(5, 10, 1, AttunementFailureReason.NoAttunementStones);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(5, result.PreviousLevel);
            Assert.AreEqual(5, result.CurrentLevel);
            Assert.AreEqual(10, result.MaximumLevel);
            Assert.AreEqual(1, result.StonesRequired);
            Assert.AreEqual(0, result.StonesConsumed);
            Assert.IsFalse(result.StonesWereConsumed);
            Assert.AreEqual(AttunementFailureReason.NoAttunementStones, result.FailureReason);
        }

        [Test]
        public void AttunementFailureReason_HasExpectedValues()
        {
            Assert.AreEqual(0, (int)AttunementFailureReason.None);
            Assert.AreEqual(1, (int)AttunementFailureReason.InvalidEquipment);
            Assert.AreEqual(2, (int)AttunementFailureReason.MissingInventory);
            Assert.AreEqual(3, (int)AttunementFailureReason.MissingStoneDefinition);
            Assert.AreEqual(4, (int)AttunementFailureReason.NoAttunementStones);
            Assert.AreEqual(5, (int)AttunementFailureReason.InsufficientAttunementStones);
            Assert.AreEqual(6, (int)AttunementFailureReason.AlreadyAtMaximum);
        }

        #endregion

        #region Attunement Failure & Protection Tests (Slice 0.8.5)

        private AttunementChanceConfig CreateTestChanceConfig()
        {
            return ScriptableObject.CreateInstance<AttunementChanceConfig>();
        }

        [Test]
        public void AttunementChanceConfig_GetBaseChance_Level0_Returns100Percent()
        {
            var config = CreateTestChanceConfig();

            float chance = config.GetBaseChance(0);

            Assert.AreEqual(1f, chance, 0.001f, "Level 0 should have 100% base chance");

            Object.DestroyImmediate(config);
        }

        [Test]
        public void AttunementChanceConfig_GetBaseChance_Level9_Returns18Percent()
        {
            var config = CreateTestChanceConfig();

            float chance = config.GetBaseChance(9);

            Assert.AreEqual(0.18f, chance, 0.01f, "Level 9 should have 18% base chance");

            Object.DestroyImmediate(config);
        }

        [Test]
        public void AttunementChanceConfig_GetProtectionBonus_0Failures_ReturnsZero()
        {
            var config = CreateTestChanceConfig();

            float bonus = config.GetProtectionBonus(0);

            Assert.AreEqual(0f, bonus, 0.001f, "0 failures should have no protection bonus");

            Object.DestroyImmediate(config);
        }

        [Test]
        public void AttunementChanceConfig_GetProtectionBonus_3Failures_Returns15Percent()
        {
            var config = CreateTestChanceConfig();

            float bonus = config.GetProtectionBonus(3);

            Assert.AreEqual(0.15f, bonus, 0.001f, "3 failures should give +15% protection");

            Object.DestroyImmediate(config);
        }

        [Test]
        public void AttunementChanceConfig_GetProtectionBonus_10Failures_CapsAt30Percent()
        {
            var config = CreateTestChanceConfig();

            float bonus = config.GetProtectionBonus(10);

            Assert.AreEqual(0.30f, bonus, 0.001f, "Protection should cap at 30%");

            Object.DestroyImmediate(config);
        }

        [Test]
        public void AttunementChanceConfig_IsGuaranteed_5Failures_ReturnsFalse()
        {
            var config = CreateTestChanceConfig();

            bool isGuaranteed = config.IsGuaranteed(5);

            Assert.IsFalse(isGuaranteed, "5 failures should not be guaranteed");

            Object.DestroyImmediate(config);
        }

        [Test]
        public void AttunementChanceConfig_IsGuaranteed_6Failures_ReturnsTrue()
        {
            var config = CreateTestChanceConfig();

            bool isGuaranteed = config.IsGuaranteed(6);

            Assert.IsTrue(isGuaranteed, "6 failures should trigger guarantee");

            Object.DestroyImmediate(config);
        }

        [Test]
        public void AttunementChanceConfig_GetEffectiveChance_WithProtection_AddsBonus()
        {
            var config = CreateTestChanceConfig();

            // Level 5 base: 50%, 2 failures: +10% protection = 60%
            float effective = config.GetEffectiveChance(5, 2);

            Assert.AreEqual(0.60f, effective, 0.01f, "Effective chance should be base + protection");

            Object.DestroyImmediate(config);
        }

        [Test]
        public void AttunementChanceConfig_GetEffectiveChance_WhenGuaranteed_Returns100()
        {
            var config = CreateTestChanceConfig();

            // 6 failures should guarantee 100%
            float effective = config.GetEffectiveChance(9, 6);

            Assert.AreEqual(1f, effective, 0.001f, "Guaranteed should be 100%");

            Object.DestroyImmediate(config);
        }

        [Test]
        public void EquipmentData_ConsecutiveAttunementFailures_StartsAtZero()
        {
            var data = new EquipmentData("blade", "Test Blade", ItemRarity.Common, null, "Location");

            Assert.AreEqual(0, data.ConsecutiveAttunementFailures);
            Assert.IsFalse(data.HasAccumulatedResonance);
        }

        [Test]
        public void EquipmentData_IncrementAttunementFailures_IncrementsCount()
        {
            var data = new EquipmentData("blade", "Test Blade", ItemRarity.Common, null, "Location");

            data.IncrementAttunementFailures();
            Assert.AreEqual(1, data.ConsecutiveAttunementFailures);
            Assert.IsTrue(data.HasAccumulatedResonance);

            data.IncrementAttunementFailures();
            Assert.AreEqual(2, data.ConsecutiveAttunementFailures);
        }

        [Test]
        public void EquipmentData_ResetAttunementFailures_SetsToZero()
        {
            var data = new EquipmentData("blade", "Test Blade", ItemRarity.Common, null, "Location");
            data.IncrementAttunementFailures();
            data.IncrementAttunementFailures();

            data.ResetAttunementFailures();

            Assert.AreEqual(0, data.ConsecutiveAttunementFailures);
            Assert.IsFalse(data.HasAccumulatedResonance);
        }

        [Test]
        public void EquipmentData_SetAttunementFailures_SetsSpecificValue()
        {
            var data = new EquipmentData("blade", "Test Blade", ItemRarity.Common, null, "Location");

            data.SetAttunementFailures(5);

            Assert.AreEqual(5, data.ConsecutiveAttunementFailures);
        }

        [Test]
        public void EquipmentData_Clone_PreservesConsecutiveFailures()
        {
            var data = new EquipmentData("blade", "Test Blade", ItemRarity.Common, null, "Location");
            data.SetAttunementFailures(3);

            var clone = data.Clone();

            Assert.AreEqual(3, clone.ConsecutiveAttunementFailures);
        }

        [Test]
        public void EquipmentData_CloneWithNewId_ResetsConsecutiveFailures()
        {
            var data = new EquipmentData("blade", "Test Blade", ItemRarity.Common, null, "Location");
            data.SetAttunementFailures(3);

            var clone = data.CloneWithNewId();

            Assert.AreEqual(0, clone.ConsecutiveAttunementFailures);
        }

        [Test]
        public void DeterministicRandomSource_AlwaysSucceed_ReturnsSuccess()
        {
            var source = DeterministicRandomSource.AlwaysSucceed;

            Assert.IsTrue(source.Roll(0.5f));
            Assert.IsTrue(source.Roll(0.1f));
        }

        [Test]
        public void DeterministicRandomSource_AlwaysFail_ReturnsFailure()
        {
            var source = DeterministicRandomSource.AlwaysFail;

            Assert.IsFalse(source.Roll(0.5f));
            Assert.IsFalse(source.Roll(0.9f));
        }

        [Test]
        public void DeterministicRandomSource_Roll_WithGuaranteedChance_AlwaysSucceeds()
        {
            var source = DeterministicRandomSource.AlwaysFail;

            // Even "always fail" should succeed when chance is 100%
            Assert.IsTrue(source.Roll(1f));
        }

        [Test]
        public void SequenceRandomSource_ReturnsSequenceInOrder()
        {
            var source = new SequenceRandomSource(true, false, true);

            Assert.IsTrue(source.Roll(0.5f));
            Assert.IsFalse(source.Roll(0.5f));
            Assert.IsTrue(source.Roll(0.5f));
            // Wraps around
            Assert.IsTrue(source.Roll(0.5f));
        }

        [Test]
        public void AttunementService_WithChanceConfig_UsesChanceSystem()
        {
            var stoneDef = CreateTestStackableItem("attunement_stone", "Attunement Stone");
            var costConfig = CreateTestCostConfig(stoneDef);
            var coreConfig = CreateTestAttunementConfig();
            var chanceConfig = CreateTestChanceConfig();

            var service = new AttunementService(costConfig, coreConfig, null, chanceConfig);

            Assert.IsTrue(service.HasChanceSystem, "Service should have chance system when config provided");

            Object.DestroyImmediate(costConfig);
            Object.DestroyImmediate(coreConfig);
            Object.DestroyImmediate(chanceConfig);
            Object.DestroyImmediate(stoneDef);
        }

        [Test]
        public void AttunementService_WithoutChanceConfig_NoChanceSystem()
        {
            var stoneDef = CreateTestStackableItem("attunement_stone", "Attunement Stone");
            var costConfig = CreateTestCostConfig(stoneDef);
            var coreConfig = CreateTestAttunementConfig();

            var service = new AttunementService(costConfig, coreConfig, null, null);

            Assert.IsFalse(service.HasChanceSystem, "Service should not have chance system when no config");

            Object.DestroyImmediate(costConfig);
            Object.DestroyImmediate(coreConfig);
            Object.DestroyImmediate(stoneDef);
        }

        [Test]
        public void AttunementService_TryAttune_WithDeterministicFail_FailsAndConsumesStones()
        {
            var stoneDef = CreateTestStackableItem("attunement_stone", "Attunement Stone");
            var costConfig = CreateTestCostConfig(stoneDef);
            var coreConfig = CreateTestAttunementConfig();
            var chanceConfig = CreateTestChanceConfig();
            var inventory = new Lootbound.Gameplay.Inventory.Inventory(20);

            inventory.TryAddItem(new ItemInstance(stoneDef, 10));

            var service = new AttunementService(costConfig, coreConfig, inventory, chanceConfig, DeterministicRandomSource.AlwaysFail);
            var equipment = new EquipmentData("blade", "Test Blade", ItemRarity.Common, null, "Location", 100f, 5);

            var result = service.TryAttune(equipment);

            Assert.IsFalse(result.Success, "Should fail with AlwaysFail source");
            Assert.IsTrue(result.WasRngFailure, "Should be an RNG failure");
            Assert.AreEqual(1, result.StonesConsumed, "Stone should be consumed");
            Assert.AreEqual(5, equipment.AttunementLevel, "Level should not change");
            Assert.AreEqual(1, equipment.ConsecutiveAttunementFailures, "Failure count should increment");
            Assert.AreEqual(9, inventory.GetItemCount(stoneDef), "Stones should be reduced");

            Object.DestroyImmediate(costConfig);
            Object.DestroyImmediate(coreConfig);
            Object.DestroyImmediate(chanceConfig);
            Object.DestroyImmediate(stoneDef);
        }

        [Test]
        public void AttunementService_TryAttune_WithDeterministicSuccess_Succeeds()
        {
            var stoneDef = CreateTestStackableItem("attunement_stone", "Attunement Stone");
            var costConfig = CreateTestCostConfig(stoneDef);
            var coreConfig = CreateTestAttunementConfig();
            var chanceConfig = CreateTestChanceConfig();
            var inventory = new Lootbound.Gameplay.Inventory.Inventory(20);

            inventory.TryAddItem(new ItemInstance(stoneDef, 10));

            var service = new AttunementService(costConfig, coreConfig, inventory, chanceConfig, DeterministicRandomSource.AlwaysSucceed);
            var equipment = new EquipmentData("blade", "Test Blade", ItemRarity.Common, null, "Location", 100f, 5);

            var result = service.TryAttune(equipment);

            Assert.IsTrue(result.Success, "Should succeed with AlwaysSucceed source");
            Assert.AreEqual(6, equipment.AttunementLevel, "Level should increase");
            Assert.AreEqual(0, equipment.ConsecutiveAttunementFailures, "Failures should reset on success");

            Object.DestroyImmediate(costConfig);
            Object.DestroyImmediate(coreConfig);
            Object.DestroyImmediate(chanceConfig);
            Object.DestroyImmediate(stoneDef);
        }

        [Test]
        public void AttunementService_TryAttune_SuccessResetsFailureCount()
        {
            var stoneDef = CreateTestStackableItem("attunement_stone", "Attunement Stone");
            var costConfig = CreateTestCostConfig(stoneDef);
            var coreConfig = CreateTestAttunementConfig();
            var chanceConfig = CreateTestChanceConfig();
            var inventory = new Lootbound.Gameplay.Inventory.Inventory(20);

            inventory.TryAddItem(new ItemInstance(stoneDef, 10));

            var service = new AttunementService(costConfig, coreConfig, inventory, chanceConfig, DeterministicRandomSource.AlwaysSucceed);
            var equipment = new EquipmentData("blade", "Test Blade", ItemRarity.Common, null, "Location", 100f, 0);

            // Simulate some failures
            equipment.SetAttunementFailures(4);
            Assert.AreEqual(4, equipment.ConsecutiveAttunementFailures);

            var result = service.TryAttune(equipment);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(0, equipment.ConsecutiveAttunementFailures, "Success should reset failure count");

            Object.DestroyImmediate(costConfig);
            Object.DestroyImmediate(coreConfig);
            Object.DestroyImmediate(chanceConfig);
            Object.DestroyImmediate(stoneDef);
        }

        [Test]
        public void AttunementService_TryAttune_FailureIncrementsFailureCount()
        {
            var stoneDef = CreateTestStackableItem("attunement_stone", "Attunement Stone");
            var costConfig = CreateTestCostConfig(stoneDef);
            var coreConfig = CreateTestAttunementConfig();
            var chanceConfig = CreateTestChanceConfig();
            var inventory = new Lootbound.Gameplay.Inventory.Inventory(20);

            inventory.TryAddItem(new ItemInstance(stoneDef, 10));

            var service = new AttunementService(costConfig, coreConfig, inventory, chanceConfig, DeterministicRandomSource.AlwaysFail);
            var equipment = new EquipmentData("blade", "Test Blade", ItemRarity.Common, null, "Location", 100f, 5);

            // First failure
            service.TryAttune(equipment);
            Assert.AreEqual(1, equipment.ConsecutiveAttunementFailures);

            // Second failure
            service.TryAttune(equipment);
            Assert.AreEqual(2, equipment.ConsecutiveAttunementFailures);

            // Third failure
            service.TryAttune(equipment);
            Assert.AreEqual(3, equipment.ConsecutiveAttunementFailures);

            Object.DestroyImmediate(costConfig);
            Object.DestroyImmediate(coreConfig);
            Object.DestroyImmediate(chanceConfig);
            Object.DestroyImmediate(stoneDef);
        }

        [Test]
        public void AttunementService_TryAttune_GuaranteeAfter6Failures()
        {
            var stoneDef = CreateTestStackableItem("attunement_stone", "Attunement Stone");
            var costConfig = CreateTestCostConfig(stoneDef);
            var coreConfig = CreateTestAttunementConfig();
            var chanceConfig = CreateTestChanceConfig();
            var inventory = new Lootbound.Gameplay.Inventory.Inventory(20);

            inventory.TryAddItem(new ItemInstance(stoneDef, 10));

            // Use AlwaysFail, but with 6 failures the guarantee should kick in
            var service = new AttunementService(costConfig, coreConfig, inventory, chanceConfig, DeterministicRandomSource.AlwaysFail);
            var equipment = new EquipmentData("blade", "Test Blade", ItemRarity.Common, null, "Location", 100f, 5);

            // Set 6 failures (guarantee threshold)
            equipment.SetAttunementFailures(6);

            var result = service.TryAttune(equipment);

            Assert.IsTrue(result.Success, "Should succeed due to guarantee after 6 failures");
            Assert.AreEqual(6, equipment.AttunementLevel, "Level should increase");
            Assert.AreEqual(0, equipment.ConsecutiveAttunementFailures, "Failures should reset");

            Object.DestroyImmediate(costConfig);
            Object.DestroyImmediate(coreConfig);
            Object.DestroyImmediate(chanceConfig);
            Object.DestroyImmediate(stoneDef);
        }

        [Test]
        public void AttunementService_ForceNextOutcome_ForcesSuccess()
        {
            var stoneDef = CreateTestStackableItem("attunement_stone", "Attunement Stone");
            var costConfig = CreateTestCostConfig(stoneDef);
            var coreConfig = CreateTestAttunementConfig();
            var chanceConfig = CreateTestChanceConfig();
            var inventory = new Lootbound.Gameplay.Inventory.Inventory(20);

            inventory.TryAddItem(new ItemInstance(stoneDef, 10));

            var service = new AttunementService(costConfig, coreConfig, inventory, chanceConfig, DeterministicRandomSource.AlwaysFail);
            var equipment = new EquipmentData("blade", "Test Blade", ItemRarity.Common, null, "Location", 100f, 5);

            // Force success
            service.ForceNextOutcome(true);
            var result = service.TryAttune(equipment);

            Assert.IsTrue(result.Success, "Forced success should override AlwaysFail");
            Assert.AreEqual(6, equipment.AttunementLevel);

            Object.DestroyImmediate(costConfig);
            Object.DestroyImmediate(coreConfig);
            Object.DestroyImmediate(chanceConfig);
            Object.DestroyImmediate(stoneDef);
        }

        [Test]
        public void AttunementService_ForceNextOutcome_ForcesFailure()
        {
            var stoneDef = CreateTestStackableItem("attunement_stone", "Attunement Stone");
            var costConfig = CreateTestCostConfig(stoneDef);
            var coreConfig = CreateTestAttunementConfig();
            var chanceConfig = CreateTestChanceConfig();
            var inventory = new Lootbound.Gameplay.Inventory.Inventory(20);

            inventory.TryAddItem(new ItemInstance(stoneDef, 10));

            var service = new AttunementService(costConfig, coreConfig, inventory, chanceConfig, DeterministicRandomSource.AlwaysSucceed);
            var equipment = new EquipmentData("blade", "Test Blade", ItemRarity.Common, null, "Location", 100f, 0);

            // Level 0 has 100% chance, but force failure
            service.ForceNextOutcome(false);
            var result = service.TryAttune(equipment);

            Assert.IsFalse(result.Success, "Forced failure should override even 100% chance");
            Assert.AreEqual(0, equipment.AttunementLevel);

            Object.DestroyImmediate(costConfig);
            Object.DestroyImmediate(coreConfig);
            Object.DestroyImmediate(chanceConfig);
            Object.DestroyImmediate(stoneDef);
        }

        [Test]
        public void AttunementService_ForceNextOutcome_OnlyAffectsOneAttempt()
        {
            var stoneDef = CreateTestStackableItem("attunement_stone", "Attunement Stone");
            var costConfig = CreateTestCostConfig(stoneDef);
            var coreConfig = CreateTestAttunementConfig();
            var chanceConfig = CreateTestChanceConfig();
            var inventory = new Lootbound.Gameplay.Inventory.Inventory(20);

            inventory.TryAddItem(new ItemInstance(stoneDef, 10));

            var service = new AttunementService(costConfig, coreConfig, inventory, chanceConfig, DeterministicRandomSource.AlwaysFail);
            var equipment = new EquipmentData("blade", "Test Blade", ItemRarity.Common, null, "Location", 100f, 5);

            // Force success for first attempt
            service.ForceNextOutcome(true);
            var result1 = service.TryAttune(equipment);
            Assert.IsTrue(result1.Success, "First attempt should be forced success");

            // Second attempt should use normal random (AlwaysFail)
            var result2 = service.TryAttune(equipment);
            Assert.IsFalse(result2.Success, "Second attempt should NOT be forced");

            Object.DestroyImmediate(costConfig);
            Object.DestroyImmediate(coreConfig);
            Object.DestroyImmediate(chanceConfig);
            Object.DestroyImmediate(stoneDef);
        }

        [Test]
        public void AttunementAttemptPreview_WithChanceConfig_ShowsProtectionInfo()
        {
            var stoneDef = CreateTestStackableItem("attunement_stone", "Attunement Stone");
            var costConfig = CreateTestCostConfig(stoneDef);
            var coreConfig = CreateTestAttunementConfig();
            var chanceConfig = CreateTestChanceConfig();
            var inventory = new Lootbound.Gameplay.Inventory.Inventory(20);

            inventory.TryAddItem(new ItemInstance(stoneDef, 10));

            var service = new AttunementService(costConfig, coreConfig, inventory, chanceConfig);
            var equipment = new EquipmentData("blade", "Test Blade", ItemRarity.Common, null, "Location", 100f, 5);
            equipment.SetAttunementFailures(3); // +15% protection

            var preview = service.PreviewAttempt(equipment);

            Assert.IsTrue(preview.CanAttempt);
            Assert.AreEqual(0.50f, preview.BaseChance, 0.01f, "Base chance for level 5");
            Assert.AreEqual(0.15f, preview.ProtectionBonus, 0.01f, "Protection from 3 failures");
            Assert.AreEqual(0.65f, preview.SuccessChance, 0.01f, "Effective chance");
            Assert.AreEqual(3, preview.ConsecutiveFailures);
            Assert.IsTrue(preview.HasProtection);
            Assert.IsFalse(preview.IsGuaranteed);

            Object.DestroyImmediate(costConfig);
            Object.DestroyImmediate(coreConfig);
            Object.DestroyImmediate(chanceConfig);
            Object.DestroyImmediate(stoneDef);
        }

        [Test]
        public void AttunementAttemptPreview_WithGuarantee_ShowsGuaranteed()
        {
            var stoneDef = CreateTestStackableItem("attunement_stone", "Attunement Stone");
            var costConfig = CreateTestCostConfig(stoneDef);
            var coreConfig = CreateTestAttunementConfig();
            var chanceConfig = CreateTestChanceConfig();
            var inventory = new Lootbound.Gameplay.Inventory.Inventory(20);

            inventory.TryAddItem(new ItemInstance(stoneDef, 10));

            var service = new AttunementService(costConfig, coreConfig, inventory, chanceConfig);
            var equipment = new EquipmentData("blade", "Test Blade", ItemRarity.Common, null, "Location", 100f, 5);
            equipment.SetAttunementFailures(6); // Guarantee threshold

            var preview = service.PreviewAttempt(equipment);

            Assert.IsTrue(preview.CanAttempt);
            Assert.AreEqual(1f, preview.SuccessChance, 0.001f, "Guaranteed should be 100%");
            Assert.IsTrue(preview.IsGuaranteed);

            Object.DestroyImmediate(costConfig);
            Object.DestroyImmediate(coreConfig);
            Object.DestroyImmediate(chanceConfig);
            Object.DestroyImmediate(stoneDef);
        }

        [Test]
        public void AttunementAttemptResult_FailedWithStones_HasCorrectProperties()
        {
            var result = AttunementAttemptResult.FailedWithStones(5, 10, 1, 1, 0.50f, 0.05f);

            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.WasRngFailure);
            Assert.AreEqual(5, result.PreviousLevel);
            Assert.AreEqual(5, result.CurrentLevel);
            Assert.AreEqual(1, result.StonesConsumed);
            Assert.IsTrue(result.StonesWereConsumed);
            Assert.AreEqual(0.50f, result.AttemptedChance, 0.001f);
            Assert.AreEqual(0.05f, result.ProtectionGained, 0.001f);
            Assert.IsFalse(result.LevelIncreased);
        }

        [Test]
        public void Attunement_ProtectionPreservedThroughRepair()
        {
            var data = new EquipmentData("blade", "Test Blade", ItemRarity.Common, null, "Location", 100f, 5);
            data.SetAttunementFailures(4);

            // Break the weapon
            data.SetDurability(0f);
            Assert.AreEqual(4, data.ConsecutiveAttunementFailures, "Protection should be preserved when broken");

            // Repair the weapon
            data.RestoreDurability(100f);
            Assert.AreEqual(4, data.ConsecutiveAttunementFailures, "Protection should be preserved after repair");
        }

        [Test]
        public void Attunement_ProtectionPreservedThroughConditionChange()
        {
            var data = new EquipmentData("blade", "Test Blade", ItemRarity.Common, null, "Location", 100f, 5);
            data.SetAttunementFailures(3);

            // Cycle through conditions
            data.SetDurability(75f);
            Assert.AreEqual(3, data.ConsecutiveAttunementFailures);

            data.SetDurability(40f);
            Assert.AreEqual(3, data.ConsecutiveAttunementFailures);

            data.SetDurability(15f);
            Assert.AreEqual(3, data.ConsecutiveAttunementFailures);

            data.SetDurability(0f);
            Assert.AreEqual(3, data.ConsecutiveAttunementFailures);

            data.SetDurability(100f);
            Assert.AreEqual(3, data.ConsecutiveAttunementFailures);
        }

        #endregion

        #region Attunement History Tests (Slice 0.8.6)

        [Test]
        public void AttunementHistory_InitialState_AllZeros()
        {
            var history = new AttunementHistory();

            Assert.AreEqual(0, history.TotalAttempts);
            Assert.AreEqual(0, history.SuccessfulAttempts);
            Assert.AreEqual(0, history.FailedAttempts);
            Assert.AreEqual(0, history.TotalStonesConsumed);
            Assert.AreEqual(0, history.HighestAttunementLevelReached);
            Assert.AreEqual(0, history.LongestFailureStreak);
            Assert.AreEqual(0, history.LastAttemptTimestamp);
            Assert.IsFalse(history.HasAttemptHistory);
        }

        [Test]
        public void AttunementHistory_RecordSuccess_CountersIncremented()
        {
            var history = new AttunementHistory();
            var result = AttunementAttemptResult.SucceededWithStones(0, 1, 10, 1, 1, 1f);

            history.RecordAttempt(result, "Test Location", 0, false);

            Assert.AreEqual(1, history.TotalAttempts);
            Assert.AreEqual(1, history.SuccessfulAttempts);
            Assert.AreEqual(0, history.FailedAttempts);
            Assert.AreEqual(1, history.TotalStonesConsumed);
            Assert.IsTrue(history.HasAttemptHistory);
            Assert.IsTrue(history.LastAttemptSucceeded);
        }

        [Test]
        public void AttunementHistory_RecordFailure_CountersIncremented()
        {
            var history = new AttunementHistory();
            var result = AttunementAttemptResult.FailedWithStones(1, 10, 1, 1, 0.9f, 0.05f);

            history.RecordAttempt(result, "Test Location", 1, false);

            Assert.AreEqual(1, history.TotalAttempts);
            Assert.AreEqual(0, history.SuccessfulAttempts);
            Assert.AreEqual(1, history.FailedAttempts);
            Assert.AreEqual(1, history.TotalStonesConsumed);
            Assert.IsFalse(history.LastAttemptSucceeded);
        }

        [Test]
        public void AttunementHistory_TechnicalRefusal_NotCounted()
        {
            var history = new AttunementHistory();
            var result = AttunementAttemptResult.CannotAttempt(0, 10, 1, AttunementFailureReason.NoAttunementStones);

            history.RecordAttempt(result, "Test Location", 0, false);

            Assert.AreEqual(0, history.TotalAttempts);
            Assert.IsFalse(history.HasAttemptHistory);
        }

        [Test]
        public void AttunementHistory_AlreadyMaximum_NotCounted()
        {
            var history = new AttunementHistory();
            var result = AttunementAttemptResult.AlreadyMaximum(10, 10);

            history.RecordAttempt(result, "Test Location", 0, false);

            Assert.AreEqual(0, history.TotalAttempts);
        }

        [Test]
        public void AttunementHistory_HighestLevel_UpdatedOnSuccess()
        {
            var history = new AttunementHistory();

            var result1 = AttunementAttemptResult.SucceededWithStones(0, 1, 10, 1, 1, 1f);
            history.RecordAttempt(result1, "Test", 0, false);
            Assert.AreEqual(1, history.HighestAttunementLevelReached);

            var result2 = AttunementAttemptResult.SucceededWithStones(1, 2, 10, 1, 1, 0.9f);
            history.RecordAttempt(result2, "Test", 0, false);
            Assert.AreEqual(2, history.HighestAttunementLevelReached);

            var result3 = AttunementAttemptResult.SucceededWithStones(2, 3, 10, 1, 1, 0.8f);
            history.RecordAttempt(result3, "Test", 0, false);
            Assert.AreEqual(3, history.HighestAttunementLevelReached);
        }

        [Test]
        public void AttunementHistory_HighestLevel_NeverDecreases()
        {
            var history = new AttunementHistory();

            // Simulate reaching +5
            history.EnsureHighestLevel(5);
            Assert.AreEqual(5, history.HighestAttunementLevelReached);

            // Even if we try to set lower, it shouldn't decrease
            history.EnsureHighestLevel(3);
            Assert.AreEqual(5, history.HighestAttunementLevelReached);
        }

        [Test]
        public void AttunementHistory_LongestFailureStreak_Updated()
        {
            var history = new AttunementHistory();

            // First failure - streak 1
            var fail1 = AttunementAttemptResult.FailedWithStones(0, 10, 1, 1, 1f, 0.05f);
            history.RecordAttempt(fail1, "Test", 1, false);
            Assert.AreEqual(1, history.LongestFailureStreak);

            // Second failure - streak 2
            var fail2 = AttunementAttemptResult.FailedWithStones(0, 10, 1, 1, 1f, 0.05f);
            history.RecordAttempt(fail2, "Test", 2, false);
            Assert.AreEqual(2, history.LongestFailureStreak);

            // Third failure - streak 3
            var fail3 = AttunementAttemptResult.FailedWithStones(0, 10, 1, 1, 1f, 0.05f);
            history.RecordAttempt(fail3, "Test", 3, false);
            Assert.AreEqual(3, history.LongestFailureStreak);
        }

        [Test]
        public void AttunementHistory_LongestStreak_PreservedAfterSuccess()
        {
            var history = new AttunementHistory();

            // Build up streak of 3
            for (int i = 1; i <= 3; i++)
            {
                var fail = AttunementAttemptResult.FailedWithStones(0, 10, 1, 1, 1f, 0.05f);
                history.RecordAttempt(fail, "Test", i, false);
            }
            Assert.AreEqual(3, history.LongestFailureStreak);

            // Success resets current streak (passed as 0), but longest should remain
            var success = AttunementAttemptResult.SucceededWithStones(0, 1, 10, 1, 1, 1f);
            history.RecordAttempt(success, "Test", 0, false);
            Assert.AreEqual(3, history.LongestFailureStreak);
        }

        [Test]
        public void AttunementHistory_LastAttemptDetails_Recorded()
        {
            var history = new AttunementHistory();
            var result = AttunementAttemptResult.SucceededWithStones(4, 5, 10, 1, 1, 0.7f);

            history.RecordAttempt(result, "Refuge Attunement Table", 0, false);

            Assert.AreEqual("Refuge Attunement Table", history.LastAttemptLocation);
            Assert.AreEqual(4, history.LastAttemptPreviousLevel);
            Assert.AreEqual(5, history.LastAttemptResultingLevel);
            Assert.AreEqual(0.7f, history.LastAttemptEffectiveChance, 0.001f);
            Assert.IsTrue(history.LastAttemptSucceeded);
            Assert.IsFalse(history.LastAttemptWasGuaranteed);
        }

        [Test]
        public void AttunementHistory_GuaranteedSuccess_Tracked()
        {
            var history = new AttunementHistory();
            var result = AttunementAttemptResult.SucceededWithStones(5, 6, 10, 1, 1, 1f);

            history.RecordAttempt(result, "Test", 0, wasGuaranteed: true);

            Assert.IsTrue(history.LastAttemptSucceeded);
            Assert.IsTrue(history.LastAttemptWasGuaranteed);
            Assert.AreEqual(1, history.SuccessfulAttempts);
        }

        [Test]
        public void AttunementHistory_StonesConsumed_AccumulatedCorrectly()
        {
            var history = new AttunementHistory();

            // First attempt: 1 stone
            var result1 = AttunementAttemptResult.SucceededWithStones(0, 1, 10, 1, 1, 1f);
            history.RecordAttempt(result1, "Test", 0, false);
            Assert.AreEqual(1, history.TotalStonesConsumed);

            // Second attempt: 2 stones
            var result2 = AttunementAttemptResult.SucceededWithStones(1, 2, 10, 2, 2, 0.9f);
            history.RecordAttempt(result2, "Test", 0, false);
            Assert.AreEqual(3, history.TotalStonesConsumed);

            // Failure: 1 stone
            var result3 = AttunementAttemptResult.FailedWithStones(2, 10, 1, 1, 0.8f, 0.05f);
            history.RecordAttempt(result3, "Test", 1, false);
            Assert.AreEqual(4, history.TotalStonesConsumed);
        }

        [Test]
        public void AttunementHistory_Clone_PreservesAllData()
        {
            var history = new AttunementHistory();

            // Add some data
            var success = AttunementAttemptResult.SucceededWithStones(0, 1, 10, 1, 1, 1f);
            history.RecordAttempt(success, "Location A", 0, false);

            var fail = AttunementAttemptResult.FailedWithStones(1, 10, 1, 1, 0.9f, 0.05f);
            history.RecordAttempt(fail, "Location B", 1, false);

            // Clone
            var clone = history.Clone();

            // Verify all data preserved
            Assert.AreEqual(history.TotalAttempts, clone.TotalAttempts);
            Assert.AreEqual(history.SuccessfulAttempts, clone.SuccessfulAttempts);
            Assert.AreEqual(history.FailedAttempts, clone.FailedAttempts);
            Assert.AreEqual(history.TotalStonesConsumed, clone.TotalStonesConsumed);
            Assert.AreEqual(history.HighestAttunementLevelReached, clone.HighestAttunementLevelReached);
            Assert.AreEqual(history.LongestFailureStreak, clone.LongestFailureStreak);
            Assert.AreEqual(history.LastAttemptLocation, clone.LastAttemptLocation);
            Assert.AreEqual(history.LastAttemptSucceeded, clone.LastAttemptSucceeded);
        }

        [Test]
        public void AttunementHistory_Clone_IsIndependent()
        {
            var history = new AttunementHistory();
            var success = AttunementAttemptResult.SucceededWithStones(0, 1, 10, 1, 1, 1f);
            history.RecordAttempt(success, "Location", 0, false);

            var clone = history.Clone();

            // Modify original
            var success2 = AttunementAttemptResult.SucceededWithStones(1, 2, 10, 1, 1, 0.9f);
            history.RecordAttempt(success2, "New Location", 0, false);

            // Clone should be unchanged
            Assert.AreEqual(1, clone.TotalAttempts);
            Assert.AreEqual("Location", clone.LastAttemptLocation);
        }

        [Test]
        public void AttunementHistory_Reset_ClearsAllData()
        {
            var history = new AttunementHistory();

            // Add some data
            var success = AttunementAttemptResult.SucceededWithStones(0, 1, 10, 1, 1, 1f);
            history.RecordAttempt(success, "Location", 0, false);
            history.EnsureHighestLevel(5);

            // Reset
            history.Reset();

            // Verify all cleared
            Assert.AreEqual(0, history.TotalAttempts);
            Assert.AreEqual(0, history.SuccessfulAttempts);
            Assert.AreEqual(0, history.FailedAttempts);
            Assert.AreEqual(0, history.TotalStonesConsumed);
            Assert.AreEqual(0, history.HighestAttunementLevelReached);
            Assert.AreEqual(0, history.LongestFailureStreak);
            Assert.IsFalse(history.HasAttemptHistory);
        }

        [Test]
        public void AttunementHistory_Timestamps_Recorded()
        {
            var history = new AttunementHistory();

            var success = AttunementAttemptResult.SucceededWithStones(0, 1, 10, 1, 1, 1f);
            history.RecordAttempt(success, "Location", 0, false);

            Assert.Greater(history.LastAttemptTimestamp, 0);
            Assert.Greater(history.LastSuccessTimestamp, 0);
            Assert.AreEqual(0, history.LastFailureTimestamp);
        }

        [Test]
        public void AttunementHistory_FailureTimestamp_Recorded()
        {
            var history = new AttunementHistory();

            var fail = AttunementAttemptResult.FailedWithStones(0, 10, 1, 1, 1f, 0.05f);
            history.RecordAttempt(fail, "Location", 1, false);

            Assert.Greater(history.LastAttemptTimestamp, 0);
            Assert.AreEqual(0, history.LastSuccessTimestamp);
            Assert.Greater(history.LastFailureTimestamp, 0);
        }

        [Test]
        public void AttunementHistory_GetSummary_NoAttempts()
        {
            var history = new AttunementHistory();
            string summary = history.GetSummary();

            Assert.IsTrue(summary.Contains("never"));
        }

        [Test]
        public void AttunementHistory_GetSummary_WithAttempts()
        {
            var history = new AttunementHistory();

            var success = AttunementAttemptResult.SucceededWithStones(0, 1, 10, 1, 1, 1f);
            history.RecordAttempt(success, "Location", 0, false);

            var fail = AttunementAttemptResult.FailedWithStones(1, 10, 1, 1, 0.9f, 0.05f);
            history.RecordAttempt(fail, "Location", 1, false);

            string summary = history.GetSummary();

            Assert.IsTrue(summary.Contains("2"));
            Assert.IsTrue(summary.Contains("1 success"));
            Assert.IsTrue(summary.Contains("1 failure"));
        }

        [Test]
        public void EquipmentHistory_AttunementProperty_LazilyInitialized()
        {
            var history = new EquipmentHistory("Test Location");

            // Access attunement property
            var attunement = history.Attunement;

            Assert.IsNotNull(attunement);
            Assert.AreEqual(0, attunement.TotalAttempts);
        }

        [Test]
        public void EquipmentHistory_Clone_IncludesAttunementHistory()
        {
            var history = new EquipmentHistory("Test Location");

            // Add some attunement history
            var success = AttunementAttemptResult.SucceededWithStones(0, 1, 10, 1, 1, 1f);
            history.Attunement.RecordAttempt(success, "Table", 0, false);

            // Clone
            var clone = history.Clone();

            // Verify attunement history is cloned
            Assert.AreEqual(1, clone.Attunement.TotalAttempts);
            Assert.AreEqual("Table", clone.Attunement.LastAttemptLocation);
        }

        [Test]
        public void EquipmentData_Clone_PreservesAttunementHistory()
        {
            var data = new EquipmentData("blade", "Test Blade", ItemRarity.Common, null, "Location");

            // Add some attunement history
            var success = AttunementAttemptResult.SucceededWithStones(0, 1, 10, 1, 1, 1f);
            data.History.Attunement.RecordAttempt(success, "Table", 0, false);

            // Clone
            var clone = data.Clone();

            // Verify attunement history is cloned
            Assert.AreEqual(1, clone.History.Attunement.TotalAttempts);
        }

        [Test]
        public void AttunementAttemptResult_WasAttemptResolved_TrueForSuccess()
        {
            var success = AttunementAttemptResult.SucceededWithStones(0, 1, 10, 1, 1, 1f);
            Assert.IsTrue(success.WasAttemptResolved);
        }

        [Test]
        public void AttunementAttemptResult_WasAttemptResolved_TrueForRngFailure()
        {
            var fail = AttunementAttemptResult.FailedWithStones(0, 10, 1, 1, 0.9f, 0.05f);
            Assert.IsTrue(fail.WasAttemptResolved);
        }

        [Test]
        public void AttunementAttemptResult_WasAttemptResolved_FalseForTechnicalRefusal()
        {
            var noStones = AttunementAttemptResult.CannotAttempt(0, 10, 1, AttunementFailureReason.NoAttunementStones);
            Assert.IsFalse(noStones.WasAttemptResolved);

            var alreadyMax = AttunementAttemptResult.AlreadyMaximum(10, 10);
            Assert.IsFalse(alreadyMax.WasAttemptResolved);
        }

        #endregion

        [TearDown]
        public void TearDown()
        {
            // Clean up created ScriptableObjects
        }
    }
}

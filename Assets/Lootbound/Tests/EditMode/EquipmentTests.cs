using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;
using Lootbound.Gameplay.Equipment;
using Lootbound.Gameplay.Inventory;

namespace Lootbound.Tests.EditMode
{
    /// <summary>
    /// EditMode tests for the equipment system.
    /// Tests equipment data, affixes, stat resolution, and inventory integration.
    /// </summary>
    public class EquipmentTests
    {
        #region Test Helpers

        private WeaponDefinition CreateTestWeapon(
            string id,
            float damage = 25f,
            float attackSpeed = 1.0f,
            float range = 2.0f,
            float stagger = 0.3f)
        {
            var weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
            var type = typeof(ItemDefinition);
            type.GetField("itemId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(weapon, id);
            type.GetField("displayName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(weapon, id);
            type.GetField("isStackable", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(weapon, false);
            type.GetField("maxStackSize", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(weapon, 1);

            var weaponType = typeof(WeaponDefinition);
            weaponType.GetField("baseDamage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(weapon, damage);
            weaponType.GetField("baseAttackSpeed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(weapon, attackSpeed);
            weaponType.GetField("baseRange", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(weapon, range);
            weaponType.GetField("baseStagger", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(weapon, stagger);

            return weapon;
        }

        private AffixDefinition CreateTestAffix(
            string id,
            AffixModifierType modType,
            float minValue = 5f,
            float maxValue = 15f,
            bool isNegative = false,
            AffixTier tier = AffixTier.Minor)
        {
            var affix = ScriptableObject.CreateInstance<AffixDefinition>();
            var type = typeof(AffixDefinition);

            type.GetField("affixId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(affix, id);
            type.GetField("displayName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(affix, id);
            type.GetField("modifierType", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(affix, modType);
            type.GetField("minValue", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(affix, minValue);
            type.GetField("maxValue", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(affix, maxValue);
            type.GetField("isNegative", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(affix, isNegative);
            type.GetField("tier", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(affix, tier);

            return affix;
        }

        private EquipmentRegistry CreateTestRegistry(
            List<WeaponDefinition> weapons = null,
            List<AffixDefinition> affixes = null)
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

        #endregion

        #region EquipmentData Tests

        [Test]
        public void EquipmentData_Creation_GeneratesUniqueGuid()
        {
            var data1 = new EquipmentData("weapon1", "Test Blade", ItemRarity.Common, null, "TestLocation");
            var data2 = new EquipmentData("weapon1", "Test Blade", ItemRarity.Common, null, "TestLocation");

            Assert.IsNotNull(data1.InstanceId);
            Assert.IsNotNull(data2.InstanceId);
            Assert.AreNotEqual(data1.InstanceId, data2.InstanceId);
            Assert.AreEqual(36, data1.InstanceId.Length); // GUID format
        }

        [Test]
        public void EquipmentData_Creation_SetsAllProperties()
        {
            var affixes = new List<AffixInstance>
            {
                new AffixInstance("sharp", 10f)
            };

            var data = new EquipmentData("weapon_blade", "Sharp Blade", ItemRarity.Uncommon, affixes, "Dungeon");

            Assert.AreEqual("weapon_blade", data.DefinitionId);
            Assert.AreEqual("Sharp Blade", data.CustomName);
            Assert.AreEqual(ItemRarity.Uncommon, data.Rarity);
            Assert.AreEqual(1, data.Affixes.Count);
            Assert.AreEqual("Dungeon", data.History.FoundLocation);
            Assert.IsFalse(data.IsEquipped);
        }

        [Test]
        public void EquipmentData_Clone_PreservesGuid()
        {
            var original = new EquipmentData("weapon1", "Test Blade", ItemRarity.Rare, null, "TestLocation");
            var clone = original.Clone();

            Assert.AreEqual(original.InstanceId, clone.InstanceId);
            Assert.AreEqual(original.DefinitionId, clone.DefinitionId);
            Assert.AreEqual(original.CustomName, clone.CustomName);
            Assert.AreEqual(original.Rarity, clone.Rarity);
        }

        [Test]
        public void EquipmentData_CloneWithNewId_GeneratesNewGuid()
        {
            var original = new EquipmentData("weapon1", "Test Blade", ItemRarity.Rare, null, "TestLocation");
            var clone = original.CloneWithNewId();

            Assert.AreNotEqual(original.InstanceId, clone.InstanceId);
            Assert.AreEqual(original.DefinitionId, clone.DefinitionId);
            Assert.AreEqual(original.CustomName, clone.CustomName);
        }

        [Test]
        public void EquipmentData_IsValid_RequiresInstanceIdAndDefinitionId()
        {
            var valid = new EquipmentData("weapon1", "Test", ItemRarity.Common, null, "Location");
            Assert.IsTrue(valid.IsValid);

            // Create invalid data using the second constructor with empty values
            var invalid = new EquipmentData("", "", "Test", ItemRarity.Common, null, null);
            Assert.IsFalse(invalid.IsValid);
        }

        #endregion

        #region EquipmentHistory Tests

        [Test]
        public void EquipmentHistory_Creation_SetsFoundLocation()
        {
            var history = new EquipmentHistory("Dark Cave");

            Assert.AreEqual("Dark Cave", history.FoundLocation);
            Assert.AreEqual(0, history.EnemiesDefeated);
            Assert.AreEqual(0, history.TimesEquipped);
            Assert.Greater(history.FoundTimestamp, 0);
        }

        [Test]
        public void EquipmentHistory_RecordKill_IncrementsCounter()
        {
            var history = new EquipmentHistory("TestLocation");

            history.RecordKill();
            Assert.AreEqual(1, history.EnemiesDefeated);

            history.RecordKill();
            Assert.AreEqual(2, history.EnemiesDefeated);
        }

        [Test]
        public void EquipmentHistory_RecordEquip_IncrementsCounter()
        {
            var history = new EquipmentHistory("TestLocation");

            history.RecordEquip();
            Assert.AreEqual(1, history.TimesEquipped);

            history.RecordEquip();
            Assert.AreEqual(2, history.TimesEquipped);
        }

        [Test]
        public void EquipmentHistory_Clone_CopiesAllData()
        {
            var original = new EquipmentHistory("TestLocation");
            original.RecordKill();
            original.RecordKill();
            original.RecordEquip();

            var clone = original.Clone();

            Assert.AreEqual(original.FoundLocation, clone.FoundLocation);
            Assert.AreEqual(original.FoundTimestamp, clone.FoundTimestamp);
            Assert.AreEqual(original.EnemiesDefeated, clone.EnemiesDefeated);
            Assert.AreEqual(original.TimesEquipped, clone.TimesEquipped);
        }

        #endregion

        #region AffixInstance Tests

        [Test]
        public void AffixInstance_Creation_SetsProperties()
        {
            var affix = new AffixInstance("sharp", 12.5f);

            Assert.AreEqual("sharp", affix.DefinitionId);
            Assert.AreEqual(12.5f, affix.RolledValue);
            Assert.IsTrue(affix.IsValid);
        }

        [Test]
        public void AffixInstance_WithDefinition_CachesReference()
        {
            var affixDef = CreateTestAffix("sharp", AffixModifierType.DamagePercent);
            var affix = new AffixInstance(affixDef, 10f);

            var registry = CreateTestRegistry(null, new List<AffixDefinition> { affixDef });
            var resolved = affix.GetDefinition(registry);

            Assert.AreEqual(affixDef, resolved);
        }

        [Test]
        public void AffixInstance_GetEffectiveValue_ReturnsPositiveForNormalAffix()
        {
            var affixDef = CreateTestAffix("sharp", AffixModifierType.DamagePercent, isNegative: false);
            var registry = CreateTestRegistry(null, new List<AffixDefinition> { affixDef });

            var affix = new AffixInstance(affixDef, 10f);
            float effective = affix.GetEffectiveValue(registry);

            Assert.AreEqual(10f, effective);
        }

        [Test]
        public void AffixInstance_GetEffectiveValue_ReturnsNegativeForPenaltyAffix()
        {
            var affixDef = CreateTestAffix("slow", AffixModifierType.AttackSpeedPercent, isNegative: true);
            var registry = CreateTestRegistry(null, new List<AffixDefinition> { affixDef });

            var affix = new AffixInstance(affixDef, 10f);
            float effective = affix.GetEffectiveValue(registry);

            Assert.AreEqual(-10f, effective);
        }

        [Test]
        public void AffixInstance_Invalid_WhenNoDefinitionId()
        {
            var affix = new AffixInstance("", 10f);
            Assert.IsFalse(affix.IsValid);
        }

        #endregion

        #region ResolvedWeaponStats Tests

        [Test]
        public void ResolvedWeaponStats_Creation_SetsAllValues()
        {
            var stats = new ResolvedWeaponStats(30f, 1.5f, 2.5f, 0.4f);

            Assert.AreEqual(30f, stats.Damage);
            Assert.AreEqual(1.5f, stats.AttackSpeed);
            Assert.AreEqual(2.5f, stats.Range);
            Assert.AreEqual(0.4f, stats.Stagger);
            Assert.IsTrue(stats.IsValid);
        }

        [Test]
        public void ResolvedWeaponStats_DurationMultiplier_InverseOfAttackSpeed()
        {
            var stats = new ResolvedWeaponStats(25f, 2.0f, 2.0f, 0.3f);

            Assert.AreEqual(0.5f, stats.DurationMultiplier, 0.001f);
        }

        [Test]
        public void ResolvedWeaponStats_Default_HasReasonableValues()
        {
            var defaults = ResolvedWeaponStats.Default;

            Assert.IsTrue(defaults.IsValid);
            Assert.Greater(defaults.Damage, 0f);
            Assert.Greater(defaults.AttackSpeed, 0f);
            Assert.Greater(defaults.Range, 0f);
        }

        [Test]
        public void ResolvedWeaponStats_Invalid_IsNotValid()
        {
            var invalid = ResolvedWeaponStats.Invalid;

            Assert.IsFalse(invalid.IsValid);
        }

        #endregion

        #region EquipmentData Stat Resolution Tests

        [Test]
        public void EquipmentData_ResolveStats_WithNoAffixes_ReturnsBaseStats()
        {
            var weapon = CreateTestWeapon("blade", damage: 30f, attackSpeed: 1.2f, range: 2.5f, stagger: 0.4f);
            var registry = CreateTestRegistry(new List<WeaponDefinition> { weapon }, null);

            var data = new EquipmentData("blade", "Test Blade", ItemRarity.Common, null, "Location");
            var stats = data.ResolveStats(registry);

            Assert.IsTrue(stats.IsValid);
            Assert.AreEqual(30f, stats.Damage, 0.01f);
            Assert.AreEqual(1.2f, stats.AttackSpeed, 0.01f);
            Assert.AreEqual(2.5f, stats.Range, 0.01f);
            Assert.AreEqual(0.4f, stats.Stagger, 0.01f);
        }

        [Test]
        public void EquipmentData_ResolveStats_WithDamageAffix_AppliesBonus()
        {
            var weapon = CreateTestWeapon("blade", damage: 100f);
            var sharpAffix = CreateTestAffix("sharp", AffixModifierType.DamagePercent);
            var registry = CreateTestRegistry(
                new List<WeaponDefinition> { weapon },
                new List<AffixDefinition> { sharpAffix });

            var affixes = new List<AffixInstance>
            {
                new AffixInstance(sharpAffix, 20f) // +20% damage
            };

            var data = new EquipmentData("blade", "Sharp Blade", ItemRarity.Uncommon, affixes, "Location");
            var stats = data.ResolveStats(registry);

            Assert.AreEqual(120f, stats.Damage, 0.01f); // 100 * 1.20 = 120
        }

        [Test]
        public void EquipmentData_ResolveStats_WithSpeedAffix_AppliesBonus()
        {
            var weapon = CreateTestWeapon("blade", attackSpeed: 1.0f);
            var swiftAffix = CreateTestAffix("swift", AffixModifierType.AttackSpeedPercent);
            var registry = CreateTestRegistry(
                new List<WeaponDefinition> { weapon },
                new List<AffixDefinition> { swiftAffix });

            var affixes = new List<AffixInstance>
            {
                new AffixInstance(swiftAffix, 15f) // +15% attack speed
            };

            var data = new EquipmentData("blade", "Swift Blade", ItemRarity.Uncommon, affixes, "Location");
            var stats = data.ResolveStats(registry);

            Assert.AreEqual(1.15f, stats.AttackSpeed, 0.01f); // 1.0 * 1.15 = 1.15
        }

        [Test]
        public void EquipmentData_ResolveStats_WithMultipleAffixes_AccumulatesModifiers()
        {
            var weapon = CreateTestWeapon("blade", damage: 100f, range: 2.0f);
            var sharpAffix = CreateTestAffix("sharp", AffixModifierType.DamagePercent);
            var balancedAffix = CreateTestAffix("balanced", AffixModifierType.RangePercent);
            var registry = CreateTestRegistry(
                new List<WeaponDefinition> { weapon },
                new List<AffixDefinition> { sharpAffix, balancedAffix });

            var affixes = new List<AffixInstance>
            {
                new AffixInstance(sharpAffix, 10f),   // +10% damage
                new AffixInstance(balancedAffix, 5f)  // +5% range
            };

            var data = new EquipmentData("blade", "Sharp Balanced Blade", ItemRarity.Rare, affixes, "Location");
            var stats = data.ResolveStats(registry);

            Assert.AreEqual(110f, stats.Damage, 0.01f); // 100 * 1.10 = 110
            Assert.AreEqual(2.1f, stats.Range, 0.01f);  // 2.0 * 1.05 = 2.1
        }

        [Test]
        public void EquipmentData_ResolveStats_ClampsToValidRanges()
        {
            var weapon = CreateTestWeapon("blade", attackSpeed: 1.0f, stagger: 0.5f);
            var superSpeed = CreateTestAffix("superfast", AffixModifierType.AttackSpeedPercent);
            var superStagger = CreateTestAffix("crushing", AffixModifierType.StaggerPercent);
            var registry = CreateTestRegistry(
                new List<WeaponDefinition> { weapon },
                new List<AffixDefinition> { superSpeed, superStagger });

            var affixes = new List<AffixInstance>
            {
                new AffixInstance(superSpeed, 500f),   // +500% speed (would be 6.0)
                new AffixInstance(superStagger, 200f)  // +200% stagger (would be 1.5)
            };

            var data = new EquipmentData("blade", "Test", ItemRarity.Rare, affixes, "Location");
            var stats = data.ResolveStats(registry);

            Assert.AreEqual(3f, stats.AttackSpeed, 0.01f);  // Clamped to max 3.0
            Assert.AreEqual(1f, stats.Stagger, 0.01f);      // Clamped to max 1.0
        }

        [Test]
        public void EquipmentData_ResolveStats_InvalidDefinition_ReturnsInvalid()
        {
            var registry = CreateTestRegistry();
            var data = new EquipmentData("nonexistent", "Test", ItemRarity.Common, null, "Location");

            var stats = data.ResolveStats(registry);

            Assert.IsFalse(stats.IsValid);
        }

        #endregion

        #region ItemInstance Equipment Integration Tests

        [Test]
        public void ItemInstance_WithEquipmentData_HasEquipmentDataTrue()
        {
            var weapon = CreateTestWeapon("blade");
            var equipData = new EquipmentData("blade", "Test Blade", ItemRarity.Common, null, "Location");

            var instance = new ItemInstance(weapon, equipData);

            Assert.IsTrue(instance.HasEquipmentData);
            Assert.AreEqual(equipData, instance.EquipmentData);
            Assert.AreEqual(1, instance.Quantity); // Equipment always quantity 1
        }

        [Test]
        public void ItemInstance_WithEquipmentData_CannotStack()
        {
            var weapon = CreateTestWeapon("blade");
            var equipData = new EquipmentData("blade", "Test Blade", ItemRarity.Common, null, "Location");

            var instance = new ItemInstance(weapon, equipData);
            int overflow = instance.Add(5);

            Assert.AreEqual(5, overflow); // All items overflow - cannot stack
            Assert.AreEqual(1, instance.Quantity);
        }

        [Test]
        public void ItemInstance_WithEquipmentData_CannotMerge()
        {
            var weapon = CreateTestWeapon("blade");
            var equipData1 = new EquipmentData("blade", "Blade 1", ItemRarity.Common, null, "Location");
            var equipData2 = new EquipmentData("blade", "Blade 2", ItemRarity.Common, null, "Location");

            var instance1 = new ItemInstance(weapon, equipData1);
            var instance2 = new ItemInstance(weapon, equipData2);

            bool merged = instance1.TryMerge(instance2);

            Assert.IsFalse(merged);
            Assert.AreEqual(1, instance1.Quantity);
        }

        [Test]
        public void ItemInstance_WithEquipmentData_CannotSplit()
        {
            var weapon = CreateTestWeapon("blade");
            var equipData = new EquipmentData("blade", "Test Blade", ItemRarity.Common, null, "Location");

            var instance = new ItemInstance(weapon, equipData);
            var split = instance.Split(1);

            Assert.IsNull(split);
            Assert.AreEqual(1, instance.Quantity);
        }

        [Test]
        public void ItemInstance_Clone_PreservesEquipmentGuid()
        {
            var weapon = CreateTestWeapon("blade");
            var equipData = new EquipmentData("blade", "Test Blade", ItemRarity.Rare, null, "Location");

            var original = new ItemInstance(weapon, equipData);
            var clone = original.Clone();

            Assert.IsTrue(clone.HasEquipmentData);
            Assert.AreEqual(original.EquipmentData.InstanceId, clone.EquipmentData.InstanceId);
        }

        [Test]
        public void ItemInstance_CloneAsNewEquipment_GeneratesNewGuid()
        {
            var weapon = CreateTestWeapon("blade");
            var equipData = new EquipmentData("blade", "Test Blade", ItemRarity.Rare, null, "Location");

            var original = new ItemInstance(weapon, equipData);
            var clone = original.CloneAsNewEquipment();

            Assert.IsTrue(clone.HasEquipmentData);
            Assert.AreNotEqual(original.EquipmentData.InstanceId, clone.EquipmentData.InstanceId);
        }

        #endregion

        #region EquipmentGenerator Tests

        [Test]
        public void EquipmentGenerator_GenerateWeapon_CreatesValidInstance()
        {
            var weapon = CreateTestWeapon("blade");
            var registry = CreateTestRegistry(new List<WeaponDefinition> { weapon });
            var generator = new EquipmentGenerator(registry);

            var instance = generator.GenerateWeapon(weapon, "TestLocation", seed: 12345);

            Assert.IsNotNull(instance);
            Assert.IsTrue(instance.HasEquipmentData);
            Assert.AreEqual("TestLocation", instance.EquipmentData.History.FoundLocation);
        }

        [Test]
        public void EquipmentGenerator_WithSameSeed_ProducesDeterministicResults()
        {
            var weapon = CreateTestWeapon("blade");
            var registry = CreateTestRegistry(new List<WeaponDefinition> { weapon });
            var generator = new EquipmentGenerator(registry);

            var instance1 = generator.GenerateWeapon(weapon, "Location", seed: 42);
            var instance2 = generator.GenerateWeapon(weapon, "Location", seed: 42);

            Assert.AreEqual(instance1.EquipmentData.Rarity, instance2.EquipmentData.Rarity);
            Assert.AreEqual(instance1.EquipmentData.CustomName, instance2.EquipmentData.CustomName);
        }

        [Test]
        public void EquipmentGenerator_GenerateWeaponWithRarity_SetsCorrectRarity()
        {
            var weapon = CreateTestWeapon("blade");
            var registry = CreateTestRegistry(new List<WeaponDefinition> { weapon });
            var generator = new EquipmentGenerator(registry);

            var instance = generator.GenerateWeaponWithRarity(weapon, ItemRarity.Rare, "Location");

            Assert.AreEqual(ItemRarity.Rare, instance.EquipmentData.Rarity);
        }

        [Test]
        public void EquipmentGenerator_CreateSimpleWeapon_HasNoAffixes()
        {
            var weapon = CreateTestWeapon("blade");
            var registry = CreateTestRegistry(new List<WeaponDefinition> { weapon });
            var generator = new EquipmentGenerator(registry);

            var instance = generator.CreateSimpleWeapon(weapon, "My Blade", "Starting Equipment");

            Assert.AreEqual(ItemRarity.Common, instance.EquipmentData.Rarity);
            Assert.AreEqual(0, instance.EquipmentData.Affixes.Count);
            Assert.AreEqual("My Blade", instance.EquipmentData.CustomName);
        }

        [Test]
        public void EquipmentGenerator_NullDefinition_ReturnsNull()
        {
            var registry = CreateTestRegistry();
            var generator = new EquipmentGenerator(registry);

            var instance = generator.GenerateWeapon(null, "Location");

            Assert.IsNull(instance);
        }

        #endregion

        #region EquipmentRegistry Tests

        [Test]
        public void EquipmentRegistry_GetWeaponDefinition_ReturnsCorrectWeapon()
        {
            var blade = CreateTestWeapon("blade");
            var sword = CreateTestWeapon("sword");
            var registry = CreateTestRegistry(new List<WeaponDefinition> { blade, sword });

            var found = registry.GetWeaponDefinition("blade");

            Assert.AreEqual(blade, found);
        }

        [Test]
        public void EquipmentRegistry_GetWeaponDefinition_InvalidId_ReturnsNull()
        {
            var blade = CreateTestWeapon("blade");
            var registry = CreateTestRegistry(new List<WeaponDefinition> { blade });

            var found = registry.GetWeaponDefinition("nonexistent");

            Assert.IsNull(found);
        }

        [Test]
        public void EquipmentRegistry_GetAffixDefinition_ReturnsCorrectAffix()
        {
            var sharp = CreateTestAffix("sharp", AffixModifierType.DamagePercent);
            var swift = CreateTestAffix("swift", AffixModifierType.AttackSpeedPercent);
            var registry = CreateTestRegistry(null, new List<AffixDefinition> { sharp, swift });

            var found = registry.GetAffixDefinition("sharp");

            Assert.AreEqual(sharp, found);
        }

        [Test]
        public void EquipmentRegistry_GetAffixesByTier_FiltersCorrectly()
        {
            var minorAffix = CreateTestAffix("minor", AffixModifierType.DamagePercent, tier: AffixTier.Minor);
            var majorAffix = CreateTestAffix("major", AffixModifierType.DamagePercent, tier: AffixTier.Major);
            var registry = CreateTestRegistry(null, new List<AffixDefinition> { minorAffix, majorAffix });

            var minorAffixes = registry.GetAffixesByTier(AffixTier.Minor);
            var majorAffixes = registry.GetAffixesByTier(AffixTier.Major);

            Assert.AreEqual(1, minorAffixes.Count);
            Assert.AreEqual(1, majorAffixes.Count);
            Assert.AreEqual(minorAffix, minorAffixes[0]);
            Assert.AreEqual(majorAffix, majorAffixes[0]);
        }

        [Test]
        public void EquipmentRegistry_ValidateAll_ReportsErrors()
        {
            var validWeapon = CreateTestWeapon("blade", damage: 25f);
            var invalidWeapon = CreateTestWeapon("invalid", damage: 0f); // Zero damage is invalid
            var registry = CreateTestRegistry(new List<WeaponDefinition> { validWeapon, invalidWeapon });

            bool isValid = registry.ValidateAll(out var errors);

            Assert.IsFalse(isValid);
            Assert.Greater(errors.Count, 0);
        }

        #endregion

        #region Inventory Equipment Tests

        [Test]
        public void Inventory_AddEquipment_UsesOneSlot()
        {
            var inventory = new Inventory(10);
            var weapon = CreateTestWeapon("blade");
            var equipData = new EquipmentData("blade", "Test Blade", ItemRarity.Common, null, "Location");
            var instance = new ItemInstance(weapon, equipData);

            bool added = inventory.TryAddItem(instance);

            Assert.IsTrue(added);
            Assert.AreEqual(1, inventory.GetOccupiedSlotCount());
        }

        [Test]
        public void Inventory_TwoEquipmentWithSameDefinition_UseTwoSlots()
        {
            var inventory = new Inventory(10);
            var weapon = CreateTestWeapon("blade");

            var instance1 = new ItemInstance(weapon, new EquipmentData("blade", "Blade 1", ItemRarity.Common, null, "L1"));
            var instance2 = new ItemInstance(weapon, new EquipmentData("blade", "Blade 2", ItemRarity.Common, null, "L2"));

            inventory.TryAddItem(instance1);
            inventory.TryAddItem(instance2);

            Assert.AreEqual(2, inventory.GetOccupiedSlotCount());
        }

        [Test]
        public void Inventory_FullInventory_RejectsEquipment()
        {
            var inventory = new Inventory(1);
            var weapon = CreateTestWeapon("blade");

            var instance1 = new ItemInstance(weapon, new EquipmentData("blade", "Blade 1", ItemRarity.Common, null, "L1"));
            var instance2 = new ItemInstance(weapon, new EquipmentData("blade", "Blade 2", ItemRarity.Common, null, "L2"));

            bool added1 = inventory.TryAddItem(instance1);
            bool added2 = inventory.TryAddItem(instance2);

            Assert.IsTrue(added1);
            Assert.IsFalse(added2);
            Assert.AreEqual(1, inventory.GetOccupiedSlotCount());
        }

        #endregion

        #region AffixDefinition Tests

        [Test]
        public void AffixDefinition_RollValue_WithinRange()
        {
            var affix = CreateTestAffix("test", AffixModifierType.DamagePercent, minValue: 5f, maxValue: 15f);

            for (int i = 0; i < 100; i++)
            {
                float value = affix.RollValue();
                Assert.GreaterOrEqual(value, 5f);
                Assert.LessOrEqual(value, 15f);
            }
        }

        [Test]
        public void AffixDefinition_RollValueWithRandom_IsDeterministic()
        {
            var affix = CreateTestAffix("test", AffixModifierType.DamagePercent, minValue: 5f, maxValue: 15f);

            var random1 = new System.Random(42);
            var random2 = new System.Random(42);

            float value1 = affix.RollValue(random1);
            float value2 = affix.RollValue(random2);

            Assert.AreEqual(value1, value2);
        }

        [Test]
        public void AffixDefinition_FormatDescription_PositiveAffix()
        {
            var affix = CreateTestAffix("sharp", AffixModifierType.DamagePercent, isNegative: false);

            string desc = affix.FormatDescription(10f);

            Assert.IsTrue(desc.Contains("10"));
            Assert.IsFalse(desc.Contains("-"));
        }

        [Test]
        public void AffixDefinition_FormatDescription_NegativeAffix()
        {
            var affix = CreateTestAffix("slow", AffixModifierType.AttackSpeedPercent, isNegative: true);

            string desc = affix.FormatDescription(10f);

            Assert.IsTrue(desc.Contains("-10"));
        }

        #endregion

        [TearDown]
        public void TearDown()
        {
            // Clean up created ScriptableObjects
        }
    }
}

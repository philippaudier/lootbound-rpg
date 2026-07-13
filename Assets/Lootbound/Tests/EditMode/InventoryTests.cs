using NUnit.Framework;
using UnityEngine;
using Lootbound.Gameplay.Inventory;

namespace Lootbound.Tests.EditMode
{
    /// <summary>
    /// EditMode tests for the inventory system.
    /// Tests item instances, slots, and inventory operations.
    /// </summary>
    public class InventoryTests
    {
        private ItemDefinition CreateTestItem(string name, bool stackable = true, int maxStack = 99)
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            // Use reflection to set private fields for testing
            var type = typeof(ItemDefinition);
            type.GetField("itemId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(item, name);
            type.GetField("displayName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(item, name);
            type.GetField("isStackable", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(item, stackable);
            type.GetField("maxStackSize", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(item, maxStack);
            return item;
        }

        #region ItemInstance Tests

        [Test]
        public void ItemInstance_Creation_SetsCorrectQuantity()
        {
            var definition = CreateTestItem("TestItem");
            var instance = new ItemInstance(definition, 5);

            Assert.AreEqual(5, instance.Quantity);
            Assert.AreEqual(definition, instance.Definition);
            Assert.IsTrue(instance.IsValid);
        }

        [Test]
        public void ItemInstance_Creation_ClampsQuantityToMaxStack()
        {
            var definition = CreateTestItem("TestItem", true, 10);
            var instance = new ItemInstance(definition, 100);

            Assert.AreEqual(10, instance.Quantity);
        }

        [Test]
        public void ItemInstance_Add_IncreasesQuantity()
        {
            var definition = CreateTestItem("TestItem", true, 99);
            var instance = new ItemInstance(definition, 5);

            int overflow = instance.Add(10);

            Assert.AreEqual(15, instance.Quantity);
            Assert.AreEqual(0, overflow);
        }

        [Test]
        public void ItemInstance_Add_ReturnsOverflow()
        {
            var definition = CreateTestItem("TestItem", true, 10);
            var instance = new ItemInstance(definition, 5);

            int overflow = instance.Add(10);

            Assert.AreEqual(10, instance.Quantity);
            Assert.AreEqual(5, overflow);
        }

        [Test]
        public void ItemInstance_Remove_DecreasesQuantity()
        {
            var definition = CreateTestItem("TestItem");
            var instance = new ItemInstance(definition, 10);

            int removed = instance.Remove(3);

            Assert.AreEqual(7, instance.Quantity);
            Assert.AreEqual(3, removed);
        }

        [Test]
        public void ItemInstance_Remove_ClampsToAvailable()
        {
            var definition = CreateTestItem("TestItem");
            var instance = new ItemInstance(definition, 5);

            int removed = instance.Remove(10);

            Assert.AreEqual(0, instance.Quantity);
            Assert.AreEqual(5, removed);
        }

        [Test]
        public void ItemInstance_Split_CreatesNewInstance()
        {
            var definition = CreateTestItem("TestItem");
            var instance = new ItemInstance(definition, 10);

            var split = instance.Split(3);

            Assert.IsNotNull(split);
            Assert.AreEqual(7, instance.Quantity);
            Assert.AreEqual(3, split.Quantity);
            Assert.AreEqual(definition, split.Definition);
        }

        [Test]
        public void ItemInstance_Split_FailsWithInvalidAmount()
        {
            var definition = CreateTestItem("TestItem");
            var instance = new ItemInstance(definition, 10);

            var split = instance.Split(10);

            Assert.IsNull(split);
            Assert.AreEqual(10, instance.Quantity);
        }

        [Test]
        public void ItemInstance_Clone_CreatesIdenticalCopy()
        {
            var definition = CreateTestItem("TestItem");
            var instance = new ItemInstance(definition, 5);

            var clone = instance.Clone();

            Assert.AreEqual(instance.Quantity, clone.Quantity);
            Assert.AreEqual(instance.Definition, clone.Definition);
            Assert.AreNotSame(instance, clone);
        }

        #endregion

        #region InventorySlot Tests

        [Test]
        public void InventorySlot_InitialState_IsEmpty()
        {
            var slot = new InventorySlot(0);

            Assert.IsTrue(slot.IsEmpty);
            Assert.IsFalse(slot.HasItem);
            Assert.IsNull(slot.Item);
        }

        [Test]
        public void InventorySlot_SetItem_MakesSlotFilled()
        {
            var slot = new InventorySlot(0);
            var definition = CreateTestItem("TestItem");
            var instance = new ItemInstance(definition, 5);

            slot.SetItem(instance);

            Assert.IsFalse(slot.IsEmpty);
            Assert.IsTrue(slot.HasItem);
            Assert.AreEqual(instance, slot.Item);
        }

        [Test]
        public void InventorySlot_Clear_ReturnsItem()
        {
            var slot = new InventorySlot(0);
            var definition = CreateTestItem("TestItem");
            var instance = new ItemInstance(definition, 5);
            slot.SetItem(instance);

            var removed = slot.Clear();

            Assert.IsTrue(slot.IsEmpty);
            Assert.AreEqual(instance, removed);
        }

        [Test]
        public void InventorySlot_CanAccept_EmptySlotAcceptsAnyItem()
        {
            var slot = new InventorySlot(0);
            var definition = CreateTestItem("TestItem");
            var instance = new ItemInstance(definition, 5);

            Assert.IsTrue(slot.CanAccept(instance));
        }

        [Test]
        public void InventorySlot_CanAccept_FilledSlotAcceptsSameType()
        {
            var slot = new InventorySlot(0);
            var definition = CreateTestItem("TestItem", true, 99);
            slot.SetItem(new ItemInstance(definition, 5));

            var incoming = new ItemInstance(definition, 10);
            Assert.IsTrue(slot.CanAccept(incoming));
        }

        [Test]
        public void InventorySlot_CanAccept_FullSlotRejectsMoreItems()
        {
            var slot = new InventorySlot(0);
            var definition = CreateTestItem("TestItem", true, 10);
            slot.SetItem(new ItemInstance(definition, 10));

            var incoming = new ItemInstance(definition, 5);
            Assert.IsFalse(slot.CanAccept(incoming));
        }

        #endregion

        #region Inventory Tests

        [Test]
        public void Inventory_Creation_HasCorrectCapacity()
        {
            var inventory = new Inventory(20);

            Assert.AreEqual(20, inventory.Capacity);
            Assert.AreEqual(20, inventory.Slots.Count);
        }

        [Test]
        public void Inventory_TryAddItem_AddsToEmptySlot()
        {
            var inventory = new Inventory(10);
            var definition = CreateTestItem("TestItem");
            var instance = new ItemInstance(definition, 5);

            bool added = inventory.TryAddItem(instance);

            Assert.IsTrue(added);
            Assert.AreEqual(5, inventory.GetItemCount(definition));
        }

        [Test]
        public void Inventory_TryAddItem_StacksWithExisting()
        {
            var inventory = new Inventory(10);
            var definition = CreateTestItem("TestItem", true, 99);

            inventory.TryAddItem(new ItemInstance(definition, 5));
            inventory.TryAddItem(new ItemInstance(definition, 10));

            Assert.AreEqual(15, inventory.GetItemCount(definition));
            Assert.AreEqual(1, inventory.GetOccupiedSlotCount());
        }

        [Test]
        public void Inventory_TryAddItem_CreatesNewStackWhenFull()
        {
            var inventory = new Inventory(10);
            var definition = CreateTestItem("TestItem", true, 10);

            inventory.TryAddItem(new ItemInstance(definition, 10));
            inventory.TryAddItem(new ItemInstance(definition, 5));

            Assert.AreEqual(15, inventory.GetItemCount(definition));
            Assert.AreEqual(2, inventory.GetOccupiedSlotCount());
        }

        [Test]
        public void Inventory_RemoveItem_DecreasesCount()
        {
            var inventory = new Inventory(10);
            var definition = CreateTestItem("TestItem");
            inventory.TryAddItem(new ItemInstance(definition, 10));

            int removed = inventory.RemoveItem(definition, 3);

            Assert.AreEqual(3, removed);
            Assert.AreEqual(7, inventory.GetItemCount(definition));
        }

        [Test]
        public void Inventory_RemoveItem_ClearsEmptySlots()
        {
            var inventory = new Inventory(10);
            var definition = CreateTestItem("TestItem");
            inventory.TryAddItem(new ItemInstance(definition, 5));

            inventory.RemoveItem(definition, 5);

            Assert.AreEqual(0, inventory.GetItemCount(definition));
            Assert.AreEqual(0, inventory.GetOccupiedSlotCount());
        }

        [Test]
        public void Inventory_HasItem_ReturnsTrueWhenSufficient()
        {
            var inventory = new Inventory(10);
            var definition = CreateTestItem("TestItem");
            inventory.TryAddItem(new ItemInstance(definition, 10));

            Assert.IsTrue(inventory.HasItem(definition, 5));
            Assert.IsTrue(inventory.HasItem(definition, 10));
            Assert.IsFalse(inventory.HasItem(definition, 15));
        }

        [Test]
        public void Inventory_SwapSlots_ExchangesItems()
        {
            var inventory = new Inventory(10);
            var itemA = CreateTestItem("ItemA");
            var itemB = CreateTestItem("ItemB");

            inventory.GetSlot(0).SetItem(new ItemInstance(itemA, 5));
            inventory.GetSlot(1).SetItem(new ItemInstance(itemB, 3));

            inventory.SwapSlots(0, 1);

            Assert.AreEqual(itemB, inventory.GetSlot(0).Definition);
            Assert.AreEqual(itemA, inventory.GetSlot(1).Definition);
        }

        [Test]
        public void Inventory_Clear_RemovesAllItems()
        {
            var inventory = new Inventory(10);
            var definition = CreateTestItem("TestItem");

            inventory.TryAddItem(new ItemInstance(definition, 5));
            inventory.TryAddItem(new ItemInstance(definition, 10));

            inventory.Clear();

            Assert.IsTrue(inventory.IsEmpty);
            Assert.AreEqual(0, inventory.GetItemCount(definition));
        }

        [Test]
        public void Inventory_IsFull_ReturnsTrueWhenNoEmptySlots()
        {
            var inventory = new Inventory(2);
            var itemA = CreateTestItem("ItemA", false, 1);
            var itemB = CreateTestItem("ItemB", false, 1);

            inventory.TryAddItem(new ItemInstance(itemA, 1));
            Assert.IsFalse(inventory.IsFull);

            inventory.TryAddItem(new ItemInstance(itemB, 1));
            Assert.IsTrue(inventory.IsFull);
        }

        [Test]
        public void Inventory_Events_FireOnChanges()
        {
            var inventory = new Inventory(10);
            var definition = CreateTestItem("TestItem");
            bool inventoryChangedFired = false;
            int slotChangedIndex = -1;

            inventory.OnInventoryChanged += () => inventoryChangedFired = true;
            inventory.OnSlotChanged += (index) => slotChangedIndex = index;

            inventory.TryAddItem(new ItemInstance(definition, 5));

            Assert.IsTrue(inventoryChangedFired);
            Assert.AreEqual(0, slotChangedIndex);
        }

        #endregion

        [TearDown]
        public void TearDown()
        {
            // Clean up any created ScriptableObjects
        }
    }
}

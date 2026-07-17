using NUnit.Framework;
using UnityEngine;
using Lootbound.Gameplay.World;
using Lootbound.Gameplay.Interaction;

namespace Lootbound.Tests.EditMode
{
    /// <summary>
    /// EditMode tests for the Attunement Table world object.
    /// Tests the IInteractable implementation and state management.
    /// </summary>
    [TestFixture]
    public class AttunementTableTests
    {
        private GameObject tableObject;
        private AttunementTable table;

        [SetUp]
        public void SetUp()
        {
            tableObject = new GameObject("TestAttunementTable");
            tableObject.AddComponent<BoxCollider>();
            table = tableObject.AddComponent<AttunementTable>();
        }

        [TearDown]
        public void TearDown()
        {
            if (tableObject != null)
            {
                Object.DestroyImmediate(tableObject);
            }
        }

        #region IInteractable Implementation

        [Test]
        public void AttunementTable_ImplementsIInteractable()
        {
            Assert.IsInstanceOf<IInteractable>(table);
        }

        [Test]
        public void AttunementTable_InteractionPrompt_ContainsExpectedText()
        {
            var prompt = table.InteractionPrompt;

            Assert.IsNotNull(prompt);
            Assert.IsTrue(prompt.Contains("Press E"), "Prompt should contain key hint");
        }

        [Test]
        public void AttunementTable_CanInteract_TrueWhenNotInUse()
        {
            Assert.IsTrue(table.CanInteract);
        }

        [Test]
        public void AttunementTable_CanInteract_FalseWhenInUse()
        {
            table.SetInUse(true);

            Assert.IsFalse(table.CanInteract);
        }

        [Test]
        public void AttunementTable_IconId_ReturnsExpectedValue()
        {
            Assert.AreEqual("attunement_table", table.IconId);
        }

        [Test]
        public void AttunementTable_HoldDuration_ReturnsZeroForInstantInteraction()
        {
            Assert.AreEqual(0f, table.HoldDuration);
        }

        [Test]
        public void AttunementTable_InteractionTransform_ReturnsTableTransform()
        {
            Assert.AreEqual(tableObject.transform, table.InteractionTransform);
        }

        #endregion

        #region State Management

        [Test]
        public void AttunementTable_IsInUse_FalseByDefault()
        {
            Assert.IsFalse(table.IsInUse);
        }

        [Test]
        public void AttunementTable_SetInUse_UpdatesState()
        {
            table.SetInUse(true);
            Assert.IsTrue(table.IsInUse);

            table.SetInUse(false);
            Assert.IsFalse(table.IsInUse);
        }

        [Test]
        public void AttunementTable_Close_SetsInUseFalse()
        {
            table.SetInUse(true);
            Assert.IsTrue(table.IsInUse);

            table.Close();
            Assert.IsFalse(table.IsInUse);
        }

        [Test]
        public void AttunementTable_SetInUse_SameValue_DoesNotTriggerEvents()
        {
            int openedCount = 0;
            int closedCount = 0;

            table.OnTableOpened += (_) => openedCount++;
            table.OnTableClosed += (_) => closedCount++;

            // Set to false when already false
            table.SetInUse(false);
            Assert.AreEqual(0, openedCount);
            Assert.AreEqual(0, closedCount);

            // Set to true
            table.SetInUse(true);
            Assert.AreEqual(1, openedCount);
            Assert.AreEqual(0, closedCount);

            // Set to true again
            table.SetInUse(true);
            Assert.AreEqual(1, openedCount, "Should not trigger again");
            Assert.AreEqual(0, closedCount);
        }

        #endregion

        #region Events

        [Test]
        public void AttunementTable_OnTableOpened_FiresWhenOpened()
        {
            AttunementTable receivedTable = null;
            table.OnTableOpened += (t) => receivedTable = t;

            table.SetInUse(true);

            Assert.IsNotNull(receivedTable);
            Assert.AreEqual(table, receivedTable);
        }

        [Test]
        public void AttunementTable_OnTableClosed_FiresWhenClosed()
        {
            AttunementTable receivedTable = null;
            table.OnTableClosed += (t) => receivedTable = t;

            table.SetInUse(true);
            table.SetInUse(false);

            Assert.IsNotNull(receivedTable);
            Assert.AreEqual(table, receivedTable);
        }

        [Test]
        public void AttunementTable_OnInteractionComplete_RaisesEvent_WhenNotInUse()
        {
            AttunementTable receivedTable = null;
            table.OnInteractionRequested += (t) => receivedTable = t;

            table.OnInteractionComplete(null);

            Assert.IsNotNull(receivedTable);
            Assert.AreEqual(table, receivedTable);
        }

        [Test]
        public void AttunementTable_OnInteractionComplete_DoesNotRaiseEvent_WhenInUse()
        {
            AttunementTable receivedTable = null;
            table.OnInteractionRequested += (t) => receivedTable = t;

            table.SetInUse(true);
            table.OnInteractionComplete(null);

            Assert.IsNull(receivedTable, "Should not raise event when already in use");
        }

        [Test]
        public void AttunementTable_OnInteractionStart_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => table.OnInteractionStart(null));
        }

        [Test]
        public void AttunementTable_OnInteractionCancel_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => table.OnInteractionCancel(null));
        }

        #endregion

        #region Anchor Points

        [Test]
        public void AttunementTable_WeaponAnchor_ReturnsTransformWhenNotSet()
        {
            // Start is called in play mode, but we can test the property exists
            Assert.IsNotNull(table.WeaponAnchor);
        }

        [Test]
        public void AttunementTable_StoneAnchor_ReturnsTransformWhenNotSet()
        {
            Assert.IsNotNull(table.StoneAnchor);
        }

        #endregion

        #region Multiple Tables

        [Test]
        public void AttunementTable_MultipleTables_IndependentState()
        {
            var table2Object = new GameObject("TestAttunementTable2");
            table2Object.AddComponent<BoxCollider>();
            var table2 = table2Object.AddComponent<AttunementTable>();

            try
            {
                table.SetInUse(true);
                Assert.IsTrue(table.IsInUse);
                Assert.IsFalse(table2.IsInUse);

                table2.SetInUse(true);
                Assert.IsTrue(table.IsInUse);
                Assert.IsTrue(table2.IsInUse);

                table.Close();
                Assert.IsFalse(table.IsInUse);
                Assert.IsTrue(table2.IsInUse);
            }
            finally
            {
                Object.DestroyImmediate(table2Object);
            }
        }

        #endregion

        #region Edge Cases

        [Test]
        public void AttunementTable_NoSubscribers_OnInteractionComplete_DoesNotThrow()
        {
            // Remove any existing subscribers by creating a new object
            var freshObject = new GameObject("FreshTable");
            freshObject.AddComponent<BoxCollider>();
            var freshTable = freshObject.AddComponent<AttunementTable>();

            try
            {
                // This should log a warning but not throw
                Assert.DoesNotThrow(() => freshTable.OnInteractionComplete(null));
            }
            finally
            {
                Object.DestroyImmediate(freshObject);
            }
        }

        [Test]
        public void AttunementTable_RapidOpenClose_HandlesCorrectly()
        {
            int openCount = 0;
            int closeCount = 0;

            table.OnTableOpened += (_) => openCount++;
            table.OnTableClosed += (_) => closeCount++;

            // Rapid state changes
            table.SetInUse(true);
            table.SetInUse(false);
            table.SetInUse(true);
            table.SetInUse(false);
            table.SetInUse(true);

            Assert.AreEqual(3, openCount);
            Assert.AreEqual(2, closeCount);
            Assert.IsTrue(table.IsInUse);
        }

        #endregion
    }
}

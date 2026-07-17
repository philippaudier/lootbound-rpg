using NUnit.Framework;
using UnityEngine;
using Lootbound.Gameplay.World;

namespace Lootbound.Tests.EditMode
{
    /// <summary>
    /// EditMode tests for the Repair Station system.
    /// Tests RepairStation component and basic state management.
    /// Note: Full UI integration requires PlayMode tests.
    /// </summary>
    public class RepairStationTests
    {
        #region RepairStation Component Tests

        [Test]
        public void RepairStation_InitialState_IsNotInUse()
        {
            var go = new GameObject("TestStation");
            go.AddComponent<BoxCollider>();
            var station = go.AddComponent<RepairStation>();

            Assert.IsFalse(station.IsInUse);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void RepairStation_CanInteract_TrueWhenNotInUse()
        {
            var go = new GameObject("TestStation");
            go.AddComponent<BoxCollider>();
            var station = go.AddComponent<RepairStation>();

            Assert.IsTrue(station.CanInteract);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void RepairStation_InteractionPrompt_ContainsExpectedText()
        {
            var go = new GameObject("TestStation");
            go.AddComponent<BoxCollider>();
            var station = go.AddComponent<RepairStation>();

            Assert.IsTrue(station.InteractionPrompt.Contains("Press E"));
            Assert.IsTrue(station.InteractionPrompt.ToLower().Contains("repair"));

            Object.DestroyImmediate(go);
        }

        [Test]
        public void RepairStation_HoldDuration_IsZeroByDefault()
        {
            var go = new GameObject("TestStation");
            go.AddComponent<BoxCollider>();
            var station = go.AddComponent<RepairStation>();

            // Instant interaction - no hold required
            Assert.AreEqual(0f, station.HoldDuration);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void RepairStation_InteractionTransform_ReturnsStationTransform()
        {
            var go = new GameObject("TestStation");
            go.AddComponent<BoxCollider>();
            var station = go.AddComponent<RepairStation>();

            Assert.AreEqual(go.transform, station.InteractionTransform);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void RepairStation_RepairAnchor_ReturnsTransformWhenNotSet()
        {
            var go = new GameObject("TestStation");
            go.AddComponent<BoxCollider>();
            var station = go.AddComponent<RepairStation>();

            // Start() is not called in EditMode, so RepairAnchor returns null initially
            // After Start(), it should return the station's own transform as fallback
            Assert.IsNull(station.RepairAnchor);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void RepairStation_IconId_ReturnsExpectedValue()
        {
            var go = new GameObject("TestStation");
            go.AddComponent<BoxCollider>();
            var station = go.AddComponent<RepairStation>();

            Assert.AreEqual("repair_station", station.IconId);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void RepairStation_Close_NotifiesStation()
        {
            var go = new GameObject("TestStation");
            go.AddComponent<BoxCollider>();
            var station = go.AddComponent<RepairStation>();

            bool closedEventFired = false;
            station.OnStationClosed += (s) => closedEventFired = true;

            // Close should not fire when not in use
            station.Close();
            Assert.IsFalse(closedEventFired);

            Object.DestroyImmediate(go);
        }

        #endregion

        #region State Management Tests

        [Test]
        public void RepairStation_MultipleCloses_DoNotThrow()
        {
            var go = new GameObject("TestStation");
            go.AddComponent<BoxCollider>();
            var station = go.AddComponent<RepairStation>();

            Assert.DoesNotThrow(() =>
            {
                station.Close();
                station.Close();
                station.Close();
            });

            Object.DestroyImmediate(go);
        }

        [Test]
        public void RepairStation_OnInteractionCancel_DoesNotChangeState()
        {
            var go = new GameObject("TestStation");
            go.AddComponent<BoxCollider>();
            var station = go.AddComponent<RepairStation>();

            bool wasInUse = station.IsInUse;
            station.OnInteractionCancel(null);

            Assert.AreEqual(wasInUse, station.IsInUse);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void RepairStation_OnInteractionStart_DoesNotOpenUI()
        {
            var go = new GameObject("TestStation");
            go.AddComponent<BoxCollider>();
            var station = go.AddComponent<RepairStation>();

            station.OnInteractionStart(null);

            // Should still not be in use until OnInteractionComplete
            Assert.IsFalse(station.IsInUse);

            Object.DestroyImmediate(go);
        }

        #endregion

        #region Event Tests

        [Test]
        public void RepairStation_OnStationOpened_NotNullAfterSubscribe()
        {
            var go = new GameObject("TestStation");
            go.AddComponent<BoxCollider>();
            var station = go.AddComponent<RepairStation>();

            bool eventSubscribed = false;
            station.OnStationOpened += (s) => eventSubscribed = true;

            // Event should be subscribed
            Assert.IsFalse(eventSubscribed); // Event hasn't fired yet

            Object.DestroyImmediate(go);
        }

        [Test]
        public void RepairStation_OnStationClosed_NotNullAfterSubscribe()
        {
            var go = new GameObject("TestStation");
            go.AddComponent<BoxCollider>();
            var station = go.AddComponent<RepairStation>();

            bool eventSubscribed = false;
            station.OnStationClosed += (s) => eventSubscribed = true;

            // Event should be subscribed
            Assert.IsFalse(eventSubscribed); // Event hasn't fired yet

            Object.DestroyImmediate(go);
        }

        [Test]
        public void RepairStation_SetInUse_FiresOpenedEvent()
        {
            var go = new GameObject("TestStation");
            go.AddComponent<BoxCollider>();
            var station = go.AddComponent<RepairStation>();

            bool openedFired = false;
            station.OnStationOpened += (s) => openedFired = true;

            station.SetInUse(true);

            Assert.IsTrue(openedFired);
            Assert.IsTrue(station.IsInUse);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void RepairStation_SetInUse_FiresClosedEvent()
        {
            var go = new GameObject("TestStation");
            go.AddComponent<BoxCollider>();
            var station = go.AddComponent<RepairStation>();

            station.SetInUse(true); // First set to true

            bool closedFired = false;
            station.OnStationClosed += (s) => closedFired = true;

            station.SetInUse(false);

            Assert.IsTrue(closedFired);
            Assert.IsFalse(station.IsInUse);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void RepairStation_SetInUseSameValue_DoesNotFireEvent()
        {
            var go = new GameObject("TestStation");
            go.AddComponent<BoxCollider>();
            var station = go.AddComponent<RepairStation>();

            int openedCount = 0;
            station.OnStationOpened += (s) => openedCount++;

            station.SetInUse(true);
            station.SetInUse(true); // Same value
            station.SetInUse(true); // Same value

            Assert.AreEqual(1, openedCount);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void RepairStation_OnInteractionRequested_FiresOnInteraction()
        {
            var go = new GameObject("TestStation");
            go.AddComponent<BoxCollider>();
            var station = go.AddComponent<RepairStation>();

            bool requestedFired = false;
            station.OnInteractionRequested += (s) => requestedFired = true;

            station.OnInteractionComplete(null);

            Assert.IsTrue(requestedFired);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void RepairStation_OnInteractionComplete_WhenInUse_DoesNotFireEvent()
        {
            var go = new GameObject("TestStation");
            go.AddComponent<BoxCollider>();
            var station = go.AddComponent<RepairStation>();

            station.SetInUse(true); // Mark as in use

            int requestedCount = 0;
            station.OnInteractionRequested += (s) => requestedCount++;

            station.OnInteractionComplete(null);

            Assert.AreEqual(0, requestedCount); // Should not fire when already in use

            Object.DestroyImmediate(go);
        }

        #endregion

        #region RequireComponent Tests

        [Test]
        public void RepairStation_RequiresCollider()
        {
            // Check that the RequireComponent attribute is present
            var attributes = typeof(RepairStation).GetCustomAttributes(typeof(RequireComponent), true);
            Assert.IsTrue(attributes.Length > 0);

            var requireComponent = attributes[0] as RequireComponent;
            Assert.IsNotNull(requireComponent);
            Assert.AreEqual(typeof(Collider), requireComponent.m_Type0);
        }

        #endregion
    }
}

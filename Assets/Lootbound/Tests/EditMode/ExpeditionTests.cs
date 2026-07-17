using NUnit.Framework;
using UnityEngine;
using Lootbound.Gameplay.Expeditions;
using Lootbound.Gameplay.Equipment;
using Lootbound.Gameplay.Inventory;

namespace Lootbound.Tests.EditMode
{
    /// <summary>
    /// EditMode tests for the expedition lifecycle system.
    /// Tests state transitions, metrics tracking, and session management.
    /// </summary>
    public class ExpeditionTests
    {
        #region ExpeditionState Extension Tests

        [Test]
        public void ExpeditionState_IsTerminal_TrueForCompletedFailedCancelled()
        {
            Assert.IsTrue(ExpeditionState.Completed.IsTerminal());
            Assert.IsTrue(ExpeditionState.Failed.IsTerminal());
            Assert.IsTrue(ExpeditionState.Cancelled.IsTerminal());
        }

        [Test]
        public void ExpeditionState_IsTerminal_FalseForNonTerminalStates()
        {
            Assert.IsFalse(ExpeditionState.None.IsTerminal());
            Assert.IsFalse(ExpeditionState.Preparing.IsTerminal());
            Assert.IsFalse(ExpeditionState.Departing.IsTerminal());
            Assert.IsFalse(ExpeditionState.Active.IsTerminal());
            Assert.IsFalse(ExpeditionState.Returning.IsTerminal());
        }

        [Test]
        public void ExpeditionState_IsTracking_TrueForActiveAndReturning()
        {
            Assert.IsTrue(ExpeditionState.Active.IsTracking());
            Assert.IsTrue(ExpeditionState.Returning.IsTracking());
        }

        [Test]
        public void ExpeditionState_IsTracking_FalseForOtherStates()
        {
            Assert.IsFalse(ExpeditionState.None.IsTracking());
            Assert.IsFalse(ExpeditionState.Preparing.IsTracking());
            Assert.IsFalse(ExpeditionState.Departing.IsTracking());
            Assert.IsFalse(ExpeditionState.Completed.IsTracking());
            Assert.IsFalse(ExpeditionState.Failed.IsTracking());
            Assert.IsFalse(ExpeditionState.Cancelled.IsTracking());
        }

        [Test]
        public void ExpeditionState_HasStarted_TrueAfterDeparting()
        {
            Assert.IsTrue(ExpeditionState.Departing.HasStarted());
            Assert.IsTrue(ExpeditionState.Active.HasStarted());
            Assert.IsTrue(ExpeditionState.Returning.HasStarted());
            Assert.IsTrue(ExpeditionState.Completed.HasStarted());
            Assert.IsTrue(ExpeditionState.Failed.HasStarted());
            Assert.IsTrue(ExpeditionState.Cancelled.HasStarted());
        }

        [Test]
        public void ExpeditionState_HasStarted_FalseBeforeDeparting()
        {
            Assert.IsFalse(ExpeditionState.None.HasStarted());
            Assert.IsFalse(ExpeditionState.Preparing.HasStarted());
        }

        #endregion

        #region ExpeditionMetrics Tests

        [Test]
        public void Metrics_InitialState_AllZero()
        {
            var metrics = new ExpeditionMetrics();

            Assert.AreEqual(0f, metrics.Duration);
            Assert.AreEqual(0f, metrics.MaxDistance);
            Assert.AreEqual(0, metrics.EnemiesDefeated);
            Assert.AreEqual(0, metrics.ItemsAcquired);
            Assert.AreEqual(0, metrics.EquipmentAcquired);
            Assert.AreEqual(0, metrics.ResourcesAcquired);
            Assert.IsNull(metrics.MainWeapon);
            Assert.IsFalse(metrics.IsFrozen);
        }

        [Test]
        public void Metrics_UpdateDuration_IncreasesDuration()
        {
            var metrics = new ExpeditionMetrics();

            metrics.UpdateDuration(1.5f);
            Assert.AreEqual(1.5f, metrics.Duration, 0.001f);

            metrics.UpdateDuration(0.5f);
            Assert.AreEqual(2f, metrics.Duration, 0.001f);
        }

        [Test]
        public void Metrics_UpdateMaxDistance_TracksMaximum()
        {
            var metrics = new ExpeditionMetrics();
            var origin = Vector3.zero;

            metrics.UpdateMaxDistance(new Vector3(10f, 5f, 0f), origin);
            Assert.AreEqual(10f, metrics.MaxDistance, 0.001f);

            // Closer position should not change max
            metrics.UpdateMaxDistance(new Vector3(5f, 0f, 0f), origin);
            Assert.AreEqual(10f, metrics.MaxDistance, 0.001f);

            // Farther position should update max
            metrics.UpdateMaxDistance(new Vector3(0f, 0f, 20f), origin);
            Assert.AreEqual(20f, metrics.MaxDistance, 0.001f);
        }

        [Test]
        public void Metrics_UpdateMaxDistance_IgnoresYComponent()
        {
            var metrics = new ExpeditionMetrics();
            var origin = Vector3.zero;

            // Y component should be ignored (horizontal distance only)
            metrics.UpdateMaxDistance(new Vector3(3f, 100f, 4f), origin);
            Assert.AreEqual(5f, metrics.MaxDistance, 0.001f); // sqrt(3^2 + 4^2) = 5
        }

        [Test]
        public void Metrics_RecordKill_IncrementsCount()
        {
            var metrics = new ExpeditionMetrics();

            metrics.RecordKill();
            Assert.AreEqual(1, metrics.EnemiesDefeated);

            metrics.RecordKill();
            Assert.AreEqual(2, metrics.EnemiesDefeated);
        }

        [Test]
        public void Metrics_RecordKill_TracksMainWeapon()
        {
            var metrics = new ExpeditionMetrics();
            var weapon1 = CreateTestEquipment("Sword");
            var weapon2 = CreateTestEquipment("Axe");

            // 2 kills with sword
            metrics.RecordKill(weapon1);
            metrics.RecordKill(weapon1);
            Assert.AreEqual(weapon1, metrics.MainWeapon);
            Assert.AreEqual(2, metrics.MainWeaponKills);

            // 1 kill with axe - sword still main
            metrics.RecordKill(weapon2);
            Assert.AreEqual(weapon1, metrics.MainWeapon);
            Assert.AreEqual(2, metrics.MainWeaponKills);

            // 2 more kills with axe - axe becomes main
            metrics.RecordKill(weapon2);
            metrics.RecordKill(weapon2);
            Assert.AreEqual(weapon2, metrics.MainWeapon);
            Assert.AreEqual(3, metrics.MainWeaponKills);
        }

        [Test]
        public void Metrics_RecordItemAcquired_TracksResources()
        {
            var metrics = new ExpeditionMetrics();
            var itemDef = CreateTestItemDefinition("Stone");

            metrics.RecordItemAcquired(itemDef, 5, isEquipment: false);

            Assert.AreEqual(5, metrics.ItemsAcquired);
            Assert.AreEqual(0, metrics.EquipmentAcquired);
            Assert.AreEqual(5, metrics.ResourcesAcquired);
        }

        [Test]
        public void Metrics_RecordItemAcquired_TracksEquipment()
        {
            var metrics = new ExpeditionMetrics();
            var itemDef = CreateTestItemDefinition("Sword");

            metrics.RecordItemAcquired(itemDef, 1, isEquipment: true);

            Assert.AreEqual(1, metrics.ItemsAcquired);
            Assert.AreEqual(1, metrics.EquipmentAcquired);
            Assert.AreEqual(0, metrics.ResourcesAcquired);
        }

        [Test]
        public void Metrics_Freeze_PreventsUpdates()
        {
            var metrics = new ExpeditionMetrics();
            metrics.UpdateDuration(1f);
            metrics.RecordKill();

            metrics.Freeze();

            metrics.UpdateDuration(5f);
            metrics.RecordKill();

            Assert.AreEqual(1f, metrics.Duration, 0.001f);
            Assert.AreEqual(1, metrics.EnemiesDefeated);
            Assert.IsTrue(metrics.IsFrozen);
        }

        [Test]
        public void Metrics_Reset_ClearsAllValues()
        {
            var metrics = new ExpeditionMetrics();
            metrics.UpdateDuration(10f);
            metrics.RecordKill(CreateTestEquipment("Sword"));
            metrics.Freeze();

            metrics.Reset();

            Assert.AreEqual(0f, metrics.Duration);
            Assert.AreEqual(0, metrics.EnemiesDefeated);
            Assert.IsNull(metrics.MainWeapon);
            Assert.IsFalse(metrics.IsFrozen);
        }

        [Test]
        public void Metrics_DurationFormatted_ReturnsCorrectFormat()
        {
            var metrics = new ExpeditionMetrics();

            metrics.UpdateDuration(65f); // 1 min 5 sec
            Assert.AreEqual("01:05", metrics.DurationFormatted);

            metrics.Reset();
            metrics.UpdateDuration(3661f); // 61 min 1 sec
            Assert.AreEqual("61:01", metrics.DurationFormatted);
        }

        #endregion

        #region ExpeditionSession State Transition Tests

        [Test]
        public void Session_InitialState_IsPreparing()
        {
            var session = new ExpeditionSession(12345, Vector3.zero);

            Assert.AreEqual(ExpeditionState.Preparing, session.State);
            Assert.AreEqual(12345, session.WorldSeed);
            Assert.IsFalse(session.HasEnded);
        }

        [Test]
        public void Session_HasValidId()
        {
            var session = new ExpeditionSession(0, Vector3.zero);

            Assert.IsNotNull(session.ExpeditionId);
            Assert.IsNotEmpty(session.ExpeditionId);
            Assert.AreEqual(36, session.ExpeditionId.Length); // GUID format
        }

        [Test]
        public void Session_Depart_TransitionsToDeparting()
        {
            var session = new ExpeditionSession(0, Vector3.zero);

            bool result = session.Depart(null, null);

            Assert.IsTrue(result);
            Assert.AreEqual(ExpeditionState.Departing, session.State);
            Assert.IsNotNull(session.Snapshot);
        }

        [Test]
        public void Session_Activate_TransitionsToActive()
        {
            var session = new ExpeditionSession(0, Vector3.zero);
            session.Depart(null, null);

            bool result = session.Activate();

            Assert.IsTrue(result);
            Assert.AreEqual(ExpeditionState.Active, session.State);
            Assert.IsTrue(session.IsTracking);
        }

        [Test]
        public void Session_BeginReturn_TransitionsToReturning()
        {
            var session = new ExpeditionSession(0, Vector3.zero);
            session.Depart(null, null);
            session.Activate();

            bool result = session.BeginReturn();

            Assert.IsTrue(result);
            Assert.AreEqual(ExpeditionState.Returning, session.State);
            Assert.IsTrue(session.IsTracking);
        }

        [Test]
        public void Session_Complete_FromActive_Succeeds()
        {
            var session = new ExpeditionSession(0, Vector3.zero);
            session.Depart(null, null);
            session.Activate();

            bool result = session.Complete();

            Assert.IsTrue(result);
            Assert.AreEqual(ExpeditionState.Completed, session.State);
            Assert.AreEqual(ExpeditionOutcome.Success, session.Outcome);
            Assert.IsTrue(session.HasEnded);
            Assert.IsTrue(session.Metrics.IsFrozen);
        }

        [Test]
        public void Session_Complete_FromReturning_Succeeds()
        {
            var session = new ExpeditionSession(0, Vector3.zero);
            session.Depart(null, null);
            session.Activate();
            session.BeginReturn();

            bool result = session.Complete();

            Assert.IsTrue(result);
            Assert.AreEqual(ExpeditionState.Completed, session.State);
        }

        [Test]
        public void Session_Fail_TransitionsToFailed()
        {
            var session = new ExpeditionSession(0, Vector3.zero);
            session.Depart(null, null);
            session.Activate();

            bool result = session.Fail();

            Assert.IsTrue(result);
            Assert.AreEqual(ExpeditionState.Failed, session.State);
            Assert.AreEqual(ExpeditionOutcome.PlayerDeath, session.Outcome);
            Assert.IsTrue(session.HasEnded);
        }

        [Test]
        public void Session_Cancel_TransitionsToCancelled()
        {
            var session = new ExpeditionSession(0, Vector3.zero);
            session.Depart(null, null);
            session.Activate();

            bool result = session.Cancel();

            Assert.IsTrue(result);
            Assert.AreEqual(ExpeditionState.Cancelled, session.State);
            Assert.AreEqual(ExpeditionOutcome.Cancelled, session.Outcome);
            Assert.IsTrue(session.HasEnded);
        }

        [Test]
        public void Session_InvalidTransitions_ReturnFalse()
        {
            var session = new ExpeditionSession(0, Vector3.zero);

            // Cannot activate from Preparing
            Assert.IsFalse(session.Activate());

            // Cannot begin return from Preparing
            Assert.IsFalse(session.BeginReturn());

            // Cannot complete from Preparing
            Assert.IsFalse(session.Complete());

            session.Depart(null, null);

            // Cannot depart again
            Assert.IsFalse(session.Depart(null, null));

            // Cannot begin return from Departing
            Assert.IsFalse(session.BeginReturn());
        }

        [Test]
        public void Session_CannotTransitionAfterTerminal()
        {
            var session = new ExpeditionSession(0, Vector3.zero);
            session.Depart(null, null);
            session.Activate();
            session.Complete();

            // All transitions should fail after terminal state
            Assert.IsFalse(session.Depart(null, null));
            Assert.IsFalse(session.Activate());
            Assert.IsFalse(session.BeginReturn());
            Assert.IsFalse(session.Complete());
            Assert.IsFalse(session.Fail());
            Assert.IsFalse(session.Cancel());
        }

        #endregion

        #region ExpeditionSnapshot Tests

        [Test]
        public void Snapshot_Empty_HasNoWeapon()
        {
            var snapshot = ExpeditionSnapshot.Empty();

            Assert.IsFalse(snapshot.HadWeaponEquipped);
            Assert.IsEmpty(snapshot.EquippedWeaponId);
            Assert.AreEqual(0, snapshot.EquippedWeaponAttunement);
        }

        [Test]
        public void Snapshot_Capture_WithNullReferences_CreatesValidSnapshot()
        {
            var snapshot = ExpeditionSnapshot.Capture(null, null);

            Assert.IsNotNull(snapshot);
            Assert.IsFalse(snapshot.HadWeaponEquipped);
        }

        #endregion

        #region RefugeZone Tests

        [Test]
        public void RefugeZone_IsPositionInside_TrueForPositionWithinRadius()
        {
            var go = new GameObject("TestRefugeZone");
            var refugeZone = go.AddComponent<RefugeZone>();

            // Set radius via reflection (private field)
            var radiusField = typeof(RefugeZone).GetField("radius",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            radiusField?.SetValue(refugeZone, 10f);

            // Position at center
            Assert.IsTrue(refugeZone.IsPositionInside(go.transform.position));

            // Position within radius
            Assert.IsTrue(refugeZone.IsPositionInside(go.transform.position + new Vector3(5f, 0f, 0f)));

            // Position at edge
            Assert.IsTrue(refugeZone.IsPositionInside(go.transform.position + new Vector3(10f, 0f, 0f)));

            Object.DestroyImmediate(go);
        }

        [Test]
        public void RefugeZone_IsPositionInside_FalseForPositionOutsideRadius()
        {
            var go = new GameObject("TestRefugeZone");
            var refugeZone = go.AddComponent<RefugeZone>();

            var radiusField = typeof(RefugeZone).GetField("radius",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            radiusField?.SetValue(refugeZone, 10f);

            // Position outside radius
            Assert.IsFalse(refugeZone.IsPositionInside(go.transform.position + new Vector3(15f, 0f, 0f)));

            // Y position shouldn't affect horizontal distance check
            Assert.IsTrue(refugeZone.IsPositionInside(go.transform.position + new Vector3(5f, 100f, 0f)));

            Object.DestroyImmediate(go);
        }

        [Test]
        public void RefugeZone_GetDistanceFromCenter_ReturnsHorizontalDistance()
        {
            var go = new GameObject("TestRefugeZone");
            var refugeZone = go.AddComponent<RefugeZone>();

            // Test horizontal distance (Y should be ignored)
            float distance = refugeZone.GetDistanceFromCenter(go.transform.position + new Vector3(3f, 100f, 4f));
            Assert.AreEqual(5f, distance, 0.001f); // sqrt(3^2 + 4^2) = 5

            Object.DestroyImmediate(go);
        }

        #endregion

        #region ExpeditionBoundary Tests

        [Test]
        public void ExpeditionBoundary_DetermineSide_ReturnsCorrectSide()
        {
            var go = new GameObject("TestBoundary");
            go.AddComponent<BoxCollider>(); // Required component
            var boundary = go.AddComponent<ExpeditionBoundary>();

            // Set refuge direction to back (local -Z)
            var dirField = typeof(ExpeditionBoundary).GetField("refugeDirection",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            dirField?.SetValue(boundary, Vector3.back);

            // Position behind boundary (refuge side, in -Z direction)
            var side = boundary.DetermineSide(go.transform.position + new Vector3(0f, 0f, -5f));
            Assert.AreEqual(ExpeditionBoundary.BoundarySide.Refuge, side);

            // Position in front of boundary (outside, in +Z direction)
            side = boundary.DetermineSide(go.transform.position + new Vector3(0f, 0f, 5f));
            Assert.AreEqual(ExpeditionBoundary.BoundarySide.Outside, side);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void ExpeditionBoundary_GetSignedDistanceFromBoundary_ReturnsCorrectValue()
        {
            var go = new GameObject("TestBoundary");
            go.AddComponent<BoxCollider>();
            var boundary = go.AddComponent<ExpeditionBoundary>();

            var dirField = typeof(ExpeditionBoundary).GetField("refugeDirection",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            dirField?.SetValue(boundary, Vector3.back);

            // Position in refuge direction (positive)
            float dist = boundary.GetSignedDistanceFromBoundary(go.transform.position + new Vector3(0f, 0f, -10f));
            Assert.Greater(dist, 0f);

            // Position in outside direction (negative)
            dist = boundary.GetSignedDistanceFromBoundary(go.transform.position + new Vector3(0f, 0f, 10f));
            Assert.Less(dist, 0f);

            Object.DestroyImmediate(go);
        }

        #endregion

        #region Test Helpers

        private EquipmentData CreateTestEquipment(string name)
        {
            // Create minimal equipment data for testing
            // EquipmentData constructor: (definitionId, customName, rarity, affixes, foundLocation)
            return new EquipmentData(
                definitionId: $"test_{name.ToLower()}",
                customName: name,
                rarity: ItemRarity.Common,
                affixes: null,
                foundLocation: "TestLocation"
            );
        }

        private ItemDefinition CreateTestItemDefinition(string name)
        {
            var def = ScriptableObject.CreateInstance<ItemDefinition>();
            def.name = name;

            // Set itemId via reflection
            var field = typeof(ItemDefinition).GetField("itemId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(def, name.ToLower());

            return def;
        }

        [TearDown]
        public void TearDown()
        {
            // Clean up ScriptableObjects
            foreach (var obj in Object.FindObjectsByType<ScriptableObject>(FindObjectsSortMode.None))
            {
                if (obj.name.StartsWith("Test") || obj.name == "Sword" || obj.name == "Axe" || obj.name == "Stone")
                {
                    Object.DestroyImmediate(obj);
                }
            }
        }

        #endregion
    }
}

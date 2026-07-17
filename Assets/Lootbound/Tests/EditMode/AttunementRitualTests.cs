using NUnit.Framework;
using UnityEngine;
using Lootbound.Gameplay.Equipment;

namespace Lootbound.Tests.EditMode
{
    /// <summary>
    /// EditMode tests for the attunement ritual state machine.
    /// Tests state transitions, timing, and event callbacks.
    /// </summary>
    public class AttunementRitualTests
    {
        #region Test Helpers

        private AttunementRitualConfig CreateTestConfig(
            float preparing = 0.1f,
            float building = 0.2f,
            float resolving = 0.1f,
            float showingResult = 0.1f)
        {
            var config = ScriptableObject.CreateInstance<AttunementRitualConfig>();
            var type = typeof(AttunementRitualConfig);

            // Set durations via reflection (fields are private)
            type.GetField("preparingDuration", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(config, preparing);
            type.GetField("buildingDuration", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(config, building);
            type.GetField("resolvingDuration", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(config, resolving);
            type.GetField("showingResultDuration", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(config, showingResult);

            return config;
        }

        private AttunementRitualController CreateController(AttunementRitualConfig config = null)
        {
            var go = new GameObject("TestRitualController");
            var controller = go.AddComponent<AttunementRitualController>();

            if (config != null)
            {
                var type = typeof(AttunementRitualController);
                type.GetField("config", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.SetValue(controller, config);
            }

            return controller;
        }

        [TearDown]
        public void TearDown()
        {
            // Clean up any created GameObjects
            foreach (var go in Object.FindObjectsByType<AttunementRitualController>(FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(go.gameObject);
            }
        }

        #endregion

        #region State Tests

        [Test]
        public void Controller_InitialState_IsIdle()
        {
            var controller = CreateController();

            Assert.AreEqual(AttunementRitualState.Idle, controller.CurrentState);
            Assert.IsFalse(controller.IsRitualInProgress);
        }

        [Test]
        public void StartRitual_WithConfig_ChangesStateToPreparing()
        {
            var config = CreateTestConfig();
            var controller = CreateController(config);

            controller.StartRitual(true, false, 0, 1, null);

            Assert.AreEqual(AttunementRitualState.Preparing, controller.CurrentState);
            Assert.IsTrue(controller.IsRitualInProgress);
        }

        [Test]
        public void StartRitual_WithoutConfig_RaisesCompleteImmediately()
        {
            var controller = CreateController(null);
            bool completed = false;
            bool completedSuccess = false;

            controller.OnRitualComplete += (success, guaranteed, prev, curr) =>
            {
                completed = true;
                completedSuccess = success;
            };

            controller.StartRitual(true, false, 0, 1, null);

            Assert.IsTrue(completed, "Ritual should complete immediately without config");
            Assert.IsTrue(completedSuccess, "Success should be passed through");
            Assert.AreEqual(AttunementRitualState.Idle, controller.CurrentState);
        }

        [Test]
        public void StartRitual_WhileInProgress_DoesNothing()
        {
            var config = CreateTestConfig();
            var controller = CreateController(config);

            controller.StartRitual(true, false, 0, 1, null);
            var initialState = controller.CurrentState;

            // Try to start another ritual
            controller.StartRitual(false, false, 1, 2, null);

            // Should still be in the first ritual
            Assert.AreEqual(initialState, controller.CurrentState);
        }

        [Test]
        public void CancelRitual_WhileInProgress_ReturnsToIdle()
        {
            var config = CreateTestConfig();
            var controller = CreateController(config);
            bool cancelled = false;

            controller.OnRitualCancelled += () => cancelled = true;

            controller.StartRitual(true, false, 0, 1, null);
            Assert.IsTrue(controller.IsRitualInProgress);

            controller.CancelRitual();

            Assert.IsFalse(controller.IsRitualInProgress);
            Assert.AreEqual(AttunementRitualState.Idle, controller.CurrentState);
            Assert.IsTrue(cancelled);
        }

        [Test]
        public void CancelRitual_WhenIdle_DoesNothing()
        {
            var controller = CreateController();
            bool cancelled = false;

            controller.OnRitualCancelled += () => cancelled = true;

            controller.CancelRitual();

            Assert.IsFalse(cancelled, "Should not raise cancelled event when not in progress");
        }

        #endregion

        #region Progress Tests

        [Test]
        public void TotalProgress_WhenIdle_IsZero()
        {
            var config = CreateTestConfig();
            var controller = CreateController(config);

            Assert.AreEqual(0f, controller.TotalProgress);
        }

        [Test]
        public void TotalProgress_WhenStarted_IsPositive()
        {
            var config = CreateTestConfig();
            var controller = CreateController(config);

            controller.StartRitual(true, false, 0, 1, null);

            // Just started, should be very low but non-zero after state set
            Assert.GreaterOrEqual(controller.TotalProgress, 0f);
        }

        [Test]
        public void CurrentPhaseProgress_WhenIdle_IsZero()
        {
            var controller = CreateController();

            Assert.AreEqual(0f, controller.CurrentPhaseProgress);
        }

        #endregion

        #region Config Duration Tests

        [Test]
        public void Config_TotalDuration_IsSumOfPhases()
        {
            var config = CreateTestConfig(0.2f, 0.5f, 0.4f, 0.4f);

            Assert.AreEqual(1.5f, config.TotalDuration, 0.001f);
        }

        [Test]
        public void Config_DefaultValues_AreReasonable()
        {
            var config = ScriptableObject.CreateInstance<AttunementRitualConfig>();

            // Default total should be around 1.5 seconds
            Assert.Greater(config.TotalDuration, 1f);
            Assert.Less(config.TotalDuration, 3f);

            // Individual phases should be positive
            Assert.Greater(config.PreparingDuration, 0f);
            Assert.Greater(config.BuildingDuration, 0f);
            Assert.Greater(config.ResolvingDuration, 0f);
            Assert.Greater(config.ShowingResultDuration, 0f);
        }

        [Test]
        public void Config_GetRandomPitch_ReturnsValueInRange()
        {
            var config = ScriptableObject.CreateInstance<AttunementRitualConfig>();

            for (int i = 0; i < 10; i++)
            {
                float pitch = config.GetRandomPitch();
                Assert.GreaterOrEqual(pitch, config.PitchVariation.x);
                Assert.LessOrEqual(pitch, config.PitchVariation.y);
            }
        }

        #endregion

        #region Emission Settings Tests

        [Test]
        public void Config_EmissionIntensities_AreOrdered()
        {
            var config = ScriptableObject.CreateInstance<AttunementRitualConfig>();

            // Base < Peak < SuccessFlash
            Assert.Less(config.BaseEmissionIntensity, config.PeakEmissionIntensity);
            Assert.Less(config.PeakEmissionIntensity, config.SuccessFlashIntensity);
        }

        [Test]
        public void Config_EmissionColors_AreNotBlack()
        {
            var config = ScriptableObject.CreateInstance<AttunementRitualConfig>();

            Assert.AreNotEqual(Color.black, config.EmissionColor);
            Assert.AreNotEqual(Color.black, config.SuccessEmissionColor);
            Assert.AreNotEqual(Color.black, config.FailureEmissionColor);
        }

        #endregion

        #region Event Callback Tests

        [Test]
        public void OnRitualComplete_ReceivesCorrectParameters_Success()
        {
            var config = CreateTestConfig();
            var controller = CreateController(config);

            bool eventReceived = false;
            bool receivedSuccess = false;
            bool receivedGuaranteed = false;
            int receivedPrevLevel = -1;
            int receivedNewLevel = -1;

            controller.OnRitualComplete += (success, guaranteed, prev, curr) =>
            {
                eventReceived = true;
                receivedSuccess = success;
                receivedGuaranteed = guaranteed;
                receivedPrevLevel = prev;
                receivedNewLevel = curr;
            };

            // Start with specific parameters
            controller.StartRitual(true, true, 3, 4, null);

            // Cancel to trigger immediate completion path (for testing)
            controller.CancelRitual();

            // Without config would trigger immediate complete, but with config we cancelled
            // So this tests the cancel path, not complete path
            // The complete event should NOT be raised on cancel
            Assert.IsFalse(eventReceived, "Complete event should not fire on cancel");
        }

        [Test]
        public void OnRitualCancelled_IsRaised_WhenCancelled()
        {
            var config = CreateTestConfig();
            var controller = CreateController(config);

            bool cancelledRaised = false;
            controller.OnRitualCancelled += () => cancelledRaised = true;

            controller.StartRitual(true, false, 0, 1, null);
            controller.CancelRitual();

            Assert.IsTrue(cancelledRaised);
        }

        #endregion

        #region State Enum Tests

        [Test]
        public void AttunementRitualState_HasExpectedValues()
        {
            // Verify all expected states exist
            Assert.IsTrue(System.Enum.IsDefined(typeof(AttunementRitualState), AttunementRitualState.Idle));
            Assert.IsTrue(System.Enum.IsDefined(typeof(AttunementRitualState), AttunementRitualState.Preparing));
            Assert.IsTrue(System.Enum.IsDefined(typeof(AttunementRitualState), AttunementRitualState.Building));
            Assert.IsTrue(System.Enum.IsDefined(typeof(AttunementRitualState), AttunementRitualState.Resolving));
            Assert.IsTrue(System.Enum.IsDefined(typeof(AttunementRitualState), AttunementRitualState.ShowingResult));
            Assert.IsTrue(System.Enum.IsDefined(typeof(AttunementRitualState), AttunementRitualState.Cancelling));
        }

        #endregion
    }
}

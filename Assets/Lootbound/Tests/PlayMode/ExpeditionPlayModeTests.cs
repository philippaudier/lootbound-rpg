using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Lootbound.Gameplay.Expeditions;

namespace Lootbound.Tests.PlayMode
{
    /// <summary>
    /// PlayMode tests for expedition lifecycle MonoBehaviour.
    /// Tests require running in Play mode to use Unity coroutines.
    /// </summary>
    public class ExpeditionPlayModeTests
    {
        private GameObject testObject;
        private ExpeditionLifecycle lifecycle;

        [SetUp]
        public void SetUp()
        {
            testObject = new GameObject("TestExpeditionLifecycle");
            lifecycle = testObject.AddComponent<ExpeditionLifecycle>();

            // Add a minimal player transform for testing
            var playerObj = new GameObject("TestPlayer");
            playerObj.transform.position = Vector3.zero;

            // Set player transform via reflection
            var field = typeof(ExpeditionLifecycle).GetField("playerTransform",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(lifecycle, playerObj.transform);
        }

        [TearDown]
        public void TearDown()
        {
            if (testObject != null)
            {
                Object.Destroy(testObject);
            }

            // Clean up any test players
            var testPlayers = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
            foreach (var t in testPlayers)
            {
                if (t.gameObject.name == "TestPlayer")
                {
                    Object.Destroy(t.gameObject);
                }
            }
        }

        [UnityTest]
        public IEnumerator Lifecycle_StartExpedition_CreatesSession()
        {
            yield return null; // Wait one frame for Awake

            bool result = lifecycle.StartExpedition();

            Assert.IsTrue(result);
            Assert.IsNotNull(lifecycle.CurrentSession);
            Assert.AreEqual(ExpeditionState.Preparing, lifecycle.State);
        }

        [UnityTest]
        public IEnumerator Lifecycle_Depart_TransitionsToActive()
        {
            yield return null;

            lifecycle.StartExpedition();
            bool result = lifecycle.Depart();

            Assert.IsTrue(result);
            Assert.AreEqual(ExpeditionState.Active, lifecycle.State);
            Assert.IsNotNull(lifecycle.CurrentSession.Snapshot);
        }

        [UnityTest]
        public IEnumerator Lifecycle_FullCycle_CompletesSuccessfully()
        {
            yield return null;

            // Track events
            ExpeditionState lastOldState = ExpeditionState.None;
            ExpeditionState lastNewState = ExpeditionState.None;
            ExpeditionOutcome? outcome = null;

            lifecycle.OnStateChanged += (old, newState) =>
            {
                lastOldState = old;
                lastNewState = newState;
            };

            lifecycle.OnExpeditionEnded += (session, result) =>
            {
                outcome = result;
            };

            // Run through full lifecycle
            lifecycle.StartExpedition();
            yield return null;

            lifecycle.Depart();
            yield return null;

            lifecycle.BeginReturn();
            yield return null;

            lifecycle.CompleteExpedition();
            yield return null;

            Assert.AreEqual(ExpeditionState.Completed, lifecycle.State);
            Assert.AreEqual(ExpeditionState.Returning, lastOldState);
            Assert.AreEqual(ExpeditionState.Completed, lastNewState);
            Assert.AreEqual(ExpeditionOutcome.Success, outcome);
        }

        [UnityTest]
        public IEnumerator Lifecycle_TracksDuration_WhenActive()
        {
            yield return null;

            lifecycle.StartExpedition();
            lifecycle.Depart();

            // Wait a bit
            yield return new WaitForSeconds(0.2f);

            float duration = lifecycle.CurrentSession.Metrics.Duration;
            Assert.Greater(duration, 0.1f);
        }

        [UnityTest]
        public IEnumerator Lifecycle_TracksDistance_WhenActive()
        {
            yield return null;

            lifecycle.StartExpedition();
            lifecycle.Depart();

            // Move the player
            var playerTransform = testObject.GetComponent<ExpeditionLifecycle>()
                .GetType()
                .GetField("playerTransform", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.GetValue(lifecycle) as Transform;

            if (playerTransform != null)
            {
                playerTransform.position = new Vector3(10f, 0f, 0f);
            }

            // Wait for distance sample
            yield return new WaitForSeconds(0.6f);

            float maxDistance = lifecycle.CurrentSession.Metrics.MaxDistance;
            Assert.Greater(maxDistance, 5f);
        }

        [UnityTest]
        public IEnumerator Lifecycle_CancelExpedition_TransitionsToCancelled()
        {
            yield return null;

            lifecycle.StartExpedition();
            lifecycle.Depart();

            bool result = lifecycle.CancelExpedition();

            Assert.IsTrue(result);
            Assert.AreEqual(ExpeditionState.Cancelled, lifecycle.State);
            Assert.AreEqual(ExpeditionOutcome.Cancelled, lifecycle.CurrentSession.Outcome);
        }

        [UnityTest]
        public IEnumerator Lifecycle_ClearSession_RemovesCompletedSession()
        {
            yield return null;

            lifecycle.StartExpedition();
            lifecycle.Depart();
            lifecycle.CompleteExpedition();

            lifecycle.ClearSession();

            Assert.IsNull(lifecycle.CurrentSession);
            Assert.AreEqual(ExpeditionState.None, lifecycle.State);
        }

        [UnityTest]
        public IEnumerator Lifecycle_CannotStartWhileActive()
        {
            yield return null;

            lifecycle.StartExpedition();

            bool result = lifecycle.StartExpedition();

            Assert.IsFalse(result);
        }

        [UnityTest]
        public IEnumerator Lifecycle_MetricsFrozen_AfterCompletion()
        {
            yield return null;

            lifecycle.StartExpedition();
            lifecycle.Depart();

            yield return new WaitForSeconds(0.1f);

            lifecycle.CompleteExpedition();

            float durationAtCompletion = lifecycle.CurrentSession.Metrics.Duration;

            yield return new WaitForSeconds(0.2f);

            // Duration should not have changed
            Assert.AreEqual(durationAtCompletion, lifecycle.CurrentSession.Metrics.Duration, 0.001f);
        }
    }
}

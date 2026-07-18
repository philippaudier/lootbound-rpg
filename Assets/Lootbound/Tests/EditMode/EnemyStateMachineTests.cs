using NUnit.Framework;
using Lootbound.Gameplay.Combat;

namespace Lootbound.Tests.EditMode
{
    /// <summary>
    /// Pure state machine tests: transitions, reasons, durations, publication
    /// contract. Time is always injected - no Time.time, no framerate.
    /// </summary>
    public class EnemyStateMachineTests
    {
        [Test]
        public void InitialState_IsIdle_WithNoReason()
        {
            var sm = new EnemyStateMachine(EnemyState.Idle, now: 10f);

            Assert.AreEqual(EnemyState.Idle, sm.CurrentState);
            Assert.AreEqual(EnemyState.Idle, sm.PreviousState);
            Assert.AreEqual(EnemyTransitionReason.None, sm.LastReason);
            Assert.AreEqual(0f, sm.StateDuration(10f), 0.0001f);
        }

        [Test]
        public void Transition_RecordsPreviousStateReasonAndTime()
        {
            var sm = new EnemyStateMachine(EnemyState.Idle, 0f);

            bool changed = sm.TryTransition(EnemyState.Wandering, EnemyTransitionReason.RoamingStarted, now: 2f);

            Assert.IsTrue(changed);
            Assert.AreEqual(EnemyState.Wandering, sm.CurrentState);
            Assert.AreEqual(EnemyState.Idle, sm.PreviousState);
            Assert.AreEqual(EnemyTransitionReason.RoamingStarted, sm.LastReason);
            Assert.AreEqual(3f, sm.StateDuration(5f), 0.0001f);
        }

        [Test]
        public void SameStateTransition_IsRejected_AndNotPublished()
        {
            var sm = new EnemyStateMachine(EnemyState.Idle, 0f);
            int published = 0;
            sm.OnTransition += _ => published++;

            Assert.IsFalse(sm.TryTransition(EnemyState.Idle, EnemyTransitionReason.RoamingStarted, 1f));
            Assert.AreEqual(0, published);
        }

        [Test]
        public void Dead_IsAbsorbing()
        {
            var sm = new EnemyStateMachine(EnemyState.Chasing, 0f);
            sm.TryTransition(EnemyState.Dead, EnemyTransitionReason.Died, 1f);

            Assert.IsFalse(sm.TryTransition(EnemyState.Idle, EnemyTransitionReason.RoamingStarted, 2f));
            Assert.AreEqual(EnemyState.Dead, sm.CurrentState);
        }

        [Test]
        public void Transition_IsPublishedExactlyOnce_WithCorrectPayload()
        {
            var sm = new EnemyStateMachine(EnemyState.Suspicious, 0f);
            int published = 0;
            EnemyStateTransition last = default;
            sm.OnTransition += t => { published++; last = t; };

            sm.TryTransition(EnemyState.Chasing, EnemyTransitionReason.SuspicionConfirmed, 3f);

            Assert.AreEqual(1, published);
            Assert.AreEqual(EnemyState.Suspicious, last.Previous);
            Assert.AreEqual(EnemyState.Chasing, last.Current);
            Assert.AreEqual(EnemyTransitionReason.SuspicionConfirmed, last.Reason);
            Assert.AreEqual(3f, last.Time, 0.0001f);
        }

        [Test]
        public void ExpectedNavigationLoop_TransitionsInOrder()
        {
            // Idle -> Wandering -> Suspicious -> Chasing -> ReturningHome -> Idle
            var sm = new EnemyStateMachine(EnemyState.Idle, 0f);

            Assert.IsTrue(sm.TryTransition(EnemyState.Wandering, EnemyTransitionReason.RoamingStarted, 1f));
            Assert.IsTrue(sm.TryTransition(EnemyState.Suspicious, EnemyTransitionReason.TargetSpotted, 2f));
            Assert.IsTrue(sm.TryTransition(EnemyState.Chasing, EnemyTransitionReason.SuspicionConfirmed, 3f));
            Assert.IsTrue(sm.TryTransition(EnemyState.ReturningHome, EnemyTransitionReason.LeashExceeded, 4f));
            Assert.IsTrue(sm.TryTransition(EnemyState.Idle, EnemyTransitionReason.ArrivedHome, 5f));

            Assert.AreEqual(EnemyState.Idle, sm.CurrentState);
            Assert.AreEqual(EnemyState.ReturningHome, sm.PreviousState);
            Assert.AreEqual(EnemyTransitionReason.ArrivedHome, sm.LastReason);
        }

        [Test]
        public void StateDuration_IsFramerateIndependent()
        {
            // Duration depends only on the provided timestamps.
            var sm = new EnemyStateMachine(EnemyState.Idle, 0f);
            sm.TryTransition(EnemyState.Suspicious, EnemyTransitionReason.TargetSpotted, 100f);

            Assert.AreEqual(0.5f, sm.StateDuration(100.5f), 0.0001f);
            Assert.AreEqual(42f, sm.StateDuration(142f), 0.0001f);
        }
    }
}

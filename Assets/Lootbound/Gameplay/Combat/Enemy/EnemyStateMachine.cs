using System;

namespace Lootbound.Gameplay.Combat
{
    /// <summary>
    /// Why a state transition happened. Lightweight diagnostics carried with
    /// every transition; shown in debug, never consumed by gameplay balance.
    /// </summary>
    public enum EnemyTransitionReason
    {
        None,
        RoamingStarted,
        DestinationReached,
        TargetSpotted,
        SuspicionConfirmed,
        TargetLost,
        AttackStarted,
        AttackPhase,
        AttackFinished,
        LeashExceeded,
        LostSightTimeout,
        AttackedWhileReturning,
        ArrivedHome,
        Staggered,
        StaggerRecovered,
        StuckDetected,
        StuckRecovered,
        Died
    }

    /// <summary>
    /// One recorded state transition.
    /// </summary>
    public readonly struct EnemyStateTransition
    {
        public EnemyState Previous { get; }
        public EnemyState Current { get; }
        public EnemyTransitionReason Reason { get; }
        public float Time { get; }

        public EnemyStateTransition(EnemyState previous, EnemyState current, EnemyTransitionReason reason, float time)
        {
            Previous = previous;
            Current = current;
            Reason = reason;
            Time = time;
        }

        public override string ToString() => $"{Previous} -> {Current} ({Reason})";
    }

    /// <summary>
    /// The single authority for the enemy's current state. Pure C#: time is
    /// always provided by the caller, so transitions and durations are fully
    /// testable in EditMode. Publishes each transition exactly once.
    /// </summary>
    public sealed class EnemyStateMachine
    {
        public EnemyState CurrentState { get; private set; }
        public EnemyState PreviousState { get; private set; }
        public EnemyTransitionReason LastReason { get; private set; } = EnemyTransitionReason.None;
        public float StateEnteredAt { get; private set; }

        public event Action<EnemyStateTransition> OnTransition;

        public EnemyStateMachine(EnemyState initialState = EnemyState.Idle, float now = 0f)
        {
            CurrentState = initialState;
            PreviousState = initialState;
            StateEnteredAt = now;
        }

        public float StateDuration(float now) => now - StateEnteredAt;

        /// <summary>
        /// Attempt a transition. Returns false (and publishes nothing) when
        /// the transition is redundant or forbidden. Dead is absorbing.
        /// </summary>
        public bool TryTransition(EnemyState to, EnemyTransitionReason reason, float now)
        {
            if (CurrentState == EnemyState.Dead)
            {
                return false;
            }

            if (to == CurrentState)
            {
                return false;
            }

            PreviousState = CurrentState;
            CurrentState = to;
            LastReason = reason;
            StateEnteredAt = now;

            OnTransition?.Invoke(new EnemyStateTransition(PreviousState, CurrentState, reason, now));
            return true;
        }
    }
}

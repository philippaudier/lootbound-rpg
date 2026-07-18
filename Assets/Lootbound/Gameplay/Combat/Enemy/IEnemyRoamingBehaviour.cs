using UnityEngine;

namespace Lootbound.Gameplay.Combat
{
    /// <summary>
    /// Delegate abstracting NavMesh.SamplePosition so roaming behaviours stay
    /// pure C# and testable with a fake sampler.
    /// </summary>
    public delegate bool NavigationSampleDelegate(Vector3 position, float maxDistance, out Vector3 sampled);

    /// <summary>
    /// How an enemy occupies its territory when no target is involved.
    /// EnemyBrain orchestrates one implementation (wander or patrol today;
    /// future slices can add pack/guard/camp behaviours without touching the
    /// brain). Implementations decide WHERE to go and WHEN; the brain applies
    /// movement through the NavMeshAgent and owns the state machine.
    /// </summary>
    public interface IEnemyRoamingBehaviour
    {
        /// <summary>State the brain enters while moving for this behaviour.</summary>
        EnemyState MovingState { get; }

        /// <summary>Display name for diagnostics (e.g. "Wander", "Patrol").</summary>
        string ModeName { get; }

        /// <summary>
        /// Called whenever roaming (re)starts: after spawn, after returning
        /// home, after a stuck recovery. Schedules the next decision.
        /// </summary>
        void OnRoamingResumed(float now);

        /// <summary>
        /// Polled while resting. Returns true when the enemy should start
        /// moving to <paramref name="destination"/> now; false to keep
        /// resting (the behaviour manages its own pacing and retries).
        /// </summary>
        bool TryGetNextDestination(Vector3 homePosition, Vector3 currentPosition, float now, out Vector3 destination);

        /// <summary>Called when the current destination was reached.</summary>
        void OnDestinationReached(float now);
    }
}

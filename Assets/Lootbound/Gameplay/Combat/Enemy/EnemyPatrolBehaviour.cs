using System.Collections.Generic;
using UnityEngine;

namespace Lootbound.Gameplay.Combat
{
    /// <summary>
    /// Optional explicit patrol along a list of world-space points. Pure C#
    /// (points, time, randomness and sampling injected). Supports loop and
    /// ping-pong traversal, a short dwell at each point, and skips invalid
    /// points cleanly (bounded, never an infinite loop).
    /// </summary>
    public sealed class EnemyPatrolBehaviour : IEnemyRoamingBehaviour
    {
        private readonly IReadOnlyList<Vector3> points;
        private readonly bool pingPong;
        private readonly float dwellSeconds;
        private readonly float sampleDistance;
        private readonly System.Random random;
        private readonly NavigationSampleDelegate sampleNavMesh;

        private int currentIndex = -1;
        private int direction = 1;
        private float nextDecisionAt;

        public EnemyState MovingState => EnemyState.Patrolling;
        public string ModeName => "Patrol";

        /// <summary>Index of the last point selected (diagnostics/tests).</summary>
        public int CurrentIndex => currentIndex;

        public EnemyPatrolBehaviour(
            IReadOnlyList<Vector3> points,
            bool pingPong,
            float dwellSeconds,
            float sampleDistance,
            System.Random random,
            NavigationSampleDelegate sampleNavMesh)
        {
            this.points = points ?? new List<Vector3>();
            this.pingPong = pingPong;
            this.dwellSeconds = Mathf.Max(0f, dwellSeconds);
            this.sampleDistance = sampleDistance;
            this.random = random;
            this.sampleNavMesh = sampleNavMesh;
        }

        public void OnRoamingResumed(float now)
        {
            // Small deterministic-per-instance stagger so patrols do not tick in sync.
            nextDecisionAt = now + dwellSeconds * (float)random.NextDouble();
        }

        public void OnDestinationReached(float now)
        {
            nextDecisionAt = now + dwellSeconds;
        }

        public bool TryGetNextDestination(Vector3 homePosition, Vector3 currentPosition, float now, out Vector3 destination)
        {
            destination = default;

            if (points.Count == 0 || now < nextDecisionAt)
            {
                return false;
            }

            // Bounded: try at most one full pass over the route, skipping
            // points that fail NavMesh validation.
            for (int skip = 0; skip < points.Count; skip++)
            {
                AdvanceIndex();

                if (sampleNavMesh(points[currentIndex], sampleDistance, out Vector3 sampled))
                {
                    destination = sampled;
                    return true;
                }
            }

            // Every point invalid: rest and retry later, never spin.
            nextDecisionAt = now + Mathf.Max(1f, dwellSeconds);
            return false;
        }

        private void AdvanceIndex()
        {
            if (points.Count == 1)
            {
                currentIndex = 0;
                return;
            }

            if (pingPong)
            {
                int next = currentIndex + direction;
                if (next >= points.Count || next < 0)
                {
                    direction = -direction;
                    next = currentIndex + direction;
                }
                currentIndex = Mathf.Clamp(next, 0, points.Count - 1);
            }
            else
            {
                currentIndex = (currentIndex + 1) % points.Count;
            }
        }
    }
}

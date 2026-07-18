using System.Collections.Generic;
using UnityEngine;

namespace Lootbound.Gameplay.Combat
{
    /// <summary>
    /// Optional authored patrol route: local-space points resolved to world
    /// space at runtime (relative to the spawn transform, so a route works
    /// wherever the enemy is placed). When present next to EnemyBrain and the
    /// navigation profile selects Patrol, the brain patrols instead of
    /// wandering. Not generated from the WorldLayout - authoring only in V1.
    /// </summary>
    public sealed class EnemyPatrolRoute : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Points in local space, relative to this transform at spawn")]
        private List<Vector3> localPoints = new List<Vector3>();

        [SerializeField]
        [Tooltip("Traverse back and forth instead of looping")]
        private bool pingPong;

        [SerializeField]
        [Tooltip("Wait at each point before moving on")]
        private float dwellSeconds = 2f;

        public bool PingPong => pingPong;
        public float DwellSeconds => dwellSeconds;
        public int PointCount => localPoints.Count;

        /// <summary>
        /// Resolve the route to world space using the current transform.
        /// Call once at initialization (before the enemy starts moving).
        /// </summary>
        public List<Vector3> ResolveWorldPoints()
        {
            var world = new List<Vector3>(localPoints.Count);
            foreach (var local in localPoints)
            {
                world.Add(transform.TransformPoint(local));
            }
            return world;
        }

        private void OnDrawGizmosSelected()
        {
            if (localPoints.Count == 0) return;

            Gizmos.color = new Color(0.4f, 0.7f, 1f, 0.9f);
            Vector3 previous = transform.TransformPoint(localPoints[0]);
            Gizmos.DrawSphere(previous, 0.3f);

            for (int i = 1; i < localPoints.Count; i++)
            {
                Vector3 current = transform.TransformPoint(localPoints[i]);
                Gizmos.DrawSphere(current, 0.3f);
                Gizmos.DrawLine(previous, current);
                previous = current;
            }

            if (!pingPong && localPoints.Count > 2)
            {
                Gizmos.DrawLine(previous, transform.TransformPoint(localPoints[0]));
            }
        }
    }
}

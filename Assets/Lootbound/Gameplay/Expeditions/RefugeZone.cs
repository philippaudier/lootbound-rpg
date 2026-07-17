using System;
using UnityEngine;
using Lootbound.Gameplay.Combat;

namespace Lootbound.Gameplay.Expeditions
{
    /// <summary>
    /// Defines the safe refuge area.
    /// The refuge is where the player prepares, repairs, and rests.
    /// This is not a generic zone system — there is only one refuge.
    /// </summary>
    public class RefugeZone : MonoBehaviour
    {
        [Header("Zone Definition")]
        [Tooltip("Center of the refuge zone (local to this transform).")]
        [SerializeField] private Vector3 centerOffset = Vector3.zero;

        [Tooltip("Radius of the safe zone.")]
        [SerializeField] private float radius = 15f;

        [Header("Identity")]
        [Tooltip("Display name for this refuge.")]
        [SerializeField] private string refugeName = "The Refuge";

        [Header("References")]
        [Tooltip("Player transform to track. Auto-finds if not set.")]
        [SerializeField] private Transform playerTransform;

        [Header("Debug")]
        [SerializeField] private bool drawGizmos = true;
        [SerializeField] private Color gizmoColor = new Color(0.2f, 0.8f, 0.4f, 0.3f);

        private bool playerInside;
        private bool wasInsideLastFrame;

        /// <summary>
        /// Center of the refuge in world space.
        /// </summary>
        public Vector3 Center => transform.position + centerOffset;

        /// <summary>
        /// Radius of the safe zone.
        /// </summary>
        public float Radius => radius;

        /// <summary>
        /// Display name of this refuge.
        /// </summary>
        public string RefugeName => refugeName;

        /// <summary>
        /// Whether the player is currently inside the refuge.
        /// </summary>
        public bool IsPlayerInside => playerInside;

        /// <summary>
        /// Fired when the player enters the refuge zone.
        /// </summary>
        public event Action OnPlayerEntered;

        /// <summary>
        /// Fired when the player exits the refuge zone.
        /// </summary>
        public event Action OnPlayerExited;

        private void Awake()
        {
            if (playerTransform == null)
            {
                var playerHealth = FindFirstObjectByType<PlayerHealth>();
                if (playerHealth != null)
                {
                    playerTransform = playerHealth.transform;
                }
            }
        }

        private void Update()
        {
            if (playerTransform == null) return;

            // Check if player is inside the refuge
            float distance = GetHorizontalDistance(playerTransform.position, Center);
            playerInside = distance <= radius;

            // Detect transitions
            if (playerInside && !wasInsideLastFrame)
            {
                OnPlayerEntered?.Invoke();
            }
            else if (!playerInside && wasInsideLastFrame)
            {
                OnPlayerExited?.Invoke();
            }

            wasInsideLastFrame = playerInside;
        }

        /// <summary>
        /// Check if a world position is inside the refuge.
        /// </summary>
        public bool IsPositionInside(Vector3 worldPosition)
        {
            float distance = GetHorizontalDistance(worldPosition, Center);
            return distance <= radius;
        }

        /// <summary>
        /// Get horizontal (XZ) distance from center.
        /// </summary>
        public float GetDistanceFromCenter(Vector3 worldPosition)
        {
            return GetHorizontalDistance(worldPosition, Center);
        }

        private float GetHorizontalDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>
        /// Set the player transform explicitly.
        /// </summary>
        public void SetPlayerTransform(Transform player)
        {
            playerTransform = player;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!drawGizmos) return;

            // Draw refuge zone
            Gizmos.color = gizmoColor;
            DrawCircle(Center, radius, 32);

            // Draw filled disc
            Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, gizmoColor.a * 0.3f);
            Gizmos.DrawSphere(Center, radius * 0.1f); // Small sphere at center

            // Label
            UnityEditor.Handles.Label(Center + Vector3.up * 2f, refugeName);
        }

        private void OnDrawGizmosSelected()
        {
            // Draw stronger outline when selected
            Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 1f);
            DrawCircle(Center, radius, 64);
        }

        private void DrawCircle(Vector3 center, float r, int segments)
        {
            float angleStep = 360f / segments;
            Vector3 prevPoint = center + new Vector3(r, 0f, 0f);

            for (int i = 1; i <= segments; i++)
            {
                float angle = i * angleStep * Mathf.Deg2Rad;
                Vector3 nextPoint = center + new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r);
                Gizmos.DrawLine(prevPoint, nextPoint);
                prevPoint = nextPoint;
            }
        }
#endif
    }
}

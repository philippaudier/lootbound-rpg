using System;
using UnityEngine;
using Lootbound.Core.Logging;
using Lootbound.Gameplay.Combat;

namespace Lootbound.Gameplay.Expeditions
{
    /// <summary>
    /// The physical boundary between the refuge and the outside world.
    /// Place this on an arch, door, bridge, or path.
    ///
    /// When the player crosses this boundary:
    /// - Outward: Starts/departs the expedition
    /// - Inward: Completes the expedition
    ///
    /// The boundary does NOT block movement or trigger scene loads.
    /// It is a contextual threshold, not a physical barrier.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ExpeditionBoundary : MonoBehaviour
    {
        private const string Category = "Boundary";

        /// <summary>
        /// Which side of the boundary the player is on.
        /// </summary>
        public enum BoundarySide
        {
            Unknown,
            Refuge,
            Outside
        }

        [Header("Direction")]
        [Tooltip("Direction pointing toward the refuge (local space). The opposite direction is 'outside'.")]
        [SerializeField] private Vector3 refugeDirection = Vector3.back;

        [Header("Anti-Spam")]
        [Tooltip("Minimum time between crossings to prevent spam.")]
        [SerializeField] private float crossingCooldown = 1f;

        [Tooltip("Minimum distance player must move past boundary to count as crossed.")]
        [SerializeField] private float minimumCrossDistance = 0.5f;

        [Header("References")]
        [SerializeField] private ExpeditionLifecycle expeditionLifecycle;
        [SerializeField] private RefugeZone refugeZone;

        [Header("Auto-Start")]
        [Tooltip("Automatically start expedition when player exits refuge (if not already started).")]
        [SerializeField] private bool autoStartExpedition = true;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = true;
        [SerializeField] private bool drawGizmos = true;

        private Transform playerTransform;
        private BoundarySide currentSide = BoundarySide.Unknown;
        private BoundarySide lastConfirmedSide = BoundarySide.Unknown;
        private float lastCrossingTime;
        private int crossingCount;
        private Vector3 lastCrossingDirection;
        private bool playerInTrigger;

        /// <summary>
        /// Current side of the boundary the player is on.
        /// </summary>
        public BoundarySide CurrentSide => currentSide;

        /// <summary>
        /// Last confirmed side before entering the trigger.
        /// </summary>
        public BoundarySide LastConfirmedSide => lastConfirmedSide;

        /// <summary>
        /// Time remaining on crossing cooldown.
        /// </summary>
        public float CooldownRemaining => Mathf.Max(0f, crossingCooldown - (Time.time - lastCrossingTime));

        /// <summary>
        /// Whether cooldown is active.
        /// </summary>
        public bool IsOnCooldown => CooldownRemaining > 0f;

        /// <summary>
        /// Total crossings since scene load.
        /// </summary>
        public int CrossingCount => crossingCount;

        /// <summary>
        /// Direction of the last crossing (normalized).
        /// </summary>
        public Vector3 LastCrossingDirection => lastCrossingDirection;

        /// <summary>
        /// Fired when player crosses toward outside (departure).
        /// </summary>
        public event Action OnDeparture;

        /// <summary>
        /// Fired when player crosses toward refuge (return).
        /// </summary>
        public event Action OnReturn;

        private void Awake()
        {
            // Ensure collider is a trigger
            var collider = GetComponent<Collider>();
            if (collider != null && !collider.isTrigger)
            {
                collider.isTrigger = true;
                Log("Collider set to trigger mode");
            }

            FindReferences();
        }

        private void Start()
        {
            // Determine initial side based on player position
            if (playerTransform != null)
            {
                currentSide = DetermineSide(playerTransform.position);
                lastConfirmedSide = currentSide;
                Log($"Initial side: {currentSide}");
            }
        }

        private void FindReferences()
        {
            if (expeditionLifecycle == null)
            {
                expeditionLifecycle = FindFirstObjectByType<ExpeditionLifecycle>();
            }

            if (refugeZone == null)
            {
                refugeZone = FindFirstObjectByType<RefugeZone>();
            }

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

            // Continuously update current side when not in trigger
            if (!playerInTrigger)
            {
                currentSide = DetermineSide(playerTransform.position);
                lastConfirmedSide = currentSide;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsPlayer(other)) return;

            playerInTrigger = true;

            // Record side when entering trigger
            if (playerTransform != null)
            {
                lastConfirmedSide = DetermineSide(playerTransform.position);
                Log($"Player entered boundary trigger from {lastConfirmedSide}");
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (!IsPlayer(other)) return;

            playerInTrigger = false;

            if (playerTransform == null) return;

            // Determine new side
            BoundarySide newSide = DetermineSide(playerTransform.position);

            // Check if actually crossed (different side)
            if (newSide != lastConfirmedSide && newSide != BoundarySide.Unknown)
            {
                // Check cooldown
                if (IsOnCooldown)
                {
                    Log($"Crossing blocked by cooldown ({CooldownRemaining:F2}s remaining)");
                    return;
                }

                // Check minimum distance
                float distanceFromBoundary = GetSignedDistanceFromBoundary(playerTransform.position);
                if (Mathf.Abs(distanceFromBoundary) < minimumCrossDistance)
                {
                    Log($"Crossing blocked - not far enough ({Mathf.Abs(distanceFromBoundary):F2}m < {minimumCrossDistance}m)");
                    return;
                }

                // Valid crossing!
                HandleCrossing(lastConfirmedSide, newSide);
            }

            currentSide = newSide;
            lastConfirmedSide = newSide;
        }

        private void HandleCrossing(BoundarySide from, BoundarySide to)
        {
            crossingCount++;
            lastCrossingTime = Time.time;
            lastCrossingDirection = (to == BoundarySide.Outside) ? -GetRefugeDirectionWorld() : GetRefugeDirectionWorld();

            Log($"Boundary crossed: {from} → {to}");

            if (to == BoundarySide.Outside)
            {
                // Departing
                HandleDeparture();
            }
            else if (to == BoundarySide.Refuge)
            {
                // Returning
                HandleReturn();
            }
        }

        private void HandleDeparture()
        {
            if (expeditionLifecycle == null)
            {
                Log("No ExpeditionLifecycle found");
                return;
            }

            var state = expeditionLifecycle.State;

            // Auto-start if needed
            if (autoStartExpedition && state == ExpeditionState.None)
            {
                Log("Auto-starting expedition");
                expeditionLifecycle.StartExpedition();
                state = expeditionLifecycle.State;
            }

            // Depart if in Preparing
            if (state == ExpeditionState.Preparing)
            {
                bool success = expeditionLifecycle.Depart();
                if (success)
                {
                    Log("Expedition departed!");
                    OnDeparture?.Invoke();
                }
            }
            else
            {
                Log($"Cannot depart from state {state}");
            }
        }

        private void HandleReturn()
        {
            if (expeditionLifecycle == null)
            {
                Log("No ExpeditionLifecycle found");
                return;
            }

            var state = expeditionLifecycle.State;

            // Complete if Active or Returning
            if (state == ExpeditionState.Active || state == ExpeditionState.Returning)
            {
                // First transition to Returning if Active
                if (state == ExpeditionState.Active)
                {
                    expeditionLifecycle.BeginReturn();
                }

                bool success = expeditionLifecycle.CompleteExpedition();
                if (success)
                {
                    Log("Expedition completed - safe return!");
                    OnReturn?.Invoke();
                }
            }
            else
            {
                Log($"Cannot complete from state {state}");
            }
        }

        /// <summary>
        /// Determine which side of the boundary a position is on.
        /// </summary>
        public BoundarySide DetermineSide(Vector3 worldPosition)
        {
            float signedDistance = GetSignedDistanceFromBoundary(worldPosition);

            // Positive = refuge side, Negative = outside
            if (signedDistance > 0)
            {
                return BoundarySide.Refuge;
            }
            else if (signedDistance < 0)
            {
                return BoundarySide.Outside;
            }

            return BoundarySide.Unknown;
        }

        /// <summary>
        /// Get signed distance from the boundary plane.
        /// Positive = refuge side, Negative = outside side.
        /// </summary>
        public float GetSignedDistanceFromBoundary(Vector3 worldPosition)
        {
            Vector3 toPosition = worldPosition - transform.position;
            Vector3 refugeDir = GetRefugeDirectionWorld();
            return Vector3.Dot(toPosition, refugeDir);
        }

        /// <summary>
        /// Get the refuge direction in world space.
        /// </summary>
        public Vector3 GetRefugeDirectionWorld()
        {
            return transform.TransformDirection(refugeDirection.normalized);
        }

        private bool IsPlayer(Collider other)
        {
            // Check if this is the player by looking for PlayerHealth component
            return other.GetComponent<PlayerHealth>() != null
                || other.GetComponentInParent<PlayerHealth>() != null;
        }

        private void Log(string message)
        {
            if (enableDebugLogs)
            {
                LootboundLog.Info(Category, message);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!drawGizmos) return;

            // Draw boundary plane
            Vector3 center = transform.position;
            Vector3 refugeDir = GetRefugeDirectionWorld();
            Vector3 right = Vector3.Cross(Vector3.up, refugeDir).normalized;
            if (right.sqrMagnitude < 0.01f)
            {
                right = Vector3.Cross(Vector3.forward, refugeDir).normalized;
            }

            float size = 3f;

            // Draw boundary line
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(center - right * size, center + right * size);

            // Draw refuge direction arrow (green)
            Gizmos.color = new Color(0.2f, 0.9f, 0.3f);
            DrawArrow(center, refugeDir * 2f);
            UnityEditor.Handles.Label(center + refugeDir * 2.5f, "REFUGE");

            // Draw outside direction arrow (red)
            Gizmos.color = new Color(0.9f, 0.3f, 0.2f);
            DrawArrow(center, -refugeDir * 2f);
            UnityEditor.Handles.Label(center - refugeDir * 2.5f, "OUTSIDE");

            // Draw minimum cross distance
            Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
            Gizmos.DrawLine(center + refugeDir * minimumCrossDistance - right * size,
                           center + refugeDir * minimumCrossDistance + right * size);
            Gizmos.DrawLine(center - refugeDir * minimumCrossDistance - right * size,
                           center - refugeDir * minimumCrossDistance + right * size);
        }

        private void DrawArrow(Vector3 from, Vector3 direction)
        {
            Vector3 to = from + direction;
            Gizmos.DrawLine(from, to);

            Vector3 right = Vector3.Cross(Vector3.up, direction.normalized) * 0.3f;
            Vector3 arrowHead1 = to - direction.normalized * 0.4f + right;
            Vector3 arrowHead2 = to - direction.normalized * 0.4f - right;

            Gizmos.DrawLine(to, arrowHead1);
            Gizmos.DrawLine(to, arrowHead2);
        }
#endif
    }
}

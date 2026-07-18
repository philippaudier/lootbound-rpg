using UnityEngine;

namespace Lootbound.Gameplay.Combat
{
    /// <summary>
    /// How an enemy occupies its territory when nothing is happening.
    /// </summary>
    public enum EnemyRoamingMode
    {
        Wander,
        Patrol
    }

    /// <summary>
    /// Navigation behaviour profile, shared between enemy types (the same
    /// profile can serve several EnemyConfigs). Speeds are multipliers of
    /// EnemyConfig.MoveSpeed so movement values never contradict each other.
    /// Detection range and field of view stay on EnemyConfig (already there).
    /// </summary>
    [CreateAssetMenu(fileName = "EnemyNavigationProfile", menuName = "Lootbound/Combat/Enemy Navigation Profile")]
    public class EnemyNavigationProfile : ScriptableObject
    {
        [Header("Roaming")]
        [Tooltip("Wander freely around HomePosition, or follow an explicit EnemyPatrolRoute when present.")]
        [SerializeField] private EnemyRoamingMode roamingMode = EnemyRoamingMode.Wander;

        [Tooltip("Radius around HomePosition for wander destinations.")]
        [SerializeField] private float wanderRadius = 14f;

        [Tooltip("Minimum rest duration between wander moves.")]
        [SerializeField] private float idleDurationMin = 2.5f;

        [Tooltip("Maximum rest duration between wander moves.")]
        [SerializeField] private float idleDurationMax = 7f;

        [Tooltip("Bounded attempts to find a navigable wander point.")]
        [SerializeField] private int wanderDestinationAttempts = 4;

        [Tooltip("NavMesh.SamplePosition search distance for wander points.")]
        [SerializeField] private float wanderSampleDistance = 2f;

        [Tooltip("Distance at which a destination counts as reached.")]
        [SerializeField] private float arrivalDistance = 0.6f;

        [Tooltip("Movement speed while roaming, as a fraction of EnemyConfig.MoveSpeed.")]
        [SerializeField, Range(0.1f, 1f)] private float roamingSpeedMultiplier = 0.5f;

        [Header("Perception")]
        [Tooltip("Omnidirectional detection distance (behind-the-back awareness). Line of sight still applies.")]
        [SerializeField] private float immediateDetectionRange = 2.5f;

        [Tooltip("How long the player stays 'seen' after breaking line of sight.")]
        [SerializeField] private float loseSightDelay = 3f;

        [Tooltip("How long the player must stay visible during Suspicious before the chase starts.")]
        [SerializeField] private float suspicionDuration = 0.9f;

        [Tooltip("Seconds between perception checks (raycasts), staggered per instance.")]
        [SerializeField, Range(0.05f, 1f)] private float perceptionInterval = 0.15f;

        [Tooltip("Eye height used as the line-of-sight origin.")]
        [SerializeField] private float eyeHeight = 1.5f;

        [Tooltip("Height offset on the target aimed at by the line-of-sight ray.")]
        [SerializeField] private float targetHeightOffset = 1f;

        [Tooltip("Layers that can block line of sight. Triggers are always ignored.")]
        [SerializeField] private LayerMask lineOfSightMask = ~0;

        [Header("Chase")]
        [Tooltip("Chase speed as a fraction of EnemyConfig.MoveSpeed.")]
        [SerializeField, Range(0.5f, 2f)] private float chaseSpeedMultiplier = 1f;

        [Tooltip("Minimum seconds between destination updates while chasing.")]
        [SerializeField] private float chaseRepathInterval = 0.25f;

        [Tooltip("Minimum target displacement before the destination is updated.")]
        [SerializeField] private float chaseRepathDistance = 0.75f;

        [Tooltip("Chase is abandoned when the PLAYER is farther than this from HomePosition.")]
        [SerializeField] private float maxChaseDistanceFromHome = 40f;

        [Tooltip("Hysteresis margin below the leash limit required to START a chase (prevents boundary oscillation).")]
        [SerializeField] private float leashHysteresis = 4f;

        [Header("Defense")]
        [Tooltip("Short omnidirectional awareness kept during ReturningHome (line of sight still applies). Long-range passive reacquisition stays disabled while returning.")]
        [SerializeField] private float awarenessRadius = 4f;

        [Tooltip("Duration of the bounded defensive chase triggered by taking damage. During this window the chase needs no line of sight; the territorial leash still applies. Successive hits never extend an active window.")]
        [SerializeField] private float defensiveChaseDuration = 6f;

        [Header("Return")]
        [Tooltip("Return speed as a fraction of EnemyConfig.MoveSpeed.")]
        [SerializeField, Range(0.1f, 1.5f)] private float returnSpeedMultiplier = 0.85f;

        [Tooltip("Distance from HomePosition at which the return is complete.")]
        [SerializeField] private float returnCompletionDistance = 1.5f;

        [Tooltip("Seconds after arriving home during which the player is ignored.")]
        [SerializeField] private float reacquireCooldown = 2f;

        [Header("Recovery")]
        [Tooltip("Below this speed the agent is considered not moving.")]
        [SerializeField] private float stuckVelocityThreshold = 0.05f;

        [Tooltip("Seconds without progress before recovery starts.")]
        [SerializeField] private float stuckTimeout = 3f;

        [Tooltip("Bounded recovery attempts before entering Stuck.")]
        [SerializeField] private int maxRecoveryAttempts = 3;

        [Tooltip("Last-resort warp toward HomePosition when everything else failed. Rare, logged, never the normal return path.")]
        [SerializeField] private bool allowEmergencyWarp = true;

        // Roaming
        public EnemyRoamingMode RoamingMode => roamingMode;
        public float WanderRadius => wanderRadius;
        public float IdleDurationMin => idleDurationMin;
        public float IdleDurationMax => idleDurationMax;
        public int WanderDestinationAttempts => wanderDestinationAttempts;
        public float WanderSampleDistance => wanderSampleDistance;
        public float ArrivalDistance => arrivalDistance;
        public float RoamingSpeedMultiplier => roamingSpeedMultiplier;

        // Perception
        public float ImmediateDetectionRange => immediateDetectionRange;
        public float LoseSightDelay => loseSightDelay;
        public float SuspicionDuration => suspicionDuration;
        public float PerceptionInterval => perceptionInterval;
        public float EyeHeight => eyeHeight;
        public float TargetHeightOffset => targetHeightOffset;
        public LayerMask LineOfSightMask => lineOfSightMask;

        // Chase
        public float ChaseSpeedMultiplier => chaseSpeedMultiplier;
        public float ChaseRepathInterval => chaseRepathInterval;
        public float ChaseRepathDistance => chaseRepathDistance;
        public float MaxChaseDistanceFromHome => maxChaseDistanceFromHome;
        public float LeashHysteresis => leashHysteresis;

        // Defense
        public float AwarenessRadius => awarenessRadius;
        public float DefensiveChaseDuration => defensiveChaseDuration;

        // Return
        public float ReturnSpeedMultiplier => returnSpeedMultiplier;
        public float ReturnCompletionDistance => returnCompletionDistance;
        public float ReacquireCooldown => reacquireCooldown;

        // Recovery
        public float StuckVelocityThreshold => stuckVelocityThreshold;
        public float StuckTimeout => stuckTimeout;
        public int MaxRecoveryAttempts => maxRecoveryAttempts;
        public bool AllowEmergencyWarp => allowEmergencyWarp;

        /// <summary>
        /// Settings snapshot consumed by the pure wander behaviour.
        /// </summary>
        public EnemyWanderSettings ToWanderSettings()
        {
            return new EnemyWanderSettings(
                wanderRadius,
                idleDurationMin,
                idleDurationMax,
                wanderDestinationAttempts,
                wanderSampleDistance);
        }

        private void OnValidate()
        {
            wanderRadius = Mathf.Max(1f, wanderRadius);
            idleDurationMin = Mathf.Max(0f, idleDurationMin);
            idleDurationMax = Mathf.Max(idleDurationMin, idleDurationMax);
            wanderDestinationAttempts = Mathf.Max(1, wanderDestinationAttempts);
            wanderSampleDistance = Mathf.Max(0.5f, wanderSampleDistance);
            arrivalDistance = Mathf.Max(0.1f, arrivalDistance);
            immediateDetectionRange = Mathf.Max(0f, immediateDetectionRange);
            loseSightDelay = Mathf.Max(0f, loseSightDelay);
            suspicionDuration = Mathf.Max(0f, suspicionDuration);
            eyeHeight = Mathf.Max(0f, eyeHeight);
            chaseRepathInterval = Mathf.Max(0.05f, chaseRepathInterval);
            chaseRepathDistance = Mathf.Max(0f, chaseRepathDistance);
            maxChaseDistanceFromHome = Mathf.Max(wanderRadius, maxChaseDistanceFromHome);
            leashHysteresis = Mathf.Clamp(leashHysteresis, 0f, maxChaseDistanceFromHome * 0.5f);
            awarenessRadius = Mathf.Max(0f, awarenessRadius);
            defensiveChaseDuration = Mathf.Max(0.5f, defensiveChaseDuration);
            returnCompletionDistance = Mathf.Max(0.2f, returnCompletionDistance);
            reacquireCooldown = Mathf.Max(0f, reacquireCooldown);
            stuckVelocityThreshold = Mathf.Max(0.01f, stuckVelocityThreshold);
            stuckTimeout = Mathf.Max(0.5f, stuckTimeout);
            maxRecoveryAttempts = Mathf.Max(1, maxRecoveryAttempts);
        }
    }
}

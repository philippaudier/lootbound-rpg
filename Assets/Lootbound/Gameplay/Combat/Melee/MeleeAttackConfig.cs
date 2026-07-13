using UnityEngine;

namespace Lootbound.Gameplay.Combat
{
    /// <summary>
    /// Configuration for a melee attack.
    /// Defines timing, damage, and detection parameters.
    /// </summary>
    [CreateAssetMenu(fileName = "MeleeAttackConfig", menuName = "Lootbound/Combat/Melee Attack Config")]
    public class MeleeAttackConfig : ScriptableObject
    {
        [Header("Damage")]
        [Tooltip("Base damage dealt by this attack.")]
        [SerializeField] private float damage = 30f;

        [Tooltip("Stagger force applied to targets (0-1).")]
        [SerializeField, Range(0f, 1f)] private float staggerForce = 0.3f;

        [Header("Timing")]
        [Tooltip("Duration of the windup phase before the attack becomes active.")]
        [SerializeField] private float windupDuration = 0.15f;

        [Tooltip("When the active hit window starts (from attack start).")]
        [SerializeField] private float activeWindowStart = 0.15f;

        [Tooltip("When the active hit window ends (from attack start).")]
        [SerializeField] private float activeWindowEnd = 0.45f;

        [Tooltip("Duration of the recovery phase after the active window.")]
        [SerializeField] private float recoveryDuration = 0.30f;

        [Header("Detection")]
        [Tooltip("Maximum range of the attack.")]
        [SerializeField] private float range = 2.0f;

        [Tooltip("Radius of the sphere cast for hit detection.")]
        [SerializeField] private float traceRadius = 0.2f;

        [Tooltip("Aim tolerance in degrees for hit detection.")]
        [SerializeField] private float aimTolerance = 15f;

        [Header("Feedback")]
        [Tooltip("Duration of hitstop effect when hitting a target.")]
        [SerializeField] private float hitstopDuration = 0.05f;

        // Properties
        public float Damage => damage;
        public float StaggerForce => staggerForce;
        public float WindupDuration => windupDuration;
        public float ActiveWindowStart => activeWindowStart;
        public float ActiveWindowEnd => activeWindowEnd;
        public float RecoveryDuration => recoveryDuration;
        public float Range => range;
        public float TraceRadius => traceRadius;
        public float AimTolerance => aimTolerance;
        public float HitstopDuration => hitstopDuration;

        /// <summary>
        /// Total duration of the attack from start to end.
        /// </summary>
        public float TotalDuration => activeWindowEnd + recoveryDuration;

        /// <summary>
        /// Duration of the active hit window.
        /// </summary>
        public float ActiveWindowDuration => activeWindowEnd - activeWindowStart;

        private void OnValidate()
        {
            // Ensure timing values are valid
            windupDuration = Mathf.Max(0f, windupDuration);
            activeWindowStart = Mathf.Max(0f, activeWindowStart);
            activeWindowEnd = Mathf.Max(activeWindowStart, activeWindowEnd);
            recoveryDuration = Mathf.Max(0f, recoveryDuration);
            range = Mathf.Max(0.1f, range);
            traceRadius = Mathf.Max(0f, traceRadius);
            aimTolerance = Mathf.Clamp(aimTolerance, 0f, 90f);
            hitstopDuration = Mathf.Max(0f, hitstopDuration);
        }
    }
}

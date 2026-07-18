using UnityEngine;

namespace Lootbound.Gameplay.Combat
{
    /// <summary>
    /// Simple, readable player perception: distance + field of view + line of
    /// sight, plus a short omnidirectional immediate range. Owned and ticked
    /// by EnemyBrain at a configurable interval (staggered per instance),
    /// never per frame. Perception decides what is SEEN; the brain decides
    /// the state; the NavMeshAgent moves.
    /// </summary>
    public sealed class EnemyPerception
    {
        private readonly Transform self;
        private readonly EnemyConfig config;
        private readonly EnemyNavigationProfile profile;

        private float nextCheckAt;

        public Transform Target { get; set; }

        /// <summary>Result of the latest perception check.</summary>
        public bool TargetVisible { get; private set; }

        /// <summary>Time of the last positive sighting (negative infinity when never seen).</summary>
        public float LastSeenTime { get; private set; } = float.NegativeInfinity;

        /// <summary>Target position at the last positive sighting.</summary>
        public Vector3 LastKnownTargetPosition { get; private set; }

        public float TimeSinceSeen(float now) => now - LastSeenTime;

        public EnemyPerception(Transform self, Transform target, EnemyConfig config, EnemyNavigationProfile profile, float initialStagger)
        {
            this.self = self;
            Target = target;
            this.config = config;
            this.profile = profile;
            nextCheckAt = initialStagger;
        }

        /// <summary>
        /// Throttled perception update. Cheap between intervals.
        /// </summary>
        public void Tick(float now)
        {
            if (now < nextCheckAt)
            {
                return;
            }

            nextCheckAt = now + profile.PerceptionInterval;
            TargetVisible = EvaluateVisibility();

            if (TargetVisible)
            {
                LastSeenTime = now;
                LastKnownTargetPosition = Target.position;
            }
        }

        private bool EvaluateVisibility()
        {
            if (Target == null || config == null)
            {
                return false;
            }

            float distance = Vector3.Distance(self.position, Target.position);
            if (distance > config.DetectionRange)
            {
                return false;
            }

            // Field of view - bypassed only inside the immediate range
            // (someone brushing past you is noticed even from behind).
            if (distance > profile.ImmediateDetectionRange)
            {
                Vector3 dirToTarget = (Target.position - self.position).normalized;
                float angle = Vector3.Angle(self.forward, dirToTarget);
                if (angle > config.FieldOfView * 0.5f)
                {
                    return false;
                }
            }

            return HasLineOfSight(distance);
        }

        private bool HasLineOfSight(float distance)
        {
            Vector3 eyePosition = self.position + Vector3.up * profile.EyeHeight;
            Vector3 targetCenter = Target.position + Vector3.up * profile.TargetHeightOffset;
            Vector3 toTarget = targetCenter - eyePosition;

            // Masked and trigger-ignoring: pickups or interaction volumes must
            // never block an enemy's eyes.
            if (Physics.Raycast(eyePosition, toTarget.normalized, out RaycastHit hit, distance,
                    profile.LineOfSightMask, QueryTriggerInteraction.Ignore))
            {
                if (!hit.transform.IsChildOf(Target) && hit.transform != Target)
                {
                    return false;
                }
            }

            return true;
        }
    }
}

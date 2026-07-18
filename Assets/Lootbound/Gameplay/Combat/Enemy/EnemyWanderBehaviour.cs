using UnityEngine;

namespace Lootbound.Gameplay.Combat
{
    /// <summary>
    /// Settings snapshot for the wander behaviour (plain data, no asset
    /// dependency in tests).
    /// </summary>
    public readonly struct EnemyWanderSettings
    {
        public float WanderRadius { get; }
        public float IdleDurationMin { get; }
        public float IdleDurationMax { get; }
        public int DestinationAttempts { get; }
        public float SampleDistance { get; }

        public EnemyWanderSettings(float wanderRadius, float idleDurationMin, float idleDurationMax,
            int destinationAttempts, float sampleDistance)
        {
            WanderRadius = wanderRadius;
            IdleDurationMin = idleDurationMin;
            IdleDurationMax = idleDurationMax;
            DestinationAttempts = destinationAttempts;
            SampleDistance = sampleDistance;
        }
    }

    /// <summary>
    /// Calm local wandering around HomePosition. Pure C#: time, randomness
    /// and NavMesh sampling are injected, so pacing and candidate selection
    /// are testable in EditMode.
    ///
    /// Move distances are weighted so enemies spend more time living than
    /// walking: 40% stay put, 30% short move, 20% medium, 10% far - all
    /// relative to WanderRadius and always centered on HomePosition (never
    /// only on the current position, so the enemy drifts back naturally).
    /// </summary>
    public sealed class EnemyWanderBehaviour : IEnemyRoamingBehaviour
    {
        // Weighted move buckets: (cumulative probability, min factor, max factor of WanderRadius)
        private const float StayProbability = 0.40f;
        private const float ShortProbability = 0.30f;   // 0.15 - 0.35 R
        private const float MediumProbability = 0.20f;  // 0.35 - 0.70 R
        // Far: remaining 0.10                          // 0.70 - 1.00 R

        private readonly EnemyWanderSettings settings;
        private readonly System.Random random;
        private readonly NavigationSampleDelegate sampleNavMesh;

        private float nextDecisionAt;

        public EnemyState MovingState => EnemyState.Wandering;
        public string ModeName => "Wander";

        public EnemyWanderBehaviour(EnemyWanderSettings settings, System.Random random, NavigationSampleDelegate sampleNavMesh)
        {
            this.settings = settings;
            this.random = random;
            this.sampleNavMesh = sampleNavMesh;
        }

        public void OnRoamingResumed(float now)
        {
            ScheduleNextDecision(now);
        }

        public void OnDestinationReached(float now)
        {
            ScheduleNextDecision(now);
        }

        public bool TryGetNextDestination(Vector3 homePosition, Vector3 currentPosition, float now, out Vector3 destination)
        {
            destination = default;

            if (now < nextDecisionAt)
            {
                return false;
            }

            float roll = (float)random.NextDouble();
            if (roll < StayProbability)
            {
                // Live a little: stay put for another rest window.
                ScheduleNextDecision(now);
                return false;
            }

            (float minFactor, float maxFactor) = SelectDistanceBucket(roll);

            for (int attempt = 0; attempt < settings.DestinationAttempts; attempt++)
            {
                float angle = (float)(random.NextDouble() * Mathf.PI * 2.0);
                float distance = settings.WanderRadius * Mathf.Lerp(minFactor, maxFactor, (float)random.NextDouble());

                Vector3 candidate = homePosition + new Vector3(
                    Mathf.Cos(angle) * distance,
                    0f,
                    Mathf.Sin(angle) * distance);

                if (sampleNavMesh(candidate, settings.SampleDistance, out Vector3 sampled))
                {
                    destination = sampled;
                    return true;
                }
            }

            // No navigable point found: fail cleanly and rest again.
            ScheduleNextDecision(now);
            return false;
        }

        private (float min, float max) SelectDistanceBucket(float roll)
        {
            if (roll < StayProbability + ShortProbability)
            {
                return (0.15f, 0.35f);
            }

            if (roll < StayProbability + ShortProbability + MediumProbability)
            {
                return (0.35f, 0.70f);
            }

            return (0.70f, 1.00f);
        }

        private void ScheduleNextDecision(float now)
        {
            float idle = Mathf.Lerp(settings.IdleDurationMin, settings.IdleDurationMax, (float)random.NextDouble());
            nextDecisionAt = now + idle;
        }
    }
}

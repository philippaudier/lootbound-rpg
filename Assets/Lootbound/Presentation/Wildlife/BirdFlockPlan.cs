using UnityEngine;

namespace Lootbound.Presentation.Wildlife
{
    /// <summary>Per-bird deterministic parameters inside a flock.</summary>
    public readonly struct BirdSpec
    {
        /// <summary>Loose formation offset around the shared path.</summary>
        public Vector3 FormationOffset { get; }

        /// <summary>Individual speed multiplier (close to 1, never identical).</summary>
        public float SpeedFactor { get; }

        /// <summary>Seconds before this bird starts along the path.</summary>
        public float StartDelay { get; }

        /// <summary>Vertical oscillation phase (radians).</summary>
        public float BobPhase { get; }

        /// <summary>Vertical oscillation amplitude (meters).</summary>
        public float BobAmplitude { get; }

        /// <summary>Uniform scale of the visual.</summary>
        public float Scale { get; }

        /// <summary>Wing-flap speed (cycles per second, fallback animation).</summary>
        public float FlapSpeed { get; }

        public BirdSpec(Vector3 formationOffset, float speedFactor, float startDelay,
            float bobPhase, float bobAmplitude, float scale, float flapSpeed)
        {
            FormationOffset = formationOffset;
            SpeedFactor = speedFactor;
            StartDelay = startDelay;
            BobPhase = bobPhase;
            BobAmplitude = bobAmplitude;
            Scale = scale;
            FlapSpeed = flapSpeed;
        }
    }

    /// <summary>
    /// Deterministic plan for one flock: variant, birds, trajectory,
    /// timing. Produced by the pure BirdFlockPlanner; consumed by the
    /// presenter. Presentation-side only - Gameplay never sees it.
    /// </summary>
    public sealed class BirdFlockPlan
    {
        /// <summary>Index into the library variants; -1 = development fallback silhouette.</summary>
        public int VariantIndex { get; }

        public BirdTrajectory Trajectory { get; private set; }

        /// <summary>Seconds a bird at speed factor 1 takes to fly the path.</summary>
        public float Duration { get; }

        /// <summary>Seconds until the LAST bird finishes (delays and speeds included).</summary>
        public float TotalDuration { get; }

        public BirdSpec[] Specs { get; }

        public int BirdCount => Specs.Length;

        public BirdFlockPlan(int variantIndex, BirdTrajectory trajectory, float duration, BirdSpec[] specs)
        {
            VariantIndex = variantIndex;
            Trajectory = trajectory;
            Duration = duration;
            Specs = specs ?? new BirdSpec[0];

            float total = duration;
            for (int i = 0; i < Specs.Length; i++)
            {
                float birdTotal = Specs[i].StartDelay + duration / Mathf.Max(0.01f, Specs[i].SpeedFactor);
                if (birdTotal > total) total = birdTotal;
            }

            TotalDuration = total;
        }

        /// <summary>
        /// Global translation of the whole path (camera avoidance). The
        /// variant, count, offsets, phases and speeds are never touched.
        /// </summary>
        public void Translate(Vector3 offset)
        {
            Trajectory = Trajectory.Translated(offset);
        }
    }
}

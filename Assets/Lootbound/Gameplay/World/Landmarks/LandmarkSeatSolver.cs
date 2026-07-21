using System.Collections.Generic;
using UnityEngine;
using Lootbound.Gameplay.World.Layout;

namespace Lootbound.Gameplay.World.Landmarks
{
    /// <summary>
    /// Evaluates the local relief under a landmark and derives its seat - the
    /// representative height the terrain should be drawn toward. Pure: reads the
    /// terrain through <see cref="ITerrainSampler"/>, writes nothing.
    ///
    /// The landmark's XZ is NEVER moved: the planner already chose the place.
    /// The reference height is a MEDIAN of a ring of samples plus the centre,
    /// which resists a lone boulder or dip far better than a single centre
    /// sample or a mean. How much of that seat actually reaches the ground -
    /// and how steep ground is only partially corrected - is the applier's job.
    /// </summary>
    public static class LandmarkSeatSolver
    {
        private const int RingSampleCount = 8;

        /// <summary>
        /// Produces the seat description for a placement, or false when the
        /// definition opts out (mode None, or a non-positive foundation radius).
        /// </summary>
        public static bool TrySolve(LandmarkPlacement placement, ITerrainSampler sampler, out LandmarkTerrainStamp stamp)
        {
            stamp = null;
            if (placement == null || sampler == null)
            {
                return false;
            }

            var definition = placement.Definition;
            if (definition == null ||
                definition.ConformingMode == LandmarkTerrainConformingMode.None ||
                definition.FoundationRadius <= 0f)
            {
                return false;
            }

            float reference = ComputeReferenceHeight(placement.X, placement.Z, definition.FoundationRadius, sampler);
            float seatHeight = reference + definition.VerticalOffset;

            stamp = new LandmarkTerrainStamp(
                placement.LandmarkId,
                definition.FoundationShape,
                definition.ConformingMode,
                placement.X,
                placement.Z,
                seatHeight,
                definition.FoundationRadius,
                definition.TransitionRadius,
                definition.MaxCutDepth,
                definition.MaxFillHeight,
                definition.ResidualRoughness,
                definition.FoundationPriority);
            return true;
        }

        /// <summary>Robust representative height: median of the centre plus a ring at the foundation radius.</summary>
        private static float ComputeReferenceHeight(float x, float z, float radius, ITerrainSampler sampler)
        {
            var samples = new List<float>(RingSampleCount + 1)
            {
                sampler.SampleHeight(x, z) // the centre always counts
            };

            for (int i = 0; i < RingSampleCount; i++)
            {
                float angle = (i / (float)RingSampleCount) * Mathf.PI * 2f;
                float sx = x + Mathf.Cos(angle) * radius;
                float sz = z + Mathf.Sin(angle) * radius;

                // Skip out-of-bounds ring points so a border seat is not biased
                // by the sampler's edge clamping.
                if (sampler.IsWithinBounds(sx, sz))
                {
                    samples.Add(sampler.SampleHeight(sx, sz));
                }
            }

            return Median(samples);
        }

        private static float Median(List<float> values)
        {
            int n = values.Count;
            if (n == 0)
            {
                return 0f;
            }

            values.Sort();
            if ((n & 1) == 1)
            {
                return values[n / 2];
            }

            return (values[n / 2 - 1] + values[n / 2]) * 0.5f;
        }
    }
}

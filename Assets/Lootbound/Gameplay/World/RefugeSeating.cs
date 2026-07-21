using System.Collections.Generic;
using UnityEngine;
using Lootbound.Gameplay.World.Landmarks;
using Lootbound.Gameplay.World.Layout;

namespace Lootbound.Gameplay.World
{
    /// <summary>
    /// Seats the Refuge into the terrain. Instead of stamping a raised platform
    /// (which left an ugly flat cone), it CARVES a natural gradient hollow: the
    /// player's home always sits in a welcoming basin - shallow on flat ground,
    /// a deep bowl when the centre falls on a mountain, its walls a smooth
    /// gradient back to the natural relief.
    ///
    /// It reuses the landmark terrain-stamp applier (clamped cut/fill, smoothstep
    /// transition, residual relief) - the Refuge is the first non-landmark
    /// structure to emit a Structure Stamp, previewing the T4 generalization.
    /// </summary>
    public static class RefugeSeating
    {
        private const int RingSampleCount = 8;
        private const string StampId = "refuge";

        public static void Carve(
            TerrainGenerationContext context,
            ITerrainSampler sampler,
            Vector3 refugePosition,
            TerrainGenerationConfig config)
        {
            if (context == null || sampler == null || config == null || config.RefugeFoundationRadius <= 0f)
            {
                return;
            }

            // Robust local reference, then carve DOWN into a basin.
            float reference = ComputeReferenceHeight(refugePosition.x, refugePosition.z, config.RefugeFoundationRadius, sampler);
            float seat = reference - config.RefugeCarveDepth;

            var stamp = new LandmarkTerrainStamp(
                StampId,
                FoundationShape.Circle,
                LandmarkTerrainConformingMode.SoftFoundation,
                refugePosition.x,
                refugePosition.z,
                seat,
                config.RefugeFoundationRadius,
                config.RefugeTransitionRadius,
                config.RefugeMaxCut,
                config.RefugeMaxFill,
                config.RefugeResidualRoughness,
                int.MaxValue); // the refuge wins any overlap

            LandmarkTerrainStampApplier.Apply(context, new[] { stamp });
        }

        /// <summary>Median of the centre plus a ring at the foundation radius (resists a lone spike/dip).</summary>
        private static float ComputeReferenceHeight(float x, float z, float radius, ITerrainSampler sampler)
        {
            var samples = new List<float>(RingSampleCount + 1) { sampler.SampleHeight(x, z) };
            for (int i = 0; i < RingSampleCount; i++)
            {
                float angle = (i / (float)RingSampleCount) * Mathf.PI * 2f;
                float sx = x + Mathf.Cos(angle) * radius;
                float sz = z + Mathf.Sin(angle) * radius;
                if (sampler.IsWithinBounds(sx, sz))
                {
                    samples.Add(sampler.SampleHeight(sx, sz));
                }
            }

            samples.Sort();
            int n = samples.Count;
            if (n == 0) return 0f;
            return (n & 1) == 1 ? samples[n / 2] : (samples[n / 2 - 1] + samples[n / 2]) * 0.5f;
        }
    }
}

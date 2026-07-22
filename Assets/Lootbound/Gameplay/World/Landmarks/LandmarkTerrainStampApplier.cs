using System.Collections.Generic;
using UnityEngine;

namespace Lootbound.Gameplay.World.Landmarks
{
    /// <summary>
    /// The V1 consumer that realizes terrain-seat descriptions on the pipeline
    /// heightmap. It decides HOW: circular footprint, smoothstep transition,
    /// clamped cut/fill (partial seating on steep ground), residual relief kept
    /// from the original terrain, then a slope-map refresh. A different backend
    /// (GPU / voxel) would be a different applier over the same stamps.
    ///
    /// Writes <see cref="TerrainGenerationContext.NormalizedHeightMap"/> in the
    /// same 0..1 space the flattening passes use, then recomputes the slope map.
    /// Runs BEFORE the heightmap is applied to the Unity Terrain, so mesh,
    /// collider and navigation all inherit the seats for free.
    /// </summary>
    public static class LandmarkTerrainStampApplier
    {
        /// <summary>
        /// Absolute floor: influence at or below this is treated as no
        /// contribution at all (pure transition tail).
        /// </summary>
        public const float InfluenceEpsilon = 1e-3f;

        /// <summary>
        /// Relative floor: at a shared cell, a stamp must reach at least this
        /// fraction of the strongest stamp's influence to compete for it.
        /// This is what makes overlap arbitration honour EFFECTIVE influence -
        /// a high-priority foundation that merely grazes a cell can never
        /// override a neighbour that genuinely covers it (no seam), while two
        /// foundations that both truly cover a cell are decided by priority.
        /// </summary>
        public const float RelativeInfluenceFloor = 0.5f;

        // The unsupported-shape fallback is announced at most once per session.
        private static bool _nonCircleShapeWarned;

        public static void Apply(TerrainGenerationContext context, IReadOnlyList<LandmarkTerrainStamp> stamps)
        {
            if (context == null || stamps == null || stamps.Count == 0)
            {
                return;
            }

            float[,] heightMap = context.NormalizedHeightMap;
            int resolution = context.Resolution;
            float worldSize = context.WorldSize;
            float terrainHeight = context.TerrainHeight;
            if (heightMap == null || resolution <= 1 || worldSize <= 0f || terrainHeight <= 0f)
            {
                return;
            }

            WarnOnUnsupportedShapes(stamps);

            if (!TryComputeAffectedRegion(stamps, context,
                    out int minX, out int maxX, out int minZ, out int maxZ))
            {
                return;
            }

            bool anyChange = false;

            for (int x = minX; x <= maxX; x++)
            {
                float worldX = context.GridToWorldX(x);
                for (int z = minZ; z <= maxZ; z++)
                {
                    float worldZ = context.GridToWorldZ(z);

                    // Deterministic, order-independent winner for this cell.
                    LandmarkTerrainStamp winner = SelectWinner(stamps, worldX, worldZ, out float winnerInfluence);
                    if (winner == null)
                    {
                        continue;
                    }

                    // Each cell is written once and no neighbour is read, so
                    // reading in place still reads the ORIGINAL height here.
                    float originalWorld = heightMap[x, z] * terrainHeight;

                    // Partial seating: the correction can never exceed the
                    // cut/fill limits, so steep ground keeps a residual rather
                    // than forming a cliff.
                    float delta = winner.SeatHeight - originalWorld;
                    delta = Mathf.Clamp(delta, -winner.MaxCutDepth, winner.MaxFillHeight);
                    float targetWorld = originalWorld + delta;

                    // Keep a fraction of the original relief, then feather by
                    // influence toward the untouched terrain across the ring.
                    float seatedWorld = Mathf.Lerp(targetWorld, originalWorld, winner.ResidualRoughness);
                    float appliedWorld = Mathf.Lerp(originalWorld, seatedWorld, winnerInfluence);

                    heightMap[x, z] = Mathf.Clamp01(appliedWorld / terrainHeight);
                    anyChange = true;
                }
            }

            if (anyChange)
            {
                // Slopes changed under every seat; downstream validation and
                // navigation read the refreshed map.
                TerrainHeightGenerator.ComputeSlopeMap(context);
            }
        }

        /// <summary>
        /// The stamp that seats this cell, or null when none genuinely
        /// influences it. Two passes: find the strongest influence (above the
        /// absolute floor), then arbitrate only among stamps that reach the
        /// relative floor of that strength - so a grazing high-priority stamp
        /// never overrides a neighbour that truly covers the cell.
        /// </summary>
        private static LandmarkTerrainStamp SelectWinner(
            IReadOnlyList<LandmarkTerrainStamp> stamps, float worldX, float worldZ, out float winnerInfluence)
        {
            winnerInfluence = 0f;

            float maxInfluence = 0f;
            foreach (var stamp in stamps)
            {
                float influence = Influence(stamp, worldX, worldZ);
                if (influence > InfluenceEpsilon && influence > maxInfluence)
                {
                    maxInfluence = influence;
                }
            }

            if (maxInfluence <= InfluenceEpsilon)
            {
                return null;
            }

            float floor = Mathf.Max(InfluenceEpsilon, maxInfluence * RelativeInfluenceFloor);

            LandmarkTerrainStamp winner = null;
            foreach (var stamp in stamps)
            {
                float influence = Influence(stamp, worldX, worldZ);
                if (influence < floor)
                {
                    continue;
                }

                if (IsBetter(stamp, influence, winner, winnerInfluence))
                {
                    winner = stamp;
                    winnerInfluence = influence;
                }
            }

            return winner;
        }

        /// <summary>
        /// Overlap arbitration among genuine competitors: higher priority wins;
        /// ties go to the stronger effective influence; final ties to the lower
        /// LandmarkId (ordinal).
        /// </summary>
        private static bool IsBetter(
            LandmarkTerrainStamp candidate, float candidateInfluence,
            LandmarkTerrainStamp current, float currentInfluence)
        {
            if (current == null)
            {
                return true;
            }

            if (candidate.Priority != current.Priority)
            {
                return candidate.Priority > current.Priority;
            }

            if (!Mathf.Approximately(candidateInfluence, currentInfluence))
            {
                return candidateInfluence > currentInfluence;
            }

            return string.CompareOrdinal(candidate.LandmarkId, current.LandmarkId) < 0;
        }

        /// <summary>Circular influence: 1 inside the foundation, smoothstep down to 0 across the transition.</summary>
        private static float Influence(LandmarkTerrainStamp stamp, float worldX, float worldZ)
        {
            float dx = worldX - stamp.CenterX;
            float dz = worldZ - stamp.CenterZ;
            float distance = Mathf.Sqrt(dx * dx + dz * dz);

            if (distance <= stamp.FoundationRadius)
            {
                return 1f;
            }

            if (stamp.TransitionRadius <= 0f || distance >= stamp.OuterRadius)
            {
                return 0f;
            }

            float t = (distance - stamp.FoundationRadius) / stamp.TransitionRadius;
            return 1f - (t * t * (3f - 2f * t));
        }

        private static void WarnOnUnsupportedShapes(IReadOnlyList<LandmarkTerrainStamp> stamps)
        {
            if (_nonCircleShapeWarned)
            {
                return;
            }

            foreach (var stamp in stamps)
            {
                if (stamp.Shape != FoundationShape.Circle)
                {
                    Debug.LogWarning(
                        $"[LandmarkTerrainStampApplier] FoundationShape.{stamp.Shape} is not implemented in V1 - " +
                        $"falling back to Circle for '{stamp.LandmarkId}' (and any other non-circle foundation this session). Logged once.");
                    _nonCircleShapeWarned = true;
                    return;
                }
            }
        }

        /// <summary>Union index-space box of every stamp's outer radius, clamped to the map.</summary>
        private static bool TryComputeAffectedRegion(
            IReadOnlyList<LandmarkTerrainStamp> stamps, TerrainGenerationContext context,
            out int minX, out int maxX, out int minZ, out int maxZ)
        {
            minX = int.MaxValue; maxX = int.MinValue;
            minZ = int.MaxValue; maxZ = int.MinValue;
            bool any = false;
            int resolution = context.Resolution;

            foreach (var stamp in stamps)
            {
                float outer = stamp.OuterRadius;
                if (outer <= 0f)
                {
                    continue;
                }

                int ix0 = Mathf.FloorToInt(context.WorldToGridX(stamp.CenterX - outer));
                int ix1 = Mathf.CeilToInt(context.WorldToGridX(stamp.CenterX + outer));
                int iz0 = Mathf.FloorToInt(context.WorldToGridZ(stamp.CenterZ - outer));
                int iz1 = Mathf.CeilToInt(context.WorldToGridZ(stamp.CenterZ + outer));

                ix0 = Mathf.Clamp(ix0, 0, resolution - 1);
                ix1 = Mathf.Clamp(ix1, 0, resolution - 1);
                iz0 = Mathf.Clamp(iz0, 0, resolution - 1);
                iz1 = Mathf.Clamp(iz1, 0, resolution - 1);

                if (ix1 < ix0 || iz1 < iz0)
                {
                    continue;
                }

                if (ix0 < minX) minX = ix0;
                if (ix1 > maxX) maxX = ix1;
                if (iz0 < minZ) minZ = iz0;
                if (iz1 > maxZ) maxZ = iz1;
                any = true;
            }

            return any;
        }
    }
}

using UnityEngine;

namespace Lootbound.Gameplay.World.Layout
{
    /// <summary>
    /// Interface for querying terrain height and slope before Unity Terrain exists.
    /// Used by WorldLayoutGenerator to evaluate candidate positions during generation.
    /// </summary>
    public interface ITerrainSampler
    {
        /// <summary>
        /// Sample height at world position in meters.
        /// </summary>
        float SampleHeight(float worldX, float worldZ);

        /// <summary>
        /// Sample slope at world position in degrees.
        /// </summary>
        float SampleSlope(float worldX, float worldZ);

        /// <summary>
        /// Check if a world position is within terrain bounds.
        /// </summary>
        bool IsWithinBounds(float worldX, float worldZ);

        /// <summary>
        /// World size in meters.
        /// </summary>
        float WorldSize { get; }

        /// <summary>
        /// Centre of the sampled region in world space (Y = 0). Lets consumers
        /// place things relative to the region without assuming a corner origin.
        /// </summary>
        Vector3 WorldCenter { get; }

        /// <summary>
        /// Maximum terrain height in meters.
        /// </summary>
        float TerrainHeight { get; }
    }
}

using UnityEngine;

namespace Lootbound.Gameplay.World.Layout
{
    /// <summary>
    /// Evaluates radial position data relative to the Refuge.
    ///
    /// IMPORTANT: NormalizedWorldRadius is calculated against the logical WorldDisc radius,
    /// NOT the local terrain size. The prototype terrain is a compressed preview window
    /// into the final world. Use worldDiscRadius parameter to specify the logical radius.
    /// </summary>
    public static class WorldRingEvaluator
    {
        /// <summary>
        /// Evaluate the ring sample at a given world position.
        /// </summary>
        /// <param name="position">World position to evaluate</param>
        /// <param name="refugePosition">Center of the Refuge (world coordinates)</param>
        /// <param name="worldDiscRadius">Logical radius of the WorldDisc (NOT local terrain size)</param>
        /// <param name="ringConfig">Ring threshold configuration</param>
        /// <returns>Ring sample with distance, normalized radius, and ring assignment</returns>
        public static WorldRingSample Evaluate(
            Vector3 position,
            Vector3 refugePosition,
            float worldDiscRadius,
            WorldRingConfig ringConfig)
        {
            // Calculate horizontal distance (ignore Y for radial distance)
            float distanceFromRefuge = Vector2.Distance(
                new Vector2(position.x, position.z),
                new Vector2(refugePosition.x, refugePosition.z)
            );

            // Normalize against WorldDisc radius (logical world size, not terrain size)
            float normalizedWorldRadius = worldDiscRadius > 0f
                ? distanceFromRefuge / worldDiscRadius
                : 0f;

            // Get ring from config
            WorldRing ring = ringConfig.GetRingAt(normalizedWorldRadius);

            return new WorldRingSample(distanceFromRefuge, normalizedWorldRadius, ring);
        }

        /// <summary>
        /// Evaluate using only distance (for pre-calculated distances).
        /// </summary>
        public static WorldRingSample EvaluateFromDistance(
            float distanceFromRefuge,
            float worldDiscRadius,
            WorldRingConfig ringConfig)
        {
            float normalizedWorldRadius = worldDiscRadius > 0f
                ? distanceFromRefuge / worldDiscRadius
                : 0f;

            WorldRing ring = ringConfig.GetRingAt(normalizedWorldRadius);

            return new WorldRingSample(distanceFromRefuge, normalizedWorldRadius, ring);
        }

        /// <summary>
        /// Calculate distance from Refuge without full evaluation.
        /// Uses horizontal distance (XZ plane).
        /// </summary>
        public static float CalculateDistanceFromRefuge(Vector3 position, Vector3 refugePosition)
        {
            return Vector2.Distance(
                new Vector2(position.x, position.z),
                new Vector2(refugePosition.x, refugePosition.z)
            );
        }

        /// <summary>
        /// Calculate normalized world radius without ring evaluation.
        /// </summary>
        /// <param name="distanceFromRefuge">Absolute distance from Refuge</param>
        /// <param name="worldDiscRadius">Logical WorldDisc radius</param>
        public static float CalculateNormalizedRadius(float distanceFromRefuge, float worldDiscRadius)
        {
            return worldDiscRadius > 0f ? distanceFromRefuge / worldDiscRadius : 0f;
        }

        /// <summary>
        /// Check if a position is within the playable world radius.
        /// </summary>
        public static bool IsWithinWorldDisc(
            Vector3 position,
            Vector3 refugePosition,
            float worldDiscRadius)
        {
            float distance = CalculateDistanceFromRefuge(position, refugePosition);
            return distance <= worldDiscRadius;
        }

        /// <summary>
        /// Calculate the angular position (in degrees) around the Refuge.
        /// 0° = positive X direction, increases counter-clockwise.
        /// </summary>
        public static float CalculateAngleFromRefuge(Vector3 position, Vector3 refugePosition)
        {
            float dx = position.x - refugePosition.x;
            float dz = position.z - refugePosition.z;
            return Mathf.Atan2(dz, dx) * Mathf.Rad2Deg;
        }
    }
}

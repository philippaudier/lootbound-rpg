using System;
using UnityEngine;

namespace Lootbound.Gameplay.World.Layout
{
    /// <summary>
    /// Immutable definition of the WorldDisc.
    ///
    /// This represents the logical world structure, independent of:
    /// - Generation seed (stored in WorldLayoutContext)
    /// - Generation state (temporary during layout generation)
    /// - Local terrain size (prototype may use compressed preview)
    ///
    /// Construction: ProceduralTerrainGenerator or future WorldManager
    /// Ownership: TerrainGenerationContext (prototype) or future WorldManager
    /// Config data: WorldRadius, RingConfig (from ScriptableObjects)
    /// Runtime data: None - this definition is immutable after construction
    /// </summary>
    public sealed class WorldDiscDefinition
    {
        /// <summary>
        /// Logical radius of the complete WorldDisc in world units.
        /// This is the authoritative radius for NormalizedWorldRadius calculations.
        /// </summary>
        public float WorldRadius { get; }

        /// <summary>
        /// Ring threshold configuration.
        /// </summary>
        public WorldRingConfig RingConfig { get; }

        /// <summary>
        /// Whether this definition uses compressed preview mode.
        /// When true, the local terrain represents a scaled-down view of the world.
        /// </summary>
        public bool IsCompressedPreview { get; }

        /// <summary>
        /// If IsCompressedPreview is true, this is the local terrain radius
        /// that maps to the logical WorldRadius.
        /// </summary>
        public float PreviewTerrainRadius { get; }

        /// <summary>
        /// Create a WorldDiscDefinition for the full logical world.
        /// </summary>
        /// <param name="worldRadius">Logical radius of the complete WorldDisc</param>
        /// <param name="ringConfig">Ring threshold configuration (will be validated)</param>
        public WorldDiscDefinition(float worldRadius, WorldRingConfig ringConfig)
        {
            if (worldRadius <= 0f)
            {
                throw new ArgumentException("WorldRadius must be positive", nameof(worldRadius));
            }

            if (ringConfig == null)
            {
                throw new ArgumentNullException(nameof(ringConfig));
            }

            // Validate ring config (throws if invalid)
            ringConfig.ValidateOrThrow();

            WorldRadius = worldRadius;
            RingConfig = ringConfig;
            IsCompressedPreview = false;
            PreviewTerrainRadius = worldRadius;
        }

        /// <summary>
        /// Create a WorldDiscDefinition with compressed preview mode.
        /// Use this when the prototype terrain is smaller than the logical world.
        /// </summary>
        /// <param name="worldRadius">Logical radius of the complete WorldDisc</param>
        /// <param name="previewTerrainRadius">Radius of the local prototype terrain</param>
        /// <param name="ringConfig">Ring threshold configuration</param>
        public WorldDiscDefinition(float worldRadius, float previewTerrainRadius, WorldRingConfig ringConfig)
        {
            if (worldRadius <= 0f)
            {
                throw new ArgumentException("WorldRadius must be positive", nameof(worldRadius));
            }

            if (previewTerrainRadius <= 0f)
            {
                throw new ArgumentException("PreviewTerrainRadius must be positive", nameof(previewTerrainRadius));
            }

            if (ringConfig == null)
            {
                throw new ArgumentNullException(nameof(ringConfig));
            }

            ringConfig.ValidateOrThrow();

            WorldRadius = worldRadius;
            RingConfig = ringConfig;
            IsCompressedPreview = previewTerrainRadius < worldRadius;
            PreviewTerrainRadius = previewTerrainRadius;
        }

        /// <summary>
        /// Evaluate ring sample at a world position.
        /// Always uses logical WorldRadius for normalization.
        /// </summary>
        public WorldRingSample EvaluateAt(Vector3 position, Vector3 refugePosition)
        {
            return WorldRingEvaluator.Evaluate(position, refugePosition, WorldRadius, RingConfig);
        }

        /// <summary>
        /// Check if a position is within the playable world radius.
        /// </summary>
        public bool IsWithinPlayableRadius(Vector3 position, Vector3 refugePosition)
        {
            return WorldRingEvaluator.IsWithinWorldDisc(position, refugePosition, WorldRadius);
        }

        /// <summary>
        /// Check if a position is within the local preview terrain.
        /// Only meaningful when IsCompressedPreview is true.
        /// </summary>
        public bool IsWithinPreviewTerrain(Vector3 position, Vector3 refugePosition)
        {
            return WorldRingEvaluator.IsWithinWorldDisc(position, refugePosition, PreviewTerrainRadius);
        }

        /// <summary>
        /// Get the compression ratio (preview terrain / logical world).
        /// Returns 1.0 if not in compressed preview mode.
        /// </summary>
        public float CompressionRatio => PreviewTerrainRadius / WorldRadius;

        /// <summary>
        /// Convert a distance in preview terrain to logical world distance.
        /// </summary>
        public float PreviewToLogicalDistance(float previewDistance)
        {
            if (!IsCompressedPreview) return previewDistance;
            return previewDistance / CompressionRatio;
        }

        /// <summary>
        /// Convert a logical world distance to preview terrain distance.
        /// </summary>
        public float LogicalToPreviewDistance(float logicalDistance)
        {
            if (!IsCompressedPreview) return logicalDistance;
            return logicalDistance * CompressionRatio;
        }
    }
}

using Lootbound.Gameplay.World.Layout;
using UnityEngine;

namespace Lootbound.Gameplay.World
{
    /// <summary>
    /// Holds all data produced during terrain generation.
    /// This is runtime data, not a persistent asset.
    /// </summary>
    public sealed class TerrainGenerationContext
    {
        /// <summary>
        /// The seed used for this generation.
        /// </summary>
        public int Seed { get; }

        /// <summary>
        /// Monotone identity of this generation within the session.
        /// Distinct from the seed: two successive generations may share a
        /// seed but never a GenerationId. Derived systems (navigation,
        /// spawning) use it to discard stale results.
        /// </summary>
        public int GenerationId { get; }

        /// <summary>
        /// Resolution of the heightmap (width and height are equal).
        /// </summary>
        public int Resolution { get; }

        /// <summary>
        /// World size in meters.
        /// </summary>
        public float WorldSize { get; }

        /// <summary>
        /// The world-space region this context materializes. NOT the size of the
        /// world: the logical world is unbounded and the generator can evaluate a
        /// height at any coordinate; these bounds only describe the finite region
        /// for which this concrete heightmap (with its authored relief) exists.
        /// The single place the world origin lives - legacy corner worlds have
        /// Min = (0,0); a refuge-centred world has a negative Min and Center = (0,0).
        /// </summary>
        public WorldBounds Bounds { get; }

        /// <summary>
        /// Maximum terrain height in meters.
        /// </summary>
        public float TerrainHeight { get; }

        /// <summary>
        /// Raw heightmap values [x, z] in range 0-1.
        /// Unity Terrain uses [z, x] indexing, but we store [x, z] for clarity.
        /// </summary>
        public float[,] HeightMap { get; private set; }

        /// <summary>
        /// Normalized heightmap after all processing, using full 0-1 range.
        /// </summary>
        public float[,] NormalizedHeightMap { get; private set; }

        /// <summary>
        /// Slope map in degrees [x, z].
        /// </summary>
        public float[,] SlopeMap { get; private set; }

        /// <summary>
        /// Macro relief contribution map [x, z] in range 0-1.
        /// Useful for future biome/region classification.
        /// </summary>
        public float[,] MacroMap { get; private set; }

        /// <summary>
        /// Computed spawn position in world coordinates.
        /// </summary>
        public Vector3 SpawnPosition { get; set; }

        /// <summary>
        /// Slope at the spawn position in degrees.
        /// </summary>
        public float SpawnSlope { get; set; }

        /// <summary>
        /// Minimum height value in the raw heightmap.
        /// </summary>
        public float MinHeight { get; private set; }

        /// <summary>
        /// Maximum height value in the raw heightmap.
        /// </summary>
        public float MaxHeight { get; private set; }

        /// <summary>
        /// Average height value in the heightmap.
        /// </summary>
        public float AverageHeight { get; private set; }

        /// <summary>
        /// Time taken for heightmap generation in milliseconds.
        /// </summary>
        public float HeightmapGenerationTimeMs { get; set; }

        /// <summary>
        /// Time taken for terrain application in milliseconds.
        /// </summary>
        public float TerrainApplicationTimeMs { get; set; }

        /// <summary>
        /// Time taken for surface painting in milliseconds.
        /// </summary>
        public float PaintingTimeMs { get; set; }

        /// <summary>
        /// Total generation time in milliseconds.
        /// </summary>
        public float TotalGenerationTimeMs { get; set; }

        /// <summary>
        /// World layout context (optional, may be null if layout generation is disabled).
        /// </summary>
        public WorldLayoutContext LayoutContext { get; set; }

        public TerrainGenerationContext(int seed, int resolution, float worldSize, float terrainHeight, int generationId = 0, WorldBounds? bounds = null)
        {
            Seed = seed;
            GenerationId = generationId;
            Resolution = resolution;
            WorldSize = worldSize;
            TerrainHeight = terrainHeight;
            // Default to the legacy corner origin so existing callers are unchanged;
            // the refuge-centred region is supplied explicitly once M2 flips it.
            Bounds = bounds ?? WorldBounds.FromCorner(0f, 0f, worldSize);

            HeightMap = new float[resolution, resolution];
            NormalizedHeightMap = new float[resolution, resolution];
            SlopeMap = new float[resolution, resolution];
            MacroMap = new float[resolution, resolution];
        }

        /// <summary>
        /// Set the heightmap data. Automatically computes statistics.
        /// </summary>
        public void SetHeightMap(float[,] heightMap)
        {
            HeightMap = heightMap;
            ComputeHeightStatistics();
        }

        /// <summary>
        /// Set the normalized heightmap.
        /// </summary>
        public void SetNormalizedHeightMap(float[,] normalizedHeightMap)
        {
            NormalizedHeightMap = normalizedHeightMap;
        }

        /// <summary>
        /// Set the slope map.
        /// </summary>
        public void SetSlopeMap(float[,] slopeMap)
        {
            SlopeMap = slopeMap;
        }

        /// <summary>
        /// Set the macro map.
        /// </summary>
        public void SetMacroMap(float[,] macroMap)
        {
            MacroMap = macroMap;
        }

        /// <summary>
        /// Compute min, max, and average height from the heightmap.
        /// </summary>
        private void ComputeHeightStatistics()
        {
            if (HeightMap == null) return;

            float min = float.MaxValue;
            float max = float.MinValue;
            double sum = 0;
            int count = 0;

            for (int x = 0; x < Resolution; x++)
            {
                for (int z = 0; z < Resolution; z++)
                {
                    float h = HeightMap[x, z];
                    if (h < min) min = h;
                    if (h > max) max = h;
                    sum += h;
                    count++;
                }
            }

            MinHeight = min;
            MaxHeight = max;
            AverageHeight = count > 0 ? (float)(sum / count) : 0f;
        }

        /// <summary>
        /// Convert world position to heightmap coordinates.
        /// </summary>
        public (int x, int z) WorldToHeightmap(Vector3 worldPos)
        {
            int x = Mathf.Clamp(Mathf.RoundToInt(Bounds.NormalizeX(worldPos.x) * (Resolution - 1)), 0, Resolution - 1);
            int z = Mathf.Clamp(Mathf.RoundToInt(Bounds.NormalizeZ(worldPos.z) * (Resolution - 1)), 0, Resolution - 1);
            return (x, z);
        }

        /// <summary>
        /// Centre of the materialized region in world space (Y = 0). At the legacy
        /// corner origin this is (WorldSize/2, 0, WorldSize/2); a refuge-centred
        /// region reports (0, 0, 0). Use this instead of WorldSize * 0.5f.
        /// </summary>
        public Vector3 WorldCenter => new Vector3(Bounds.CenterX, 0f, Bounds.CenterZ);

        /// <summary>True if the world coordinate lies inside the materialized region.</summary>
        public bool Contains(float worldX, float worldZ) => Bounds.Contains(worldX, worldZ);

        /// <summary>World X -> fractional heightmap column [0 .. Resolution-1] (unclamped).</summary>
        public float WorldToGridX(float worldX) => Bounds.NormalizeX(worldX) * (Resolution - 1);

        /// <summary>World Z -> fractional heightmap row [0 .. Resolution-1] (unclamped).</summary>
        public float WorldToGridZ(float worldZ) => Bounds.NormalizeZ(worldZ) * (Resolution - 1);

        /// <summary>Heightmap column -> world X in meters.</summary>
        public float GridToWorldX(int x) => Bounds.MinX + (x / (float)(Resolution - 1)) * Bounds.SizeX;

        /// <summary>Heightmap row -> world Z in meters.</summary>
        public float GridToWorldZ(int z) => Bounds.MinZ + (z / (float)(Resolution - 1)) * Bounds.SizeZ;

        /// <summary>
        /// Sample height at world position using bilinear interpolation.
        /// Reads NormalizedHeightMap - the map actually applied to the Unity
        /// Terrain via GetTerrainHeightmapData - so the result matches the
        /// real ground surface (raw HeightMap lives in pre-normalization space).
        /// </summary>
        public float SampleHeightAtWorld(float worldX, float worldZ)
        {
            float normX = Mathf.Clamp01(Bounds.NormalizeX(worldX));
            float normZ = Mathf.Clamp01(Bounds.NormalizeZ(worldZ));

            float fx = normX * (Resolution - 1);
            float fz = normZ * (Resolution - 1);

            int x0 = Mathf.FloorToInt(fx);
            int z0 = Mathf.FloorToInt(fz);
            int x1 = Mathf.Min(x0 + 1, Resolution - 1);
            int z1 = Mathf.Min(z0 + 1, Resolution - 1);

            float tx = fx - x0;
            float tz = fz - z0;

            float h00 = NormalizedHeightMap[x0, z0];
            float h10 = NormalizedHeightMap[x1, z0];
            float h01 = NormalizedHeightMap[x0, z1];
            float h11 = NormalizedHeightMap[x1, z1];

            float h0 = Mathf.Lerp(h00, h10, tx);
            float h1 = Mathf.Lerp(h01, h11, tx);

            return Mathf.Lerp(h0, h1, tz) * TerrainHeight;
        }

        /// <summary>
        /// Sample slope at world position.
        /// </summary>
        public float SampleSlopeAtWorld(float worldX, float worldZ)
        {
            var (x, z) = WorldToHeightmap(new Vector3(worldX, 0, worldZ));
            return SlopeMap[x, z];
        }

        /// <summary>
        /// Get heightmap data formatted for Unity Terrain (using [z, x] indexing).
        /// </summary>
        public float[,] GetTerrainHeightmapData()
        {
            float[,] terrainData = new float[Resolution, Resolution];
            for (int x = 0; x < Resolution; x++)
            {
                for (int z = 0; z < Resolution; z++)
                {
                    // Unity Terrain uses [z, x] indexing
                    terrainData[z, x] = NormalizedHeightMap[x, z];
                }
            }
            return terrainData;
        }
    }
}

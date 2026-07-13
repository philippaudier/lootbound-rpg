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
        /// Resolution of the heightmap (width and height are equal).
        /// </summary>
        public int Resolution { get; }

        /// <summary>
        /// World size in meters.
        /// </summary>
        public float WorldSize { get; }

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

        public TerrainGenerationContext(int seed, int resolution, float worldSize, float terrainHeight)
        {
            Seed = seed;
            Resolution = resolution;
            WorldSize = worldSize;
            TerrainHeight = terrainHeight;

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
        /// Convert heightmap coordinates to world position.
        /// </summary>
        public Vector3 HeightmapToWorld(int x, int z)
        {
            float worldX = (x / (float)(Resolution - 1)) * WorldSize;
            float worldZ = (z / (float)(Resolution - 1)) * WorldSize;
            float worldY = HeightMap[x, z] * TerrainHeight;
            return new Vector3(worldX, worldY, worldZ);
        }

        /// <summary>
        /// Convert world position to heightmap coordinates.
        /// </summary>
        public (int x, int z) WorldToHeightmap(Vector3 worldPos)
        {
            int x = Mathf.Clamp(Mathf.RoundToInt((worldPos.x / WorldSize) * (Resolution - 1)), 0, Resolution - 1);
            int z = Mathf.Clamp(Mathf.RoundToInt((worldPos.z / WorldSize) * (Resolution - 1)), 0, Resolution - 1);
            return (x, z);
        }

        /// <summary>
        /// Sample height at world position using bilinear interpolation.
        /// </summary>
        public float SampleHeightAtWorld(float worldX, float worldZ)
        {
            float normX = Mathf.Clamp01(worldX / WorldSize);
            float normZ = Mathf.Clamp01(worldZ / WorldSize);

            float fx = normX * (Resolution - 1);
            float fz = normZ * (Resolution - 1);

            int x0 = Mathf.FloorToInt(fx);
            int z0 = Mathf.FloorToInt(fz);
            int x1 = Mathf.Min(x0 + 1, Resolution - 1);
            int z1 = Mathf.Min(z0 + 1, Resolution - 1);

            float tx = fx - x0;
            float tz = fz - z0;

            float h00 = HeightMap[x0, z0];
            float h10 = HeightMap[x1, z0];
            float h01 = HeightMap[x0, z1];
            float h11 = HeightMap[x1, z1];

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

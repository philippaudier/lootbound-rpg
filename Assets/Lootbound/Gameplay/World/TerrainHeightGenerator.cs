using UnityEngine;

namespace Lootbound.Gameplay.World
{
    /// <summary>
    /// Generates the terrain heightmap using layered noise.
    /// Uses deterministic noise based on seed for reproducible results.
    /// </summary>
    public static class TerrainHeightGenerator
    {
        // Offset constants for different noise channels to ensure they don't correlate
        private const int MACRO_OFFSET = 0;
        private const int RIDGE_OFFSET = 31415;
        private const int VALLEY_OFFSET = 27182;
        private const int DETAIL_OFFSET = 14142;
        private const int WARP_OFFSET_X = 17320;
        private const int WARP_OFFSET_Z = 22360;

        /// <summary>
        /// Generate the complete heightmap for the terrain.
        /// </summary>
        public static void Generate(TerrainGenerationContext context, TerrainGenerationConfig config)
        {
            int resolution = context.Resolution;
            float worldSize = context.WorldSize;
            int seed = context.Seed;

            float[,] heightMap = new float[resolution, resolution];
            float[,] macroMap = new float[resolution, resolution];

            // Compute all offsets from seed
            System.Random seedRandom = new System.Random(seed);
            float macroOffsetX = (float)(seedRandom.NextDouble() * 10000);
            float macroOffsetZ = (float)(seedRandom.NextDouble() * 10000);
            float ridgeOffsetX = (float)(seedRandom.NextDouble() * 10000);
            float ridgeOffsetZ = (float)(seedRandom.NextDouble() * 10000);
            float valleyOffsetX = (float)(seedRandom.NextDouble() * 10000);
            float valleyOffsetZ = (float)(seedRandom.NextDouble() * 10000);
            float detailOffsetX = (float)(seedRandom.NextDouble() * 10000);
            float detailOffsetZ = (float)(seedRandom.NextDouble() * 10000);
            float warpOffsetX = (float)(seedRandom.NextDouble() * 10000);
            float warpOffsetZ = (float)(seedRandom.NextDouble() * 10000);

            // Generate heightmap
            for (int x = 0; x < resolution; x++)
            {
                for (int z = 0; z < resolution; z++)
                {
                    // Convert to world coordinates
                    float worldX = (x / (float)(resolution - 1)) * worldSize;
                    float worldZ = (z / (float)(resolution - 1)) * worldSize;

                    // Apply light domain warping for more organic shapes
                    float warpStrength = 0.15f;
                    float warpScale = config.MacroScale * 0.5f;
                    float warpX = SamplePerlin(worldX + warpOffsetX, worldZ + warpOffsetZ, warpScale) * warpStrength * worldSize * 0.1f;
                    float warpZ = SamplePerlin(worldX + warpOffsetX + 1000, worldZ + warpOffsetZ + 1000, warpScale) * warpStrength * worldSize * 0.1f;

                    float warpedX = worldX + warpX;
                    float warpedZ = worldZ + warpZ;

                    // 1. Macro terrain - main shape
                    float macro = SampleFBM(
                        warpedX + macroOffsetX,
                        warpedZ + macroOffsetZ,
                        config.MacroScale,
                        config.MacroOctaves,
                        config.MacroPersistence,
                        config.MacroLacunarity
                    );

                    // Store macro for surface classification
                    macroMap[x, z] = macro;

                    // 2. Valley features - create low areas and corridors
                    float valley = 0f;
                    if (config.ValleyStrength > 0f)
                    {
                        float valleyNoise = SampleFBM(
                            worldX + valleyOffsetX,
                            worldZ + valleyOffsetZ,
                            config.ValleyScale,
                            3,
                            0.5f,
                            2f
                        );

                        // Invert and shape valleys - deeper in low macro areas
                        float valleyMask = 1f - Mathf.Clamp01(macro * 1.5f);
                        valley = (1f - Mathf.Abs(valleyNoise * 2f - 1f)) * valleyMask;
                        valley = Mathf.Pow(valley, 1.5f) * config.ValleyStrength;
                    }

                    // 3. Ridge features - create high points
                    float ridge = 0f;
                    if (config.RidgeStrength > 0f)
                    {
                        float ridgeNoise = SampleFBM(
                            worldX + ridgeOffsetX,
                            worldZ + ridgeOffsetZ,
                            config.RidgeScale,
                            3,
                            0.45f,
                            2.1f
                        );

                        // Ridged noise formula
                        ridge = 1f - Mathf.Abs(ridgeNoise * 2f - 1f);
                        ridge = Mathf.Pow(ridge, 2f);

                        // Only apply ridges in higher macro areas
                        float ridgeMask = Mathf.Clamp01((macro - 0.4f) * 2f);
                        ridge *= ridgeMask * config.RidgeStrength;
                    }

                    // 4. Detail noise - fine variation
                    float detail = 0f;
                    if (config.DetailStrength > 0f)
                    {
                        detail = SampleFBM(
                            worldX + detailOffsetX,
                            worldZ + detailOffsetZ,
                            config.DetailScale,
                            2,
                            0.5f,
                            2f
                        ) * config.DetailStrength;
                    }

                    // Combine all layers
                    float height = macro;
                    height -= valley * 0.3f; // Valleys subtract from height
                    height += ridge * 0.25f; // Ridges add to height
                    height += detail;

                    // Apply height remap curve
                    height = Mathf.Clamp01(height);
                    height = config.HeightRemap.Evaluate(height);

                    // Apply global strength
                    height *= config.GlobalHeightStrength;

                    heightMap[x, z] = height;
                }
            }

            // Store raw heightmap
            context.SetHeightMap(heightMap);
            context.SetMacroMap(macroMap);

            // Normalize if requested
            if (config.NormalizeHeightmap)
            {
                NormalizeHeightmap(context);
            }
            else
            {
                // Copy raw to normalized
                float[,] normalized = new float[resolution, resolution];
                System.Array.Copy(heightMap, normalized, heightMap.Length);
                context.SetNormalizedHeightMap(normalized);
            }

            // Compute slope map
            ComputeSlopeMap(context, config);
        }

        /// <summary>
        /// Normalize the heightmap to use the full 0-1 range.
        /// </summary>
        private static void NormalizeHeightmap(TerrainGenerationContext context)
        {
            int resolution = context.Resolution;
            float[,] normalized = new float[resolution, resolution];

            float min = context.MinHeight;
            float max = context.MaxHeight;
            float range = max - min;

            if (range < 0.001f)
            {
                // Flat terrain - set to middle value
                for (int x = 0; x < resolution; x++)
                {
                    for (int z = 0; z < resolution; z++)
                    {
                        normalized[x, z] = 0.5f;
                    }
                }
            }
            else
            {
                for (int x = 0; x < resolution; x++)
                {
                    for (int z = 0; z < resolution; z++)
                    {
                        normalized[x, z] = (context.HeightMap[x, z] - min) / range;
                    }
                }
            }

            context.SetNormalizedHeightMap(normalized);
        }

        /// <summary>
        /// Compute slope map from the heightmap.
        /// </summary>
        private static void ComputeSlopeMap(TerrainGenerationContext context, TerrainGenerationConfig config)
        {
            int resolution = context.Resolution;
            float[,] slopeMap = new float[resolution, resolution];
            float[,] heightMap = context.NormalizedHeightMap;

            // Scale factor for gradient calculation
            float cellSize = context.WorldSize / (resolution - 1);
            float heightScale = context.TerrainHeight;

            for (int x = 0; x < resolution; x++)
            {
                for (int z = 0; z < resolution; z++)
                {
                    // Sample neighboring heights for gradient
                    int xm = Mathf.Max(0, x - 1);
                    int xp = Mathf.Min(resolution - 1, x + 1);
                    int zm = Mathf.Max(0, z - 1);
                    int zp = Mathf.Min(resolution - 1, z + 1);

                    float hL = heightMap[xm, z] * heightScale;
                    float hR = heightMap[xp, z] * heightScale;
                    float hD = heightMap[x, zm] * heightScale;
                    float hU = heightMap[x, zp] * heightScale;

                    // Compute gradient
                    float dx = (hR - hL) / (cellSize * (xp - xm));
                    float dz = (hU - hD) / (cellSize * (zp - zm));

                    // Slope angle in degrees
                    float gradient = Mathf.Sqrt(dx * dx + dz * dz);
                    float slopeAngle = Mathf.Atan(gradient) * Mathf.Rad2Deg;

                    slopeMap[x, z] = slopeAngle;
                }
            }

            context.SetSlopeMap(slopeMap);
        }

        /// <summary>
        /// Sample Perlin noise at given world coordinates.
        /// </summary>
        private static float SamplePerlin(float worldX, float worldZ, float scale)
        {
            float x = worldX / scale;
            float z = worldZ / scale;
            return Mathf.PerlinNoise(x, z);
        }

        /// <summary>
        /// Sample Fractional Brownian Motion (FBM) noise.
        /// </summary>
        private static float SampleFBM(float worldX, float worldZ, float scale, int octaves, float persistence, float lacunarity)
        {
            float value = 0f;
            float amplitude = 1f;
            float frequency = 1f;
            float maxValue = 0f;

            for (int i = 0; i < octaves; i++)
            {
                float x = worldX / scale * frequency;
                float z = worldZ / scale * frequency;

                value += Mathf.PerlinNoise(x, z) * amplitude;
                maxValue += amplitude;

                amplitude *= persistence;
                frequency *= lacunarity;
            }

            // Normalize to 0-1 range
            return value / maxValue;
        }

        /// <summary>
        /// Apply spawn zone flattening to the heightmap.
        /// </summary>
        public static void ApplySpawnFlattening(TerrainGenerationContext context, TerrainGenerationConfig config, Vector3 spawnWorldPos)
        {
            int resolution = context.Resolution;
            float[,] heightMap = context.NormalizedHeightMap;

            float safeRadius = config.SpawnSafeRadius;
            float blendRadius = config.SpawnBlendRadius;
            float targetHeight = config.SpawnTargetHeight;

            // Convert spawn position to heightmap coordinates
            float spawnNormX = spawnWorldPos.x / context.WorldSize;
            float spawnNormZ = spawnWorldPos.z / context.WorldSize;

            // Sample current height at spawn to use as local target
            var (spawnX, spawnZ) = context.WorldToHeightmap(spawnWorldPos);
            float localTargetHeight = heightMap[spawnX, spawnZ];

            // Blend between local height and config target for more natural result
            float blendedTarget = Mathf.Lerp(localTargetHeight, targetHeight, 0.6f);

            for (int x = 0; x < resolution; x++)
            {
                for (int z = 0; z < resolution; z++)
                {
                    float normX = x / (float)(resolution - 1);
                    float normZ = z / (float)(resolution - 1);

                    float worldX = normX * context.WorldSize;
                    float worldZ = normZ * context.WorldSize;

                    float distanceToSpawn = Vector2.Distance(
                        new Vector2(worldX, worldZ),
                        new Vector2(spawnWorldPos.x, spawnWorldPos.z)
                    );

                    if (distanceToSpawn < blendRadius)
                    {
                        float originalHeight = heightMap[x, z];
                        float flattenedHeight;

                        if (distanceToSpawn < safeRadius)
                        {
                            // Core safe zone - mostly flat with slight variation
                            float microVariation = (Mathf.PerlinNoise(x * 0.1f, z * 0.1f) - 0.5f) * 0.005f;
                            flattenedHeight = blendedTarget + microVariation;
                        }
                        else
                        {
                            // Blend zone - smooth transition
                            float t = (distanceToSpawn - safeRadius) / (blendRadius - safeRadius);
                            // Use smooth step for natural transition
                            t = t * t * (3f - 2f * t);
                            flattenedHeight = Mathf.Lerp(blendedTarget, originalHeight, t);
                        }

                        heightMap[x, z] = flattenedHeight;
                    }
                }
            }

            // Recompute slope map after flattening
            ComputeSlopeMap(context, config);
        }
    }
}

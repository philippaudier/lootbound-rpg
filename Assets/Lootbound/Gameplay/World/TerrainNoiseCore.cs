using UnityEngine;

namespace Lootbound.Gameplay.World
{
    /// <summary>
    /// Centralized terrain noise evaluation used by both ITerrainSampler and TerrainHeightGenerator.
    /// This ensures the sampler produces identical values to the final terrain.
    /// </summary>
    public static class TerrainNoiseCore
    {
        /// <summary>
        /// Precomputed offsets for deterministic noise evaluation.
        /// Generated once from seed, then reused for all evaluations.
        /// </summary>
        public readonly struct NoiseOffsets
        {
            public readonly float MacroOffsetX;
            public readonly float MacroOffsetZ;
            public readonly float RidgeOffsetX;
            public readonly float RidgeOffsetZ;
            public readonly float ValleyOffsetX;
            public readonly float ValleyOffsetZ;
            public readonly float DetailOffsetX;
            public readonly float DetailOffsetZ;
            public readonly float WarpOffsetX;
            public readonly float WarpOffsetZ;

            public NoiseOffsets(int seed)
            {
                var random = new System.Random(seed);
                MacroOffsetX = (float)(random.NextDouble() * 10000);
                MacroOffsetZ = (float)(random.NextDouble() * 10000);
                RidgeOffsetX = (float)(random.NextDouble() * 10000);
                RidgeOffsetZ = (float)(random.NextDouble() * 10000);
                ValleyOffsetX = (float)(random.NextDouble() * 10000);
                ValleyOffsetZ = (float)(random.NextDouble() * 10000);
                DetailOffsetX = (float)(random.NextDouble() * 10000);
                DetailOffsetZ = (float)(random.NextDouble() * 10000);
                WarpOffsetX = (float)(random.NextDouble() * 10000);
                WarpOffsetZ = (float)(random.NextDouble() * 10000);
            }
        }

        /// <summary>
        /// Evaluate raw height at world position. Returns unnormalized height in 0-1 range.
        /// </summary>
        public static float EvaluateHeight(
            float worldX,
            float worldZ,
            in NoiseOffsets offsets,
            TerrainGenerationConfig config)
        {
            float worldSize = config.WorldSize;

            // Apply light domain warping for more organic shapes
            float warpStrength = 0.15f;
            float warpScale = config.MacroScale * 0.5f;
            float warpX = SamplePerlin(worldX + offsets.WarpOffsetX, worldZ + offsets.WarpOffsetZ, warpScale) * warpStrength * worldSize * 0.1f;
            float warpZ = SamplePerlin(worldX + offsets.WarpOffsetX + 1000, worldZ + offsets.WarpOffsetZ + 1000, warpScale) * warpStrength * worldSize * 0.1f;

            float warpedX = worldX + warpX;
            float warpedZ = worldZ + warpZ;

            // 1. Macro terrain - main shape
            float macro = SampleFBM(
                warpedX + offsets.MacroOffsetX,
                warpedZ + offsets.MacroOffsetZ,
                config.MacroScale,
                config.MacroOctaves,
                config.MacroPersistence,
                config.MacroLacunarity
            );

            // 2. Valley features - create low areas and corridors
            float valley = 0f;
            if (config.ValleyStrength > 0f)
            {
                float valleyNoise = SampleFBM(
                    worldX + offsets.ValleyOffsetX,
                    worldZ + offsets.ValleyOffsetZ,
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
                    worldX + offsets.RidgeOffsetX,
                    worldZ + offsets.RidgeOffsetZ,
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
                    worldX + offsets.DetailOffsetX,
                    worldZ + offsets.DetailOffsetZ,
                    config.DetailScale,
                    2,
                    0.5f,
                    2f
                ) * config.DetailStrength;
            }

            // Combine all layers
            float height = macro;
            height -= valley * 0.3f;
            height += ridge * 0.25f;
            height += detail;

            // Apply height remap curve
            height = Mathf.Clamp01(height);
            height = config.HeightRemap.Evaluate(height);

            // Apply global strength
            height *= config.GlobalHeightStrength;

            return height;
        }

        /// <summary>
        /// Evaluate macro terrain contribution only (for biome classification).
        /// </summary>
        public static float EvaluateMacro(
            float worldX,
            float worldZ,
            in NoiseOffsets offsets,
            TerrainGenerationConfig config)
        {
            float worldSize = config.WorldSize;

            // Apply domain warping
            float warpStrength = 0.15f;
            float warpScale = config.MacroScale * 0.5f;
            float warpX = SamplePerlin(worldX + offsets.WarpOffsetX, worldZ + offsets.WarpOffsetZ, warpScale) * warpStrength * worldSize * 0.1f;
            float warpZ = SamplePerlin(worldX + offsets.WarpOffsetX + 1000, worldZ + offsets.WarpOffsetZ + 1000, warpScale) * warpStrength * worldSize * 0.1f;

            return SampleFBM(
                worldX + warpX + offsets.MacroOffsetX,
                worldZ + warpZ + offsets.MacroOffsetZ,
                config.MacroScale,
                config.MacroOctaves,
                config.MacroPersistence,
                config.MacroLacunarity
            );
        }

        /// <summary>
        /// Sample Perlin noise at given world coordinates.
        /// </summary>
        public static float SamplePerlin(float worldX, float worldZ, float scale)
        {
            float x = worldX / scale;
            float z = worldZ / scale;
            return Mathf.PerlinNoise(x, z);
        }

        /// <summary>
        /// Sample Fractional Brownian Motion (FBM) noise.
        /// </summary>
        public static float SampleFBM(float worldX, float worldZ, float scale, int octaves, float persistence, float lacunarity)
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

            return value / maxValue;
        }
    }
}

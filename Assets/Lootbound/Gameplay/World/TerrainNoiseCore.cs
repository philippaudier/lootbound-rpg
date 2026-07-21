using UnityEngine;
using Lootbound.World.Layers.Fields;

namespace Lootbound.Gameplay.World
{
    /// <summary>
    /// Unity-side macro-noise helper feeding the MacroMap (surface painting).
    ///
    /// Since slice T2 the terrain HEIGHT is no longer produced here: it is the
    /// World Engine's <see cref="Lootbound.World.Layers.Fields.HeightField"/>,
    /// a pure Unity-free World Field. This class remains only as the macro
    /// contribution used by the painter, and shares the World layer's
    /// <see cref="NoiseOffsets"/>.
    /// </summary>
    public static class TerrainNoiseCore
    {
        /// <summary>
        /// Evaluate macro terrain contribution only (for biome classification / painting).
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

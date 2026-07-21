using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Lootbound.Gameplay.World;
using Lootbound.Gameplay.World.Providers;
using Lootbound.World.Coordinates;
using Lootbound.World.Layers.Fields;

namespace Lootbound.Tests.EditMode
{
    /// <summary>
    /// The T2 guarantee: ZERO visual change. The new World Engine HeightField
    /// must produce bit-for-(near)bit identical heights to the legacy
    /// TerrainNoiseCore.EvaluateHeight formula it replaced. The legacy formula is
    /// inlined here as an ORACLE (using the real Unity Mathf/Perlin, exactly like
    /// the old code), so the refactor is proven equivalent without keeping dead
    /// code in production.
    /// </summary>
    public class HeightFieldCompatibilityTests
    {
        // The two implementations are algebraically identical (same Perlin via the
        // provider, Mathf.Pow == (float)Math.Pow, equivalent Clamp01/Abs). Any
        // residual is float-eval non-determinism permitted by ECMA-335 (a method
        // may compute float intermediates at higher precision), a few ULP,
        // slightly amplified by the remap curve. 3e-5 normalized is ~9 mm at a
        // 300 m terrain height - visually identical, well under the 2 cm bar.
        private const float Tolerance = 3e-5f;

        private static void SetField(object obj, string name, object value)
        {
            var f = obj.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(f, $"field {name} not found");
            f.SetValue(obj, value);
        }

        private static TerrainGenerationConfig CreateConfig()
        {
            var config = ScriptableObject.CreateInstance<TerrainGenerationConfig>();
            SetField(config, "worldSize", 1024f);
            SetField(config, "macroScale", 450f);
            SetField(config, "macroOctaves", 4);
            SetField(config, "macroPersistence", 0.5f);
            SetField(config, "macroLacunarity", 2f);
            SetField(config, "ridgeScale", 350f);
            SetField(config, "ridgeStrength", 0.25f);
            SetField(config, "valleyScale", 400f);
            SetField(config, "valleyStrength", 0.3f);
            SetField(config, "detailScale", 60f);
            SetField(config, "detailStrength", 0.08f);
            SetField(config, "globalHeightStrength", 1f);
            SetField(config, "heightRemap", new AnimationCurve(new Keyframe(0f, 0.05f), new Keyframe(0.5f, 0.4f), new Keyframe(1f, 1f)));
            return config;
        }

        [Test]
        public void HeightField_MatchesLegacyFormula_AcrossSeedsAndCoordinates()
        {
            var config = CreateConfig();
            int[] seeds = { 1, 42, 99, 704659999 };
            float worldSize = config.WorldSize;

            foreach (int seed in seeds)
            {
                var offsets = new NoiseOffsets(seed);
                HeightField field = WorldFieldComposer.BuildHeightField(config, offsets);

                for (int i = 0; i <= 16; i++)
                {
                    for (int j = 0; j <= 16; j++)
                    {
                        float worldX = (i / 16f) * worldSize;
                        float worldZ = (j / 16f) * worldSize;

                        float engine = field.Evaluate(new WorldCoordinate(worldX, worldZ));
                        float legacy = LegacyEvaluateHeight(worldX, worldZ, offsets, config);

                        Assert.AreEqual(legacy, engine, Tolerance,
                            $"seed {seed} @ ({worldX},{worldZ}): World HeightField diverged from the legacy formula");
                    }
                }
            }

            Object.DestroyImmediate(config);
        }

        // ---- Legacy oracle: an exact copy of the removed TerrainNoiseCore.EvaluateHeight ----

        private static float LegacyEvaluateHeight(float worldX, float worldZ, in NoiseOffsets offsets, TerrainGenerationConfig config)
        {
            float worldSize = config.WorldSize;

            float warpStrength = 0.15f;
            float warpScale = config.MacroScale * 0.5f;
            float warpX = SamplePerlin(worldX + offsets.WarpOffsetX, worldZ + offsets.WarpOffsetZ, warpScale) * warpStrength * worldSize * 0.1f;
            float warpZ = SamplePerlin(worldX + offsets.WarpOffsetX + 1000, worldZ + offsets.WarpOffsetZ + 1000, warpScale) * warpStrength * worldSize * 0.1f;

            float warpedX = worldX + warpX;
            float warpedZ = worldZ + warpZ;

            float macro = SampleFBM(warpedX + offsets.MacroOffsetX, warpedZ + offsets.MacroOffsetZ,
                config.MacroScale, config.MacroOctaves, config.MacroPersistence, config.MacroLacunarity);

            float valley = 0f;
            if (config.ValleyStrength > 0f)
            {
                float valleyNoise = SampleFBM(worldX + offsets.ValleyOffsetX, worldZ + offsets.ValleyOffsetZ, config.ValleyScale, 3, 0.5f, 2f);
                float valleyMask = 1f - Mathf.Clamp01(macro * 1.5f);
                valley = (1f - Mathf.Abs(valleyNoise * 2f - 1f)) * valleyMask;
                valley = Mathf.Pow(valley, 1.5f) * config.ValleyStrength;
            }

            float ridge = 0f;
            if (config.RidgeStrength > 0f)
            {
                float ridgeNoise = SampleFBM(worldX + offsets.RidgeOffsetX, worldZ + offsets.RidgeOffsetZ, config.RidgeScale, 3, 0.45f, 2.1f);
                ridge = 1f - Mathf.Abs(ridgeNoise * 2f - 1f);
                ridge = Mathf.Pow(ridge, 2f);
                float ridgeMask = Mathf.Clamp01((macro - 0.4f) * 2f);
                ridge *= ridgeMask * config.RidgeStrength;
            }

            float detail = 0f;
            if (config.DetailStrength > 0f)
            {
                detail = SampleFBM(worldX + offsets.DetailOffsetX, worldZ + offsets.DetailOffsetZ, config.DetailScale, 2, 0.5f, 2f) * config.DetailStrength;
            }

            float height = macro;
            height -= valley * 0.3f;
            height += ridge * 0.25f;
            height += detail;

            height = Mathf.Clamp01(height);
            height = config.HeightRemap.Evaluate(height);
            height *= config.GlobalHeightStrength;

            return height;
        }

        private static float SamplePerlin(float worldX, float worldZ, float scale)
            => Mathf.PerlinNoise(worldX / scale, worldZ / scale);

        private static float SampleFBM(float worldX, float worldZ, float scale, int octaves, float persistence, float lacunarity)
        {
            float value = 0f, amplitude = 1f, frequency = 1f, maxValue = 0f;
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

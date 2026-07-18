using NUnit.Framework;
using Lootbound.Gameplay.World;

namespace Lootbound.Tests.EditMode
{
    /// <summary>
    /// Regression tests for the terrain height-space bug: with
    /// normalizeHeightmap enabled, the raw HeightMap and the
    /// NormalizedHeightMap live in different spaces, and only the normalized
    /// map is applied to the Unity Terrain. Any world-space height query must
    /// therefore read the normalized map, otherwise spawned content floats
    /// above (or sinks below) the real ground.
    /// </summary>
    public class TerrainHeightSpaceTests
    {
        private const float EPSILON = 0.0001f;
        private const int RESOLUTION = 33;
        private const float WORLD_SIZE = 1024f;
        private const float TERRAIN_HEIGHT = 200f;

        private TerrainGenerationContext CreateContext()
        {
            return new TerrainGenerationContext(1, RESOLUTION, WORLD_SIZE, TERRAIN_HEIGHT);
        }

        private static float[,] FilledMap(float value)
        {
            var map = new float[RESOLUTION, RESOLUTION];
            for (int x = 0; x < RESOLUTION; x++)
            {
                for (int z = 0; z < RESOLUTION; z++)
                {
                    map[x, z] = value;
                }
            }
            return map;
        }

        [Test]
        public void SampleHeightAtWorld_UsesTerrainAppliedHeights_NotRawNoise()
        {
            var context = CreateContext();

            // Raw and normalized maps deliberately diverge, as they do when
            // normalizeHeightmap stretches the raw range to 0-1.
            context.SetHeightMap(FilledMap(0.2f));
            context.SetNormalizedHeightMap(FilledMap(0.8f));

            float sampled = context.SampleHeightAtWorld(WORLD_SIZE * 0.5f, WORLD_SIZE * 0.5f);

            Assert.AreEqual(0.8f * TERRAIN_HEIGHT, sampled, EPSILON,
                "SampleHeightAtWorld must read the normalized map (the real ground), not the raw pre-normalization noise");
        }

        [Test]
        public void NormalizeToFullRange_StretchesToFullRange()
        {
            // X gradient over a narrow raw band [0.2, 0.6]
            var map = new float[RESOLUTION, RESOLUTION];
            for (int x = 0; x < RESOLUTION; x++)
            {
                for (int z = 0; z < RESOLUTION; z++)
                {
                    map[x, z] = 0.2f + 0.4f * (x / (float)(RESOLUTION - 1));
                }
            }

            var normalized = TerrainHeightGenerator.NormalizeToFullRange(map, 0.2f, 0.6f);

            Assert.AreEqual(0f, normalized[0, 0], EPSILON, "Raw minimum must map to 0");
            Assert.AreEqual(1f, normalized[RESOLUTION - 1, 0], EPSILON, "Raw maximum must map to 1");
            Assert.AreEqual(0.5f, normalized[(RESOLUTION - 1) / 2, 0], 0.02f, "Raw midpoint must map near 0.5");
        }

        [Test]
        public void NormalizeToFullRange_AmplifiesGradientsByInverseRange()
        {
            // Linear map with raw gradient g: the normalized gradient must be
            // exactly g / range. This is the seed-dependent slope amplification
            // that makes normalized presets unpredictable.
            const float rawMin = 0.3f;
            const float rawMax = 0.55f;
            const float range = rawMax - rawMin;

            var map = new float[RESOLUTION, RESOLUTION];
            for (int x = 0; x < RESOLUTION; x++)
            {
                for (int z = 0; z < RESOLUTION; z++)
                {
                    map[x, z] = rawMin + range * (x / (float)(RESOLUTION - 1));
                }
            }

            var normalized = TerrainHeightGenerator.NormalizeToFullRange(map, rawMin, rawMax);

            float rawGradient = map[1, 0] - map[0, 0];
            float normalizedGradient = normalized[1, 0] - normalized[0, 0];

            Assert.AreEqual(rawGradient / range, normalizedGradient, EPSILON,
                "Normalization must amplify gradients by exactly 1/(max-min)");
        }

        [Test]
        public void NormalizeToFullRange_NearFlatInput_MapsToMidValue()
        {
            var map = FilledMap(0.42f);
            var normalized = TerrainHeightGenerator.NormalizeToFullRange(map, 0.42f, 0.42f);

            for (int x = 0; x < RESOLUTION; x += 8)
            {
                for (int z = 0; z < RESOLUTION; z += 8)
                {
                    Assert.AreEqual(0.5f, normalized[x, z], EPSILON,
                        "A near-flat map (range < 0.001) must normalize to a uniform 0.5");
                }
            }
        }

        [Test]
        public void NormalizeToFullRange_FullRangeInput_IsUnchanged()
        {
            var map = new float[RESOLUTION, RESOLUTION];
            for (int x = 0; x < RESOLUTION; x++)
            {
                for (int z = 0; z < RESOLUTION; z++)
                {
                    map[x, z] = x / (float)(RESOLUTION - 1);
                }
            }

            var normalized = TerrainHeightGenerator.NormalizeToFullRange(map, 0f, 1f);

            for (int x = 0; x < RESOLUTION; x += 4)
            {
                Assert.AreEqual(map[x, 0], normalized[x, 0], EPSILON,
                    "A map already spanning 0-1 must be unchanged by normalization");
            }
        }

        [Test]
        public void SampleHeightAtWorld_MatchesTerrainDataAtGridPoints()
        {
            var context = CreateContext();
            context.SetHeightMap(FilledMap(0.1f));

            // Non-uniform normalized map (X gradient)
            var normalized = new float[RESOLUTION, RESOLUTION];
            for (int x = 0; x < RESOLUTION; x++)
            {
                for (int z = 0; z < RESOLUTION; z++)
                {
                    normalized[x, z] = x / (float)(RESOLUTION - 1);
                }
            }
            context.SetNormalizedHeightMap(normalized);

            // The exact data written to the Unity Terrain ([z, x] indexing)
            float[,] terrainData = context.GetTerrainHeightmapData();

            int[] gridPoints = { 0, 8, 16, 24, RESOLUTION - 1 };
            foreach (int x in gridPoints)
            {
                foreach (int z in gridPoints)
                {
                    float worldX = (x / (float)(RESOLUTION - 1)) * WORLD_SIZE;
                    float worldZ = (z / (float)(RESOLUTION - 1)) * WORLD_SIZE;

                    float expected = terrainData[z, x] * TERRAIN_HEIGHT;
                    float sampled = context.SampleHeightAtWorld(worldX, worldZ);

                    Assert.AreEqual(expected, sampled, EPSILON,
                        $"Grid point ({x},{z}): sampled height must equal the height written to the Unity Terrain");
                }
            }
        }
    }
}

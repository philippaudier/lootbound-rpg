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

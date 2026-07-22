using NUnit.Framework;
using UnityEngine;
using Lootbound.Gameplay.World;

namespace Lootbound.Tests.EditMode
{
    public class TerrainSurfacePainterTests
    {
        [Test]
        public void CalculateLayerWeights_NonAllocOverload_MatchesTheAllocatingOne()
        {
            var config = ScriptableObject.CreateInstance<TerrainGenerationConfig>();
            try
            {
                var into = new float[4];
                float[][] cases =
                {
                    new[] { 0.1f, 5f },   // low and flat -> grass
                    new[] { 0.5f, 20f },  // mid elevation -> dry ground
                    new[] { 0.9f, 10f },  // high -> highland
                    new[] { 0.5f, 50f },  // steep -> rock
                };

                foreach (float[] c in cases)
                {
                    float[] expected = TerrainSurfacePainter.CalculateLayerWeights(c[0], c[1], 0f, 0.5f, 0.5f, config);
                    TerrainSurfacePainter.CalculateLayerWeights(c[0], c[1], 0f, 0.5f, 0.5f, config, into);
                    for (int i = 0; i < 4; i++)
                    {
                        Assert.AreEqual(expected[i], into[i], 0f, $"h={c[0]} s={c[1]} layer {i}");
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }
    }
}

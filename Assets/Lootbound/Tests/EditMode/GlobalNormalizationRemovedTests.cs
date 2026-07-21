using System.Reflection;
using NUnit.Framework;
using Lootbound.Gameplay.World;

namespace Lootbound.Tests.EditMode
{
    /// <summary>
    /// Architectural guard: global normalization is definitively gone. No pass
    /// may depend on the world's min/max height any more (slice T2).
    /// </summary>
    public class GlobalNormalizationRemovedTests
    {
        [Test]
        public void NormalizeToFullRange_MethodIsRemoved()
        {
            var method = typeof(TerrainHeightGenerator).GetMethod(
                "NormalizeToFullRange", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNull(method, "global min/max normalization must no longer exist");
        }

        [Test]
        public void Config_NormalizeHeightmapFlagIsRemoved()
        {
            Assert.IsNull(typeof(TerrainGenerationConfig).GetProperty("NormalizeHeightmap"),
                "the normalizeHeightmap flag must be gone; relief is defined only by noise + remap curves");
        }
    }
}

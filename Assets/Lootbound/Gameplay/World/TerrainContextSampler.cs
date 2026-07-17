using Lootbound.Gameplay.World.Layout;

namespace Lootbound.Gameplay.World
{
    /// <summary>
    /// Adapts TerrainGenerationContext to ITerrainSampler.
    /// Single conversion authority for the pipeline height space
    /// (NormalizedHeightMap -> terrain-local height -> world-space Y):
    /// layout generation, terrain validation, and content spawning all sample
    /// heights and slopes through it instead of converting on their own.
    /// </summary>
    public sealed class TerrainContextSampler : ITerrainSampler
    {
        private readonly TerrainGenerationContext _context;

        public TerrainContextSampler(TerrainGenerationContext context)
        {
            _context = context;
        }

        public float WorldSize => _context.WorldSize;
        public float TerrainHeight => _context.TerrainHeight;

        public float SampleHeight(float worldX, float worldZ)
        {
            return _context.SampleHeightAtWorld(worldX, worldZ);
        }

        public float SampleSlope(float worldX, float worldZ)
        {
            return _context.SampleSlopeAtWorld(worldX, worldZ);
        }

        public bool IsWithinBounds(float worldX, float worldZ)
        {
            return worldX >= 0f && worldX <= _context.WorldSize &&
                   worldZ >= 0f && worldZ <= _context.WorldSize;
        }
    }
}

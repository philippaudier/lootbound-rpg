using Lootbound.Gameplay.World.Layout;

namespace Lootbound.Gameplay.World.Spawning
{
    /// <summary>
    /// Adapts the post-generation TerrainGenerationContext to ITerrainSampler
    /// so the content planner can validate placements against the final,
    /// corrected heightmap (the noise sampler predates layout flattening).
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

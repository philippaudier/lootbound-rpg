namespace Lootbound.Gameplay.World.Layout
{
    /// <summary>
    /// ITerrainSampler implementation that queries TerrainNoiseCore.
    /// Used by WorldLayoutGenerator to evaluate terrain before Unity Terrain exists.
    /// </summary>
    public sealed class TerrainNoiseSampler : ITerrainSampler
    {
        private readonly TerrainNoiseCore.NoiseOffsets _offsets;
        private readonly TerrainGenerationConfig _config;

        public float WorldSize => _config.WorldSize;
        public float TerrainHeight => _config.TerrainHeight;

        public TerrainNoiseSampler(int seed, TerrainGenerationConfig config)
        {
            _offsets = new TerrainNoiseCore.NoiseOffsets(seed);
            _config = config;
        }

        public float SampleHeight(float worldX, float worldZ)
        {
            float normalizedHeight = TerrainNoiseCore.EvaluateHeight(worldX, worldZ, _offsets, _config);
            return normalizedHeight * _config.TerrainHeight;
        }

        public float SampleSlope(float worldX, float worldZ)
        {
            return TerrainNoiseCore.EvaluateSlope(worldX, worldZ, _offsets, _config);
        }

        public bool IsWithinBounds(float worldX, float worldZ)
        {
            return worldX >= 0 && worldX <= _config.WorldSize &&
                   worldZ >= 0 && worldZ <= _config.WorldSize;
        }

        /// <summary>
        /// Get the noise offsets for this sampler (for use by heightmap generator).
        /// </summary>
        public TerrainNoiseCore.NoiseOffsets GetOffsets()
        {
            return _offsets;
        }
    }
}

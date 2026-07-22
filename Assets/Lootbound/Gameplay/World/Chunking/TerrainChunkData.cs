namespace Lootbound.Gameplay.World.Chunking
{
    /// <summary>
    /// The built data for one chunk: everything a <see cref="TerrainChunk"/> needs
    /// to display, and nothing about Unity. It is produced by the
    /// <see cref="TerrainChunkBuilder"/> from the generator's samples and is pure
    /// data, so it could equally come from a save file, the network or an import
    /// later without touching the chunk that displays it.
    /// </summary>
    public sealed class TerrainChunkData
    {
        public TerrainChunkCoordinate Coordinate { get; }

        /// <summary>Side length of the chunk in world metres.</summary>
        public float ChunkWorldSize { get; }

        /// <summary>Vertical scale in metres (Unity heightmaps store 0..1 of this).</summary>
        public float TerrainHeight { get; }

        /// <summary>Heightmap resolution (Resolution x Resolution, a 2^n+1 grid).</summary>
        public int Resolution { get; }

        /// <summary>
        /// Normalized heights in [0,1], indexed [z, x] to match Unity Terrain's
        /// <c>SetHeights</c> convention, so the chunk applies them verbatim.
        /// </summary>
        public float[,] Heights { get; }

        /// <summary>Alphamap resolution, or 0 when the chunk carries no splat.</summary>
        public int AlphamapResolution { get; }

        /// <summary>
        /// Splat weights indexed [z, x, layer] for <c>SetAlphamaps</c>, or null
        /// when the height sampler provides no surface classification (chunks then
        /// show their first layer).
        /// </summary>
        public float[,,] Alphamaps { get; }

        public TerrainChunkData(
            TerrainChunkCoordinate coordinate,
            float chunkWorldSize,
            float terrainHeight,
            int resolution,
            float[,] heights,
            int alphamapResolution = 0,
            float[,,] alphamaps = null)
        {
            Coordinate = coordinate;
            ChunkWorldSize = chunkWorldSize;
            TerrainHeight = terrainHeight;
            Resolution = resolution;
            Heights = heights;
            AlphamapResolution = alphamapResolution;
            Alphamaps = alphamaps;
        }
    }
}

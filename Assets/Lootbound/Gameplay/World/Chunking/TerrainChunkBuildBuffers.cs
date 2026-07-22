namespace Lootbound.Gameplay.World.Chunking
{
    /// <summary>
    /// A reusable set of build buffers (margin grid, height grid, alphamaps,
    /// weights) lent to one <see cref="TerrainChunkBuildState"/> at a time by the
    /// scheduler. Arrays are only reallocated when dimensions change, so steady
    /// streaming performs no large per-build allocations.
    ///
    /// Lifecycle: the scheduler lends a set to the running build; the finished
    /// <see cref="TerrainChunkData"/> then VIEWS these arrays until the streamer
    /// has applied it and calls the scheduler's ReleaseBuffers - after which the
    /// set may be reused and the old data must not be read again. The
    /// synchronous <c>TerrainChunkBuilder.Build</c> path does not use pooled
    /// buffers and keeps its arrays private.
    /// </summary>
    public sealed class TerrainChunkBuildBuffers
    {
        public float[,] Margin;
        public float[,] Heights;
        public float[,,] Alphamaps;
        public float[] Weights;

        public void Ensure(int resolution, int alphamapResolution, int splatLayers)
        {
            int marginSide = resolution + 2;
            if (Margin == null || Margin.GetLength(0) != marginSide)
            {
                Margin = new float[marginSide, marginSide];
            }
            if (Heights == null || Heights.GetLength(0) != resolution)
            {
                Heights = new float[resolution, resolution];
            }

            if (alphamapResolution > 1 && splatLayers > 0)
            {
                if (Alphamaps == null ||
                    Alphamaps.GetLength(0) != alphamapResolution ||
                    Alphamaps.GetLength(2) != splatLayers)
                {
                    Alphamaps = new float[alphamapResolution, alphamapResolution, splatLayers];
                }
                if (Weights == null || Weights.Length != splatLayers)
                {
                    Weights = new float[splatLayers];
                }
            }
        }
    }
}

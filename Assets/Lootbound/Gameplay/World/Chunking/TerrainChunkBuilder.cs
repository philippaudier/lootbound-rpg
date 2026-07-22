using Unity.Profiling;
using UnityEngine;

namespace Lootbound.Gameplay.World.Chunking
{
    /// <summary>
    /// Builds a <see cref="TerrainChunkData"/> for a chunk by asking the world's
    /// height sampler for a grid of heights. This is the only bridge between the
    /// generator and Unity's terrain data: it calls the generator, fills the
    /// heightmap, and returns pure data. It never decides which chunks exist and
    /// owns no Unity object.
    ///
    /// Seams are exact by construction: the grid samples both endpoints of the
    /// chunk (step = size / (resolution - 1)), so a chunk's last row/column is
    /// evaluated at exactly the same world coordinate as its neighbour's first
    /// row/column. Same coordinate, same sampler, same height - no crack.
    /// </summary>
    public sealed class TerrainChunkBuilder
    {
        private static readonly ProfilerMarker TotalBuildMarker = new ProfilerMarker("Chunk.TotalBuild");

        private readonly IWorldHeightSampler _sampler;

        public TerrainChunkBuilder(IWorldHeightSampler sampler)
        {
            _sampler = sampler;
        }

        /// <summary>
        /// Begin an incremental build. The caller (normally the scheduler) drives
        /// the returned state with Advance() under its own time budget.
        /// </summary>
        public TerrainChunkBuildState CreateBuildState(
            TerrainChunkCoordinate coordinate, int resolution, float chunkWorldSize, int alphamapResolution = 0,
            TerrainChunkBuildBuffers buffers = null)
        {
            return new TerrainChunkBuildState(_sampler, coordinate, resolution, chunkWorldSize, alphamapResolution, buffers);
        }

        /// <summary>
        /// Build a chunk's data synchronously (tests, editor tools, benchmarks).
        /// Same single implementation as the incremental path: it just runs the
        /// build state to completion with an unlimited deadline.
        /// </summary>
        public TerrainChunkData Build(
            TerrainChunkCoordinate coordinate, int resolution, float chunkWorldSize, int alphamapResolution = 0)
        {
            using (TotalBuildMarker.Auto())
            {
                TerrainChunkBuildState state = CreateBuildState(coordinate, resolution, chunkWorldSize, alphamapResolution);
                var clock = System.Diagnostics.Stopwatch.StartNew();
                while (!state.Advance(clock, long.MaxValue))
                {
                }
                return state.Result;
            }
        }
    }
}

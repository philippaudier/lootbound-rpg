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
        private readonly IWorldHeightSampler _sampler;

        public TerrainChunkBuilder(IWorldHeightSampler sampler)
        {
            _sampler = sampler;
        }

        public TerrainChunkData Build(TerrainChunkCoordinate coordinate, int resolution, float chunkWorldSize)
        {
            if (resolution < 2)
            {
                resolution = 2;
            }

            float terrainHeight = _sampler.TerrainHeight;
            float invHeight = terrainHeight > 0f ? 1f / terrainHeight : 0f;
            int last = resolution - 1;

            // World coordinate as (chunkIndex + fraction) * size. At the far edge
            // the fraction is exactly x/(res-1) = 1.0, so the neighbour chunk
            // evaluates the SAME expression (index+1)+0.0 at that boundary - the
            // shared row/column is bit-identical, no crack, for any size/resolution.
            float[,] heights = new float[resolution, resolution]; // [z, x] for SetHeights
            for (int z = 0; z < resolution; z++)
            {
                double worldZ = (coordinate.Z + z / (double)last) * chunkWorldSize;
                for (int x = 0; x < resolution; x++)
                {
                    double worldX = (coordinate.X + x / (double)last) * chunkWorldSize;
                    float metres = _sampler.SampleHeight(worldX, worldZ);
                    heights[z, x] = Mathf.Clamp01(metres * invHeight);
                }
            }

            return new TerrainChunkData(coordinate, chunkWorldSize, terrainHeight, resolution, heights);
        }
    }
}

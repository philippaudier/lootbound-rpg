using System.Diagnostics;
using Unity.Profiling;
using UnityEngine;

namespace Lootbound.Gameplay.World.Chunking
{
    /// <summary>
    /// The incremental construction of one chunk's data - an explicit state
    /// machine, deliberately NOT named a "Job" (real Unity Jobs/Burst may exist
    /// one day; this is a plain main-thread process sliced by rows).
    ///
    /// The states tell the real dependencies:
    ///   SamplingHeights  - sample the height grid, with a one-cell world margin
    ///   BuildingSurface  - DERIVE the surface from that buffer: height and slope
    ///                      are read from the sampled grid (the margin makes
    ///                      border slopes read true world neighbours, so texture
    ///                      weights match the adjacent chunk bit-for-bit); only
    ///                      the classification itself is asked of the generator
    ///   Done             - the immutable TerrainChunkData is ready
    ///
    /// The scheduler that drives it sees none of this: it only calls
    /// <see cref="Advance"/> until the state reports finished. Every call
    /// processes at least one row before honouring the deadline, so even a zero
    /// budget makes progress.
    /// </summary>
    public sealed class TerrainChunkBuildState
    {
        private static readonly ProfilerMarker SampleHeightsMarker = new ProfilerMarker("Chunk.SampleHeights");
        private static readonly ProfilerMarker BuildSurfaceMarker = new ProfilerMarker("Chunk.SampleSurface");

        private enum Step
        {
            SamplingHeights,
            BuildingSurface,
            Done,
        }

        public TerrainChunkCoordinate Coordinate { get; }
        public bool IsFinished => _step == Step.Done;

        /// <summary>The built data; non-null once finished.</summary>
        public TerrainChunkData Result { get; private set; }

        private readonly IWorldHeightSampler _sampler;
        private readonly IWorldSplatSampler _splat;
        private readonly int _resolution;
        private readonly int _alphamapResolution;
        private readonly float _chunkWorldSize;
        private readonly float _terrainHeight;
        private readonly float _invHeight;

        // Normalized heights with a one-cell margin on every side (the margin is
        // only used to compute seam-consistent border slopes, never displayed).
        private readonly float[,] _margin;
        private readonly float[,] _heights;   // interior [res, res], [z, x]
        private readonly float[,,] _alphamaps;
        private readonly float[] _weights;

        private Step _step = Step.SamplingHeights;
        private int _row;

        public TerrainChunkBuildState(
            IWorldHeightSampler sampler,
            TerrainChunkCoordinate coordinate,
            int resolution,
            float chunkWorldSize,
            int alphamapResolution,
            TerrainChunkBuildBuffers buffers = null)
        {
            _sampler = sampler;
            _splat = sampler as IWorldSplatSampler;
            Coordinate = coordinate;
            _resolution = resolution < 2 ? 2 : resolution;
            _chunkWorldSize = chunkWorldSize;
            _terrainHeight = sampler.TerrainHeight;
            _invHeight = _terrainHeight > 0f ? 1f / _terrainHeight : 0f;

            bool paint = alphamapResolution > 1 && _splat != null;
            _alphamapResolution = paint ? alphamapResolution : 0;
            int layers = paint ? _splat.SplatLayerCount : 0;

            if (buffers != null)
            {
                // Borrowed set (streaming path): every cell is overwritten during
                // the build, so stale contents are harmless.
                buffers.Ensure(_resolution, _alphamapResolution, layers);
                _margin = buffers.Margin;
                _heights = buffers.Heights;
                _alphamaps = paint ? buffers.Alphamaps : null;
                _weights = paint ? buffers.Weights : null;
            }
            else
            {
                // Private arrays (synchronous Build path): the result stays
                // immutable for its consumer.
                _margin = new float[_resolution + 2, _resolution + 2];
                _heights = new float[_resolution, _resolution];
                _alphamaps = paint ? new float[_alphamapResolution, _alphamapResolution, layers] : null;
                _weights = paint ? new float[layers] : null;
            }
        }

        /// <summary>
        /// Work until the shared clock reaches <paramref name="deadlineTicks"/>,
        /// one row at a time (always at least one row). True once finished.
        /// </summary>
        public bool Advance(Stopwatch clock, long deadlineTicks)
        {
            bool progressed = false;
            while (true)
            {
                if (_step == Step.Done)
                {
                    return true;
                }
                if (progressed && clock.ElapsedTicks >= deadlineTicks)
                {
                    return false;
                }

                if (_step == Step.SamplingHeights)
                {
                    using (SampleHeightsMarker.Auto())
                    {
                        SampleOneHeightRow();
                    }
                }
                else
                {
                    using (BuildSurfaceMarker.Auto())
                    {
                        BuildOneSurfaceRow();
                    }
                }
                progressed = true;
            }
        }

        private void SampleOneHeightRow()
        {
            int last = _resolution - 1;
            int marginSide = _resolution + 2;
            int m = _row;

            // Margin index -> fraction (m-1)/last: row 0 sits one cell outside the
            // chunk, row 1 is the chunk's own first row (fraction 0), etc. All
            // fractions are exact dyadics (last is a power of two), so shared
            // world coordinates are bit-identical across neighbouring chunks.
            double fz = (m - 1) / (double)last;
            double worldZ = (Coordinate.Z + fz) * _chunkWorldSize;
            for (int c = 0; c < marginSide; c++)
            {
                double fx = (c - 1) / (double)last;
                double worldX = (Coordinate.X + fx) * _chunkWorldSize;
                _margin[m, c] = Mathf.Clamp01(_sampler.SampleHeight(worldX, worldZ) * _invHeight);
            }

            _row++;
            if (_row >= marginSide)
            {
                ExtractInterior();
                _row = 0;
                if (_alphamaps == null)
                {
                    Finish();
                }
                else
                {
                    _step = Step.BuildingSurface;
                }
            }
        }

        private void ExtractInterior()
        {
            for (int z = 0; z < _resolution; z++)
            {
                for (int x = 0; x < _resolution; x++)
                {
                    _heights[z, x] = _margin[z + 1, x + 1];
                }
            }
        }

        private void BuildOneSurfaceRow()
        {
            int a = _alphamapResolution;
            int alast = a - 1;
            int last = _resolution - 1;
            int layers = _splat.SplatLayerCount;
            int z = _row;

            double fz = z / (double)alast;
            double worldZ = (Coordinate.Z + fz) * _chunkWorldSize;
            float bz = (float)(fz * last); // position in interior buffer space

            for (int x = 0; x < a; x++)
            {
                double fx = x / (double)alast;
                double worldX = (Coordinate.X + fx) * _chunkWorldSize;
                float bx = (float)(fx * last);

                float height = SampleBufferHeight(bx, bz);
                float slope = SampleBufferSlope(bx, bz);
                _splat.SampleSplat(worldX, worldZ, height, slope, _weights);

                for (int l = 0; l < layers; l++)
                {
                    _alphamaps[z, x, l] = _weights[l];
                }
            }

            _row++;
            if (_row >= a)
            {
                Finish();
            }
        }

        /// <summary>Bilinear normalized height from the interior of the buffer.</summary>
        private float SampleBufferHeight(float bx, float bz)
        {
            int last = _resolution - 1;
            int x0 = Mathf.Clamp(Mathf.FloorToInt(bx), 0, last - 1);
            int z0 = Mathf.Clamp(Mathf.FloorToInt(bz), 0, last - 1);
            float tx = bx - x0;
            float tz = bz - z0;

            float h00 = _margin[z0 + 1, x0 + 1];
            float h10 = _margin[z0 + 1, x0 + 2];
            float h01 = _margin[z0 + 2, x0 + 1];
            float h11 = _margin[z0 + 2, x0 + 2];

            float h0 = h00 + (h10 - h00) * tx;
            float h1 = h01 + (h11 - h01) * tx;
            return h0 + (h1 - h0) * tz;
        }

        /// <summary>
        /// Slope (degrees) from a central difference at the nearest buffer cell.
        /// Border cells read the margin, so both chunks sharing an edge compute
        /// the slope from the same world samples - no texture seam.
        /// </summary>
        private float SampleBufferSlope(float bx, float bz)
        {
            int last = _resolution - 1;
            int cx = Mathf.Clamp(Mathf.RoundToInt(bx), 0, last) + 1;
            int cz = Mathf.Clamp(Mathf.RoundToInt(bz), 0, last) + 1;

            float cell = _chunkWorldSize / last;
            float inv = _terrainHeight / (2f * cell);
            float dhx = (_margin[cz, cx + 1] - _margin[cz, cx - 1]) * inv;
            float dhz = (_margin[cz + 1, cx] - _margin[cz - 1, cx]) * inv;
            return Mathf.Atan(Mathf.Sqrt(dhx * dhx + dhz * dhz)) * Mathf.Rad2Deg;
        }

        private void Finish()
        {
            Result = new TerrainChunkData(
                Coordinate, _chunkWorldSize, _terrainHeight, _resolution, _heights,
                _alphamaps != null ? _alphamapResolution : 0, _alphamaps);
            _step = Step.Done;
        }
    }
}

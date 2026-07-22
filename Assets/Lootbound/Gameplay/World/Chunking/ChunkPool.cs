using System;
using System.Collections.Generic;

namespace Lootbound.Gameplay.World.Chunking
{
    /// <summary>
    /// A pool of reusable <see cref="TerrainChunk"/> instances. During normal
    /// movement chunks leave the active set and return here rather than being
    /// destroyed, and new required chunks are taken from here rather than being
    /// created - so a steady walk performs no Instantiate/Destroy once the pool is
    /// warm, and the number of live Unity Terrains stays bounded.
    /// </summary>
    public sealed class ChunkPool
    {
        private readonly Stack<TerrainChunk> _free = new Stack<TerrainChunk>();
        private readonly Func<TerrainChunk> _factory;

        /// <summary>Total instances ever created - the ceiling on live Unity Terrains.</summary>
        public int TotalCreated { get; private set; }

        public int FreeCount => _free.Count;

        public ChunkPool(Func<TerrainChunk> factory)
        {
            _factory = factory;
        }

        /// <summary>
        /// Create instances up front so streaming never pays instance creation
        /// mid-play. Counts toward <see cref="TotalCreated"/>.
        /// </summary>
        public void Prewarm(int count)
        {
            while (TotalCreated < count)
            {
                TotalCreated++;
                _free.Push(_factory());
            }
        }

        public TerrainChunk Acquire()
        {
            if (_free.Count > 0)
            {
                return _free.Pop();
            }
            TotalCreated++;
            return _factory();
        }

        public void Release(TerrainChunk chunk)
        {
            if (chunk == null)
            {
                return;
            }
            chunk.Recycle();
            _free.Push(chunk);
        }

        /// <summary>Destroy every pooled instance; teardown only.</summary>
        public void DisposeAll()
        {
            while (_free.Count > 0)
            {
                _free.Pop().Dispose();
            }
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lootbound.Gameplay.World.Chunking
{
    /// <summary>
    /// Streams terrain chunks around the player: it decides which chunks must
    /// exist, asks the <see cref="TerrainChunkBuilder"/> to build the missing
    /// ones, and returns the far ones to the <see cref="ChunkPool"/>. It owns the
    /// active set and the pool - nothing about how a chunk is built or displayed,
    /// and nothing about the generator beyond its sampling contract.
    ///
    /// It is an ordinary instance with injected dependencies: no static instance,
    /// no scene-wide search. Several streamers (menu, Refuge, a dungeon, a test)
    /// can run at once, each with its own player, sampler and pool.
    /// </summary>
    public sealed class TerrainChunkStreamer : MonoBehaviour
    {
        [Header("Bindings (scene use)")]
        [SerializeField] private ProceduralTerrainGenerator generator;
        [SerializeField] private Transform player;
        [SerializeField] private TerrainLayer[] terrainLayers;
        [Tooltip("Optional parent the active chunks are nested under (e.g. an 'ActiveChunks' object). If empty, the streamer creates its own container.")]
        [SerializeField] private Transform chunkRoot;

        [Header("Streaming")]
        [SerializeField] private float chunkWorldSize = 128f;
        [SerializeField] private int heightmapResolution = 129;
        [SerializeField] private int activeRadiusInChunks = 2;
        [Tooltip("At most this many chunks are built per tick, so a big jump spreads its work over a few frames rather than one spike.")]
        [SerializeField] private int maxBuildsPerTick = 4;

        private IWorldHeightSampler _sampler;
        private Transform _player;
        private Transform _chunkRoot;
        private TerrainChunkBuilder _builder;
        private ChunkPool _pool;
        private readonly Dictionary<TerrainChunkCoordinate, TerrainChunk> _active =
            new Dictionary<TerrainChunkCoordinate, TerrainChunk>();
        private readonly List<TerrainChunkCoordinate> _toRelease = new List<TerrainChunkCoordinate>();
        private bool _initialized;

        public int ActiveChunkCount => _active.Count;
        public int PooledChunkCount => _pool?.FreeCount ?? 0;
        public int InstancesCreated => _pool?.TotalCreated ?? 0;

        /// <summary>
        /// Wire the streamer explicitly. Used by tests and by any caller that
        /// wants its own streamer without relying on serialized scene bindings.
        /// </summary>
        public void Initialize(IWorldHeightSampler sampler, Transform playerTransform,
            Transform chunkRoot = null, TerrainLayer[] layers = null)
        {
            _sampler = sampler ?? throw new ArgumentNullException(nameof(sampler));
            _player = playerTransform;
            TerrainLayer[] useLayers = layers ?? terrainLayers;

            _chunkRoot = chunkRoot;
            if (_chunkRoot == null)
            {
                var root = new GameObject("Chunks");
                root.transform.SetParent(transform, false);
                _chunkRoot = root.transform;
            }

            _builder = new TerrainChunkBuilder(_sampler);
            _pool = new ChunkPool(() => new TerrainChunk(_chunkRoot, useLayers));
            _initialized = true;
        }

        private void Start()
        {
            if (_initialized)
            {
                return;
            }
            if (generator != null && player != null)
            {
                Initialize(generator, player, chunkRoot, terrainLayers);
            }
        }

        private void Update()
        {
            Tick();
        }

        /// <summary>
        /// One streaming step: release chunks out of range, then build missing
        /// in-range chunks up to the per-tick budget. Public so tests (and any
        /// deterministic driver) can advance streaming without waiting on frames.
        /// </summary>
        public void Tick()
        {
            if (!_initialized || _player == null || _sampler == null || !_sampler.IsReady)
            {
                return;
            }

            Vector3 p = _player.position;
            TerrainChunkCoordinate center = TerrainChunkCoordinate.FromWorld(p.x, p.z, chunkWorldSize);

            // Release everything outside the active radius.
            _toRelease.Clear();
            foreach (KeyValuePair<TerrainChunkCoordinate, TerrainChunk> kv in _active)
            {
                if (ChebyshevDistance(kv.Key, center) > activeRadiusInChunks)
                {
                    _toRelease.Add(kv.Key);
                }
            }
            for (int i = 0; i < _toRelease.Count; i++)
            {
                TerrainChunkCoordinate coord = _toRelease[i];
                _pool.Release(_active[coord]);
                _active.Remove(coord);
            }

            // Build the missing in-range chunks, budgeted per tick.
            int budget = maxBuildsPerTick > 0 ? maxBuildsPerTick : int.MaxValue;
            for (int dz = -activeRadiusInChunks; dz <= activeRadiusInChunks; dz++)
            {
                for (int dx = -activeRadiusInChunks; dx <= activeRadiusInChunks; dx++)
                {
                    var coord = new TerrainChunkCoordinate(center.X + dx, center.Z + dz);
                    if (_active.ContainsKey(coord))
                    {
                        continue;
                    }

                    TerrainChunk chunk = _pool.Acquire();
                    chunk.Apply(_builder.Build(coord, heightmapResolution, chunkWorldSize));
                    _active[coord] = chunk;

                    if (--budget <= 0)
                    {
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// The set of chunk coordinates that must be active for a player in
        /// <paramref name="center"/> at the given radius: a (2r+1)x(2r+1) square,
        /// with no duplicates. Static and Unity-free so it is unit-testable.
        /// </summary>
        public static IReadOnlyList<TerrainChunkCoordinate> RequiredChunks(TerrainChunkCoordinate center, int radius)
        {
            var list = new List<TerrainChunkCoordinate>();
            for (int dz = -radius; dz <= radius; dz++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    list.Add(new TerrainChunkCoordinate(center.X + dx, center.Z + dz));
                }
            }
            return list;
        }

        private static int ChebyshevDistance(TerrainChunkCoordinate a, TerrainChunkCoordinate b)
        {
            int dx = Mathf.Abs(a.X - b.X);
            int dz = Mathf.Abs(a.Z - b.Z);
            return Mathf.Max(dx, dz);
        }

        private void OnDestroy()
        {
            foreach (KeyValuePair<TerrainChunkCoordinate, TerrainChunk> kv in _active)
            {
                kv.Value.Dispose();
            }
            _active.Clear();
            _pool?.DisposeAll();
        }
    }
}

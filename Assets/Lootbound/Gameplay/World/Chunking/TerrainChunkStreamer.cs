using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Serialization;

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
        [Tooltip("Splat/alphamap resolution per chunk (surface texturing). 0 leaves chunks on their first layer.")]
        [SerializeField] private int alphamapResolution = 129;
        [SerializeField] private int activeRadiusInChunks = 2;
        [Tooltip("At most this many chunks are built and activated per frame, so a big jump spreads its work over several frames rather than one spike.")]
        [FormerlySerializedAs("maxBuildsPerTick")]
        [SerializeField] private int maxChunkActivationsPerFrame = 1;
        [Tooltip("Cap on queued build requests; ignored overflow is simply re-requested on a later tick.")]
        [SerializeField] private int maxQueuedRequests = 64;
        [Tooltip("Time budget (ms) the scheduler may spend building chunk data each frame. A build larger than the budget simply resumes next frame.")]
        [SerializeField] private float maxChunkBuildMillisecondsPerFrame = 4f;
        [Tooltip("Create one view's worth of chunk instances up front (at initialize), so first streaming never pays instance creation mid-play.")]
        [SerializeField] private bool prewarmPool = true;

        [Header("Chunk Rendering")]
        [Tooltip("Screen-space error (px) Unity may decimate terrain geometry to. Higher = fewer vertices, mostly far away.")]
        [SerializeField] private float pixelError = 8f;
        [Tooltip("Beyond this distance (m), chunks render their cheap blended basemap instead of the full splat.")]
        [SerializeField] private float basemapDistance = 300f;
        [Tooltip("Instanced terrain rendering - the main CPU render-cost reduction when many chunks are visible.")]
        [SerializeField] private bool drawInstancedTerrain = true;

        private IWorldHeightSampler _sampler;
        private Transform _player;
        private Transform _chunkRoot;
        private TerrainChunkBuilder _builder;
        private TerrainChunkBuildScheduler _scheduler;
        private ChunkPool _pool;
        private readonly Dictionary<TerrainChunkCoordinate, TerrainChunk> _active =
            new Dictionary<TerrainChunkCoordinate, TerrainChunk>();
        private readonly List<TerrainChunkCoordinate> _toRelease = new List<TerrainChunkCoordinate>();
        private readonly List<TerrainChunkCoordinate> _queuedScratch = new List<TerrainChunkCoordinate>();
        private readonly List<TerrainChunkData> _finished = new List<TerrainChunkData>();
        private readonly List<TerrainChunkCoordinate> _changedThisTick = new List<TerrainChunkCoordinate>();
        private bool _initialized;

        public int ActiveChunkCount => _active.Count;
        public int PooledChunkCount => _pool?.FreeCount ?? 0;
        public int InstancesCreated => _pool?.TotalCreated ?? 0;
        public int QueuedBuildCount => _scheduler?.QueuedCount ?? 0;
        public bool HasRunningBuild => _scheduler != null && _scheduler.HasRunningBuild;
        public int TotalChunksBuilt => _scheduler?.TotalBuilt ?? 0;
        public int TotalBuildsCancelled => _scheduler?.TotalCancelled ?? 0;

        // Diagnostics: streaming cost of the last tick, its worst observed value,
        // and how many chunks were activated by it.
        public float LastTickMilliseconds { get; private set; }
        public float PeakTickMilliseconds { get; private set; }
        public int LastTickActivations { get; private set; }
        public double AverageBuildMilliseconds =>
            _scheduler != null && _scheduler.TotalBuilt > 0
                ? _scheduler.TotalProcessMilliseconds / _scheduler.TotalBuilt
                : 0.0;

        private readonly System.Diagnostics.Stopwatch _tickClock = new System.Diagnostics.Stopwatch();

        /// <summary>True when the chunk containing this world position is active (built, collider live).</summary>
        public bool HasActiveChunkAt(double worldX, double worldZ)
        {
            return _initialized &&
                   _active.ContainsKey(TerrainChunkCoordinate.FromWorld(worldX, worldZ, chunkWorldSize));
        }

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
            _scheduler = new TerrainChunkBuildScheduler(
                _builder, heightmapResolution, chunkWorldSize, alphamapResolution, maxQueuedRequests);
            _pool = new ChunkPool(() => new TerrainChunk(
                _chunkRoot, useLayers, pixelError, basemapDistance, drawInstancedTerrain));
            if (prewarmPool)
            {
                int side = 2 * activeRadiusInChunks + 1;
                _pool.Prewarm(side * side);
            }
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

            _tickClock.Restart();
            _changedThisTick.Clear();
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
                _changedThisTick.Add(coord);
            }

            // Drop pending requests (queued or building) that left the radius.
            _queuedScratch.Clear();
            _scheduler.CollectPending(_queuedScratch);
            for (int i = 0; i < _queuedScratch.Count; i++)
            {
                if (ChebyshevDistance(_queuedScratch[i], center) > activeRadiusInChunks)
                {
                    _scheduler.Cancel(_queuedScratch[i]);
                }
            }

            // Request every missing in-range chunk; the scheduler dedups and caps.
            for (int dz = -activeRadiusInChunks; dz <= activeRadiusInChunks; dz++)
            {
                for (int dx = -activeRadiusInChunks; dx <= activeRadiusInChunks; dx++)
                {
                    var coord = new TerrainChunkCoordinate(center.X + dx, center.Z + dz);
                    if (!_active.ContainsKey(coord) && !_scheduler.IsPending(coord))
                    {
                        _scheduler.Request(coord);
                    }
                }
            }

            // Let the scheduler work under its time budget and finish up to the
            // frame's activation budget (nearest first), then ACTIVATE the
            // results - pool and active set are the streamer's responsibilities.
            int budget = maxChunkActivationsPerFrame > 0 ? maxChunkActivationsPerFrame : 1;
            _finished.Clear();
            _scheduler.Process(center, budget, maxChunkBuildMillisecondsPerFrame, _finished);
            for (int i = 0; i < _finished.Count; i++)
            {
                TerrainChunkData data = _finished[i];
                TerrainChunk chunk = _pool.Acquire();
                chunk.Apply(data);
                _active[data.Coordinate] = chunk;
                _changedThisTick.Add(data.Coordinate);

                // Applied (copied into the TerrainData) - hand the build buffers
                // back so the next build reuses them instead of allocating.
                _scheduler.ReleaseBuffers(data);
            }

            // Stitch INCREMENTALLY: only the chunks that changed this tick and
            // their four neighbours are re-declared - never the whole active set
            // (at large radii that alone was the residual frame spike).
            if (_changedThisTick.Count > 0)
            {
                RefreshNeighborsAroundChanges();
            }

            _tickClock.Stop();
            LastTickMilliseconds = (float)_tickClock.Elapsed.TotalMilliseconds;
            if (LastTickMilliseconds > PeakTickMilliseconds)
            {
                PeakTickMilliseconds = LastTickMilliseconds;
            }
            LastTickActivations = _finished.Count;
        }

        private static readonly ProfilerMarker StitchNeighborsMarker = new ProfilerMarker("Chunk.StitchNeighbors");

        private void RefreshNeighborsAroundChanges()
        {
            using var _ = StitchNeighborsMarker.Auto();
            for (int i = 0; i < _changedThisTick.Count; i++)
            {
                TerrainChunkCoordinate c = _changedThisTick[i];
                // The changed cell plus its four neighbours; duplicates across
                // changes are harmless (SetNeighbors is idempotent).
                RefreshNeighborsAt(c);
                RefreshNeighborsAt(new TerrainChunkCoordinate(c.X - 1, c.Z));
                RefreshNeighborsAt(new TerrainChunkCoordinate(c.X + 1, c.Z));
                RefreshNeighborsAt(new TerrainChunkCoordinate(c.X, c.Z - 1));
                RefreshNeighborsAt(new TerrainChunkCoordinate(c.X, c.Z + 1));
            }
        }

        private void RefreshNeighborsAt(TerrainChunkCoordinate c)
        {
            if (!_active.TryGetValue(c, out TerrainChunk chunk))
            {
                return;
            }
            _active.TryGetValue(new TerrainChunkCoordinate(c.X - 1, c.Z), out TerrainChunk left);
            _active.TryGetValue(new TerrainChunkCoordinate(c.X, c.Z + 1), out TerrainChunk top);
            _active.TryGetValue(new TerrainChunkCoordinate(c.X + 1, c.Z), out TerrainChunk right);
            _active.TryGetValue(new TerrainChunkCoordinate(c.X, c.Z - 1), out TerrainChunk bottom);
            chunk.SetNeighbors(left, top, right, bottom);
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

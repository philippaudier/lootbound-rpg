using System.Collections.Generic;
using System.Diagnostics;

namespace Lootbound.Gameplay.World.Chunking
{
    /// <summary>
    /// Decides WHICH chunk gets built next and HOW MANY get built per frame -
    /// never HOW a chunk is built. To the scheduler a request is only
    /// Queued -> Running -> Finished; every internal build step belongs to the
    /// <see cref="TerrainChunkBuilder"/> (and, from M4.3, its build state
    /// machine). Dependencies point one way only: the scheduler drives the
    /// builder; the builder knows nothing of the scheduler, the pool, the
    /// streamer or Unity - so the same builder can serve an editor tool, a
    /// benchmark or a test unchanged.
    ///
    /// Priority is the Chebyshev distance to the player's chunk (the chunk under
    /// the player first, then its ring of neighbours, then outer rings), with a
    /// deterministic (Z, then X) tie-break. Plain C#, main thread, no Unity API.
    /// </summary>
    public sealed class TerrainChunkBuildScheduler
    {
        private readonly TerrainChunkBuilder _builder;
        private readonly int _heightmapResolution;
        private readonly float _chunkWorldSize;
        private readonly int _alphamapResolution;
        private readonly int _maxQueuedRequests;

        private readonly List<TerrainChunkCoordinate> _queue = new List<TerrainChunkCoordinate>();
        private readonly HashSet<TerrainChunkCoordinate> _queuedSet = new HashSet<TerrainChunkCoordinate>();
        private readonly Stopwatch _clock = new Stopwatch();

        // At most ONE build is in flight: the heavy work is serialized by design.
        private TerrainChunkBuildState _running;
        private TerrainChunkBuildBuffers _runningBuffers;

        // Buffer sets are lent to finished data until the consumer releases them
        // (after Apply). Free sets are reused - steady streaming allocates no
        // large arrays. Both lists move in lockstep and stay tiny.
        private readonly Stack<TerrainChunkBuildBuffers> _freeBuffers = new Stack<TerrainChunkBuildBuffers>();
        private readonly List<TerrainChunkData> _lentData = new List<TerrainChunkData>();
        private readonly List<TerrainChunkBuildBuffers> _lentBuffers = new List<TerrainChunkBuildBuffers>();

        public int QueuedCount => _queue.Count;
        public bool HasRunningBuild => _running != null;
        public int TotalBuilt { get; private set; }
        public int TotalCancelled { get; private set; }

        /// <summary>Cumulative time spent inside Process, for diagnostics (avg build cost).</summary>
        public double TotalProcessMilliseconds { get; private set; }

        public TerrainChunkBuildScheduler(
            TerrainChunkBuilder builder,
            int heightmapResolution,
            float chunkWorldSize,
            int alphamapResolution,
            int maxQueuedRequests = 64)
        {
            _builder = builder;
            _heightmapResolution = heightmapResolution;
            _chunkWorldSize = chunkWorldSize;
            _alphamapResolution = alphamapResolution;
            _maxQueuedRequests = maxQueuedRequests < 1 ? 1 : maxQueuedRequests;
        }

        /// <summary>
        /// Queue a build request. Duplicates are ignored, and so is overflow past
        /// the queue cap - an ignored request is simply re-requested by the
        /// caller on a later tick, so nothing is lost.
        /// </summary>
        public bool Request(TerrainChunkCoordinate coordinate)
        {
            if (_queuedSet.Contains(coordinate) || _queue.Count >= _maxQueuedRequests)
            {
                return false;
            }
            _queuedSet.Add(coordinate);
            _queue.Add(coordinate);
            return true;
        }

        /// <summary>True if the coordinate is queued OR currently building.</summary>
        public bool IsPending(TerrainChunkCoordinate coordinate)
        {
            return _queuedSet.Contains(coordinate) ||
                   (_running != null && _running.Coordinate == coordinate);
        }

        /// <summary>
        /// Cancel a pending request that became obsolete (e.g. it left the
        /// streaming radius) - whether it is still queued or already building.
        /// A cancelled running build is simply discarded, never emitted.
        /// </summary>
        public bool Cancel(TerrainChunkCoordinate coordinate)
        {
            if (_running != null && _running.Coordinate == coordinate)
            {
                if (_runningBuffers != null)
                {
                    _freeBuffers.Push(_runningBuffers);
                    _runningBuffers = null;
                }
                _running = null;
                TotalCancelled++;
                return true;
            }

            if (!_queuedSet.Remove(coordinate))
            {
                return false;
            }
            _queue.Remove(coordinate);
            TotalCancelled++;
            return true;
        }

        /// <summary>Copy every pending coordinate (queued + running) into a caller-owned scratch list.</summary>
        public void CollectPending(List<TerrainChunkCoordinate> into)
        {
            if (_running != null)
            {
                into.Add(_running.Coordinate);
            }
            for (int i = 0; i < _queue.Count; i++)
            {
                into.Add(_queue[i]);
            }
        }

        /// <summary>
        /// Advance building under a per-frame time budget: the running build is
        /// progressed (at least one row even at zero budget), new builds start
        /// nearest-first while time remains, and up to
        /// <paramref name="maxActivations"/> finished results are appended to
        /// <paramref name="results"/>. Returns how many finished this call.
        /// </summary>
        public int Process(
            TerrainChunkCoordinate playerChunk, int maxActivations, double budgetMilliseconds,
            List<TerrainChunkData> results)
        {
            _clock.Restart();
            long deadline = budgetMilliseconds <= 0
                ? 0
                : (long)(budgetMilliseconds * Stopwatch.Frequency / 1000.0);

            int finished = 0;
            while (finished < maxActivations)
            {
                if (_running == null)
                {
                    if (_queue.Count == 0)
                    {
                        break;
                    }
                    int best = SelectBestIndex(playerChunk);
                    TerrainChunkCoordinate coordinate = _queue[best];
                    _queue.RemoveAt(best);
                    _queuedSet.Remove(coordinate);
                    _runningBuffers = _freeBuffers.Count > 0 ? _freeBuffers.Pop() : new TerrainChunkBuildBuffers();
                    _running = _builder.CreateBuildState(
                        coordinate, _heightmapResolution, _chunkWorldSize, _alphamapResolution, _runningBuffers);
                }

                if (_running.Advance(_clock, deadline))
                {
                    results.Add(_running.Result);
                    _lentData.Add(_running.Result);
                    _lentBuffers.Add(_runningBuffers);
                    _running = null;
                    _runningBuffers = null;
                    TotalBuilt++;
                    finished++;
                }
                else
                {
                    break; // budget exhausted mid-build; resumes next frame
                }
            }

            TotalProcessMilliseconds += _clock.Elapsed.TotalMilliseconds;
            return finished;
        }

        /// <summary>
        /// Return a finished build's buffer set to the pool, once its data has
        /// been applied. After this call the data's arrays may be overwritten by
        /// a later build and must not be read again.
        /// </summary>
        public void ReleaseBuffers(TerrainChunkData data)
        {
            for (int i = 0; i < _lentData.Count; i++)
            {
                if (ReferenceEquals(_lentData[i], data))
                {
                    _freeBuffers.Push(_lentBuffers[i]);
                    _lentData.RemoveAt(i);
                    _lentBuffers.RemoveAt(i);
                    return;
                }
            }
        }

        // Linear scan (the queue is small and capped): nearest Chebyshev distance
        // wins; equal distances break deterministically on Z, then X.
        private int SelectBestIndex(TerrainChunkCoordinate playerChunk)
        {
            int bestIndex = 0;
            int bestDistance = Distance(_queue[0], playerChunk);
            for (int i = 1; i < _queue.Count; i++)
            {
                int distance = Distance(_queue[i], playerChunk);
                if (distance < bestDistance ||
                    (distance == bestDistance && IsBeforeInTieOrder(_queue[i], _queue[bestIndex])))
                {
                    bestIndex = i;
                    bestDistance = distance;
                }
            }
            return bestIndex;
        }

        private static bool IsBeforeInTieOrder(TerrainChunkCoordinate a, TerrainChunkCoordinate b)
        {
            if (a.Z != b.Z)
            {
                return a.Z < b.Z;
            }
            return a.X < b.X;
        }

        private static int Distance(TerrainChunkCoordinate a, TerrainChunkCoordinate b)
        {
            int dx = a.X - b.X;
            if (dx < 0) dx = -dx;
            int dz = a.Z - b.Z;
            if (dz < 0) dz = -dz;
            return dx > dz ? dx : dz;
        }
    }
}

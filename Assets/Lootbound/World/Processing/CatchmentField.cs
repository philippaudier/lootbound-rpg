using System;
using System.Collections.Generic;
using Lootbound.World.Coordinates;
using Lootbound.World.Layers.Fields;

namespace Lootbound.World.Processing
{
    /// <summary>
    /// Question answered: "How much water drains through this point?"
    ///
    /// Unit: cell count (number of upstream cells draining through here, itself
    /// included). Range: [1, domain cells]. Reads: FlowField ONLY (honours the
    /// DAG - no height). V1 algorithm (CatchmentAnalyzer): topological
    /// accumulation over the flow graph (Kahn: process cells with no upstream
    /// first, push their accumulation downstream). Deterministic; no cycles are
    /// possible since D8 flow strictly descends. Swappable behind the interface.
    /// Assumptions: single-downstream flow. Limits: accumulation is imperfect on
    /// plateaus and stops at closed depressions (sinks); resolution-dependent
    /// magnitude. Future consumers: RiverMask, WaterTable, paths, erosion.
    /// </summary>
    public sealed class CatchmentField : IWorldField<float>
    {
        private readonly WorldDomain _domain;
        private readonly float[,] _accumulation;

        public CatchmentField(WorldDomain domain, float[,] accumulation)
        {
            _domain = domain;
            _accumulation = accumulation ?? throw new ArgumentNullException(nameof(accumulation));
        }

        public WorldDomain Domain => _domain;
        public float AccumulationAt(int ix, int iz) => _accumulation[ix, iz];

        public float Evaluate(WorldCoordinate c)
        {
            var (ix, iz) = DomainGrid.NearestCell(_domain, c);
            return _accumulation[ix, iz];
        }
    }

    /// <summary>Produces the <see cref="CatchmentField"/> by topological flow accumulation.</summary>
    public static class CatchmentAnalyzer
    {
        public static CatchmentField Analyze(FlowField flow)
        {
            if (flow == null) throw new ArgumentNullException(nameof(flow));

            WorldDomain domain = flow.Domain;
            int n = domain.Resolution;

            var inDegree = new int[n, n];
            for (int ix = 0; ix < n; ix++)
            {
                for (int iz = 0; iz < n; iz++)
                {
                    var dir = flow.DirectionAt(ix, iz);
                    if (dir == FlowDirection.None) continue;
                    var (dx, dz) = DomainGrid.Offset(dir);
                    int nx = ix + dx, nz = iz + dz;
                    if (nx >= 0 && nx < n && nz >= 0 && nz < n) inDegree[nx, nz]++;
                }
            }

            var acc = new float[n, n];
            var queue = new Queue<(int ix, int iz)>();
            for (int ix = 0; ix < n; ix++)
                for (int iz = 0; iz < n; iz++)
                {
                    acc[ix, iz] = 1f;
                    if (inDegree[ix, iz] == 0) queue.Enqueue((ix, iz));
                }

            while (queue.Count > 0)
            {
                var (ix, iz) = queue.Dequeue();
                var dir = flow.DirectionAt(ix, iz);
                if (dir == FlowDirection.None) continue;
                var (dx, dz) = DomainGrid.Offset(dir);
                int nx = ix + dx, nz = iz + dz;
                if (nx < 0 || nx >= n || nz < 0 || nz >= n) continue;

                acc[nx, nz] += acc[ix, iz];
                if (--inDegree[nx, nz] == 0) queue.Enqueue((nx, nz));
            }

            return new CatchmentField(domain, acc);
        }
    }
}

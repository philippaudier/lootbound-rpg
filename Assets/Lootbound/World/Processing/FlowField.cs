using System;
using Lootbound.World.Coordinates;
using Lootbound.World.Layers.Fields;

namespace Lootbound.World.Processing
{
    /// <summary>
    /// Question answered: "Which way does water leave this point?"
    ///
    /// Unit: a <see cref="FlowDirection"/> (D8). Reads: HeightField over a
    /// WorldDomain. This is Domain Processing: materialized on the domain grid,
    /// sampled by nearest cell. V1 algorithm (FlowAnalyzer): D8 steepest descent
    /// to the single lowest neighbour. Swappable behind the interface (a future
    /// D-infinity / MFD / hydraulic sim keeps the same contract).
    /// Assumptions: D8, single downstream, no pit-filling. Limits: no flow across
    /// flats or out of closed depressions (they become sinks); diagonal bias.
    /// Future consumers: Catchment, paths, erosion (later).
    /// </summary>
    public sealed class FlowField : IWorldField<FlowDirection>
    {
        private readonly WorldDomain _domain;
        private readonly FlowDirection[,] _flow;

        public FlowField(WorldDomain domain, FlowDirection[,] flow)
        {
            _domain = domain;
            _flow = flow ?? throw new ArgumentNullException(nameof(flow));
        }

        public WorldDomain Domain => _domain;

        /// <summary>Direction at grid cell (for the CatchmentAnalyzer).</summary>
        public FlowDirection DirectionAt(int ix, int iz) => _flow[ix, iz];

        public FlowDirection Evaluate(WorldCoordinate c)
        {
            var (ix, iz) = DomainGrid.NearestCell(_domain, c);
            return _flow[ix, iz];
        }
    }

    /// <summary>Produces the <see cref="FlowField"/> by D8 steepest descent.</summary>
    public static class FlowAnalyzer
    {
        public static FlowField Analyze(IWorldField<float> height, WorldDomain domain, float heightScale)
        {
            if (height == null) throw new ArgumentNullException(nameof(height));

            int n = domain.Resolution;
            var h = new float[n, n];
            for (int ix = 0; ix < n; ix++)
                for (int iz = 0; iz < n; iz++)
                    h[ix, iz] = height.Evaluate(domain.CellToWorld(ix, iz)) * heightScale;

            var flow = new FlowDirection[n, n];
            for (int ix = 0; ix < n; ix++)
            {
                for (int iz = 0; iz < n; iz++)
                {
                    float centre = h[ix, iz];
                    float bestSlope = 0f;
                    FlowDirection best = FlowDirection.None;

                    foreach (var dir in DomainGrid.AllDirections)
                    {
                        var (dx, dz) = DomainGrid.Offset(dir);
                        int nx = ix + dx, nz = iz + dz;
                        if (nx < 0 || nx >= n || nz < 0 || nz >= n) continue;

                        float drop = centre - h[nx, nz];
                        if (drop <= 0f) continue;

                        float dist = (dx != 0 && dz != 0) ? 1.41421356f : 1f;
                        float slope = drop / dist;
                        if (slope > bestSlope)
                        {
                            bestSlope = slope;
                            best = dir;
                        }
                    }

                    flow[ix, iz] = best;
                }
            }

            return new FlowField(domain, flow);
        }
    }
}

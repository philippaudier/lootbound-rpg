using System;
using Lootbound.World.Coordinates;
using Lootbound.World.Layers.Fields;

namespace Lootbound.World.Processing
{
    /// <summary>
    /// Question answered: "Should a river pass here?"
    ///
    /// Unit: boolean. Reads: CatchmentField (upstream). Rivers are DISCOVERED,
    /// not drawn: where enough water accumulates, a river should exist. V1
    /// algorithm (RiverMaskAnalyzer): drainage above a threshold. Swappable
    /// behind the interface. Assumptions: a single global accumulation threshold.
    /// Limits: threshold is resolution-dependent; no river width, order or
    /// continuity guarantees; inherits the FlowField's sink/plateau limits.
    /// Future consumers: Procedural Paths, bridges, villages, fishing, ambient
    /// audio, Landscape.
    /// </summary>
    public sealed class RiverMaskField : IWorldField<bool>
    {
        private readonly WorldDomain _domain;
        private readonly bool[,] _mask;

        public RiverMaskField(WorldDomain domain, bool[,] mask)
        {
            _domain = domain;
            _mask = mask ?? throw new ArgumentNullException(nameof(mask));
        }

        public WorldDomain Domain => _domain;

        public bool Evaluate(WorldCoordinate c)
        {
            var (ix, iz) = DomainGrid.NearestCell(_domain, c);
            return _mask[ix, iz];
        }
    }

    /// <summary>Produces the <see cref="RiverMaskField"/> by thresholding drainage.</summary>
    public static class RiverMaskAnalyzer
    {
        public static RiverMaskField Analyze(CatchmentField catchment, float accumulationThreshold)
        {
            if (catchment == null) throw new ArgumentNullException(nameof(catchment));

            WorldDomain domain = catchment.Domain;
            int n = domain.Resolution;
            var mask = new bool[n, n];
            for (int ix = 0; ix < n; ix++)
                for (int iz = 0; iz < n; iz++)
                    mask[ix, iz] = catchment.AccumulationAt(ix, iz) > accumulationThreshold;

            return new RiverMaskField(domain, mask);
        }
    }
}

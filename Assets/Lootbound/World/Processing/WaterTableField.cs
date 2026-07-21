using System;
using Lootbound.World.Coordinates;
using Lootbound.World.Layers.Fields;

namespace Lootbound.World.Processing
{
    /// <summary>
    /// Question answered: "How wet / waterlogged is this point?"
    ///
    /// Unit: normalized wetness. Range: [0, 1]. Reads: CatchmentField + Elevation
    /// (upstream). V1 algorithm (WaterTableAnalyzer): a crude proxy - more
    /// upstream drainage and lower ground read as wetter. Swappable behind the
    /// interface. Assumptions: wetness tracks drainage; no hydraulic head.
    /// Limits: NOT a real water table - no depression filling, no infiltration,
    /// no evaporation; a placeholder until a proper hydrology model lands. Future
    /// consumers: vegetation, biomes, ambient audio, fishing, Landscape.
    /// </summary>
    public sealed class WaterTableField : IWorldField<float>
    {
        private readonly WorldDomain _domain;
        private readonly float[,] _wetness;

        public WaterTableField(WorldDomain domain, float[,] wetness)
        {
            _domain = domain;
            _wetness = wetness ?? throw new ArgumentNullException(nameof(wetness));
        }

        public WorldDomain Domain => _domain;

        public float Evaluate(WorldCoordinate c)
        {
            var (ix, iz) = DomainGrid.NearestCell(_domain, c);
            return _wetness[ix, iz];
        }
    }

    /// <summary>Produces the <see cref="WaterTableField"/> from drainage and elevation.</summary>
    public static class WaterTableAnalyzer
    {
        public static WaterTableField Analyze(
            CatchmentField catchment, IWorldField<float> elevation, float wetnessScale)
        {
            if (catchment == null) throw new ArgumentNullException(nameof(catchment));
            if (elevation == null) throw new ArgumentNullException(nameof(elevation));

            WorldDomain domain = catchment.Domain;
            int n = domain.Resolution;
            float scale = wetnessScale <= 0f ? 1f : wetnessScale;

            var wetness = new float[n, n];
            for (int ix = 0; ix < n; ix++)
            {
                for (int iz = 0; iz < n; iz++)
                {
                    float drainage = catchment.AccumulationAt(ix, iz) / scale;
                    float elev = elevation.Evaluate(domain.CellToWorld(ix, iz));
                    float wet = drainage * (1.2f - elev); // wetter with more drainage / lower ground
                    wetness[ix, iz] = wet < 0f ? 0f : (wet > 1f ? 1f : wet);
                }
            }

            return new WaterTableField(domain, wetness);
        }
    }
}

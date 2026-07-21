using Lootbound.World.Coordinates;

namespace Lootbound.World.Processing
{
    /// <summary>
    /// Placeholder marker for Domain Processing - the domain-agnostic transforms
    /// that turn analytic fields into DERIVED fields and recognitions (Hydrology,
    /// Erosion, Landscape Recognition, Vegetation). A "domain" is a bounded region
    /// to process; it never assumes the world's size. Lands in T3. Empty on
    /// purpose so the layer exists now.
    /// </summary>
    public interface IDomainProcess
    {
    }

    /// <summary>
    /// A bounded region to process. Today a domain may be the whole world;
    /// tomorrow a region, a tile grid, a hierarchy or a distributed shard - the
    /// architecture is identical. Defined now, used in T3.
    /// </summary>
    public readonly struct WorldDomain
    {
        public readonly WorldCoordinate Min;
        public readonly WorldCoordinate Max;

        public WorldDomain(WorldCoordinate min, WorldCoordinate max)
        {
            Min = min;
            Max = max;
        }
    }
}

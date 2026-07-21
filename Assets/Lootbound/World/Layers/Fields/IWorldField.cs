using Lootbound.World.Coordinates;

namespace Lootbound.World.Layers.Fields
{
    /// <summary>
    /// The one contract of the World Engine: a property of the world, evaluated
    /// at any coordinate. This is the general concept - the engine turns around
    /// World Fields, not around relief. HeightField is only the FIRST concrete
    /// implementation of a World Field; Danger and Climate are World Fields too,
    /// and later a LandscapeField (IWorldField&lt;LandscapeType&gt;) or a
    /// BiomeField (IWorldField&lt;WorldRegion&gt;) fit the same contract
    /// regardless of the value type.
    /// </summary>
    public interface IWorldField<out T>
    {
        T Evaluate(WorldCoordinate coordinate);
    }
}

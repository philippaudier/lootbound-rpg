using Lootbound.World.Coordinates;

namespace Lootbound.World.Layers.Fields
{
    /// <summary>
    /// Placeholder ClimateField for T2: returns a constant until the real climate
    /// model (temperature/moisture, seasons) lands. It exists now only so the
    /// contract and the concept have a home; nothing consumes it yet.
    /// </summary>
    public sealed class PlaceholderClimateField : IWorldField<float>
    {
        public float Evaluate(WorldCoordinate coordinate) => 0f;
    }
}

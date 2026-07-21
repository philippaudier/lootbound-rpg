namespace Lootbound.World.Layers.Fields
{
    /// <summary>
    /// Coarse elevation region of a world position. A first, deliberately simple
    /// classification (the real biome/RegionField will fold in climate later).
    /// It is an enum precisely to show that a World Field's value type is not
    /// limited to float: RegionField is IWorldField&lt;WorldRegion&gt;.
    /// </summary>
    public enum WorldRegion
    {
        Lowland,
        Midland,
        Highland
    }
}

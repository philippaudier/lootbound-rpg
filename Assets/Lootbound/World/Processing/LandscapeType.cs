namespace Lootbound.World.Processing
{
    /// <summary>
    /// A coarse geomorphological classification of a place - what the terrain IS,
    /// deduced from its shape and water, never from gameplay or world position.
    /// </summary>
    public enum LandscapeType
    {
        Plain,
        Valley,
        Ridge,
        Mountain,
        Plateau,
        Pass,
        Basin,
        Cliff
    }
}

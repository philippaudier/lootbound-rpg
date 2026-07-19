namespace Lootbound.Gameplay.World.Ambience.Events
{
    /// <summary>
    /// Family of an ambient event. Categories share one generic system -
    /// they only differ by which activity intent drives them and by their
    /// authored profiles.
    /// </summary>
    public enum AmbientEventCategory
    {
        Birds,
        Insects,
        Wind,
        Environmental,
        Rare
    }
}

namespace Lootbound.Gameplay.World.Navigation
{
    /// <summary>
    /// Lifecycle state of the runtime navigation surface.
    /// </summary>
    public enum RuntimeNavigationState
    {
        NotBuilt,
        Building,
        Ready,
        Failed
    }
}

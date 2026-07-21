namespace Lootbound.World.Providers
{
    /// <summary>
    /// Remaps a normalized height [0,1] to a shaped [0,1]. Provided by an
    /// authoring curve today (a Unity AnimationCurve behind the seam), a pure
    /// curve tomorrow. The World Engine never sees the authoring type.
    /// </summary>
    public interface IHeightRemap
    {
        float Evaluate(float normalizedHeight);
    }
}

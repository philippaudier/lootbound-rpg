namespace Lootbound.Gameplay.World
{
    /// <summary>
    /// The world's surface classification (the "masks" the generator owns per
    /// §6.1) as a function of world coordinates: how much of each terrain layer
    /// covers a point. Like <see cref="IWorldHeightSampler"/>, it lives beside the
    /// generator and the chunk layer only depends on it - never the reverse.
    ///
    /// A sampler may implement height only; the chunk builder detects this
    /// capability and paints an alphamap when it is available, leaving chunks
    /// untextured otherwise.
    /// </summary>
    public interface IWorldSplatSampler
    {
        /// <summary>Number of terrain layers the splat weights cover.</summary>
        int SplatLayerCount { get; }

        /// <summary>
        /// Fill <paramref name="weights"/> (normalized, summing to 1) with the
        /// layer coverage at (worldX, worldZ). Continuous across the world, so
        /// texturing has no chunk-boundary seam.
        /// </summary>
        void SampleSplat(double worldX, double worldZ, float[] weights);
    }
}

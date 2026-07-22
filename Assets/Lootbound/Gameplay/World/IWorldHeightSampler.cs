namespace Lootbound.Gameplay.World
{
    /// <summary>
    /// The world's height as a function of world coordinates, in metres. This is
    /// the generator's own sampling contract - it lives here, beside the
    /// generator, NOT in the chunk layer: the generator answers Height(x,z) and
    /// never learns that chunks, Unity Terrains, a streamer or a pool exist. The
    /// chunk system depends on this interface; the dependency only ever points
    /// from the chunks toward the generator, never the reverse.
    /// </summary>
    public interface IWorldHeightSampler
    {
        /// <summary>
        /// World-space height in metres at (worldX, worldZ), for ANY coordinate.
        /// Inside the region currently materialized this is the final relief
        /// (base field plus the authored refuge / paths / landmark deformations);
        /// beyond it, the base analytic field.
        /// </summary>
        float SampleHeight(double worldX, double worldZ);

        /// <summary>Vertical scale in metres, used to normalize heights for a Unity Terrain.</summary>
        float TerrainHeight { get; }

        /// <summary>True when the sampler can be sampled meaningfully (the world has been built).</summary>
        bool IsReady { get; }
    }
}

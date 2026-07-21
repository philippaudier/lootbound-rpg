namespace Lootbound.World.Providers
{
    /// <summary>
    /// Raw 2D coherent-noise primitive. The World Engine depends on THIS
    /// abstraction, never on a concrete noise library: Unity's Perlin provides it
    /// today; FastNoiseLite, a GPU kernel or a pure-C# implementation can provide
    /// it tomorrow, without touching a single field. The provider is injected.
    /// </summary>
    public interface INoiseSource
    {
        /// <summary>Coherent noise at (x, y). Matches UnityEngine.Mathf.PerlinNoise' range.</summary>
        float Sample(float x, float y);
    }
}

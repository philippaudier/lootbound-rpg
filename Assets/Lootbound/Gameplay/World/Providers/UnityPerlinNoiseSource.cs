using UnityEngine;
using Lootbound.World.Providers;

namespace Lootbound.Gameplay.World.Providers
{
    /// <summary>
    /// Provides the World Engine's noise from Unity's Perlin implementation. This
    /// is the ONE place Mathf.PerlinNoise is called; the World layer only ever
    /// sees <see cref="INoiseSource"/>. Swapping to FastNoiseLite / GPU / pure-C#
    /// later is a change to this class alone, never to a field.
    /// </summary>
    public sealed class UnityPerlinNoiseSource : INoiseSource
    {
        public float Sample(float x, float y) => Mathf.PerlinNoise(x, y);
    }
}

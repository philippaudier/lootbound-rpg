using UnityEngine;
using Lootbound.World.Providers;

namespace Lootbound.Gameplay.World.Providers
{
    /// <summary>
    /// Provides the World Engine's height remap from a Unity AnimationCurve. The
    /// authoring type (AnimationCurve) never crosses into the World layer, which
    /// only sees <see cref="IHeightRemap"/>. Using the same curve's Evaluate
    /// preserves the exact legacy height values.
    /// </summary>
    public sealed class AnimationCurveHeightRemap : IHeightRemap
    {
        private readonly AnimationCurve _curve;

        public AnimationCurveHeightRemap(AnimationCurve curve)
        {
            _curve = curve;
        }

        public float Evaluate(float normalizedHeight) => _curve.Evaluate(normalizedHeight);
    }
}

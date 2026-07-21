using UnityEngine;
using Lootbound.World.Coordinates;
using Lootbound.World.Layers.Fields;
using Lootbound.Gameplay.World.Progression;

namespace Lootbound.Gameplay.World.Providers
{
    /// <summary>
    /// Provides the DangerField (an <see cref="IWorldField{float}"/>) from the
    /// existing <see cref="WorldProgression"/>. The World Engine only ever sees a
    /// danger field; WHERE the danger comes from - procedural today, a scripted
    /// campaign tomorrow - is a Provider concern, and the engine never changes.
    /// Danger is the radial difficulty (0 = calm near the Refuge, 1 = deepest).
    /// </summary>
    public sealed class WorldProgressionDangerProvider : IWorldField<float>
    {
        private readonly WorldProgression _progression;

        public WorldProgressionDangerProvider(WorldProgression progression)
        {
            _progression = progression;
        }

        public float Evaluate(WorldCoordinate coordinate)
        {
            var context = _progression.GetContext(new Vector3((float)coordinate.X, 0f, (float)coordinate.Z));
            return context.Difficulty01;
        }
    }
}

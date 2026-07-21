using NUnit.Framework;
using Lootbound.World.Coordinates;
using Lootbound.World.Layers.Fields;

namespace Lootbound.World.Tests
{
    public class RegionFieldTests
    {
        private sealed class ConstantHeight : IWorldField<float>
        {
            private readonly float _h;
            public ConstantHeight(float h) => _h = h;
            public float Evaluate(WorldCoordinate coordinate) => _h;
        }

        private static WorldRegion Classify(float height)
            => new RegionField(new ConstantHeight(height), 0.3f, 0.65f).Evaluate(new WorldCoordinate(0, 0));

        [Test]
        public void ClassifiesByThresholds()
        {
            Assert.AreEqual(WorldRegion.Lowland, Classify(0.1f));
            Assert.AreEqual(WorldRegion.Midland, Classify(0.5f));
            Assert.AreEqual(WorldRegion.Highland, Classify(0.9f));
        }

        [Test]
        public void BoundariesAreInclusiveOfHighland()
        {
            Assert.AreEqual(WorldRegion.Midland, Classify(0.3f), "lowland is strict below threshold");
            Assert.AreEqual(WorldRegion.Highland, Classify(0.65f), "highland is inclusive at threshold");
        }
    }
}

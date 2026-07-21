using NUnit.Framework;
using Lootbound.World.Coordinates;
using Lootbound.World.Layers.Fields;
using Lootbound.World.Sampling;

namespace Lootbound.World.Tests
{
    public class WorldSamplerTests
    {
        private sealed class ConstFloat : IWorldField<float>
        {
            private readonly float _v;
            public ConstFloat(float v) => _v = v;
            public float Evaluate(WorldCoordinate coordinate) => _v;
        }

        private sealed class ConstRegion : IWorldField<WorldRegion>
        {
            private readonly WorldRegion _v;
            public ConstRegion(WorldRegion v) => _v = v;
            public WorldRegion Evaluate(WorldCoordinate coordinate) => _v;
        }

        [Test]
        public void Sampler_DelegatesToItsFields()
        {
            var sampler = new WorldSampler(
                new ConstFloat(7f),
                new ConstFloat(0.9f),
                new ConstRegion(WorldRegion.Highland));

            var c = new WorldCoordinate(1, 2);
            Assert.AreEqual(7f, sampler.SampleHeight(c));
            Assert.AreEqual(0.9f, sampler.SampleDanger(c));
            Assert.AreEqual(WorldRegion.Highland, sampler.SampleRegion(c));
        }
    }
}

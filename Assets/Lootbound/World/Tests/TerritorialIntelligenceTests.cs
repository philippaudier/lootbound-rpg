using System;
using NUnit.Framework;
using Lootbound.World.Coordinates;
using Lootbound.World.Layers.Fields;
using Lootbound.World.Processing;

namespace Lootbound.World.Tests
{
    /// <summary>
    /// Territorial Intelligence (PCE 0.4): measures, never names. Proves the
    /// PROPERTIES - a plain is accessible/connected/not isolated, a walled
    /// pocket is the opposite, a corridor sits in between with few open
    /// directions - plus determinism, 0..1 ranges, grid-free continuity and
    /// independence from chunks/gameplay (the API only ever sees a cost view).
    /// </summary>
    public class TerritorialIntelligenceTests
    {
        private sealed class UniformCost : IWorldField<float>
        {
            private readonly float _value;
            public UniformCost(float value) { _value = value; }
            public float Evaluate(WorldCoordinate c) => _value;
        }

        /// <summary>Cheap band along X around Z = 0; expensive everywhere else.</summary>
        private sealed class CorridorCost : IWorldField<float>
        {
            public float Evaluate(WorldCoordinate c) => Math.Abs(c.Z) < 10.0 ? 1f : 20f;
        }

        /// <summary>Smooth, coordinate-dependent cost for continuity checks.</summary>
        private sealed class SmoothCost : IWorldField<float>
        {
            public float Evaluate(WorldCoordinate c) =>
                (float)(2.0 + Math.Sin(c.X * 0.01) * Math.Cos(c.Z * 0.013));
        }

        private static readonly WorldCoordinate Origin = new WorldCoordinate(0, 0);

        private static TerritorialIdentityField Field(IWorldField<float> cost)
            => new TerritorialIdentityField(cost, new TerritorialSettings());

        [Test]
        public void Plain_IsAccessible_Connected_AndNotIsolated()
        {
            TerritorialIdentity id = Field(new UniformCost(1f)).Evaluate(Origin);

            Assert.AreEqual(1f, id.Accessibility, 1e-4f, "ideal ground everywhere = full accessibility");
            Assert.AreEqual(0f, id.Isolation, 1e-4f, "every way out is ideal = no isolation");
            Assert.AreEqual(1f, id.Connectivity, 1e-4f, "open in every direction");
        }

        [Test]
        public void WalledPocket_IsIsolated_AndDisconnected()
        {
            TerritorialIdentity id = Field(new UniformCost(30f)).Evaluate(Origin);

            Assert.Less(id.Accessibility, 0.1f, "expensive ground all around");
            Assert.Greater(id.Isolation, 0.9f, "even the easiest way out is expensive");
            Assert.AreEqual(0f, id.Connectivity, 1e-4f, "no open direction");
        }

        [Test]
        public void Corridor_HasFewOpenDirections_ButAnEasyWayOut()
        {
            TerritorialIdentity id = Field(new CorridorCost()).Evaluate(Origin);

            Assert.AreEqual(0f, id.Isolation, 1e-4f, "the corridor itself is an ideal way out");
            Assert.Greater(id.Connectivity, 0f, "the two corridor directions are open");
            Assert.Less(id.Connectivity, 0.5f, "but most directions are walls");
        }

        [Test]
        public void Rankings_TellTheThreePlacesApart()
        {
            TerritorialIdentity plain = Field(new UniformCost(1f)).Evaluate(Origin);
            TerritorialIdentity corridor = Field(new CorridorCost()).Evaluate(Origin);
            TerritorialIdentity pocket = Field(new UniformCost(30f)).Evaluate(Origin);

            Assert.Greater(plain.Connectivity, corridor.Connectivity);
            Assert.Greater(corridor.Connectivity, pocket.Connectivity);
            Assert.Greater(pocket.Isolation, corridor.Isolation);
            Assert.Greater(plain.Accessibility, corridor.Accessibility);
            Assert.Greater(corridor.Accessibility, pocket.Accessibility);
        }

        [Test]
        public void Identity_IsDeterministic_InRange_AndFiniteAtExtremeCoordinates()
        {
            var field = Field(new SmoothCost());
            var far = new WorldCoordinate(7_500_000.25, -9_999_999.5);

            TerritorialIdentity a = field.Evaluate(far);
            TerritorialIdentity b = field.Evaluate(far);

            Assert.AreEqual(a.Accessibility, b.Accessibility, 0f);
            Assert.AreEqual(a.Isolation, b.Isolation, 0f);
            Assert.AreEqual(a.Connectivity, b.Connectivity, 0f);

            foreach (float v in new[] { a.Accessibility, a.Isolation, a.Connectivity })
            {
                Assert.IsFalse(float.IsNaN(v) || float.IsInfinity(v));
                Assert.GreaterOrEqual(v, 0f);
                Assert.LessOrEqual(v, 1f);
            }
        }

        [Test]
        public void Boundaries_AreNeverExact_InfluenceDecaysContinuously()
        {
            // Invariant 20: no hard territorial frontier. Over a smooth cost
            // field, a half-metre move changes the identity only slightly.
            var field = Field(new SmoothCost());
            TerritorialIdentity here = field.Evaluate(new WorldCoordinate(1234.5, -678.9));
            TerritorialIdentity near = field.Evaluate(new WorldCoordinate(1235.0, -678.9));

            Assert.Less(Math.Abs(here.Accessibility - near.Accessibility), 0.05f);
            Assert.Less(Math.Abs(here.Isolation - near.Isolation), 0.05f);
            Assert.Less(Math.Abs(here.Connectivity - near.Connectivity), 0.15f, "connectivity is quantized by rays but must not jump");
        }

        [Test]
        public void Guards_RefuseMissingCostOrDegenerateSettings()
        {
            Assert.Throws<ArgumentNullException>(() => new TerritorialIdentityField(null, new TerritorialSettings()));
            Assert.Throws<ArgumentNullException>(() => new TerritorialIdentityField(new UniformCost(1f), null));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new TerritorialIdentityField(new UniformCost(1f), new TerritorialSettings { DirectionCount = 2 }));
        }
    }
}

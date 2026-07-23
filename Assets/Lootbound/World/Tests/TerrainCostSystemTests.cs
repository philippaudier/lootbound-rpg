using System;
using NUnit.Framework;
using Lootbound.World.Coordinates;
using Lootbound.World.Layers.Fields;
using Lootbound.World.Processing;

namespace Lootbound.World.Tests
{
    /// <summary>
    /// The Terrain Cost System (PCE 0.3): one mechanism, many perceptions.
    /// Proves the three contract points: the default profile reproduces the
    /// historical TraversabilityField EXACTLY; different profiles rank the
    /// same terrain differently; and no cost exists without a profile
    /// (PCE invariant 16). Profiles here are built inline: the named presets
    /// are gameplay data, the MECHANISM is what this suite guards.
    /// </summary>
    public class TerrainCostSystemTests
    {
        private sealed class Const<T> : IWorldField<T>
        {
            private readonly T _value;
            public Const(T value) { _value = value; }
            public T Evaluate(WorldCoordinate c) => _value;
        }

        private sealed class SlopeFromCoordinate : IWorldField<float>
        {
            public float Evaluate(WorldCoordinate c) =>
                (float)(20.0 + 15.0 * Math.Sin(c.X * 0.01) * Math.Cos(c.Z * 0.013));
        }

        private static readonly WorldCoordinate P = new WorldCoordinate(37.5, -12.25);

        [Test]
        public void DefaultProfile_ReproducesTraversabilityField_Exactly()
        {
            var settings = new WorldKnowledgeSettings();
            var slope = new Const<float>(23.7f);
            var rough = new Const<float>(2.31f);

            foreach (bool cliff in new[] { false, true })
            {
                foreach (bool river in new[] { false, true })
                {
                    var legacy = new TraversabilityField(
                        slope, new Const<bool>(cliff), rough, new Const<bool>(river), settings);
                    var cost = new TerrainCostField(
                        slope, new Const<bool>(cliff), rough, new Const<bool>(river),
                        landscape: null, TraversalProfile.FromSettings(settings));

                    Assert.AreEqual(legacy.Evaluate(P), cost.Evaluate(P), 0f,
                        $"cliff={cliff} river={river}: the default profile must be bit-identical to the legacy field");
                }
            }
        }

        [Test]
        public void Profiles_RankTheSameTerrainDifferently()
        {
            // Steep, rough ground; no cliff, no river.
            var slope = new Const<float>(35f);
            var rough = new Const<float>(3f);
            var noCliff = new Const<bool>(false);
            var noRiver = new Const<bool>(false);

            var roadBuilder = new TraversalProfile { SlopeCostPerDegree = 0.09f, RoughnessCostPerMetre = 0.8f };
            var mountaineer = new TraversalProfile { SlopeCostPerDegree = 0.03f, RoughnessCostPerMetre = 0.3f };

            float roadCost = new TerrainCostField(slope, noCliff, rough, noRiver, null, roadBuilder).Evaluate(P);
            float mountainCost = new TerrainCostField(slope, noCliff, rough, noRiver, null, mountaineer).Evaluate(P);

            Assert.Greater(roadCost, mountainCost,
                "the same steep ground must cost MORE to a road-building perception than to a mountain one");
        }

        [Test]
        public void LandscapePerception_IsOptional_AndMultiplies()
        {
            var slope = new Const<float>(10f);
            var rough = new Const<float>(1f);
            var noCliff = new Const<bool>(false);
            var noRiver = new Const<bool>(false);

            float[] valleyLover = new float[8];
            for (int i = 0; i < valleyLover.Length; i++) valleyLover[i] = 1f;
            valleyLover[(int)LandscapeType.Valley] = 0.5f;

            var animal = new TraversalProfile { LandscapeCostMultipliers = valleyLover };
            var blind = new TraversalProfile(); // no landscape perception

            float animalInValley = new TerrainCostField(
                slope, noCliff, rough, noRiver, new Const<LandscapeType>(LandscapeType.Valley), animal).Evaluate(P);
            float animalInPlain = new TerrainCostField(
                slope, noCliff, rough, noRiver, new Const<LandscapeType>(LandscapeType.Plain), animal).Evaluate(P);
            float blindAnywhere = new TerrainCostField(
                slope, noCliff, rough, noRiver, landscape: null, blind).Evaluate(P);

            Assert.AreEqual(animalInPlain * 0.5f, animalInValley, 1e-5f,
                "a valley-loving perception halves its cost in valleys");
            Assert.AreEqual(blindAnywhere, animalInPlain, 0f,
                "outside its preferences the perceiving profile matches the blind one");
        }

        [Test]
        public void NoCostWithoutAProfile_AndNoPerceptionWithoutItsField()
        {
            var f = new Const<float>(1f);
            var b = new Const<bool>(false);

            Assert.Throws<ArgumentNullException>(
                () => new TerrainCostField(f, b, f, b, null, profile: null),
                "invariant 16: no cost API ever answers without a profile");

            var perceiving = new TraversalProfile { LandscapeCostMultipliers = new float[8] };
            Assert.Throws<ArgumentNullException>(
                () => new TerrainCostField(f, b, f, b, landscape: null, perceiving),
                "a landscape-perceiving profile requires the landscape field");
        }

        [Test]
        public void Cost_IsDeterministic_AndFiniteAtExtremeCoordinates()
        {
            var field = new TerrainCostField(
                new SlopeFromCoordinate(), new Const<bool>(false),
                new Const<float>(0.5f), new Const<bool>(false),
                null, new TraversalProfile());

            var far = new WorldCoordinate(9_000_000.5, -12_345_678.9);
            float a = field.Evaluate(far);
            float b = field.Evaluate(far);

            Assert.AreEqual(a, b, 0f, "same coordinate, same cost - always");
            Assert.IsFalse(float.IsNaN(a) || float.IsInfinity(a), "finite at extreme coordinates");
            Assert.GreaterOrEqual(a, 1f, "never below the base cost");
        }
    }
}

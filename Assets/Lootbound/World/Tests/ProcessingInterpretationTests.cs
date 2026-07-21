using NUnit.Framework;
using Lootbound.World.Coordinates;
using Lootbound.World.Layers.Fields;
using Lootbound.World.Processing;

namespace Lootbound.World.Tests
{
    /// <summary>
    /// Pure tests for the interpretation fields: TraversabilityField (local cost)
    /// and LandscapeField (geomorphological classification). Both are driven by
    /// constant input fields so each branch is exercised in isolation.
    /// </summary>
    public class ProcessingInterpretationTests
    {
        private sealed class ConstFloat : IWorldField<float>
        {
            private readonly float _v;
            public ConstFloat(float v) => _v = v;
            public float Evaluate(WorldCoordinate c) => _v;
        }

        private sealed class ConstBool : IWorldField<bool>
        {
            private readonly bool _v;
            public ConstBool(bool v) => _v = v;
            public bool Evaluate(WorldCoordinate c) => _v;
        }

        private static readonly WorldCoordinate P = new WorldCoordinate(10, 10);

        // ---- Traversability ----

        private static float Cost(float slope, bool cliff, float roughness, bool river)
        {
            var f = new TraversabilityField(
                new ConstFloat(slope), new ConstBool(cliff), new ConstFloat(roughness), new ConstBool(river),
                new WorldKnowledgeSettings());
            return f.Evaluate(P);
        }

        [Test]
        public void Traversability_EasyGround_IsBaseCost()
        {
            Assert.AreEqual(1f, Cost(0f, false, 0f, false), 1e-4f);
        }

        [Test]
        public void Traversability_SteeperCostsMore()
        {
            Assert.Greater(Cost(30f, false, 0f, false), Cost(5f, false, 0f, false));
        }

        [Test]
        public void Traversability_CliffAndWaterAddTheirPenalties()
        {
            Assert.AreEqual(101f, Cost(0f, true, 0f, false), 1e-4f, "base + cliff cost");
            Assert.AreEqual(26f, Cost(0f, false, 0f, true), 1e-4f, "base + water cost");
            Assert.AreEqual(3f, Cost(0f, false, 4f, false), 1e-4f, "base + roughness cost");
        }

        // ---- Landscape ----

        private static LandscapeType Classify(float elev, float slope, float curv, bool cliff, bool river)
        {
            var f = new LandscapeField(
                new ConstFloat(elev), new ConstFloat(slope), new ConstFloat(curv),
                new ConstBool(cliff), new ConstBool(river), new WorldKnowledgeSettings());
            return f.Evaluate(P);
        }

        [Test]
        public void Landscape_ClassifiesEachShape()
        {
            Assert.AreEqual(LandscapeType.Cliff, Classify(0.5f, 80f, 0f, cliff: true, river: false));
            Assert.AreEqual(LandscapeType.Mountain, Classify(0.8f, 40f, 0f, false, false), "steep + high");
            Assert.AreEqual(LandscapeType.Ridge, Classify(0.45f, 40f, 0f, false, false), "steep + not high");
            Assert.AreEqual(LandscapeType.Plateau, Classify(0.8f, 2f, 0f, false, false), "high + flat");
            Assert.AreEqual(LandscapeType.Valley, Classify(0.5f, 2f, 0f, false, river: true), "water carves a valley");
            Assert.AreEqual(LandscapeType.Basin, Classify(0.2f, 2f, -10f, false, false), "concave + low + dry");
            Assert.AreEqual(LandscapeType.Pass, Classify(0.5f, 2f, -10f, false, false), "concave + mid + dry");
            Assert.AreEqual(LandscapeType.Plain, Classify(0.5f, 2f, 0f, false, false), "flat mid, no concavity, no water");
        }

        [Test]
        public void Landscape_IsGeomorphologicalAndDeterministic()
        {
            // Same shape at very different world positions -> same classification
            // (it never reads world position).
            var f = new LandscapeField(
                new ConstFloat(0.5f), new ConstFloat(2f), new ConstFloat(0f),
                new ConstBool(false), new ConstBool(false), new WorldKnowledgeSettings());
            Assert.AreEqual(f.Evaluate(new WorldCoordinate(0, 0)), f.Evaluate(new WorldCoordinate(9_000_000, -5_000_000)));
        }
    }
}

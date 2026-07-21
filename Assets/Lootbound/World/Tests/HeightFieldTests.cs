using System;
using NUnit.Framework;
using Lootbound.World.Coordinates;
using Lootbound.World.Layers.Fields;
using Lootbound.World.Providers;

namespace Lootbound.World.Tests
{
    /// <summary>
    /// Pure World-layer tests for the HeightField: it is a deterministic function
    /// of a coordinate with no grid, evaluable anywhere - including negative and
    /// very large coordinates. Uses a fake deterministic noise source; the
    /// EXACT-match-to-legacy guarantee is a separate golden test on the Unity
    /// side (which uses the real Perlin provider).
    /// </summary>
    public class HeightFieldTests
    {
        private sealed class DeterministicNoise : INoiseSource
        {
            public float Sample(float x, float y)
            {
                double s = Math.Sin(x * 12.9898 + y * 78.233) * 43758.5453;
                return (float)(s - Math.Floor(s)); // fractional part in [0,1)
            }
        }

        private sealed class IdentityRemap : IHeightRemap
        {
            public float Evaluate(float normalizedHeight) => normalizedHeight;
        }

        private static HeightField Build(int seed = 42)
        {
            var settings = new HeightFieldSettings
            {
                WorldSize = 1024f,
                MacroScale = 450f, MacroOctaves = 4, MacroPersistence = 0.5f, MacroLacunarity = 2f,
                RidgeScale = 350f, RidgeStrength = 0.25f,
                ValleyScale = 400f, ValleyStrength = 0.3f,
                DetailScale = 60f, DetailStrength = 0.08f,
                GlobalHeightStrength = 1f
            };
            return new HeightField(new DeterministicNoise(), new IdentityRemap(), settings, new NoiseOffsets(seed));
        }

        [Test]
        public void SameCoordinate_SameHeight()
        {
            var field = Build();
            var c = new WorldCoordinate(137.5, -812.25);
            Assert.AreEqual(field.Evaluate(c), field.Evaluate(c));
        }

        [Test]
        public void SameSeed_DifferentInstances_Agree()
        {
            var a = Build();
            var b = Build();
            var c = new WorldCoordinate(10.0, 20.0);
            Assert.AreEqual(a.Evaluate(c), b.Evaluate(c));
        }

        [Test]
        public void NegativeCoordinates_AreFiniteAndDeterministic()
        {
            var field = Build();
            var c = new WorldCoordinate(-5000.5, -9999.9);
            float h = field.Evaluate(c);
            Assert.IsFalse(float.IsNaN(h) || float.IsInfinity(h), "negative coords must be finite");
            Assert.AreEqual(h, field.Evaluate(c), "the field is unbounded, not grid-limited to [0,WorldSize]");
        }

        [Test]
        public void VeryLargeCoordinates_AreFiniteAndDeterministic()
        {
            var field = Build();
            var c = new WorldCoordinate(5_000_000.0, 12_000_000.0);
            float h = field.Evaluate(c);
            Assert.IsFalse(float.IsNaN(h) || float.IsInfinity(h), "huge coords must be finite (noise quality far out is a known limit)");
            Assert.AreEqual(h, field.Evaluate(c));
        }

        [Test]
        public void OffGridFractionalCoordinate_Evaluates()
        {
            // No grid assumption: a fractional, non-cell-aligned coordinate works.
            var field = Build();
            float h = field.Evaluate(new WorldCoordinate(0.3333, 0.6667));
            Assert.IsFalse(float.IsNaN(h) || float.IsInfinity(h));
        }
    }
}

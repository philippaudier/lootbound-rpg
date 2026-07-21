using System;
using NUnit.Framework;
using Lootbound.World.Coordinates;
using Lootbound.World.Layers.Fields;
using Lootbound.World.Processing;

namespace Lootbound.World.Tests
{
    /// <summary>
    /// Pure tests for the analytic World Knowledge fields (Slope, Curvature,
    /// Roughness, Elevation, Exposure, Cliff): determinism, coherence on known
    /// reliefs, and finiteness at very large coordinates. Fields are built over
    /// controlled synthetic height functions.
    /// </summary>
    public class ProcessingAnalyticFieldsTests
    {
        private const float HeightScale = 100f;
        private const float Step = 2f;

        private sealed class FuncField : IWorldField<float>
        {
            private readonly Func<double, double, float> _f;
            public FuncField(Func<double, double, float> f) => _f = f;
            public float Evaluate(WorldCoordinate c) => _f(c.X, c.Z);
        }

        private static FuncField Constant(float v) => new FuncField((_, __) => v);

        [Test]
        public void Slope_FlatGround_IsZero()
        {
            var slope = new SlopeField(Constant(0.5f), HeightScale, Step);
            Assert.AreEqual(0f, slope.Evaluate(new WorldCoordinate(123, -456)), 1e-4f);
        }

        [Test]
        public void Slope_LinearRamp_MatchesAtan()
        {
            // H = 0.01*x (normalized) -> metres gradient = 0.01*100 = 1 -> 45 degrees.
            var slope = new SlopeField(new FuncField((x, _) => (float)(0.01 * x)), HeightScale, Step);
            Assert.AreEqual(45f, slope.Evaluate(new WorldCoordinate(500, 500)), 0.01f);
        }

        [Test]
        public void Slope_IsDeterministicAndFiniteAtHugeCoordinates()
        {
            var slope = new SlopeField(new FuncField((x, z) => 0.5f + 0.1f * (float)Math.Sin(x * 0.001)), HeightScale, Step);
            var c = new WorldCoordinate(9_000_000.0, -12_000_000.0);
            float a = slope.Evaluate(c);
            Assert.IsFalse(float.IsNaN(a) || float.IsInfinity(a));
            Assert.AreEqual(a, slope.Evaluate(c));
        }

        [Test]
        public void Curvature_Peak_IsConvexPositive()
        {
            // Dome: H = 1 - k*(x^2+z^2) -> peak at origin -> convex -> curvature > 0.
            var dome = new FuncField((x, z) => 1f - 1e-4f * (float)(x * x + z * z));
            var curv = new CurvatureField(dome, HeightScale, Step);
            Assert.Greater(curv.Evaluate(new WorldCoordinate(0, 0)), 0f);
        }

        [Test]
        public void Curvature_Valley_IsConcaveNegative()
        {
            var bowl = new FuncField((x, z) => 1e-4f * (float)(x * x + z * z));
            var curv = new CurvatureField(bowl, HeightScale, Step);
            Assert.Less(curv.Evaluate(new WorldCoordinate(0, 0)), 0f);
        }

        [Test]
        public void Roughness_FlatIsZero_VariedIsPositive()
        {
            var flat = new RoughnessField(Constant(0.4f), HeightScale, 6f);
            Assert.AreEqual(0f, flat.Evaluate(new WorldCoordinate(10, 10)), 1e-4f);

            var bumpy = new RoughnessField(new FuncField((x, z) => 0.5f + 0.2f * (float)Math.Sin(x) * (float)Math.Cos(z)), HeightScale, 6f);
            Assert.Greater(bumpy.Evaluate(new WorldCoordinate(3, 3)), 0f);
        }

        [Test]
        public void Curvature_IsNotRoughness_LinearRampHasZeroCurvature()
        {
            // A perfectly smooth linear ramp bends nowhere: curvature ~ 0. Its
            // slope still makes the window vary, so raw V1 roughness > 0. This
            // proves curvature and roughness are DIFFERENT quantities.
            var ramp = new FuncField((x, _) => (float)(0.01 * x));
            var curv = new CurvatureField(ramp, HeightScale, Step);
            var rough = new RoughnessField(ramp, HeightScale, 6f);

            var c = new WorldCoordinate(200, 200);
            Assert.AreEqual(0f, curv.Evaluate(c), 1e-2f, "a linear ramp has no curvature");
            Assert.Greater(rough.Evaluate(c), 0f);
        }

        [Test]
        public void Elevation_IsHeightPassthrough()
        {
            var elev = new ElevationField(new FuncField((x, _) => (float)(x * 0.001)));
            Assert.AreEqual(0.3f, elev.Evaluate(new WorldCoordinate(300, 0)), 1e-5f);
        }

        [Test]
        public void Exposure_FlatIsMinusOne()
        {
            var exp = new ExposureField(Constant(0.5f), HeightScale, Step);
            Assert.AreEqual(-1f, exp.Evaluate(new WorldCoordinate(5, 5)), 1e-4f);
        }

        [Test]
        public void Exposure_SlopeRisingEast_FacesWest()
        {
            // Height rises toward +x (East); the slope faces downhill = -x (West).
            var exp = new ExposureField(new FuncField((x, _) => (float)(0.01 * x)), HeightScale, Step);
            float bearing = exp.Evaluate(new WorldCoordinate(100, 100));
            Assert.AreEqual(Aspect.West, AspectClassifier.FromBearing(bearing));
        }

        [Test]
        public void Cliff_AboveThreshold_IsTrue_BelowIsFalse()
        {
            var gentle = new SlopeField(new FuncField((x, _) => (float)(0.001 * x)), HeightScale, Step); // ~5.7 deg
            var steep = new SlopeField(new FuncField((x, _) => (float)(0.1 * x)), HeightScale, Step);    // ~84 deg

            Assert.IsFalse(new CliffField(gentle, 60f).Evaluate(new WorldCoordinate(50, 0)));
            Assert.IsTrue(new CliffField(steep, 60f).Evaluate(new WorldCoordinate(50, 0)));
        }
    }
}

using System;
using NUnit.Framework;
using Lootbound.World.Coordinates;
using Lootbound.World.Layers.Fields;
using Lootbound.World.Processing;

namespace Lootbound.World.Tests
{
    /// <summary>
    /// Pure tests for the hydrology chain (Flow → Catchment → WaterTable /
    /// RiverMask) over a WorldDomain. Uses a plane tilting toward +X so water
    /// flows East and accumulates along each row.
    /// </summary>
    public class ProcessingHydrologyTests
    {
        private const float HeightScale = 100f;

        private sealed class FuncField : IWorldField<float>
        {
            private readonly Func<double, double, float> _f;
            public FuncField(Func<double, double, float> f) => _f = f;
            public float Evaluate(WorldCoordinate c) => _f(c.X, c.Z);
        }

        // 11x11 grid over [0,100]^2 (cell size 10 m). Height decreases toward +X.
        private static WorldDomain Domain() => WorldDomain.FromOrigin(100, 11);
        private static FuncField TiltEast() => new FuncField((x, _) => 1f - 0.01f * (float)x);

        [Test]
        public void Flow_OnPlaneTiltedEast_PointsEast()
        {
            var flow = FlowAnalyzer.Analyze(TiltEast(), Domain(), HeightScale);
            Assert.AreEqual(FlowDirection.East, flow.DirectionAt(5, 5));
        }

        [Test]
        public void Flow_LowEdgeIsASink()
        {
            var flow = FlowAnalyzer.Analyze(TiltEast(), Domain(), HeightScale);
            Assert.AreEqual(FlowDirection.None, flow.DirectionAt(10, 5), "the lowest edge has no lower neighbour");
        }

        [Test]
        public void Catchment_IncreasesDownstream_UpstreamIsOne()
        {
            var flow = FlowAnalyzer.Analyze(TiltEast(), Domain(), HeightScale);
            var catchment = CatchmentAnalyzer.Analyze(flow);

            Assert.AreEqual(1f, catchment.AccumulationAt(0, 5), 1e-4f, "the top cell has no upstream");
            Assert.AreEqual(11f, catchment.AccumulationAt(10, 5), 1e-4f, "the whole row (11 cells) drains to the low edge");
            Assert.Greater(catchment.AccumulationAt(8, 5), catchment.AccumulationAt(3, 5), "accumulation grows downstream");
        }

        [Test]
        public void RiverMask_HighAccumulationIsRiver_LowIsNot()
        {
            var catchment = CatchmentAnalyzer.Analyze(FlowAnalyzer.Analyze(TiltEast(), Domain(), HeightScale));
            var river = RiverMaskAnalyzer.Analyze(catchment, accumulationThreshold: 5f);

            var domain = Domain();
            Assert.IsTrue(river.Evaluate(domain.CellToWorld(10, 5)), "the low edge collects a river");
            Assert.IsFalse(river.Evaluate(domain.CellToWorld(0, 5)), "the ridge is not a river");
        }

        [Test]
        public void WaterTable_IsWetterWhereMoreWaterDrains()
        {
            var catchment = CatchmentAnalyzer.Analyze(FlowAnalyzer.Analyze(TiltEast(), Domain(), HeightScale));
            var water = WaterTableAnalyzer.Analyze(catchment, new ElevationField(TiltEast()), wetnessScale: 11f);

            var domain = Domain();
            Assert.Greater(
                water.Evaluate(domain.CellToWorld(10, 5)),
                water.Evaluate(domain.CellToWorld(0, 5)),
                "the draining low edge is wetter than the dry ridge");
        }

        [Test]
        public void Hydrology_IsDeterministic()
        {
            var a = CatchmentAnalyzer.Analyze(FlowAnalyzer.Analyze(TiltEast(), Domain(), HeightScale));
            var b = CatchmentAnalyzer.Analyze(FlowAnalyzer.Analyze(TiltEast(), Domain(), HeightScale));
            for (int ix = 0; ix < 11; ix++)
                for (int iz = 0; iz < 11; iz++)
                    Assert.AreEqual(a.AccumulationAt(ix, iz), b.AccumulationAt(ix, iz));
        }
    }
}

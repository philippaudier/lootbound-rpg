using Lootbound.World.Layers.Fields;
using Lootbound.World.Processing;
using Lootbound.Gameplay.World.Providers;

namespace Lootbound.Gameplay.World.Knowledge
{
    /// <summary>
    /// Composition seam for Domain Processing: wires the World Engine's
    /// HeightField into the analyzers and produces the full <see cref="WorldKnowledge"/>.
    /// Maps the Unity config into plain settings; nothing leaks back into the
    /// pure World layer. The hydrology domain (resolution) lives here as a V1
    /// default, never inside an algorithm.
    /// </summary>
    public static class WorldKnowledgeComposer
    {
        public static WorldKnowledge Build(TerrainGenerationConfig config, int seed, int hydrologyResolution = 129)
        {
            var settings = new WorldKnowledgeSettings
            {
                HeightScale = config.TerrainHeight
            };

            var offsets = new NoiseOffsets(seed);
            IWorldField<float> height = WorldFieldComposer.BuildHeightField(config, offsets);

            var slope = new SlopeField(height, settings.HeightScale, settings.SampleStep);
            var curvature = new CurvatureField(height, settings.HeightScale, settings.SampleStep);
            var roughness = new RoughnessField(height, settings.HeightScale, settings.RoughnessRadius);
            var elevation = new ElevationField(height);
            var exposure = new ExposureField(height, settings.HeightScale, settings.SampleStep);
            var cliff = new CliffField(slope, settings.CliffSlopeThreshold);

            var domain = WorldDomain.FromOrigin(config.WorldSize, hydrologyResolution);
            var flow = FlowAnalyzer.Analyze(height, domain, settings.HeightScale);
            var catchment = CatchmentAnalyzer.Analyze(flow);
            var waterTable = WaterTableAnalyzer.Analyze(catchment, elevation, wetnessScale: hydrologyResolution);
            var riverMask = RiverMaskAnalyzer.Analyze(catchment, accumulationThreshold: hydrologyResolution * 0.5f);

            var traversability = new TraversabilityField(slope, cliff, roughness, riverMask, settings);
            var landscape = new LandscapeField(elevation, slope, curvature, cliff, riverMask, settings);

            return new WorldKnowledge
            {
                Slope = slope,
                Curvature = curvature,
                Roughness = roughness,
                Elevation = elevation,
                Exposure = exposure,
                Cliff = cliff,
                Flow = flow,
                Catchment = catchment,
                WaterTable = waterTable,
                RiverMask = riverMask,
                HydrologyDomain = domain,
                Traversability = traversability,
                Landscape = landscape
            };
        }
    }
}

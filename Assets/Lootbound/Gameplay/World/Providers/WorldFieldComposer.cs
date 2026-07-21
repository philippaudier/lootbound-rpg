using Lootbound.World.Layers.Fields;
using Lootbound.World.Sampling;
using Lootbound.Gameplay.World.Progression;

namespace Lootbound.Gameplay.World.Providers
{
    /// <summary>
    /// Composition seam: maps the Unity authoring config into the World Engine's
    /// plain settings and wires the injected Providers into the pure fields and
    /// the sampler. This is where "Unity meets the World Engine"; nothing here
    /// leaks back into the World layer.
    /// </summary>
    public static class WorldFieldComposer
    {
        public static HeightFieldSettings BuildHeightSettings(TerrainGenerationConfig config)
        {
            return new HeightFieldSettings
            {
                WorldSize = config.WorldSize,
                MacroScale = config.MacroScale,
                MacroOctaves = config.MacroOctaves,
                MacroPersistence = config.MacroPersistence,
                MacroLacunarity = config.MacroLacunarity,
                RidgeScale = config.RidgeScale,
                RidgeStrength = config.RidgeStrength,
                ValleyScale = config.ValleyScale,
                ValleyStrength = config.ValleyStrength,
                DetailScale = config.DetailScale,
                DetailStrength = config.DetailStrength,
                GlobalHeightStrength = config.GlobalHeightStrength
            };
        }

        /// <summary>The HeightField, sharing the provided deterministic offsets.</summary>
        public static HeightField BuildHeightField(TerrainGenerationConfig config, NoiseOffsets offsets)
        {
            return new HeightField(
                new UnityPerlinNoiseSource(),
                new AnimationCurveHeightRemap(config.HeightRemap),
                BuildHeightSettings(config),
                offsets);
        }

        public static RegionField BuildRegionField(IWorldField<float> height, TerrainGenerationConfig config)
        {
            return new RegionField(height, config.LowlandThreshold, config.HighlandThreshold);
        }

        /// <summary>
        /// The full official entry point: Height + Danger + Region at any
        /// coordinate. Danger needs the resolved progression (refuge + rings),
        /// available once the layout exists.
        /// </summary>
        public static WorldSampler BuildSampler(TerrainGenerationConfig config, NoiseOffsets offsets, WorldProgression progression)
        {
            var height = BuildHeightField(config, offsets);
            var danger = new WorldProgressionDangerProvider(progression);
            var region = BuildRegionField(height, config);
            return new WorldSampler(height, danger, region);
        }
    }
}

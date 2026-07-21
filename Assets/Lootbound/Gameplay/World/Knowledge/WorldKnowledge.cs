using Lootbound.World.Layers.Fields;
using Lootbound.World.Processing;

namespace Lootbound.Gameplay.World.Knowledge
{
    /// <summary>
    /// The World Knowledge for one world (seed + config): the derived fields the
    /// engine deduces about its geography. Produced by
    /// <see cref="WorldKnowledgeComposer"/>, consumed by the F8 debug overlay
    /// today and by future gameplay systems (paths, structures, wildlife...).
    /// Pure holder - no logic.
    /// </summary>
    public sealed class WorldKnowledge
    {
        public IWorldField<float> Slope;
        public IWorldField<float> Curvature;
        public IWorldField<float> Roughness;
        public IWorldField<float> Elevation;
        public IWorldField<float> Exposure;
        public IWorldField<bool> Cliff;

        public FlowField Flow;
        public CatchmentField Catchment;
        public WaterTableField WaterTable;
        public RiverMaskField RiverMask;
        public WorldDomain HydrologyDomain;

        public IWorldField<float> Traversability;
        public IWorldField<LandscapeType> Landscape;
    }
}

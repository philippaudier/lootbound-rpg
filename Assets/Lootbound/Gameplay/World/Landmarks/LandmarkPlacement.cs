using Lootbound.Gameplay.World.Layout;
using Lootbound.Gameplay.World.Spawning;

namespace Lootbound.Gameplay.World.Landmarks
{
    /// <summary>
    /// The pre-terrain result of landmark planning: everything decided from the
    /// layout ALONE (host XZ, ring/depth/difficulty, selected definition), with
    /// NO final ground height yet. This is the first half of the split that
    /// breaks the circular dependency "need the landmark to know the stamp, need
    /// the stamp to know the height, need the height to finalize the landmark":
    /// placements are computed pre-stamp, the terrain is seated, then
    /// <see cref="LandmarkPlanner.Finalize"/> grounds each landmark on the
    /// stamped terrain.
    ///
    /// Ring / depth / difficulty and definition selection are purely horizontal
    /// (the progression uses distance-from-refuge, not height), so they are
    /// stable under stamping - which is exactly why the split is sound.
    /// </summary>
    public sealed class LandmarkPlacement
    {
        /// <summary>Stable identity: landmark_{worldSeed}_{hostNodeId}_{slot}.</summary>
        public string LandmarkId { get; }

        /// <summary>Archetype selected for this place. Carries the terrain-integration parameters.</summary>
        public LandmarkDefinition Definition { get; }

        /// <summary>World X of the host (never moved by terrain integration).</summary>
        public float X { get; }

        /// <summary>World Z of the host (never moved by terrain integration).</summary>
        public float Z { get; }

        public WorldRing Ring { get; }
        public float Depth01 { get; }
        public float Difficulty01 { get; }
        public string RadialPathId { get; }
        public string HostNodeId { get; }
        public int Slot { get; }

        public LandmarkPlacement(
            string landmarkId,
            LandmarkDefinition definition,
            float x,
            float z,
            WorldRing ring,
            float depth01,
            float difficulty01,
            string radialPathId,
            string hostNodeId,
            int slot)
        {
            LandmarkId = landmarkId;
            Definition = definition;
            X = x;
            Z = z;
            Ring = ring;
            Depth01 = depth01;
            Difficulty01 = difficulty01;
            RadialPathId = radialPathId;
            HostNodeId = hostNodeId;
            Slot = slot;
        }
    }
}

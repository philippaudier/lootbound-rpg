using UnityEngine;
using Lootbound.Gameplay.World.Layout;

namespace Lootbound.Gameplay.World.Landmarks
{
    /// <summary>
    /// The permanent identity of a landmark - a notable place of the world,
    /// the "geographic memory" other systems anchor to (wildlife, merchants,
    /// encounters, lore, campfires...). What stays permanent is the identity,
    /// not any runtime representation of it.
    ///
    /// Pure, immutable, serializable data: no ScriptableObject reference, only
    /// stable values (strings / Vector3 / enum / float). The presentation
    /// layer resolves DefinitionId through the LandmarkRegistry to obtain the
    /// prefab. The identity is derived deterministically from the world seed
    /// and the host node: landmark_{worldSeed}_{hostNodeId}_{slot}.
    /// </summary>
    public sealed class LandmarkIdentity
    {
        /// <summary>Stable ID: landmark_{worldSeed}_{hostNodeId}_{slot}.</summary>
        public string LandmarkId { get; }

        /// <summary>Stable ID of the LandmarkDefinition selected for this place.</summary>
        public string DefinitionId { get; }

        /// <summary>World position, grounded on the final terrain.</summary>
        public Vector3 Position { get; }

        /// <summary>Spatial ring of this place.</summary>
        public WorldRing Ring { get; }

        /// <summary>Global depth (0 = Refuge, 1 = disc edge).</summary>
        public float Depth01 { get; }

        /// <summary>Expected danger at this place (0 = calm, 1 = deepest).</summary>
        public float Difficulty01 { get; }

        /// <summary>RadialPath this place belongs to (null if none).</summary>
        public string RadialPathId { get; }

        /// <summary>Layout node this place was born from.</summary>
        public string HostNodeId { get; }

        /// <summary>Slot on the host node (0 in V1; part of the identity for future multi-landmark hosts).</summary>
        public int Slot { get; }

        /// <summary>Distance at which the place counts as noticed/discovered (from the definition).</summary>
        public float DiscoveryRadius { get; }

        public LandmarkIdentity(
            string landmarkId,
            string definitionId,
            Vector3 position,
            WorldRing ring,
            float depth01,
            float difficulty01,
            string radialPathId,
            string hostNodeId,
            int slot,
            float discoveryRadius)
        {
            LandmarkId = landmarkId;
            DefinitionId = definitionId;
            Position = position;
            Ring = ring;
            Depth01 = depth01;
            Difficulty01 = difficulty01;
            RadialPathId = radialPathId;
            HostNodeId = hostNodeId;
            Slot = slot;
            DiscoveryRadius = discoveryRadius;
        }

        public override string ToString()
        {
            return $"Landmark[{LandmarkId}] def={DefinitionId} {Ring} @ {Position} r={DiscoveryRadius:F0}m";
        }
    }
}

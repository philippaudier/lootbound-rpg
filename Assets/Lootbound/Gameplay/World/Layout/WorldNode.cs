using System.Collections.Generic;
using UnityEngine;

namespace Lootbound.Gameplay.World.Layout
{
    /// <summary>
    /// A node in the world layout graph representing a significant location.
    /// </summary>
    public sealed class WorldNode
    {
        /// <summary>
        /// Deterministic ID: "node_{seed}_{type}_{index}".
        /// </summary>
        public string NodeId { get; }

        /// <summary>
        /// Type of this node.
        /// </summary>
        public WorldNodeType Type { get; }

        /// <summary>
        /// World position of this node.
        /// </summary>
        public Vector3 Position { get; }

        /// <summary>
        /// Radius of this node's area of influence.
        /// </summary>
        public float Radius { get; }

        #region Global Radial Progression

        /// <summary>
        /// Absolute distance from the Refuge center in world units.
        /// </summary>
        public float DistanceFromRefuge { get; }

        /// <summary>
        /// Normalized distance from Refuge (0.0) to world edge (1.0).
        /// Calculated against the logical WorldDisc radius, not local terrain size.
        /// </summary>
        public float NormalizedWorldRadius { get; }

        /// <summary>
        /// The WorldRing this node belongs to.
        /// </summary>
        public WorldRing Ring { get; }

        #endregion

        #region Local Path Progression

        /// <summary>
        /// ID of the RadialPath this node belongs to.
        /// Null for Refuge node. Non-null for all path and branch nodes.
        /// Branch nodes inherit the RadialPathId of their host path.
        /// </summary>
        public string RadialPathId { get; }

        /// <summary>
        /// Topological order within the primary path spine.
        /// 0 = first node after Refuge connection.
        /// -1 for Refuge node and branch nodes (not on primary spine).
        /// </summary>
        public int PathStepIndex { get; }

        #endregion

        #region Terrain Data

        /// <summary>
        /// Terrain height at this node's position.
        /// </summary>
        public float TerrainHeight { get; }

        /// <summary>
        /// Terrain slope at this node's position in degrees.
        /// </summary>
        public float TerrainSlope { get; }

        #endregion

        #region Connections

        /// <summary>
        /// IDs of edges connected to this node.
        /// </summary>
        public IReadOnlyList<string> ConnectedEdgeIds => _connectedEdgeIds;
        private readonly List<string> _connectedEdgeIds;

        #endregion

        public WorldNode(
            string nodeId,
            WorldNodeType type,
            Vector3 position,
            float radius,
            float distanceFromRefuge,
            float normalizedWorldRadius,
            WorldRing ring,
            string radialPathId,
            int pathStepIndex,
            float terrainHeight,
            float terrainSlope)
        {
            NodeId = nodeId;
            Type = type;
            Position = position;
            Radius = radius;
            DistanceFromRefuge = distanceFromRefuge;
            NormalizedWorldRadius = normalizedWorldRadius;
            Ring = ring;
            RadialPathId = radialPathId;
            PathStepIndex = pathStepIndex;
            TerrainHeight = terrainHeight;
            TerrainSlope = terrainSlope;
            _connectedEdgeIds = new List<string>();
        }

        /// <summary>
        /// Add an edge connection to this node.
        /// </summary>
        internal void AddEdgeConnection(string edgeId)
        {
            if (!_connectedEdgeIds.Contains(edgeId))
            {
                _connectedEdgeIds.Add(edgeId);
            }
        }

        /// <summary>
        /// Check if this node is on a primary path spine (not a branch).
        /// </summary>
        public bool IsOnPrimaryPath => PathStepIndex >= 0;

        /// <summary>
        /// Check if this node is a branch node.
        /// </summary>
        public bool IsBranchNode => RadialPathId != null && PathStepIndex < 0;

        /// <summary>
        /// Generate a deterministic node ID.
        /// </summary>
        public static string GenerateId(int seed, WorldNodeType type, int index)
        {
            return $"node_{seed}_{type}_{index}";
        }

        public override string ToString()
        {
            return $"WorldNode[{NodeId}] {Type} @ {Position} ring={Ring} dist={DistanceFromRefuge:F1}m";
        }
    }
}

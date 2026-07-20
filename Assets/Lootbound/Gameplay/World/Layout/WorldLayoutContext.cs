using System.Collections.Generic;
using UnityEngine;

namespace Lootbound.Gameplay.World.Layout
{
    /// <summary>
    /// Complete world layout containing all nodes, edges, paths, and reservations.
    /// This is runtime data representing a local layout region, not the complete WorldDisc.
    ///
    /// For the prototype, this represents the entire generated terrain.
    /// In the future, this will represent a loaded WorldSector within the larger WorldDisc.
    /// </summary>
    public sealed class WorldLayoutContext
    {
        #region Generation Identity

        /// <summary>
        /// The original world seed used for generation.
        /// </summary>
        public int WorldSeed { get; }

        /// <summary>
        /// Which retry attempt succeeded (0-based).
        /// </summary>
        public int GenerationAttempt { get; }

        /// <summary>
        /// The effective seed used for this layout (WorldSeed combined with attempt).
        /// </summary>
        public int EffectiveLayoutSeed { get; }

        #endregion

        #region World Definition

        /// <summary>
        /// Logical radius of the complete WorldDisc.
        /// NormalizedWorldRadius is calculated against this value, not local terrain size.
        /// </summary>
        public float WorldDiscRadius { get; }

        /// <summary>
        /// Center position of the Refuge (world coordinates).
        /// </summary>
        public Vector3 RefugePosition { get; }

        /// <summary>
        /// Ring configuration for evaluating positions.
        /// </summary>
        public WorldRingConfig RingConfig { get; }

        /// <summary>
        /// Progression authority for this world (attached after layout
        /// validation). Every consumer reads position context through it;
        /// nobody computes refuge distance or depth locally.
        /// </summary>
        public Progression.WorldProgression Progression { get; private set; }

        /// <summary>
        /// Attach the progression authority (called once by the generator
        /// after the layout is published).
        /// </summary>
        public void AttachProgression(Progression.WorldProgression progression)
        {
            Progression = progression;
        }

        /// <summary>
        /// The landmarks of this world - permanent notable places, the shared
        /// generation result consumed by the LandmarkDirector and the ambient
        /// population as independent readers. Read-only, never null (empty
        /// until attached), single-assignment like the progression authority.
        /// </summary>
        public IReadOnlyList<Landmarks.LandmarkIdentity> Landmarks => _landmarks;
        private IReadOnlyList<Landmarks.LandmarkIdentity> _landmarks = System.Array.Empty<Landmarks.LandmarkIdentity>();
        private bool _landmarksAttached;

        /// <summary>
        /// Attach the landmark set (called once by the generator after the
        /// layout is published). A null set attaches an empty collection.
        /// Subsequent calls are ignored (single-assignment).
        /// </summary>
        public void AttachLandmarks(IReadOnlyList<Landmarks.LandmarkIdentity> landmarks)
        {
            if (_landmarksAttached)
            {
                return;
            }

            _landmarks = landmarks ?? System.Array.Empty<Landmarks.LandmarkIdentity>();
            _landmarksAttached = true;
        }

        #endregion

        #region Nodes and Edges

        /// <summary>
        /// Ordered list of all nodes (deterministic iteration order).
        /// </summary>
        public IReadOnlyList<WorldNode> NodesOrdered => _nodesOrdered;
        private readonly List<WorldNode> _nodesOrdered;

        /// <summary>
        /// Ordered list of all edges (deterministic iteration order).
        /// </summary>
        public IReadOnlyList<WorldEdge> EdgesOrdered => _edgesOrdered;
        private readonly List<WorldEdge> _edgesOrdered;

        /// <summary>
        /// Fast lookup for nodes by ID.
        /// </summary>
        public IReadOnlyDictionary<string, WorldNode> NodesById => _nodesById;
        private readonly Dictionary<string, WorldNode> _nodesById;

        /// <summary>
        /// Fast lookup for edges by ID.
        /// </summary>
        public IReadOnlyDictionary<string, WorldEdge> EdgesById => _edgesById;
        private readonly Dictionary<string, WorldEdge> _edgesById;

        #endregion

        #region Radial Paths

        /// <summary>
        /// All radial paths from Refuge outward.
        /// </summary>
        public IReadOnlyList<RadialPath> RadialPaths => _radialPaths;
        private readonly List<RadialPath> _radialPaths;

        /// <summary>
        /// The Refuge node (quick access).
        /// </summary>
        public WorldNode RefugeNode { get; private set; }

        /// <summary>
        /// All OuterDestination nodes (termini of radial paths).
        /// </summary>
        public IReadOnlyList<WorldNode> OuterDestinationNodes => _outerDestinationNodes;
        private readonly List<WorldNode> _outerDestinationNodes;

        #endregion

        #region Reservations

        /// <summary>
        /// Encounter reservations (not graph nodes).
        /// </summary>
        public IReadOnlyList<EncounterReservation> EncounterReservations => _encounterReservations;
        private readonly List<EncounterReservation> _encounterReservations;

        /// <summary>
        /// Resource reservations (not graph nodes).
        /// </summary>
        public IReadOnlyList<ResourceReservation> ResourceReservations => _resourceReservations;
        private readonly List<ResourceReservation> _resourceReservations;

        #endregion

        public WorldLayoutContext(
            int worldSeed,
            int generationAttempt,
            int effectiveLayoutSeed,
            float worldDiscRadius,
            Vector3 refugePosition,
            WorldRingConfig ringConfig)
        {
            WorldSeed = worldSeed;
            GenerationAttempt = generationAttempt;
            EffectiveLayoutSeed = effectiveLayoutSeed;
            WorldDiscRadius = worldDiscRadius;
            RefugePosition = refugePosition;
            RingConfig = ringConfig;

            _nodesOrdered = new List<WorldNode>();
            _edgesOrdered = new List<WorldEdge>();
            _nodesById = new Dictionary<string, WorldNode>();
            _edgesById = new Dictionary<string, WorldEdge>();
            _radialPaths = new List<RadialPath>();
            _outerDestinationNodes = new List<WorldNode>();
            _encounterReservations = new List<EncounterReservation>();
            _resourceReservations = new List<ResourceReservation>();
        }

        #region Node/Edge Management

        /// <summary>
        /// Add a node to the layout.
        /// </summary>
        internal void AddNode(WorldNode node)
        {
            _nodesOrdered.Add(node);
            _nodesById[node.NodeId] = node;

            if (node.Type == WorldNodeType.Refuge)
            {
                RefugeNode = node;
            }
            else if (node.Type == WorldNodeType.OuterDestination)
            {
                _outerDestinationNodes.Add(node);
            }
        }

        /// <summary>
        /// Add an edge to the layout.
        /// </summary>
        internal void AddEdge(WorldEdge edge)
        {
            _edgesOrdered.Add(edge);
            _edgesById[edge.EdgeId] = edge;

            // Connect nodes
            if (_nodesById.TryGetValue(edge.NodeAId, out var nodeA))
            {
                nodeA.AddEdgeConnection(edge.EdgeId);
            }
            if (_nodesById.TryGetValue(edge.NodeBId, out var nodeB))
            {
                nodeB.AddEdgeConnection(edge.EdgeId);
            }
        }

        /// <summary>
        /// Add a radial path to the layout.
        /// </summary>
        internal void AddRadialPath(RadialPath path)
        {
            _radialPaths.Add(path);
        }

        #endregion

        #region Reservation Management

        /// <summary>
        /// Add an encounter reservation.
        /// </summary>
        internal void AddEncounterReservation(EncounterReservation reservation)
        {
            _encounterReservations.Add(reservation);
        }

        /// <summary>
        /// Add a resource reservation.
        /// </summary>
        internal void AddResourceReservation(ResourceReservation reservation)
        {
            _resourceReservations.Add(reservation);
        }

        #endregion

        #region Queries

        /// <summary>
        /// Get the nearest node to a world position.
        /// </summary>
        public WorldNode GetNearestNode(Vector3 position)
        {
            WorldNode nearest = null;
            float minDistSq = float.MaxValue;

            foreach (var node in _nodesOrdered)
            {
                float distSq = (node.Position - position).sqrMagnitude;
                if (distSq < minDistSq)
                {
                    minDistSq = distSq;
                    nearest = node;
                }
            }

            return nearest;
        }

        /// <summary>
        /// Get the ring sample at a world position.
        /// </summary>
        public WorldRingSample GetRingSampleAt(Vector3 position)
        {
            return WorldRingEvaluator.Evaluate(position, RefugePosition, WorldDiscRadius, RingConfig);
        }

        /// <summary>
        /// Check if all radial paths are fully traversable.
        /// </summary>
        public bool AreAllRadialPathsTraversable()
        {
            foreach (var path in _radialPaths)
            {
                foreach (var edgeId in path.EdgeIds)
                {
                    if (_edgesById.TryGetValue(edgeId, out var edge))
                    {
                        if (!edge.IsTraversable)
                        {
                            return false;
                        }
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// Get a radial path by ID.
        /// </summary>
        public RadialPath GetRadialPath(string pathId)
        {
            foreach (var path in _radialPaths)
            {
                if (path.PathId == pathId)
                {
                    return path;
                }
            }
            return null;
        }

        /// <summary>
        /// Get all nodes belonging to a specific radial path (including branches).
        /// </summary>
        public IEnumerable<WorldNode> GetNodesOnPath(string pathId)
        {
            foreach (var node in _nodesOrdered)
            {
                if (node.RadialPathId == pathId)
                {
                    yield return node;
                }
            }
        }

        /// <summary>
        /// Get all edges belonging to a specific radial path.
        /// </summary>
        public IEnumerable<WorldEdge> GetEdgesOnPath(string pathId)
        {
            foreach (var edge in _edgesOrdered)
            {
                if (edge.RadialPathId == pathId)
                {
                    yield return edge;
                }
            }
        }

        /// <summary>
        /// Get all primary path edges (not branch edges).
        /// </summary>
        public IEnumerable<WorldEdge> GetPrimaryPathEdges()
        {
            foreach (var edge in _edgesOrdered)
            {
                if (edge.IsPrimaryPathEdge)
                {
                    yield return edge;
                }
            }
        }

        /// <summary>
        /// Get all branch edges.
        /// </summary>
        public IEnumerable<WorldEdge> GetBranchEdges()
        {
            foreach (var edge in _edgesOrdered)
            {
                if (!edge.IsPrimaryPathEdge && edge.RadialPathId != null)
                {
                    yield return edge;
                }
            }
        }

        #endregion
    }
}

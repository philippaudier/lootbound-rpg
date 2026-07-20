using UnityEngine;

namespace Lootbound.Gameplay.World.Layout
{
    /// <summary>
    /// A reserved location for an encounter, attached to a host node.
    /// This is not a graph node - it represents a spawn position for enemies.
    /// </summary>
    public sealed class EncounterReservation
    {
        /// <summary>
        /// Deterministic ID: "encounter_{seed}_{index}".
        /// </summary>
        public string ReservationId { get; }

        /// <summary>
        /// ID of the node this reservation is attached to.
        /// </summary>
        public string HostNodeId { get; }

        /// <summary>
        /// World position of this reservation.
        /// </summary>
        public Vector3 Position { get; private set; }

        /// <summary>
        /// Replace the stored world-space height. Called by the
        /// post-flattening reprojection pass so the position matches the
        /// final terrain; XZ never changes.
        /// </summary>
        public void ReprojectHeight(float worldY)
        {
            Position = new Vector3(Position.x, worldY, Position.z);
        }

        /// <summary>
        /// Radius of the encounter area.
        /// </summary>
        public float Radius { get; }

        #region Radial Properties

        /// <summary>
        /// Absolute distance from the Refuge center in world units.
        /// </summary>
        public float DistanceFromRefuge { get; }

        /// <summary>
        /// Normalized distance from Refuge (0.0) to world edge (1.0).
        /// </summary>
        public float NormalizedWorldRadius { get; }

        /// <summary>
        /// The WorldRing this reservation belongs to.
        /// </summary>
        public WorldRing Ring { get; }

        /// <summary>
        /// ID of the RadialPath this reservation is associated with.
        /// Null if not associated with a specific path.
        /// </summary>
        public string RadialPathId { get; }

        #endregion

        public EncounterReservation(
            string reservationId,
            string hostNodeId,
            Vector3 position,
            float radius,
            float distanceFromRefuge,
            float normalizedWorldRadius,
            WorldRing ring,
            string radialPathId)
        {
            ReservationId = reservationId;
            HostNodeId = hostNodeId;
            Position = position;
            Radius = radius;
            DistanceFromRefuge = distanceFromRefuge;
            NormalizedWorldRadius = normalizedWorldRadius;
            Ring = ring;
            RadialPathId = radialPathId;
        }

        public static string GenerateId(int seed, int index)
        {
            return $"encounter_{seed}_{index}";
        }

        public override string ToString()
        {
            return $"EncounterReservation[{ReservationId}] host={HostNodeId} ring={Ring}";
        }
    }

    /// <summary>
    /// A reserved location for resources, attached to a host node.
    /// This is not a graph node - it represents a spawn position for resources.
    /// </summary>
    public sealed class ResourceReservation
    {
        /// <summary>
        /// Deterministic ID: "resource_{seed}_{index}".
        /// </summary>
        public string ReservationId { get; }

        /// <summary>
        /// ID of the node this reservation is attached to.
        /// </summary>
        public string HostNodeId { get; }

        /// <summary>
        /// World position of this reservation.
        /// </summary>
        public Vector3 Position { get; private set; }

        /// <summary>
        /// Replace the stored world-space height. Called by the
        /// post-flattening reprojection pass so the position matches the
        /// final terrain; XZ never changes.
        /// </summary>
        public void ReprojectHeight(float worldY)
        {
            Position = new Vector3(Position.x, worldY, Position.z);
        }

        /// <summary>
        /// Radius of the resource area.
        /// </summary>
        public float Radius { get; }

        #region Radial Properties

        /// <summary>
        /// Absolute distance from the Refuge center in world units.
        /// </summary>
        public float DistanceFromRefuge { get; }

        /// <summary>
        /// Normalized distance from Refuge (0.0) to world edge (1.0).
        /// </summary>
        public float NormalizedWorldRadius { get; }

        /// <summary>
        /// The WorldRing this reservation belongs to.
        /// </summary>
        public WorldRing Ring { get; }

        /// <summary>
        /// ID of the RadialPath this reservation is associated with.
        /// Null if not associated with a specific path.
        /// </summary>
        public string RadialPathId { get; }

        #endregion

        public ResourceReservation(
            string reservationId,
            string hostNodeId,
            Vector3 position,
            float radius,
            float distanceFromRefuge,
            float normalizedWorldRadius,
            WorldRing ring,
            string radialPathId)
        {
            ReservationId = reservationId;
            HostNodeId = hostNodeId;
            Position = position;
            Radius = radius;
            DistanceFromRefuge = distanceFromRefuge;
            NormalizedWorldRadius = normalizedWorldRadius;
            Ring = ring;
            RadialPathId = radialPathId;
        }

        public static string GenerateId(int seed, int index)
        {
            return $"resource_{seed}_{index}";
        }

        public override string ToString()
        {
            return $"ResourceReservation[{ReservationId}] host={HostNodeId} ring={Ring}";
        }
    }
}

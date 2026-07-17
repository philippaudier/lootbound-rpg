using System.Collections.Generic;

namespace Lootbound.Gameplay.World.Spawning
{
    /// <summary>
    /// Why a reservation could not be planned or spawned.
    /// </summary>
    public enum SpawnRejectionReason
    {
        /// <summary>No definition in the registry is compatible with the reservation.</summary>
        NoCompatibleDefinition,

        /// <summary>The selected definition has no prefab assigned.</summary>
        MissingPrefab,

        /// <summary>The selected resource definition has no ItemDefinition assigned.</summary>
        MissingItem,

        /// <summary>Encounters are never allowed in the Refuge ring.</summary>
        RefugeExclusion,

        /// <summary>The reservation anchor lies outside the terrain bounds.</summary>
        OutOfTerrainBounds,

        /// <summary>Terrain slope at the anchor exceeds the placement limit.</summary>
        SlopeTooSteep,

        /// <summary>The category is disabled or its registry is missing.</summary>
        CategoryDisabled,

        /// <summary>No NavMesh found near the spawn position (spawn-time, encounters only).</summary>
        NavMeshUnavailable,

        /// <summary>Instantiation failed at spawn time.</summary>
        InstantiationFailed
    }

    /// <summary>
    /// A reservation that was received but did not produce a spawn, with the reason.
    /// </summary>
    public sealed class SpawnRejection
    {
        public string ReservationId { get; }
        public WorldContentCategory Category { get; }
        public SpawnRejectionReason Reason { get; }
        public string Detail { get; }

        public SpawnRejection(string reservationId, WorldContentCategory category, SpawnRejectionReason reason, string detail = null)
        {
            ReservationId = reservationId;
            Category = category;
            Reason = reason;
            Detail = detail;
        }

        public override string ToString()
        {
            string detail = string.IsNullOrEmpty(Detail) ? "" : $" ({Detail})";
            return $"{Category} {ReservationId}: {Reason}{detail}";
        }
    }

    /// <summary>
    /// Deterministic result of planning all reservations of a validated layout.
    /// Pure data - no Unity objects are created at this stage.
    /// </summary>
    public sealed class WorldContentPlan
    {
        public IReadOnlyList<SpawnRecipe> Recipes { get; }
        public IReadOnlyList<SpawnRejection> Rejections { get; }

        /// <summary>Total number of reservations received across all categories.</summary>
        public int TotalReservations { get; }

        public WorldContentPlan(
            IReadOnlyList<SpawnRecipe> recipes,
            IReadOnlyList<SpawnRejection> rejections,
            int totalReservations)
        {
            Recipes = recipes;
            Rejections = rejections;
            TotalReservations = totalReservations;
        }
    }
}

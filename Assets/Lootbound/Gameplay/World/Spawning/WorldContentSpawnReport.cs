using System.Collections.Generic;
using UnityEngine;
using Lootbound.Gameplay.World.Layout;

namespace Lootbound.Gameplay.World.Spawning
{
    /// <summary>
    /// Diagnostic record for one navigation-dependent entry: the position the
    /// deterministic plan requested, the navigable position actually used,
    /// and whether NavMesh resolution succeeded. Diagnostics only - never
    /// consumed by gameplay decisions.
    /// </summary>
    public readonly struct EntryPlacement
    {
        public int EntryIndex { get; }
        public Vector3 RequestedPosition { get; }
        public Vector3 ResolvedPosition { get; }
        public bool NavMeshResolved { get; }

        public float ResolveDistance => NavMeshResolved
            ? Vector3.Distance(RequestedPosition, ResolvedPosition)
            : float.PositiveInfinity;

        public EntryPlacement(int entryIndex, Vector3 requestedPosition, Vector3 resolvedPosition, bool navMeshResolved)
        {
            EntryIndex = entryIndex;
            RequestedPosition = requestedPosition;
            ResolvedPosition = resolvedPosition;
            NavMeshResolved = navMeshResolved;
        }
    }

    /// <summary>
    /// Runtime outcome of instantiating one SpawnRecipe.
    /// </summary>
    public sealed class SpawnOutcome
    {
        public string ReservationId { get; }
        public string HostNodeId { get; }
        public WorldContentCategory Category { get; }
        public string DefinitionId { get; }
        public WorldRing Ring { get; }
        public string RadialPathId { get; }
        public int RequestedEntries { get; }
        public int SpawnedEntries { get; }
        public string FailureDetail { get; }

        /// <summary>
        /// Per-entry NavMesh resolution diagnostics (encounters only; empty
        /// for content that does not depend on navigation).
        /// </summary>
        public IReadOnlyList<EntryPlacement> Placements { get; }

        public bool Success => SpawnedEntries > 0;

        /// <summary>Entries whose NavMesh resolution failed.</summary>
        public int NavMeshMisses
        {
            get
            {
                int count = 0;
                foreach (var placement in Placements)
                {
                    if (!placement.NavMeshResolved) count++;
                }
                return count;
            }
        }

        private static readonly IReadOnlyList<EntryPlacement> EmptyPlacements = new List<EntryPlacement>();

        public SpawnOutcome(SpawnRecipe recipe, int spawnedEntries, string failureDetail = null,
            IReadOnlyList<EntryPlacement> placements = null)
        {
            ReservationId = recipe.ReservationId;
            HostNodeId = recipe.HostNodeId;
            Category = recipe.Category;
            DefinitionId = recipe.DefinitionId;
            Ring = recipe.Ring;
            RadialPathId = recipe.RadialPathId;
            RequestedEntries = recipe.Entries.Count;
            SpawnedEntries = spawnedEntries;
            FailureDetail = failureDetail;
            Placements = placements ?? EmptyPlacements;
        }
    }

    /// <summary>
    /// Full debug report of a spawning pass: what was received, planned,
    /// spawned, and why anything was rejected. Read by the debug panel.
    /// </summary>
    public sealed class WorldContentSpawnReport
    {
        public int ReservationsReceived { get; }
        public IReadOnlyList<SpawnOutcome> Outcomes { get; }
        public IReadOnlyList<SpawnRejection> Rejections { get; }

        public int RecipesPlanned => Outcomes.Count;

        public int SpawnsSucceeded
        {
            get
            {
                int count = 0;
                foreach (var outcome in Outcomes)
                {
                    if (outcome.Success) count++;
                }
                return count;
            }
        }

        public int SpawnsRejected => Rejections.Count + (RecipesPlanned - SpawnsSucceeded);

        /// <summary>Total entries whose NavMesh resolution failed (diagnostics).</summary>
        public int TotalNavMeshMisses
        {
            get
            {
                int count = 0;
                foreach (var outcome in Outcomes)
                {
                    count += outcome.NavMeshMisses;
                }
                return count;
            }
        }

        public WorldContentSpawnReport(
            int reservationsReceived,
            IReadOnlyList<SpawnOutcome> outcomes,
            IReadOnlyList<SpawnRejection> rejections)
        {
            ReservationsReceived = reservationsReceived;
            Outcomes = outcomes;
            Rejections = rejections;
        }
    }
}

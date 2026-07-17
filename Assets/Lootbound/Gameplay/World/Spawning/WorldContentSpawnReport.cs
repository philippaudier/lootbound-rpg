using System.Collections.Generic;
using Lootbound.Gameplay.World.Layout;

namespace Lootbound.Gameplay.World.Spawning
{
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

        public bool Success => SpawnedEntries > 0;

        public SpawnOutcome(SpawnRecipe recipe, int spawnedEntries, string failureDetail = null)
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

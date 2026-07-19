using UnityEngine;
using Lootbound.Gameplay.World.Layout;

namespace Lootbound.Gameplay.World.Spawning
{
    /// <summary>
    /// Where a runtime world instance comes from.
    /// </summary>
    public enum WorldContentOrigin
    {
        /// <summary>Placed through the layout: reservation -> recipe -> spawn.</summary>
        Authored,

        /// <summary>Ambient population: lives freely in a spatial cell.</summary>
        Ambient
    }

    /// <summary>
    /// Persistent identity carried by every runtime world instance - the ONE
    /// identity system for both authored and ambient content. Authored
    /// instances link back to their reservation; ambient instances carry a
    /// namespaced stable id ("ambient_v..."), their cell and their origin.
    /// EnemyBrain seeds its per-instance RNG from (WorldSeed, ReservationId,
    /// EntryIndex) for both origins.
    /// </summary>
    public sealed class WorldContentIdentity : MonoBehaviour
    {
        [SerializeField] private string reservationId;
        [SerializeField] private string hostNodeId;
        [SerializeField] private string definitionId;
        [SerializeField] private WorldContentCategory category;
        [SerializeField] private WorldRing ring;
        [SerializeField] private string radialPathId;
        [SerializeField] private string role;
        [SerializeField] private int worldSeed;
        [SerializeField] private int entryIndex;
        [SerializeField] private WorldContentOrigin origin = WorldContentOrigin.Authored;
        [SerializeField] private Vector2Int cellCoordinate;

        public string ReservationId => reservationId;
        public string HostNodeId => hostNodeId;
        public string DefinitionId => definitionId;
        public WorldContentCategory Category => category;
        public WorldRing Ring => ring;
        public string RadialPathId => radialPathId;
        public string Role => role;

        /// <summary>World seed of the generation this instance belongs to.</summary>
        public int WorldSeed => worldSeed;

        /// <summary>Index of this entry within its recipe (0 for single-entry content).</summary>
        public int EntryIndex => entryIndex;

        /// <summary>Authored (reservation-driven) or Ambient (cell-driven).</summary>
        public WorldContentOrigin Origin => origin;

        /// <summary>Ambient population cell (meaningful when Origin is Ambient).</summary>
        public Vector2Int CellCoordinate => cellCoordinate;

        /// <summary>
        /// Initialize an ambient instance: namespaced stable id, world seed,
        /// spatial cell and member index. Never collides with authored
        /// reservation ids.
        /// </summary>
        public void InitializeAmbient(string stableMemberId, string populationId, int seed, Vector2Int cell, int memberIndex)
        {
            reservationId = stableMemberId;
            hostNodeId = null;
            definitionId = populationId ?? string.Empty;
            category = WorldContentCategory.Encounter;
            role = "Ambient";
            worldSeed = seed;
            entryIndex = memberIndex;
            origin = WorldContentOrigin.Ambient;
            cellCoordinate = cell;
        }

        public void Initialize(SpawnRecipe recipe, string entryRole, int seed, int index)
        {
            reservationId = recipe.ReservationId;
            hostNodeId = recipe.HostNodeId;
            definitionId = recipe.DefinitionId;
            category = recipe.Category;
            ring = recipe.Ring;
            radialPathId = recipe.RadialPathId;
            role = entryRole;
            worldSeed = seed;
            entryIndex = index;
        }
    }
}

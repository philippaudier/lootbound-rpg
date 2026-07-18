using UnityEngine;
using Lootbound.Gameplay.World.Layout;

namespace Lootbound.Gameplay.World.Spawning
{
    /// <summary>
    /// Persistent identity carried by every runtime instance spawned from a
    /// reservation. Links the GameObject back to the generated layout
    /// (reservation and host node) and preserves the radial context.
    /// Attached by WorldContentSpawner at spawn time.
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

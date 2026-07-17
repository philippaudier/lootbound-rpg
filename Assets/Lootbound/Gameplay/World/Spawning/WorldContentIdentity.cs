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

        public string ReservationId => reservationId;
        public string HostNodeId => hostNodeId;
        public string DefinitionId => definitionId;
        public WorldContentCategory Category => category;
        public WorldRing Ring => ring;
        public string RadialPathId => radialPathId;
        public string Role => role;

        public void Initialize(SpawnRecipe recipe, string entryRole)
        {
            reservationId = recipe.ReservationId;
            hostNodeId = recipe.HostNodeId;
            definitionId = recipe.DefinitionId;
            category = recipe.Category;
            ring = recipe.Ring;
            radialPathId = recipe.RadialPathId;
            role = entryRole;
        }
    }
}

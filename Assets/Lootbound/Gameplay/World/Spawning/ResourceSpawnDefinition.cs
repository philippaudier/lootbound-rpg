using UnityEngine;
using Lootbound.Gameplay.Inventory;
using Lootbound.Gameplay.World.Layout;

namespace Lootbound.Gameplay.World.Spawning
{
    /// <summary>
    /// Immutable definition of a resource pickup that can occupy a ResourceReservation.
    /// The actual world object comes from the referenced ItemDefinition's WorldPrefab
    /// through the existing ItemWorldPickup.SpawnPickup path.
    /// </summary>
    [CreateAssetMenu(fileName = "ResourceSpawn_", menuName = "Lootbound/World Content/Resource Spawn Definition")]
    public class ResourceSpawnDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField]
        [Tooltip("Stable ID. Falls back to the asset name when empty.")]
        private string resourceId;

        [Header("Content")]
        [SerializeField]
        [Tooltip("Item granted when the player picks up this resource")]
        private ItemDefinition item;

        [SerializeField]
        [Range(1, 99)]
        private int minimumQuantity = 1;

        [SerializeField]
        [Range(1, 99)]
        private int maximumQuantity = 1;

        [Header("Placement")]
        [SerializeField]
        [Tooltip("Innermost ring where this resource may appear")]
        private WorldRing minimumRing = WorldRing.Refuge;

        public string ResourceId => string.IsNullOrEmpty(resourceId) ? name : resourceId;
        public ItemDefinition Item => item;
        public int MinimumQuantity => Mathf.Min(minimumQuantity, maximumQuantity);
        public int MaximumQuantity => Mathf.Max(minimumQuantity, maximumQuantity);
        public WorldRing MinimumRing => minimumRing;
    }
}

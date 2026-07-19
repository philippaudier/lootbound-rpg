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
        [Tooltip("Innermost ring where this resource may appear (inclusive)")]
        private WorldRing minimumRing = WorldRing.Refuge;

        [SerializeField]
        [Tooltip("Outermost ring where this resource may appear (inclusive). Void is outside the playable disc by default - opt in explicitly.")]
        private WorldRing maximumRing = WorldRing.Edgelands;

        [Header("Selection")]
        [SerializeField]
        [Min(0f)]
        [Tooltip("Relative selection weight among compatible definitions (0 excludes)")]
        private float selectionWeight = 1f;

        [SerializeField]
        [Tooltip("Weight multiplier evaluated at the GLOBAL world depth (Depth01: 0 = Refuge, 1 = disc edge), multiplied with Selection Weight")]
        private AnimationCurve weightByDepth = AnimationCurve.Constant(0f, 1f, 1f);

        [Header("Progression Metadata (V1: authoring/debug only, not yet consumed by balance)")]
        [SerializeField]
        [Min(0f)]
        private float lootValue = 1f;

        public string ResourceId => string.IsNullOrEmpty(resourceId) ? name : resourceId;
        public ItemDefinition Item => item;
        public int MinimumQuantity => Mathf.Min(minimumQuantity, maximumQuantity);
        public int MaximumQuantity => Mathf.Max(minimumQuantity, maximumQuantity);
        public WorldRing MinimumRing => minimumRing;
        public WorldRing MaximumRing => maximumRing;
        public float SelectionWeight => selectionWeight;
        public AnimationCurve WeightByDepth => weightByDepth;
        public float LootValue => lootValue;
    }
}

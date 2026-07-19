using UnityEngine;
using Lootbound.Gameplay.World.Layout;

namespace Lootbound.Gameplay.World.Spawning
{
    /// <summary>
    /// Immutable definition of a landmark that can occupy a LandmarkReservation.
    /// V1 landmarks are visual markers only. When no prefab is assigned, the
    /// spawner creates a clearly named placeholder primitive; the placeholder
    /// path is encapsulated in the spawner and must not become a permanent
    /// debug dependency.
    /// </summary>
    [CreateAssetMenu(fileName = "Landmark_", menuName = "Lootbound/World Content/Landmark Definition")]
    public class LandmarkDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField]
        [Tooltip("Stable ID. Falls back to the asset name when empty.")]
        private string landmarkId;

        [SerializeField]
        private string displayName;

        [Header("Content")]
        [SerializeField]
        [Tooltip("Optional prefab. When null, the spawner uses a clearly named placeholder primitive.")]
        private GameObject landmarkPrefab;

        [SerializeField]
        [Tooltip("Innermost ring where this landmark may appear (inclusive)")]
        private WorldRing minimumRing = WorldRing.Refuge;

        [SerializeField]
        [Tooltip("Outermost ring where this landmark may appear (inclusive). Void is outside the playable disc by default - opt in explicitly.")]
        private WorldRing maximumRing = WorldRing.Edgelands;

        [Header("Selection")]
        [SerializeField]
        [Min(0f)]
        [Tooltip("Relative selection weight among compatible definitions (0 excludes)")]
        private float selectionWeight = 1f;

        [SerializeField]
        [Tooltip("Weight multiplier evaluated at the GLOBAL world depth (Depth01: 0 = Refuge, 1 = disc edge), multiplied with Selection Weight")]
        private AnimationCurve weightByDepth = AnimationCurve.Constant(0f, 1f, 1f);

        public string LandmarkId => string.IsNullOrEmpty(landmarkId) ? name : landmarkId;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
        public GameObject LandmarkPrefab => landmarkPrefab;
        public WorldRing MinimumRing => minimumRing;
        public WorldRing MaximumRing => maximumRing;
        public float SelectionWeight => selectionWeight;
        public AnimationCurve WeightByDepth => weightByDepth;
    }
}

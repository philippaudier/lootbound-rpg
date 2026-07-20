using UnityEngine;
using Lootbound.Gameplay.World.Layout;

namespace Lootbound.Gameplay.World.Spawning
{
    /// <summary>
    /// Immutable definition of a landmark archetype - a permanent, notable
    /// place of the world (a windmill, a shrine, a ruined tower...). Since
    /// slice 0.9.10 landmarks are a first-class World system, not transient
    /// spawn content: the LandmarkPlanner turns eligible layout nodes into
    /// LandmarkIdentity records, and the presentation layer renders them.
    ///
    /// V1 keeps the visual prefab on the definition (one archetype = one
    /// prefab). A LandmarkVisualLibrary would only be justified once an
    /// archetype needs several visual variants.
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
        [Tooltip("Optional prefab. When null, the presenter shows nothing (or a development silhouette when explicitly enabled).")]
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

        [Header("Discovery")]
        [SerializeField]
        [Min(1f)]
        [Tooltip("Distance (meters) at which this place counts as noticed/discovered. Belongs to the place itself; carried on the identity so future discovery/journal/map mechanics need no asset change.")]
        private float discoveryRadius = 100f;

        public string LandmarkId => string.IsNullOrEmpty(landmarkId) ? name : landmarkId;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
        public GameObject LandmarkPrefab => landmarkPrefab;
        public WorldRing MinimumRing => minimumRing;
        public WorldRing MaximumRing => maximumRing;
        public float SelectionWeight => selectionWeight;
        public AnimationCurve WeightByDepth => weightByDepth;
        public float DiscoveryRadius => Mathf.Max(1f, discoveryRadius);
    }
}

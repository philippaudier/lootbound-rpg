using UnityEngine;
using Lootbound.Gameplay.World.Layout;
using Lootbound.Gameplay.World.Landmarks;

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

        [Header("Terrain Integration")]
        [SerializeField]
        [Tooltip("How the terrain conforms to seat this landmark. None = the terrain is never modified.")]
        private LandmarkTerrainConformingMode conformingMode = LandmarkTerrainConformingMode.None;

        [SerializeField]
        [Tooltip("Foundation footprint. V1 implements Circle only; other values fall back to Circle with a one-time warning.")]
        private FoundationShape foundationShape = FoundationShape.Circle;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Radius (m) of the fully seated foundation. 0 disables terrain integration for this landmark.")]
        private float foundationRadius = 6f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Width (m) of the smooth transition ring beyond the foundation, blending back to the natural terrain.")]
        private float transitionRadius = 8f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Maximum downward correction (m). The seat never cuts deeper than this; steeper ground keeps a residual intersection rather than a cliff.")]
        private float maxCutDepth = 4f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Maximum upward correction (m). The seat never fills higher than this.")]
        private float maxFillHeight = 4f;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Fraction of the original relief kept inside the foundation (0 = flat seat, 1 = terrain untouched). No new noise - keeps a lived-in feel.")]
        private float residualRoughness = 0.15f;

        [SerializeField]
        [Tooltip("Authoring offset (m) applied to the seat height on the TERRAIN side (negative sinks the foundation). Not a presenter offset.")]
        private float verticalOffset = 0f;

        [SerializeField]
        [Tooltip("Overlap arbitration: when foundations overlap, the higher priority wins. Ties are broken by effective influence, then landmark id.")]
        private int foundationPriority = 0;

        public string LandmarkId => string.IsNullOrEmpty(landmarkId) ? name : landmarkId;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
        public GameObject LandmarkPrefab => landmarkPrefab;
        public WorldRing MinimumRing => minimumRing;
        public WorldRing MaximumRing => maximumRing;
        public float SelectionWeight => selectionWeight;
        public AnimationCurve WeightByDepth => weightByDepth;
        public float DiscoveryRadius => Mathf.Max(1f, discoveryRadius);

        public LandmarkTerrainConformingMode ConformingMode => conformingMode;
        public FoundationShape FoundationShape => foundationShape;
        public float FoundationRadius => Mathf.Max(0f, foundationRadius);
        public float TransitionRadius => Mathf.Max(0f, transitionRadius);
        public float MaxCutDepth => Mathf.Max(0f, maxCutDepth);
        public float MaxFillHeight => Mathf.Max(0f, maxFillHeight);
        public float ResidualRoughness => Mathf.Clamp01(residualRoughness);
        public float VerticalOffset => verticalOffset;
        public int FoundationPriority => foundationPriority;
    }
}

using UnityEngine;
using Lootbound.Gameplay.World.Layout;

namespace Lootbound.Gameplay.World.Population
{
    /// <summary>
    /// One kind of ambient presence (a creature type living freely in the
    /// world, independent of paths, nodes and reservations). Ring windows,
    /// weights and depth curves follow the exact 0.9.7 content rules
    /// (WorldContentCompatibility). No hardcoded hostility rule: the Refuge
    /// stays empty through ring windows, so future passive populations
    /// (animals, birds, NPCs) fit without special cases.
    /// </summary>
    [CreateAssetMenu(fileName = "Ambient_", menuName = "Lootbound/World Content/Ambient Population Definition")]
    public class AmbientPopulationDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField]
        [Tooltip("Stable ID. Falls back to the asset name when empty.")]
        private string populationId;

        [SerializeField]
        [Tooltip("Creature prefab (must carry EnemyBrain/EnemyHealth for hostile populations)")]
        private GameObject prefab;

        [Header("Ring Window (inclusive, like every content definition)")]
        [SerializeField] private WorldRing minimumRing = WorldRing.Nearlands;

        [SerializeField]
        [Tooltip("Void is outside the playable disc by default - opt in explicitly.")]
        private WorldRing maximumRing = WorldRing.Edgelands;

        [Header("Selection")]
        [SerializeField, Min(0f)] private float selectionWeight = 1f;

        [SerializeField]
        [Tooltip("Weight multiplier evaluated at the GLOBAL world depth (Depth01)")]
        private AnimationCurve weightByDepth = AnimationCurve.Constant(0f, 1f, 1f);

        [Header("Group")]
        [SerializeField, Range(1, 3)] private int minimumGroupSize = 1;
        [SerializeField, Range(1, 3)] private int maximumGroupSize = 2;

        [SerializeField, Min(1f)]
        [Tooltip("Members are placed within this radius around the resolved anchor")]
        private float groupRadius = 5f;

        [Header("Terrain")]
        [SerializeField, Range(0f, 60f)] private float maximumSlope = 35f;

        [Header("Placement Rules")]
        [SerializeField, Min(0f)]
        [Tooltip("Additional refuge buffer for this population (0 = use the global config value only)")]
        private float minimumDistanceFromRefuge;

        [SerializeField, Min(0f)]
        [Tooltip("Exclusion radius around authored reservations (kept readable ambushes)")]
        private float minimumDistanceFromAuthoredContent = 25f;

        [SerializeField] private bool excludeNearEncounters = true;
        [SerializeField] private bool excludeNearLandmarks = true;

        [SerializeField]
        [Tooltip("A small resource should not empty a whole area of ambient life - off by default.")]
        private bool excludeNearResources;

        [Header("Budgets")]
        [SerializeField, Min(1)] private int maxAliveGlobally = 5;
        [SerializeField, Min(1)] private int maxAlivePerCell = 3;

        public string PopulationId => string.IsNullOrEmpty(populationId) ? name : populationId;
        public GameObject Prefab => prefab;
        public WorldRing MinimumRing => minimumRing;
        public WorldRing MaximumRing => maximumRing;
        public float SelectionWeight => selectionWeight;
        public AnimationCurve WeightByDepth => weightByDepth;
        public int MinimumGroupSize => Mathf.Min(minimumGroupSize, maximumGroupSize);
        public int MaximumGroupSize => Mathf.Max(minimumGroupSize, maximumGroupSize);
        public float GroupRadius => groupRadius;
        public float MaximumSlope => maximumSlope;
        public float MinimumDistanceFromRefuge => minimumDistanceFromRefuge;
        public float MinimumDistanceFromAuthoredContent => minimumDistanceFromAuthoredContent;
        public bool ExcludeNearEncounters => excludeNearEncounters;
        public bool ExcludeNearLandmarks => excludeNearLandmarks;
        public bool ExcludeNearResources => excludeNearResources;
        public int MaxAliveGlobally => maxAliveGlobally;
        public int MaxAlivePerCell => maxAlivePerCell;
    }
}

using UnityEngine;

namespace Lootbound.Gameplay.World.Layout
{
    /// <summary>
    /// Configuration for world layout generation.
    /// Controls radial path generation, branches, and terrain integration.
    /// Ring thresholds are configured separately in WorldRingConfig.
    /// </summary>
    [CreateAssetMenu(fileName = "WorldLayoutConfig", menuName = "Lootbound/World Layout Config")]
    public class WorldLayoutConfig : ScriptableObject
    {
        [Header("Generation Retries")]
        [Tooltip("Maximum number of generation attempts before failing")]
        [Range(1, 10)]
        [SerializeField] private int maxGenerationAttempts = 5;

        [Header("Radial Paths")]
        [Tooltip("Minimum number of radial paths from Refuge")]
        [Range(3, 8)]
        [SerializeField] private int minimumRadialPathCount = 4;

        [Tooltip("Maximum number of radial paths from Refuge")]
        [Range(3, 10)]
        [SerializeField] private int maximumRadialPathCount = 6;

        [Tooltip("Number of nodes per radial path (excluding Refuge)")]
        [Range(3, 10)]
        [SerializeField] private int nodesPerRadialPath = 6;

        [Tooltip("Minimum distance between path nodes in meters")]
        [SerializeField] private float radialStepMin = 60f;

        [Tooltip("Maximum distance between path nodes in meters")]
        [SerializeField] private float radialStepMax = 120f;

        [Header("Angular Distribution")]
        [Tooltip("Maximum angular gap between adjacent radial paths (degrees)")]
        [Range(45f, 120f)]
        [SerializeField] private float maxAngularGap = 90f;

        [Tooltip("Minimum angular separation between radial paths (degrees)")]
        [Range(20f, 60f)]
        [SerializeField] private float minimumAngularSeparation = 30f;

        [Header("Candidate Scoring")]
        [Tooltip("Weight for outward progression in candidate scoring")]
        [Range(0f, 2f)]
        [SerializeField] private float outwardProgressionWeight = 1.0f;

        [Tooltip("Weight for terrain slope in candidate scoring")]
        [Range(0f, 2f)]
        [SerializeField] private float terrainSlopeWeight = 1.0f;

        [Tooltip("Penalty weight for sharp turns (curvature) in candidate scoring")]
        [Range(0f, 1f)]
        [SerializeField] private float curvaturePenaltyWeight = 0.3f;

        [Header("Refuge Placement")]
        [Tooltip("Maximum offset from world center for Refuge placement")]
        [SerializeField] private float refugeMaxCenterOffset = 50f;

        [Tooltip("Radius of the Refuge node area")]
        [SerializeField] private float refugeRadius = 24f;

        [Header("Traversability")]
        [Tooltip("Maximum traversable slope for radial paths (degrees)")]
        [Range(15f, 45f)]
        [SerializeField] private float radialPathMaxSlope = 35f;

        [Tooltip("Number of sample points along each edge for validation")]
        [Range(3, 10)]
        [SerializeField] private int edgeSamplePoints = 5;

        [Header("Branches")]
        [Tooltip("Number of secondary branches from path nodes")]
        [Range(0, 6)]
        [SerializeField] private int branchCount = 3;

        [Tooltip("Maximum nodes per branch")]
        [Range(1, 5)]
        [SerializeField] private int branchMaxNodes = 3;

        [Tooltip("Chance that each eligible node spawns a branch")]
        [Range(0f, 1f)]
        [SerializeField] private float branchChance = 0.4f;

        [Header("Points of Interest")]
        [Tooltip("Number of clearings to generate")]
        [Range(0, 8)]
        [SerializeField] private int clearingCount = 4;

        [Tooltip("Number of viewpoints to generate")]
        [Range(0, 6)]
        [SerializeField] private int viewpointCount = 3;

        [Tooltip("Number of encounter reservations")]
        [Range(0, 10)]
        [SerializeField] private int encounterReservationCount = 5;

        [Tooltip("Number of resource reservations")]
        [Range(0, 10)]
        [SerializeField] private int resourceReservationCount = 4;

        [Header("Node Sizing")]
        [Tooltip("Default radius for junction nodes")]
        [SerializeField] private float junctionRadius = 8f;

        [Tooltip("Default radius for clearing nodes")]
        [SerializeField] private float clearingRadius = 15f;

        [Tooltip("Default radius for viewpoint nodes")]
        [SerializeField] private float viewpointRadius = 6f;

        [Tooltip("Radius for outer destination nodes")]
        [SerializeField] private float outerDestinationRadius = 20f;

        [Header("Terrain Correction")]
        [Tooltip("Width of path corridor in meters")]
        [SerializeField] private float corridorWidth = 8f;

        [Tooltip("Blend distance for corridor edges in meters")]
        [SerializeField] private float corridorBlend = 12f;

        [Tooltip("Maximum vertical correction strength (0-1)")]
        [Range(0f, 0.3f)]
        [SerializeField] private float maxCorrectionStrength = 0.15f;

        [Tooltip("Radius for refuge flattening in meters")]
        [SerializeField] private float refugeFlattenRadius = 20f;

        [Tooltip("Radius for clearing flattening in meters")]
        [SerializeField] private float clearingFlattenRadius = 12f;

        [Header("Spacing")]
        [Tooltip("Minimum spacing between nodes in meters")]
        [SerializeField] private float nodeMinSpacing = 40f;

        [Tooltip("Number of candidate positions evaluated per path step")]
        [Range(6, 20)]
        [SerializeField] private int candidatesPerStep = 12;

        #region Properties

        // Generation
        public int MaxGenerationAttempts => maxGenerationAttempts;

        // Radial Paths
        public int MinimumRadialPathCount => minimumRadialPathCount;
        public int MaximumRadialPathCount => maximumRadialPathCount;
        public int NodesPerRadialPath => nodesPerRadialPath;
        public float RadialStepMin => radialStepMin;
        public float RadialStepMax => radialStepMax;

        // Angular Distribution
        public float MaxAngularGap => maxAngularGap;
        public float MinimumAngularSeparation => minimumAngularSeparation;

        // Candidate Scoring
        public float OutwardProgressionWeight => outwardProgressionWeight;
        public float TerrainSlopeWeight => terrainSlopeWeight;
        public float CurvaturePenaltyWeight => curvaturePenaltyWeight;

        // Refuge
        public float RefugeMaxCenterOffset => refugeMaxCenterOffset;
        public float RefugeRadius => refugeRadius;

        // Traversability
        public float RadialPathMaxSlope => radialPathMaxSlope;
        public float PrimaryPathMaxSlope => radialPathMaxSlope; // Alias for consistency
        public int EdgeSamplePoints => edgeSamplePoints;

        // Branches
        public int BranchCount => branchCount;
        public int BranchMaxNodes => branchMaxNodes;
        public float BranchChance => branchChance;

        // Points of Interest
        public int ClearingCount => clearingCount;
        public int ViewpointCount => viewpointCount;
        public int EncounterReservationCount => encounterReservationCount;
        public int ResourceReservationCount => resourceReservationCount;

        // Node Sizing
        public float JunctionRadius => junctionRadius;
        public float ClearingRadiusSize => clearingRadius;
        public float ViewpointRadius => viewpointRadius;
        public float OuterDestinationRadius => outerDestinationRadius;

        // Terrain Correction
        public float CorridorWidth => corridorWidth;
        public float CorridorBlend => corridorBlend;
        public float MaxCorrectionStrength => maxCorrectionStrength;
        public float RefugeFlattenRadius => refugeFlattenRadius;
        public float ClearingFlattenRadius => clearingFlattenRadius;

        // Spacing
        public float NodeMinSpacing => nodeMinSpacing;
        public int CandidatesPerStep => candidatesPerStep;

        #endregion

        /// <summary>
        /// Get the radius for a given node type.
        /// </summary>
        public float GetRadiusForType(WorldNodeType type)
        {
            return type switch
            {
                WorldNodeType.Refuge => refugeRadius,
                WorldNodeType.Junction => junctionRadius,
                WorldNodeType.Clearing => clearingRadius,
                WorldNodeType.Viewpoint => viewpointRadius,
                WorldNodeType.Landmark => junctionRadius,
                WorldNodeType.OuterDestination => outerDestinationRadius,
                WorldNodeType.DeadEnd => junctionRadius,
                _ => junctionRadius
            };
        }

        private void OnValidate()
        {
            // Ensure min <= max
            minimumRadialPathCount = Mathf.Min(minimumRadialPathCount, maximumRadialPathCount);
            radialStepMin = Mathf.Max(20f, radialStepMin);
            radialStepMax = Mathf.Max(radialStepMin + 10f, radialStepMax);

            // Minimum sensible values
            nodeMinSpacing = Mathf.Max(10f, nodeMinSpacing);
            corridorWidth = Mathf.Max(2f, corridorWidth);
            corridorBlend = Mathf.Max(2f, corridorBlend);
            refugeFlattenRadius = Mathf.Max(5f, refugeFlattenRadius);
            clearingFlattenRadius = Mathf.Max(3f, clearingFlattenRadius);

            // Angular constraints
            minimumAngularSeparation = Mathf.Min(minimumAngularSeparation, maxAngularGap * 0.8f);
        }
    }
}

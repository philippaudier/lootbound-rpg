using System.Collections.Generic;
using UnityEngine;

namespace Lootbound.Gameplay.World.Population
{
    /// <summary>
    /// Global settings and definition catalogue for the ambient population.
    /// Defaults are V1 starting points, meant to be tuned to the real world
    /// scale. Constraint: spawnRadiusMin &lt; spawnRadiusMax &lt; despawnRadius -
    /// the despawn ring is far enough that stepping back a few meters never
    /// makes a creature visibly vanish.
    /// </summary>
    [CreateAssetMenu(fileName = "AmbientPopulationConfig", menuName = "Lootbound/World/Ambient Population Config")]
    public class AmbientPopulationConfig : ScriptableObject
    {
        [Header("Definitions")]
        [SerializeField] private List<AmbientPopulationDefinition> definitions = new List<AmbientPopulationDefinition>();

        [Header("Cells")]
        [SerializeField, Range(24f, 96f)]
        [Tooltip("Logical cell size in meters (relative to the WorldDisc center)")]
        private float cellSize = 48f;

        [SerializeField, Range(1, 4)]
        [Tooltip("Maximum ambient anchors planned per cell (sobriety first)")]
        private int maxPlansPerCell = 2;

        [SerializeField, Range(2, 12)]
        [Tooltip("Stable candidate positions generated per anchor - one unlucky NavMesh point never condemns a cell")]
        private int candidatesPerAnchor = 6;

        [Header("Activation")]
        [SerializeField, Range(0.25f, 10f)] private float evaluationInterval = 1.5f;
        [SerializeField, Range(1, 10)] private int maxCellActivationsPerTick = 3;
        [SerializeField, Range(1, 12)] private int spawnAttemptsPerTick = 4;

        [Header("Distances")]
        [SerializeField, Min(0f)] private float spawnRadiusMin = 35f;
        [SerializeField, Min(0f)] private float spawnRadiusMax = 100f;
        [SerializeField, Min(0f)] private float despawnRadius = 150f;
        [SerializeField, Min(0f)] private float minimumDistanceFromPlayer = 30f;
        [SerializeField, Min(0f)] private float minimumDistanceBetweenIndividuals = 10f;
        [SerializeField, Min(0f)] private float minimumDistanceFromRefuge = 20f;

        [Header("Budgets")]
        [SerializeField, Min(1)] private int globalPopulationBudget = 20;

        [Header("Despawn")]
        [SerializeField, Range(1f, 60f)]
        [Tooltip("Grace period beyond the despawn radius before an instance is unloaded")]
        private float despawnGraceDuration = 10f;

        [Header("Death")]
        [SerializeField]
        [Tooltip("V1: defeated presences never return during the session (preferable to a five-minute resurrection behind the same rock)")]
        private bool respawnAfterDeath;

        [SerializeField, Min(30f)]
        [Tooltip("Cooldown before a defeated presence may return (only when respawnAfterDeath is enabled)")]
        private float respawnCooldown = 600f;

        [Header("Anti-pop")]
        [SerializeField]
        [Tooltip("Reject spawn points inside the camera frustum")]
        private bool rejectInsideCameraFrustum = true;

        [SerializeField, Min(0f)]
        [Tooltip("Frustum rejection only applies within this distance of the player: a far spawn in view is imperceptible. MinimumDistanceFromPlayer remains the absolute protection.")]
        private float visibleSpawnProtectionDistance = 55f;

        [SerializeField]
        [Tooltip("Optional extra line-of-sight raycast rejection (off by default)")]
        private bool useLineOfSightRejection;

        public IReadOnlyList<AmbientPopulationDefinition> Definitions => definitions;
        public float CellSize => cellSize;
        public int MaxPlansPerCell => maxPlansPerCell;
        public int CandidatesPerAnchor => candidatesPerAnchor;
        public float EvaluationInterval => evaluationInterval;
        public int MaxCellActivationsPerTick => maxCellActivationsPerTick;
        public int SpawnAttemptsPerTick => spawnAttemptsPerTick;
        public float SpawnRadiusMin => spawnRadiusMin;
        public float SpawnRadiusMax => spawnRadiusMax;
        public float DespawnRadius => despawnRadius;
        public float MinimumDistanceFromPlayer => minimumDistanceFromPlayer;
        public float MinimumDistanceBetweenIndividuals => minimumDistanceBetweenIndividuals;
        public float MinimumDistanceFromRefuge => minimumDistanceFromRefuge;
        public int GlobalPopulationBudget => globalPopulationBudget;
        public float DespawnGraceDuration => despawnGraceDuration;
        public bool RespawnAfterDeath => respawnAfterDeath;
        public float RespawnCooldown => respawnCooldown;
        public bool RejectInsideCameraFrustum => rejectInsideCameraFrustum;
        public float VisibleSpawnProtectionDistance => visibleSpawnProtectionDistance;
        public bool UseLineOfSightRejection => useLineOfSightRejection;

        private void OnValidate()
        {
            spawnRadiusMax = Mathf.Max(spawnRadiusMax, spawnRadiusMin + 1f);
            despawnRadius = Mathf.Max(despawnRadius, spawnRadiusMax + 10f);
            minimumDistanceFromPlayer = Mathf.Min(minimumDistanceFromPlayer, spawnRadiusMin);
            visibleSpawnProtectionDistance = Mathf.Max(visibleSpawnProtectionDistance, minimumDistanceFromPlayer);
        }
    }
}

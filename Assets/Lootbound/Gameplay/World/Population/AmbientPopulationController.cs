using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Lootbound.Core.Logging;
using Lootbound.Gameplay.Combat;
using Lootbound.Gameplay.World.Layout;
using Lootbound.Gameplay.World.Navigation;
using Lootbound.Gameplay.World.Progression;
using Lootbound.Gameplay.World.Spawning;

namespace Lootbound.Gameplay.World.Population
{
    /// <summary>
    /// One recent spawn rejection, kept for the F7 debug panel.
    /// </summary>
    public readonly struct AmbientRejectionRecord
    {
        public AmbientSpawnRejectionReason Reason { get; }
        public AmbientSpawnRejectionKind Kind { get; }
        public Vector3 Position { get; }
        public float Time { get; }

        public AmbientRejectionRecord(AmbientSpawnRejectionReason reason, AmbientSpawnRejectionKind kind, Vector3 position, float time)
        {
            Reason = reason;
            Kind = kind;
            Position = position;
            Time = time;
        }
    }

    /// <summary>
    /// Orchestrates the ambient population: waits for the published terrain
    /// and its matching runtime navigation (NavigationContentGate), plans
    /// cells around the player, materializes intentions under the anti-pop
    /// rules and budgets, streams far instances out with a grace period, and
    /// purges everything the moment a generation becomes invalid.
    ///
    /// It never chooses species (planner), never judges a point (validator),
    /// never touches instances directly (spawner) and never counts by itself
    /// (registry).
    /// </summary>
    public sealed class AmbientPopulationController : MonoBehaviour
    {
        private const string LogCategory = "AmbientPopulation";
        private const int MaxRecentRejections = 12;

        [Header("Sources")]
        [SerializeField] private ProceduralTerrainGenerator terrainGenerator;
        [SerializeField] private RuntimeNavigationBuilder navigationBuilder;
        [SerializeField] private Transform player;

        [Header("Configuration")]
        [SerializeField] private AmbientPopulationConfig config;

        private readonly NavigationContentGate gate = new NavigationContentGate();
        private readonly AmbientPopulationRegistry registry = new AmbientPopulationRegistry();
        private readonly Dictionary<string, (AmbientPopulationRegistry.PlanRecord record, AmbientPopulationInstance instance)> members =
            new Dictionary<string, (AmbientPopulationRegistry.PlanRecord, AmbientPopulationInstance)>();
        private readonly Dictionary<string, AmbientPopulationDefinition> definitionsById =
            new Dictionary<string, AmbientPopulationDefinition>();
        private readonly List<AmbientRejectionRecord> recentRejections = new List<AmbientRejectionRecord>();
        private readonly List<Vector2Int> cellScratch = new List<Vector2Int>();
        private readonly List<AmbientPopulationInstance> despawnScratch = new List<AmbientPopulationInstance>();

        private TerrainGenerationContext pendingContext;
        private WorldProgression progression;
        private ITerrainSampler terrainSampler;
        private List<Vector3> encounterPositions;
        private List<Vector3> landmarkPositions;
        private List<Vector3> resourcePositions;
        private GameObject root;
        private AmbientPopulationSpawner spawner;
        private int worldSeed;
        private bool active;
        private float nextTickAt;

        #region Debug API (F7)

        public AmbientPopulationRegistry Registry => registry;
        public bool IsActive => active;
        public int GlobalBudget => config != null ? config.GlobalPopulationBudget : 0;
        public IReadOnlyList<AmbientRejectionRecord> RecentRejections => recentRejections;

        public bool TryGetCurrentCellInfo(out Vector2Int cell, out int cellSeed, out WorldRingContext context)
        {
            cell = default;
            cellSeed = 0;
            context = default;
            if (!active || player == null || progression == null) return false;

            cell = AmbientPopulationCells.WorldToCell(player.position, progression.RefugePosition, config.CellSize);
            cellSeed = AmbientPopulationIds.CellSeed(worldSeed, cell);
            context = progression.GetContext(player.position);
            return true;
        }

        #endregion

        #region Lifecycle

        private void OnEnable()
        {
            if (terrainGenerator != null)
            {
                terrainGenerator.OnGenerationComplete += HandleGenerationComplete;
            }

            if (navigationBuilder != null)
            {
                navigationBuilder.OnNavigationCompleted += HandleNavigationCompleted;
            }
        }

        private void OnDisable()
        {
            if (terrainGenerator != null)
            {
                terrainGenerator.OnGenerationComplete -= HandleGenerationComplete;
            }

            if (navigationBuilder != null)
            {
                navigationBuilder.OnNavigationCompleted -= HandleNavigationCompleted;
            }
        }

        private void Start()
        {
            // Catch-up: generation and navigation may both have completed
            // before this component subscribed.
            if (terrainGenerator != null && terrainGenerator.IsGenerated && !active)
            {
                HandleGenerationComplete(terrainGenerator.Context);
            }
        }

        private void OnDestroy()
        {
            // Full cleanup: no root, instance, subscription or memory may
            // outlive its controller.
            PurgeAll();
        }

        private void Update()
        {
            if (!active)
            {
                return;
            }

            float now = Time.time;
            if (now < nextTickAt)
            {
                return;
            }
            nextTickAt = now + config.EvaluationInterval;

            Tick(now);
        }

        #endregion

        #region Generation lifecycle

        private void HandleGenerationComplete(TerrainGenerationContext generationContext)
        {
            if (!Application.isPlaying)
            {
                return;
            }

            // Earliest available invalidation signal: the old population is
            // invalid the moment a new generation is published.
            PurgeAll();

            if (generationContext == null || generationContext.LayoutContext == null || config == null)
            {
                return;
            }

            pendingContext = generationContext;
            gate.TerrainPublished(generationContext.GenerationId);

            // The navigation may already have been built for this generation
            // (subscriber order independence - same pattern as the spawner).
            if (navigationBuilder != null && navigationBuilder.LastResult != null &&
                navigationBuilder.LastResult.GenerationId == generationContext.GenerationId)
            {
                HandleNavigationCompleted(navigationBuilder.LastResult);
            }
        }

        private void HandleNavigationCompleted(RuntimeNavigationBuildResult result)
        {
            if (!Application.isPlaying)
            {
                return;
            }

            var decision = gate.NavigationCompleted(result.GenerationId, result.Success);
            switch (decision)
            {
                case NavigationGateDecision.Ignore:
                    return;

                case NavigationGateDecision.SpawnAll:
                    BeginPopulation(pendingContext);
                    break;

                case NavigationGateDecision.SpawnWithoutEncounters:
                    LootboundLog.Warning(LogCategory,
                        "Navigation build failed - ambient population stays dormant for this generation");
                    break;
            }

            pendingContext = null;
        }

        /// <summary>
        /// Start populating a published, navigation-ready generation.
        /// Public for targeted PlayMode tests; production drives it through
        /// the generation/navigation events.
        /// </summary>
        public void BeginPopulation(TerrainGenerationContext generationContext)
        {
            if (generationContext?.LayoutContext?.Progression == null || config == null)
            {
                return;
            }

            var layout = generationContext.LayoutContext;
            progression = layout.Progression;
            worldSeed = layout.WorldSeed;
            terrainSampler = new TerrainContextSampler(generationContext);
            CollectAuthoredPositions(layout);

            definitionsById.Clear();
            foreach (var definition in config.Definitions)
            {
                if (definition != null)
                {
                    definitionsById[definition.PopulationId] = definition;
                }
            }

            root = new GameObject("AmbientPopulation_Spawned");
            spawner = new AmbientPopulationSpawner(registry, root.transform, worldSeed);
            active = true;
            nextTickAt = 0f;

            // Initial pass: PLAN every relevant cell, but MATERIALIZE only
            // what passes the full anti-pop rules - a nearby cell may hold an
            // intention without a creature popping next to the player.
            float now = Time.time;
            Vector3 anchor = PlayerAnchor();
            AmbientPopulationCells.CollectCellsInRadius(
                anchor, config.SpawnRadiusMax, progression.RefugePosition, config.CellSize, cellScratch);
            foreach (var cell in cellScratch)
            {
                PlanCellIfNeeded(cell);
            }
            ExecutePendingPlans(now, anchor, int.MaxValue);

            LootboundLog.Info(LogCategory,
                $"Ambient population initialized: {registry.PlannedCellCount} cells planned, {registry.TotalAlive} alive");
        }

        private void CollectAuthoredPositions(WorldLayoutContext layout)
        {
            encounterPositions = new List<Vector3>();
            foreach (var reservation in layout.EncounterReservations) encounterPositions.Add(reservation.Position);

            landmarkPositions = new List<Vector3>();
            foreach (var reservation in layout.LandmarkReservations) landmarkPositions.Add(reservation.Position);

            resourcePositions = new List<Vector3>();
            foreach (var reservation in layout.ResourceReservations) resourcePositions.Add(reservation.Position);
        }

        /// <summary>
        /// Full purge: every previous instance, plan, snapshot and stat is
        /// invalid the moment the world changes.
        /// </summary>
        public void PurgeAll()
        {
            foreach (var (_, instance) in members.Values)
            {
                AmbientPopulationSpawner.DestroyForPurge(instance);
            }
            members.Clear();
            registry.Clear();
            recentRejections.Clear();
            gate.Reset();
            active = false;
            progression = null;
            spawner = null;

            if (root != null)
            {
                Destroy(root);
                root = null;
            }
        }

        #endregion

        #region Ticking

        private Vector3 PlayerAnchor()
        {
            return player != null ? player.position : progression.RefugePosition;
        }

        private void Tick(float now)
        {
            Vector3 anchor = PlayerAnchor();

            // 1. Progressive planning of unplanned cells in range
            AmbientPopulationCells.CollectCellsInRadius(
                anchor, config.SpawnRadiusMax, progression.RefugePosition, config.CellSize, cellScratch);
            int planned = 0;
            foreach (var cell in cellScratch)
            {
                if (planned >= config.MaxCellActivationsPerTick) break;
                if (PlanCellIfNeeded(cell)) planned++;
            }

            // 2. Bounded materialization of pending intentions
            ExecutePendingPlans(now, anchor, config.SpawnAttemptsPerTick);

            // 3. Streaming despawn with grace
            DespawnPass(now, anchor);
        }

        private bool PlanCellIfNeeded(Vector2Int cell)
        {
            if (registry.IsCellPlanned(cell))
            {
                return false;
            }

            var plans = AmbientPopulationPlanner.PlanCell(cell, worldSeed, progression, config);
            registry.StoreCellPlans(cell, plans);
            return true;
        }

        private void ExecutePendingPlans(float now, Vector3 anchor, int maxAttempts)
        {
            var validationContext = BuildValidationContext(anchor);
            int attempts = 0;

            foreach (var cell in cellScratch)
            {
                var records = registry.GetCellPlans(cell);
                if (records == null) continue;

                foreach (var record in records)
                {
                    if (attempts >= maxAttempts) return;
                    if (record.State != AmbientPlanState.Pending) continue;

                    attempts++;
                    TryExecutePlan(record, validationContext, now);
                }
            }
        }

        private void TryExecutePlan(
            AmbientPopulationRegistry.PlanRecord record,
            AmbientSpawnValidationContext validationContext,
            float now)
        {
            if (!definitionsById.TryGetValue(record.Plan.PopulationId, out var definition))
            {
                registry.MarkPlanStructurallyDead(record);
                return;
            }

            // Budgets first: always transient, always explainable.
            if (registry.TotalAlive >= config.GlobalPopulationBudget)
            {
                RecordRejection(AmbientSpawnRejectionReason.GlobalBudgetReached, record.Plan.CandidatePositions[0], now);
                return;
            }

            if (registry.AliveCount(definition.PopulationId) >= definition.MaxAliveGlobally)
            {
                RecordRejection(AmbientSpawnRejectionReason.DefinitionBudgetReached, record.Plan.CandidatePositions[0], now);
                return;
            }

            if (registry.AliveInCell(record.Plan.CellCoordinate) >= definition.MaxAlivePerCell)
            {
                RecordRejection(AmbientSpawnRejectionReason.CellBudgetReached, record.Plan.CandidatePositions[0], now);
                return;
            }

            var result = AmbientSpawnValidator.Validate(record.Plan, definition, validationContext);
            if (!result.IsValid)
            {
                RecordRejection(result.RejectionReason, result.RequestedPosition, now);

                // Only a fully structural failure exhausts the intention;
                // transient rejections just mean "not now".
                if (result.RejectionKind == AmbientSpawnRejectionKind.Structural)
                {
                    registry.MarkPlanStructurallyDead(record);
                }
                return;
            }

            var spawned = spawner.SpawnPlan(record, definition, result.ResolvedPosition, SampleNavMesh, now, OnMemberDied);
            foreach (var instance in spawned)
            {
                members[instance.MemberId] = (record, instance);
            }
        }

        private AmbientSpawnValidationContext BuildValidationContext(Vector3 anchor)
        {
            Plane[] frustum = null;
            if (config.RejectInsideCameraFrustum)
            {
                var camera = Camera.main;
                if (camera != null)
                {
                    frustum = GeometryUtility.CalculateFrustumPlanes(camera);
                }
            }

            return new AmbientSpawnValidationContext
            {
                Progression = progression,
                TerrainSampler = terrainSampler,
                SampleNavMesh = SampleNavMesh,
                Registry = registry,
                Config = config,
                PlayerPosition = player != null ? player.position : (Vector3?)null,
                FrustumPlanes = frustum,
                EncounterPositions = encounterPositions,
                LandmarkPositions = landmarkPositions,
                ResourcePositions = resourcePositions
            };
        }

        private void DespawnPass(float now, Vector3 anchor)
        {
            despawnScratch.Clear();

            foreach (var (record, instance) in members.Values)
            {
                if (instance.GameObject == null || (instance.Health != null && instance.Health.IsDead))
                {
                    continue;
                }

                bool farEnough = Vector3.Distance(instance.Position, anchor) > config.DespawnRadius;
                bool peaceful = IsPeaceful(instance.Brain);

                if (!farEnough || !peaceful)
                {
                    instance.DespawnGraceStartedAt = -1f;
                    continue;
                }

                if (instance.DespawnGraceStartedAt < 0f)
                {
                    instance.DespawnGraceStartedAt = now;
                    continue;
                }

                if (now - instance.DespawnGraceStartedAt >= config.DespawnGraceDuration)
                {
                    despawnScratch.Add(instance);
                }
            }

            foreach (var instance in despawnScratch)
            {
                if (members.TryGetValue(instance.MemberId, out var entry))
                {
                    spawner.Despawn(entry.record, entry.instance);
                    members.Remove(instance.MemberId);
                }
            }
        }

        /// <summary>
        /// A fought creature never disappears because the player stepped back:
        /// any alerted, chasing, attacking or staggered state blocks despawn.
        /// </summary>
        private static bool IsPeaceful(EnemyBrain brain)
        {
            if (brain == null) return true;

            switch (brain.CurrentState)
            {
                case EnemyState.Idle:
                case EnemyState.Wandering:
                case EnemyState.Patrolling:
                case EnemyState.ReturningHome:
                    return true;
                default:
                    return false;
            }
        }

        private void OnMemberDied(AmbientPopulationInstance instance)
        {
            if (!members.TryGetValue(instance.MemberId, out var entry))
            {
                return;
            }

            instance.UnsubscribeDeath();
            registry.MarkMemberDead(entry.record, instance);
            members.Remove(instance.MemberId);
            // The corpse (and its loot) stays in the world like any enemy.
        }

        private void RecordRejection(AmbientSpawnRejectionReason reason, Vector3 position, float now)
        {
            recentRejections.Add(new AmbientRejectionRecord(reason, AmbientSpawnRejection.KindOf(reason), position, now));
            if (recentRejections.Count > MaxRecentRejections)
            {
                recentRejections.RemoveAt(0);
            }
        }

        private static bool SampleNavMesh(Vector3 position, float maxDistance, out Vector3 sampled)
        {
            if (NavMesh.SamplePosition(position, out NavMeshHit hit, maxDistance, NavMesh.AllAreas))
            {
                sampled = hit.position;
                return true;
            }

            sampled = default;
            return false;
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmosSelected()
        {
            if (!active || progression == null || config == null)
            {
                return;
            }

            Vector3 anchor = PlayerAnchor();

            // Radii
            DrawCircleColored(anchor, config.SpawnRadiusMin, new Color(1f, 1f, 0.3f, 0.5f));
            DrawCircleColored(anchor, config.SpawnRadiusMax, new Color(0.3f, 1f, 0.5f, 0.5f));
            DrawCircleColored(anchor, config.DespawnRadius, new Color(1f, 0.4f, 0.2f, 0.4f));

            // Cell grid with states
            AmbientPopulationCells.CollectCellsInRadius(
                anchor, config.SpawnRadiusMax, progression.RefugePosition, config.CellSize, cellScratch);
            foreach (var cell in cellScratch)
            {
                var records = registry.GetCellPlans(cell);
                Color color = new Color(1f, 1f, 1f, 0.08f); // unplanned
                if (records != null)
                {
                    color = new Color(0.3f, 0.8f, 1f, 0.15f); // planned, empty
                    foreach (var record in records)
                    {
                        if (record.State == AmbientPlanState.Alive) color = new Color(0.2f, 1f, 0.3f, 0.25f);
                        else if (record.State == AmbientPlanState.Defeated) color = new Color(0.5f, 0.5f, 0.5f, 0.2f);
                        else if (record.State == AmbientPlanState.StructurallyDead) color = new Color(1f, 0.3f, 0.2f, 0.12f);
                    }
                }

                Gizmos.color = color;
                Vector3 center = AmbientPopulationCells.CellCenter(cell, progression.RefugePosition, config.CellSize);
                center.y = anchor.y;
                Gizmos.DrawCube(center, new Vector3(config.CellSize * 0.95f, 0.3f, config.CellSize * 0.95f));
            }

            // Alive homes and anchors
            Gizmos.color = Color.green;
            foreach (var (_, instance) in members.Values)
            {
                if (instance.Brain != null && instance.Brain.IsInitialized)
                {
                    Gizmos.DrawWireSphere(instance.Brain.HomePosition + Vector3.up * 0.3f, 0.5f);
                }
                Gizmos.DrawLine(instance.AnchorPosition, instance.Position + Vector3.up * 0.5f);
            }

            // Recent rejections
            foreach (var rejection in recentRejections)
            {
                Gizmos.color = rejection.Kind == AmbientSpawnRejectionKind.Structural
                    ? new Color(1f, 0.2f, 0.2f, 0.8f)
                    : new Color(1f, 0.9f, 0.2f, 0.8f);
                Gizmos.DrawWireCube(rejection.Position + Vector3.up * 0.5f, Vector3.one * 0.6f);
            }
        }

        private static void DrawCircleColored(Vector3 center, float radius, Color color, int segments = 48)
        {
            Gizmos.color = color;
            Vector3 previous = center + new Vector3(radius, 0f, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float angle = (i / (float)segments) * Mathf.PI * 2f;
                Vector3 next = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }

        #endregion
    }
}

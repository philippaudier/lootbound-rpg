using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Lootbound.Core.Logging;
using Lootbound.Gameplay.World.Layout;
using Lootbound.Gameplay.World.Navigation;

namespace Lootbound.Gameplay.World.Spawning
{
    /// <summary>
    /// Instantiates world content from the validated layout's reservations.
    ///
    /// Consumes ONLY the published, validated WorldLayoutContext. On
    /// OnGenerationComplete it clears the previous content immediately (so
    /// nothing stale contributes to the new NavMesh build) and waits; content
    /// is instantiated when RuntimeNavigationBuilder publishes the navigation
    /// result for that same generation (NavigationContentGate decides).
    ///
    /// All selection/placement decisions are delegated to WorldContentPlanner
    /// (pure C#, deterministic); navigation never changes the plan, only the
    /// moment of instantiation and the final physical position of entries
    /// that depend on it.
    ///
    /// Navigation is global for building the mesh but LOCAL for validation:
    /// each encounter entry is resolved individually via NavMesh.SamplePosition
    /// (bounded by navMeshSampleDistance); one failed entry never blocks the
    /// others. If the whole navigation build failed, resources and landmarks
    /// still spawn and encounters are rejected with an explicit reason.
    /// </summary>
    public sealed class WorldContentSpawner : MonoBehaviour
    {
        private const string LogCategory = "WorldContent";

        [Header("Source")]
        [SerializeField]
        [Tooltip("Generator whose OnGenerationComplete publishes the validated layout")]
        private ProceduralTerrainGenerator terrainGenerator;

        [SerializeField]
        [Tooltip("Runtime navigation builder gating encounter instantiation. If absent, content spawns immediately but encounters are rejected (no valid navigation can exist).")]
        private RuntimeNavigationBuilder navigationBuilder;

        [Header("Content Registries")]
        [SerializeField] private EncounterRegistry encounterRegistry;
        [SerializeField] private ResourceSpawnRegistry resourceRegistry;
        [SerializeField] private LandmarkRegistry landmarkRegistry;

        [Header("Categories")]
        [SerializeField] private bool spawnEncounters = true;
        [SerializeField] private bool spawnResources = true;
        [SerializeField] private bool spawnLandmarks = true;

        [Header("Placement")]
        [SerializeField]
        [Range(1f, 90f)]
        [Tooltip("Maximum terrain slope (degrees) accepted at a spawn position")]
        private float maxPlacementSlope = 40f;

        [SerializeField]
        [Range(0.5f, 20f)]
        [Tooltip("Maximum distance to search for baked NavMesh around an enemy spawn position")]
        private float navMeshSampleDistance = 4f;

        [SerializeField]
        [Range(0f, 2f)]
        [Tooltip("Vertical offset above the terrain for resource pickups. Matches the enemy loot spawn convention (EnemyHealth.lootSpawnOffset) so world resources sit at the same height as dropped items.")]
        private float resourceSpawnHeightOffset = 0.5f;

        private GameObject contentRoot;

        // Pure orchestration: which generation may spawn, and when.
        private readonly NavigationContentGate gate = new NavigationContentGate();

        // Context of the generation waiting for its navigation result.
        private TerrainGenerationContext pendingContext;

        /// <summary>Report of the last spawning pass, for the debug panel.</summary>
        public WorldContentSpawnReport LastReport { get; private set; }

        /// <summary>True while a generation is waiting for its navigation result.</summary>
        public bool IsWaitingForNavigation => gate.PendingGenerationId != null;

        public event System.Action<WorldContentSpawnReport> OnSpawnCompleted;

        private void OnEnable()
        {
            if (terrainGenerator == null)
            {
                LootboundLog.Error(LogCategory, "WorldContentSpawner has no ProceduralTerrainGenerator assigned");
                return;
            }

            terrainGenerator.OnGenerationComplete += HandleGenerationComplete;

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
            // Cover the case where generation (and possibly the navigation
            // build) finished before this component subscribed.
            if (terrainGenerator != null && terrainGenerator.IsGenerated && LastReport == null)
            {
                HandleGenerationComplete(terrainGenerator.Context);

                if (navigationBuilder != null && navigationBuilder.LastResult != null)
                {
                    HandleNavigationCompleted(navigationBuilder.LastResult);
                }
            }
        }

        private void HandleGenerationComplete(TerrainGenerationContext generationContext)
        {
            // Editor-mode generation (inspector buttons) must not pollute the scene.
            if (!Application.isPlaying)
            {
                return;
            }

            // Clear immediately so stale content cannot contribute to the
            // navigation build of the new generation.
            ClearSpawnedContent();
            pendingContext = null;
            gate.Reset();

            if (generationContext == null || generationContext.LayoutContext == null)
            {
                LootboundLog.Warning(LogCategory, "No validated layout available - skipping content spawning");
                LastReport = new WorldContentSpawnReport(0, new List<SpawnOutcome>(), new List<SpawnRejection>());
                OnSpawnCompleted?.Invoke(LastReport);
                return;
            }

            if (navigationBuilder == null)
            {
                // No navigation layer at all: spawn what does not depend on
                // it, reject encounters explicitly (never against a stale mesh).
                LootboundLog.Warning(LogCategory,
                    "No RuntimeNavigationBuilder assigned - spawning without encounters");
                SpawnContent(generationContext, encountersAllowed: false,
                    encounterRejectionDetail: "no RuntimeNavigationBuilder assigned");
                return;
            }

            pendingContext = generationContext;
            gate.TerrainPublished(generationContext.GenerationId);
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
                    SpawnContent(pendingContext, encountersAllowed: true, encounterRejectionDetail: null);
                    break;

                case NavigationGateDecision.SpawnWithoutEncounters:
                    LootboundLog.Warning(LogCategory,
                        $"Navigation build failed for generation {result.GenerationId} " +
                        $"({result.FailureReason}) - spawning without encounters");
                    SpawnContent(pendingContext, encountersAllowed: false,
                        encounterRejectionDetail: $"navigation build failed: {result.FailureReason}");
                    break;
            }

            pendingContext = null;
        }

        private void SpawnContent(TerrainGenerationContext generationContext, bool encountersAllowed, string encounterRejectionDetail)
        {
            var layout = generationContext.LayoutContext;
            var sampler = new TerrainContextSampler(generationContext);
            var settings = new WorldContentPlannerSettings
            {
                EncountersEnabled = spawnEncounters,
                ResourcesEnabled = spawnResources,
                LandmarksEnabled = spawnLandmarks,
                MaxPlacementSlope = maxPlacementSlope
            };

            var plan = WorldContentPlanner.Plan(
                layout.WorldSeed,
                layout.EncounterReservations,
                layout.ResourceReservations,
                layout.LandmarkReservations,
                encounterRegistry,
                resourceRegistry,
                landmarkRegistry,
                sampler,
                settings);

            contentRoot = new GameObject("WorldContent_Spawned");
            var encounterParent = CreateContainer("Encounters");
            var resourceParent = CreateContainer("Resources");
            var landmarkParent = CreateContainer("Landmarks");

            var outcomes = new List<SpawnOutcome>(plan.Recipes.Count);

            foreach (var recipe in plan.Recipes)
            {
                switch (recipe.Category)
                {
                    case WorldContentCategory.Encounter:
                        outcomes.Add(encountersAllowed
                            ? SpawnEncounter(recipe, encounterParent)
                            : new SpawnOutcome(recipe, 0,
                                $"{SpawnRejectionReason.NavMeshUnavailable} ({encounterRejectionDetail})"));
                        break;
                    case WorldContentCategory.Resource:
                        outcomes.Add(SpawnResource(recipe, resourceParent));
                        break;
                    case WorldContentCategory.Landmark:
                        outcomes.Add(SpawnLandmark(recipe, landmarkParent));
                        break;
                }
            }

            LastReport = new WorldContentSpawnReport(plan.TotalReservations, outcomes, plan.Rejections);

            LootboundLog.Info(LogCategory,
                $"Spawning pass (gen {generationContext.GenerationId}): {LastReport.ReservationsReceived} reservations, " +
                $"{LastReport.RecipesPlanned} recipes, {LastReport.SpawnsSucceeded} spawned, " +
                $"{LastReport.SpawnsRejected} rejected");

            foreach (var rejection in plan.Rejections)
            {
                LootboundLog.Info(LogCategory, $"Rejected: {rejection}");
            }

            OnSpawnCompleted?.Invoke(LastReport);
        }

        private void ClearSpawnedContent()
        {
            if (contentRoot != null)
            {
                Destroy(contentRoot);
                contentRoot = null;
            }
        }

        private Transform CreateContainer(string containerName)
        {
            var container = new GameObject(containerName);
            container.transform.SetParent(contentRoot.transform, false);
            return container.transform;
        }

        #region Encounter instantiation

        private SpawnOutcome SpawnEncounter(SpawnRecipe recipe, Transform parent)
        {
            var definition = FindEncounterDefinition(recipe.DefinitionId);
            if (definition == null || definition.EnemyPrefab == null)
            {
                LootboundLog.Warning(LogCategory, $"Encounter {recipe.ReservationId}: definition or prefab missing at spawn time");
                return new SpawnOutcome(recipe, 0, "definition or prefab missing");
            }

            var groupRoot = new GameObject($"Encounter_{recipe.DefinitionId}_{recipe.ReservationId}");
            groupRoot.transform.SetParent(parent, false);
            groupRoot.transform.position = recipe.AnchorPosition;
            AttachIdentity(groupRoot, recipe, "Group");

            int spawned = 0;
            int navMeshMisses = 0;
            var placements = new List<EntryPlacement>(recipe.Entries.Count);

            for (int i = 0; i < recipe.Entries.Count; i++)
            {
                var entry = recipe.Entries[i];

                // Local validation: each entry resolves its own navigable
                // position within the bounded tolerance. One failed entry
                // never blocks the others - reported, never masked. The
                // requested position is already on the final terrain; the
                // NavMesh only confirms navigability, it never repairs a
                // wrong height or teleports an enemy away from its reservation.
                if (!NavMesh.SamplePosition(entry.Position, out NavMeshHit hit, navMeshSampleDistance, NavMesh.AllAreas))
                {
                    navMeshMisses++;
                    placements.Add(new EntryPlacement(i, entry.Position, entry.Position, navMeshResolved: false));
                    continue;
                }

                placements.Add(new EntryPlacement(i, entry.Position, hit.position, navMeshResolved: true));

                float yaw = DeterministicYaw(recipe.ReservationId, i);
                var enemy = Instantiate(definition.EnemyPrefab, hit.position, Quaternion.Euler(0f, yaw, 0f), groupRoot.transform);
                enemy.name = $"{definition.EnemyPrefab.name}_{recipe.ReservationId}_{i}";
                AttachIdentity(enemy, recipe, entry.Role);

                var agent = enemy.GetComponent<NavMeshAgent>();
                if (agent != null)
                {
                    agent.Warp(hit.position);
                }

                spawned++;
            }

            if (spawned == 0)
            {
                Destroy(groupRoot);
                LootboundLog.Warning(LogCategory,
                    $"Encounter {recipe.ReservationId}: no NavMesh within {navMeshSampleDistance}m of any member position ({SpawnRejectionReason.NavMeshUnavailable})");
                return new SpawnOutcome(recipe, 0, SpawnRejectionReason.NavMeshUnavailable.ToString(), placements);
            }

            string detail = navMeshMisses > 0 ? $"{navMeshMisses} member(s) dropped: {SpawnRejectionReason.NavMeshUnavailable}" : null;
            return new SpawnOutcome(recipe, spawned, detail, placements);
        }

        private EncounterDefinition FindEncounterDefinition(string definitionId)
        {
            if (encounterRegistry == null) return null;
            foreach (var definition in encounterRegistry.Definitions)
            {
                if (definition != null && definition.EncounterId == definitionId)
                {
                    return definition;
                }
            }
            return null;
        }

        #endregion

        #region Resource instantiation

        private SpawnOutcome SpawnResource(SpawnRecipe recipe, Transform parent)
        {
            var definition = FindResourceDefinition(recipe.DefinitionId);
            if (definition == null || definition.Item == null)
            {
                LootboundLog.Warning(LogCategory, $"Resource {recipe.ReservationId}: definition or item missing at spawn time");
                return new SpawnOutcome(recipe, 0, "definition or item missing");
            }

            int spawned = 0;
            foreach (var entry in recipe.Entries)
            {
                Vector3 spawnPosition = entry.Position + Vector3.up * resourceSpawnHeightOffset;
                var pickup = ItemWorldPickup.SpawnPickup(definition.Item, spawnPosition, entry.Quantity);
                if (pickup == null)
                {
                    continue;
                }

                pickup.transform.SetParent(parent, true);
                AttachIdentity(pickup.gameObject, recipe, entry.Role);
                spawned++;
            }

            if (spawned == 0)
            {
                return new SpawnOutcome(recipe, 0, SpawnRejectionReason.InstantiationFailed.ToString());
            }

            return new SpawnOutcome(recipe, spawned);
        }

        private ResourceSpawnDefinition FindResourceDefinition(string definitionId)
        {
            if (resourceRegistry == null) return null;
            foreach (var definition in resourceRegistry.Definitions)
            {
                if (definition != null && definition.ResourceId == definitionId)
                {
                    return definition;
                }
            }
            return null;
        }

        #endregion

        #region Landmark instantiation

        private SpawnOutcome SpawnLandmark(SpawnRecipe recipe, Transform parent)
        {
            var definition = FindLandmarkDefinition(recipe.DefinitionId);
            if (definition == null)
            {
                LootboundLog.Warning(LogCategory, $"Landmark {recipe.ReservationId}: definition missing at spawn time");
                return new SpawnOutcome(recipe, 0, "definition missing");
            }

            var entry = recipe.Entries[0];
            GameObject landmark;

            if (definition.LandmarkPrefab != null)
            {
                landmark = Instantiate(definition.LandmarkPrefab, entry.Position, Quaternion.identity, parent);
                landmark.name = $"Landmark_{definition.LandmarkId}_{recipe.ReservationId}";
            }
            else
            {
                landmark = CreateLandmarkPlaceholder(entry.Position, definition.LandmarkId, recipe.ReservationId, parent);
            }

            AttachIdentity(landmark, recipe, entry.Role);
            return new SpawnOutcome(recipe, 1);
        }

        private LandmarkDefinition FindLandmarkDefinition(string definitionId)
        {
            if (landmarkRegistry == null) return null;
            foreach (var definition in landmarkRegistry.Definitions)
            {
                if (definition != null && definition.LandmarkId == definitionId)
                {
                    return definition;
                }
            }
            return null;
        }

        /// <summary>
        /// Placeholder landmark used when a definition has no prefab yet.
        /// A tall, clearly named primitive - visible from a distance, and easy
        /// to find and replace once real landmark assets exist.
        /// </summary>
        private static GameObject CreateLandmarkPlaceholder(Vector3 position, string landmarkId, string reservationId, Transform parent)
        {
            var placeholder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            placeholder.name = $"Landmark_PLACEHOLDER_{landmarkId}_{reservationId}";
            placeholder.transform.SetParent(parent, false);
            placeholder.transform.localScale = new Vector3(1.5f, 4f, 1.5f);
            // Cylinder pivot is its center; height = 2 * scaleY.
            placeholder.transform.position = position + Vector3.up * 4f;
            return placeholder;
        }

        #endregion

        #region Helpers

        private static void AttachIdentity(GameObject target, SpawnRecipe recipe, string role)
        {
            var identity = target.AddComponent<WorldContentIdentity>();
            identity.Initialize(recipe, role);
        }

        /// <summary>
        /// Deterministic cosmetic yaw derived from the reservation identity.
        /// Purely visual, never consumed by gameplay decisions.
        /// </summary>
        private static float DeterministicYaw(string reservationId, int entryIndex)
        {
            unchecked
            {
                uint hash = 2166136261u;
                foreach (char c in reservationId)
                {
                    hash = (hash ^ c) * 16777619u;
                }
                hash = (hash ^ (uint)entryIndex) * 16777619u;
                return (hash % 360u);
            }
        }

        #endregion
    }
}

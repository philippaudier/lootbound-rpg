using System.Diagnostics;
using Lootbound.Core.Logging;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace Lootbound.Gameplay.World.Navigation
{
    /// <summary>
    /// Builds the runtime NavMesh for the generated terrain.
    ///
    /// Knows ONLY: Terrain -> NavMeshSurface -> NavMesh. It never references
    /// encounters, enemies, resources, landmarks or any spawning type - that
    /// boundary is deliberate and must survive future slices.
    ///
    /// Listens to ProceduralTerrainGenerator.OnGenerationComplete (the
    /// published, final terrain), rebuilds the dedicated NavMeshSurface over
    /// the terrain bounds, and publishes OnNavigationCompleted with a result
    /// carrying the generation identity. Consumers decide what to do with it;
    /// this component never gates or triggers content itself.
    ///
    /// NavMeshSurface.BuildNavMesh() replaces the previous NavMeshData
    /// instance internally, so successive generations never accumulate data.
    /// </summary>
    public sealed class RuntimeNavigationBuilder : MonoBehaviour
    {
        private const string LogCategory = "Navigation";

        [Header("Sources")]
        [SerializeField]
        [Tooltip("Generator whose OnGenerationComplete publishes the final terrain")]
        private ProceduralTerrainGenerator terrainGenerator;

        [SerializeField]
        [Tooltip("The terrain the navigation is built for")]
        private Terrain terrain;

        [Header("Surface")]
        [SerializeField]
        [Tooltip("Dedicated NavMeshSurface for the procedural terrain. Its layer mask and geometry source are authored in the scene; this component drives its volume bounds and rebuilds.")]
        private NavMeshSurface surface;

        [SerializeField]
        [Range(0f, 50f)]
        [Tooltip("Horizontal margin added around the terrain bounds")]
        private float boundsMargin = 5f;

        private int currentGenerationId = -1;

        public RuntimeNavigationState State { get; private set; } = RuntimeNavigationState.NotBuilt;
        public RuntimeNavigationBuildResult LastResult { get; private set; }
        public RuntimeNavigationStats Stats { get; } = new RuntimeNavigationStats();

        /// <summary>
        /// Published once per build, success or failure. Carries the
        /// GenerationId so consumers can ignore stale results.
        /// </summary>
        public event System.Action<RuntimeNavigationBuildResult> OnNavigationCompleted;

        private void OnEnable()
        {
            if (terrainGenerator == null)
            {
                // Warning, not error: the builder stays usable through
                // Rebuild() (tests, manual driving) - only the automatic
                // rebuild-on-generation is unavailable.
                LootboundLog.Warning(LogCategory,
                    "RuntimeNavigationBuilder has no ProceduralTerrainGenerator assigned - no automatic rebuild on generation");
                return;
            }

            terrainGenerator.OnGenerationComplete += HandleGenerationComplete;
        }

        private void OnDisable()
        {
            if (terrainGenerator != null)
            {
                terrainGenerator.OnGenerationComplete -= HandleGenerationComplete;
            }
        }

        private void Start()
        {
            // Cover the case where generation finished before this component subscribed.
            if (terrainGenerator != null && terrainGenerator.IsGenerated && LastResult == null)
            {
                HandleGenerationComplete(terrainGenerator.Context);
            }
        }

        private void HandleGenerationComplete(TerrainGenerationContext context)
        {
            // Editor-mode generation (inspector buttons) must not build runtime navigation.
            if (!Application.isPlaying)
            {
                return;
            }

            Rebuild(context);
        }

        /// <summary>
        /// Build the navigation for the given published terrain context.
        /// Synchronous in V1 (measured acceptable on the development preset).
        /// </summary>
        public RuntimeNavigationBuildResult Rebuild(TerrainGenerationContext context)
        {
            int generationId = context?.GenerationId ?? -1;
            int seed = context?.Seed ?? 0;
            currentGenerationId = generationId;

            State = RuntimeNavigationState.Building;

            RuntimeNavigationBuildResult result = BuildInternal(context, generationId, seed);

            // A result belonging to an older generation must never be
            // published as valid navigation (defensive: with the synchronous
            // build this cannot trigger, but the contract must hold if the
            // build ever becomes asynchronous).
            if (generationId != currentGenerationId)
            {
                LootboundLog.Warning(LogCategory,
                    $"Discarding stale navigation build for generation {generationId} (current: {currentGenerationId})");
                return result;
            }

            State = result.Success ? RuntimeNavigationState.Ready : RuntimeNavigationState.Failed;
            LastResult = result;

            if (result.Success)
            {
                LootboundLog.Info(LogCategory,
                    $"NavMesh built: gen={result.GenerationId} seed={result.Seed} " +
                    $"{result.DurationMs:F0}ms, {result.TriangleCount} triangles, bounds={result.BoundsUsed.size}");
            }
            else
            {
                LootboundLog.Warning(LogCategory, $"NavMesh build failed: {result.FailureReason}");
            }

            OnNavigationCompleted?.Invoke(result);
            return result;
        }

        private RuntimeNavigationBuildResult BuildInternal(TerrainGenerationContext context, int generationId, int seed)
        {
            if (context == null)
            {
                return RuntimeNavigationBuildResult.Failed(generationId, seed, "No terrain generation context");
            }

            if (surface == null)
            {
                return RuntimeNavigationBuildResult.Failed(generationId, seed, "No NavMeshSurface configured");
            }

            if (terrain == null)
            {
                return RuntimeNavigationBuildResult.Failed(generationId, seed, "No Terrain assigned");
            }

            if (terrain.terrainData == null)
            {
                return RuntimeNavigationBuildResult.Failed(generationId, seed, "Terrain has no TerrainData");
            }

            Bounds worldBounds = ComputeWorldBounds();
            ConfigureSurfaceVolume(worldBounds);

            var watch = Stopwatch.StartNew();
            try
            {
                surface.BuildNavMesh();
            }
            catch (System.Exception e)
            {
                watch.Stop();
                return RuntimeNavigationBuildResult.Failed(
                    generationId, seed, $"BuildNavMesh threw: {e.Message}", watch.ElapsedMilliseconds);
            }
            watch.Stop();

            if (surface.navMeshData == null)
            {
                return RuntimeNavigationBuildResult.Failed(
                    generationId, seed, "BuildNavMesh produced no NavMeshData", watch.ElapsedMilliseconds);
            }

            int triangleCount = CountNavMeshTriangles();
            if (triangleCount == 0)
            {
                return RuntimeNavigationBuildResult.Failed(
                    generationId, seed, "Built NavMesh contains no triangles", watch.ElapsedMilliseconds);
            }

            var result = RuntimeNavigationBuildResult.Succeeded(
                generationId, seed, watch.ElapsedMilliseconds, worldBounds, surfaceCount: 1, triangleCount);
            Stats.RecordBuild(result.DurationMs, result.TriangleCount, result.BoundsUsed);
            return result;
        }

        /// <summary>
        /// Bounds derived from the actual TerrainData - never from a preset
        /// constant, so any world size (and later, other terrains) works.
        /// </summary>
        private Bounds ComputeWorldBounds()
        {
            Vector3 size = terrain.terrainData.size;
            Vector3 center = terrain.transform.position + size * 0.5f;
            Vector3 expanded = new Vector3(
                size.x + boundsMargin * 2f,
                size.y + boundsMargin * 2f,
                size.z + boundsMargin * 2f);
            return new Bounds(center, expanded);
        }

        private void ConfigureSurfaceVolume(Bounds worldBounds)
        {
            surface.collectObjects = CollectObjects.Volume;
            // NavMeshSurface volume center is expressed in the surface's local space.
            surface.center = surface.transform.InverseTransformPoint(worldBounds.center);
            surface.size = worldBounds.size;
        }

        private static int CountNavMeshTriangles()
        {
            // Diagnostics only; allocates once per build, never per frame.
            var triangulation = NavMesh.CalculateTriangulation();
            return triangulation.indices != null ? triangulation.indices.Length / 3 : 0;
        }
    }
}

using Lootbound.Gameplay.World.Layout;
using Lootbound.Gameplay.World.Landmarks;
using Lootbound.Gameplay.World.Spawning;
using UnityEngine;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

namespace Lootbound.Gameplay.World
{
    /// <summary>
    /// Orchestrates procedural terrain generation.
    /// Manages the Unity Terrain component and coordinates all generation steps.
    /// </summary>
    public class ProceduralTerrainGenerator : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private TerrainGenerationConfig config;

        [Header("Landmarks")]
        [SerializeField]
        [Tooltip("Landmark archetypes. When assigned, the world's elevated / terminal nodes become permanent landmarks attached to the layout (consumed by the LandmarkDirector and the ambient population). Optional: absence is a clean no-op.")]
        private LandmarkRegistry landmarkRegistry;

        [Header("Terrain Reference")]
        [SerializeField] private Terrain terrain;

        [Header("Terrain Layers")]
        [SerializeField] private TerrainLayer[] terrainLayers;

        [Header("Player")]
        [SerializeField] private Transform playerTransform;
        [SerializeField] private float playerHeight = 1.8f;

        [Header("Runtime")]
        [SerializeField] private int currentSeed;
        [SerializeField] private bool isGenerated;

        // Current generation context
        private TerrainGenerationContext context;

        // Monotone per-session generation identity. Two generations with the
        // same seed remain distinguishable for derived systems (navigation).
        private int generationCounter;

        // The "no landmark registry" warning is logged at most once.
        private bool landmarkRegistryWarningLogged;

        // Events
        public event System.Action<TerrainGenerationContext> OnGenerationComplete;

        // Public properties
        public TerrainGenerationConfig Config => config;
        public TerrainGenerationContext Context => context;
        public int CurrentSeed => currentSeed;
        public bool IsGenerated => isGenerated;

        private void Start()
        {
            if (config != null && config.GenerateOnStart)
            {
                Generate(config.DefaultSeed);
            }
        }

        /// <summary>
        /// Generate terrain with the default seed from config.
        /// </summary>
        public void GenerateDefault()
        {
            if (config == null)
            {
                Debug.LogError("[ProceduralTerrainGenerator] No configuration assigned.");
                return;
            }

            Generate(config.DefaultSeed);
        }

        /// <summary>
        /// Generate terrain with a random seed.
        /// </summary>
        public void GenerateRandom()
        {
            int randomSeed = System.Environment.TickCount;
            Generate(randomSeed);
        }

        /// <summary>
        /// Generate terrain with the specified seed.
        /// </summary>
        public void Generate(int seed)
        {
            if (!ValidateSetup())
            {
                return;
            }

            Stopwatch totalWatch = Stopwatch.StartNew();

            currentSeed = seed;
            isGenerated = false;

            Debug.Log($"[ProceduralTerrainGenerator] Generating terrain with seed {seed}...");

            // Create context
            generationCounter++;
            context = new TerrainGenerationContext(
                seed,
                config.HeightmapResolution,
                config.WorldSize,
                config.TerrainHeight,
                generationCounter
            );

            // Configure terrain data
            ConfigureTerrainData();

            // Step 1: Generate initial heightmap for spawn planning
            Stopwatch stepWatch = Stopwatch.StartNew();
            TerrainHeightGenerator.Generate(context, config);
            stepWatch.Stop();
            context.HeightmapGenerationTimeMs = stepWatch.ElapsedMilliseconds;

            // Step 2: Find refuge origin FIRST (before layout)
            TerrainSpawnPlanner.PlanSpawn(context, config);

            // Terrain sampler for layout generation, backed by the context's
            // NormalizedHeightMap/SlopeMap so every downstream consumer (layout,
            // flattening targets, ValidateAgainstTerrain, gizmos, spawning)
            // shares the height space actually applied to the Unity Terrain.
            var sampler = new TerrainContextSampler(context);

            // Step 3: Generate world layout (terrain-aware) if config is provided
            if (config.LayoutConfig != null && config.RingConfig != null)
            {
                stepWatch.Restart();

                // Create WorldDiscDefinition with logical world radius
                // For now, use terrain size as the world radius (can be different for streaming later)
                float worldDiscRadius = config.WorldSize * 0.5f;
                var worldDiscDefinition = new WorldDiscDefinition(worldDiscRadius, config.RingConfig);

                var layoutResult = WorldLayoutGenerator.Generate(seed, worldDiscDefinition, sampler, config.LayoutConfig);
                stepWatch.Stop();

                if (!layoutResult.Success)
                {
                    Debug.LogError($"[ProceduralTerrainGenerator] Layout generation failed: {layoutResult.Error}");
                    // Continue without layout - terrain will still be generated
                }
                else
                {
                    context.LayoutContext = layoutResult.Layout;

                    // Attach the progression authority: from here on, every
                    // consumer reads position context through it.
                    layoutResult.Layout.AttachProgression(new Progression.WorldProgression(
                        layoutResult.Layout.RefugePosition,
                        worldDiscRadius,
                        config.RingConfig,
                        config.ProgressionConfig));
                    Debug.Log($"[ProceduralTerrainGenerator] Layout generated with {layoutResult.Layout.NodesOrdered.Count} nodes, " +
                              $"{layoutResult.Layout.EdgesOrdered.Count} edges, {layoutResult.Layout.RadialPaths.Count} radial paths " +
                              $"(attempt {layoutResult.Layout.GenerationAttempt + 1})");

                    // Update spawn position to use layout's refuge position
                    context.SpawnPosition = layoutResult.Layout.RefugePosition;

                    // Step 4: Apply layout-aware flattening
                    TerrainHeightGenerator.ApplyLayoutFlattening(context, config, layoutResult.Layout);

                    // Step 4b: landmark terrain integration - seat the ground
                    // under conforming landmarks BEFORE anything grounds on it,
                    // then compute the shared landmark set once. Attached here
                    // (before OnGenerationComplete) so the LandmarkDirector and
                    // the ambient population both see a full set, order-independently.
                    IntegrateLandmarks(layoutResult.Layout, sampler);

                    // Step 4c: reproject reservation heights onto the (now
                    // stamped) terrain so stored positions match the final ground.
                    WorldLayoutGenerator.ReprojectReservationHeights(layoutResult.Layout, sampler);
                }
            }

            // Step 5: Apply heightmap to terrain
            stepWatch.Restart();
            ApplyHeightmapToTerrain();
            stepWatch.Stop();
            context.TerrainApplicationTimeMs = stepWatch.ElapsedMilliseconds;

            // Step 6: Validate layout against final terrain (if layout exists)
            if (context.LayoutContext != null)
            {
                var terrainValidation = WorldLayoutValidator.ValidateAgainstTerrain(
                    context.LayoutContext, context, config.LayoutConfig.PrimaryPathMaxSlope);
                if (!terrainValidation.IsValid)
                {
                    Debug.LogWarning($"[ProceduralTerrainGenerator] Post-correction validation warning: {terrainValidation.Error}");
                }
            }

            // Step 7: Paint terrain
            stepWatch.Restart();
            PaintTerrain();
            stepWatch.Stop();
            context.PaintingTimeMs = stepWatch.ElapsedMilliseconds;

            // Step 8: Position player
            PositionPlayer();

            totalWatch.Stop();
            context.TotalGenerationTimeMs = totalWatch.ElapsedMilliseconds;

            isGenerated = true;

            Debug.Log($"[ProceduralTerrainGenerator] Generation complete in {context.TotalGenerationTimeMs}ms " +
                      $"(Heightmap: {context.HeightmapGenerationTimeMs}ms, " +
                      $"Apply: {context.TerrainApplicationTimeMs}ms, " +
                      $"Paint: {context.PaintingTimeMs}ms)");

            OnGenerationComplete?.Invoke(context);
        }

        /// <summary>
        /// Regenerate with the current seed.
        /// </summary>
        public void Regenerate()
        {
            Generate(currentSeed);
        }

        /// <summary>
        /// Clear the terrain.
        /// </summary>
        public void ClearTerrain()
        {
            if (terrain == null || terrain.terrainData == null)
            {
                return;
            }

            TerrainData data = terrain.terrainData;

            // Reset heightmap to flat
            int resolution = data.heightmapResolution;
            float[,] heights = new float[resolution, resolution];
            data.SetHeights(0, 0, heights);

            // Clear alphamaps
            int alphaRes = data.alphamapResolution;
            int layers = data.alphamapLayers;
            if (layers > 0)
            {
                float[,,] alphas = new float[alphaRes, alphaRes, layers];
                for (int y = 0; y < alphaRes; y++)
                {
                    for (int x = 0; x < alphaRes; x++)
                    {
                        alphas[y, x, 0] = 1f;
                    }
                }
                data.SetAlphamaps(0, 0, alphas);
            }

            context = null;
            isGenerated = false;

            Debug.Log("[ProceduralTerrainGenerator] Terrain cleared.");
        }

        /// <summary>
        /// Validate that all required components are set up.
        /// </summary>
        /// <summary>
        /// Seat the terrain under conforming landmarks, then compute the world's
        /// landmarks once and attach them (with their terrain seats) to the
        /// layout. The order matters: placements are decided from the layout
        /// alone, the seats are described from the pre-stamp relief and realized
        /// on the heightmap, then the landmarks ground on the stamped terrain -
        /// so none float or sink. A missing registry is a clean no-op (empty
        /// sets + a single warning), never a failure.
        /// </summary>
        private void IntegrateLandmarks(WorldLayoutContext layout, ITerrainSampler sampler)
        {
            if (landmarkRegistry == null)
            {
                if (!landmarkRegistryWarningLogged)
                {
                    Debug.LogWarning("[ProceduralTerrainGenerator] No LandmarkRegistry assigned - the world has no landmarks.");
                    landmarkRegistryWarningLogged = true;
                }

                layout.AttachLandmarks(null);
                layout.AttachTerrainStamps(null);
                return;
            }

            // 1. Decide placements from the layout alone (pre-stamp, XZ frozen).
            var placements = LandmarkPlanner.PlanPlacements(layout, landmarkRegistry, layout.Progression);

            // 2. Describe each seat from the current (pre-stamp) relief.
            var stamps = LandmarkTerrainStampPlanner.Plan(placements, sampler);

            // 3. Realize the seats on the heightmap, before it reaches the Terrain.
            LandmarkTerrainStampApplier.Apply(context, stamps);

            // 4. Ground the landmarks on the now-stamped terrain.
            var landmarks = LandmarkPlanner.Finalize(placements, sampler);

            layout.AttachTerrainStamps(stamps);
            layout.AttachLandmarks(landmarks);
            Debug.Log($"[ProceduralTerrainGenerator] {landmarks.Count} landmark(s), {stamps.Count} terrain seat(s) attached to the layout");
        }

        private bool ValidateSetup()
        {
            if (config == null)
            {
                Debug.LogError("[ProceduralTerrainGenerator] Configuration is not assigned.");
                return false;
            }

            if (!config.Validate(out string configError))
            {
                Debug.LogError($"[ProceduralTerrainGenerator] Invalid configuration: {configError}");
                return false;
            }

            if (terrain == null)
            {
                Debug.LogError("[ProceduralTerrainGenerator] Terrain is not assigned.");
                return false;
            }

            if (terrain.terrainData == null)
            {
                Debug.LogError("[ProceduralTerrainGenerator] Terrain has no TerrainData.");
                return false;
            }

            if (terrainLayers == null || terrainLayers.Length < 4)
            {
                Debug.LogWarning("[ProceduralTerrainGenerator] Terrain layers not properly configured. Need at least 4 layers.");
            }

            return true;
        }

        /// <summary>
        /// Configure the terrain data dimensions.
        /// </summary>
        private void ConfigureTerrainData()
        {
            TerrainData data = terrain.terrainData;

            // Set heightmap resolution
            data.heightmapResolution = config.HeightmapResolution;

            // Set alphamap resolution
            data.alphamapResolution = config.AlphamapResolution;

            // Set terrain size
            data.size = new Vector3(config.WorldSize, config.TerrainHeight, config.WorldSize);

            // Position terrain at origin
            terrain.transform.position = Vector3.zero;

            // Apply terrain layers
            if (terrainLayers != null && terrainLayers.Length > 0)
            {
                data.terrainLayers = terrainLayers;
            }
        }

        /// <summary>
        /// Apply the generated heightmap to the Unity Terrain.
        /// </summary>
        private void ApplyHeightmapToTerrain()
        {
            TerrainData data = terrain.terrainData;
            float[,] terrainHeights = context.GetTerrainHeightmapData();
            data.SetHeights(0, 0, terrainHeights);
        }

        /// <summary>
        /// Paint the terrain with texture layers.
        /// </summary>
        private void PaintTerrain()
        {
            if (terrainLayers == null || terrainLayers.Length < 4)
            {
                Debug.LogWarning("[ProceduralTerrainGenerator] Skipping painting - insufficient terrain layers.");
                return;
            }

            TerrainData data = terrain.terrainData;
            float[,,] alphamap = TerrainSurfacePainter.Paint(context, config, data.alphamapResolution);
            data.SetAlphamaps(0, 0, alphamap);
        }

        /// <summary>
        /// Position the player at the spawn location.
        /// </summary>
        private void PositionPlayer()
        {
            if (playerTransform == null)
            {
                return;
            }

            // Get spawn XZ from context
            Vector3 spawnXZ = context.SpawnPosition;

            // Sample ACTUAL terrain height from Unity Terrain (not context heightmap)
            // This ensures player is positioned on the real terrain surface after all modifications
            float actualTerrainHeight = terrain.SampleHeight(new Vector3(spawnXZ.x, 0, spawnXZ.z));

            // Add terrain's world position Y offset (usually 0, but be safe)
            actualTerrainHeight += terrain.transform.position.y;

            // Place player above terrain surface
            float safeHeight = actualTerrainHeight + playerHeight * 0.5f + 0.5f;
            Vector3 startPos = new Vector3(spawnXZ.x, safeHeight, spawnXZ.z);

            // Disable CharacterController temporarily to allow teleport
            CharacterController cc = playerTransform.GetComponent<CharacterController>();
            if (cc != null)
            {
                cc.enabled = false;
            }

            playerTransform.position = startPos;

            // Reset rotation to face a random direction based on seed
            System.Random rotRandom = new System.Random(context.Seed + 99999);
            float yRotation = (float)(rotRandom.NextDouble() * 360f);
            playerTransform.rotation = Quaternion.Euler(0, yRotation, 0);

            // Re-enable CharacterController
            if (cc != null)
            {
                cc.enabled = true;
            }

            // Update context spawn position with actual height for consistency
            context.SpawnPosition = new Vector3(spawnXZ.x, actualTerrainHeight, spawnXZ.z);

            Debug.Log($"[ProceduralTerrainGenerator] Player positioned at {startPos} (terrain height: {actualTerrainHeight:F2})");
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (context == null || !isGenerated)
            {
                return;
            }

            // Draw spawn zone
            Vector3 spawn = context.SpawnPosition;

            // Safe radius
            Gizmos.color = new Color(0, 1, 0, 0.3f);
            DrawCircle(spawn, config.SpawnSafeRadius, 32);

            // Blend radius
            Gizmos.color = new Color(1, 1, 0, 0.2f);
            DrawCircle(spawn, config.SpawnBlendRadius, 48);

            // Spawn point marker
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(spawn + Vector3.up * 2f, 0.5f);
            Gizmos.DrawLine(spawn, spawn + Vector3.up * 5f);

            // Terrain bounds
            Gizmos.color = new Color(1, 1, 1, 0.2f);
            Vector3 center = new Vector3(context.WorldSize * 0.5f, context.TerrainHeight * 0.5f, context.WorldSize * 0.5f);
            Vector3 size = new Vector3(context.WorldSize, context.TerrainHeight, context.WorldSize);
            Gizmos.DrawWireCube(center, size);
        }

        private void DrawCircle(Vector3 center, float radius, int segments)
        {
            float angleStep = 360f / segments;
            Vector3 prevPoint = center + new Vector3(radius, 0, 0);

            for (int i = 1; i <= segments; i++)
            {
                float angle = i * angleStep * Mathf.Deg2Rad;
                Vector3 nextPoint = center + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);

                // Adjust Y to terrain height
                float prevHeight = 0f;
                float nextHeight = 0f;
                if (context != null)
                {
                    prevHeight = context.SampleHeightAtWorld(prevPoint.x, prevPoint.z);
                    nextHeight = context.SampleHeightAtWorld(nextPoint.x, nextPoint.z);
                }

                Gizmos.DrawLine(
                    new Vector3(prevPoint.x, prevHeight + 0.5f, prevPoint.z),
                    new Vector3(nextPoint.x, nextHeight + 0.5f, nextPoint.z)
                );

                prevPoint = nextPoint;
            }
        }
#endif
    }
}

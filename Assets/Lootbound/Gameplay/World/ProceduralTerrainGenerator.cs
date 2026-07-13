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
            context = new TerrainGenerationContext(
                seed,
                config.HeightmapResolution,
                config.WorldSize,
                config.TerrainHeight
            );

            // Configure terrain data
            ConfigureTerrainData();

            // Step 1: Generate heightmap
            Stopwatch stepWatch = Stopwatch.StartNew();
            TerrainHeightGenerator.Generate(context, config);
            stepWatch.Stop();
            context.HeightmapGenerationTimeMs = stepWatch.ElapsedMilliseconds;

            // Step 2: Plan spawn
            TerrainSpawnPlanner.PlanSpawn(context, config);

            // Step 3: Apply heightmap to terrain
            stepWatch.Restart();
            ApplyHeightmapToTerrain();
            stepWatch.Stop();
            context.TerrainApplicationTimeMs = stepWatch.ElapsedMilliseconds;

            // Step 4: Paint terrain
            stepWatch.Restart();
            PaintTerrain();
            stepWatch.Stop();
            context.PaintingTimeMs = stepWatch.ElapsedMilliseconds;

            // Step 5: Position player
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

            Vector3 startPos = TerrainSpawnPlanner.GetPlayerStartPosition(context, playerHeight);
            playerTransform.position = startPos;

            // Reset rotation to face a random direction based on seed
            System.Random rotRandom = new System.Random(context.Seed + 99999);
            float yRotation = (float)(rotRandom.NextDouble() * 360f);
            playerTransform.rotation = Quaternion.Euler(0, yRotation, 0);

            // Reset velocity if player has a character controller
            CharacterController cc = playerTransform.GetComponent<CharacterController>();
            if (cc != null)
            {
                // CharacterController doesn't have velocity - it's handled by FirstPersonMotor
                // The motor will naturally reset on next frame
            }

            Debug.Log($"[ProceduralTerrainGenerator] Player positioned at {startPos}");
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

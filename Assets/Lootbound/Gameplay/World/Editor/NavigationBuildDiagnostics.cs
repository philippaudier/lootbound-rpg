using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Profiling;
using Debug = UnityEngine.Debug;

namespace Lootbound.Gameplay.World.Editor
{
    /// <summary>
    /// Measures runtime NavMesh build cost on generated terrain so the
    /// NavMeshSurface configuration (geometry source) and the sync-build
    /// decision rest on numbers, not intuition. Compares RenderMeshes vs
    /// PhysicsColliders across world presets (build time, triangles, memory).
    /// Run from the menu or in batch via -executeMethod.
    /// </summary>
    public static class NavigationBuildDiagnostics
    {
        private const string TerrainConfigPath = "Assets/Lootbound/ScriptableObjects/World/DefaultTerrainGenerationConfig.asset";
        private const string OutputPath = "NavigationBuildDiagnostics.txt";

        private static readonly (float worldSize, int resolution)[] Presets =
        {
            (512f, 257),
            (1024f, 513),
            (2048f, 1025)
        };

        [MenuItem("Lootbound/Diagnostics/Navigation Build Report")]
        public static void Run()
        {
            var baseConfig = AssetDatabase.LoadAssetAtPath<TerrainGenerationConfig>(TerrainConfigPath);
            if (baseConfig == null)
            {
                Debug.LogError("[NavigationBuildDiagnostics] Could not load DefaultTerrainGenerationConfig.");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine("# Navigation build diagnostics");
            sb.AppendLine($"# Base preset: terrainHeight={baseConfig.TerrainHeight} normalizeHeightmap={baseConfig.NormalizeHeightmap} seed=42");
            sb.AppendLine("preset;geometry;build#;durationMs;triangles;navMeshDataKB");

            foreach (var (worldSize, resolution) in Presets)
            {
                MeasurePreset(sb, baseConfig, worldSize, resolution);
            }

            System.IO.File.WriteAllText(OutputPath, sb.ToString());
            Debug.Log($"[NavigationBuildDiagnostics] Report written to {System.IO.Path.GetFullPath(OutputPath)}");
        }

        private static void MeasurePreset(StringBuilder sb, TerrainGenerationConfig baseConfig, float worldSize, int resolution)
        {
            // Work on a copy so the asset is never mutated.
            var config = Object.Instantiate(baseConfig);
            SetField(config, "worldSize", worldSize);
            SetField(config, "heightmapResolution", resolution);

            var context = new TerrainGenerationContext(42, resolution, worldSize, config.TerrainHeight);
            TerrainHeightGenerator.Generate(context, config);

            var terrainData = new TerrainData();
            terrainData.heightmapResolution = resolution;
            terrainData.size = new Vector3(worldSize, config.TerrainHeight, worldSize);
            terrainData.SetHeights(0, 0, context.GetTerrainHeightmapData());

            GameObject terrainGo = Terrain.CreateTerrainGameObject(terrainData);
            GameObject surfaceGo = new GameObject("Diag_NavMeshSurface");

            try
            {
                terrainGo.transform.position = Vector3.zero;
                var surface = surfaceGo.AddComponent<NavMeshSurface>();
                surface.agentTypeID = 0; // Humanoid - matches Enemy.prefab
                surface.collectObjects = CollectObjects.Volume;
                Vector3 size = terrainData.size;
                surface.center = terrainGo.transform.position + size * 0.5f;
                surface.size = size + new Vector3(10f, 10f, 10f);

                foreach (var geometry in new[] { NavMeshCollectGeometry.RenderMeshes, NavMeshCollectGeometry.PhysicsColliders })
                {
                    surface.useGeometry = geometry;

                    // Two builds: cold, then rebuild (the regeneration path).
                    for (int buildIndex = 1; buildIndex <= 2; buildIndex++)
                    {
                        var watch = Stopwatch.StartNew();
                        surface.BuildNavMesh();
                        watch.Stop();

                        var triangulation = NavMesh.CalculateTriangulation();
                        int triangles = triangulation.indices != null ? triangulation.indices.Length / 3 : 0;
                        long memoryBytes = surface.navMeshData != null
                            ? Profiler.GetRuntimeMemorySizeLong(surface.navMeshData)
                            : 0;

                        sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                            "{0};{1};{2};{3};{4};{5:F0}",
                            worldSize, geometry, buildIndex, watch.ElapsedMilliseconds, triangles, memoryBytes / 1024f));
                    }

                    surface.RemoveData();
                    surface.navMeshData = null;
                }
            }
            finally
            {
                Object.DestroyImmediate(surfaceGo);
                Object.DestroyImmediate(terrainGo);
                Object.DestroyImmediate(terrainData);
                Object.DestroyImmediate(config);
            }
        }

        private static void SetField(object obj, string fieldName, object value)
        {
            var field = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                Debug.LogError($"[NavigationBuildDiagnostics] Field '{fieldName}' not found on {obj.GetType().Name}");
                return;
            }
            field.SetValue(obj, value);
        }
    }
}

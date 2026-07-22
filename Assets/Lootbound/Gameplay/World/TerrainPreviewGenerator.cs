using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Lootbound.Gameplay.World
{
    /// <summary>
    /// Fills the "Editor Terrain Preview" - the single authored Unity Terrain kept
    /// as an edit-time laboratory (shaders, textures, stamps, gizmos, captures).
    /// It is a CLIENT of the generator, exactly like the streamed chunks: it
    /// listens for a built world and displays it. The generator knows nothing
    /// about it, and the chunk streamer never uses it.
    ///
    /// ExecuteAlways so the inspector's Generate buttons refresh the preview in
    /// Edit Mode too. In Play Mode the preview stays filled - its collider is the
    /// documented transitional spawn safety net - while TerrainPreviewHider hides
    /// its mesh so only the streamed chunks are visible.
    /// </summary>
    [ExecuteAlways]
    public sealed class TerrainPreviewGenerator : MonoBehaviour
    {
        [SerializeField] private ProceduralTerrainGenerator generator;
        [SerializeField] private Terrain previewTerrain;
        [SerializeField] private TerrainLayer[] terrainLayers;

        private void OnEnable()
        {
            if (generator == null)
            {
                return;
            }

            generator.OnGenerationComplete += Fill;
            if (generator.IsGenerated && generator.Context != null)
            {
                Fill(generator.Context);
            }
        }

        private void OnDisable()
        {
            if (generator != null)
            {
                generator.OnGenerationComplete -= Fill;
            }
        }

        /// <summary>Display the built world on the preview Terrain.</summary>
        public void Fill(TerrainGenerationContext context)
        {
            if (context == null || previewTerrain == null || previewTerrain.terrainData == null ||
                generator == null || generator.Config == null)
            {
                return;
            }

            TerrainGenerationConfig config = generator.Config;
            TerrainData data = previewTerrain.terrainData;
            var watch = Stopwatch.StartNew();

            data.heightmapResolution = context.Resolution;
            data.alphamapResolution = config.AlphamapResolution;
            data.size = new Vector3(context.WorldSize, context.TerrainHeight, context.WorldSize);
            previewTerrain.transform.position = new Vector3(context.Bounds.MinX, 0f, context.Bounds.MinZ);
            if (terrainLayers != null && terrainLayers.Length > 0)
            {
                data.terrainLayers = terrainLayers;
            }

            data.SetHeights(0, 0, context.GetTerrainHeightmapData());
            watch.Stop();
            context.TerrainApplicationTimeMs = watch.ElapsedMilliseconds;

            watch.Restart();
            if (terrainLayers != null && terrainLayers.Length >= 4)
            {
                float[,,] alphamap = TerrainSurfacePainter.Paint(context, config, data.alphamapResolution);
                data.SetAlphamaps(0, 0, alphamap);
            }
            else
            {
                Debug.LogWarning("[TerrainPreviewGenerator] Skipping painting - fewer than 4 terrain layers assigned.");
            }
            watch.Stop();
            context.PaintingTimeMs = watch.ElapsedMilliseconds;
        }

        /// <summary>Reset the preview to a flat, first-layer terrain.</summary>
        public void Clear()
        {
            if (previewTerrain == null || previewTerrain.terrainData == null)
            {
                return;
            }

            TerrainData data = previewTerrain.terrainData;
            int resolution = data.heightmapResolution;
            data.SetHeights(0, 0, new float[resolution, resolution]);

            int alphaRes = data.alphamapResolution;
            int layers = data.alphamapLayers;
            if (layers > 0)
            {
                var alphas = new float[alphaRes, alphaRes, layers];
                for (int z = 0; z < alphaRes; z++)
                {
                    for (int x = 0; x < alphaRes; x++)
                    {
                        alphas[z, x, 0] = 1f;
                    }
                }
                data.SetAlphamaps(0, 0, alphas);
            }
        }
    }
}

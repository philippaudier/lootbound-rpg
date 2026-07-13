using UnityEngine;
using UnityEngine.InputSystem;

namespace Lootbound.Gameplay.World
{
    /// <summary>
    /// Debug overlay for terrain generation.
    /// Shows generation metrics and allows visualizing terrain maps.
    /// </summary>
    public class TerrainGenerationDebug : MonoBehaviour
    {
        [SerializeField] private ProceduralTerrainGenerator generator;

        [Header("Display Settings")]
        [SerializeField] private bool showOnStart = true;
        [SerializeField] private Key toggleKey = Key.F5;

        [Header("Map Visualization")]
        [SerializeField] private bool showMapPreview = false;
        [SerializeField] private MapVisualization currentMap = MapVisualization.None;
        [SerializeField] private int mapPreviewSize = 200;

        public enum MapVisualization
        {
            None,
            Height,
            Slope,
            Macro
        }

        private bool isVisible;
        private GUIStyle boxStyle;
        private GUIStyle labelStyle;
        private GUIStyle headerStyle;
        private Texture2D mapTexture;
        private MapVisualization lastRenderedMap;
        private int lastRenderedSeed;

        private void Start()
        {
            isVisible = showOnStart;
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current[toggleKey].wasPressedThisFrame)
            {
                isVisible = !isVisible;
            }

            // Cycle through maps with number keys when visible
            if (isVisible && Keyboard.current != null)
            {
                if (Keyboard.current[Key.Digit1].wasPressedThisFrame)
                {
                    currentMap = MapVisualization.None;
                    showMapPreview = false;
                }
                else if (Keyboard.current[Key.Digit2].wasPressedThisFrame)
                {
                    currentMap = MapVisualization.Height;
                    showMapPreview = true;
                }
                else if (Keyboard.current[Key.Digit3].wasPressedThisFrame)
                {
                    currentMap = MapVisualization.Slope;
                    showMapPreview = true;
                }
                else if (Keyboard.current[Key.Digit4].wasPressedThisFrame)
                {
                    currentMap = MapVisualization.Macro;
                    showMapPreview = true;
                }
            }
        }

        private void OnGUI()
        {
            if (!isVisible || generator == null) return;

            EnsureStyles();
            DrawDebugPanel();

            if (showMapPreview && generator.IsGenerated)
            {
                DrawMapPreview();
            }
        }

        private void EnsureStyles()
        {
            if (boxStyle == null)
            {
                boxStyle = new GUIStyle(GUI.skin.box)
                {
                    padding = new RectOffset(10, 10, 10, 10)
                };
            }

            if (labelStyle == null)
            {
                labelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 12,
                    normal = { textColor = Color.white }
                };
            }

            if (headerStyle == null)
            {
                headerStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = new Color(0.8f, 0.9f, 1f) }
                };
            }
        }

        private void DrawDebugPanel()
        {
            int width = 280;
            int height = generator.IsGenerated ? 340 : 120;
            int x = 10;
            int y = 160; // Below the main debug overlay

            GUI.Box(new Rect(x, y, width, height), "", boxStyle);

            int lineY = y + 10;
            int lineHeight = 18;
            int sectionSpacing = 8;

            // Header
            GUI.Label(new Rect(x + 10, lineY, width - 20, lineHeight), "Terrain Debug (F5)", headerStyle);
            lineY += lineHeight + sectionSpacing;

            // Generation status
            string statusText = generator.IsGenerated
                ? "<color=green>Generated</color>"
                : "<color=yellow>Not Generated</color>";

            GUIStyle richStyle = new GUIStyle(labelStyle) { richText = true };
            GUI.Label(new Rect(x + 10, lineY, width - 20, lineHeight), $"Status: {statusText}", richStyle);
            lineY += lineHeight;

            GUI.Label(new Rect(x + 10, lineY, width - 20, lineHeight), $"Current Seed: {generator.CurrentSeed}", labelStyle);
            lineY += lineHeight;

            if (!generator.IsGenerated)
            {
                GUI.Label(new Rect(x + 10, lineY, width - 20, lineHeight), "Press F5 to toggle", labelStyle);
                return;
            }

            var ctx = generator.Context;
            if (ctx == null) return;

            // Terrain info
            lineY += sectionSpacing;
            GUI.Label(new Rect(x + 10, lineY, width - 20, lineHeight), "Terrain", headerStyle);
            lineY += lineHeight;

            GUI.Label(new Rect(x + 10, lineY, width - 20, lineHeight), $"Size: {ctx.WorldSize}m x {ctx.WorldSize}m", labelStyle);
            lineY += lineHeight;

            GUI.Label(new Rect(x + 10, lineY, width - 20, lineHeight), $"Resolution: {ctx.Resolution}", labelStyle);
            lineY += lineHeight;

            GUI.Label(new Rect(x + 10, lineY, width - 20, lineHeight), $"Max Height: {ctx.TerrainHeight}m", labelStyle);
            lineY += lineHeight;

            // Height stats
            lineY += sectionSpacing;
            GUI.Label(new Rect(x + 10, lineY, width - 20, lineHeight), "Heights", headerStyle);
            lineY += lineHeight;

            float minWorldHeight = ctx.MinHeight * ctx.TerrainHeight;
            float maxWorldHeight = ctx.MaxHeight * ctx.TerrainHeight;
            float avgWorldHeight = ctx.AverageHeight * ctx.TerrainHeight;

            GUI.Label(new Rect(x + 10, lineY, width - 20, lineHeight), $"Min: {minWorldHeight:0.0}m  Max: {maxWorldHeight:0.0}m", labelStyle);
            lineY += lineHeight;

            GUI.Label(new Rect(x + 10, lineY, width - 20, lineHeight), $"Average: {avgWorldHeight:0.0}m", labelStyle);
            lineY += lineHeight;

            // Spawn info
            lineY += sectionSpacing;
            GUI.Label(new Rect(x + 10, lineY, width - 20, lineHeight), "Spawn", headerStyle);
            lineY += lineHeight;

            Vector3 spawn = ctx.SpawnPosition;
            GUI.Label(new Rect(x + 10, lineY, width - 20, lineHeight),
                $"Pos: ({spawn.x:0.0}, {spawn.y:0.0}, {spawn.z:0.0})", labelStyle);
            lineY += lineHeight;

            GUI.Label(new Rect(x + 10, lineY, width - 20, lineHeight), $"Slope: {ctx.SpawnSlope:0.1}°", labelStyle);
            lineY += lineHeight;

            // Timing
            lineY += sectionSpacing;
            GUI.Label(new Rect(x + 10, lineY, width - 20, lineHeight), "Generation Time", headerStyle);
            lineY += lineHeight;

            GUI.Label(new Rect(x + 10, lineY, width - 20, lineHeight),
                $"Total: {ctx.TotalGenerationTimeMs}ms", labelStyle);
            lineY += lineHeight;

            GUI.Label(new Rect(x + 10, lineY, width - 20, lineHeight),
                $"Height: {ctx.HeightmapGenerationTimeMs}ms  Paint: {ctx.PaintingTimeMs}ms", labelStyle);
            lineY += lineHeight;

            // Map preview controls
            lineY += sectionSpacing;
            GUI.Label(new Rect(x + 10, lineY, width - 20, lineHeight), "Maps: 1=Off 2=Height 3=Slope 4=Macro", labelStyle);
        }

        private void DrawMapPreview()
        {
            var ctx = generator.Context;
            if (ctx == null) return;

            // Update texture if needed
            if (mapTexture == null || lastRenderedMap != currentMap || lastRenderedSeed != ctx.Seed)
            {
                UpdateMapTexture(ctx);
            }

            if (mapTexture == null) return;

            int x = 300;
            int y = 160;

            // Draw label
            GUI.Label(new Rect(x, y - 20, mapPreviewSize, 20), $"Map: {currentMap}", headerStyle);

            // Draw texture
            GUI.DrawTexture(new Rect(x, y, mapPreviewSize, mapPreviewSize), mapTexture);
        }

        private void UpdateMapTexture(TerrainGenerationContext ctx)
        {
            if (currentMap == MapVisualization.None)
            {
                return;
            }

            int size = Mathf.Min(mapPreviewSize, ctx.Resolution);

            if (mapTexture == null || mapTexture.width != size)
            {
                if (mapTexture != null)
                {
                    Destroy(mapTexture);
                }
                mapTexture = new Texture2D(size, size, TextureFormat.RGB24, false);
                mapTexture.filterMode = FilterMode.Point;
            }

            float[,] sourceMap = currentMap switch
            {
                MapVisualization.Height => ctx.NormalizedHeightMap,
                MapVisualization.Slope => ctx.SlopeMap,
                MapVisualization.Macro => ctx.MacroMap,
                _ => null
            };

            if (sourceMap == null) return;

            int resolution = ctx.Resolution;
            float scale = (float)resolution / size;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int srcX = Mathf.FloorToInt(x * scale);
                    int srcZ = Mathf.FloorToInt(y * scale);
                    srcX = Mathf.Clamp(srcX, 0, resolution - 1);
                    srcZ = Mathf.Clamp(srcZ, 0, resolution - 1);

                    float value = sourceMap[srcX, srcZ];

                    Color color;
                    if (currentMap == MapVisualization.Slope)
                    {
                        // Slope is in degrees, normalize to 0-1 for display
                        value = Mathf.Clamp01(value / 60f);
                        // Color code: green = flat, yellow = moderate, red = steep
                        if (value < 0.3f)
                            color = Color.Lerp(Color.green, Color.yellow, value / 0.3f);
                        else
                            color = Color.Lerp(Color.yellow, Color.red, (value - 0.3f) / 0.7f);
                    }
                    else
                    {
                        // Grayscale for height and macro
                        color = new Color(value, value, value);
                    }

                    mapTexture.SetPixel(x, y, color);
                }
            }

            mapTexture.Apply();

            lastRenderedMap = currentMap;
            lastRenderedSeed = ctx.Seed;
        }

        private void OnDestroy()
        {
            if (mapTexture != null)
            {
                Destroy(mapTexture);
            }
        }
    }
}

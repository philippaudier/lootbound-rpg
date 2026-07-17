using UnityEngine;
using UnityEngine.InputSystem;

namespace Lootbound.Gameplay.World
{
    /// <summary>
    /// Debug overlay for terrain generation.
    /// Shows generation metrics and allows visualizing terrain maps.
    /// Toggle with F5. Position: Right of Movement panel (F4), auto-sized.
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
        private Texture2D mapTexture;
        private MapVisualization lastRenderedMap;
        private int lastRenderedSeed;

        // Styles matching WearMetrics
        private GUIStyle boxStyle;
        private GUIStyle labelStyle;
        private GUIStyle headerStyle;
        private GUIStyle subHeaderStyle;
        private GUIStyle valueStyle;

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

            InitializeStyles();
            DrawDebugPanel();

            if (showMapPreview && generator.IsGenerated)
            {
                DrawMapPreview();
            }
        }

        private void InitializeStyles()
        {
            if (boxStyle != null) return;

            boxStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = MakeTexture(2, 2, new Color(0.1f, 0.1f, 0.12f, 0.92f)) }
            };

            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                normal = { textColor = new Color(0.8f, 0.8f, 0.8f) }
            };

            headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.9f, 0.8f, 0.6f) }
            };

            subHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                fontStyle = FontStyle.Italic,
                normal = { textColor = new Color(0.6f, 0.6f, 0.65f) }
            };

            valueStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                normal = { textColor = new Color(0.9f, 0.9f, 0.9f) }
            };
        }

        private Texture2D MakeTexture(int width, int height, Color color)
        {
            var pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = color;

            var texture = new Texture2D(width, height);
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        private float CalculateContentHeight()
        {
            float lineHeight = 18f;
            float height = 10f; // Top padding

            // Header
            height += lineHeight + 2f;

            // Status + Seed
            height += lineHeight * 2;

            if (!generator.IsGenerated)
            {
                // Controls hint
                height += 5f + lineHeight;
                return height + 10f; // Bottom padding
            }

            // Terrain section: header + 3 lines
            height += 5f + lineHeight + lineHeight * 3;

            // Heights section: header + 2 lines
            height += 5f + lineHeight + lineHeight * 2;

            // Spawn section: header + 2 lines
            height += 5f + lineHeight + lineHeight * 2;

            // Timing section: header + 2 lines
            height += 5f + lineHeight + lineHeight * 2;

            // Controls hint
            height += 5f + lineHeight;

            return height + 10f; // Bottom padding
        }

        private float lastPanelHeight;

        private void DrawDebugPanel()
        {
            float width = 280f;
            float height = CalculateContentHeight();
            lastPanelHeight = height; // Store for map preview positioning
            float x = 550f; // Right of Movement panel (280 + 260 + 10 gap)
            float y = 10f;  // Aligned with System/Movement panels

            GUI.Box(new Rect(x, y, width, height), "", boxStyle);

            float lineY = y + 10f;
            float lineHeight = 18f;
            float labelX = x + 10f;
            float valueX = x + 140f;
            float valueWidth = 130f;

            // Header
            GUI.Label(new Rect(labelX, lineY, width - 20f, lineHeight), "TERRAIN DEBUG (F5)", headerStyle);
            lineY += lineHeight + 2f;

            // Generation status
            GUI.Label(new Rect(labelX, lineY, 130f, lineHeight), "Status:", labelStyle);
            string statusText = generator.IsGenerated ? "Generated" : "Not Generated";
            Color statusColor = generator.IsGenerated ? new Color(0.6f, 0.9f, 0.6f) : new Color(0.9f, 0.9f, 0.5f);
            var statusStyle = new GUIStyle(valueStyle) { normal = { textColor = statusColor } };
            GUI.Label(new Rect(valueX, lineY, valueWidth, lineHeight), statusText, statusStyle);
            lineY += lineHeight;

            GUI.Label(new Rect(labelX, lineY, 130f, lineHeight), "Seed:", labelStyle);
            GUI.Label(new Rect(valueX, lineY, valueWidth, lineHeight), generator.CurrentSeed.ToString(), valueStyle);
            lineY += lineHeight;

            if (!generator.IsGenerated)
            {
                lineY += 5f;
                GUI.Label(new Rect(labelX, lineY, width - 20f, lineHeight), "1=Off 2=Height 3=Slope 4=Macro", subHeaderStyle);
                return;
            }

            var ctx = generator.Context;
            if (ctx == null) return;

            // Terrain info
            lineY += 5f;
            GUI.Label(new Rect(labelX, lineY, width - 20f, lineHeight), "— Terrain —", subHeaderStyle);
            lineY += lineHeight;

            GUI.Label(new Rect(labelX, lineY, 130f, lineHeight), "Size:", labelStyle);
            GUI.Label(new Rect(valueX, lineY, valueWidth, lineHeight), $"{ctx.WorldSize}m x {ctx.WorldSize}m", valueStyle);
            lineY += lineHeight;

            GUI.Label(new Rect(labelX, lineY, 130f, lineHeight), "Resolution:", labelStyle);
            GUI.Label(new Rect(valueX, lineY, valueWidth, lineHeight), ctx.Resolution.ToString(), valueStyle);
            lineY += lineHeight;

            GUI.Label(new Rect(labelX, lineY, 130f, lineHeight), "Max Height:", labelStyle);
            GUI.Label(new Rect(valueX, lineY, valueWidth, lineHeight), $"{ctx.TerrainHeight}m", valueStyle);
            lineY += lineHeight;

            // Height stats
            lineY += 5f;
            GUI.Label(new Rect(labelX, lineY, width - 20f, lineHeight), "— Heights —", subHeaderStyle);
            lineY += lineHeight;

            float minWorldHeight = ctx.MinHeight * ctx.TerrainHeight;
            float maxWorldHeight = ctx.MaxHeight * ctx.TerrainHeight;
            float avgWorldHeight = ctx.AverageHeight * ctx.TerrainHeight;

            GUI.Label(new Rect(labelX, lineY, 130f, lineHeight), "Min / Max:", labelStyle);
            GUI.Label(new Rect(valueX, lineY, valueWidth, lineHeight), $"{minWorldHeight:F1}m / {maxWorldHeight:F1}m", valueStyle);
            lineY += lineHeight;

            GUI.Label(new Rect(labelX, lineY, 130f, lineHeight), "Average:", labelStyle);
            GUI.Label(new Rect(valueX, lineY, valueWidth, lineHeight), $"{avgWorldHeight:F1}m", valueStyle);
            lineY += lineHeight;

            // Spawn info
            lineY += 5f;
            GUI.Label(new Rect(labelX, lineY, width - 20f, lineHeight), "— Spawn —", subHeaderStyle);
            lineY += lineHeight;

            Vector3 spawn = ctx.SpawnPosition;
            GUI.Label(new Rect(labelX, lineY, 130f, lineHeight), "Position:", labelStyle);
            GUI.Label(new Rect(valueX, lineY, valueWidth, lineHeight), $"({spawn.x:F0}, {spawn.y:F0}, {spawn.z:F0})", valueStyle);
            lineY += lineHeight;

            GUI.Label(new Rect(labelX, lineY, 130f, lineHeight), "Slope:", labelStyle);
            GUI.Label(new Rect(valueX, lineY, valueWidth, lineHeight), $"{ctx.SpawnSlope:F1}°", valueStyle);
            lineY += lineHeight;

            // Timing
            lineY += 5f;
            GUI.Label(new Rect(labelX, lineY, width - 20f, lineHeight), "— Generation Time —", subHeaderStyle);
            lineY += lineHeight;

            GUI.Label(new Rect(labelX, lineY, 130f, lineHeight), "Total:", labelStyle);
            GUI.Label(new Rect(valueX, lineY, valueWidth, lineHeight), $"{ctx.TotalGenerationTimeMs}ms", valueStyle);
            lineY += lineHeight;

            GUI.Label(new Rect(labelX, lineY, 130f, lineHeight), "Height / Paint:", labelStyle);
            GUI.Label(new Rect(valueX, lineY, valueWidth, lineHeight), $"{ctx.HeightmapGenerationTimeMs}ms / {ctx.PaintingTimeMs}ms", valueStyle);
            lineY += lineHeight;

            // Map preview controls
            lineY += 5f;
            GUI.Label(new Rect(labelX, lineY, width - 20f, lineHeight), "1=Off 2=Height 3=Slope 4=Macro", subHeaderStyle);
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

            float x = 550f; // Aligned with terrain panel
            float y = 10f + lastPanelHeight + 10f; // Below terrain panel

            // Draw label
            GUI.Label(new Rect(x, y - 18f, mapPreviewSize, 18f), $"Map: {currentMap}", headerStyle);

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

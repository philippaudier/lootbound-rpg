using UnityEngine;
using UnityEngine.InputSystem;
using Lootbound.Core.Bootstrap;
using Lootbound.Core.Scenes;

namespace Lootbound.Debugging
{
    /// <summary>
    /// System debug overlay showing FPS, scene info, and build status.
    /// Toggle with F3.
    /// </summary>
    public class DebugOverlay : MonoBehaviour
    {
        [SerializeField] private bool showOnStart = true;
        [SerializeField] private Key toggleKey = Key.F3;

        private bool isVisible;
        private float deltaTime;

        // Styles matching WearMetrics
        private GUIStyle boxStyle;
        private GUIStyle labelStyle;
        private GUIStyle headerStyle;
        private GUIStyle subHeaderStyle;

        private void Start()
        {
            isVisible = showOnStart;
        }

        private void Update()
        {
            deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;

            if (Keyboard.current != null && Keyboard.current[toggleKey].wasPressedThisFrame)
            {
                isVisible = !isVisible;
            }
        }

        private void OnGUI()
        {
            if (!isVisible)
            {
                return;
            }

            InitializeStyles();
            DrawOverlay();
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

        private void DrawOverlay()
        {
            float fps = 1.0f / deltaTime;
            string sceneName = SceneLoader.GetActiveSceneName();
            string bootstrapStatus = GameBootstrap.IsBootstrapped ? "Ready" : "Not initialized";
            string gameVersion = GameBootstrap.GameConfig?.GameVersion ?? "Unknown";
            string unityVersion = Application.unityVersion;
            bool isDevBuild = Debug.isDebugBuild;

            float width = 260f;
            float height = 155f;
            float x = 10f;
            float y = 10f;

            GUI.Box(new Rect(x, y, width, height), "", boxStyle);

            float lineY = y + 10f;
            float lineHeight = 18f;
            float labelX = x + 10f;
            float valueX = x + 100f;

            // Header
            GUI.Label(new Rect(labelX, lineY, width - 20f, lineHeight), "SYSTEM (F3)", headerStyle);
            lineY += lineHeight + 2f;

            // Version
            GUI.Label(new Rect(labelX, lineY, width - 20f, lineHeight), $"Lootbound v{gameVersion}", subHeaderStyle);
            lineY += lineHeight;

            // FPS
            GUI.Label(new Rect(labelX, lineY, 90f, lineHeight), "FPS:", labelStyle);
            GUI.Label(new Rect(valueX, lineY, 150f, lineHeight), $"{fps:F1}", labelStyle);
            lineY += lineHeight;

            // Scene
            GUI.Label(new Rect(labelX, lineY, 90f, lineHeight), "Scene:", labelStyle);
            GUI.Label(new Rect(valueX, lineY, 150f, lineHeight), sceneName, labelStyle);
            lineY += lineHeight;

            // Unity
            GUI.Label(new Rect(labelX, lineY, 90f, lineHeight), "Unity:", labelStyle);
            GUI.Label(new Rect(valueX, lineY, 150f, lineHeight), unityVersion, labelStyle);
            lineY += lineHeight;

            // Bootstrap
            GUI.Label(new Rect(labelX, lineY, 90f, lineHeight), "Bootstrap:", labelStyle);
            GUI.Label(new Rect(valueX, lineY, 150f, lineHeight), bootstrapStatus, labelStyle);
            lineY += lineHeight;

            // Dev Build
            GUI.Label(new Rect(labelX, lineY, 90f, lineHeight), "Dev Build:", labelStyle);
            GUI.Label(new Rect(valueX, lineY, 150f, lineHeight), isDevBuild ? "Yes" : "No", labelStyle);
        }
    }
}

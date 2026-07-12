using UnityEngine;
using UnityEngine.InputSystem;
using Lootbound.Core.Bootstrap;
using Lootbound.Core.Scenes;

namespace Lootbound.Debugging
{
    public class DebugOverlay : MonoBehaviour
    {
        [SerializeField] private bool showOnStart = true;
        [SerializeField] private Key toggleKey = Key.F3;

        private bool isVisible;
        private float deltaTime;
        private GUIStyle boxStyle;
        private GUIStyle labelStyle;

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

            EnsureStyles();
            DrawOverlay();
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
                    fontSize = 14,
                    normal = { textColor = Color.white }
                };
            }
        }

        private void DrawOverlay()
        {
            float fps = 1.0f / deltaTime;
            string sceneName = SceneLoader.GetActiveSceneName();
            string bootstrapStatus = GameBootstrap.IsBootstrapped ? "Ready" : "Not initialized";
            string gameVersion = GameBootstrap.GameConfig?.GameVersion ?? "Unknown";
            string unityVersion = Application.unityVersion;
            bool isDevBuild = Debug.isDebugBuild;

            int width = 280;
            int height = 140;
            int x = 10;
            int y = 10;

            GUI.Box(new Rect(x, y, width, height), "", boxStyle);

            int lineY = y + 10;
            int lineHeight = 20;

            GUI.Label(new Rect(x + 10, lineY, width - 20, lineHeight), $"Lootbound v{gameVersion}", labelStyle);
            lineY += lineHeight;

            GUI.Label(new Rect(x + 10, lineY, width - 20, lineHeight), $"FPS: {fps:0.0}", labelStyle);
            lineY += lineHeight;

            GUI.Label(new Rect(x + 10, lineY, width - 20, lineHeight), $"Scene: {sceneName}", labelStyle);
            lineY += lineHeight;

            GUI.Label(new Rect(x + 10, lineY, width - 20, lineHeight), $"Unity: {unityVersion}", labelStyle);
            lineY += lineHeight;

            GUI.Label(new Rect(x + 10, lineY, width - 20, lineHeight), $"Bootstrap: {bootstrapStatus}", labelStyle);
            lineY += lineHeight;

            GUI.Label(new Rect(x + 10, lineY, width - 20, lineHeight), $"Dev Build: {(isDevBuild ? "Yes" : "No")}", labelStyle);
        }
    }
}

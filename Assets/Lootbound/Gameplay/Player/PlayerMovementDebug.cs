using UnityEngine;
using UnityEngine.InputSystem;

namespace Lootbound.Gameplay.Player
{
    /// <summary>
    /// Displays movement debug information using OnGUI.
    /// Toggle with F4. Position: Right of System panel (F3), auto-sized.
    /// </summary>
    public class PlayerMovementDebug : MonoBehaviour
    {
        [SerializeField] private FirstPersonMotor motor;
        [SerializeField] private PlayerInputReader inputReader;
        [SerializeField] private PlayerStanceController stanceController;
        [SerializeField] private PlayerCameraController cameraController;

        [Header("Display Settings")]
        [SerializeField] private bool showOnStart = true;
        [SerializeField] private Key toggleKey = Key.F4;

        private bool isVisible;
        private Vector2 scrollPosition;

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
        }

        private void OnGUI()
        {
            if (!isVisible || motor == null) return;

            InitializeStyles();

            float width = 260f;
            float x = 280f; // Right of System panel (10 + 260 + 10 gap)
            float y = 10f;  // Aligned with System panel
            float contentHeight = CalculateContentHeight();
            float maxHeight = Screen.height - 40f;
            float panelHeight = Mathf.Min(contentHeight + 15f, maxHeight);

            GUI.Box(new Rect(x, y, width, panelHeight), "", boxStyle);

            Rect viewRect = new Rect(x + 5f, y + 5f, width - 10f, panelHeight - 10f);
            Rect contentRect = new Rect(0, 0, width - 25f, contentHeight);

            scrollPosition = GUI.BeginScrollView(viewRect, scrollPosition, contentRect);
            DrawDebugContent(width - 25f);
            GUI.EndScrollView();
        }

        private float CalculateContentHeight()
        {
            float lineHeight = 16f;
            float height = 22f; // Header
            height += lineHeight * 3 + 3f; // Ground section (header + grounded + normal + spacing)
            height += lineHeight * 3 + 3f; // Velocity section (header + speed + horizontal + spacing)
            height += lineHeight * 2; // State section (header + movement)
            if (stanceController != null) height += lineHeight; // Stance line (combined with height)
            height += 3f;
            height += lineHeight * 2 + 3f; // Timers section (header + combined coyote/buffer + spacing)
            if (inputReader != null) height += lineHeight * 2; // Input section (header + move)
            return height;
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

        private void DrawDebugContent(float width)
        {
            float lineY = 0f;
            float lineHeight = 16f;
            float labelX = 5f;
            float valueX = 100f;
            float valueWidth = width - valueX;

            // Header
            GUI.Label(new Rect(labelX, lineY, width, 20f), "MOVEMENT (F4)", headerStyle);
            lineY += 22f;

            // Ground section
            GUI.Label(new Rect(labelX, lineY, width, lineHeight), "— Ground —", subHeaderStyle);
            lineY += lineHeight;

            string groundedText = motor.IsGrounded ? "Yes" : "No";
            Color groundedColor = motor.IsGrounded ? new Color(0.6f, 0.9f, 0.6f) : new Color(0.9f, 0.6f, 0.6f);
            var groundedStyle = new GUIStyle(valueStyle) { normal = { textColor = groundedColor } };
            GUI.Label(new Rect(labelX, lineY, 90f, lineHeight), "Grounded:", labelStyle);
            GUI.Label(new Rect(valueX, lineY, valueWidth, lineHeight), $"{groundedText}  Angle: {motor.GroundAngle:F1}°", groundedStyle);
            lineY += lineHeight;

            GUI.Label(new Rect(labelX, lineY, 90f, lineHeight), "Normal:", labelStyle);
            GUI.Label(new Rect(valueX, lineY, valueWidth, lineHeight), FormatVector3(motor.GroundNormal), valueStyle);
            lineY += lineHeight + 3f;

            // Velocity section
            GUI.Label(new Rect(labelX, lineY, width, lineHeight), "— Velocity —", subHeaderStyle);
            lineY += lineHeight;

            GUI.Label(new Rect(labelX, lineY, 90f, lineHeight), "Speed:", labelStyle);
            GUI.Label(new Rect(valueX, lineY, valueWidth, lineHeight), $"{motor.CurrentSpeed:F2} m/s  V: {motor.VerticalVelocity:F2}", valueStyle);
            lineY += lineHeight;

            GUI.Label(new Rect(labelX, lineY, 90f, lineHeight), "Horizontal:", labelStyle);
            GUI.Label(new Rect(valueX, lineY, valueWidth, lineHeight), FormatVector3(motor.HorizontalVelocity), valueStyle);
            lineY += lineHeight + 3f;

            // State section
            GUI.Label(new Rect(labelX, lineY, width, lineHeight), "— State —", subHeaderStyle);
            lineY += lineHeight;

            string sprintText = motor.IsSprinting ? "Sprinting" : "Walking";
            Color sprintColor = motor.IsSprinting ? new Color(0.9f, 0.9f, 0.5f) : new Color(0.8f, 0.8f, 0.8f);
            var sprintStyle = new GUIStyle(valueStyle) { normal = { textColor = sprintColor } };
            GUI.Label(new Rect(labelX, lineY, 90f, lineHeight), "Movement:", labelStyle);
            GUI.Label(new Rect(valueX, lineY, valueWidth, lineHeight), sprintText, sprintStyle);
            lineY += lineHeight;

            if (stanceController != null)
            {
                string stanceText = stanceController.IsCrouching ? "Crouching" : "Standing";
                Color stanceColor = stanceController.IsCrouching ? new Color(0.5f, 0.9f, 0.9f) : new Color(0.8f, 0.8f, 0.8f);
                var stanceStyle = new GUIStyle(valueStyle) { normal = { textColor = stanceColor } };
                GUI.Label(new Rect(labelX, lineY, 90f, lineHeight), "Stance:", labelStyle);
                GUI.Label(new Rect(valueX, lineY, valueWidth, lineHeight), $"{stanceText}  H: {stanceController.CurrentHeight:F2}m", stanceStyle);
                lineY += lineHeight;
            }
            lineY += 3f;

            // Timers section
            GUI.Label(new Rect(labelX, lineY, width, lineHeight), "— Timers —", subHeaderStyle);
            lineY += lineHeight;

            GUI.Label(new Rect(labelX, lineY, 90f, lineHeight), "Coyote:", labelStyle);
            GUI.Label(new Rect(valueX, lineY, valueWidth, lineHeight), $"{motor.CoyoteTimeRemaining:F2}s  Buffer: {motor.JumpBufferRemaining:F2}s", valueStyle);
            lineY += lineHeight + 3f;

            // Input section
            if (inputReader != null)
            {
                GUI.Label(new Rect(labelX, lineY, width, lineHeight), "— Input —", subHeaderStyle);
                lineY += lineHeight;

                Vector2 moveInput = inputReader.GetNormalizedMoveInput();
                GUI.Label(new Rect(labelX, lineY, 90f, lineHeight), "Move:", labelStyle);
                GUI.Label(new Rect(valueX, lineY, valueWidth, lineHeight), $"({moveInput.x:F2}, {moveInput.y:F2})", valueStyle);
            }
        }

        private string FormatVector3(Vector3 v)
        {
            return $"({v.x:F2}, {v.y:F2}, {v.z:F2})";
        }
    }
}

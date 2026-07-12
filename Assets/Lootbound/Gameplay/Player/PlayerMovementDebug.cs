using UnityEngine;
using UnityEngine.InputSystem;

namespace Lootbound.Gameplay.Player
{
    /// <summary>
    /// Displays movement debug information using OnGUI.
    /// Separate from the main DebugOverlay to show player-specific data.
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
        private float deltaTime;
        private GUIStyle boxStyle;
        private GUIStyle labelStyle;
        private GUIStyle headerStyle;

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
            if (!isVisible || motor == null) return;

            EnsureStyles();
            DrawDebugPanel();
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
            int width = 260;
            int height = 400;
            int x = Screen.width - width - 10;
            int y = 10;

            GUI.Box(new Rect(x, y, width, height), "", boxStyle);

            int lineY = y + 10;
            int lineHeight = 18;
            int sectionSpacing = 8;

            // Header
            GUI.Label(new Rect(x + 10, lineY, width - 20, lineHeight), "Movement Debug (F4)", headerStyle);
            lineY += lineHeight + sectionSpacing;

            // FPS
            float fps = 1.0f / deltaTime;
            GUI.Label(new Rect(x + 10, lineY, width - 20, lineHeight), $"FPS: {fps:0.0}", labelStyle);
            lineY += lineHeight;

            // Ground state
            lineY += sectionSpacing;
            GUI.Label(new Rect(x + 10, lineY, width - 20, lineHeight), "Ground", headerStyle);
            lineY += lineHeight;

            string groundedText = motor.IsGrounded ? "<color=green>Yes</color>" : "<color=red>No</color>";
            DrawLabel(x, ref lineY, lineHeight, $"Grounded: {groundedText}");
            DrawLabel(x, ref lineY, lineHeight, $"Ground Angle: {motor.GroundAngle:0.0}°");
            DrawLabel(x, ref lineY, lineHeight, $"Ground Normal: {FormatVector3(motor.GroundNormal)}");

            // Velocity
            lineY += sectionSpacing;
            GUI.Label(new Rect(x + 10, lineY, width - 20, lineHeight), "Velocity", headerStyle);
            lineY += lineHeight;

            DrawLabel(x, ref lineY, lineHeight, $"Speed: {motor.CurrentSpeed:0.00} m/s");
            DrawLabel(x, ref lineY, lineHeight, $"Horizontal: {FormatVector3(motor.HorizontalVelocity)}");
            DrawLabel(x, ref lineY, lineHeight, $"Vertical: {motor.VerticalVelocity:0.00} m/s");

            // State
            lineY += sectionSpacing;
            GUI.Label(new Rect(x + 10, lineY, width - 20, lineHeight), "State", headerStyle);
            lineY += lineHeight;

            string sprintText = motor.IsSprinting ? "<color=yellow>Sprinting</color>" : "Walking";
            DrawLabel(x, ref lineY, lineHeight, $"Movement: {sprintText}");

            if (stanceController != null)
            {
                string stanceText = stanceController.IsCrouching ? "<color=cyan>Crouching</color>" : "Standing";
                DrawLabel(x, ref lineY, lineHeight, $"Stance: {stanceText}");
                DrawLabel(x, ref lineY, lineHeight, $"Height: {stanceController.CurrentHeight:0.00}m");
            }

            // Timers
            lineY += sectionSpacing;
            GUI.Label(new Rect(x + 10, lineY, width - 20, lineHeight), "Timers", headerStyle);
            lineY += lineHeight;

            DrawLabel(x, ref lineY, lineHeight, $"Coyote: {motor.CoyoteTimeRemaining:0.00}s");
            DrawLabel(x, ref lineY, lineHeight, $"Jump Buffer: {motor.JumpBufferRemaining:0.00}s");

            // Input
            if (inputReader != null)
            {
                lineY += sectionSpacing;
                GUI.Label(new Rect(x + 10, lineY, width - 20, lineHeight), "Input", headerStyle);
                lineY += lineHeight;

                Vector2 moveInput = inputReader.GetNormalizedMoveInput();
                DrawLabel(x, ref lineY, lineHeight, $"Move: ({moveInput.x:0.00}, {moveInput.y:0.00})");
            }
        }

        private void DrawLabel(int x, ref int lineY, int lineHeight, string text)
        {
            // Use richtext for colored labels
            GUIStyle richStyle = new GUIStyle(labelStyle) { richText = true };
            GUI.Label(new Rect(x + 10, lineY, 240, lineHeight), text, richStyle);
            lineY += lineHeight;
        }

        private string FormatVector3(Vector3 v)
        {
            return $"({v.x:0.00}, {v.y:0.00}, {v.z:0.00})";
        }
    }
}

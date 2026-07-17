using UnityEngine;
using UnityEngine.InputSystem;
using Lootbound.Gameplay.Expeditions;
using Lootbound.Gameplay.Combat;

namespace Lootbound.Debugging
{
    /// <summary>
    /// Debug panel for expedition systems.
    /// Toggle with F8. Uses OnGUI for immediate mode rendering.
    /// </summary>
    public class ExpeditionDebugPanel : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private ExpeditionLifecycle lifecycle;
        [SerializeField] private RefugeZone refugeZone;
        [SerializeField] private ExpeditionBoundary boundary;
        [SerializeField] private Transform playerTransform;

        [Header("Settings")]
        [SerializeField] private bool showOnStart = false;

        private bool isVisible;

        // Styles
        private GUIStyle boxStyle;
        private GUIStyle labelStyle;
        private GUIStyle headerStyle;
        private GUIStyle subHeaderStyle;
        private GUIStyle buttonStyle;
        private GUIStyle valueStyle;
        private GUIStyle stateStyle;

        // Scroll position
        private Vector2 scrollPosition;

        private void Start()
        {
            isVisible = showOnStart;
            AutoFindDependencies();
        }

        private void AutoFindDependencies()
        {
            if (lifecycle == null)
                lifecycle = FindFirstObjectByType<ExpeditionLifecycle>();

            if (refugeZone == null)
                refugeZone = FindFirstObjectByType<RefugeZone>();

            if (boundary == null)
                boundary = FindFirstObjectByType<ExpeditionBoundary>();

            if (playerTransform == null)
            {
                var playerHealth = FindFirstObjectByType<PlayerHealth>();
                if (playerHealth != null)
                    playerTransform = playerHealth.transform;
            }
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f8Key.wasPressedThisFrame)
            {
                isVisible = !isVisible;
            }
        }

        private void OnGUI()
        {
            if (!isVisible) return;

            InitializeStyles();

            float width = 280f;
            float x = Screen.width - width - 10f; // Right side
            float y = 10f;

            float contentHeight = CalculateContentHeight();
            float maxHeight = Screen.height - 40f;
            float panelHeight = Mathf.Min(contentHeight + 15f, maxHeight);

            GUI.Box(new Rect(x, y, width, panelHeight), "", boxStyle);

            scrollPosition = GUI.BeginScrollView(
                new Rect(x, y, width, panelHeight),
                scrollPosition,
                new Rect(0, 0, width - 20f, contentHeight),
                false, contentHeight > panelHeight);

            float lineY = 8f;
            float lineHeight = 18f;
            float labelX = 8f;
            float valueX = 120f;
            float valueWidth = 150f;

            // Header
            GUI.Label(new Rect(labelX, lineY, width - 16f, lineHeight), "EXPEDITION (F8)", headerStyle);
            lineY += lineHeight + 4f;

            // State Section
            lineY = DrawStateSection(lineY, lineHeight, labelX, valueX, valueWidth, width);

            // Refuge Section
            lineY = DrawRefugeSection(lineY, lineHeight, labelX, valueX, valueWidth, width);

            // Control Buttons
            lineY = DrawControlButtons(lineY, lineHeight, labelX, width);

            // Metrics Section
            lineY = DrawMetricsSection(lineY, lineHeight, labelX, valueX, valueWidth, width);

            // Snapshot Section
            lineY = DrawSnapshotSection(lineY, lineHeight, labelX, valueX, valueWidth, width);

            GUI.EndScrollView();
        }

        private float CalculateContentHeight()
        {
            float lineHeight = 18f;
            float height = 8f; // Initial padding

            height += lineHeight + 4f; // Header

            // State section
            height += lineHeight; // Section header
            height += lineHeight * 3; // State, Seed, ID

            // Refuge section
            height += lineHeight; // Section header
            height += lineHeight * 4; // Inside, Side, Cooldown, Crossings
            height += 26f + 4f; // Teleport buttons

            // Control buttons
            height += lineHeight + 4f; // Section header
            height += 26f * 2 + 8f; // Two rows of buttons

            // Metrics section
            height += lineHeight; // Section header
            height += lineHeight * 6; // Duration, Distance, Kills, Items, Equipment, Main Weapon

            // Snapshot section
            height += lineHeight; // Section header
            height += lineHeight * 2; // Weapon info

            height += 16f; // Bottom padding
            return height;
        }

        private void InitializeStyles()
        {
            if (boxStyle != null) return;

            boxStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = CreateColorTexture(new Color(0.1f, 0.1f, 0.1f, 0.9f)) }
            };

            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                normal = { textColor = new Color(0.85f, 0.85f, 0.85f) }
            };

            headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.7f, 0.9f, 1f) }
            };

            subHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.6f, 0.8f, 0.6f) }
            };

            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 10
            };

            valueStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                normal = { textColor = Color.white },
                alignment = TextAnchor.MiddleRight
            };

            stateStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight
            };
        }

        private Texture2D CreateColorTexture(Color color)
        {
            var texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        private float DrawStateSection(float lineY, float lineHeight, float labelX, float valueX, float valueWidth, float width)
        {
            GUI.Label(new Rect(labelX, lineY, width - 16f, lineHeight), "State", subHeaderStyle);
            lineY += lineHeight;

            var state = lifecycle != null ? lifecycle.State : ExpeditionState.None;
            var session = lifecycle?.CurrentSession;

            // State with color
            GUI.Label(new Rect(labelX, lineY, 100f, lineHeight), "Status:", labelStyle);
            stateStyle.normal.textColor = GetStateColor(state);
            GUI.Label(new Rect(valueX, lineY, valueWidth, lineHeight), state.ToString(), stateStyle);
            lineY += lineHeight;

            // Seed
            GUI.Label(new Rect(labelX, lineY, 100f, lineHeight), "Seed:", labelStyle);
            string seed = session != null ? session.WorldSeed.ToString() : "-";
            GUI.Label(new Rect(valueX, lineY, valueWidth, lineHeight), seed, valueStyle);
            lineY += lineHeight;

            // Session ID (shortened)
            GUI.Label(new Rect(labelX, lineY, 100f, lineHeight), "Session:", labelStyle);
            string sessionId = session != null ? session.ExpeditionId[..8] + "..." : "-";
            GUI.Label(new Rect(valueX, lineY, valueWidth, lineHeight), sessionId, valueStyle);
            lineY += lineHeight;

            return lineY;
        }

        private float DrawRefugeSection(float lineY, float lineHeight, float labelX, float valueX, float valueWidth, float width)
        {
            GUI.Label(new Rect(labelX, lineY, width - 16f, lineHeight), "Refuge", subHeaderStyle);
            lineY += lineHeight;

            // Player inside refuge
            GUI.Label(new Rect(labelX, lineY, 100f, lineHeight), "Inside Refuge:", labelStyle);
            bool inside = refugeZone != null && refugeZone.IsPlayerInside;
            valueStyle.normal.textColor = inside ? new Color(0.4f, 0.9f, 0.4f) : new Color(0.9f, 0.6f, 0.4f);
            GUI.Label(new Rect(valueX, lineY, valueWidth, lineHeight), inside ? "Yes" : "No", valueStyle);
            valueStyle.normal.textColor = Color.white;
            lineY += lineHeight;

            // Current side at boundary
            GUI.Label(new Rect(labelX, lineY, 100f, lineHeight), "Boundary Side:", labelStyle);
            string side = boundary != null ? boundary.CurrentSide.ToString() : "-";
            GUI.Label(new Rect(valueX, lineY, valueWidth, lineHeight), side, valueStyle);
            lineY += lineHeight;

            // Boundary cooldown
            GUI.Label(new Rect(labelX, lineY, 100f, lineHeight), "Cooldown:", labelStyle);
            string cooldown = boundary != null ? $"{boundary.CooldownRemaining:F1}s" : "-";
            GUI.Label(new Rect(valueX, lineY, valueWidth, lineHeight), cooldown, valueStyle);
            lineY += lineHeight;

            // Crossing count
            GUI.Label(new Rect(labelX, lineY, 100f, lineHeight), "Crossings:", labelStyle);
            string crossings = boundary != null ? boundary.CrossingCount.ToString() : "0";
            GUI.Label(new Rect(valueX, lineY, valueWidth, lineHeight), crossings, valueStyle);
            lineY += lineHeight;

            // Teleport buttons
            float buttonWidth = 80f;
            float buttonHeight = 22f;
            float buttonX = labelX;

            GUI.enabled = refugeZone != null && playerTransform != null;
            if (GUI.Button(new Rect(buttonX, lineY, buttonWidth, buttonHeight), "To Refuge", buttonStyle))
            {
                TeleportToRefuge();
            }
            buttonX += buttonWidth + 4f;

            if (GUI.Button(new Rect(buttonX, lineY, buttonWidth, buttonHeight), "To Outside", buttonStyle))
            {
                TeleportOutsideRefuge();
            }

            GUI.enabled = true;
            lineY += buttonHeight + 4f;

            return lineY;
        }

        private void TeleportToRefuge()
        {
            if (refugeZone == null || playerTransform == null) return;

            // Teleport to center of refuge
            Vector3 targetPos = refugeZone.Center;
            targetPos.y = playerTransform.position.y; // Keep current height
            playerTransform.position = targetPos;
        }

        private void TeleportOutsideRefuge()
        {
            if (refugeZone == null || playerTransform == null) return;

            // Teleport just outside refuge radius
            Vector3 direction = playerTransform.forward;
            if (direction.sqrMagnitude < 0.1f)
            {
                direction = Vector3.forward;
            }
            direction.y = 0;
            direction.Normalize();

            Vector3 targetPos = refugeZone.Center + direction * (refugeZone.Radius + 5f);
            targetPos.y = playerTransform.position.y;
            playerTransform.position = targetPos;
        }

        private float DrawControlButtons(float lineY, float lineHeight, float labelX, float width)
        {
            GUI.Label(new Rect(labelX, lineY, width - 16f, lineHeight), "Controls", subHeaderStyle);
            lineY += lineHeight + 4f;

            float buttonWidth = 60f;
            float buttonHeight = 22f;
            float buttonX = labelX;

            var state = lifecycle != null ? lifecycle.State : ExpeditionState.None;

            // Row 1: Start, Depart, Complete
            GUI.enabled = lifecycle != null && state == ExpeditionState.None;
            if (GUI.Button(new Rect(buttonX, lineY, buttonWidth, buttonHeight), "Start", buttonStyle))
            {
                lifecycle?.StartExpedition();
            }
            buttonX += buttonWidth + 4f;

            GUI.enabled = lifecycle != null && state == ExpeditionState.Preparing;
            if (GUI.Button(new Rect(buttonX, lineY, buttonWidth, buttonHeight), "Depart", buttonStyle))
            {
                lifecycle?.Depart();
            }
            buttonX += buttonWidth + 4f;

            GUI.enabled = lifecycle != null && (state == ExpeditionState.Active || state == ExpeditionState.Returning);
            if (GUI.Button(new Rect(buttonX, lineY, buttonWidth, buttonHeight), "Complete", buttonStyle))
            {
                lifecycle?.CompleteExpedition();
            }
            buttonX += buttonWidth + 4f;

            GUI.enabled = lifecycle != null && state == ExpeditionState.Active;
            if (GUI.Button(new Rect(buttonX, lineY, buttonWidth, buttonHeight), "Return", buttonStyle))
            {
                lifecycle?.BeginReturn();
            }

            lineY += buttonHeight + 4f;
            buttonX = labelX;

            // Row 2: Cancel, Clear
            GUI.enabled = lifecycle != null && lifecycle.IsExpeditionActive;
            if (GUI.Button(new Rect(buttonX, lineY, buttonWidth, buttonHeight), "Cancel", buttonStyle))
            {
                lifecycle?.CancelExpedition();
            }
            buttonX += buttonWidth + 4f;

            GUI.enabled = lifecycle != null && lifecycle.CurrentSession != null && lifecycle.CurrentSession.HasEnded;
            if (GUI.Button(new Rect(buttonX, lineY, buttonWidth, buttonHeight), "Clear", buttonStyle))
            {
                lifecycle?.ClearSession();
            }

            GUI.enabled = true;
            lineY += buttonHeight + 4f;

            return lineY;
        }

        private float DrawMetricsSection(float lineY, float lineHeight, float labelX, float valueX, float valueWidth, float width)
        {
            GUI.Label(new Rect(labelX, lineY, width - 16f, lineHeight), "Metrics", subHeaderStyle);
            lineY += lineHeight;

            var metrics = lifecycle?.CurrentSession?.Metrics;

            // Duration
            GUI.Label(new Rect(labelX, lineY, 100f, lineHeight), "Duration:", labelStyle);
            string duration = metrics != null ? metrics.DurationFormatted : "00:00";
            GUI.Label(new Rect(valueX, lineY, valueWidth, lineHeight), duration, valueStyle);
            lineY += lineHeight;

            // Max Distance
            GUI.Label(new Rect(labelX, lineY, 100f, lineHeight), "Max Distance:", labelStyle);
            string distance = metrics != null ? $"{metrics.MaxDistance:F1}m" : "0.0m";
            GUI.Label(new Rect(valueX, lineY, valueWidth, lineHeight), distance, valueStyle);
            lineY += lineHeight;

            // Enemies Defeated
            GUI.Label(new Rect(labelX, lineY, 100f, lineHeight), "Kills:", labelStyle);
            string kills = metrics != null ? metrics.EnemiesDefeated.ToString() : "0";
            GUI.Label(new Rect(valueX, lineY, valueWidth, lineHeight), kills, valueStyle);
            lineY += lineHeight;

            // Items Acquired
            GUI.Label(new Rect(labelX, lineY, 100f, lineHeight), "Items:", labelStyle);
            string items = metrics != null ? metrics.ItemsAcquired.ToString() : "0";
            GUI.Label(new Rect(valueX, lineY, valueWidth, lineHeight), items, valueStyle);
            lineY += lineHeight;

            // Equipment Acquired
            GUI.Label(new Rect(labelX, lineY, 100f, lineHeight), "Equipment:", labelStyle);
            string equipment = metrics != null ? metrics.EquipmentAcquired.ToString() : "0";
            GUI.Label(new Rect(valueX, lineY, valueWidth, lineHeight), equipment, valueStyle);
            lineY += lineHeight;

            // Main Weapon
            GUI.Label(new Rect(labelX, lineY, 100f, lineHeight), "Main Weapon:", labelStyle);
            string mainWeapon = "-";
            if (metrics?.MainWeapon != null)
            {
                mainWeapon = $"{metrics.MainWeapon.CustomName} ({metrics.MainWeaponKills})";
            }
            GUI.Label(new Rect(valueX, lineY, valueWidth, lineHeight), mainWeapon, valueStyle);
            lineY += lineHeight;

            return lineY;
        }

        private float DrawSnapshotSection(float lineY, float lineHeight, float labelX, float valueX, float valueWidth, float width)
        {
            GUI.Label(new Rect(labelX, lineY, width - 16f, lineHeight), "Departure Snapshot", subHeaderStyle);
            lineY += lineHeight;

            var snapshot = lifecycle?.CurrentSession?.Snapshot;

            // Equipped Weapon
            GUI.Label(new Rect(labelX, lineY, 100f, lineHeight), "Weapon:", labelStyle);
            string weapon = "-";
            if (snapshot != null && snapshot.HadWeaponEquipped)
            {
                string attunement = snapshot.EquippedWeaponAttunement > 0
                    ? $" +{snapshot.EquippedWeaponAttunement}"
                    : "";
                weapon = $"{snapshot.EquippedWeaponName}{attunement}";
            }
            GUI.Label(new Rect(valueX, lineY, valueWidth, lineHeight), weapon, valueStyle);
            lineY += lineHeight;

            return lineY;
        }

        private Color GetStateColor(ExpeditionState state)
        {
            return state switch
            {
                ExpeditionState.None => new Color(0.6f, 0.6f, 0.6f),
                ExpeditionState.Preparing => new Color(0.9f, 0.9f, 0.4f),
                ExpeditionState.Departing => new Color(0.9f, 0.7f, 0.3f),
                ExpeditionState.Active => new Color(0.4f, 0.9f, 0.4f),
                ExpeditionState.Returning => new Color(0.4f, 0.7f, 0.9f),
                ExpeditionState.Completed => new Color(0.3f, 1f, 0.3f),
                ExpeditionState.Failed => new Color(1f, 0.3f, 0.3f),
                ExpeditionState.Cancelled => new Color(0.7f, 0.5f, 0.3f),
                _ => Color.white
            };
        }
    }
}

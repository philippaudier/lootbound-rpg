using UnityEngine;
using UnityEngine.InputSystem;

namespace Lootbound.Gameplay.Equipment
{
    /// <summary>
    /// Tracks wear and repair metrics for tuning and playtesting.
    /// Slice 0.7.7: Debug tool for measuring system balance.
    /// Toggle visibility with F7. Press F7+Shift to reset.
    /// Note: Metrics are cumulative for the entire session (all weapons).
    /// </summary>
    public class WearMetricsTracker : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerWeaponWear playerWeaponWear;
        [SerializeField] private PlayerRepair playerRepair;
        [SerializeField] private PlayerEquipment playerEquipment;

        [Header("Display")]
        [SerializeField] private bool showOnStart = false;

        // Metrics
        private int wearEvents;         // Number of times OnWearApplied fired
        private int wearApplied;        // Number of times durability was actually lost
        private int conditionChanges;
        private int brokenCount;
        private int repairsPerformed;
        private float totalDurabilityLost;
        private float totalDurabilityRestored;
        private float sessionStartTime;
        private string currentWeaponName;

        private bool isVisible;
        private GUIStyle boxStyle;
        private GUIStyle labelStyle;
        private GUIStyle headerStyle;
        private GUIStyle subHeaderStyle;

        private void Awake()
        {
            AutoFindReferences();
        }

        private void Start()
        {
            isVisible = showOnStart;
            sessionStartTime = Time.time;
        }

        private void OnEnable()
        {
            if (playerWeaponWear != null)
            {
                playerWeaponWear.OnWearApplied += HandleWearApplied;
                playerWeaponWear.OnConditionChanged += HandleConditionChanged;
            }

            if (playerRepair != null)
            {
                playerRepair.OnRepairCompleted += HandleRepairCompleted;
            }

            if (playerEquipment != null)
            {
                playerEquipment.OnWeaponEquipped += HandleWeaponEquipped;
            }
        }

        private void OnDisable()
        {
            if (playerWeaponWear != null)
            {
                playerWeaponWear.OnWearApplied -= HandleWearApplied;
                playerWeaponWear.OnConditionChanged -= HandleConditionChanged;
            }

            if (playerRepair != null)
            {
                playerRepair.OnRepairCompleted -= HandleRepairCompleted;
            }

            if (playerEquipment != null)
            {
                playerEquipment.OnWeaponEquipped -= HandleWeaponEquipped;
            }
        }

        private void AutoFindReferences()
        {
            if (playerWeaponWear == null)
                playerWeaponWear = FindFirstObjectByType<PlayerWeaponWear>();

            if (playerRepair == null)
                playerRepair = FindFirstObjectByType<PlayerRepair>();

            if (playerEquipment == null)
                playerEquipment = FindFirstObjectByType<PlayerEquipment>();
        }

        private void Update()
        {
            if (Keyboard.current == null) return;

            if (Keyboard.current.f7Key.wasPressedThisFrame)
            {
                if (Keyboard.current.shiftKey.isPressed)
                {
                    ResetMetrics();
                    Debug.Log("[WearMetrics] Metrics reset");
                }
                else
                {
                    isVisible = !isVisible;
                }
            }

            // Update current weapon name
            if (playerEquipment != null && playerEquipment.HasWeaponEquipped)
            {
                currentWeaponName = playerEquipment.CurrentEquipment?.CustomName ?? "Unknown";
            }
            else
            {
                currentWeaponName = "None";
            }
        }

        private void HandleWeaponEquipped(Inventory.ItemInstance item)
        {
            if (item?.EquipmentData != null)
            {
                currentWeaponName = item.EquipmentData.CustomName ?? "Unknown";
            }
        }

        private void HandleWearApplied(WearResult result)
        {
            wearEvents++;

            if (result.WearApplied)
            {
                wearApplied++;
                totalDurabilityLost += result.DurabilityLost;
            }
        }

        private void HandleConditionChanged(WearResult result)
        {
            if (result.ConditionChanged)
            {
                conditionChanges++;

                if (result.NowBroken)
                {
                    brokenCount++;
                }
            }
        }

        private void HandleRepairCompleted(RepairResult result)
        {
            if (result.Success)
            {
                repairsPerformed++;
                totalDurabilityRestored += result.DurabilityRestored;
            }
        }

        private void OnGUI()
        {
            if (!isVisible) return;

            InitializeStyles();

            float width = 280f;
            float height = 260f;
            float x = Screen.width - width - 20f;
            float y = Screen.height - height - 20f;

            GUI.Box(new Rect(x, y, width, height), "", boxStyle);

            float lineY = y + 10f;
            float lineHeight = 18f;
            float labelX = x + 10f;
            float valueX = x + 170f;

            // Header
            GUI.Label(new Rect(labelX, lineY, width - 20f, lineHeight), "WEAR METRICS (F7)", headerStyle);
            lineY += lineHeight + 2f;

            // Session info
            float sessionTime = Time.time - sessionStartTime;
            GUI.Label(new Rect(labelX, lineY, width - 20f, lineHeight),
                $"Session: {sessionTime / 60f:F1} min | Shift+F7 = reset", subHeaderStyle);
            lineY += lineHeight;

            // Current weapon
            GUI.Label(new Rect(labelX, lineY, 160f, lineHeight), "Current weapon:", labelStyle);
            GUI.Label(new Rect(valueX, lineY, 100f, lineHeight), currentWeaponName, labelStyle);
            lineY += lineHeight + 5f;

            // Wear section
            GUI.Label(new Rect(labelX, lineY, width - 20f, lineHeight), "— Wear (all weapons) —", subHeaderStyle);
            lineY += lineHeight;

            GUI.Label(new Rect(labelX, lineY, 160f, lineHeight), "Wear events:", labelStyle);
            GUI.Label(new Rect(valueX, lineY, 100f, lineHeight), wearEvents.ToString(), labelStyle);
            lineY += lineHeight;

            GUI.Label(new Rect(labelX, lineY, 160f, lineHeight), "Durability lost:", labelStyle);
            GUI.Label(new Rect(valueX, lineY, 100f, lineHeight), wearApplied.ToString(), labelStyle);
            lineY += lineHeight;

            GUI.Label(new Rect(labelX, lineY, 160f, lineHeight), "Total durability lost:", labelStyle);
            GUI.Label(new Rect(valueX, lineY, 100f, lineHeight), $"{totalDurabilityLost:F0}", labelStyle);
            lineY += lineHeight;

            GUI.Label(new Rect(labelX, lineY, 160f, lineHeight), "Condition changes:", labelStyle);
            GUI.Label(new Rect(valueX, lineY, 100f, lineHeight), conditionChanges.ToString(), labelStyle);
            lineY += lineHeight;

            GUI.Label(new Rect(labelX, lineY, 160f, lineHeight), "Times broken:", labelStyle);
            GUI.Label(new Rect(valueX, lineY, 100f, lineHeight), brokenCount.ToString(), labelStyle);
            lineY += lineHeight + 5f;

            // Repair section
            GUI.Label(new Rect(labelX, lineY, width - 20f, lineHeight), "— Repair —", subHeaderStyle);
            lineY += lineHeight;

            GUI.Label(new Rect(labelX, lineY, 160f, lineHeight), "Repairs performed:", labelStyle);
            GUI.Label(new Rect(valueX, lineY, 100f, lineHeight), repairsPerformed.ToString(), labelStyle);
            lineY += lineHeight;

            GUI.Label(new Rect(labelX, lineY, 160f, lineHeight), "Durability restored:", labelStyle);
            GUI.Label(new Rect(valueX, lineY, 100f, lineHeight), $"{totalDurabilityRestored:F0}", labelStyle);
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

        /// <summary>
        /// Reset all metrics.
        /// </summary>
        public void ResetMetrics()
        {
            wearEvents = 0;
            wearApplied = 0;
            conditionChanges = 0;
            brokenCount = 0;
            repairsPerformed = 0;
            totalDurabilityLost = 0f;
            totalDurabilityRestored = 0f;
            sessionStartTime = Time.time;
        }

        /// <summary>
        /// Log current metrics to console.
        /// </summary>
        public void LogMetrics()
        {
            float sessionTime = Time.time - sessionStartTime;

            Debug.Log($"[WearMetrics] Session: {sessionTime / 60f:F1} min | " +
                $"Events: {wearEvents} | Applied: {wearApplied} | " +
                $"Durability lost: {totalDurabilityLost:F0} | " +
                $"Condition changes: {conditionChanges} | Broken: {brokenCount} | " +
                $"Repairs: {repairsPerformed} | Restored: {totalDurabilityRestored:F0}");
        }
    }
}

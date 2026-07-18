using UnityEngine;
using UnityEngine.InputSystem;
using Lootbound.Gameplay.Combat;
using Lootbound.Gameplay.Player;
using Lootbound.Gameplay.Equipment;
using Lootbound.Gameplay.Inventory;

namespace Lootbound.Debugging
{
    /// <summary>
    /// Debug overlay for combat systems.
    /// Toggle with F6. Uses OnGUI for immediate mode rendering.
    /// Positioned below System panel (F3), auto-sized.
    /// </summary>
    public class CombatDebugOverlay : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private PlayerDodge playerDodge;
        [SerializeField] private PlayerMeleeWeapon playerWeapon;
        [SerializeField] private PlayerCombatController combatController;

        [Header("Equipment")]
        [SerializeField] private PlayerEquipment playerEquipment;
        [SerializeField] private PlayerWeaponWear playerWeaponWear;
        [SerializeField] private PlayerRepair playerRepair;
        [SerializeField] private PlayerInventory playerInventory;

        [Header("Enemy (Optional)")]
        [SerializeField] private EnemyHealth trackedEnemy;
        [SerializeField] private EnemyBrain trackedEnemyBrain;

        [Header("Settings")]
        [SerializeField] private bool showOnStart = false;

        private bool isVisible;

        // Styles matching WearMetrics
        private GUIStyle boxStyle;
        private GUIStyle labelStyle;
        private GUIStyle headerStyle;
        private GUIStyle subHeaderStyle;
        private GUIStyle buttonStyle;
        private GUIStyle valueStyle;

        // Scroll position for long content
        private Vector2 scrollPosition;

        private void Start()
        {
            isVisible = showOnStart;
            AutoFindDependencies();
        }

        private void AutoFindDependencies()
        {
            if (playerHealth == null)
                playerHealth = FindFirstObjectByType<PlayerHealth>();

            if (playerDodge == null)
                playerDodge = FindFirstObjectByType<PlayerDodge>();

            if (playerWeapon == null)
                playerWeapon = FindFirstObjectByType<PlayerMeleeWeapon>();

            if (combatController == null)
                combatController = FindFirstObjectByType<PlayerCombatController>();

            if (trackedEnemy == null)
                trackedEnemy = FindFirstObjectByType<EnemyHealth>();

            if (trackedEnemyBrain == null)
                trackedEnemyBrain = FindFirstObjectByType<EnemyBrain>();

            if (playerEquipment == null)
                playerEquipment = FindFirstObjectByType<PlayerEquipment>();

            if (playerWeaponWear == null)
                playerWeaponWear = FindFirstObjectByType<PlayerWeaponWear>();

            if (playerRepair == null)
                playerRepair = FindFirstObjectByType<PlayerRepair>();

            if (playerInventory == null)
                playerInventory = FindFirstObjectByType<PlayerInventory>();
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f6Key.wasPressedThisFrame)
            {
                isVisible = !isVisible;
            }
        }

        private void OnGUI()
        {
            if (!isVisible) return;

            InitializeStyles();

            float width = 260f;
            float x = 10f;   // Aligned with System panel
            float y = 175f;  // Below System panel (10 + 155 + 10 gap)

            // Calculate actual content height
            float contentHeight = CalculateContentHeight();
            float maxHeight = Screen.height - 40f;
            float panelHeight = Mathf.Min(contentHeight + 15f, maxHeight);

            GUI.Box(new Rect(x, y, width, panelHeight), "", boxStyle);

            // Scroll view if content is too tall
            scrollPosition = GUI.BeginScrollView(
                new Rect(x, y, width, panelHeight),
                scrollPosition,
                new Rect(0, 0, width - 20f, contentHeight),
                false, contentHeight > panelHeight);

            float lineY = 8f;
            float lineHeight = 16f;
            float labelX = 8f;
            float valueX = 110f;
            float valueWidth = 150f;

            // Header
            GUI.Label(new Rect(labelX, lineY, width - 16f, lineHeight), "COMBAT DEBUG (F6)", headerStyle);
            lineY += lineHeight + 4f;

            // Player Section
            lineY = DrawPlayerSection(lineY, lineHeight, labelX, valueX, valueWidth, width);

            // Attack Section
            lineY = DrawAttackSection(lineY, lineHeight, labelX, valueX, valueWidth, width);

            // Dodge Section
            lineY = DrawDodgeSection(lineY, lineHeight, labelX, valueX, valueWidth, width);

            // Equipment Section
            lineY = DrawEquipmentSection(lineY, lineHeight, labelX, valueX, valueWidth, width);

            // Enemy Section
            lineY = DrawEnemySection(lineY, lineHeight, labelX, valueX, valueWidth, width);

            GUI.EndScrollView();
        }

        private float CalculateContentHeight()
        {
            float lineHeight = 16f;
            float height = 24f; // Header

            // Player: header + 1 line + spacing
            height += lineHeight + lineHeight + 3f;

            // Attack: header + 2 lines + spacing (or 1 if not found)
            height += lineHeight + (playerWeapon != null ? lineHeight * 2 : lineHeight) + 3f;

            // Dodge: header + 1 line + spacing
            height += lineHeight + lineHeight + 3f;

            // Equipment
            if (playerEquipment != null && playerEquipment.HasWeaponEquipped)
            {
                // Header + 7 info lines + 3 button rows + repair section + history section
                height += lineHeight; // header
                height += lineHeight * 7; // info lines
                height += 20f * 3 + 8f; // 3 button rows
                height += lineHeight * 2 + 20f + 8f; // repair section
                height += lineHeight * 4 + 20f + 8f; // history section (header + 3 lines + buttons)
            }
            else
            {
                height += lineHeight * 2 + 3f;
            }

            // Enemy: header + up to 2 lines + spacing
            height += lineHeight + lineHeight * 2 + 3f;

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

            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 10,
                padding = new RectOffset(4, 4, 2, 2),
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

        private float DrawPlayerSection(float lineY, float lineHeight, float labelX, float valueX, float valueWidth, float width)
        {
            GUI.Label(new Rect(labelX, lineY, width - 16f, lineHeight), "— Player —", subHeaderStyle);
            lineY += lineHeight;

            if (playerHealth != null)
            {
                GUI.Label(new Rect(labelX, lineY, 50f, lineHeight), "Health:", labelStyle);
                GUI.Label(new Rect(labelX + 50f, lineY, 100f, lineHeight),
                    $"{playerHealth.CurrentHealth:F0}/{playerHealth.MaxHealth:F0}", valueStyle);
            }
            else
            {
                GUI.Label(new Rect(labelX, lineY, width - 16f, lineHeight), "Not found", labelStyle);
            }
            lineY += lineHeight;

            return lineY + 3f;
        }

        private float DrawAttackSection(float lineY, float lineHeight, float labelX, float valueX, float valueWidth, float width)
        {
            GUI.Label(new Rect(labelX, lineY, width - 16f, lineHeight), "— Attack —", subHeaderStyle);
            lineY += lineHeight;

            if (playerWeapon != null)
            {
                // Phase + Attacking on same line
                GUI.Label(new Rect(labelX, lineY, 45f, lineHeight), "Phase:", labelStyle);
                GUI.Label(new Rect(labelX + 45f, lineY, 70f, lineHeight), playerWeapon.CurrentPhase.ToString(), valueStyle);
                GUI.Label(new Rect(labelX + 115f, lineY, 55f, lineHeight), "Atking:", labelStyle);
                GUI.Label(new Rect(labelX + 165f, lineY, 40f, lineHeight), FormatBool(playerWeapon.IsAttacking), valueStyle);
                lineY += lineHeight;

                // Progress + Hits on same line
                GUI.Label(new Rect(labelX, lineY, 50f, lineHeight), "Prog:", labelStyle);
                GUI.Label(new Rect(labelX + 45f, lineY, 50f, lineHeight), $"{playerWeapon.AttackProgress:P0}", valueStyle);
                GUI.Label(new Rect(labelX + 115f, lineY, 35f, lineHeight), "Hits:", labelStyle);
                GUI.Label(new Rect(labelX + 150f, lineY, 30f, lineHeight), playerWeapon.HitsThisAttack.ToString(), valueStyle);
                lineY += lineHeight;
            }
            else
            {
                GUI.Label(new Rect(labelX, lineY, width - 16f, lineHeight), "Not found", labelStyle);
                lineY += lineHeight;
            }

            return lineY + 3f;
        }

        private float DrawDodgeSection(float lineY, float lineHeight, float labelX, float valueX, float valueWidth, float width)
        {
            GUI.Label(new Rect(labelX, lineY, width - 16f, lineHeight), "— Dodge —", subHeaderStyle);
            lineY += lineHeight;

            if (playerDodge != null)
            {
                // Dodging + Invuln on same line
                GUI.Label(new Rect(labelX, lineY, 50f, lineHeight), "Dodge:", labelStyle);
                GUI.Label(new Rect(labelX + 50f, lineY, 35f, lineHeight), FormatBool(playerDodge.IsDodging), valueStyle);
                GUI.Label(new Rect(labelX + 90f, lineY, 45f, lineHeight), "Invul:", labelStyle);
                GUI.Label(new Rect(labelX + 135f, lineY, 35f, lineHeight), FormatBool(playerDodge.IsInvulnerable), valueStyle);
                GUI.Label(new Rect(labelX + 175f, lineY, 30f, lineHeight), "CD:", labelStyle);
                GUI.Label(new Rect(labelX + 200f, lineY, 50f, lineHeight), $"{playerDodge.CooldownRemaining:F1}s", valueStyle);
            }
            else
            {
                GUI.Label(new Rect(labelX, lineY, width - 16f, lineHeight), "Not found", labelStyle);
            }
            lineY += lineHeight;

            return lineY + 3f;
        }

        private float DrawEquipmentSection(float lineY, float lineHeight, float labelX, float valueX, float valueWidth, float width)
        {
            GUI.Label(new Rect(labelX, lineY, width - 16f, lineHeight), "— Equipment —", subHeaderStyle);
            lineY += lineHeight;

            if (playerEquipment == null)
            {
                GUI.Label(new Rect(labelX, lineY, width - 16f, lineHeight), "Not found", labelStyle);
                return lineY + lineHeight + 3f;
            }

            if (!playerEquipment.HasWeaponEquipped)
            {
                GUI.Label(new Rect(labelX, lineY, width - 16f, lineHeight), "No weapon", labelStyle);
                return lineY + lineHeight + 3f;
            }

            var equipment = playerEquipment.CurrentEquipment;
            var stats = playerEquipment.CurrentStats;
            int maxLevel = playerEquipment.AttunementConfig?.MaximumLevel ?? equipment.MaximumAttunementLevel;
            int availableStones = playerEquipment.GetAvailableAttunementStones();
            int failures = equipment.ConsecutiveAttunementFailures;

            // Compact info: Name | Condition | Durability
            GUI.Label(new Rect(labelX, lineY, 70f, lineHeight), "Weapon:", labelStyle);
            GUI.Label(new Rect(labelX + 55f, lineY, valueWidth, lineHeight),
                equipment.CustomName ?? equipment.DefinitionId, valueStyle);
            lineY += lineHeight;

            // Condition + Durability on same line
            GUI.Label(new Rect(labelX, lineY, 55f, lineHeight), "Status:", labelStyle);
            GUI.Label(new Rect(labelX + 55f, lineY, 80f, lineHeight),
                $"{equipment.Condition}", valueStyle);
            GUI.Label(new Rect(labelX + 130f, lineY, 120f, lineHeight),
                $"({equipment.CurrentDurability:F0}/{equipment.MaxDurability:F0})", valueStyle);
            lineY += lineHeight;

            // Damage + Speed on same line
            if (stats.IsValid)
            {
                GUI.Label(new Rect(labelX, lineY, 55f, lineHeight), "Stats:", labelStyle);
                GUI.Label(new Rect(labelX + 55f, lineY, 80f, lineHeight),
                    $"Dmg:{stats.Damage:F0}", valueStyle);
                GUI.Label(new Rect(labelX + 130f, lineY, 80f, lineHeight),
                    $"Spd:{stats.AttackSpeed:F2}", valueStyle);
                lineY += lineHeight;
            }

            // Attunement + Bonus on same line
            GUI.Label(new Rect(labelX, lineY, 55f, lineHeight), "Attune:", labelStyle);
            string attuneText = $"+{equipment.AttunementLevel}/+{maxLevel}";
            if (equipment.IsAttuned && playerEquipment.AttunementConfig != null)
            {
                float bonusPercent = playerEquipment.AttunementConfig.GetDamageBonusPercent(equipment.AttunementLevel);
                attuneText += $" (+{bonusPercent:F0}%)";
            }
            GUI.Label(new Rect(labelX + 55f, lineY, 150f, lineHeight), attuneText, valueStyle);
            lineY += lineHeight;

            // Stones + Resonance on same line
            GUI.Label(new Rect(labelX, lineY, 55f, lineHeight), "Stones:", labelStyle);
            GUI.Label(new Rect(labelX + 55f, lineY, 40f, lineHeight), availableStones.ToString(), valueStyle);
            GUI.Label(new Rect(labelX + 95f, lineY, 55f, lineHeight), "Reson:", labelStyle);
            var resonanceStyle = new GUIStyle(valueStyle)
            {
                normal = { textColor = failures > 0 ? new Color(0.9f, 0.7f, 0.3f) : new Color(0.6f, 0.6f, 0.6f) }
            };
            GUI.Label(new Rect(labelX + 145f, lineY, 40f, lineHeight), failures.ToString(), resonanceStyle);
            lineY += lineHeight;

            // Success chance
            var preview = playerEquipment.PreviewAttuneEquippedWeapon();
            if (preview.CanAttempt || equipment.AttunementLevel < maxLevel)
            {
                GUI.Label(new Rect(labelX, lineY, 55f, lineHeight), "Chance:", labelStyle);
                string chanceText = preview.IsGuaranteed ? "GUARANTEED" : $"{preview.SuccessChance:P0}";
                var chanceStyle = new GUIStyle(valueStyle)
                {
                    normal = { textColor = preview.IsGuaranteed
                        ? new Color(0.4f, 0.9f, 1f)
                        : (preview.SuccessChance >= 1f ? new Color(0.5f, 0.9f, 0.5f) : new Color(1f, 0.8f, 0.4f)) }
                };
                GUI.Label(new Rect(labelX + 55f, lineY, 120f, lineHeight), chanceText, chanceStyle);
                lineY += lineHeight;
            }

            lineY += 2f;

            // Button rows - more compact
            float btnH = 18f;
            float btnY = lineY;
            float btnSpacing = 2f;

            // Row 1: Attune | +Stones | Free+1
            bool canAttuneWithStone = availableStones >= 1 && equipment.AttunementLevel < maxLevel;
            GUI.enabled = canAttuneWithStone;
            if (GUI.Button(new Rect(labelX, btnY, 55f, btnH), "Attune", buttonStyle))
            {
                playerEquipment.TryAttuneEquippedWeaponWithStones();
            }
            GUI.enabled = true;

            if (GUI.Button(new Rect(labelX + 57f, btnY, 50f, btnH), "+5 St", buttonStyle))
                AddDebugAttunementStones(5);
            if (GUI.Button(new Rect(labelX + 109f, btnY, 50f, btnH), "+20 St", buttonStyle))
                AddDebugAttunementStones(20);
            if (GUI.Button(new Rect(labelX + 161f, btnY, 50f, btnH), "Free+1", buttonStyle))
                playerEquipment.TryAttuneEquippedWeapon();
            if (GUI.Button(new Rect(labelX + 213f, btnY, 45f, btnH), "Reset", buttonStyle))
                playerEquipment.ResetEquippedWeaponAttunement();

            lineY = btnY + btnH + btnSpacing;
            btnY = lineY;

            // Row 2: Level presets
            if (GUI.Button(new Rect(labelX, btnY, 35f, btnH), "+0", buttonStyle))
                playerEquipment.SetEquippedWeaponAttunement(0);
            if (GUI.Button(new Rect(labelX + 37f, btnY, 35f, btnH), "+3", buttonStyle))
                playerEquipment.SetEquippedWeaponAttunement(3);
            if (GUI.Button(new Rect(labelX + 74f, btnY, 35f, btnH), "+5", buttonStyle))
                playerEquipment.SetEquippedWeaponAttunement(5);
            if (GUI.Button(new Rect(labelX + 111f, btnY, 40f, btnH), "+10", buttonStyle))
                playerEquipment.SetEquippedWeaponAttunement(10);
            if (GUI.Button(new Rect(labelX + 153f, btnY, 35f, btnH), "Max", buttonStyle))
                playerEquipment.SetEquippedWeaponAttunement(maxLevel);

            // Force buttons on same row
            var service = playerEquipment.GetAttunementService();
            if (service != null && service.HasChanceSystem)
            {
                if (GUI.Button(new Rect(labelX + 190f, btnY, 35f, btnH), "Win", buttonStyle))
                    service.ForceNextOutcome(true);
                if (GUI.Button(new Rect(labelX + 227f, btnY, 35f, btnH), "Lose", buttonStyle))
                    service.ForceNextOutcome(false);
            }

            lineY = btnY + btnH + btnSpacing;
            btnY = lineY;

            // Row 3: Resonance + Wear
            if (GUI.Button(new Rect(labelX, btnY, 35f, btnH), "R=0", buttonStyle))
                equipment.ResetAttunementFailures();
            if (GUI.Button(new Rect(labelX + 37f, btnY, 35f, btnH), "R+1", buttonStyle))
                equipment.IncrementAttunementFailures();
            if (GUI.Button(new Rect(labelX + 74f, btnY, 35f, btnH), "R=5", buttonStyle))
                equipment.SetAttunementFailures(5);
            if (GUI.Button(new Rect(labelX + 111f, btnY, 35f, btnH), "R=6", buttonStyle))
                equipment.SetAttunementFailures(6);

            if (playerWeaponWear != null)
            {
                if (GUI.Button(new Rect(labelX + 153f, btnY, 50f, btnH), "Wear", buttonStyle))
                    playerWeaponWear.ApplyDebugWear();
                if (GUI.Button(new Rect(labelX + 205f, btnY, 50f, btnH), "Break", buttonStyle))
                    playerWeaponWear.ForceBreakWeapon();
            }

            lineY = btnY + btnH + 4f;

            // Repair section
            lineY = DrawRepairSection(lineY, lineHeight, labelX, valueX, valueWidth, width, equipment);

            // History section
            lineY = DrawHistorySection(lineY, lineHeight, labelX, valueX, valueWidth, width, equipment);

            return lineY;
        }

        private float DrawRepairSection(float lineY, float lineHeight, float labelX, float valueX, float valueWidth, float width, EquipmentData equipment)
        {
            GUI.Label(new Rect(labelX, lineY, width - 16f, lineHeight), "— Repair —", subHeaderStyle);
            lineY += lineHeight;

            if (playerRepair == null)
            {
                GUI.Label(new Rect(labelX, lineY, width - 16f, lineHeight), "Not found", labelStyle);
                return lineY + lineHeight + 3f;
            }

            int fragments = playerRepair.GetAvailableFragments();
            GUI.Label(new Rect(labelX, lineY, 55f, lineHeight), "Frags:", labelStyle);
            GUI.Label(new Rect(labelX + 55f, lineY, 40f, lineHeight), fragments.ToString(), valueStyle);

            if (equipment != null)
            {
                var preview = playerRepair.PreviewRepair(equipment);
                if (preview.CanRepair)
                {
                    GUI.Label(new Rect(labelX + 100f, lineY, 150f, lineHeight),
                        $"Need: {preview.FragmentsToConsume}/{preview.FragmentsForFullRepair}", valueStyle);
                }
            }
            lineY += lineHeight + 2f;

            // Repair buttons - compact
            float btnH = 18f;
            float btnY = lineY;

            if (GUI.Button(new Rect(labelX, btnY, 50f, btnH), "+5", buttonStyle))
                AddDebugFragments(5);
            if (GUI.Button(new Rect(labelX + 52f, btnY, 50f, btnH), "+20", buttonStyle))
                AddDebugFragments(20);

            if (equipment != null && playerRepair.CanRepair(equipment))
            {
                if (GUI.Button(new Rect(labelX + 104f, btnY, 55f, btnH), "Repair", buttonStyle))
                    playerRepair.RepairEquipment(equipment);
            }

            if (playerWeaponWear != null)
            {
                if (GUI.Button(new Rect(labelX + 161f, btnY, 55f, btnH), "Restore", buttonStyle))
                    playerWeaponWear.RestoreWeaponDurability();
            }

            lineY = btnY + btnH + 4f;

            return lineY;
        }

        private float DrawHistorySection(float lineY, float lineHeight, float labelX, float valueX, float valueWidth, float width, EquipmentData equipment)
        {
            GUI.Label(new Rect(labelX, lineY, width - 16f, lineHeight), "— Attunement History —", subHeaderStyle);
            lineY += lineHeight;

            if (equipment == null)
            {
                GUI.Label(new Rect(labelX, lineY, width - 16f, lineHeight), "No equipment", labelStyle);
                return lineY + lineHeight + 3f;
            }

            var history = equipment.History?.Attunement;
            if (history == null)
            {
                GUI.Label(new Rect(labelX, lineY, width - 16f, lineHeight), "No history", labelStyle);
                return lineY + lineHeight + 3f;
            }

            // Attempts + Successes + Failures on same line
            GUI.Label(new Rect(labelX, lineY, 35f, lineHeight), "Att:", labelStyle);
            GUI.Label(new Rect(labelX + 35f, lineY, 25f, lineHeight), history.TotalAttempts.ToString(), valueStyle);
            GUI.Label(new Rect(labelX + 65f, lineY, 30f, lineHeight), "Suc:", labelStyle);
            GUI.Label(new Rect(labelX + 95f, lineY, 25f, lineHeight), history.SuccessfulAttempts.ToString(), valueStyle);
            GUI.Label(new Rect(labelX + 125f, lineY, 30f, lineHeight), "Fail:", labelStyle);
            GUI.Label(new Rect(labelX + 155f, lineY, 25f, lineHeight), history.FailedAttempts.ToString(), valueStyle);
            lineY += lineHeight;

            // Stones + Highest + Longest Streak
            GUI.Label(new Rect(labelX, lineY, 50f, lineHeight), "Stones:", labelStyle);
            GUI.Label(new Rect(labelX + 45f, lineY, 25f, lineHeight), history.TotalStonesConsumed.ToString(), valueStyle);
            GUI.Label(new Rect(labelX + 80f, lineY, 35f, lineHeight), "High:", labelStyle);
            GUI.Label(new Rect(labelX + 115f, lineY, 25f, lineHeight), $"+{history.HighestAttunementLevelReached}", valueStyle);
            GUI.Label(new Rect(labelX + 150f, lineY, 45f, lineHeight), "LStrk:", labelStyle);
            GUI.Label(new Rect(labelX + 190f, lineY, 20f, lineHeight), history.LongestFailureStreak.ToString(), valueStyle);
            lineY += lineHeight;

            // Last attempt info
            if (history.LastAttemptTimestamp > 0)
            {
                string lastResult = history.LastAttemptSucceeded
                    ? (history.LastAttemptWasGuaranteed ? "Guaranteed" : "Success")
                    : "Failed";
                GUI.Label(new Rect(labelX, lineY, 35f, lineHeight), "Last:", labelStyle);
                GUI.Label(new Rect(labelX + 35f, lineY, 70f, lineHeight), lastResult, valueStyle);
                GUI.Label(new Rect(labelX + 110f, lineY, 100f, lineHeight), $"+{history.LastAttemptPreviousLevel}→+{history.LastAttemptResultingLevel}", valueStyle);
                lineY += lineHeight;
            }

            // Debug buttons
            float btnH = 18f;
            float btnY = lineY;

            if (GUI.Button(new Rect(labelX, btnY, 55f, btnH), "Reset", buttonStyle))
            {
                history.Reset();
            }

            lineY = btnY + btnH + 4f;

            return lineY;
        }

        private float DrawEnemySection(float lineY, float lineHeight, float labelX, float valueX, float valueWidth, float width)
        {
            GUI.Label(new Rect(labelX, lineY, width - 16f, lineHeight), "— Enemy —", subHeaderStyle);
            lineY += lineHeight;

            if (trackedEnemy != null)
            {
                // Health + Dead on same line
                GUI.Label(new Rect(labelX, lineY, 45f, lineHeight), "HP:", labelStyle);
                GUI.Label(new Rect(labelX + 40f, lineY, 90f, lineHeight),
                    $"{trackedEnemy.CurrentHealth:F0}/{trackedEnemy.MaxHealth:F0}", valueStyle);
                GUI.Label(new Rect(labelX + 135f, lineY, 40f, lineHeight), "Dead:", labelStyle);
                GUI.Label(new Rect(labelX + 175f, lineY, 30f, lineHeight), FormatBool(trackedEnemy.IsDead), valueStyle);
                lineY += lineHeight;
            }

            if (trackedEnemyBrain != null)
            {
                // State + Distance on same line
                GUI.Label(new Rect(labelX, lineY, 40f, lineHeight), "State:", labelStyle);
                GUI.Label(new Rect(labelX + 40f, lineY, 70f, lineHeight), trackedEnemyBrain.CurrentState.ToString(), valueStyle);
                GUI.Label(new Rect(labelX + 115f, lineY, 40f, lineHeight), "Dist:", labelStyle);
                GUI.Label(new Rect(labelX + 155f, lineY, 50f, lineHeight), $"{trackedEnemyBrain.DistanceToTarget:F1}m", valueStyle);
                lineY += lineHeight;

                // Navigation diagnostics (slice 0.9.6)
                GUI.Label(new Rect(labelX, lineY, width - 16f, lineHeight),
                    $"Prev: {trackedEnemyBrain.PreviousState}  Reason: {trackedEnemyBrain.LastTransitionReason}  " +
                    $"In state: {trackedEnemyBrain.CurrentStateDuration:F1}s", labelStyle);
                lineY += lineHeight;

                GUI.Label(new Rect(labelX, lineY, width - 16f, lineHeight),
                    $"Mode: {trackedEnemyBrain.RoamingModeName}  Home dist: {trackedEnemyBrain.DistanceFromHome:F1}m  " +
                    $"Path: {trackedEnemyBrain.PathStatusText}", labelStyle);
                lineY += lineHeight;

                string seen = float.IsInfinity(trackedEnemyBrain.TimeSinceTargetSeen)
                    ? "never"
                    : $"{trackedEnemyBrain.TimeSinceTargetSeen:F1}s ago";
                GUI.Label(new Rect(labelX, lineY, width - 16f, lineHeight),
                    $"Sees player: {FormatBool(trackedEnemyBrain.TargetVisible)} ({seen})  " +
                    $"Recoveries: {trackedEnemyBrain.RecoveryCount}  Warps: {trackedEnemyBrain.EmergencyWarpCount}", labelStyle);
                lineY += lineHeight;
            }

            if (trackedEnemy == null && trackedEnemyBrain == null)
            {
                GUI.Label(new Rect(labelX, lineY, width - 16f, lineHeight), "No enemy tracked", labelStyle);
                lineY += lineHeight;
            }

            return lineY + 3f;
        }

        private string FormatBool(bool value)
        {
            return value ? "Yes" : "No";
        }

        private void AddDebugFragments(int count)
        {
            if (playerInventory == null || playerRepair?.FragmentDefinition == null)
            {
                Debug.LogWarning("[CombatDebug] Cannot add fragments - missing inventory or fragment definition");
                return;
            }

            var addResult = playerInventory.Inventory.TryAddItemWithResult(new ItemInstance(playerRepair.FragmentDefinition, count));
            Debug.Log($"[CombatDebug] Added {addResult.Added} repair fragments");
        }

        private void AddDebugAttunementStones(int count)
        {
            if (playerInventory == null || playerEquipment?.AttunementStoneDefinition == null)
            {
                Debug.LogWarning("[CombatDebug] Cannot add stones - missing inventory or stone definition");
                return;
            }

            var addResult = playerInventory.Inventory.TryAddItemWithResult(new ItemInstance(playerEquipment.AttunementStoneDefinition, count));
            Debug.Log($"[CombatDebug] Added {addResult.Added} attunement stones");
        }

        private void OnDrawGizmos()
        {
            if (!isVisible) return;

            if (playerWeapon != null)
            {
                Gizmos.color = playerWeapon.IsAttacking ? Color.yellow : Color.gray;
                Gizmos.DrawWireSphere(playerWeapon.transform.position, 2f);
            }

            if (playerDodge != null && playerDodge.IsDodging)
            {
                Gizmos.color = playerDodge.IsInvulnerable ? Color.cyan : Color.blue;
                Gizmos.DrawSphere(playerDodge.transform.position, 0.3f);
            }
        }
    }
}

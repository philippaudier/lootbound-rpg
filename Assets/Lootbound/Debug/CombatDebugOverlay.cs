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

        private GUIStyle buttonStyle;

        private bool isVisible;
        private GUIStyle boxStyle;
        private GUIStyle labelStyle;
        private GUIStyle headerStyle;
        private GUIStyle windowStyle;

        private Rect windowRect = new Rect(Screen.width - 340f, 20f, 320f, 100f);

        private void Start()
        {
            isVisible = showOnStart;
            AutoFindDependencies();
        }

        private void AutoFindDependencies()
        {
            if (playerHealth == null)
            {
                playerHealth = FindFirstObjectByType<PlayerHealth>();
            }

            if (playerDodge == null)
            {
                playerDodge = FindFirstObjectByType<PlayerDodge>();
            }

            if (playerWeapon == null)
            {
                playerWeapon = FindFirstObjectByType<PlayerMeleeWeapon>();
            }

            if (combatController == null)
            {
                combatController = FindFirstObjectByType<PlayerCombatController>();
            }

            if (trackedEnemy == null)
            {
                trackedEnemy = FindFirstObjectByType<EnemyHealth>();
            }

            if (trackedEnemyBrain == null)
            {
                trackedEnemyBrain = FindFirstObjectByType<EnemyBrain>();
            }

            if (playerEquipment == null)
            {
                playerEquipment = FindFirstObjectByType<PlayerEquipment>();
            }

            if (playerWeaponWear == null)
            {
                playerWeaponWear = FindFirstObjectByType<PlayerWeaponWear>();
            }

            if (playerRepair == null)
            {
                playerRepair = FindFirstObjectByType<PlayerRepair>();
            }

            if (playerInventory == null)
            {
                playerInventory = FindFirstObjectByType<PlayerInventory>();
            }
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
            if (!isVisible)
            {
                return;
            }

            InitializeStyles();

            // Position window on right side of screen
            windowRect.x = Screen.width - windowRect.width - 20f;

            // Use GUILayout.Window for auto-sizing
            windowRect = GUILayout.Window(0, windowRect, DrawDebugWindow, "", windowStyle);
        }

        private void DrawDebugWindow(int windowID)
        {
            DrawHeader("COMBAT DEBUG (F6)");
            GUILayout.Space(10f);

            DrawPlayerSection();
            GUILayout.Space(10f);

            DrawAttackSection();
            GUILayout.Space(10f);

            DrawDodgeSection();
            GUILayout.Space(10f);

            DrawEquipmentSection();
            GUILayout.Space(10f);

            DrawEnemySection();
        }

        private void InitializeStyles()
        {
            if (boxStyle != null)
            {
                return;
            }

            boxStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = MakeTexture(2, 2, new Color(0f, 0f, 0f, 0.85f)) }
            };

            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                normal = { textColor = Color.white }
            };

            headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.9f, 0.8f, 0.6f) }
            };

            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                padding = new RectOffset(8, 8, 4, 4)
            };

            windowStyle = new GUIStyle(GUI.skin.window)
            {
                normal = { background = MakeTexture(2, 2, new Color(0f, 0f, 0f, 0.85f)) },
                padding = new RectOffset(15, 15, 10, 10)
            };
        }

        private Texture2D MakeTexture(int width, int height, Color color)
        {
            var pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }

            var texture = new Texture2D(width, height);
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        private void DrawHeader(string text)
        {
            GUILayout.Label(text, headerStyle);
        }

        private void DrawLabel(string label, string value)
        {
            GUILayout.Label($"{label}: {value}", labelStyle);
        }

        private void DrawLabel(string label, float value, string format = "F2")
        {
            GUILayout.Label($"{label}: {value.ToString(format)}", labelStyle);
        }

        private void DrawLabel(string label, bool value)
        {
            string color = value ? "<color=#90EE90>TRUE</color>" : "<color=#FFB6C1>FALSE</color>";
            var richStyle = new GUIStyle(labelStyle) { richText = true };
            GUILayout.Label($"{label}: {color}", richStyle);
        }

        private void DrawPlayerSection()
        {
            DrawHeader("Player");

            if (playerHealth != null)
            {
                DrawLabel("Health", $"{playerHealth.CurrentHealth:F0} / {playerHealth.MaxHealth:F0}");
                DrawLabel("Dead", playerHealth.IsDead);
            }
            else
            {
                GUILayout.Label("PlayerHealth: Not found", labelStyle);
            }
        }

        private void DrawAttackSection()
        {
            DrawHeader("Attack");

            if (playerWeapon != null)
            {
                DrawLabel("Phase", playerWeapon.CurrentPhase.ToString());
                DrawLabel("Is Attacking", playerWeapon.IsAttacking);
                DrawLabel("Can Attack", playerWeapon.CanAttack);
                DrawLabel("Progress", playerWeapon.AttackProgress, "P0");
                DrawLabel("Hits This Attack", playerWeapon.HitsThisAttack.ToString());
            }
            else
            {
                GUILayout.Label("PlayerMeleeWeapon: Not found", labelStyle);
            }
        }

        private void DrawDodgeSection()
        {
            DrawHeader("Dodge");

            if (playerDodge != null)
            {
                DrawLabel("Is Dodging", playerDodge.IsDodging);
                DrawLabel("Is Invulnerable", playerDodge.IsInvulnerable);
                DrawLabel("Can Dodge", playerDodge.CanDodge);
                DrawLabel("Cooldown", playerDodge.CooldownRemaining, "F2");
            }
            else
            {
                GUILayout.Label("PlayerDodge: Not found", labelStyle);
            }
        }

        private void DrawEquipmentSection()
        {
            DrawHeader("Equipment");

            if (playerEquipment == null)
            {
                GUILayout.Label("PlayerEquipment: Not found", labelStyle);
                return;
            }

            if (!playerEquipment.HasWeaponEquipped)
            {
                GUILayout.Label("No weapon equipped", labelStyle);
                return;
            }

            var equipment = playerEquipment.CurrentEquipment;
            var stats = playerEquipment.CurrentStats;

            DrawLabel("Weapon", equipment.CustomName ?? equipment.DefinitionId);
            DrawLabel("Condition", equipment.Condition.ToString());
            DrawLabel("Durability", $"{equipment.CurrentDurability:F0}/{equipment.MaxDurability:F0}");

            if (stats.IsValid)
            {
                DrawLabel("Damage", stats.Damage, "F0");
                DrawLabel("Attack Speed", stats.AttackSpeed, "F2");
                DrawLabel("Range", stats.Range, "F1");
            }

            GUILayout.Space(5f);

            // Debug buttons - Wear
            GUILayout.BeginHorizontal();

            if (playerWeaponWear != null)
            {
                if (GUILayout.Button("Apply Wear", buttonStyle))
                {
                    playerWeaponWear.ApplyDebugWear();
                }

                if (GUILayout.Button("Break", buttonStyle))
                {
                    playerWeaponWear.ForceBreakWeapon();
                }

                if (GUILayout.Button("Restore", buttonStyle))
                {
                    playerWeaponWear.RestoreWeaponDurability();
                }
            }

            GUILayout.EndHorizontal();

            // Draw repair section
            DrawRepairSection(equipment);
        }

        private void DrawRepairSection(EquipmentData equipment)
        {
            GUILayout.Space(5f);
            DrawHeader("Repair");

            if (playerRepair == null)
            {
                GUILayout.Label("PlayerRepair: Not found", labelStyle);
                return;
            }

            // Show repair info
            int fragments = playerRepair.GetAvailableFragments();
            DrawLabel("Repair Fragments", fragments.ToString());

            if (equipment != null)
            {
                var preview = playerRepair.PreviewRepair(equipment);
                DrawLabel("Can Repair", preview.CanRepair);

                if (preview.CanRepair)
                {
                    DrawLabel("Fragments Needed", $"{preview.FragmentsToConsume} / {preview.FragmentsForFullRepair}");
                    DrawLabel("After Repair", $"{preview.DurabilityAfterRepair:F0} ({preview.ConditionAfter})");
                }
                else if (preview.FailureReason != RepairFailureReason.None)
                {
                    DrawLabel("Failure", preview.FailureReason.ToString());
                }
            }

            GUILayout.Space(5f);

            // Repair debug buttons
            GUILayout.BeginHorizontal();

            if (GUILayout.Button("+5 Fragments", buttonStyle))
            {
                AddDebugFragments(5);
            }

            if (GUILayout.Button("+20 Fragments", buttonStyle))
            {
                AddDebugFragments(20);
            }

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();

            if (equipment != null && playerRepair.CanRepair(equipment))
            {
                if (GUILayout.Button("Repair", buttonStyle))
                {
                    var result = playerRepair.RepairEquipment(equipment);
                    Debug.Log($"[CombatDebug] Repair result: {result.Success}, used {result.FragmentsConsumed} fragments");
                }
            }

            GUILayout.EndHorizontal();
        }

        private void AddDebugFragments(int count)
        {
            if (playerInventory == null || playerRepair?.FragmentDefinition == null)
            {
                Debug.LogWarning("[CombatDebug] Cannot add fragments - missing inventory or fragment definition");
                return;
            }

            var addResult = playerInventory.Inventory.TryAddItemWithResult(new ItemInstance(playerRepair.FragmentDefinition, count));
            Debug.Log($"[CombatDebug] Added {addResult.Added} repair fragments (requested {count})");
        }

        private void DrawEnemySection()
        {
            DrawHeader("Enemy");

            if (trackedEnemy != null)
            {
                DrawLabel("Health", $"{trackedEnemy.CurrentHealth:F0} / {trackedEnemy.MaxHealth:F0}");
                DrawLabel("Dead", trackedEnemy.IsDead);
            }

            if (trackedEnemyBrain != null)
            {
                DrawLabel("State", trackedEnemyBrain.CurrentState.ToString());
                DrawLabel("Distance", trackedEnemyBrain.DistanceToTarget, "F1");
            }

            if (trackedEnemy == null && trackedEnemyBrain == null)
            {
                GUILayout.Label("No enemy tracked", labelStyle);
            }
        }

        private void OnDrawGizmos()
        {
            if (!isVisible)
            {
                return;
            }

            // Draw attack range
            if (playerWeapon != null)
            {
                Gizmos.color = playerWeapon.IsAttacking ? Color.yellow : Color.gray;
                Gizmos.DrawWireSphere(playerWeapon.transform.position, 2f); // Approximate range
            }

            // Draw dodge direction if dodging
            if (playerDodge != null && playerDodge.IsDodging)
            {
                Gizmos.color = playerDodge.IsInvulnerable ? Color.cyan : Color.blue;
                Gizmos.DrawSphere(playerDodge.transform.position, 0.3f);
            }
        }
    }
}

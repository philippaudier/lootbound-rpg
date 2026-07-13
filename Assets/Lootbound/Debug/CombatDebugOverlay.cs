using UnityEngine;
using UnityEngine.InputSystem;
using Lootbound.Gameplay.Combat;
using Lootbound.Gameplay.Player;

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

        [Header("Enemy (Optional)")]
        [SerializeField] private EnemyHealth trackedEnemy;
        [SerializeField] private EnemyBrain trackedEnemyBrain;

        [Header("Settings")]
        [SerializeField] private bool showOnStart = false;

        private bool isVisible;
        private GUIStyle boxStyle;
        private GUIStyle labelStyle;
        private GUIStyle headerStyle;

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

            float panelWidth = 300f;
            float panelHeight = 400f;
            float x = Screen.width - panelWidth - 20f;
            float y = 20f;

            GUI.Box(new Rect(x - 10f, y - 10f, panelWidth + 20f, panelHeight + 20f), "", boxStyle);

            GUILayout.BeginArea(new Rect(x, y, panelWidth, panelHeight));

            DrawHeader("COMBAT DEBUG (F6)");
            GUILayout.Space(10f);

            DrawPlayerSection();
            GUILayout.Space(10f);

            DrawAttackSection();
            GUILayout.Space(10f);

            DrawDodgeSection();
            GUILayout.Space(10f);

            DrawEnemySection();

            GUILayout.EndArea();
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

using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Lootbound.Core.Logging;
using Lootbound.Gameplay.Combat;
using Lootbound.Gameplay.Player;

namespace Lootbound.UI.Combat
{
    /// <summary>
    /// Controls the Combat HUD UI elements.
    /// Handles health display, damage flash, and death screen.
    /// </summary>
    public class CombatHUDController : MonoBehaviour
    {
        private const string Category = "CombatHUD";

        [Header("UI Document")]
        [SerializeField] private UIDocument uiDocument;

        [Header("Dependencies")]
        [SerializeField] private PlayerHealth playerHealth;

        [Header("Flash Settings")]
        [SerializeField] private float damageFlashDuration = 0.2f;

        [Header("Enemy Health (Debug)")]
        [Tooltip("Optional enemy to track health for (debug feature).")]
        [SerializeField] private EnemyHealth trackedEnemy;
        [SerializeField] private float enemyHealthDisplayDuration = 3f;

        // UI Elements
        private VisualElement root;
        private VisualElement healthBarFill;
        private Label healthText;
        private VisualElement damageFlash;
        private VisualElement deathPanel;
        private VisualElement crosshair;

        // Enemy health
        private VisualElement enemyHealthContainer;
        private VisualElement enemyHealthBarFill;
        private Label enemyHealthLabel;
        private float enemyHealthDisplayTimer;

        private Coroutine damageFlashCoroutine;
        private bool isDead;

        private void Awake()
        {
            if (uiDocument == null)
            {
                uiDocument = GetComponent<UIDocument>();
            }
        }

        private void Start()
        {
            // Force update after all components are initialized
            StartCoroutine(DelayedHealthUpdate());
        }

        private System.Collections.IEnumerator DelayedHealthUpdate()
        {
            // Wait one frame to ensure all components are ready
            yield return null;
            UpdateHealthDisplay();
        }

        private void OnEnable()
        {
            if (uiDocument == null)
            {
                LootboundLog.Error(Category, "UIDocument not assigned!");
                return;
            }

            root = uiDocument.rootVisualElement;
            CacheElements();
            SubscribeToEvents();

            // Wait for UI to be fully attached before updating
            root.RegisterCallback<AttachToPanelEvent>(OnPanelAttached);

            // Also try immediate update in case already attached
            if (root.panel != null)
            {
                UpdateHealthDisplay();
            }
        }

        private void OnPanelAttached(AttachToPanelEvent evt)
        {
            // Update health display once UI is ready
            UpdateHealthDisplay();
        }

        private void OnDisable()
        {
            if (root != null)
            {
                root.UnregisterCallback<AttachToPanelEvent>(OnPanelAttached);
            }
            UnsubscribeFromEvents();
        }

        private void Update()
        {
            UpdateEnemyHealthDisplay();
            HandleRestartInput();
        }

        private void CacheElements()
        {
            healthBarFill = root.Q<VisualElement>("health-bar-fill");
            healthText = root.Q<Label>("health-text");
            damageFlash = root.Q<VisualElement>("damage-flash");
            deathPanel = root.Q<VisualElement>("death-panel");
            crosshair = root.Q<VisualElement>("crosshair");

            enemyHealthContainer = root.Q<VisualElement>("enemy-health-container");
            enemyHealthBarFill = root.Q<VisualElement>("enemy-health-bar-fill");
            enemyHealthLabel = root.Q<Label>("enemy-health-label");
        }

        private void SubscribeToEvents()
        {
            if (playerHealth != null)
            {
                playerHealth.OnHealthChanged += HandlePlayerHealthChanged;
                playerHealth.OnDamaged += HandlePlayerDamaged;
                playerHealth.OnDied += HandlePlayerDied;
            }

            if (trackedEnemy != null)
            {
                trackedEnemy.OnHealthChanged += HandleEnemyHealthChanged;
            }
        }

        private void UnsubscribeFromEvents()
        {
            if (playerHealth != null)
            {
                playerHealth.OnHealthChanged -= HandlePlayerHealthChanged;
                playerHealth.OnDamaged -= HandlePlayerDamaged;
                playerHealth.OnDied -= HandlePlayerDied;
            }

            if (trackedEnemy != null)
            {
                trackedEnemy.OnHealthChanged -= HandleEnemyHealthChanged;
            }
        }

        private void HandlePlayerHealthChanged(float current, float max)
        {
            UpdateHealthDisplay(current, max);
        }

        private void HandlePlayerDamaged(DamageRequest request)
        {
            PlayDamageFlash();
        }

        private void HandlePlayerDied()
        {
            isDead = true;
            ShowDeathPanel();
        }

        private void UpdateHealthDisplay()
        {
            if (playerHealth != null)
            {
                UpdateHealthDisplay(playerHealth.CurrentHealth, playerHealth.MaxHealth);
            }
        }

        private void UpdateHealthDisplay(float current, float max)
        {
            if (healthBarFill != null)
            {
                float percentage = max > 0 ? current / max : 0f;
                float widthPercent = percentage * 100f;
                healthBarFill.style.width = Length.Percent(widthPercent);

                // Low health visual
                if (percentage <= 0.25f)
                {
                    healthBarFill.AddToClassList("health-bar-fill-low");
                }
                else
                {
                    healthBarFill.RemoveFromClassList("health-bar-fill-low");
                }
            }

            if (healthText != null)
            {
                healthText.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
            }
        }

        private void PlayDamageFlash()
        {
            if (damageFlash == null)
            {
                return;
            }

            if (damageFlashCoroutine != null)
            {
                StopCoroutine(damageFlashCoroutine);
            }

            damageFlashCoroutine = StartCoroutine(DamageFlashCoroutine());
        }

        private IEnumerator DamageFlashCoroutine()
        {
            damageFlash.AddToClassList("damage-flash-active");
            yield return new WaitForSeconds(damageFlashDuration);
            damageFlash.RemoveFromClassList("damage-flash-active");
            damageFlashCoroutine = null;
        }

        private void ShowDeathPanel()
        {
            if (deathPanel == null)
            {
                return;
            }

            deathPanel.style.display = DisplayStyle.Flex;

            // Add visible class after a frame for transition
            StartCoroutine(ShowDeathPanelDelayed());
        }

        private IEnumerator ShowDeathPanelDelayed()
        {
            yield return null;
            deathPanel.AddToClassList("death-panel-visible");
        }

        private void HideDeathPanel()
        {
            if (deathPanel == null)
            {
                return;
            }

            deathPanel.RemoveFromClassList("death-panel-visible");
            deathPanel.style.display = DisplayStyle.None;
        }

        private void HandleRestartInput()
        {
            if (!isDead)
            {
                return;
            }

            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            {
                Restart();
            }
        }

        private void Restart()
        {
            isDead = false;
            HideDeathPanel();

            if (playerHealth != null)
            {
                playerHealth.Reset();
            }

            // Find and reset player combat
            var combatController = FindFirstObjectByType<PlayerCombatController>();
            if (combatController != null)
            {
                combatController.ResetCombat();
            }

            LootboundLog.Info(Category, "Player restarted");
        }

        // Enemy health tracking (debug feature)
        private void HandleEnemyHealthChanged(float current, float max)
        {
            ShowEnemyHealth(trackedEnemy?.name ?? "Enemy", current, max);
        }

        /// <summary>
        /// Show enemy health bar temporarily.
        /// </summary>
        public void ShowEnemyHealth(string enemyName, float current, float max)
        {
            if (enemyHealthContainer == null)
            {
                return;
            }

            enemyHealthDisplayTimer = enemyHealthDisplayDuration;
            enemyHealthContainer.AddToClassList("enemy-health-container-visible");

            if (enemyHealthBarFill != null)
            {
                float percentage = max > 0 ? current / max : 0f;
                enemyHealthBarFill.style.width = new StyleLength(new Length(percentage * 100f, LengthUnit.Percent));
            }

            if (enemyHealthLabel != null)
            {
                enemyHealthLabel.text = enemyName;
            }
        }

        private void UpdateEnemyHealthDisplay()
        {
            if (enemyHealthDisplayTimer > 0f)
            {
                enemyHealthDisplayTimer -= Time.deltaTime;

                if (enemyHealthDisplayTimer <= 0f && enemyHealthContainer != null)
                {
                    enemyHealthContainer.RemoveFromClassList("enemy-health-container-visible");
                }
            }
        }

        /// <summary>
        /// Set the tracked enemy at runtime.
        /// </summary>
        public void SetTrackedEnemy(EnemyHealth enemy)
        {
            if (trackedEnemy != null)
            {
                trackedEnemy.OnHealthChanged -= HandleEnemyHealthChanged;
            }

            trackedEnemy = enemy;

            if (trackedEnemy != null)
            {
                trackedEnemy.OnHealthChanged += HandleEnemyHealthChanged;
            }
        }

        /// <summary>
        /// Update crosshair state based on combat.
        /// </summary>
        public void SetCrosshairAttacking(bool attacking)
        {
            if (crosshair == null)
            {
                return;
            }

            if (attacking)
            {
                crosshair.AddToClassList("crosshair-attacking");
            }
            else
            {
                crosshair.RemoveFromClassList("crosshair-attacking");
            }
        }
    }
}

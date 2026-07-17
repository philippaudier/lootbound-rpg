using System;
using UnityEngine;
using Lootbound.Gameplay.Combat;
using Lootbound.Gameplay.Equipment;
using Lootbound.Gameplay.Inventory;
using Lootbound.Gameplay.World;
using Lootbound.Core.Logging;

namespace Lootbound.Gameplay.Expeditions
{
    /// <summary>
    /// Manages the expedition lifecycle state machine.
    /// Authoritative source for expedition state transitions and metric tracking.
    /// </summary>
    public class ExpeditionLifecycle : MonoBehaviour
    {
        private const string Category = "Expedition";
        private const float DistanceSampleInterval = 0.5f;

        [Header("Player References")]
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private PlayerInventory playerInventory;
        [SerializeField] private PlayerEquipment playerEquipment;
        [SerializeField] private Transform playerTransform;

        [Header("World References")]
        [SerializeField] private ProceduralTerrainGenerator terrainGenerator;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = true;

        private ExpeditionSession currentSession;
        private float distanceSampleTimer;

        /// <summary>
        /// The current expedition session. Null if no expedition active.
        /// </summary>
        public ExpeditionSession CurrentSession => currentSession;

        /// <summary>
        /// Current expedition state.
        /// </summary>
        public ExpeditionState State => currentSession?.State ?? ExpeditionState.None;

        /// <summary>
        /// Whether an expedition is currently in progress (not None and not terminal).
        /// </summary>
        public bool IsExpeditionActive => currentSession != null && !currentSession.HasEnded;

        /// <summary>
        /// Whether metrics are being tracked (Active or Returning state).
        /// </summary>
        public bool IsTracking => currentSession?.IsTracking ?? false;

        /// <summary>
        /// Fired when expedition state changes.
        /// Parameters: oldState, newState
        /// </summary>
        public event Action<ExpeditionState, ExpeditionState> OnStateChanged;

        /// <summary>
        /// Fired when an expedition starts (enters Preparing).
        /// </summary>
        public event Action<ExpeditionSession> OnExpeditionStarted;

        /// <summary>
        /// Fired when an expedition ends (enters terminal state).
        /// </summary>
        public event Action<ExpeditionSession, ExpeditionOutcome> OnExpeditionEnded;

        private void Awake()
        {
            FindReferencesIfNeeded();
        }

        private void OnEnable()
        {
            SubscribeToEvents();
        }

        private void OnDisable()
        {
            UnsubscribeFromEvents();
        }

        private void Update()
        {
            if (!IsTracking || currentSession == null || playerTransform == null)
                return;

            // Update duration
            currentSession.Metrics.UpdateDuration(Time.deltaTime);

            // Sample distance periodically
            distanceSampleTimer -= Time.deltaTime;
            if (distanceSampleTimer <= 0f)
            {
                currentSession.Metrics.UpdateMaxDistance(playerTransform.position, currentSession.Origin);
                distanceSampleTimer = DistanceSampleInterval;
            }
        }

        /// <summary>
        /// Start a new expedition. Player enters preparation phase.
        /// </summary>
        /// <returns>True if expedition started successfully.</returns>
        public bool StartExpedition()
        {
            if (IsExpeditionActive)
            {
                Log("Cannot start: expedition already active");
                return false;
            }

            FindReferencesIfNeeded();

            if (playerTransform == null)
            {
                Debug.LogError("[ExpeditionLifecycle] Cannot start expedition: player transform not found");
                return false;
            }

            // Get world seed
            int seed = terrainGenerator != null ? terrainGenerator.CurrentSeed : 0;

            // Create new session
            var oldState = State;
            currentSession = new ExpeditionSession(seed, playerTransform.position);

            Log($"Expedition started: {currentSession.ExpeditionId[..8]}, seed={seed}");

            RaiseStateChanged(oldState, currentSession.State);
            OnExpeditionStarted?.Invoke(currentSession);

            return true;
        }

        /// <summary>
        /// Depart from refuge. Captures snapshot and transitions to Active.
        /// </summary>
        /// <returns>True if departure succeeded.</returns>
        public bool Depart()
        {
            if (currentSession == null || currentSession.State != ExpeditionState.Preparing)
            {
                Log("Cannot depart: not in Preparing state");
                return false;
            }

            var oldState = currentSession.State;

            // Depart captures snapshot
            if (!currentSession.Depart(playerEquipment, playerInventory))
            {
                return false;
            }

            Log($"Departing with: {currentSession.Snapshot}");

            RaiseStateChanged(oldState, currentSession.State);

            // Immediately transition to Active
            oldState = currentSession.State;
            if (currentSession.Activate())
            {
                distanceSampleTimer = DistanceSampleInterval;
                Log("Expedition now Active - tracking started");
                RaiseStateChanged(oldState, currentSession.State);
            }

            return true;
        }

        /// <summary>
        /// Mark that the player is returning to refuge.
        /// </summary>
        /// <returns>True if transition succeeded.</returns>
        public bool BeginReturn()
        {
            if (currentSession == null || currentSession.State != ExpeditionState.Active)
            {
                Log("Cannot begin return: not in Active state");
                return false;
            }

            var oldState = currentSession.State;
            if (currentSession.BeginReturn())
            {
                Log("Returning to refuge");
                RaiseStateChanged(oldState, currentSession.State);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Complete the expedition successfully.
        /// Called when player reaches the refuge.
        /// </summary>
        /// <returns>True if completion succeeded.</returns>
        public bool CompleteExpedition()
        {
            if (currentSession == null || currentSession.HasEnded)
            {
                Log("Cannot complete: no active expedition");
                return false;
            }

            if (currentSession.State != ExpeditionState.Active &&
                currentSession.State != ExpeditionState.Returning)
            {
                Log($"Cannot complete from state {currentSession.State}");
                return false;
            }

            var oldState = currentSession.State;
            if (currentSession.Complete())
            {
                Log($"Expedition completed: {currentSession.Metrics}");
                RaiseStateChanged(oldState, currentSession.State);
                OnExpeditionEnded?.Invoke(currentSession, ExpeditionOutcome.Success);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Cancel the current expedition (debug/development).
        /// </summary>
        /// <returns>True if cancellation succeeded.</returns>
        public bool CancelExpedition()
        {
            if (currentSession == null || currentSession.HasEnded)
            {
                Log("Cannot cancel: no active expedition");
                return false;
            }

            var oldState = currentSession.State;
            if (currentSession.Cancel())
            {
                Log("Expedition cancelled");
                RaiseStateChanged(oldState, currentSession.State);
                OnExpeditionEnded?.Invoke(currentSession, ExpeditionOutcome.Cancelled);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Clear the current session (for testing or after viewing results).
        /// Only works if expedition has ended.
        /// </summary>
        public void ClearSession()
        {
            if (currentSession != null && currentSession.HasEnded)
            {
                Log("Session cleared");
                currentSession = null;
            }
        }

        private void FailExpedition()
        {
            if (currentSession == null || currentSession.HasEnded)
                return;

            var oldState = currentSession.State;
            if (currentSession.Fail())
            {
                Log($"Expedition failed (player death): {currentSession.Metrics}");
                RaiseStateChanged(oldState, currentSession.State);
                OnExpeditionEnded?.Invoke(currentSession, ExpeditionOutcome.PlayerDeath);
            }
        }

        private void FindReferencesIfNeeded()
        {
            if (playerHealth == null)
            {
                playerHealth = FindFirstObjectByType<PlayerHealth>();
            }

            if (playerInventory == null)
            {
                playerInventory = FindFirstObjectByType<PlayerInventory>();
            }

            if (playerEquipment == null)
            {
                playerEquipment = FindFirstObjectByType<PlayerEquipment>();
            }

            if (playerTransform == null && playerHealth != null)
            {
                playerTransform = playerHealth.transform;
            }

            if (terrainGenerator == null)
            {
                terrainGenerator = FindFirstObjectByType<ProceduralTerrainGenerator>();
            }
        }

        private void SubscribeToEvents()
        {
            // Player death
            if (playerHealth != null)
            {
                playerHealth.OnDied += HandlePlayerDeath;
            }

            // Item acquisition
            if (playerInventory != null)
            {
                playerInventory.OnItemAdded += HandleItemAdded;
                playerInventory.OnEquipmentAdded += HandleEquipmentAdded;
            }

            // Enemy kills (static event)
            EnemyHealth.OnAnyEnemyDied += HandleEnemyKilled;
        }

        private void UnsubscribeFromEvents()
        {
            if (playerHealth != null)
            {
                playerHealth.OnDied -= HandlePlayerDeath;
            }

            if (playerInventory != null)
            {
                playerInventory.OnItemAdded -= HandleItemAdded;
                playerInventory.OnEquipmentAdded -= HandleEquipmentAdded;
            }

            EnemyHealth.OnAnyEnemyDied -= HandleEnemyKilled;
        }

        private void HandlePlayerDeath()
        {
            if (IsTracking)
            {
                FailExpedition();
            }
        }

        private void HandleItemAdded(ItemDefinition definition, int quantity)
        {
            if (!IsTracking || currentSession == null)
                return;

            // Resources are non-equipment items
            currentSession.Metrics.RecordItemAcquired(definition, quantity, isEquipment: false);

            if (enableDebugLogs)
            {
                Log($"Item acquired: {definition.DisplayName} x{quantity}");
            }
        }

        private void HandleEquipmentAdded(ItemInstance item)
        {
            if (!IsTracking || currentSession == null)
                return;

            currentSession.Metrics.RecordEquipmentAcquired();

            if (enableDebugLogs && item.Definition != null)
            {
                Log($"Equipment acquired: {item.Definition.DisplayName}");
            }
        }

        private void HandleEnemyKilled(EnemyHealth enemy)
        {
            if (!IsTracking || currentSession == null)
                return;

            // Get the currently equipped weapon (assumed to be the killing weapon)
            var weapon = playerEquipment?.CurrentEquipment;

            currentSession.Metrics.RecordKill(weapon);

            if (enableDebugLogs)
            {
                string weaponName = weapon != null ? weapon.CustomName : "Unknown";
                Log($"Enemy killed with {weaponName}. Total: {currentSession.Metrics.EnemiesDefeated}");
            }
        }

        private void RaiseStateChanged(ExpeditionState oldState, ExpeditionState newState)
        {
            if (oldState != newState)
            {
                OnStateChanged?.Invoke(oldState, newState);
            }
        }

        private void Log(string message)
        {
            if (enableDebugLogs)
            {
                LootboundLog.Info(Category, message);
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Debug: Start Expedition")]
        private void DebugStartExpedition()
        {
            StartExpedition();
        }

        [ContextMenu("Debug: Depart")]
        private void DebugDepart()
        {
            Depart();
        }

        [ContextMenu("Debug: Complete")]
        private void DebugComplete()
        {
            CompleteExpedition();
        }

        [ContextMenu("Debug: Cancel")]
        private void DebugCancel()
        {
            CancelExpedition();
        }

        [ContextMenu("Debug: Log State")]
        private void DebugLogState()
        {
            Debug.Log($"[ExpeditionLifecycle] State: {State}, Session: {currentSession}");
        }
#endif
    }
}

using System;
using Lootbound.Gameplay.Equipment;
using Lootbound.Gameplay.Inventory;
using UnityEngine;

namespace Lootbound.Gameplay.Expeditions
{
    /// <summary>
    /// Represents a single expedition session.
    /// Contains all data related to one expedition from start to finish.
    /// </summary>
    [Serializable]
    public class ExpeditionSession
    {
        // Identity
        [SerializeField] private string expeditionId;
        [SerializeField] private int worldSeed;

        // Timestamps (Unix epoch seconds for serialization)
        [SerializeField] private long startTimestamp;
        [SerializeField] private long endTimestamp;

        // State
        [SerializeField] private ExpeditionState state;
        [SerializeField] private ExpeditionOutcome outcome;

        // Origin for distance tracking
        [SerializeField] private Vector3 origin;

        // Components
        private ExpeditionMetrics metrics;
        private ExpeditionSnapshot snapshot;

        /// <summary>
        /// Unique identifier for this expedition (GUID).
        /// </summary>
        public string ExpeditionId => expeditionId;

        /// <summary>
        /// World seed used for this expedition's terrain.
        /// </summary>
        public int WorldSeed => worldSeed;

        /// <summary>
        /// When the expedition started (DateTime).
        /// </summary>
        public DateTime StartTime => DateTimeOffset.FromUnixTimeSeconds(startTimestamp).LocalDateTime;

        /// <summary>
        /// When the expedition ended (DateTime). Default if not ended.
        /// </summary>
        public DateTime EndTime => endTimestamp > 0
            ? DateTimeOffset.FromUnixTimeSeconds(endTimestamp).LocalDateTime
            : default;

        /// <summary>
        /// Current state of the expedition.
        /// </summary>
        public ExpeditionState State => state;

        /// <summary>
        /// Final outcome of the expedition.
        /// </summary>
        public ExpeditionOutcome Outcome => outcome;

        /// <summary>
        /// Origin point for distance calculations.
        /// </summary>
        public Vector3 Origin => origin;

        /// <summary>
        /// Metrics tracked during this expedition.
        /// </summary>
        public ExpeditionMetrics Metrics => metrics;

        /// <summary>
        /// Snapshot of state at departure.
        /// </summary>
        public ExpeditionSnapshot Snapshot => snapshot;

        /// <summary>
        /// Whether this expedition has ended (terminal state).
        /// </summary>
        public bool HasEnded => state.IsTerminal();

        /// <summary>
        /// Whether metrics are being actively tracked.
        /// </summary>
        public bool IsTracking => state.IsTracking();

        /// <summary>
        /// Create a new expedition session.
        /// </summary>
        /// <param name="worldSeed">Seed for terrain generation.</param>
        /// <param name="startPosition">Player starting position (becomes origin).</param>
        public ExpeditionSession(int worldSeed, Vector3 startPosition)
        {
            expeditionId = Guid.NewGuid().ToString();
            this.worldSeed = worldSeed;
            startTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            endTimestamp = 0;
            state = ExpeditionState.Preparing;
            outcome = ExpeditionOutcome.None;
            origin = startPosition;
            metrics = new ExpeditionMetrics();
            snapshot = null;
        }

        /// <summary>
        /// Create a session for testing with a specific ID.
        /// </summary>
        internal ExpeditionSession(string id, int worldSeed, Vector3 startPosition)
        {
            expeditionId = id;
            this.worldSeed = worldSeed;
            startTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            endTimestamp = 0;
            state = ExpeditionState.Preparing;
            outcome = ExpeditionOutcome.None;
            origin = startPosition;
            metrics = new ExpeditionMetrics();
            snapshot = null;
        }

        /// <summary>
        /// Transition to Departing state and capture snapshot.
        /// </summary>
        /// <returns>True if transition was valid.</returns>
        public bool Depart(PlayerEquipment equipment, PlayerInventory inventory)
        {
            if (state != ExpeditionState.Preparing)
            {
                Debug.LogWarning($"[ExpeditionSession] Cannot depart from state {state}");
                return false;
            }

            snapshot = ExpeditionSnapshot.Capture(equipment, inventory);
            state = ExpeditionState.Departing;
            return true;
        }

        /// <summary>
        /// Transition to Active state. Begin tracking.
        /// </summary>
        /// <returns>True if transition was valid.</returns>
        public bool Activate()
        {
            if (state != ExpeditionState.Departing)
            {
                Debug.LogWarning($"[ExpeditionSession] Cannot activate from state {state}");
                return false;
            }

            state = ExpeditionState.Active;
            return true;
        }

        /// <summary>
        /// Transition to Returning state.
        /// </summary>
        /// <returns>True if transition was valid.</returns>
        public bool BeginReturn()
        {
            if (state != ExpeditionState.Active)
            {
                Debug.LogWarning($"[ExpeditionSession] Cannot begin return from state {state}");
                return false;
            }

            state = ExpeditionState.Returning;
            return true;
        }

        /// <summary>
        /// Complete the expedition successfully.
        /// </summary>
        /// <returns>True if transition was valid.</returns>
        public bool Complete()
        {
            if (state != ExpeditionState.Active && state != ExpeditionState.Returning)
            {
                Debug.LogWarning($"[ExpeditionSession] Cannot complete from state {state}");
                return false;
            }

            state = ExpeditionState.Completed;
            outcome = ExpeditionOutcome.Success;
            endTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            metrics.Freeze();
            return true;
        }

        /// <summary>
        /// Fail the expedition due to player death.
        /// </summary>
        /// <returns>True if transition was valid.</returns>
        public bool Fail()
        {
            if (state == ExpeditionState.None || state.IsTerminal())
            {
                Debug.LogWarning($"[ExpeditionSession] Cannot fail from state {state}");
                return false;
            }

            state = ExpeditionState.Failed;
            outcome = ExpeditionOutcome.PlayerDeath;
            endTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            metrics.Freeze();
            return true;
        }

        /// <summary>
        /// Cancel the expedition (debug/development).
        /// </summary>
        /// <returns>True if transition was valid.</returns>
        public bool Cancel()
        {
            if (state == ExpeditionState.None || state.IsTerminal())
            {
                Debug.LogWarning($"[ExpeditionSession] Cannot cancel from state {state}");
                return false;
            }

            state = ExpeditionState.Cancelled;
            outcome = ExpeditionOutcome.Cancelled;
            endTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            metrics.Freeze();
            return true;
        }

        /// <summary>
        /// Update metrics with current player position.
        /// Call this regularly while tracking.
        /// </summary>
        public void UpdateTracking(Vector3 playerPosition, float deltaTime)
        {
            if (!IsTracking) return;

            metrics.UpdateDuration(deltaTime);
            metrics.UpdateMaxDistance(playerPosition, origin);
        }

        public override string ToString()
        {
            return $"[Expedition {expeditionId[..8]}] State={state}, Seed={worldSeed}, {metrics}";
        }
    }
}

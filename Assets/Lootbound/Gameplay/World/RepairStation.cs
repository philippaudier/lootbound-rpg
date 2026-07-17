using UnityEngine;
using Lootbound.Gameplay.Interaction;

namespace Lootbound.Gameplay.World
{
    /// <summary>
    /// Repair Station world object that allows players to repair equipment.
    /// Implements IInteractable for the interaction system.
    /// Raises events that UI can subscribe to for opening the repair interface.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class RepairStation : MonoBehaviour, IInteractable
    {
        [Header("Interaction")]
        [SerializeField] private string interactionPrompt = "Repair equipment";
        [SerializeField, Range(0f, 5f)] private float holdDuration = 0f;

        [Header("Station")]
        [SerializeField] private Transform repairAnchor;

        private bool isInUse;

        // IInteractable implementation
        public string InteractionPrompt => $"Press E to {interactionPrompt}";
        public bool CanInteract => !isInUse;
        public string IconId => "repair_station";
        public float HoldDuration => holdDuration;
        public Transform InteractionTransform => transform;

        /// <summary>
        /// Whether the station is currently being used.
        /// </summary>
        public bool IsInUse => isInUse;

        /// <summary>
        /// The anchor point where equipment is visually placed.
        /// </summary>
        public Transform RepairAnchor => repairAnchor;

        /// <summary>
        /// Event raised when the station interaction completes and UI should open.
        /// Subscribe to this from RepairStationUI to open the repair interface.
        /// </summary>
        public event System.Action<RepairStation> OnInteractionRequested;

        /// <summary>
        /// Event raised when the station is opened.
        /// </summary>
        public event System.Action<RepairStation> OnStationOpened;

        /// <summary>
        /// Event raised when the station is closed.
        /// </summary>
        public event System.Action<RepairStation> OnStationClosed;

        private void Start()
        {
            // Ensure collider is set to trigger
            var collider = GetComponent<Collider>();
            if (collider != null && !collider.isTrigger)
            {
                // Keep it as a solid collider - interaction uses raycast
            }

            // Validate anchor
            if (repairAnchor == null)
            {
                repairAnchor = transform;
            }
        }

        public void OnInteractionStart(PlayerInteractor interactor)
        {
            // Nothing special on start - instant interaction
        }

        public void OnInteractionComplete(PlayerInteractor interactor)
        {
            if (isInUse)
            {
                return;
            }

            // Raise event for UI to handle
            // If no subscribers, log warning
            if (OnInteractionRequested == null)
            {
                Debug.LogWarning("[RepairStation] No UI subscribed to OnInteractionRequested event!");
                return;
            }

            OnInteractionRequested?.Invoke(this);
        }

        public void OnInteractionCancel(PlayerInteractor interactor)
        {
            // Nothing to cancel for instant interaction
        }

        /// <summary>
        /// Called by RepairStationUI to mark the station as in use.
        /// </summary>
        public void SetInUse(bool inUse)
        {
            if (isInUse == inUse)
            {
                return;
            }

            isInUse = inUse;

            if (isInUse)
            {
                OnStationOpened?.Invoke(this);
                Debug.Log("[RepairStation] Station opened");
            }
            else
            {
                OnStationClosed?.Invoke(this);
                Debug.Log("[RepairStation] Station closed");
            }
        }

        /// <summary>
        /// Called by RepairStationUI when the player closes the station.
        /// </summary>
        public void Close()
        {
            SetInUse(false);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // Draw interaction area
            Gizmos.color = new Color(0.5f, 0.8f, 0.5f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, 2f);

            // Draw anchor point
            if (repairAnchor != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireCube(repairAnchor.position, Vector3.one * 0.2f);
            }
        }
#endif
    }
}

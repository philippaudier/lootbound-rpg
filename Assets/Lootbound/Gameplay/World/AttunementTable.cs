using UnityEngine;
using Lootbound.Gameplay.Interaction;

namespace Lootbound.Gameplay.World
{
    /// <summary>
    /// Attunement Table world object that allows players to deepen equipment attunement.
    /// Implements IInteractable for the interaction system.
    /// Raises events that UI can subscribe to for opening the attunement interface.
    /// </summary>
    /// <remarks>
    /// The Attunement Table (Table d'Accord) is a physical place where players
    /// can consume Attunement Stones to increase equipment levels.
    ///
    /// Unlike the Repair Station which restores what equipment was,
    /// the Attunement Table reveals what equipment can become.
    /// </remarks>
    [RequireComponent(typeof(Collider))]
    public class AttunementTable : MonoBehaviour, IInteractable
    {
        [Header("Interaction")]
        [SerializeField] private string interactionPrompt = "Use Attunement Table";
        [SerializeField, Range(0f, 5f)] private float holdDuration = 0f;

        [Header("Table")]
        [SerializeField] private Transform weaponAnchor;
        [SerializeField] private Transform stoneAnchor;

        [Header("History")]
        [SerializeField] private string locationName = "Refuge Attunement Table";

        private bool isInUse;

        // IInteractable implementation
        public string InteractionPrompt => $"Press E to {interactionPrompt}";
        public bool CanInteract => !isInUse;
        public string IconId => "attunement_table";
        public float HoldDuration => holdDuration;
        public Transform InteractionTransform => transform;

        /// <summary>
        /// Whether the table is currently being used.
        /// </summary>
        public bool IsInUse => isInUse;

        /// <summary>
        /// Location name for attunement history records.
        /// </summary>
        public string LocationName => locationName;

        /// <summary>
        /// The anchor point where weapons could be visually placed (future use).
        /// Returns the table transform if not explicitly set.
        /// </summary>
        public Transform WeaponAnchor => weaponAnchor != null ? weaponAnchor : transform;

        /// <summary>
        /// The anchor point where stones could be visually placed (future use).
        /// Returns the table transform if not explicitly set.
        /// </summary>
        public Transform StoneAnchor => stoneAnchor != null ? stoneAnchor : transform;

        /// <summary>
        /// Event raised when the table interaction completes and UI should open.
        /// Subscribe to this from AttunementTableUI to open the attunement interface.
        /// </summary>
        public event System.Action<AttunementTable> OnInteractionRequested;

        /// <summary>
        /// Event raised when the table is opened.
        /// </summary>
        public event System.Action<AttunementTable> OnTableOpened;

        /// <summary>
        /// Event raised when the table is closed.
        /// </summary>
        public event System.Action<AttunementTable> OnTableClosed;

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
            if (OnInteractionRequested == null)
            {
                Debug.LogWarning("[AttunementTable] No UI subscribed to OnInteractionRequested event!");
                return;
            }

            OnInteractionRequested?.Invoke(this);
        }

        public void OnInteractionCancel(PlayerInteractor interactor)
        {
            // Nothing to cancel for instant interaction
        }

        /// <summary>
        /// Called by AttunementTableUI to mark the table as in use.
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
                OnTableOpened?.Invoke(this);
                Debug.Log("[AttunementTable] Table opened");
            }
            else
            {
                OnTableClosed?.Invoke(this);
                Debug.Log("[AttunementTable] Table closed");
            }
        }

        /// <summary>
        /// Called by AttunementTableUI when the player closes the table.
        /// </summary>
        public void Close()
        {
            SetInUse(false);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // Draw interaction area
            Gizmos.color = new Color(0.6f, 0.5f, 0.8f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, 2f);

            // Draw weapon anchor point
            if (weaponAnchor != null && weaponAnchor != transform)
            {
                Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.8f);
                Gizmos.DrawWireCube(weaponAnchor.position, Vector3.one * 0.15f);
                Gizmos.DrawLine(transform.position, weaponAnchor.position);
            }

            // Draw stone anchor point
            if (stoneAnchor != null && stoneAnchor != transform)
            {
                Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.8f);
                Gizmos.DrawWireSphere(stoneAnchor.position, 0.08f);
                Gizmos.DrawLine(transform.position, stoneAnchor.position);
            }
        }
#endif
    }
}

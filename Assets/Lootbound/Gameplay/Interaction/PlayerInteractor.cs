using UnityEngine;
using System;
using Lootbound.Gameplay.Player;

namespace Lootbound.Gameplay.Interaction
{
    /// <summary>
    /// Handles player interaction with IInteractable objects in the world.
    /// Uses raycast/spherecast from camera to detect interactables.
    /// </summary>
    public class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] private InteractionConfig config;
        [SerializeField] private Transform raycastOrigin;
        [SerializeField] private PlayerInputReader inputReader;

        // Current state
        private IInteractable currentTarget;
        private IInteractable activeInteraction;
        private float holdProgress;
        private bool isHolding;

        // Events for UI binding
        public event Action<IInteractable> OnTargetChanged;
        public event Action<float> OnHoldProgressChanged;
        public event Action<IInteractable> OnInteractionCompleted;

        // Public accessors
        public IInteractable CurrentTarget => currentTarget;
        public bool HasTarget => currentTarget != null && currentTarget as UnityEngine.Object != null;
        public float HoldProgress => holdProgress;
        public bool IsInteracting => activeInteraction != null && activeInteraction as UnityEngine.Object != null;
        public InteractionConfig Config => config;

        private void Awake()
        {
            ValidateReferences();
        }

        private void OnEnable()
        {
            if (inputReader != null)
            {
                inputReader.OnInteractPressed += StartInteraction;
                inputReader.OnInteractReleased += StopInteraction;
                Debug.Log("[PlayerInteractor] Subscribed to input events");
            }
        }

        private void OnDisable()
        {
            if (inputReader != null)
            {
                inputReader.OnInteractPressed -= StartInteraction;
                inputReader.OnInteractReleased -= StopInteraction;
            }
        }

        private void ValidateReferences()
        {
            if (config == null)
            {
                Debug.LogError("[PlayerInteractor] InteractionConfig is not assigned!");
            }

            if (raycastOrigin == null)
            {
                Debug.LogWarning("[PlayerInteractor] RaycastOrigin not assigned, using Camera.main.");
            }

            if (inputReader == null)
            {
                Debug.LogError("[PlayerInteractor] PlayerInputReader is not assigned!");
            }
        }

        private void Update()
        {
            if (config == null) return;

            // Check if current target was destroyed (e.g., picked up)
            CheckTargetDestroyed();

            UpdateTargetDetection();
            UpdateHoldProgress();
        }

        /// <summary>
        /// Check if the current target was destroyed and clear references.
        /// </summary>
        private void CheckTargetDestroyed()
        {
            // Unity objects return true for == null when destroyed
            if (currentTarget != null && currentTarget as UnityEngine.Object == null)
            {
                currentTarget = null;
                activeInteraction = null;
                isHolding = false;
                holdProgress = 0f;
                OnTargetChanged?.Invoke(null);
                OnHoldProgressChanged?.Invoke(0f);
            }
        }

        /// <summary>
        /// Detect interactable objects via raycast/spherecast.
        /// </summary>
        private void UpdateTargetDetection()
        {
            // Don't change target while actively interacting
            if (activeInteraction != null) return;

            Transform origin = raycastOrigin;
            if (origin == null && Camera.main != null)
            {
                origin = Camera.main.transform;
            }

            if (origin == null) return;

            IInteractable detected = null;
            RaycastHit hit;

            bool didHit;
            if (config.SpherecastRadius > 0)
            {
                didHit = Physics.SphereCast(
                    origin.position,
                    config.SpherecastRadius,
                    origin.forward,
                    out hit,
                    config.RaycastDistance,
                    config.InteractableLayer,
                    QueryTriggerInteraction.Collide
                );
            }
            else
            {
                didHit = Physics.Raycast(
                    origin.position,
                    origin.forward,
                    out hit,
                    config.RaycastDistance,
                    config.InteractableLayer,
                    QueryTriggerInteraction.Collide
                );
            }

            if (didHit)
            {
                // Try to get IInteractable from hit object or parents
                detected = hit.collider.GetComponentInParent<IInteractable>();

                // Check if it can be interacted with
                if (detected != null && !detected.CanInteract)
                {
                    detected = null;
                }
            }

            // Update target if changed
            if (detected != currentTarget)
            {
                currentTarget = detected;
                OnTargetChanged?.Invoke(currentTarget);

                if (currentTarget != null)
                {
                    Debug.Log($"[PlayerInteractor] Target found: {currentTarget.InteractionPrompt}");
                }
            }
        }

        /// <summary>
        /// Update hold progress for hold-type interactions.
        /// </summary>
        private void UpdateHoldProgress()
        {
            if (activeInteraction == null || !isHolding) return;

            float holdDuration = activeInteraction.HoldDuration;
            if (holdDuration <= 0)
            {
                // Instant interaction - complete immediately
                CompleteInteraction();
                return;
            }

            holdProgress += Time.deltaTime / holdDuration;

            if (holdProgress >= 1f)
            {
                holdProgress = 1f;
                OnHoldProgressChanged?.Invoke(holdProgress);
                CompleteInteraction();
            }
            else
            {
                OnHoldProgressChanged?.Invoke(holdProgress);
            }
        }

        /// <summary>
        /// Called when the player presses the interact button.
        /// </summary>
        public void StartInteraction()
        {
            if (currentTarget == null || !currentTarget.CanInteract) return;
            if (activeInteraction != null) return;

            activeInteraction = currentTarget;
            isHolding = true;
            holdProgress = 0f;

            activeInteraction.OnInteractionStart(this);
            OnHoldProgressChanged?.Invoke(holdProgress);

            // For instant interactions, complete immediately in UpdateHoldProgress
        }

        /// <summary>
        /// Called when the player releases the interact button.
        /// </summary>
        public void StopInteraction()
        {
            if (activeInteraction == null) return;

            if (holdProgress < 1f)
            {
                // Cancelled before completion
                activeInteraction.OnInteractionCancel(this);
            }

            isHolding = false;
            holdProgress = 0f;
            activeInteraction = null;
            OnHoldProgressChanged?.Invoke(holdProgress);
        }

        /// <summary>
        /// Complete the current interaction.
        /// </summary>
        private void CompleteInteraction()
        {
            if (activeInteraction == null) return;

            var completed = activeInteraction;
            completed.OnInteractionComplete(this);

            isHolding = false;
            holdProgress = 0f;
            activeInteraction = null;

            OnInteractionCompleted?.Invoke(completed);
            OnHoldProgressChanged?.Invoke(holdProgress);
        }

        /// <summary>
        /// Force cancel the current interaction.
        /// </summary>
        public void CancelInteraction()
        {
            if (activeInteraction != null)
            {
                activeInteraction.OnInteractionCancel(this);
                isHolding = false;
                holdProgress = 0f;
                activeInteraction = null;
                OnHoldProgressChanged?.Invoke(holdProgress);
            }
        }

        /// <summary>
        /// Get the distance to the current target.
        /// </summary>
        public float GetDistanceToTarget()
        {
            // Check for null or destroyed target
            if (currentTarget == null || currentTarget as UnityEngine.Object == null)
                return float.MaxValue;

            Transform origin = raycastOrigin;
            if (origin == null && Camera.main != null)
            {
                origin = Camera.main.transform;
            }

            if (origin == null) return float.MaxValue;

            var targetTransform = currentTarget.InteractionTransform;
            if (targetTransform == null) return float.MaxValue;

            return Vector3.Distance(origin.position, targetTransform.position);
        }

        /// <summary>
        /// Calculate prompt opacity based on distance.
        /// </summary>
        public float GetPromptOpacity()
        {
            // Check for null or destroyed target
            if (currentTarget == null || currentTarget as UnityEngine.Object == null || config == null)
                return 0f;

            float distance = GetDistanceToTarget();

            if (distance <= config.PromptFadeStartDistance)
            {
                return 1f;
            }

            if (distance >= config.PromptFadeEndDistance)
            {
                return 0f;
            }

            float t = Mathf.InverseLerp(config.PromptFadeStartDistance, config.PromptFadeEndDistance, distance);
            return 1f - t;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (config == null) return;

            Transform origin = raycastOrigin;
            if (origin == null && Camera.main != null)
            {
                origin = Camera.main.transform;
            }

            if (origin == null) return;

            Gizmos.color = currentTarget != null ? Color.green : Color.yellow;

            if (config.SpherecastRadius > 0)
            {
                // Draw spherecast visualization
                Vector3 endPos = origin.position + origin.forward * config.RaycastDistance;
                Gizmos.DrawWireSphere(origin.position, config.SpherecastRadius);
                Gizmos.DrawWireSphere(endPos, config.SpherecastRadius);
                Gizmos.DrawLine(origin.position, endPos);
            }
            else
            {
                // Draw raycast
                Gizmos.DrawRay(origin.position, origin.forward * config.RaycastDistance);
            }
        }
#endif
    }
}

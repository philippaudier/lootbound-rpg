using UnityEngine;

namespace Lootbound.Gameplay.Interaction
{
    /// <summary>
    /// Configuration for the player interaction system.
    /// </summary>
    [CreateAssetMenu(fileName = "InteractionConfig", menuName = "Lootbound/Interaction/Interaction Config")]
    public class InteractionConfig : ScriptableObject
    {
        [Header("Detection")]
        [Tooltip("Maximum distance for interaction detection via raycast.")]
        [SerializeField, Range(0.5f, 10f)] private float raycastDistance = 3f;

        [Tooltip("Radius for spherecast detection. Set to 0 for pure raycast.")]
        [SerializeField, Range(0f, 0.5f)] private float spherecastRadius = 0.1f;

        [Tooltip("Layer mask for interactable objects.")]
        [SerializeField] private LayerMask interactableLayer = ~0;

        [Header("Hold Interaction")]
        [Tooltip("Default hold duration for interactions that don't specify their own.")]
        [SerializeField, Range(0f, 5f)] private float defaultHoldDuration = 0f;

        [Header("UI")]
        [Tooltip("Distance at which the prompt starts fading out.")]
        [SerializeField, Range(0.5f, 5f)] private float promptFadeStartDistance = 2.5f;

        [Tooltip("Distance at which the prompt is fully faded.")]
        [SerializeField, Range(1f, 10f)] private float promptFadeEndDistance = 3f;

        // Public accessors
        public float RaycastDistance => raycastDistance;
        public float SpherecastRadius => spherecastRadius;
        public LayerMask InteractableLayer => interactableLayer;
        public float DefaultHoldDuration => defaultHoldDuration;
        public float PromptFadeStartDistance => promptFadeStartDistance;
        public float PromptFadeEndDistance => promptFadeEndDistance;

        /// <summary>
        /// Validate configuration values.
        /// </summary>
        public bool Validate(out string error)
        {
            if (raycastDistance <= 0)
            {
                error = "Raycast distance must be positive.";
                return false;
            }

            if (promptFadeStartDistance >= promptFadeEndDistance)
            {
                error = "Prompt fade start distance must be less than fade end distance.";
                return false;
            }

            error = null;
            return true;
        }
    }
}

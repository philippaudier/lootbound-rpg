using UnityEngine;

namespace Lootbound.Gameplay.Interaction
{
    /// <summary>
    /// Interface for objects that can be interacted with by the player.
    /// Implement this on any GameObject that should respond to player interaction.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>
        /// Display name shown in the interaction prompt.
        /// </summary>
        string InteractionPrompt { get; }

        /// <summary>
        /// Whether this object can currently be interacted with.
        /// Used to disable interaction based on game state.
        /// </summary>
        bool CanInteract { get; }

        /// <summary>
        /// Optional icon identifier for the interaction prompt UI.
        /// Returns null if no icon should be displayed.
        /// </summary>
        string IconId { get; }

        /// <summary>
        /// Called when the player begins interacting (press).
        /// </summary>
        /// <param name="interactor">The PlayerInteractor initiating the interaction.</param>
        void OnInteractionStart(PlayerInteractor interactor);

        /// <summary>
        /// Called when the player completes a hold interaction.
        /// For instant interactions, this is called immediately after OnInteractionStart.
        /// </summary>
        /// <param name="interactor">The PlayerInteractor completing the interaction.</param>
        void OnInteractionComplete(PlayerInteractor interactor);

        /// <summary>
        /// Called when the player cancels an interaction (releases before hold completes).
        /// </summary>
        /// <param name="interactor">The PlayerInteractor canceling the interaction.</param>
        void OnInteractionCancel(PlayerInteractor interactor);

        /// <summary>
        /// Duration in seconds the player must hold to complete interaction.
        /// Return 0 for instant interactions.
        /// </summary>
        float HoldDuration { get; }

        /// <summary>
        /// The transform to use for distance calculations.
        /// Usually returns the GameObject's transform.
        /// </summary>
        Transform InteractionTransform { get; }
    }
}

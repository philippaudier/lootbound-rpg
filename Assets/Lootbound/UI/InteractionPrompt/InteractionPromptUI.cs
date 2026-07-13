using UnityEngine;
using UnityEngine.UIElements;
using Lootbound.Gameplay.Interaction;

namespace Lootbound.UI
{
    /// <summary>
    /// UI for displaying interaction prompts using UI Toolkit.
    /// Shows when the player is looking at an interactable object.
    /// </summary>
    public class InteractionPromptUI : MonoBehaviour
    {
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private PlayerInteractor playerInteractor;

        private VisualElement root;
        private VisualElement promptContainer;
        private Label promptLabel;
        private Label keyHintLabel;
        private VisualElement holdProgressBar;
        private VisualElement holdProgressFill;

        private bool isVisible;

        private void Awake()
        {
            if (uiDocument == null)
            {
                Debug.LogError("[InteractionPromptUI] UIDocument is not assigned!");
                return;
            }

            SetupUI();
        }

        private void OnEnable()
        {
            if (playerInteractor != null)
            {
                playerInteractor.OnTargetChanged += HandleTargetChanged;
                playerInteractor.OnHoldProgressChanged += HandleHoldProgressChanged;
            }
        }

        private void OnDisable()
        {
            if (playerInteractor != null)
            {
                playerInteractor.OnTargetChanged -= HandleTargetChanged;
                playerInteractor.OnHoldProgressChanged -= HandleHoldProgressChanged;
            }
        }

        private void SetupUI()
        {
            root = uiDocument.rootVisualElement;

            if (root == null)
            {
                Debug.LogError("[InteractionPromptUI] Root visual element is null!");
                return;
            }

            // Set lower sort order (behind inventory)
            uiDocument.sortingOrder = 0;

            promptContainer = root.Q<VisualElement>("prompt-container");
            promptLabel = root.Q<Label>("prompt-label");
            keyHintLabel = root.Q<Label>("key-hint-label");
            holdProgressBar = root.Q<VisualElement>("hold-progress-bar");
            holdProgressFill = root.Q<VisualElement>("hold-progress-fill");

            // Hide by default
            if (promptContainer != null)
            {
                promptContainer.style.display = DisplayStyle.None;
                Debug.Log("[InteractionPromptUI] Prompt hidden on setup");
            }
            else
            {
                Debug.LogError("[InteractionPromptUI] prompt-container not found!");
            }

            // Don't block clicks (this UI doesn't need interaction)
            root.pickingMode = PickingMode.Ignore;
        }

        private void Update()
        {
            if (playerInteractor == null || promptContainer == null) return;

            // Update opacity based on distance
            if (isVisible && playerInteractor.HasTarget)
            {
                float opacity = playerInteractor.GetPromptOpacity();
                promptContainer.style.opacity = opacity;
            }
        }

        private void HandleTargetChanged(IInteractable target)
        {
            if (target == null)
            {
                HidePrompt();
            }
            else
            {
                ShowPrompt(target);
            }
        }

        private void HandleHoldProgressChanged(float progress)
        {
            if (holdProgressFill != null)
            {
                holdProgressFill.style.width = Length.Percent(progress * 100f);
            }

            // Show/hide progress bar based on whether we're holding
            if (holdProgressBar != null)
            {
                holdProgressBar.style.display = progress > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private void ShowPrompt(IInteractable target)
        {
            if (promptContainer == null) return;

            isVisible = true;
            promptContainer.style.display = DisplayStyle.Flex;

            if (promptLabel != null)
            {
                promptLabel.text = target.InteractionPrompt;
            }

            if (keyHintLabel != null)
            {
                keyHintLabel.text = target.HoldDuration > 0 ? "[E] Hold" : "[E]";
            }

            // Reset progress bar
            if (holdProgressFill != null)
            {
                holdProgressFill.style.width = Length.Percent(0);
            }

            if (holdProgressBar != null)
            {
                holdProgressBar.style.display = DisplayStyle.None;
            }
        }

        private void HidePrompt()
        {
            if (promptContainer == null) return;

            isVisible = false;
            promptContainer.style.display = DisplayStyle.None;
        }
    }
}

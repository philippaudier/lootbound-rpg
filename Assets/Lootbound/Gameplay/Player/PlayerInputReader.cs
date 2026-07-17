using UnityEngine;
using UnityEngine.InputSystem;
using System;

namespace Lootbound.Gameplay.Player
{
    /// <summary>
    /// Reads input from the Unity Input System and exposes semantic gameplay intentions.
    /// Does not process or apply movement - only provides input state.
    /// </summary>
    public class PlayerInputReader : MonoBehaviour
    {
        [SerializeField] private InputActionAsset inputActions;

        private InputAction moveAction;
        private InputAction lookAction;
        private InputAction jumpAction;
        private InputAction sprintAction;
        private InputAction crouchAction;
        private InputAction pauseAction;
        private InputAction interactAction;
        private InputAction inventoryAction;
        private InputAction attackAction;
        private InputAction dodgeAction;

        private Vector2 moveInput;
        private Vector2 lookInput;
        private bool jumpPressedThisFrame;
        private bool sprintHeld;
        private bool crouchHeld;
        private bool pausePressedThisFrame;
        private bool interactHeld;
        private bool interactPressedThisFrame;
        private bool interactReleasedThisFrame;
        private bool inventoryPressedThisFrame;
        private bool attackPressedThisFrame;
        private bool dodgePressedThisFrame;

        private bool inputEnabled = true;

        // Events for interaction system
        public event Action OnInteractPressed;
        public event Action OnInteractReleased;
        public event Action OnInventoryToggled;

        // Events for combat system
        public event Action OnAttackPressed;
        public event Action OnDodgePressed;

        // Public accessors for other components
        public Vector2 MoveInput => inputEnabled ? moveInput : Vector2.zero;
        public Vector2 LookInput => inputEnabled ? lookInput : Vector2.zero;
        public bool JumpPressedThisFrame => inputEnabled && jumpPressedThisFrame;
        public bool SprintHeld => inputEnabled && sprintHeld;
        public bool CrouchHeld => inputEnabled && crouchHeld;
        public bool PausePressedThisFrame => pausePressedThisFrame;
        public bool InteractHeld => inputEnabled && interactHeld;
        public bool InteractPressedThisFrame => inputEnabled && interactPressedThisFrame;
        public bool InteractReleasedThisFrame => inputEnabled && interactReleasedThisFrame;
        public bool InventoryPressedThisFrame => inputEnabled && inventoryPressedThisFrame;
        public bool AttackPressedThisFrame => inputEnabled && attackPressedThisFrame;
        public bool DodgePressedThisFrame => inputEnabled && dodgePressedThisFrame;
        public bool InputEnabled => inputEnabled;

        private void Awake()
        {
            if (inputActions == null)
            {
                Debug.LogError("[PlayerInputReader] InputActionAsset is not assigned!");
                return;
            }

            SetupActions();
        }

        private void SetupActions()
        {
            var playerMap = inputActions.FindActionMap("Player");
            if (playerMap == null)
            {
                Debug.LogError("[PlayerInputReader] Player action map not found!");
                return;
            }

            moveAction = playerMap.FindAction("Move");
            lookAction = playerMap.FindAction("Look");
            jumpAction = playerMap.FindAction("Jump");
            sprintAction = playerMap.FindAction("Sprint");
            crouchAction = playerMap.FindAction("Crouch");
            pauseAction = playerMap.FindAction("Pause");
            interactAction = playerMap.FindAction("Interact");
            inventoryAction = playerMap.FindAction("Inventory");
            attackAction = playerMap.FindAction("Attack");
            dodgeAction = playerMap.FindAction("Dodge");

            // Subscribe to events
            if (jumpAction != null)
            {
                jumpAction.performed += OnJumpPerformed;
            }

            if (pauseAction != null)
            {
                pauseAction.performed += OnPausePerformed;
            }

            if (interactAction != null)
            {
                interactAction.started += OnInteractStarted;
                interactAction.canceled += OnInteractCanceled;
            }

            if (inventoryAction != null)
            {
                inventoryAction.performed += OnInventoryPerformed;
            }

            if (attackAction != null)
            {
                attackAction.performed += OnAttackPerformed;
            }

            if (dodgeAction != null)
            {
                dodgeAction.performed += OnDodgePerformed;
            }
        }

        private void OnEnable()
        {
            inputActions?.Enable();
        }

        private void OnDisable()
        {
            inputActions?.Disable();
        }

        private void OnDestroy()
        {
            if (jumpAction != null)
            {
                jumpAction.performed -= OnJumpPerformed;
            }

            if (pauseAction != null)
            {
                pauseAction.performed -= OnPausePerformed;
            }

            if (interactAction != null)
            {
                interactAction.started -= OnInteractStarted;
                interactAction.canceled -= OnInteractCanceled;
            }

            if (inventoryAction != null)
            {
                inventoryAction.performed -= OnInventoryPerformed;
            }

            if (attackAction != null)
            {
                attackAction.performed -= OnAttackPerformed;
            }

            if (dodgeAction != null)
            {
                dodgeAction.performed -= OnDodgePerformed;
            }
        }

        private void Update()
        {
            ReadInputValues();
        }

        private void LateUpdate()
        {
            // Clear one-frame flags after all Update processing
            jumpPressedThisFrame = false;
            pausePressedThisFrame = false;
            interactPressedThisFrame = false;
            interactReleasedThisFrame = false;
            inventoryPressedThisFrame = false;
            attackPressedThisFrame = false;
            dodgePressedThisFrame = false;
        }

        private void ReadInputValues()
        {
            if (moveAction != null)
            {
                moveInput = moveAction.ReadValue<Vector2>();
            }

            if (lookAction != null)
            {
                lookInput = lookAction.ReadValue<Vector2>();
            }

            if (sprintAction != null)
            {
                sprintHeld = sprintAction.IsPressed();
            }

            if (crouchAction != null)
            {
                crouchHeld = crouchAction.IsPressed();
            }

            if (interactAction != null)
            {
                interactHeld = interactAction.IsPressed();
            }
        }

        private void OnJumpPerformed(InputAction.CallbackContext context)
        {
            jumpPressedThisFrame = true;
        }

        private void OnPausePerformed(InputAction.CallbackContext context)
        {
            pausePressedThisFrame = true;
        }

        private void OnInteractStarted(InputAction.CallbackContext context)
        {
            interactPressedThisFrame = true;
            if (inputEnabled)
            {
                OnInteractPressed?.Invoke();
            }
        }

        private void OnInteractCanceled(InputAction.CallbackContext context)
        {
            interactReleasedThisFrame = true;
            if (inputEnabled)
            {
                OnInteractReleased?.Invoke();
            }
        }

        private void OnInventoryPerformed(InputAction.CallbackContext context)
        {
            inventoryPressedThisFrame = true;
            if (inputEnabled)
            {
                OnInventoryToggled?.Invoke();
            }
        }

        private void OnAttackPerformed(InputAction.CallbackContext context)
        {
            attackPressedThisFrame = true;
            if (inputEnabled)
            {
                OnAttackPressed?.Invoke();
            }
        }

        private void OnDodgePerformed(InputAction.CallbackContext context)
        {
            dodgePressedThisFrame = true;
            if (inputEnabled)
            {
                OnDodgePressed?.Invoke();
            }
        }

        /// <summary>
        /// Enable or disable gameplay input processing.
        /// Pause input is always processed.
        /// </summary>
        public void SetInputEnabled(bool enabled)
        {
            inputEnabled = enabled;
        }

        /// <summary>
        /// Get normalized movement input (clamped to magnitude 1)
        /// </summary>
        public Vector2 GetNormalizedMoveInput()
        {
            if (!inputEnabled) return Vector2.zero;

            Vector2 input = moveInput;
            if (input.sqrMagnitude > 1f)
            {
                input.Normalize();
            }
            return input;
        }
    }
}

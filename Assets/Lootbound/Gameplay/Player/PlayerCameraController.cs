using System;
using UnityEngine;

namespace Lootbound.Gameplay.Player
{
    /// <summary>
    /// Controls first-person camera rotation.
    /// Handles horizontal rotation on the player body and vertical pitch on the camera.
    /// </summary>
    public class PlayerCameraController : MonoBehaviour
    {
        [SerializeField] private PlayerMovementConfig config;
        [SerializeField] private PlayerInputReader inputReader;
        [SerializeField] private Transform playerBody;
        [SerializeField] private Transform cameraTransform;

        private float pitch;
        private float yaw;

        private bool cursorLocked = true;

        public float Pitch => pitch;
        public float Yaw => yaw;
        public bool CursorLocked => cursorLocked;

        /// <summary>
        /// Invoked when the pause input is pressed.
        /// Subscribe to this to handle pause menu or other pause logic.
        /// </summary>
        public event Action OnPauseRequested;

        private void Awake()
        {
            ValidateReferences();
        }

        private void ValidateReferences()
        {
            if (config == null)
            {
                Debug.LogError("[PlayerCameraController] PlayerMovementConfig is not assigned!");
            }

            if (inputReader == null)
            {
                Debug.LogError("[PlayerCameraController] PlayerInputReader is not assigned!");
            }

            if (playerBody == null)
            {
                Debug.LogError("[PlayerCameraController] PlayerBody transform is not assigned!");
            }

            if (cameraTransform == null)
            {
                Debug.LogError("[PlayerCameraController] CameraTransform is not assigned!");
            }
        }

        private void Start()
        {
            // Initialize from current rotation
            if (playerBody != null)
            {
                yaw = playerBody.eulerAngles.y;
            }

            if (cameraTransform != null)
            {
                pitch = cameraTransform.localEulerAngles.x;
                // Convert from 0-360 to -180 to 180 range
                if (pitch > 180f)
                {
                    pitch -= 360f;
                }
            }

            LockCursor();
        }

        private void Update()
        {
            if (config == null || inputReader == null) return;

            HandlePause();

            if (cursorLocked)
            {
                HandleLook();
            }
        }

        private void HandlePause()
        {
            if (inputReader.PausePressedThisFrame)
            {
                // Invoke event for pause menu handling
                // If no subscribers, fall back to cursor toggle for backwards compatibility
                if (OnPauseRequested != null)
                {
                    OnPauseRequested.Invoke();
                }
                else
                {
                    ToggleCursorLock();
                }
            }
        }

        private void HandleLook()
        {
            Vector2 lookInput = inputReader.LookInput;

            // Apply sensitivity
            float mouseX = lookInput.x * config.MouseSensitivity * 0.1f;
            float mouseY = lookInput.y * config.MouseSensitivity * 0.1f;

            // Horizontal rotation (yaw) - rotate the player body
            yaw += mouseX;

            // Vertical rotation (pitch) - rotate the camera only
            pitch -= mouseY;
            pitch = Mathf.Clamp(pitch, config.MinPitch, config.MaxPitch);

            // Apply rotations
            if (playerBody != null)
            {
                playerBody.rotation = Quaternion.Euler(0f, yaw, 0f);
            }

            if (cameraTransform != null)
            {
                cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }
        }

        /// <summary>
        /// Lock the cursor and enable look input
        /// </summary>
        public void LockCursor()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            cursorLocked = true;
        }

        /// <summary>
        /// Unlock the cursor and disable look input
        /// </summary>
        public void UnlockCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            cursorLocked = false;
        }

        /// <summary>
        /// Toggle cursor lock state
        /// </summary>
        public void ToggleCursorLock()
        {
            if (cursorLocked)
            {
                UnlockCursor();
            }
            else
            {
                LockCursor();
            }
        }

        /// <summary>
        /// Set camera height (called by stance controller during crouch transitions)
        /// </summary>
        public void SetCameraHeight(float height)
        {
            if (cameraTransform != null)
            {
                Vector3 localPos = cameraTransform.localPosition;
                localPos.y = height;
                cameraTransform.localPosition = localPos;
            }
        }
    }
}

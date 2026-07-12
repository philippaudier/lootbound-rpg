using UnityEngine;

namespace Lootbound.Gameplay.Player
{
    /// <summary>
    /// Manages player stance (standing/crouching) with smooth transitions.
    /// Updates CharacterController height and camera position.
    /// </summary>
    public class PlayerStanceController : MonoBehaviour
    {
        [SerializeField] private PlayerMovementConfig config;
        [SerializeField] private PlayerInputReader inputReader;
        [SerializeField] private FirstPersonMotor motor;
        [SerializeField] private PlayerCameraController cameraController;

        private float currentHeight;
        private float targetHeight;
        private bool isCrouching;
        private bool wantsToCrouch;

        // Camera height offset from controller height (eyes are near top of head)
        private const float CameraHeightOffset = -0.15f;

        public bool IsCrouching => isCrouching;
        public float CurrentHeight => currentHeight;
        public float TargetHeight => targetHeight;
        public float StanceProgress => Mathf.InverseLerp(config.CrouchingHeight, config.StandingHeight, currentHeight);

        private void Awake()
        {
            ValidateReferences();
        }

        private void ValidateReferences()
        {
            if (config == null)
            {
                Debug.LogError("[PlayerStanceController] PlayerMovementConfig is not assigned!");
            }

            if (inputReader == null)
            {
                Debug.LogError("[PlayerStanceController] PlayerInputReader is not assigned!");
            }

            if (motor == null)
            {
                Debug.LogError("[PlayerStanceController] FirstPersonMotor is not assigned!");
            }
        }

        private void Start()
        {
            if (config != null)
            {
                currentHeight = config.StandingHeight;
                targetHeight = config.StandingHeight;
                ApplyHeight(currentHeight);
            }
        }

        private void Update()
        {
            if (config == null || inputReader == null) return;

            UpdateStanceIntent();
            UpdateStanceTransition();
        }

        private void UpdateStanceIntent()
        {
            wantsToCrouch = inputReader.CrouchHeld;

            if (wantsToCrouch)
            {
                targetHeight = config.CrouchingHeight;
                isCrouching = true;
            }
            else
            {
                // Only stand up if there's headroom
                if (motor != null && motor.HasHeadroom(config.StandingHeight))
                {
                    targetHeight = config.StandingHeight;
                    isCrouching = false;
                }
                else
                {
                    // Stay crouched if blocked
                    targetHeight = config.CrouchingHeight;
                    isCrouching = true;
                }
            }
        }

        private void UpdateStanceTransition()
        {
            if (Mathf.Approximately(currentHeight, targetHeight))
            {
                return;
            }

            // Smoothly transition to target height
            currentHeight = Mathf.MoveTowards(
                currentHeight,
                targetHeight,
                config.StanceTransitionSpeed * Time.deltaTime
            );

            ApplyHeight(currentHeight);
        }

        private void ApplyHeight(float height)
        {
            // Update motor's CharacterController
            if (motor != null)
            {
                motor.SetControllerHeight(height);
            }

            // Update camera position
            if (cameraController != null)
            {
                float cameraHeight = height + CameraHeightOffset;
                cameraController.SetCameraHeight(cameraHeight);
            }
        }

        /// <summary>
        /// Force a specific stance immediately (no transition)
        /// </summary>
        public void SetStanceImmediate(bool crouching)
        {
            if (config == null) return;

            isCrouching = crouching;
            currentHeight = crouching ? config.CrouchingHeight : config.StandingHeight;
            targetHeight = currentHeight;
            ApplyHeight(currentHeight);
        }
    }
}

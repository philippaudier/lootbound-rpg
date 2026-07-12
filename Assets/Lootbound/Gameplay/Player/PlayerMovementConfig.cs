using UnityEngine;

namespace Lootbound.Gameplay.Player
{
    [CreateAssetMenu(fileName = "PlayerMovementConfig", menuName = "Lootbound/Player Movement Config")]
    public class PlayerMovementConfig : ScriptableObject
    {
        [Header("Movement Speeds")]
        [Tooltip("Walking speed in m/s")]
        [SerializeField] private float walkSpeed = 4.2f;

        [Tooltip("Sprint speed in m/s")]
        [SerializeField] private float sprintSpeed = 6.2f;

        [Tooltip("Crouching speed in m/s")]
        [SerializeField] private float crouchSpeed = 2.2f;

        [Header("Acceleration")]
        [Tooltip("Ground acceleration in m/s²")]
        [SerializeField] private float groundAcceleration = 30f;

        [Tooltip("Ground deceleration in m/s²")]
        [SerializeField] private float groundDeceleration = 35f;

        [Tooltip("Air acceleration in m/s²")]
        [SerializeField] private float airAcceleration = 8f;

        [Tooltip("Air control multiplier (0-1)")]
        [Range(0f, 1f)]
        [SerializeField] private float airControl = 0.3f;

        [Header("Jump & Gravity")]
        [Tooltip("Jump height in meters")]
        [SerializeField] private float jumpHeight = 1.2f;

        [Tooltip("Gravity acceleration (negative value)")]
        [SerializeField] private float gravity = -25f;

        [Tooltip("Maximum falling speed")]
        [SerializeField] private float terminalVelocity = -50f;

        [Tooltip("Coyote time duration in seconds")]
        [SerializeField] private float coyoteTime = 0.1f;

        [Tooltip("Jump buffer duration in seconds")]
        [SerializeField] private float jumpBufferTime = 0.12f;

        [Header("Ground Detection")]
        [Tooltip("Downward force when grounded to maintain contact")]
        [SerializeField] private float groundedStickForce = 5f;

        [Tooltip("Maximum walkable slope angle in degrees")]
        [SerializeField] private float maxSlopeAngle = 45f;

        [Header("Stance")]
        [Tooltip("Standing height")]
        [SerializeField] private float standingHeight = 1.8f;

        [Tooltip("Crouching height")]
        [SerializeField] private float crouchingHeight = 1.1f;

        [Tooltip("Speed of stance transitions")]
        [SerializeField] private float stanceTransitionSpeed = 8f;

        [Header("Camera")]
        [Tooltip("Mouse sensitivity")]
        [SerializeField] private float mouseSensitivity = 2f;

        [Tooltip("Minimum pitch angle (looking down)")]
        [SerializeField] private float minPitch = -85f;

        [Tooltip("Maximum pitch angle (looking up)")]
        [SerializeField] private float maxPitch = 85f;

        [Header("Character Controller")]
        [Tooltip("Controller radius")]
        [SerializeField] private float controllerRadius = 0.3f;

        [Tooltip("Skin width for collision detection")]
        [SerializeField] private float skinWidth = 0.02f;

        [Tooltip("Step offset for climbing small obstacles")]
        [SerializeField] private float stepOffset = 0.35f;

        // Properties
        public float WalkSpeed => walkSpeed;
        public float SprintSpeed => sprintSpeed;
        public float CrouchSpeed => crouchSpeed;

        public float GroundAcceleration => groundAcceleration;
        public float GroundDeceleration => groundDeceleration;
        public float AirAcceleration => airAcceleration;
        public float AirControl => airControl;

        public float JumpHeight => jumpHeight;
        public float Gravity => gravity;
        public float TerminalVelocity => terminalVelocity;
        public float CoyoteTime => coyoteTime;
        public float JumpBufferTime => jumpBufferTime;

        public float GroundedStickForce => groundedStickForce;
        public float MaxSlopeAngle => maxSlopeAngle;

        public float StandingHeight => standingHeight;
        public float CrouchingHeight => crouchingHeight;
        public float StanceTransitionSpeed => stanceTransitionSpeed;

        public float MouseSensitivity => mouseSensitivity;
        public float MinPitch => minPitch;
        public float MaxPitch => maxPitch;

        public float ControllerRadius => controllerRadius;
        public float SkinWidth => skinWidth;
        public float StepOffset => stepOffset;

        /// <summary>
        /// Calculate jump velocity from jump height and gravity
        /// </summary>
        public float CalculateJumpVelocity()
        {
            return Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        /// <summary>
        /// Get the current target speed based on stance and sprint state
        /// </summary>
        public float GetTargetSpeed(bool isCrouching, bool isSprinting)
        {
            if (isCrouching) return crouchSpeed;
            if (isSprinting) return sprintSpeed;
            return walkSpeed;
        }
    }
}

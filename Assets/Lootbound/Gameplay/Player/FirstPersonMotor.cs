using UnityEngine;

namespace Lootbound.Gameplay.Player
{
    /// <summary>
    /// Handles first-person character movement using CharacterController.
    /// Calculates and applies movement based on input from PlayerInputReader.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonMotor : MonoBehaviour
    {
        [SerializeField] private PlayerMovementConfig config;
        [SerializeField] private PlayerInputReader inputReader;
        [SerializeField] private PlayerStanceController stanceController;

        private CharacterController controller;

        // Movement state
        private Vector3 velocity;
        private Vector3 horizontalVelocity;
        private float verticalVelocity;

        // Ground detection
        private bool isGrounded;
        private bool wasGroundedLastFrame;
        private Vector3 groundNormal = Vector3.up;
        private float groundAngle;

        // Jump state
        private float coyoteTimeCounter;
        private float jumpBufferCounter;
        private bool hasJumpedDuringCoyote;

        // Sprint state
        private bool isSprinting;

        // Exposed state for other systems
        public bool IsGrounded => isGrounded;
        public bool IsSprinting => isSprinting;
        public bool IsCrouching => stanceController != null && stanceController.IsCrouching;
        public Vector3 Velocity => velocity;
        public Vector3 HorizontalVelocity => horizontalVelocity;
        public float VerticalVelocity => verticalVelocity;
        public float CurrentSpeed => horizontalVelocity.magnitude;
        public float GroundAngle => groundAngle;
        public Vector3 GroundNormal => groundNormal;
        public float CoyoteTimeRemaining => coyoteTimeCounter;
        public float JumpBufferRemaining => jumpBufferCounter;
        public CollisionFlags LastCollisionFlags { get; private set; }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            ValidateReferences();
        }

        private void ValidateReferences()
        {
            if (config == null)
            {
                Debug.LogError("[FirstPersonMotor] PlayerMovementConfig is not assigned!");
            }

            if (inputReader == null)
            {
                Debug.LogError("[FirstPersonMotor] PlayerInputReader is not assigned!");
            }
        }

        private void Start()
        {
            ConfigureCharacterController();
        }

        private void ConfigureCharacterController()
        {
            if (config == null) return;

            controller.radius = config.ControllerRadius;
            controller.skinWidth = config.SkinWidth;
            controller.stepOffset = config.StepOffset;
            controller.slopeLimit = config.MaxSlopeAngle;
        }

        private void Update()
        {
            if (config == null || inputReader == null) return;

            UpdateGroundDetection();
            UpdateTimers();
            UpdateSprint();
            HandleJump();
            ApplyGravity();
            ApplyMovement();
        }

        private void UpdateGroundDetection()
        {
            wasGroundedLastFrame = isGrounded;

            // Use CharacterController's built-in ground check, supplemented by our own
            isGrounded = controller.isGrounded;

            // Additional ground check using sphere cast for more reliable detection
            if (!isGrounded)
            {
                float checkDistance = config.SkinWidth + 0.1f;
                Vector3 spherePosition = transform.position + Vector3.up * config.ControllerRadius;

                if (Physics.SphereCast(spherePosition, config.ControllerRadius * 0.9f, Vector3.down,
                    out RaycastHit hit, checkDistance, ~0, QueryTriggerInteraction.Ignore))
                {
                    groundNormal = hit.normal;
                    groundAngle = Vector3.Angle(Vector3.up, groundNormal);
                    isGrounded = groundAngle <= config.MaxSlopeAngle;
                }
            }
            else
            {
                // When CharacterController reports grounded, do a raycast for normal
                if (Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down,
                    out RaycastHit hit, 0.3f, ~0, QueryTriggerInteraction.Ignore))
                {
                    groundNormal = hit.normal;
                    groundAngle = Vector3.Angle(Vector3.up, groundNormal);
                }
                else
                {
                    groundNormal = Vector3.up;
                    groundAngle = 0f;
                }
            }

            // Reset coyote time when landing
            if (isGrounded && !wasGroundedLastFrame)
            {
                hasJumpedDuringCoyote = false;
            }
        }

        private void UpdateTimers()
        {
            // Coyote time
            if (isGrounded)
            {
                coyoteTimeCounter = config.CoyoteTime;
            }
            else
            {
                coyoteTimeCounter -= Time.deltaTime;
            }

            // Jump buffer
            if (inputReader.JumpPressedThisFrame)
            {
                jumpBufferCounter = config.JumpBufferTime;
            }
            else
            {
                jumpBufferCounter -= Time.deltaTime;
            }
        }

        private void UpdateSprint()
        {
            bool wantsSprint = inputReader.SprintHeld;
            bool hasMovementInput = inputReader.GetNormalizedMoveInput().sqrMagnitude > 0.1f;
            bool canSprint = !IsCrouching && hasMovementInput;

            isSprinting = wantsSprint && canSprint;
        }

        private void HandleJump()
        {
            bool canJump = (isGrounded || (coyoteTimeCounter > 0 && !hasJumpedDuringCoyote));
            bool wantsJump = jumpBufferCounter > 0;

            if (canJump && wantsJump && !IsCrouching)
            {
                verticalVelocity = config.CalculateJumpVelocity();
                jumpBufferCounter = 0;
                hasJumpedDuringCoyote = true;
                coyoteTimeCounter = 0;
                isGrounded = false;
            }
        }

        private void ApplyGravity()
        {
            if (isGrounded && verticalVelocity < 0)
            {
                // Small downward force to maintain ground contact
                verticalVelocity = -config.GroundedStickForce;
            }
            else
            {
                verticalVelocity += config.Gravity * Time.deltaTime;

                // Clamp to terminal velocity
                if (verticalVelocity < config.TerminalVelocity)
                {
                    verticalVelocity = config.TerminalVelocity;
                }
            }
        }

        private void ApplyMovement()
        {
            Vector2 input = inputReader.GetNormalizedMoveInput();
            Vector3 inputDirection = new Vector3(input.x, 0f, input.y);

            // Transform input to world space relative to player facing
            Vector3 worldDirection = transform.TransformDirection(inputDirection);

            // Calculate target speed
            float targetSpeed = config.GetTargetSpeed(IsCrouching, isSprinting);

            // Calculate desired velocity
            Vector3 desiredHorizontalVelocity = worldDirection * targetSpeed;

            // Apply acceleration/deceleration
            float acceleration;
            if (isGrounded)
            {
                acceleration = input.sqrMagnitude > 0.01f
                    ? config.GroundAcceleration
                    : config.GroundDeceleration;
            }
            else
            {
                // Reduced control in air
                acceleration = config.AirAcceleration * config.AirControl;

                // Preserve momentum in air - only allow minor corrections
                if (horizontalVelocity.sqrMagnitude > 0.01f && desiredHorizontalVelocity.sqrMagnitude > 0.01f)
                {
                    // Limit how much the player can change direction in air
                    float currentSpeed = horizontalVelocity.magnitude;
                    desiredHorizontalVelocity = Vector3.ClampMagnitude(desiredHorizontalVelocity, currentSpeed);
                }
            }

            // Smoothly interpolate horizontal velocity
            horizontalVelocity = Vector3.MoveTowards(
                horizontalVelocity,
                desiredHorizontalVelocity,
                acceleration * Time.deltaTime
            );

            // Adjust movement for slopes when grounded
            Vector3 adjustedHorizontalVelocity = horizontalVelocity;
            if (isGrounded && groundAngle > 0.01f && groundAngle <= config.MaxSlopeAngle)
            {
                // Project movement onto slope
                adjustedHorizontalVelocity = Vector3.ProjectOnPlane(horizontalVelocity, groundNormal);
                adjustedHorizontalVelocity = adjustedHorizontalVelocity.normalized * horizontalVelocity.magnitude;
            }

            // Combine horizontal and vertical movement
            velocity = adjustedHorizontalVelocity + Vector3.up * verticalVelocity;

            // Apply movement
            LastCollisionFlags = controller.Move(velocity * Time.deltaTime);

            // Handle hitting ceiling
            if ((LastCollisionFlags & CollisionFlags.Above) != 0 && verticalVelocity > 0)
            {
                verticalVelocity = 0;
            }
        }

        /// <summary>
        /// Update character controller height. Called by PlayerStanceController.
        /// </summary>
        public void SetControllerHeight(float height)
        {
            controller.height = height;
            controller.center = new Vector3(0, height / 2f, 0);
        }

        /// <summary>
        /// Check if there's enough headroom to stand up
        /// </summary>
        public bool HasHeadroom(float targetHeight)
        {
            float currentHeight = controller.height;
            if (targetHeight <= currentHeight) return true;

            float heightDifference = targetHeight - currentHeight;
            Vector3 checkStart = transform.position + Vector3.up * currentHeight;

            return !Physics.SphereCast(
                checkStart,
                config.ControllerRadius * 0.9f,
                Vector3.up,
                out _,
                heightDifference + 0.05f,
                ~0,
                QueryTriggerInteraction.Ignore
            );
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (config == null) return;

            // Ground check sphere
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Vector3 spherePosition = transform.position + Vector3.up * config.ControllerRadius;
            Gizmos.DrawWireSphere(spherePosition, config.ControllerRadius * 0.9f);

            // Ground normal
            if (isGrounded)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawRay(transform.position, groundNormal);
            }

            // Velocity
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(transform.position + Vector3.up, horizontalVelocity * 0.5f);
        }
#endif
    }
}

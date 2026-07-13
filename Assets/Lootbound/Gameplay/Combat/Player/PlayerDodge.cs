using System;
using UnityEngine;
using Lootbound.Core.Logging;
using Lootbound.Gameplay.Player;

namespace Lootbound.Gameplay.Combat
{
    /// <summary>
    /// Handles player dodge/roll with invulnerability frames.
    /// Uses CharacterController for movement.
    /// </summary>
    public class PlayerDodge : MonoBehaviour
    {
        private const string Category = "PlayerDodge";

        [Header("Dodge Settings")]
        [Tooltip("Distance traveled during dodge.")]
        [SerializeField] private float distance = 2.5f;

        [Tooltip("Total duration of the dodge.")]
        [SerializeField] private float duration = 0.3f;

        [Tooltip("When invulnerability starts (from dodge start).")]
        [SerializeField] private float invulnerabilityStart = 0.05f;

        [Tooltip("When invulnerability ends (from dodge start).")]
        [SerializeField] private float invulnerabilityEnd = 0.25f;

        [Tooltip("Cooldown between dodges.")]
        [SerializeField] private float cooldown = 0.8f;

        [Header("Dependencies")]
        [SerializeField] private CharacterController characterController;
        [SerializeField] private PlayerInputReader inputReader;
        [SerializeField] private Transform cameraTransform;

        private bool isDodging;
        private float dodgeTimer;
        private float cooldownTimer;
        private Vector3 dodgeDirection;
        private float dodgeSpeed;

        /// <summary>
        /// True if currently in a dodge.
        /// </summary>
        public bool IsDodging => isDodging;

        /// <summary>
        /// True if currently in the invulnerability window.
        /// </summary>
        public bool IsInvulnerable
        {
            get
            {
                if (!isDodging) return false;
                return dodgeTimer >= invulnerabilityStart && dodgeTimer <= invulnerabilityEnd;
            }
        }

        /// <summary>
        /// True if dodge is ready to be used.
        /// </summary>
        public bool CanDodge => !isDodging && cooldownTimer <= 0f;

        /// <summary>
        /// Current cooldown remaining (0 if ready).
        /// </summary>
        public float CooldownRemaining => cooldownTimer;

        /// <summary>
        /// Fired when a dodge starts.
        /// </summary>
        public event Action OnDodgeStarted;

        /// <summary>
        /// Fired when a dodge ends.
        /// </summary>
        public event Action OnDodgeEnded;

        private void Awake()
        {
            if (characterController == null)
            {
                characterController = GetComponentInParent<CharacterController>();
            }

            if (inputReader == null)
            {
                inputReader = GetComponentInParent<PlayerInputReader>();
            }

            if (cameraTransform == null)
            {
                var cam = Camera.main;
                if (cam != null)
                {
                    cameraTransform = cam.transform;
                }
            }

            // Pre-calculate dodge speed
            dodgeSpeed = distance / duration;
        }

        private void Update()
        {
            UpdateCooldown();
            UpdateDodge();
        }

        private void UpdateCooldown()
        {
            if (cooldownTimer > 0f)
            {
                cooldownTimer -= Time.deltaTime;
            }
        }

        private void UpdateDodge()
        {
            if (!isDodging)
            {
                return;
            }

            dodgeTimer += Time.deltaTime;

            // Apply movement
            if (characterController != null)
            {
                Vector3 movement = dodgeDirection * dodgeSpeed * Time.deltaTime;
                characterController.Move(movement);
            }

            // Check if dodge is complete
            if (dodgeTimer >= duration)
            {
                EndDodge();
            }
        }

        /// <summary>
        /// Attempt to start a dodge in the given input direction.
        /// </summary>
        /// <param name="inputDirection">Movement input direction (can be zero for backstep).</param>
        /// <returns>True if dodge started successfully.</returns>
        public bool TryDodge(Vector2 inputDirection)
        {
            if (!CanDodge)
            {
                return false;
            }

            // Determine dodge direction
            dodgeDirection = CalculateDodgeDirection(inputDirection);

            StartDodge();
            return true;
        }

        private Vector3 CalculateDodgeDirection(Vector2 inputDirection)
        {
            Vector3 direction;

            if (inputDirection.sqrMagnitude > 0.01f)
            {
                // Convert input to world direction using camera orientation
                Vector3 forward = cameraTransform != null ? cameraTransform.forward : transform.forward;
                Vector3 right = cameraTransform != null ? cameraTransform.right : transform.right;

                // Flatten to horizontal plane
                forward.y = 0f;
                right.y = 0f;
                forward.Normalize();
                right.Normalize();

                direction = (forward * inputDirection.y + right * inputDirection.x).normalized;
            }
            else
            {
                // No input: dodge backward
                Vector3 forward = cameraTransform != null ? cameraTransform.forward : transform.forward;
                forward.y = 0f;
                direction = -forward.normalized;
            }

            return direction;
        }

        private void StartDodge()
        {
            isDodging = true;
            dodgeTimer = 0f;

            LootboundLog.Info(Category, $"Dodge started, direction: {dodgeDirection}");
            OnDodgeStarted?.Invoke();
        }

        private void EndDodge()
        {
            isDodging = false;
            cooldownTimer = cooldown;

            LootboundLog.Info(Category, "Dodge ended");
            OnDodgeEnded?.Invoke();
        }

        /// <summary>
        /// Interrupt the current dodge (e.g., on death).
        /// </summary>
        public void InterruptDodge()
        {
            if (isDodging)
            {
                isDodging = false;
                cooldownTimer = cooldown;
                OnDodgeEnded?.Invoke();
            }
        }

        private void OnValidate()
        {
            distance = Mathf.Max(0.1f, distance);
            duration = Mathf.Max(0.05f, duration);
            invulnerabilityStart = Mathf.Clamp(invulnerabilityStart, 0f, duration);
            invulnerabilityEnd = Mathf.Clamp(invulnerabilityEnd, invulnerabilityStart, duration);
            cooldown = Mathf.Max(0f, cooldown);
        }
    }
}

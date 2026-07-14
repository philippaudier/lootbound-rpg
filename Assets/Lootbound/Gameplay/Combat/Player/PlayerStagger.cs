using System;
using UnityEngine;
using Lootbound.Core.Logging;

namespace Lootbound.Gameplay.Combat
{
    /// <summary>
    /// Applies a brief stagger effect when the player takes damage.
    /// Provides a short knockback push and movement slowdown.
    /// </summary>
    public class PlayerStagger : MonoBehaviour
    {
        private const string Category = "PlayerStagger";

        [Header("Stagger Settings")]
        [Tooltip("Duration of the stagger effect.")]
        [SerializeField] private float staggerDuration = 0.15f;

        [Tooltip("Knockback force applied on hit.")]
        [SerializeField] private float knockbackForce = 2f;

        [Tooltip("Movement speed multiplier during stagger (0-1).")]
        [SerializeField] private float staggerSpeedMultiplier = 0.3f;

        [Header("Dependencies")]
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private CharacterController characterController;
        [SerializeField] private PlayerMeleeWeapon meleeWeapon;

        private bool isStaggered;
        private float staggerTimer;
        private Vector3 knockbackVelocity;

        /// <summary>
        /// True if currently staggered.
        /// </summary>
        public bool IsStaggered => isStaggered;

        /// <summary>
        /// Movement speed multiplier (1.0 when not staggered).
        /// </summary>
        public float SpeedMultiplier => isStaggered ? staggerSpeedMultiplier : 1f;

        /// <summary>
        /// Fired when stagger starts.
        /// </summary>
        public event Action OnStaggerStarted;

        /// <summary>
        /// Fired when stagger ends.
        /// </summary>
        public event Action OnStaggerEnded;

        private void Awake()
        {
            if (playerHealth == null)
            {
                playerHealth = GetComponent<PlayerHealth>();
            }

            if (characterController == null)
            {
                characterController = GetComponent<CharacterController>();
            }

            if (meleeWeapon == null)
            {
                meleeWeapon = GetComponentInChildren<PlayerMeleeWeapon>();
            }
        }

        private void OnEnable()
        {
            if (playerHealth != null)
            {
                playerHealth.OnDamaged += HandleDamaged;
            }
        }

        private void OnDisable()
        {
            if (playerHealth != null)
            {
                playerHealth.OnDamaged -= HandleDamaged;
            }
        }

        private void Update()
        {
            if (!isStaggered)
            {
                return;
            }

            staggerTimer -= Time.deltaTime;

            // Apply knockback movement
            if (characterController != null && knockbackVelocity.sqrMagnitude > 0.01f)
            {
                characterController.Move(knockbackVelocity * Time.deltaTime);
                knockbackVelocity = Vector3.Lerp(knockbackVelocity, Vector3.zero, Time.deltaTime * 10f);
            }

            if (staggerTimer <= 0f)
            {
                EndStagger();
            }
        }

        private void HandleDamaged(DamageRequest request)
        {
            // Apply stagger based on stagger force from the damage
            if (request.StaggerForce > 0f)
            {
                ApplyStagger(request.HitDirection, request.StaggerForce);
            }
        }

        private void ApplyStagger(Vector3 hitDirection, float force)
        {
            isStaggered = true;
            staggerTimer = staggerDuration;

            // Interrupt any ongoing attack
            if (meleeWeapon != null && meleeWeapon.IsAttacking)
            {
                meleeWeapon.InterruptAttack();
                LootboundLog.Info(Category, "Attack interrupted by stagger");
            }

            // Calculate knockback - push away from hit direction
            Vector3 knockbackDir = hitDirection;
            knockbackDir.y = 0f;
            if (knockbackDir.sqrMagnitude > 0.01f)
            {
                knockbackDir.Normalize();
            }
            else
            {
                knockbackDir = -transform.forward;
            }

            knockbackVelocity = knockbackDir * knockbackForce * force;

            LootboundLog.Info(Category, $"Stagger applied, force: {force:F2}");
            OnStaggerStarted?.Invoke();
        }

        private void EndStagger()
        {
            isStaggered = false;
            knockbackVelocity = Vector3.zero;

            LootboundLog.Info(Category, "Stagger ended");
            OnStaggerEnded?.Invoke();
        }

        /// <summary>
        /// Interrupt stagger (e.g., on death or dodge).
        /// </summary>
        public void InterruptStagger()
        {
            if (isStaggered)
            {
                isStaggered = false;
                knockbackVelocity = Vector3.zero;
                OnStaggerEnded?.Invoke();
            }
        }

        private void OnValidate()
        {
            staggerDuration = Mathf.Max(0.01f, staggerDuration);
            knockbackForce = Mathf.Max(0f, knockbackForce);
            staggerSpeedMultiplier = Mathf.Clamp01(staggerSpeedMultiplier);
        }
    }
}

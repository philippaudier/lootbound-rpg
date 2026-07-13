using System;
using UnityEngine;
using Lootbound.Core.Logging;
using Lootbound.Gameplay.Player;

namespace Lootbound.Gameplay.Combat
{
    /// <summary>
    /// Coordinates player combat systems: weapon, dodge, and input.
    /// </summary>
    public class PlayerCombatController : MonoBehaviour
    {
        private const string Category = "PlayerCombat";

        [Header("Dependencies")]
        [SerializeField] private PlayerInputReader inputReader;
        [SerializeField] private PlayerMeleeWeapon meleeWeapon;
        [SerializeField] private PlayerDodge dodge;
        [SerializeField] private PlayerHealth playerHealth;

        [Header("State")]
        [Tooltip("If true, combat actions are blocked (e.g., inventory open).")]
        [SerializeField] private bool combatBlocked;

        private bool isDead;

        /// <summary>
        /// True if combat actions are currently blocked.
        /// </summary>
        public bool CombatBlocked
        {
            get => combatBlocked;
            set
            {
                combatBlocked = value;
                if (combatBlocked)
                {
                    LootboundLog.Info(Category, "Combat blocked");
                }
            }
        }

        /// <summary>
        /// True if able to perform an attack.
        /// </summary>
        public bool CanAttack => !combatBlocked && !isDead && (meleeWeapon?.CanAttack ?? false) && !(dodge?.IsDodging ?? false);

        /// <summary>
        /// True if able to dodge.
        /// </summary>
        public bool CanDodge => !combatBlocked && !isDead && (dodge?.CanDodge ?? false) && !(meleeWeapon?.IsAttacking ?? false);

        /// <summary>
        /// True if currently attacking.
        /// </summary>
        public bool IsAttacking => meleeWeapon?.IsAttacking ?? false;

        /// <summary>
        /// True if currently dodging.
        /// </summary>
        public bool IsDodging => dodge?.IsDodging ?? false;

        /// <summary>
        /// Current attack phase.
        /// </summary>
        public AttackPhase CurrentAttackPhase => meleeWeapon?.CurrentPhase ?? AttackPhase.Ready;

        /// <summary>
        /// Fired when player successfully attacks.
        /// </summary>
        public event Action OnAttack;

        /// <summary>
        /// Fired when player hits a target.
        /// </summary>
        public event Action<DamageResult> OnHitTarget;

        /// <summary>
        /// Fired when player dodges.
        /// </summary>
        public event Action OnDodge;

        private void Awake()
        {
            // Auto-find dependencies if not assigned
            if (inputReader == null)
            {
                inputReader = GetComponentInParent<PlayerInputReader>();
            }

            if (meleeWeapon == null)
            {
                meleeWeapon = GetComponentInChildren<PlayerMeleeWeapon>();
            }

            if (dodge == null)
            {
                dodge = GetComponentInParent<PlayerDodge>();
            }

            if (playerHealth == null)
            {
                playerHealth = GetComponentInParent<PlayerHealth>();
            }
        }

        private void OnEnable()
        {
            if (inputReader != null)
            {
                inputReader.OnAttackPressed += HandleAttackInput;
                inputReader.OnDodgePressed += HandleDodgeInput;
            }

            if (meleeWeapon != null)
            {
                meleeWeapon.OnHit += HandleHit;
            }

            if (playerHealth != null)
            {
                playerHealth.OnDied += HandleDeath;
            }
        }

        private void OnDisable()
        {
            if (inputReader != null)
            {
                inputReader.OnAttackPressed -= HandleAttackInput;
                inputReader.OnDodgePressed -= HandleDodgeInput;
            }

            if (meleeWeapon != null)
            {
                meleeWeapon.OnHit -= HandleHit;
            }

            if (playerHealth != null)
            {
                playerHealth.OnDied -= HandleDeath;
            }
        }

        private void HandleAttackInput()
        {
            if (!CanAttack)
            {
                return;
            }

            if (meleeWeapon != null && meleeWeapon.TryAttack())
            {
                LootboundLog.Info(Category, "Attack initiated");
                OnAttack?.Invoke();
            }
        }

        private void HandleDodgeInput()
        {
            if (!CanDodge)
            {
                return;
            }

            Vector2 moveInput = inputReader?.MoveInput ?? Vector2.zero;

            if (dodge != null && dodge.TryDodge(moveInput))
            {
                LootboundLog.Info(Category, "Dodge initiated");
                OnDodge?.Invoke();
            }
        }

        private void HandleHit(DamageResult result)
        {
            OnHitTarget?.Invoke(result);
        }

        private void HandleDeath()
        {
            isDead = true;

            // Interrupt any ongoing actions
            if (meleeWeapon != null && meleeWeapon.IsAttacking)
            {
                meleeWeapon.InterruptAttack();
            }

            if (dodge != null && dodge.IsDodging)
            {
                dodge.InterruptDodge();
            }

            LootboundLog.Info(Category, "Player died - combat disabled");
        }

        /// <summary>
        /// Reset combat state (e.g., after respawn).
        /// </summary>
        public void ResetCombat()
        {
            isDead = false;
            combatBlocked = false;
            LootboundLog.Info(Category, "Combat state reset");
        }
    }
}

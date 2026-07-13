using System;
using UnityEngine;
using Lootbound.Core.Logging;

namespace Lootbound.Gameplay.Combat
{
    /// <summary>
    /// Player health component implementing IDamageable.
    /// Handles damage application with invulnerability checking via PlayerDodge.
    /// </summary>
    public class PlayerHealth : MonoBehaviour, IDamageable
    {
        private const string Category = "PlayerHealth";

        [Header("Health Settings")]
        [SerializeField] private float maxHealth = 100f;

        private Health health;
        private PlayerDodge dodge;

        /// <summary>
        /// Current health value.
        /// </summary>
        public float CurrentHealth => health?.Current ?? 0f;

        /// <summary>
        /// Maximum health value.
        /// </summary>
        public float MaxHealth => health?.Max ?? maxHealth;

        /// <summary>
        /// Health as a 0-1 normalized value.
        /// </summary>
        public float NormalizedHealth => health?.Normalized ?? 0f;

        /// <summary>
        /// True if the player is dead.
        /// </summary>
        public bool IsDead => health?.IsDead ?? false;

        /// <summary>
        /// Fired when health changes. Parameters: (current, max).
        /// </summary>
        public event Action<float, float> OnHealthChanged;

        /// <summary>
        /// Fired when damage is received.
        /// </summary>
        public event Action<DamageRequest> OnDamaged;

        /// <summary>
        /// Fired when the player dies.
        /// </summary>
        public event Action OnDied;

        private void Awake()
        {
            health = new Health(maxHealth);
            health.OnHealthChanged += HandleHealthChanged;
            health.OnDamaged += HandleDamaged;
            health.OnDied += HandleDied;

            dodge = GetComponent<PlayerDodge>();
        }

        private void OnDestroy()
        {
            if (health != null)
            {
                health.OnHealthChanged -= HandleHealthChanged;
                health.OnDamaged -= HandleDamaged;
                health.OnDied -= HandleDied;
            }
        }

        /// <summary>
        /// Apply damage to the player.
        /// </summary>
        public DamageResult TakeDamage(DamageRequest request)
        {
            if (IsDead)
            {
                return DamageResult.NotApplied();
            }

            // Check invulnerability from dodge
            if (dodge != null && dodge.IsInvulnerable)
            {
                LootboundLog.Info(Category, "Damage blocked by i-frames");
                return DamageResult.Blocked();
            }

            var result = health.ApplyDamage(request);

            if (result.Applied)
            {
                string sourceInfo = request.Source != null ? request.Source.name : "Unknown";
                LootboundLog.Info(Category, $"Took {result.DamageDealt} damage from {sourceInfo}. Health: {health.Current}/{health.Max}");
            }

            return result;
        }

        /// <summary>
        /// Heal the player by the specified amount.
        /// </summary>
        public void Heal(float amount)
        {
            if (IsDead || amount <= 0f)
            {
                return;
            }

            float previousHealth = health.Current;
            health.Heal(amount);
            LootboundLog.Info(Category, $"Healed {health.Current - previousHealth}. Health: {health.Current}/{health.Max}");
        }

        /// <summary>
        /// Reset health to full and clear death state.
        /// </summary>
        public void Reset()
        {
            health.Reset();
            LootboundLog.Info(Category, "Health reset to full");
        }

        private void HandleHealthChanged(float current, float max)
        {
            OnHealthChanged?.Invoke(current, max);
        }

        private void HandleDamaged(DamageRequest request)
        {
            OnDamaged?.Invoke(request);
        }

        private void HandleDied()
        {
            LootboundLog.Info(Category, "Player died!");
            OnDied?.Invoke();
        }
    }
}

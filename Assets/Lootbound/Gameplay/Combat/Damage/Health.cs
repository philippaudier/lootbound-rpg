using System;

namespace Lootbound.Gameplay.Combat
{
    /// <summary>
    /// Pure C# health logic that can be composed into any MonoBehaviour.
    /// Handles damage, healing, and death state.
    /// </summary>
    public sealed class Health
    {
        private float current;
        private bool isDead;

        /// <summary>
        /// Current health value.
        /// </summary>
        public float Current => current;

        /// <summary>
        /// Maximum health value.
        /// </summary>
        public float Max { get; }

        /// <summary>
        /// Health as a 0-1 normalized value.
        /// </summary>
        public float Normalized => Max > 0f ? current / Max : 0f;

        /// <summary>
        /// True if health reached zero.
        /// </summary>
        public bool IsDead => isDead;

        /// <summary>
        /// Fired when health changes. Parameters: (current, max).
        /// </summary>
        public event Action<float, float> OnHealthChanged;

        /// <summary>
        /// Fired when damage is received.
        /// </summary>
        public event Action<DamageRequest> OnDamaged;

        /// <summary>
        /// Fired once when health reaches zero.
        /// </summary>
        public event Action OnDied;

        public Health(float maxHealth)
        {
            Max = maxHealth > 0f ? maxHealth : 1f;
            current = Max;
            isDead = false;
        }

        /// <summary>
        /// Apply damage and return the result.
        /// </summary>
        public DamageResult ApplyDamage(DamageRequest request)
        {
            if (isDead)
            {
                return DamageResult.NotApplied();
            }

            if (!request.IsValid)
            {
                return DamageResult.NotApplied();
            }

            float previousHealth = current;
            current = Math.Max(0f, current - request.Amount);
            float actualDamage = previousHealth - current;

            OnDamaged?.Invoke(request);
            OnHealthChanged?.Invoke(current, Max);

            bool wasFatal = current <= 0f && !isDead;
            if (wasFatal)
            {
                isDead = true;
                OnDied?.Invoke();
            }

            return DamageResult.Success(actualDamage, wasFatal);
        }

        /// <summary>
        /// Heal by the specified amount.
        /// </summary>
        public void Heal(float amount)
        {
            if (isDead || amount <= 0f)
            {
                return;
            }

            current = Math.Min(Max, current + amount);
            OnHealthChanged?.Invoke(current, Max);
        }

        /// <summary>
        /// Reset health to max and clear death state.
        /// </summary>
        public void Reset()
        {
            current = Max;
            isDead = false;
            OnHealthChanged?.Invoke(current, Max);
        }

        /// <summary>
        /// Set health to a specific value. Used for loading saves.
        /// </summary>
        public void SetHealth(float value)
        {
            current = Math.Clamp(value, 0f, Max);
            isDead = current <= 0f;
            OnHealthChanged?.Invoke(current, Max);
        }
    }
}

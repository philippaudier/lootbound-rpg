using System;
using UnityEngine;
using Lootbound.Core.Logging;
using Lootbound.Gameplay.Inventory;
using Lootbound.Gameplay.World;
using Lootbound.Gameplay.Equipment;

namespace Lootbound.Gameplay.Combat
{
    /// <summary>
    /// Enemy health component implementing IDamageable.
    /// Handles damage application, stagger triggering, and loot spawning on death.
    /// </summary>
    public class EnemyHealth : MonoBehaviour, IDamageable
    {
        private const string Category = "EnemyHealth";

        [Header("Configuration")]
        [SerializeField] private EnemyConfig config;

        [Header("Equipment")]
        [Tooltip("Equipment registry for weapon generation.")]
        [SerializeField] private EquipmentRegistry equipmentRegistry;

        [Header("Loot")]
        [Tooltip("Spawn position offset for loot drops.")]
        [SerializeField] private Vector3 lootSpawnOffset = new(0f, 0.5f, 0f);

        private Health health;

        // Poise system
        private float currentPoise;
        private float poiseRegenTimer;
        private float staggerImmunityTimer;

        // Knockback system
        private Vector3 lastHitDirection;
        private Vector3 knockbackVelocity;
        private float knockbackTimer;

        /// <summary>
        /// Current health value.
        /// </summary>
        public float CurrentHealth => health?.Current ?? 0f;

        /// <summary>
        /// Maximum health value.
        /// </summary>
        public float MaxHealth => health?.Max ?? (config != null ? config.MaxHealth : 0f);

        /// <summary>
        /// Health as a 0-1 normalized value.
        /// </summary>
        public float NormalizedHealth => health?.Normalized ?? 0f;

        /// <summary>
        /// True if the enemy is dead.
        /// </summary>
        public bool IsDead => health?.IsDead ?? false;

        /// <summary>
        /// Current poise value (0 = will stagger on next hit).
        /// </summary>
        public float CurrentPoise => currentPoise;

        /// <summary>
        /// True if currently immune to stagger.
        /// </summary>
        public bool IsStaggerImmune => staggerImmunityTimer > 0f;

        /// <summary>
        /// Fired when health changes. Parameters: (current, max).
        /// </summary>
        public event Action<float, float> OnHealthChanged;

        /// <summary>
        /// Fired when damage is received.
        /// </summary>
        public event Action<DamageRequest> OnDamaged;

        /// <summary>
        /// Fired when a stagger should occur.
        /// </summary>
        public event Action<float> OnStagger;

        /// <summary>
        /// Fired when the enemy dies.
        /// </summary>
        public event Action OnDied;

        /// <summary>
        /// Static event fired when any enemy dies.
        /// Used by expedition tracking and other global systems.
        /// </summary>
        public static event Action<EnemyHealth> OnAnyEnemyDied;

        private void Awake()
        {
            float maxHealth = config != null ? config.MaxHealth : 100f;
            health = new Health(maxHealth);

            health.OnHealthChanged += HandleHealthChanged;
            health.OnDamaged += HandleDamaged;
            health.OnDied += HandleDied;

            // Initialize poise
            currentPoise = config != null ? config.MaxPoise : 30f;
        }

        private void Update()
        {
            if (IsDead || config == null) return;

            // Update stagger immunity timer
            if (staggerImmunityTimer > 0f)
            {
                staggerImmunityTimer -= Time.deltaTime;
            }

            // Regenerate poise after delay
            if (poiseRegenTimer > 0f)
            {
                poiseRegenTimer -= Time.deltaTime;
            }
            else if (currentPoise < config.MaxPoise)
            {
                currentPoise = Mathf.Min(currentPoise + config.PoiseRegenRate * Time.deltaTime, config.MaxPoise);
            }

            // Apply knockback
            if (knockbackTimer > 0f)
            {
                knockbackTimer -= Time.deltaTime;
                float t = knockbackTimer / config.StaggerDuration;
                Vector3 frameKnockback = knockbackVelocity * t * Time.deltaTime;
                transform.position += frameKnockback;
            }
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
        /// Apply damage to the enemy.
        /// </summary>
        public DamageResult TakeDamage(DamageRequest request)
        {
            if (IsDead)
            {
                return DamageResult.NotApplied();
            }

            var result = health.ApplyDamage(request);

            if (result.Applied)
            {
                string sourceInfo = request.Source != null ? request.Source.name : "Unknown";
                LootboundLog.Info(Category, $"{gameObject.name} took {result.DamageDealt} damage from {sourceInfo}. Health: {health.Current}/{health.Max}");

                // Store hit direction for knockback
                lastHitDirection = request.HitDirection;

                // Process poise damage and potential stagger
                if (request.StaggerForce > 0f && config != null)
                {
                    ProcessPoiseDamage(request.StaggerForce);
                }
            }

            return result;
        }

        private void ProcessPoiseDamage(float staggerForce)
        {
            // Reset poise regen timer on any hit
            poiseRegenTimer = config.PoiseRegenDelay;

            // If immune to stagger, skip poise damage
            if (IsStaggerImmune)
            {
                LootboundLog.Info(Category, $"{gameObject.name} is stagger immune, hit ignored for poise");
                return;
            }

            // Apply poise damage based on stagger force
            // Stagger force of 1.0 = full poise damage, 0.5 = half, etc.
            float poiseDamage = config.MaxPoise * staggerForce;
            currentPoise -= poiseDamage;

            LootboundLog.Info(Category, $"{gameObject.name} poise: {currentPoise:F1}/{config.MaxPoise}");

            // Check if poise broken
            if (currentPoise <= 0f)
            {
                // Trigger stagger
                OnStagger?.Invoke(staggerForce);

                // Apply knockback
                ApplyKnockback(lastHitDirection);

                // Reset poise and apply immunity
                currentPoise = config.MaxPoise;
                staggerImmunityTimer = config.StaggerImmunityDuration;

                LootboundLog.Info(Category, $"{gameObject.name} staggered! Immunity for {config.StaggerImmunityDuration}s");
            }
        }

        private void ApplyKnockback(Vector3 direction)
        {
            if (config.KnockbackDistance <= 0f || config.StaggerDuration <= 0f)
            {
                return;
            }

            // Calculate knockback velocity to cover the distance over stagger duration
            // Direction is where the attack came FROM, so we move in that direction (away from attacker)
            Vector3 knockbackDir = new Vector3(direction.x, 0f, direction.z).normalized;
            if (knockbackDir.sqrMagnitude < 0.01f)
            {
                knockbackDir = -transform.forward; // Fallback: push backwards
            }

            // Velocity needed to travel knockbackDistance over staggerDuration with linear falloff
            // With linear falloff (t going from 1 to 0), total distance = velocity * duration / 2
            // So velocity = 2 * distance / duration
            knockbackVelocity = knockbackDir * (2f * config.KnockbackDistance / config.StaggerDuration);
            knockbackTimer = config.StaggerDuration;

            LootboundLog.Info(Category, $"{gameObject.name} knockback applied: {config.KnockbackDistance}m");
        }

        /// <summary>
        /// Set configuration at runtime.
        /// </summary>
        public void SetConfig(EnemyConfig newConfig)
        {
            config = newConfig;
            if (config != null)
            {
                health = new Health(config.MaxHealth);
                health.OnHealthChanged += HandleHealthChanged;
                health.OnDamaged += HandleDamaged;
                health.OnDied += HandleDied;

                // Reset poise
                currentPoise = config.MaxPoise;
                staggerImmunityTimer = 0f;
                poiseRegenTimer = 0f;
            }
        }

        /// <summary>
        /// Reset health and poise to full.
        /// </summary>
        public void Reset()
        {
            health?.Reset();

            if (config != null)
            {
                currentPoise = config.MaxPoise;
                staggerImmunityTimer = 0f;
                poiseRegenTimer = 0f;
                knockbackVelocity = Vector3.zero;
                knockbackTimer = 0f;
            }
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
            LootboundLog.Info(Category, $"{gameObject.name} died!");
            SpawnLoot();
            OnDied?.Invoke();
            OnAnyEnemyDied?.Invoke(this);
        }

        private void SpawnLoot()
        {
            if (config == null) return;

            Vector3 spawnPosition = transform.position + lootSpawnOffset;

            // Spawn regular loot items
            SpawnRegularLoot(spawnPosition);

            // Try to spawn equipment
            SpawnEquipmentLoot(spawnPosition);
        }

        private void SpawnRegularLoot(Vector3 spawnPosition)
        {
            if (config.LootItems == null || config.LootItems.Length == 0) return;

            int quantity = UnityEngine.Random.Range(config.MinLootQuantity, config.MaxLootQuantity + 1);

            for (int i = 0; i < quantity; i++)
            {
                // Pick a random item from the loot table
                var item = config.LootItems[UnityEngine.Random.Range(0, config.LootItems.Length)];
                if (item != null)
                {
                    // Offset each item slightly to prevent overlap
                    Vector3 offset = new Vector3(
                        UnityEngine.Random.Range(-0.3f, 0.3f),
                        0f,
                        UnityEngine.Random.Range(-0.3f, 0.3f)
                    );

                    ItemWorldPickup.SpawnPickup(item, spawnPosition + offset, 1);
                    LootboundLog.Info(Category, $"Spawned loot: {item.DisplayName}");
                }
            }
        }

        private void SpawnEquipmentLoot(Vector3 spawnPosition)
        {
            if (config.WeaponLoot == null || config.WeaponLoot.Length == 0) return;

            // Roll for weapon drop
            float roll = UnityEngine.Random.value;
            if (roll > config.WeaponDropChance) return;

            // Pick a random weapon
            var weaponDef = config.WeaponLoot[UnityEngine.Random.Range(0, config.WeaponLoot.Length)];
            if (weaponDef == null) return;

            // Generate equipment instance
            var generator = new EquipmentGenerator(equipmentRegistry);
            string foundLocation = gameObject.name;
            var equipmentItem = generator.GenerateWeapon(weaponDef, foundLocation);

            if (equipmentItem != null)
            {
                // Offset from regular loot
                Vector3 offset = new Vector3(
                    UnityEngine.Random.Range(-0.4f, 0.4f),
                    0.2f,
                    UnityEngine.Random.Range(-0.4f, 0.4f)
                );

                ItemWorldPickup.SpawnPickup(equipmentItem, spawnPosition + offset);

                var data = equipmentItem.EquipmentData;
                LootboundLog.Info(Category, $"Spawned equipment: {data.CustomName} ({data.Rarity})");
            }
        }
    }
}

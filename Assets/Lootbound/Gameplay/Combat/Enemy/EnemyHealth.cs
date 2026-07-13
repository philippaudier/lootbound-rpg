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

        private void Awake()
        {
            float maxHealth = config != null ? config.MaxHealth : 100f;
            health = new Health(maxHealth);

            health.OnHealthChanged += HandleHealthChanged;
            health.OnDamaged += HandleDamaged;
            health.OnDied += HandleDied;
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

                // Trigger stagger if damage has stagger force
                if (request.StaggerForce > 0f)
                {
                    OnStagger?.Invoke(request.StaggerForce);
                }
            }

            return result;
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
            }
        }

        /// <summary>
        /// Reset health to full.
        /// </summary>
        public void Reset()
        {
            health?.Reset();
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

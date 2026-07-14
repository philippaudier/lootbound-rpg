using UnityEngine;
using Lootbound.Gameplay.Inventory;
using Lootbound.Gameplay.Equipment;

namespace Lootbound.Gameplay.Combat
{
    /// <summary>
    /// Configuration for an enemy type.
    /// Defines health, perception, movement, attack, and loot parameters.
    /// </summary>
    [CreateAssetMenu(fileName = "EnemyConfig", menuName = "Lootbound/Combat/Enemy Config")]
    public class EnemyConfig : ScriptableObject
    {
        [Header("Health")]
        [Tooltip("Maximum health points.")]
        [SerializeField] private float maxHealth = 80f;

        [Header("Perception")]
        [Tooltip("Distance at which the enemy can detect the player.")]
        [SerializeField] private float detectionRange = 12f;

        [Tooltip("Distance at which the enemy will attempt to attack.")]
        [SerializeField] private float attackRange = 2f;

        [Tooltip("Field of view angle for detection (degrees).")]
        [SerializeField] private float fieldOfView = 120f;

        [Header("Movement")]
        [Tooltip("Movement speed when chasing.")]
        [SerializeField] private float moveSpeed = 3.5f;

        [Tooltip("Turning speed in degrees per second.")]
        [SerializeField] private float turnSpeed = 180f;

        [Header("Attack")]
        [Tooltip("Damage dealt by attacks.")]
        [SerializeField] private float attackDamage = 20f;

        [Tooltip("Duration of the attack windup (telegraph) phase.")]
        [SerializeField] private float attackWindup = 0.8f;

        [Tooltip("Duration of the active attack phase.")]
        [SerializeField] private float attackActive = 0.3f;

        [Tooltip("Duration of the recovery phase after attacking.")]
        [SerializeField] private float attackRecovery = 0.5f;

        [Tooltip("Cooldown between attacks.")]
        [SerializeField] private float attackCooldown = 1.5f;

        [Tooltip("Stagger force applied to targets.")]
        [SerializeField, Range(0f, 1f)] private float attackStaggerForce = 0.2f;

        [Header("Stagger & Poise")]
        [Tooltip("Duration the enemy is staggered when hit.")]
        [SerializeField] private float staggerDuration = 0.4f;

        [Tooltip("Maximum poise. Enemy staggers when poise reaches 0.")]
        [SerializeField] private float maxPoise = 30f;

        [Tooltip("Poise regeneration per second (only when not recently hit).")]
        [SerializeField] private float poiseRegenRate = 20f;

        [Tooltip("Delay before poise starts regenerating after being hit.")]
        [SerializeField] private float poiseRegenDelay = 1f;

        [Tooltip("Immunity duration after being staggered (prevents stagger-lock).")]
        [SerializeField] private float staggerImmunityDuration = 1.2f;

        [Tooltip("Knockback distance when staggered.")]
        [SerializeField] private float knockbackDistance = 1f;

        [Header("Loot")]
        [Tooltip("Items that can drop when this enemy dies.")]
        [SerializeField] private ItemDefinition[] lootItems;

        [Tooltip("Minimum number of items to drop.")]
        [SerializeField] private int minLootQuantity = 1;

        [Tooltip("Maximum number of items to drop.")]
        [SerializeField] private int maxLootQuantity = 3;

        [Header("Equipment Loot")]
        [Tooltip("Weapons that can drop when this enemy dies.")]
        [SerializeField] private WeaponDefinition[] weaponLoot;

        [Tooltip("Chance (0-1) to drop a weapon.")]
        [SerializeField, Range(0f, 1f)] private float weaponDropChance = 0.15f;

        // Health
        public float MaxHealth => maxHealth;

        // Perception
        public float DetectionRange => detectionRange;
        public float AttackRange => attackRange;
        public float FieldOfView => fieldOfView;

        // Movement
        public float MoveSpeed => moveSpeed;
        public float TurnSpeed => turnSpeed;

        // Attack
        public float AttackDamage => attackDamage;
        public float AttackWindup => attackWindup;
        public float AttackActive => attackActive;
        public float AttackRecovery => attackRecovery;
        public float AttackCooldown => attackCooldown;
        public float AttackStaggerForce => attackStaggerForce;

        // Stagger & Poise
        public float StaggerDuration => staggerDuration;
        public float MaxPoise => maxPoise;
        public float PoiseRegenRate => poiseRegenRate;
        public float PoiseRegenDelay => poiseRegenDelay;
        public float StaggerImmunityDuration => staggerImmunityDuration;
        public float KnockbackDistance => knockbackDistance;

        // Loot
        public ItemDefinition[] LootItems => lootItems;
        public int MinLootQuantity => minLootQuantity;
        public int MaxLootQuantity => maxLootQuantity;

        // Equipment Loot
        public WeaponDefinition[] WeaponLoot => weaponLoot;
        public float WeaponDropChance => weaponDropChance;

        /// <summary>
        /// Total duration of a single attack cycle.
        /// </summary>
        public float TotalAttackDuration => attackWindup + attackActive + attackRecovery;

        private void OnValidate()
        {
            maxHealth = Mathf.Max(1f, maxHealth);
            detectionRange = Mathf.Max(0f, detectionRange);
            attackRange = Mathf.Max(0f, attackRange);
            fieldOfView = Mathf.Clamp(fieldOfView, 0f, 360f);
            moveSpeed = Mathf.Max(0f, moveSpeed);
            turnSpeed = Mathf.Max(0f, turnSpeed);
            attackDamage = Mathf.Max(0f, attackDamage);
            attackWindup = Mathf.Max(0f, attackWindup);
            attackActive = Mathf.Max(0f, attackActive);
            attackRecovery = Mathf.Max(0f, attackRecovery);
            attackCooldown = Mathf.Max(0f, attackCooldown);
            staggerDuration = Mathf.Max(0f, staggerDuration);
            maxPoise = Mathf.Max(1f, maxPoise);
            poiseRegenRate = Mathf.Max(0f, poiseRegenRate);
            poiseRegenDelay = Mathf.Max(0f, poiseRegenDelay);
            staggerImmunityDuration = Mathf.Max(0f, staggerImmunityDuration);
            knockbackDistance = Mathf.Max(0f, knockbackDistance);
            minLootQuantity = Mathf.Max(0, minLootQuantity);
            maxLootQuantity = Mathf.Max(minLootQuantity, maxLootQuantity);
        }
    }
}

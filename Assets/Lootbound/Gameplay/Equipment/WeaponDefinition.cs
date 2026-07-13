using UnityEngine;
using Lootbound.Gameplay.Combat;
using Lootbound.Gameplay.Inventory;

namespace Lootbound.Gameplay.Equipment
{
    /// <summary>
    /// Definition of a weapon type.
    /// Extends ItemDefinition with weapon-specific stats.
    /// </summary>
    [CreateAssetMenu(fileName = "Weapon_", menuName = "Lootbound/Equipment/Weapon Definition")]
    public class WeaponDefinition : ItemDefinition
    {
        [Header("Weapon Stats")]
        [Tooltip("Base damage dealt by this weapon.")]
        [SerializeField] private float baseDamage = 25f;

        [Tooltip("Base attack speed (attacks per second).")]
        [SerializeField] private float baseAttackSpeed = 1.0f;

        [Tooltip("Base attack range in meters.")]
        [SerializeField] private float baseRange = 2.0f;

        [Tooltip("Base stagger force (0-1).")]
        [SerializeField, Range(0f, 1f)] private float baseStagger = 0.3f;

        [Header("Attack Configuration")]
        [Tooltip("Attack timing configuration for this weapon.")]
        [SerializeField] private MeleeAttackConfig attackConfig;

        [Header("Visuals")]
        [Tooltip("Prefab for first-person weapon view.")]
        [SerializeField] private GameObject firstPersonPrefab;

        // Public accessors
        public float BaseDamage => baseDamage;
        public float BaseAttackSpeed => baseAttackSpeed;
        public float BaseRange => baseRange;
        public float BaseStagger => baseStagger;
        public MeleeAttackConfig AttackConfig => attackConfig;
        public GameObject FirstPersonPrefab => firstPersonPrefab;

        /// <summary>
        /// Get the equipment type for this weapon.
        /// </summary>
        public EquipmentType EquipmentType => EquipmentType.Weapon;

        /// <summary>
        /// Get the equipment slot this weapon occupies.
        /// </summary>
        public EquipmentSlot DefaultSlot => EquipmentSlot.MainHand;

        /// <summary>
        /// Validate that this weapon has required references.
        /// </summary>
        public bool Validate(out string error)
        {
            if (string.IsNullOrEmpty(ItemId))
            {
                error = "Weapon has no item ID";
                return false;
            }

            if (baseDamage <= 0)
            {
                error = "Weapon has no base damage";
                return false;
            }

            if (baseAttackSpeed <= 0)
            {
                error = "Weapon has invalid attack speed";
                return false;
            }

            if (baseRange <= 0)
            {
                error = "Weapon has invalid range";
                return false;
            }

            error = null;
            return true;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            baseDamage = Mathf.Max(1f, baseDamage);
            baseAttackSpeed = Mathf.Max(0.1f, baseAttackSpeed);
            baseRange = Mathf.Max(0.5f, baseRange);

            // Weapons should not be stackable
            SetNonStackable();
        }

        private void SetNonStackable()
        {
            var type = typeof(ItemDefinition);
            var stackField = type.GetField("isStackable", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var maxField = type.GetField("maxStackSize", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (stackField != null) stackField.SetValue(this, false);
            if (maxField != null) maxField.SetValue(this, 1);
        }
#endif
    }
}

using System.Collections.Generic;
using UnityEngine;
using Lootbound.Gameplay.Inventory;

namespace Lootbound.Gameplay.Equipment
{
    /// <summary>
    /// Registry of equipment and affix definitions.
    /// Holds references to all definitions for runtime lookup.
    /// </summary>
    [CreateAssetMenu(fileName = "EquipmentRegistry", menuName = "Lootbound/Equipment/Equipment Registry")]
    public class EquipmentRegistry : ScriptableObject, IEquipmentRegistry
    {
        [Header("Weapon Definitions")]
        [SerializeField] private List<WeaponDefinition> weaponDefinitions = new();

        [Header("Affix Definitions")]
        [SerializeField] private List<AffixDefinition> affixDefinitions = new();

        [Header("Item Definitions")]
        [SerializeField] private List<ItemDefinition> itemDefinitions = new();

        // Runtime lookup dictionaries
        private Dictionary<string, WeaponDefinition> weaponLookup;
        private Dictionary<string, AffixDefinition> affixLookup;
        private Dictionary<string, ItemDefinition> itemLookup;
        private bool isInitialized;

        /// <summary>
        /// All registered weapon definitions.
        /// </summary>
        public IReadOnlyList<WeaponDefinition> WeaponDefinitions => weaponDefinitions;

        /// <summary>
        /// All registered affix definitions.
        /// </summary>
        public IReadOnlyList<AffixDefinition> AffixDefinitions => affixDefinitions;

        private void OnEnable()
        {
            Initialize();
        }

        /// <summary>
        /// Initialize lookup tables.
        /// </summary>
        public void Initialize()
        {
            if (isInitialized) return;

            weaponLookup = new Dictionary<string, WeaponDefinition>();
            affixLookup = new Dictionary<string, AffixDefinition>();
            itemLookup = new Dictionary<string, ItemDefinition>();

            foreach (var weapon in weaponDefinitions)
            {
                if (weapon != null && !string.IsNullOrEmpty(weapon.ItemId))
                {
                    weaponLookup[weapon.ItemId] = weapon;
                    // Also add to item lookup since weapons are items
                    itemLookup[weapon.ItemId] = weapon;
                }
            }

            foreach (var affix in affixDefinitions)
            {
                if (affix != null && !string.IsNullOrEmpty(affix.AffixId))
                {
                    affixLookup[affix.AffixId] = affix;
                }
            }

            foreach (var item in itemDefinitions)
            {
                if (item != null && !string.IsNullOrEmpty(item.ItemId))
                {
                    // Don't override weapons
                    if (!itemLookup.ContainsKey(item.ItemId))
                    {
                        itemLookup[item.ItemId] = item;
                    }
                }
            }

            isInitialized = true;
        }

        /// <summary>
        /// Force reinitialize lookup tables.
        /// </summary>
        public void Reinitialize()
        {
            isInitialized = false;
            Initialize();
        }

        /// <summary>
        /// Get a weapon definition by ID.
        /// </summary>
        public WeaponDefinition GetWeaponDefinition(string definitionId)
        {
            if (string.IsNullOrEmpty(definitionId)) return null;
            Initialize();
            return weaponLookup.TryGetValue(definitionId, out var def) ? def : null;
        }

        /// <summary>
        /// Get an affix definition by ID.
        /// </summary>
        public AffixDefinition GetAffixDefinition(string affixId)
        {
            if (string.IsNullOrEmpty(affixId)) return null;
            Initialize();
            return affixLookup.TryGetValue(affixId, out var def) ? def : null;
        }

        /// <summary>
        /// Get an item definition by ID.
        /// </summary>
        public ItemDefinition GetItemDefinition(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;
            Initialize();
            return itemLookup.TryGetValue(itemId, out var def) ? def : null;
        }

        /// <summary>
        /// Get all affixes of a specific tier.
        /// </summary>
        public List<AffixDefinition> GetAffixesByTier(AffixTier tier)
        {
            Initialize();
            var result = new List<AffixDefinition>();
            foreach (var affix in affixDefinitions)
            {
                if (affix != null && affix.Tier == tier)
                {
                    result.Add(affix);
                }
            }
            return result;
        }

        /// <summary>
        /// Get all affixes that modify a specific stat.
        /// </summary>
        public List<AffixDefinition> GetAffixesByModifier(AffixModifierType modifierType)
        {
            Initialize();
            var result = new List<AffixDefinition>();
            foreach (var affix in affixDefinitions)
            {
                if (affix != null && affix.ModifierType == modifierType)
                {
                    result.Add(affix);
                }
            }
            return result;
        }

        /// <summary>
        /// Validate all definitions have required data.
        /// </summary>
        public bool ValidateAll(out List<string> errors)
        {
            errors = new List<string>();
            Initialize();

            foreach (var weapon in weaponDefinitions)
            {
                if (weapon == null)
                {
                    errors.Add("Null weapon definition in registry");
                    continue;
                }

                if (!weapon.Validate(out string error))
                {
                    errors.Add($"Weapon '{weapon.name}': {error}");
                }
            }

            foreach (var affix in affixDefinitions)
            {
                if (affix == null)
                {
                    errors.Add("Null affix definition in registry");
                    continue;
                }

                if (string.IsNullOrEmpty(affix.AffixId))
                {
                    errors.Add($"Affix '{affix.name}' has no ID");
                }
            }

            // Check for duplicate IDs
            var seenIds = new HashSet<string>();
            foreach (var weapon in weaponDefinitions)
            {
                if (weapon != null && !seenIds.Add(weapon.ItemId))
                {
                    errors.Add($"Duplicate weapon ID: {weapon.ItemId}");
                }
            }

            seenIds.Clear();
            foreach (var affix in affixDefinitions)
            {
                if (affix != null && !seenIds.Add(affix.AffixId))
                {
                    errors.Add($"Duplicate affix ID: {affix.AffixId}");
                }
            }

            return errors.Count == 0;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            isInitialized = false;
        }
#endif
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;
using Lootbound.Gameplay.Inventory;

namespace Lootbound.Gameplay.Equipment
{
    /// <summary>
    /// Generates equipment instances with random affixes and names.
    /// </summary>
    public class EquipmentGenerator
    {
        private readonly IEquipmentRegistry registry;

        // V1 Rarity probabilities
        private const float CommonProbability = 0.65f;
        private const float UncommonProbability = 0.28f;
        // Rare: remaining 7%

        /// <summary>
        /// Create a new equipment generator.
        /// </summary>
        public EquipmentGenerator(IEquipmentRegistry registry)
        {
            this.registry = registry;
        }

        /// <summary>
        /// Generate an equipment instance from a weapon definition.
        /// </summary>
        /// <param name="definition">The weapon to base the equipment on</param>
        /// <param name="foundLocation">Where the equipment was found</param>
        /// <param name="seed">Optional seed for deterministic generation</param>
        /// <returns>A new ItemInstance with EquipmentData</returns>
        public ItemInstance GenerateWeapon(
            WeaponDefinition definition,
            string foundLocation,
            int? seed = null)
        {
            if (definition == null) return null;

            var random = seed.HasValue ? new System.Random(seed.Value) : new System.Random();

            // Roll rarity
            var rarity = RollRarity(random);

            // Roll affixes based on rarity
            var affixes = RollAffixes(rarity, random);

            // Generate name
            string baseName = ExtractBaseName(definition.DisplayName);
            string generatedName = EquipmentNameGenerator.Generate(baseName, rarity, affixes, random);

            // Create equipment data
            var equipmentData = new EquipmentData(
                definition.ItemId,
                generatedName,
                rarity,
                affixes,
                foundLocation ?? "Unknown");

            // Create item instance with equipment data
            return new ItemInstance(definition, equipmentData);
        }

        /// <summary>
        /// Generate equipment with a specific rarity.
        /// </summary>
        public ItemInstance GenerateWeaponWithRarity(
            WeaponDefinition definition,
            ItemRarity rarity,
            string foundLocation,
            int? seed = null)
        {
            if (definition == null) return null;

            var random = seed.HasValue ? new System.Random(seed.Value) : new System.Random();

            // Roll affixes based on specified rarity
            var affixes = RollAffixes(rarity, random);

            // Generate name
            string baseName = ExtractBaseName(definition.DisplayName);
            string generatedName = EquipmentNameGenerator.Generate(baseName, rarity, affixes, random);

            // Create equipment data
            var equipmentData = new EquipmentData(
                definition.ItemId,
                generatedName,
                rarity,
                affixes,
                foundLocation ?? "Unknown");

            return new ItemInstance(definition, equipmentData);
        }

        /// <summary>
        /// Roll rarity based on V1 probabilities.
        /// </summary>
        private ItemRarity RollRarity(System.Random random)
        {
            float roll = (float)random.NextDouble();

            if (roll < CommonProbability)
            {
                return ItemRarity.Common;
            }
            else if (roll < CommonProbability + UncommonProbability)
            {
                return ItemRarity.Uncommon;
            }
            else
            {
                return ItemRarity.Rare;
            }
        }

        /// <summary>
        /// Roll affixes based on rarity.
        /// V1 Rules:
        /// - Common: 0 affixes
        /// - Uncommon: 1 minor affix
        /// - Rare: 1 major affix
        /// </summary>
        private List<AffixInstance> RollAffixes(ItemRarity rarity, System.Random random)
        {
            var affixes = new List<AffixInstance>();

            switch (rarity)
            {
                case ItemRarity.Common:
                    // No affixes
                    break;

                case ItemRarity.Uncommon:
                    // One minor affix
                    var minorAffix = RollRandomAffix(AffixTier.Minor, random);
                    if (minorAffix != null)
                    {
                        affixes.Add(minorAffix);
                    }
                    break;

                case ItemRarity.Rare:
                    // One major affix
                    var majorAffix = RollRandomAffix(AffixTier.Major, random);
                    if (majorAffix != null)
                    {
                        affixes.Add(majorAffix);
                    }
                    break;

                default:
                    // Epic/Legendary not implemented in V1 - treat as Rare
                    var rareAffix = RollRandomAffix(AffixTier.Major, random);
                    if (rareAffix != null)
                    {
                        affixes.Add(rareAffix);
                    }
                    break;
            }

            return affixes;
        }

        /// <summary>
        /// Roll a random affix of a specific tier.
        /// </summary>
        private AffixInstance RollRandomAffix(AffixTier tier, System.Random random)
        {
            if (registry == null) return null;

            var registryObj = registry as EquipmentRegistry;
            if (registryObj == null) return null;

            var availableAffixes = registryObj.GetAffixesByTier(tier);
            if (availableAffixes == null || availableAffixes.Count == 0)
            {
                // Fall back to any tier
                availableAffixes = new List<AffixDefinition>(registryObj.AffixDefinitions);
            }

            if (availableAffixes.Count == 0) return null;

            // Pick random affix
            var affixDef = availableAffixes[random.Next(availableAffixes.Count)];

            // Roll value
            float value = affixDef.RollValue(random);

            return new AffixInstance(affixDef, value);
        }

        /// <summary>
        /// Extract a base name from a full display name.
        /// E.g., "Traveler Blade" -> "Blade"
        /// </summary>
        private string ExtractBaseName(string displayName)
        {
            if (string.IsNullOrEmpty(displayName)) return "Weapon";

            // Try to get the last word as the base name
            var parts = displayName.Split(' ');
            if (parts.Length > 1)
            {
                return parts[parts.Length - 1];
            }

            return displayName;
        }

        /// <summary>
        /// Create a simple common weapon with no affixes.
        /// Used for starting equipment.
        /// </summary>
        public ItemInstance CreateSimpleWeapon(
            WeaponDefinition definition,
            string customName,
            string foundLocation)
        {
            if (definition == null) return null;

            var equipmentData = new EquipmentData(
                definition.ItemId,
                customName ?? definition.DisplayName,
                ItemRarity.Common,
                new List<AffixInstance>(),
                foundLocation ?? "Starting Equipment");

            return new ItemInstance(definition, equipmentData);
        }
    }
}

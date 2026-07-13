using System;
using System.Collections.Generic;
using Lootbound.Gameplay.Inventory;

namespace Lootbound.Gameplay.Equipment
{
    /// <summary>
    /// Generates names for equipment based on rarity, affixes, and location.
    /// </summary>
    public static class EquipmentNameGenerator
    {
        // Base name prefixes for Common items
        private static readonly string[] CommonPrefixes = {
            "Old", "Worn", "Simple", "Plain", "Rusty", "Dull", "Battered"
        };

        // Base name prefixes for Uncommon items (affix-related)
        private static readonly string[] UncommonPrefixesSharp = {
            "Sharp", "Keen", "Honed"
        };

        private static readonly string[] UncommonPrefixesSwift = {
            "Swift", "Quick", "Light"
        };

        private static readonly string[] UncommonPrefixesBalanced = {
            "Balanced", "Steady", "True"
        };

        private static readonly string[] UncommonPrefixesHeavy = {
            "Heavy", "Weighty", "Dense"
        };

        // Location suffixes for Rare items
        private static readonly string[] LocationSuffixes = {
            "of the Quiet Valley",
            "of the Last Path",
            "of the High Grass",
            "of the Misty Hills",
            "of the Silent Woods",
            "of the Fallen Stones",
            "of the Old Road",
            "of the Distant Peaks",
            "of the Forgotten Trail",
            "of the Wanderer"
        };

        /// <summary>
        /// Generate a name for equipment.
        /// </summary>
        /// <param name="baseName">Base name of the item (e.g., "Blade", "Sword")</param>
        /// <param name="rarity">Rarity of the item</param>
        /// <param name="affixes">Affixes on the item</param>
        /// <param name="random">Random source for determinism</param>
        /// <returns>Generated name</returns>
        public static string Generate(
            string baseName,
            ItemRarity rarity,
            IReadOnlyList<AffixInstance> affixes,
            Random random)
        {
            if (string.IsNullOrEmpty(baseName))
            {
                baseName = "Weapon";
            }

            switch (rarity)
            {
                case ItemRarity.Common:
                    return GenerateCommonName(baseName, random);

                case ItemRarity.Uncommon:
                    return GenerateUncommonName(baseName, affixes, random);

                case ItemRarity.Rare:
                    return GenerateRareName(baseName, random);

                default:
                    // Epic/Legendary not implemented in V1
                    return GenerateRareName(baseName, random);
            }
        }

        private static string GenerateCommonName(string baseName, Random random)
        {
            string prefix = CommonPrefixes[random.Next(CommonPrefixes.Length)];
            return $"{prefix} {baseName}";
        }

        private static string GenerateUncommonName(
            string baseName,
            IReadOnlyList<AffixInstance> affixes,
            Random random)
        {
            // Pick prefix based on first affix type if available
            string[] prefixPool = CommonPrefixes;

            if (affixes != null && affixes.Count > 0)
            {
                var firstAffix = affixes[0];
                string affixId = firstAffix.DefinitionId?.ToLowerInvariant() ?? "";

                if (affixId.Contains("sharp") || affixId.Contains("damage"))
                {
                    prefixPool = UncommonPrefixesSharp;
                }
                else if (affixId.Contains("swift") || affixId.Contains("speed"))
                {
                    prefixPool = UncommonPrefixesSwift;
                }
                else if (affixId.Contains("balanced") || affixId.Contains("range"))
                {
                    prefixPool = UncommonPrefixesBalanced;
                }
                else if (affixId.Contains("heavy"))
                {
                    prefixPool = UncommonPrefixesHeavy;
                }
            }

            string prefix = prefixPool[random.Next(prefixPool.Length)];
            return $"{prefix} {baseName}";
        }

        private static string GenerateRareName(string baseName, Random random)
        {
            string suffix = LocationSuffixes[random.Next(LocationSuffixes.Length)];
            return $"{baseName} {suffix}";
        }

        /// <summary>
        /// Generate a simple name using Unity's random.
        /// </summary>
        public static string Generate(
            string baseName,
            ItemRarity rarity,
            IReadOnlyList<AffixInstance> affixes)
        {
            var random = new Random(UnityEngine.Random.Range(0, int.MaxValue));
            return Generate(baseName, rarity, affixes, random);
        }
    }
}

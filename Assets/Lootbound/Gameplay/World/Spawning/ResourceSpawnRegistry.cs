using System.Collections.Generic;
using UnityEngine;

namespace Lootbound.Gameplay.World.Spawning
{
    /// <summary>
    /// Registry of resource spawn definitions available to the content planner.
    /// One registry per content family (mirrors the EquipmentRegistry pattern).
    /// </summary>
    [CreateAssetMenu(fileName = "ResourceSpawnRegistry", menuName = "Lootbound/World Content/Resource Spawn Registry")]
    public class ResourceSpawnRegistry : ScriptableObject
    {
        [SerializeField]
        private List<ResourceSpawnDefinition> definitions = new List<ResourceSpawnDefinition>();

        public IReadOnlyList<ResourceSpawnDefinition> Definitions => definitions;
    }
}

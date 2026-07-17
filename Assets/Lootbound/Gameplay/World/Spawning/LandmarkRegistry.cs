using System.Collections.Generic;
using UnityEngine;

namespace Lootbound.Gameplay.World.Spawning
{
    /// <summary>
    /// Registry of landmark definitions available to the content planner.
    /// One registry per content family (mirrors the EquipmentRegistry pattern).
    /// </summary>
    [CreateAssetMenu(fileName = "LandmarkRegistry", menuName = "Lootbound/World Content/Landmark Registry")]
    public class LandmarkRegistry : ScriptableObject
    {
        [SerializeField]
        private List<LandmarkDefinition> definitions = new List<LandmarkDefinition>();

        public IReadOnlyList<LandmarkDefinition> Definitions => definitions;
    }
}

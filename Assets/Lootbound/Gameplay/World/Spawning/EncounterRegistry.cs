using System.Collections.Generic;
using UnityEngine;

namespace Lootbound.Gameplay.World.Spawning
{
    /// <summary>
    /// Registry of encounter definitions available to the content planner.
    /// One registry per content family (mirrors the EquipmentRegistry pattern).
    /// </summary>
    [CreateAssetMenu(fileName = "EncounterRegistry", menuName = "Lootbound/World Content/Encounter Registry")]
    public class EncounterRegistry : ScriptableObject
    {
        [SerializeField]
        private List<EncounterDefinition> definitions = new List<EncounterDefinition>();

        public IReadOnlyList<EncounterDefinition> Definitions => definitions;
    }
}

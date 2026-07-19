using UnityEngine;
using Lootbound.Gameplay.World.Layout;

namespace Lootbound.Gameplay.World.Spawning
{
    /// <summary>
    /// Immutable definition of a simple encounter that can occupy an EncounterReservation.
    /// V1: a small group of identical enemies spawned around the reservation anchor.
    /// </summary>
    [CreateAssetMenu(fileName = "Encounter_", menuName = "Lootbound/World Content/Encounter Definition")]
    public class EncounterDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField]
        [Tooltip("Stable ID. Falls back to the asset name when empty.")]
        private string encounterId;

        [SerializeField]
        private string displayName;

        [Header("Composition")]
        [SerializeField]
        [Tooltip("Enemy prefab spawned for each member (must carry EnemyBrain/EnemyHealth/EnemyCombat)")]
        private GameObject enemyPrefab;

        [SerializeField]
        [Range(1, 3)]
        private int minimumEnemyCount = 1;

        [SerializeField]
        [Range(1, 3)]
        private int maximumEnemyCount = 2;

        [Header("Placement")]
        [SerializeField]
        [Min(0f)]
        [Tooltip("Members are placed within this radius around the reservation anchor")]
        private float spawnSpreadRadius = 4f;

        [SerializeField]
        [Tooltip("Innermost ring where this encounter may appear (inclusive). The Refuge ring is always excluded for encounters, regardless of this value.")]
        private WorldRing minimumRing = WorldRing.Nearlands;

        [SerializeField]
        [Tooltip("Outermost ring where this encounter may appear (inclusive). Void is outside the playable disc by default - opt in explicitly.")]
        private WorldRing maximumRing = WorldRing.Edgelands;

        [Header("Selection")]
        [SerializeField]
        [Min(0f)]
        [Tooltip("Relative selection weight among compatible definitions (0 excludes)")]
        private float selectionWeight = 1f;

        [SerializeField]
        [Tooltip("Weight multiplier evaluated at the GLOBAL world depth (Depth01: 0 = Refuge, 1 = disc edge), multiplied with Selection Weight")]
        private AnimationCurve weightByDepth = AnimationCurve.Constant(0f, 1f, 1f);

        [Header("Progression Metadata (V1: authoring/debug only, not yet consumed by balance)")]
        [SerializeField]
        [Min(0f)]
        private float difficultyRating = 1f;

        [SerializeField]
        [Min(0f)]
        private float lootValue = 1f;

        public string EncounterId => string.IsNullOrEmpty(encounterId) ? name : encounterId;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
        public GameObject EnemyPrefab => enemyPrefab;
        public int MinimumEnemyCount => Mathf.Min(minimumEnemyCount, maximumEnemyCount);
        public int MaximumEnemyCount => Mathf.Max(minimumEnemyCount, maximumEnemyCount);
        public float SpawnSpreadRadius => spawnSpreadRadius;
        public WorldRing MinimumRing => minimumRing;
        public WorldRing MaximumRing => maximumRing;
        public float SelectionWeight => selectionWeight;
        public AnimationCurve WeightByDepth => weightByDepth;
        public float DifficultyRating => difficultyRating;
        public float LootValue => lootValue;
    }
}

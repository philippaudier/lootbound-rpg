using Lootbound.Gameplay.World.Layout;
using UnityEngine;

namespace Lootbound.Gameplay.World
{
    /// <summary>
    /// Configuration for procedural terrain generation.
    /// Contains all authoring parameters for the V1 terrain system.
    /// </summary>
    [CreateAssetMenu(fileName = "TerrainGenerationConfig", menuName = "Lootbound/Terrain Generation Config")]
    public class TerrainGenerationConfig : ScriptableObject
    {
        [Header("Seed")]
        [Tooltip("Default seed for terrain generation. Same seed always produces same terrain.")]
        [SerializeField] private int defaultSeed = 42;

        [Header("Terrain Dimensions")]
        [Tooltip("World size in meters (square terrain)")]
        [SerializeField] private float worldSize = 1024f;

        [Tooltip("Maximum terrain height in meters")]
        [SerializeField] private float terrainHeight = 260f;

        [Tooltip("Heightmap resolution (should be power of 2 + 1, e.g., 513, 1025)")]
        [SerializeField] private int heightmapResolution = 513;

        [Tooltip("Alphamap resolution for texture painting")]
        [SerializeField] private int alphamapResolution = 512;

        [Header("Macro Terrain")]
        [Tooltip("Scale of the main terrain features in meters")]
        [SerializeField] private float macroScale = 450f;

        [Tooltip("Number of noise octaves for macro terrain")]
        [Range(1, 8)]
        [SerializeField] private int macroOctaves = 4;

        [Tooltip("Amplitude reduction per octave")]
        [Range(0.1f, 0.9f)]
        [SerializeField] private float macroPersistence = 0.5f;

        [Tooltip("Frequency increase per octave")]
        [Range(1.5f, 3f)]
        [SerializeField] private float macroLacunarity = 2f;

        [Header("Ridge Features")]
        [Tooltip("Scale of ridge features in meters")]
        [SerializeField] private float ridgeScale = 350f;

        [Tooltip("Strength of ridge contribution (0-1)")]
        [Range(0f, 1f)]
        [SerializeField] private float ridgeStrength = 0.25f;

        [Header("Valley Features")]
        [Tooltip("Scale of valley features in meters")]
        [SerializeField] private float valleyScale = 400f;

        [Tooltip("Strength of valley contribution (0-1)")]
        [Range(0f, 1f)]
        [SerializeField] private float valleyStrength = 0.3f;

        [Header("Detail Noise")]
        [Tooltip("Scale of fine detail noise in meters")]
        [SerializeField] private float detailScale = 60f;

        [Tooltip("Strength of detail noise (0-1)")]
        [Range(0f, 0.5f)]
        [SerializeField] private float detailStrength = 0.08f;

        [Header("Height Processing")]
        [Tooltip("Curve to remap height distribution")]
        [SerializeField] private AnimationCurve heightRemap = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [Tooltip("Overall height multiplier after all processing")]
        [Range(0.1f, 2f)]
        [SerializeField] private float globalHeightStrength = 1f;

        [Header("Spawn Zone")]
        [Tooltip("Radius of the completely safe zone around spawn in meters")]
        [SerializeField] private float spawnSafeRadius = 24f;

        [Tooltip("Radius of the blend zone from safe to natural terrain in meters")]
        [SerializeField] private float spawnBlendRadius = 55f;

        [Tooltip("Target normalized height for spawn area (0-1)")]
        [Range(0.1f, 0.5f)]
        [SerializeField] private float spawnTargetHeight = 0.25f;

        [Tooltip("Maximum slope angle allowed at spawn point in degrees")]
        [SerializeField] private float maxSpawnSlope = 10f;

        [Header("Surface Classification")]
        [Tooltip("Normalized height threshold for lowland areas")]
        [Range(0f, 0.5f)]
        [SerializeField] private float lowlandThreshold = 0.3f;

        [Tooltip("Normalized height threshold for highland areas")]
        [Range(0.5f, 1f)]
        [SerializeField] private float highlandThreshold = 0.65f;

        [Tooltip("Slope angle threshold for steep surfaces in degrees")]
        [SerializeField] private float steepSlopeThreshold = 35f;

        [Header("World Layout")]
        [Tooltip("Configuration for world layout generation (optional)")]
        [SerializeField] private WorldLayoutConfig layoutConfig;

        [Tooltip("Configuration for world ring thresholds")]
        [SerializeField] private WorldRingConfig ringConfig;

        [Tooltip("Depth-to-progression curves (difficulty, loot tier, ambience). Built-in linear defaults apply when empty.")]
        [SerializeField] private Progression.WorldProgressionConfig progressionConfig;

        [Header("Generation Settings")]
        [Tooltip("Generate terrain when entering Play Mode")]
        [SerializeField] private bool generateOnStart = true;

        // Properties
        public int DefaultSeed => defaultSeed;
        public float WorldSize => worldSize;
        public float TerrainHeight => terrainHeight;
        public int HeightmapResolution => heightmapResolution;
        public int AlphamapResolution => alphamapResolution;

        public float MacroScale => macroScale;
        public int MacroOctaves => macroOctaves;
        public float MacroPersistence => macroPersistence;
        public float MacroLacunarity => macroLacunarity;

        public float RidgeScale => ridgeScale;
        public float RidgeStrength => ridgeStrength;

        public float ValleyScale => valleyScale;
        public float ValleyStrength => valleyStrength;

        public float DetailScale => detailScale;
        public float DetailStrength => detailStrength;

        public AnimationCurve HeightRemap => heightRemap;
        public float GlobalHeightStrength => globalHeightStrength;

        public float SpawnSafeRadius => spawnSafeRadius;
        public float SpawnBlendRadius => spawnBlendRadius;
        public float SpawnTargetHeight => spawnTargetHeight;
        public float MaxSpawnSlope => maxSpawnSlope;

        public float LowlandThreshold => lowlandThreshold;
        public float HighlandThreshold => highlandThreshold;
        public float SteepSlopeThreshold => steepSlopeThreshold;

        public bool GenerateOnStart => generateOnStart;

        public WorldLayoutConfig LayoutConfig => layoutConfig;
        public WorldRingConfig RingConfig => ringConfig;
        public Progression.WorldProgressionConfig ProgressionConfig => progressionConfig;

        /// <summary>
        /// Validate configuration values.
        /// </summary>
        public bool Validate(out string errorMessage)
        {
            if (worldSize <= 0f)
            {
                errorMessage = "World size must be positive.";
                return false;
            }

            if (terrainHeight <= 0f)
            {
                errorMessage = "Terrain height must be positive.";
                return false;
            }

            if (heightmapResolution < 33 || heightmapResolution > 4097)
            {
                errorMessage = "Heightmap resolution must be between 33 and 4097.";
                return false;
            }

            if (alphamapResolution < 16 || alphamapResolution > 2048)
            {
                errorMessage = "Alphamap resolution must be between 16 and 2048.";
                return false;
            }

            if (spawnSafeRadius <= 0f)
            {
                errorMessage = "Spawn safe radius must be positive.";
                return false;
            }

            if (spawnBlendRadius <= spawnSafeRadius)
            {
                errorMessage = "Spawn blend radius must be greater than safe radius.";
                return false;
            }

            if (heightRemap == null || heightRemap.keys.Length < 2)
            {
                errorMessage = "Height remap curve must have at least 2 keys.";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        private void OnValidate()
        {
            // Ensure heightmap resolution is valid for Unity Terrain
            int[] validResolutions = { 33, 65, 129, 257, 513, 1025, 2049, 4097 };
            bool isValid = false;
            foreach (int res in validResolutions)
            {
                if (heightmapResolution == res)
                {
                    isValid = true;
                    break;
                }
            }

            if (!isValid)
            {
                // Find closest valid resolution
                int closest = 513;
                int minDiff = int.MaxValue;
                foreach (int res in validResolutions)
                {
                    int diff = Mathf.Abs(heightmapResolution - res);
                    if (diff < minDiff)
                    {
                        minDiff = diff;
                        closest = res;
                    }
                }
                heightmapResolution = closest;
            }

            // Clamp values
            worldSize = Mathf.Max(64f, worldSize);
            terrainHeight = Mathf.Max(10f, terrainHeight);
            macroScale = Mathf.Max(10f, macroScale);
            ridgeScale = Mathf.Max(10f, ridgeScale);
            valleyScale = Mathf.Max(10f, valleyScale);
            detailScale = Mathf.Max(1f, detailScale);
            spawnSafeRadius = Mathf.Max(5f, spawnSafeRadius);
            spawnBlendRadius = Mathf.Max(spawnSafeRadius + 10f, spawnBlendRadius);
            maxSpawnSlope = Mathf.Clamp(maxSpawnSlope, 1f, 30f);
            steepSlopeThreshold = Mathf.Clamp(steepSlopeThreshold, 15f, 60f);

            // Ensure height remap curve exists
            if (heightRemap == null || heightRemap.keys.Length == 0)
            {
                heightRemap = AnimationCurve.Linear(0f, 0f, 1f, 1f);
            }
        }
    }
}

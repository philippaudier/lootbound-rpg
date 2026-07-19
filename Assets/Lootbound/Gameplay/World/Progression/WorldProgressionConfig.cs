using UnityEngine;

namespace Lootbound.Gameplay.World.Progression
{
    /// <summary>
    /// Designer-authored mapping from world depth (Depth01: 0 = Refuge,
    /// 1 = WorldDisc edge) to progression values. Pure data: gameplay and
    /// ambience systems read the resulting WorldRingContext, never this asset
    /// directly. When no asset is assigned, WorldProgression falls back to
    /// built-in linear defaults.
    /// </summary>
    [CreateAssetMenu(fileName = "WorldProgressionConfig", menuName = "Lootbound/World/Progression Config")]
    public class WorldProgressionConfig : ScriptableObject
    {
        [Header("Gameplay")]
        [Tooltip("Expected danger by depth (0 = calm, 1 = deadliest)")]
        [SerializeField] private AnimationCurve difficultyByDepth = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [Tooltip("Loot tier progression by depth (0-1, scaled to Max Loot Tier)")]
        [SerializeField] private AnimationCurve lootTierByDepth = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [Tooltip("Highest loot tier reachable at depth 1")]
        [SerializeField, Range(1, 10)] private int maxLootTier = 4;

        [Header("Ambience (parameters only - no rendering driven yet)")]
        [Tooltip("Fog density by depth (0 = clear Refuge, 1 = dense deep fog)")]
        [SerializeField] private AnimationCurve fogDensityByDepth = AnimationCurve.Linear(0f, 0f, 1f, 0.8f);

        [Tooltip("Light attenuation by depth (0 = full light, 1 = darkest)")]
        [SerializeField] private AnimationCurve lightAttenuationByDepth = AnimationCurve.Linear(0f, 0f, 1f, 0.5f);

        [Tooltip("Color saturation by depth (1 = vivid Refuge, lower = washed out)")]
        [SerializeField] private AnimationCurve saturationByDepth = AnimationCurve.Linear(0f, 1f, 1f, 0.6f);

        [Tooltip("Color temperature by depth (1 = warm, 0 = cold)")]
        [SerializeField] private AnimationCurve temperatureByDepth = AnimationCurve.Linear(0f, 0.7f, 1f, 0.25f);

        public AnimationCurve DifficultyByDepth => difficultyByDepth;
        public AnimationCurve LootTierByDepth => lootTierByDepth;
        public int MaxLootTier => maxLootTier;
        public AnimationCurve FogDensityByDepth => fogDensityByDepth;
        public AnimationCurve LightAttenuationByDepth => lightAttenuationByDepth;
        public AnimationCurve SaturationByDepth => saturationByDepth;
        public AnimationCurve TemperatureByDepth => temperatureByDepth;
    }
}

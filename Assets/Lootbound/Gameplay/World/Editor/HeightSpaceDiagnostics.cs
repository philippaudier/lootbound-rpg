using System.Globalization;
using System.Reflection;
using System.Text;
using Lootbound.Gameplay.World.Layout;
using UnityEditor;
using UnityEngine;

namespace Lootbound.Gameplay.World.Editor
{
    /// <summary>
    /// Measures how terrain generation settings behave across a deterministic
    /// seed corpus: raw noise range, slope statistics of the published height
    /// space, and world layout success rate. Used to tune the runtime preset
    /// (normalization, height budget, slope budget) with data instead of
    /// guesses. Run from the menu or in batch mode via -executeMethod.
    /// </summary>
    public static class HeightSpaceDiagnostics
    {
        private const string TerrainConfigPath = "Assets/Lootbound/ScriptableObjects/World/DefaultTerrainGenerationConfig.asset";
        private const string LayoutConfigPath = "Assets/Lootbound/ScriptableObjects/DefaultWorldLayoutConfig.asset";
        private const string RingConfigPath = "Assets/Lootbound/ScriptableObjects/World/DefaultWorldRingConfig.asset";
        private const string OutputPath = "HeightSpaceDiagnostics.txt";

        private const int RuntimeSeedCount = 100;
        private const int FixtureSeedCount = 50;

        [MenuItem("Lootbound/Diagnostics/Height Space Report")]
        public static void Run()
        {
            var terrainConfig = AssetDatabase.LoadAssetAtPath<TerrainGenerationConfig>(TerrainConfigPath);
            var layoutConfig = AssetDatabase.LoadAssetAtPath<WorldLayoutConfig>(LayoutConfigPath);
            var ringConfig = AssetDatabase.LoadAssetAtPath<WorldRingConfig>(RingConfigPath);

            if (terrainConfig == null || layoutConfig == null || ringConfig == null)
            {
                Debug.LogError("[HeightSpaceDiagnostics] Could not load runtime config assets.");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine("# Height space diagnostics");
            sb.AppendLine($"# Runtime preset: worldSize={terrainConfig.WorldSize} terrainHeight={terrainConfig.TerrainHeight} " +
                          $"resolution={terrainConfig.HeightmapResolution} normalizeHeightmap={terrainConfig.NormalizeHeightmap} " +
                          $"radialPathMaxSlope={layoutConfig.PrimaryPathMaxSlope} nodesPerRadialPath={layoutConfig.NodesPerRadialPath} " +
                          $"steps=[{layoutConfig.RadialStepMin},{layoutConfig.RadialStepMax}] attempts={layoutConfig.MaxGenerationAttempts}");
            sb.AppendLine();

            bool originalNormalize = terrainConfig.NormalizeHeightmap;
            try
            {
                SetNormalize(terrainConfig, true);
                RunCorpus(sb, "runtime-normalization-ON", terrainConfig, layoutConfig, ringConfig, RuntimeSeedCount);

                SetNormalize(terrainConfig, false);
                RunCorpus(sb, "runtime-normalization-OFF", terrainConfig, layoutConfig, ringConfig, RuntimeSeedCount);
            }
            finally
            {
                SetNormalize(terrainConfig, originalNormalize);
            }

            var fixtureTerrain = CreateFixtureTerrainConfig();
            var fixtureLayout = CreateFixtureLayoutConfig();
            RunCorpus(sb, "fixture-normalization-OFF", fixtureTerrain, fixtureLayout, WorldRingConfig.CreateDefault(), FixtureSeedCount);

            SetField(fixtureTerrain, "normalizeHeightmap", true);
            RunCorpus(sb, "fixture-normalization-ON", fixtureTerrain, fixtureLayout, WorldRingConfig.CreateDefault(), FixtureSeedCount);

            System.IO.File.WriteAllText(OutputPath, sb.ToString());
            Debug.Log($"[HeightSpaceDiagnostics] Report written to {System.IO.Path.GetFullPath(OutputPath)}");
        }

        private static void RunCorpus(
            StringBuilder sb,
            string label,
            TerrainGenerationConfig terrainConfig,
            WorldLayoutConfig layoutConfig,
            WorldRingConfig ringConfig,
            int seedCount)
        {
            sb.AppendLine($"## {label}");
            sb.AppendLine("seed;rawMin;rawMax;rawRange;slopeMin;slopeMax;slopeMean;layoutSuccess;attemptUsed;error");

            int successCount = 0;
            float slopeMaxOverall = 0f;
            double slopeMeanSum = 0;

            for (int seed = 0; seed < seedCount; seed++)
            {
                var line = MeasureSeed(seed, terrainConfig, layoutConfig, ringConfig, out bool success);
                sb.AppendLine(line);
                if (success) successCount++;
            }

            // Extra seeds of interest beyond the corpus range
            foreach (int seed in new[] { 11, 12345, 54321, 99999 })
            {
                if (seed < seedCount) continue;
                sb.AppendLine(MeasureSeed(seed, terrainConfig, layoutConfig, ringConfig, out _));
            }

            sb.AppendLine($"# {label}: layout success {successCount}/{seedCount}");
            sb.AppendLine();
        }

        private static string MeasureSeed(
            int seed,
            TerrainGenerationConfig terrainConfig,
            WorldLayoutConfig layoutConfig,
            WorldRingConfig ringConfig,
            out bool success)
        {
            var context = new TerrainGenerationContext(
                seed, terrainConfig.HeightmapResolution, terrainConfig.WorldSize, terrainConfig.TerrainHeight);
            TerrainHeightGenerator.Generate(context, terrainConfig);

            float slopeMin = float.MaxValue, slopeMax = float.MinValue;
            double slopeSum = 0;
            int resolution = context.Resolution;
            for (int x = 0; x < resolution; x++)
            {
                for (int z = 0; z < resolution; z++)
                {
                    float s = context.SlopeMap[x, z];
                    if (s < slopeMin) slopeMin = s;
                    if (s > slopeMax) slopeMax = s;
                    slopeSum += s;
                }
            }
            float slopeMean = (float)(slopeSum / (resolution * resolution));

            var disc = new WorldDiscDefinition(terrainConfig.WorldSize * 0.5f, ringConfig);
            var sampler = new TerrainContextSampler(context);
            var result = WorldLayoutGenerator.Generate(seed, disc, sampler, layoutConfig);
            success = result.Success;

            var ci = CultureInfo.InvariantCulture;
            string attempt = result.Success ? (result.Layout.GenerationAttempt + 1).ToString() : "-";
            string error = result.Success ? "" : result.Error;
            return string.Format(ci, "{0};{1:F4};{2:F4};{3:F4};{4:F1};{5:F1};{6:F1};{7};{8};{9}",
                seed, context.MinHeight, context.MaxHeight, context.MaxHeight - context.MinHeight,
                slopeMin, slopeMax, slopeMean, result.Success ? "OK" : "FAIL", attempt, error);
        }

        private static void SetNormalize(TerrainGenerationConfig config, bool value)
        {
            SetField(config, "normalizeHeightmap", value);
        }

        private static void SetField(object obj, string fieldName, object value)
        {
            var field = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                Debug.LogError($"[HeightSpaceDiagnostics] Field '{fieldName}' not found on {obj.GetType().Name}");
                return;
            }
            field.SetValue(obj, value);
        }

        /// <summary>
        /// Replica of the WorldLayoutTests terrain fixture.
        /// </summary>
        private static TerrainGenerationConfig CreateFixtureTerrainConfig()
        {
            var config = ScriptableObject.CreateInstance<TerrainGenerationConfig>();
            SetField(config, "worldSize", 1024f);
            SetField(config, "terrainHeight", 150f);
            SetField(config, "heightmapResolution", 129);
            SetField(config, "normalizeHeightmap", false);
            SetField(config, "macroScale", 500f);
            SetField(config, "macroOctaves", 3);
            SetField(config, "macroPersistence", 0.4f);
            SetField(config, "macroLacunarity", 2f);
            SetField(config, "ridgeScale", 400f);
            SetField(config, "ridgeStrength", 0.1f);
            SetField(config, "valleyScale", 400f);
            SetField(config, "valleyStrength", 0.1f);
            SetField(config, "detailScale", 80f);
            SetField(config, "detailStrength", 0.03f);
            SetField(config, "globalHeightStrength", 0.8f);
            SetField(config, "heightRemap", new AnimationCurve(
                new Keyframe(0f, 0.1f), new Keyframe(0.5f, 0.5f), new Keyframe(1f, 0.9f)));
            return config;
        }

        /// <summary>
        /// Replica of the WorldLayoutTests layout fixture.
        /// </summary>
        private static WorldLayoutConfig CreateFixtureLayoutConfig()
        {
            var config = ScriptableObject.CreateInstance<WorldLayoutConfig>();
            SetField(config, "maxGenerationAttempts", 10);
            SetField(config, "minimumRadialPathCount", 3);
            SetField(config, "maximumRadialPathCount", 4);
            SetField(config, "nodesPerRadialPath", 4);
            SetField(config, "radialStepMin", 80f);
            SetField(config, "radialStepMax", 150f);
            SetField(config, "radialPathMaxSlope", 40f);
            SetField(config, "edgeSamplePoints", 3);
            SetField(config, "branchCount", 2);
            SetField(config, "branchMaxNodes", 2);
            SetField(config, "branchChance", 0.5f);
            SetField(config, "encounterReservationCount", 3);
            SetField(config, "resourceReservationCount", 2);
            SetField(config, "landmarkReservationCount", 2);
            SetField(config, "nodeMinSpacing", 50f);
            SetField(config, "candidatesPerStep", 16);
            SetField(config, "outwardProgressionWeight", 30f);
            SetField(config, "terrainSlopeWeight", 25f);
            SetField(config, "curvaturePenaltyWeight", 15f);
            SetField(config, "corridorWidth", 8f);
            SetField(config, "corridorBlend", 12f);
            SetField(config, "maxCorrectionStrength", 0.25f);
            SetField(config, "refugeFlattenRadius", 20f);
            SetField(config, "clearingFlattenRadius", 12f);
            SetField(config, "refugeMaxCenterOffset", 30f);
            SetField(config, "minimumAngularSeparation", 45f);
            SetField(config, "maxAngularGap", 120f);
            SetField(config, "junctionRadius", 8f);
            SetField(config, "clearingRadius", 15f);
            SetField(config, "viewpointRadius", 6f);
            SetField(config, "refugeRadius", 24f);
            SetField(config, "outerDestinationRadius", 20f);
            return config;
        }
    }
}

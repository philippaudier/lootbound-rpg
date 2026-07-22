using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Lootbound.Gameplay.World;
using Lootbound.Gameplay.World.Chunking;
using Debug = UnityEngine.Debug;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Lootbound.Tests.PlayMode
{
    /// <summary>
    /// Performance probe for the chunk pipeline (M4.0 baseline, kept for the
    /// before/after comparison). It MEASURES and logs - it asserts nothing about
    /// timings, so it can never flake. Uses the REAL generator: the in-region
    /// final grid is built headlessly (no Unity Terrain) and injected, the
    /// out-of-region path exercises the analytic field. Results are logged with
    /// the [CHUNK-BASELINE] prefix.
    /// </summary>
    public class TerrainChunkPerfProbeTests
    {
        private const int Res = 129;
        private const int AlphaRes = 129;
        private const float Size = 128f;

        [UnityTest]
        public IEnumerator Baseline_MeasuresCurrentChunkPipeline()
        {
            var config = ScriptableObject.CreateInstance<TerrainGenerationConfig>();
            // The probe drives the generator manually - never let its Start() try
            // to generate onto a (deliberately absent) legacy Terrain.
            SetField(config, "generateOnStart", false);
            var host = new GameObject("PerfGenerator");
            var generator = host.AddComponent<ProceduralTerrainGenerator>();
            SetField(generator, "config", config);
            SetField(generator, "currentSeed", 12345);

            // The final centred world grid, built headlessly (no Unity Terrain).
            var bounds = WorldBounds.FromCenter(0f, 0f, config.WorldSize);
            var context = new TerrainGenerationContext(
                12345, config.HeightmapResolution, config.WorldSize, config.TerrainHeight, 1, bounds);
            TerrainHeightGenerator.Generate(context, config);
            SetField(generator, "context", context);
            SetField(generator, "isGenerated", true);

            var builder = new TerrainChunkBuilder(generator);
            var parent = new GameObject("PerfChunks").transform;
            var layers = new[] { new TerrainLayer(), new TerrainLayer(), new TerrainLayer(), new TerrainLayer() };

            // Warmup: JIT + first-time Unity terrain paths.
            var warm = new TerrainChunk(parent, layers);
            warm.Apply(builder.Build(new TerrainChunkCoordinate(0, 0), Res, Size, AlphaRes));
            yield return null;

            var sw = new Stopwatch();

            // --- builds (CPU sampling only, no Unity apply) ---
            double buildHeightsInRegion = AverageBuildMs(builder, sw, 0, new[] { -4, -3, -2, -1 }, 0);
            double buildFullInRegion = AverageBuildMs(builder, sw, 1, new[] { -4, -3, -2, -1 }, AlphaRes);
            double buildHeightsField = AverageBuildMs(builder, sw, 100, new[] { 100, 101, 102, 103 }, 0);

            // --- cold instance creation (pool factory at first sight) ---
            sw.Restart();
            var cold = new TerrainChunk(parent, layers);
            sw.Stop();
            double instanceMs = sw.Elapsed.TotalMilliseconds;

            // --- applies (Unity side: SetHeights + collider cook, SetAlphamaps) ---
            TerrainChunkData dataFull = builder.Build(new TerrainChunkCoordinate(0, 2), Res, Size, AlphaRes);
            sw.Restart();
            cold.Apply(dataFull);
            sw.Stop();
            double applyColdMs = sw.Elapsed.TotalMilliseconds; // first-time resolutions included

            TerrainChunkData dataHeights = builder.Build(new TerrainChunkCoordinate(1, 2), Res, Size, 0);
            sw.Restart();
            cold.Apply(dataHeights);
            sw.Stop();
            double applyHeightsWarmMs = sw.Elapsed.TotalMilliseconds;

            TerrainChunkData dataFull2 = builder.Build(new TerrainChunkCoordinate(2, 2), Res, Size, AlphaRes);
            sw.Restart();
            cold.Apply(dataFull2);
            sw.Stop();
            double applyFullWarmMs = sw.Elapsed.TotalMilliseconds;

            // --- GC churn of one full build (approximate: single sample) ---
            int gcBefore = GC.CollectionCount(0);
            long memBefore = GC.GetTotalMemory(false);
            builder.Build(new TerrainChunkCoordinate(3, 2), Res, Size, AlphaRes);
            long churnBytes = GC.GetTotalMemory(false) - memBefore;
            bool gcRanDuringProbe = GC.CollectionCount(0) != gcBefore;

            double m3FrameEstimate = 4.0 * (buildFullInRegion + applyFullWarmMs);

            Debug.Log(
                "[CHUNK-BASELINE] res=129 alpha=129 size=128 (times in ms, avg of 4 unless noted)\n" +
                $"  build.heights.inRegion   = {buildHeightsInRegion:F1}\n" +
                $"  build.full.inRegion      = {buildFullInRegion:F1}   (splat = {buildFullInRegion - buildHeightsInRegion:F1})\n" +
                $"  build.heights.fieldPath  = {buildHeightsField:F1}   (out-of-region analytic field)\n" +
                $"  instance.create.cold     = {instanceMs:F1}   (single)\n" +
                $"  apply.full.cold          = {applyColdMs:F1}   (single, first-time resolutions)\n" +
                $"  apply.heightsOnly.warm   = {applyHeightsWarmMs:F1}   (single, SetHeights+collider)\n" +
                $"  apply.full.warm          = {applyFullWarmMs:F1}   (single, +SetAlphamaps ~= {applyFullWarmMs - applyHeightsWarmMs:F1})\n" +
                $"  gc.churn.perFullBuild    = {churnBytes / 1024} KB{(gcRanDuringProbe ? " (GC ran during probe - underestimated)" : string.Empty)}\n" +
                $"  M3 worst frame estimate  = 4 x (build.full + apply.full) ~= {m3FrameEstimate:F0} ms");

            warm.Dispose();
            cold.Dispose();
            UnityEngine.Object.Destroy(parent.gameObject);
            UnityEngine.Object.Destroy(host);
            UnityEngine.Object.Destroy(config);
            yield return null;
        }

        private static double AverageBuildMs(
            TerrainChunkBuilder builder, Stopwatch sw, int z, int[] xs, int alphamapResolution)
        {
            double total = 0;
            for (int i = 0; i < xs.Length; i++)
            {
                sw.Restart();
                builder.Build(new TerrainChunkCoordinate(xs[i], z), Res, Size, alphamapResolution);
                sw.Stop();
                total += sw.Elapsed.TotalMilliseconds;
            }
            return total / xs.Length;
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"field {name} not found on {target.GetType().Name}");
            field.SetValue(target, value);
        }
    }
}

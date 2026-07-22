using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using Lootbound.Gameplay.World;
using Lootbound.Gameplay.World.Knowledge;
using Lootbound.World.Coordinates;

namespace Lootbound.Tests.EditMode
{
    /// <summary>
    /// Golden snapshot of the generator's deterministic output for a fixed seed.
    ///
    /// This is NOT a cross-milestone bit-for-bit contract. When the world is
    /// deliberately changed - e.g. the M2 migration re-anchors the procedural
    /// field around the Refuge at (0,0), which intentionally changes the sampled
    /// portion of the field - the reference is REGENERATED and the new values
    /// become the reference. Its job is to catch UNINTENTIONAL drift: in six
    /// months, if someone breaks the noise, the curves, the slope or the
    /// roughness maths, this goes red immediately instead of silently.
    ///
    /// Scope: the deterministic CORE (base height + slope + roughness) sampled at
    /// the Refuge and a fixed lattice. Authored deformations (layout flattening,
    /// landmark seats, the Refuge carve) have their own dedicated tests, and the
    /// full visual is covered by the manual before/after capture.
    ///
    /// Re-baseline after an INTENTIONAL change: delete the baseline file, run this
    /// test once (it recreates the file and passes), then commit the file.
    /// </summary>
    public class GeneratorGoldenSnapshotTests
    {
        private const int Seed = 12345;

        // Per-metric tolerances. Same-platform Perlin/Mathf is deterministic, so
        // these only absorb text round-trip; a real regression dwarfs them.
        private const float HeightToleranceMeters = 0.02f;
        private const float SlopeToleranceDegrees = 0.05f;
        private const float RoughnessToleranceMeters = 0.02f;

        private static string BaselineDirectory =>
            Path.Combine(Application.dataPath, "Lootbound", "Tests", "EditMode", "GoldenData");

        private static string BaselinePath =>
            Path.Combine(BaselineDirectory, $"GeneratorGolden_{Seed}.csv");

        private readonly struct Probe
        {
            public readonly float X, Z, Height, Slope, Roughness;
            public Probe(float x, float z, float height, float slope, float roughness)
            {
                X = x; Z = z; Height = height; Slope = slope; Roughness = roughness;
            }
        }

        [Test]
        public void Generator_MatchesGoldenSnapshot()
        {
            var config = ScriptableObject.CreateInstance<TerrainGenerationConfig>();
            try
            {
                var actual = ComputeFingerprint(config);

                if (!File.Exists(BaselinePath))
                {
                    Directory.CreateDirectory(BaselineDirectory);
                    File.WriteAllText(BaselinePath, Serialize(actual));
                    Assert.Pass($"Golden baseline created with {actual.Count} probes at {BaselinePath}. " +
                                "Commit it, then re-run to compare.");
                    return;
                }

                var expected = Deserialize(File.ReadAllText(BaselinePath));
                Assert.AreEqual(expected.Count, actual.Count,
                    "Probe count changed - regenerate the golden baseline intentionally.");

                for (int i = 0; i < expected.Count; i++)
                {
                    Probe e = expected[i];
                    Probe a = actual[i];
                    Assert.AreEqual(e.X, a.X, 0.001f, $"probe {i} X drifted");
                    Assert.AreEqual(e.Z, a.Z, 0.001f, $"probe {i} Z drifted");
                    Assert.AreEqual(e.Height, a.Height, HeightToleranceMeters,
                        $"probe {i} at ({a.X:F1},{a.Z:F1}) height regressed");
                    Assert.AreEqual(e.Slope, a.Slope, SlopeToleranceDegrees,
                        $"probe {i} at ({a.X:F1},{a.Z:F1}) slope regressed");
                    Assert.AreEqual(e.Roughness, a.Roughness, RoughnessToleranceMeters,
                        $"probe {i} at ({a.X:F1},{a.Z:F1}) roughness regressed");
                }
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        private static List<Probe> ComputeFingerprint(TerrainGenerationConfig config)
        {
            int resolution = config.HeightmapResolution;

            // The world region as the CURRENT generator materializes it. M2 flips
            // this single line to WorldBounds.FromCenter(0f, 0f, config.WorldSize)
            // (Refuge at (0,0)) - the intentional re-anchoring point where the
            // baseline is regenerated. No coordinate offset is ever added.
            var bounds = WorldBounds.FromCorner(0f, 0f, config.WorldSize);

            var context = new TerrainGenerationContext(
                Seed, resolution, config.WorldSize, config.TerrainHeight, 1, bounds);
            TerrainHeightGenerator.Generate(context, config);

            WorldKnowledge knowledge = WorldKnowledgeComposer.Build(config, Seed);

            var probes = new List<Probe>();
            foreach ((float x, float z) in ProbeCoordinates(context))
            {
                float height = context.SampleHeightAtWorld(x, z);
                float slope = context.SampleSlopeAtWorld(x, z);
                float roughness = knowledge.Roughness.Evaluate(new WorldCoordinate(x, z));
                probes.Add(new Probe(x, z, height, slope, roughness));
            }
            return probes;
        }

        // The Refuge centre plus a fixed 5x4 lattice across the region. Anchored on
        // the region centre so the probe SET is identical before and after the
        // recentring - only the sampled VALUES change.
        private static IEnumerable<(float x, float z)> ProbeCoordinates(TerrainGenerationContext context)
        {
            Vector3 c = context.WorldCenter;
            float half = context.WorldSize * 0.5f;

            yield return (c.x, c.z); // the Refuge

            float[] fx = { -0.8f, -0.4f, 0f, 0.4f, 0.8f };
            float[] fz = { -0.6f, -0.2f, 0.2f, 0.6f };
            foreach (float fzz in fz)
            {
                foreach (float fxx in fx)
                {
                    yield return (c.x + fxx * half, c.z + fzz * half);
                }
            }
        }

        private static string Serialize(List<Probe> probes)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# GeneratorGolden seed={Seed} probes={probes.Count} fields=x;z;height_m;slope_deg;roughness_m");
            foreach (Probe p in probes)
            {
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "{0:F3};{1:F3};{2:F5};{3:F5};{4:F5}", p.X, p.Z, p.Height, p.Slope, p.Roughness));
            }
            return sb.ToString();
        }

        private static List<Probe> Deserialize(string text)
        {
            var probes = new List<Probe>();
            foreach (string raw in text.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#"))
                {
                    continue;
                }

                string[] parts = line.Split(';');
                probes.Add(new Probe(
                    float.Parse(parts[0], CultureInfo.InvariantCulture),
                    float.Parse(parts[1], CultureInfo.InvariantCulture),
                    float.Parse(parts[2], CultureInfo.InvariantCulture),
                    float.Parse(parts[3], CultureInfo.InvariantCulture),
                    float.Parse(parts[4], CultureInfo.InvariantCulture)));
            }
            return probes;
        }
    }
}

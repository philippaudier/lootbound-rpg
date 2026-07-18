using NUnit.Framework;
using Lootbound.Gameplay.World;
using Lootbound.Gameplay.World.Navigation;

namespace Lootbound.Tests.EditMode
{
    /// <summary>
    /// Orchestration tests for the navigation/content gate: WHEN content may
    /// spawn relative to terrain publication and navigation builds. Pure C#,
    /// no NavMesh involved.
    /// </summary>
    public class NavigationContentGateTests
    {
        [Test]
        public void NavigationReady_ForPendingGeneration_AllowsFullSpawn()
        {
            var gate = new NavigationContentGate();
            gate.TerrainPublished(1);

            Assert.AreEqual(NavigationGateDecision.SpawnAll, gate.NavigationCompleted(1, success: true));
        }

        [Test]
        public void NavigationFailed_ForPendingGeneration_AllowsSpawnWithoutEncounters()
        {
            var gate = new NavigationContentGate();
            gate.TerrainPublished(1);

            Assert.AreEqual(NavigationGateDecision.SpawnWithoutEncounters, gate.NavigationCompleted(1, success: false));
        }

        [Test]
        public void NavigationResult_WithNothingPending_IsIgnored()
        {
            var gate = new NavigationContentGate();

            Assert.AreEqual(NavigationGateDecision.Ignore, gate.NavigationCompleted(1, success: true));
        }

        [Test]
        public void StaleNavigationResult_FromPreviousGeneration_IsIgnored()
        {
            var gate = new NavigationContentGate();
            gate.TerrainPublished(1);
            gate.TerrainPublished(2); // regeneration before navigation finished

            Assert.AreEqual(NavigationGateDecision.Ignore, gate.NavigationCompleted(1, success: true),
                "A build belonging to an older generation must never release content");
            Assert.AreEqual(NavigationGateDecision.SpawnAll, gate.NavigationCompleted(2, success: true),
                "The current generation must still be released by its own build");
        }

        [Test]
        public void Generation_IsReleasedExactlyOnce()
        {
            var gate = new NavigationContentGate();
            gate.TerrainPublished(1);

            Assert.AreEqual(NavigationGateDecision.SpawnAll, gate.NavigationCompleted(1, success: true));
            Assert.AreEqual(NavigationGateDecision.Ignore, gate.NavigationCompleted(1, success: true),
                "A duplicate navigation result must not trigger a second spawn pass");
        }

        [Test]
        public void TwoGenerationsWithSameSeed_RemainDistinguishable()
        {
            // The gate is keyed on the monotone GenerationId, never the seed:
            // contexts built from the same seed get distinct identities.
            var first = new TerrainGenerationContext(42, 33, 128f, 50f, generationId: 1);
            var second = new TerrainGenerationContext(42, 33, 128f, 50f, generationId: 2);

            Assert.AreEqual(first.Seed, second.Seed);
            Assert.AreNotEqual(first.GenerationId, second.GenerationId);

            var gate = new NavigationContentGate();
            gate.TerrainPublished(second.GenerationId);

            Assert.AreEqual(NavigationGateDecision.Ignore,
                gate.NavigationCompleted(first.GenerationId, success: true),
                "Same seed must not be enough to release a different generation");
            Assert.AreEqual(NavigationGateDecision.SpawnAll,
                gate.NavigationCompleted(second.GenerationId, success: true));
        }

        [Test]
        public void Reset_ClearsPendingGeneration()
        {
            var gate = new NavigationContentGate();
            gate.TerrainPublished(7);
            gate.Reset();

            Assert.IsNull(gate.PendingGenerationId);
            Assert.AreEqual(NavigationGateDecision.Ignore, gate.NavigationCompleted(7, success: true));
        }

        [Test]
        public void PendingGenerationId_TracksLifecycle()
        {
            var gate = new NavigationContentGate();
            Assert.IsNull(gate.PendingGenerationId);

            gate.TerrainPublished(3);
            Assert.AreEqual(3, gate.PendingGenerationId);

            gate.NavigationCompleted(3, success: true);
            Assert.IsNull(gate.PendingGenerationId, "Releasing a generation must consume the pending state");
        }
    }
}

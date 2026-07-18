namespace Lootbound.Gameplay.World.Navigation
{
    /// <summary>
    /// Decision returned when a navigation build result arrives for a
    /// pending generation.
    /// </summary>
    public enum NavigationGateDecision
    {
        /// <summary>Result is stale or nothing is pending - do nothing.</summary>
        Ignore,

        /// <summary>Navigation is ready: spawn the full content plan.</summary>
        SpawnAll,

        /// <summary>
        /// Navigation build failed for this generation: spawn content that
        /// does not depend on navigation (resources, landmarks) and reject
        /// navigation-dependent content (encounters) with an explicit reason.
        /// </summary>
        SpawnWithoutEncounters
    }

    /// <summary>
    /// Pure orchestration state deciding WHEN content may spawn relative to
    /// terrain publication and navigation builds. Holds no Unity objects and
    /// no content knowledge - only generation identities.
    ///
    /// Navigation is global for building the mesh but LOCAL for validating
    /// individual spawns: this gate only answers "may this generation spawn
    /// now"; per-entry navigability stays the spawner's per-position check.
    /// </summary>
    public sealed class NavigationContentGate
    {
        /// <summary>
        /// Generation currently waiting for its navigation result, or null.
        /// </summary>
        public int? PendingGenerationId { get; private set; }

        /// <summary>
        /// A new terrain generation was published: it becomes the only
        /// generation whose navigation result will be honoured.
        /// </summary>
        public void TerrainPublished(int generationId)
        {
            PendingGenerationId = generationId;
        }

        /// <summary>
        /// A navigation build finished. Returns the spawn decision and
        /// consumes the pending generation, so a given generation is
        /// released exactly once.
        /// </summary>
        public NavigationGateDecision NavigationCompleted(int generationId, bool success)
        {
            if (PendingGenerationId == null || PendingGenerationId.Value != generationId)
            {
                return NavigationGateDecision.Ignore;
            }

            PendingGenerationId = null;
            return success ? NavigationGateDecision.SpawnAll : NavigationGateDecision.SpawnWithoutEncounters;
        }

        public void Reset()
        {
            PendingGenerationId = null;
        }
    }
}

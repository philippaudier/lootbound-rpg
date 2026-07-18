using UnityEngine;

namespace Lootbound.Gameplay.World.Navigation
{
    /// <summary>
    /// Outcome of one runtime NavMesh build, tied to a specific generation.
    /// Consumers must compare GenerationId against the generation they are
    /// waiting for and ignore results from older generations.
    /// </summary>
    public sealed class RuntimeNavigationBuildResult
    {
        public bool Success { get; }

        /// <summary>Monotone generation identity this navigation belongs to.</summary>
        public int GenerationId { get; }

        /// <summary>Seed of the generation (informational; not an identity).</summary>
        public int Seed { get; }

        public float DurationMs { get; }

        /// <summary>World-space bounds the surface collected geometry from.</summary>
        public Bounds BoundsUsed { get; }

        /// <summary>Number of NavMesh surfaces built (1 in V1).</summary>
        public int SurfaceCount { get; }

        /// <summary>Triangle count of the built NavMesh (diagnostics only).</summary>
        public int TriangleCount { get; }

        /// <summary>Human-readable reason when Success is false.</summary>
        public string FailureReason { get; }

        private RuntimeNavigationBuildResult(
            bool success,
            int generationId,
            int seed,
            float durationMs,
            Bounds boundsUsed,
            int surfaceCount,
            int triangleCount,
            string failureReason)
        {
            Success = success;
            GenerationId = generationId;
            Seed = seed;
            DurationMs = durationMs;
            BoundsUsed = boundsUsed;
            SurfaceCount = surfaceCount;
            TriangleCount = triangleCount;
            FailureReason = failureReason;
        }

        public static RuntimeNavigationBuildResult Succeeded(
            int generationId, int seed, float durationMs, Bounds bounds, int surfaceCount, int triangleCount)
        {
            return new RuntimeNavigationBuildResult(
                true, generationId, seed, durationMs, bounds, surfaceCount, triangleCount, null);
        }

        public static RuntimeNavigationBuildResult Failed(
            int generationId, int seed, string reason, float durationMs = 0f)
        {
            return new RuntimeNavigationBuildResult(
                false, generationId, seed, durationMs, default, 0, 0, reason);
        }

        public override string ToString()
        {
            return Success
                ? $"NavigationBuild[gen={GenerationId} seed={Seed} {DurationMs:F0}ms {TriangleCount} tris]"
                : $"NavigationBuild[gen={GenerationId} seed={Seed} FAILED: {FailureReason}]";
        }
    }
}

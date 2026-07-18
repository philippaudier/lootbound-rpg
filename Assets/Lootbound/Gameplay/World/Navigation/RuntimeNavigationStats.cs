using UnityEngine;

namespace Lootbound.Gameplay.World.Navigation
{
    /// <summary>
    /// Aggregated build statistics across a session. Developer diagnostics
    /// only - never consumed by gameplay.
    /// </summary>
    public sealed class RuntimeNavigationStats
    {
        public int BuildCount { get; private set; }
        public float LastDurationMs { get; private set; }
        public float AverageDurationMs { get; private set; }
        public float WorstDurationMs { get; private set; }
        public int LastTriangleCount { get; private set; }
        public Bounds LastBounds { get; private set; }

        private double totalDurationMs;

        public void RecordBuild(float durationMs, int triangleCount, Bounds bounds)
        {
            BuildCount++;
            totalDurationMs += durationMs;
            LastDurationMs = durationMs;
            AverageDurationMs = (float)(totalDurationMs / BuildCount);
            if (durationMs > WorstDurationMs) WorstDurationMs = durationMs;
            LastTriangleCount = triangleCount;
            LastBounds = bounds;
        }
    }
}

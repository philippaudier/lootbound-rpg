using System.Collections.Generic;
using Lootbound.Gameplay.World.Layout;

namespace Lootbound.Gameplay.World.Landmarks
{
    /// <summary>
    /// Turns landmark placements into the ordered set of terrain-seat
    /// descriptions. Pure: one <see cref="LandmarkSeatSolver"/> call per
    /// placement, keeping only those whose definition asks for terrain
    /// integration. Ordered deterministically by LandmarkId so the set is
    /// stable and independent of iteration order.
    /// </summary>
    public static class LandmarkTerrainStampPlanner
    {
        public static IReadOnlyList<LandmarkTerrainStamp> Plan(
            IReadOnlyList<LandmarkPlacement> placements,
            ITerrainSampler sampler)
        {
            var stamps = new List<LandmarkTerrainStamp>();
            if (placements == null || sampler == null)
            {
                return stamps;
            }

            foreach (var placement in placements)
            {
                if (LandmarkSeatSolver.TrySolve(placement, sampler, out var stamp))
                {
                    stamps.Add(stamp);
                }
            }

            stamps.Sort((a, b) => string.CompareOrdinal(a.LandmarkId, b.LandmarkId));
            return stamps;
        }
    }
}

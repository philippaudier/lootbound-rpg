using UnityEngine;

namespace Lootbound.Gameplay.World
{
    /// <summary>
    /// Provides a default spawn at the world centre - a safe fallback used only
    /// when no layout exists. The real home is the layout's Refuge (resolved
    /// after generation), and the terrain around it is carved by
    /// <see cref="RefugeSeating"/>. This planner no longer spiral-searches a spot
    /// nor flattens the terrain: that produced an orphaned flat cone the player
    /// could spawn on, disconnected from the actual Refuge.
    /// </summary>
    public static class TerrainSpawnPlanner
    {
        public static void PlanSpawn(TerrainGenerationContext context, TerrainGenerationConfig config)
        {
            float center = context.WorldSize * 0.5f;
            float height = context.SampleHeightAtWorld(center, center);
            context.SpawnPosition = new Vector3(center, height, center);

            var (cx, cz) = context.WorldToHeightmap(context.SpawnPosition);
            context.SpawnSlope = context.SlopeMap[cx, cz];
        }
    }
}

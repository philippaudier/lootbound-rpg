using System.Collections.Generic;
using UnityEngine;
using Lootbound.Gameplay.World.Progression;
using Lootbound.Gameplay.World.Spawning;

namespace Lootbound.Gameplay.World.Population
{
    /// <summary>
    /// Decides what SHOULD live in a cell. Pure C# and fully deterministic:
    /// the same WorldSeed + cell + PopulationGenerationVersion always
    /// produces the same intentions (definition, group size, candidate
    /// positions). The player only influences WHEN a cell is planned, never
    /// WHAT it contains. Ring/depth rules go exclusively through
    /// WorldProgression.GetContext and the shared WorldContentCompatibility
    /// rule - no local depth math.
    /// </summary>
    public static class AmbientPopulationPlanner
    {
        /// <summary>
        /// Plan one cell. Returns the deterministic intentions (possibly
        /// empty: a Refuge cell with no compatible definition is a valid,
        /// legitimately quiet place).
        /// </summary>
        public static List<AmbientPopulationPlan> PlanCell(
            Vector2Int cell,
            int worldSeed,
            WorldProgression progression,
            AmbientPopulationConfig config)
        {
            var plans = new List<AmbientPopulationPlan>();
            if (progression == null || config == null || config.Definitions.Count == 0)
            {
                return plans;
            }

            var cellRandom = new System.Random(AmbientPopulationIds.CellSeed(worldSeed, cell));
            Vector3 discCenter = progression.RefugePosition;

            for (int anchorIndex = 0; anchorIndex < config.MaxPlansPerCell; anchorIndex++)
            {
                // Every anchor consumes a fixed number of draws so later
                // anchors stay stable regardless of earlier outcomes.
                var candidates = GenerateCandidates(cell, discCenter, config, cellRandom);
                double selectionRoll = cellRandom.NextDouble();
                double groupRoll = cellRandom.NextDouble();

                var (definition, hasDefinition) = SelectDefinition(candidates, progression, config, selectionRoll);
                if (!hasDefinition)
                {
                    continue;
                }

                int groupSize = definition.MinimumGroupSize +
                                (int)(groupRoll * (definition.MaximumGroupSize - definition.MinimumGroupSize + 1));
                groupSize = Mathf.Clamp(groupSize, definition.MinimumGroupSize, definition.MaximumGroupSize);

                plans.Add(new AmbientPopulationPlan(
                    AmbientPopulationIds.PlanId(cell, anchorIndex),
                    definition.PopulationId,
                    cell,
                    anchorIndex,
                    groupSize,
                    AmbientPopulationIds.PlanSeed(worldSeed, cell, anchorIndex),
                    candidates));
            }

            return plans;
        }

        /// <summary>
        /// Stable ordered candidate positions inside the cell. Several
        /// candidates per anchor: one unlucky NavMesh point must never
        /// condemn the whole intention.
        /// </summary>
        private static Vector3[] GenerateCandidates(
            Vector2Int cell, Vector3 discCenter, AmbientPopulationConfig config, System.Random cellRandom)
        {
            var candidates = new Vector3[config.CandidatesPerAnchor];
            Vector3 corner = AmbientPopulationCells.CellMinCorner(cell, discCenter, config.CellSize);

            for (int i = 0; i < candidates.Length; i++)
            {
                float x = corner.x + (float)cellRandom.NextDouble() * config.CellSize;
                float z = corner.z + (float)cellRandom.NextDouble() * config.CellSize;
                candidates[i] = new Vector3(x, 0f, z);
            }

            return candidates;
        }

        /// <summary>
        /// Weighted definition pick at the first candidate whose context
        /// admits at least one definition. Uses a pre-drawn roll so the draw
        /// count stays fixed per anchor.
        /// </summary>
        private static (AmbientPopulationDefinition definition, bool found) SelectDefinition(
            Vector3[] candidates,
            WorldProgression progression,
            AmbientPopulationConfig config,
            double selectionRoll)
        {
            foreach (var candidate in candidates)
            {
                var context = progression.GetContext(candidate);
                if (!context.IsInsideWorldDisc)
                {
                    continue;
                }

                double totalWeight = 0;
                var compatible = new List<(AmbientPopulationDefinition definition, float weight)>();
                foreach (var definition in config.Definitions)
                {
                    if (definition == null) continue;

                    if (WorldContentCompatibility.Evaluate(
                            context.Ring, context.Depth01,
                            definition.MinimumRing, definition.MaximumRing,
                            definition.SelectionWeight, definition.WeightByDepth,
                            out float weight, out _))
                    {
                        compatible.Add((definition, weight));
                        totalWeight += weight;
                    }
                }

                if (compatible.Count == 0)
                {
                    continue;
                }

                double roll = selectionRoll * totalWeight;
                double accumulated = 0;
                foreach (var (definition, weight) in compatible)
                {
                    accumulated += weight;
                    if (roll < accumulated)
                    {
                        return (definition, true);
                    }
                }

                return (compatible[compatible.Count - 1].definition, true);
            }

            return (null, false);
        }
    }
}

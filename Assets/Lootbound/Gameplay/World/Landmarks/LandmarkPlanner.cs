using System.Collections.Generic;
using UnityEngine;
using Lootbound.Gameplay.World.Layout;
using Lootbound.Gameplay.World.Progression;
using Lootbound.Gameplay.World.Spawning;

namespace Lootbound.Gameplay.World.Landmarks
{
    /// <summary>
    /// Turns a validated world layout into the deterministic set of
    /// LandmarkIdentity records - the single source of truth consumed by the
    /// LandmarkDirector and the ambient population as independent readers.
    ///
    /// Pure C#: no scene, no instantiation, no UnityEngine.Random, no mutable
    /// global state. Landmarks are born from the layout's elevated / terminal
    /// nodes (Viewpoint, Landmark, OuterDestination) - the very places seen
    /// from afar. Each host yields one landmark in V1 (slot 0). Definition
    /// selection is a weighted draw among ring-compatible definitions, seeded
    /// by FNV-1a over (worldSeed, LandmarkId) - the same recipe as the content
    /// planner, so it is stable across processes and iteration order.
    ///
    /// The work is split in two so terrain integration can seat the ground
    /// between them (see <see cref="LandmarkPlacement"/>):
    /// <see cref="PlanPlacements"/> decides everything layout-only (pre-stamp),
    /// then <see cref="Finalize"/> grounds each landmark on the final terrain.
    /// <see cref="Plan"/> is the shortcut for callers that do not stamp.
    /// </summary>
    public static class LandmarkPlanner
    {
        private const int V1Slot = 0;

        /// <summary>
        /// Plans the landmarks for a layout, grounding them on the terrain the
        /// sampler currently exposes. Returns an empty (never null) list when
        /// the layout or the registry is missing, or when no host has a
        /// compatible definition. Ordered deterministically by LandmarkId.
        /// </summary>
        public static IReadOnlyList<LandmarkIdentity> Plan(
            WorldLayoutContext layout,
            LandmarkRegistry registry,
            WorldProgression progression,
            ITerrainSampler sampler)
        {
            return Finalize(PlanPlacements(layout, registry, progression), sampler);
        }

        /// <summary>
        /// First half: everything decided from the layout alone (host XZ,
        /// ring/depth/difficulty, selected definition), with no ground height
        /// yet. Ordered deterministically by LandmarkId. Empty (never null)
        /// when the layout or registry is missing.
        /// </summary>
        public static IReadOnlyList<LandmarkPlacement> PlanPlacements(
            WorldLayoutContext layout,
            LandmarkRegistry registry,
            WorldProgression progression)
        {
            var result = new List<LandmarkPlacement>();
            if (layout == null || registry == null)
            {
                return result;
            }

            foreach (var host in CollectHostNodes(layout))
            {
                string landmarkId = MakeLandmarkId(layout.WorldSeed, host.NodeId, V1Slot);

                // Ring / depth / difficulty from the progression authority
                // (horizontal only - stable under stamping), falling back to
                // the host's own radial data if absent.
                WorldRing ring;
                float depth01;
                float difficulty01;
                if (progression != null)
                {
                    var ctx = progression.GetContext(host.Position);
                    ring = ctx.Ring;
                    depth01 = ctx.Depth01;
                    difficulty01 = ctx.Difficulty01;
                }
                else
                {
                    ring = host.Ring;
                    depth01 = Mathf.Clamp01(host.NormalizedWorldRadius);
                    difficulty01 = depth01;
                }

                var definition = SelectDefinition(registry, ring, depth01, layout.WorldSeed, landmarkId);
                if (definition == null)
                {
                    // No archetype fits this place: it stays an anonymous node.
                    continue;
                }

                result.Add(new LandmarkPlacement(
                    landmarkId,
                    definition,
                    host.Position.x,
                    host.Position.z,
                    ring,
                    depth01,
                    difficulty01,
                    host.RadialPathId,
                    host.NodeId,
                    V1Slot));
            }

            result.Sort((a, b) => string.CompareOrdinal(a.LandmarkId, b.LandmarkId));
            return result;
        }

        /// <summary>
        /// Second half: grounds each placement on the terrain the sampler
        /// exposes NOW (the stamped terrain, when terrain integration has run)
        /// and builds the immutable identities. Preserves the placements' order.
        /// </summary>
        public static IReadOnlyList<LandmarkIdentity> Finalize(
            IReadOnlyList<LandmarkPlacement> placements,
            ITerrainSampler sampler)
        {
            var result = new List<LandmarkIdentity>();
            if (placements == null || sampler == null)
            {
                return result;
            }

            foreach (var placement in placements)
            {
                // Ground on the final terrain, like every other placement in
                // the pipeline. When a stamp has seated the ground here, this
                // reads the seated height - so the landmark never floats or sinks.
                Vector3 position = new Vector3(
                    placement.X,
                    sampler.SampleHeight(placement.X, placement.Z),
                    placement.Z);

                result.Add(new LandmarkIdentity(
                    placement.LandmarkId,
                    placement.Definition.LandmarkId,
                    position,
                    placement.Ring,
                    placement.Depth01,
                    placement.Difficulty01,
                    placement.RadialPathId,
                    placement.HostNodeId,
                    placement.Slot,
                    placement.Definition.DiscoveryRadius));
            }

            return result;
        }

        /// <summary>Deterministic landmark id derived from seed, host node and slot.</summary>
        public static string MakeLandmarkId(int worldSeed, string hostNodeId, int slot)
        {
            return $"landmark_{worldSeed}_{hostNodeId}_{slot}";
        }

        /// <summary>
        /// Eligible host nodes: the world's elevated and terminal points -
        /// Viewpoints, Landmark-typed nodes and OuterDestinations. These are
        /// the places a traveller notices on the horizon.
        /// </summary>
        private static IEnumerable<WorldNode> CollectHostNodes(WorldLayoutContext layout)
        {
            var seen = new HashSet<string>();

            foreach (var node in layout.NodesOrdered)
            {
                if (node.Type == WorldNodeType.Viewpoint || node.Type == WorldNodeType.Landmark)
                {
                    if (seen.Add(node.NodeId))
                    {
                        yield return node;
                    }
                }
            }

            foreach (var node in layout.OuterDestinationNodes)
            {
                if (seen.Add(node.NodeId))
                {
                    yield return node;
                }
            }
        }

        /// <summary>
        /// Weighted definition pick among ring/depth-compatible definitions,
        /// seeded stably by the landmark's identity. Null when none fits.
        /// </summary>
        private static LandmarkDefinition SelectDefinition(
            LandmarkRegistry registry, WorldRing ring, float depth01, int worldSeed, string landmarkId)
        {
            var compatible = new List<(LandmarkDefinition definition, float weight)>();
            double totalWeight = 0;
            foreach (var definition in registry.Definitions)
            {
                if (definition != null &&
                    WorldContentCompatibility.Evaluate(definition, ring, depth01, out float weight, out _))
                {
                    compatible.Add((definition, weight));
                    totalWeight += weight;
                }
            }

            if (compatible.Count == 0)
            {
                return null;
            }

            var random = CreateLandmarkRandom(worldSeed, landmarkId);
            double roll = random.NextDouble() * totalWeight;
            double accumulated = 0;
            for (int i = 0; i < compatible.Count; i++)
            {
                accumulated += compatible[i].weight;
                if (roll < accumulated)
                {
                    return compatible[i].definition;
                }
            }

            return compatible[compatible.Count - 1].definition;
        }

        /// <summary>
        /// Deterministic per-landmark random source: FNV-1a over the world
        /// seed and the stable LandmarkId (identical recipe to the content
        /// planner - stable across processes and iteration order).
        /// </summary>
        private static System.Random CreateLandmarkRandom(int worldSeed, string landmarkId)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = (hash ^ (uint)worldSeed) * 16777619u;
                if (landmarkId != null)
                {
                    foreach (char c in landmarkId)
                    {
                        hash = (hash ^ c) * 16777619u;
                    }
                }
                return new System.Random((int)hash);
            }
        }
    }
}

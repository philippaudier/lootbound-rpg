using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lootbound.Gameplay.World.Layout
{
    /// <summary>
    /// Generates radial world layout from terrain data.
    ///
    /// The layout consists of RadialPaths emanating from a central Refuge position.
    /// Each path curves naturally following terrain while maintaining general outward progression.
    /// Curvature emerges from scoring (outward progression + terrain slope + curvature penalty).
    /// </summary>
    public static class WorldLayoutGenerator
    {
        // Prime number for deterministic hash combining
        private const int HASH_PRIME = 16777619;

        /// <summary>
        /// Generate a radial world layout centered on the Refuge.
        /// </summary>
        /// <param name="worldSeed">World seed for deterministic generation</param>
        /// <param name="worldDiscDefinition">Definition of the logical world disc</param>
        /// <param name="sampler">Terrain sampler for height/slope queries</param>
        /// <param name="config">Layout generation configuration</param>
        /// <returns>Generation result with layout context on success</returns>
        public static WorldLayoutResult Generate(
            int worldSeed,
            WorldDiscDefinition worldDiscDefinition,
            ITerrainSampler sampler,
            WorldLayoutConfig config)
        {
            if (worldDiscDefinition == null)
            {
                return WorldLayoutResult.Failed("WorldDiscDefinition is null");
            }

            if (sampler == null)
            {
                return WorldLayoutResult.Failed("Terrain sampler is null");
            }

            if (config == null)
            {
                return WorldLayoutResult.Failed("Layout config is null");
            }

            // Validate ring config early
            try
            {
                worldDiscDefinition.RingConfig.ValidateOrThrow();
            }
            catch (Exception e)
            {
                return WorldLayoutResult.Failed($"WorldRingConfig validation failed: {e.Message}");
            }

            // Try generation with deterministic retries
            for (int attempt = 0; attempt < config.MaxGenerationAttempts; attempt++)
            {
                int effectiveSeed = HashCombine(worldSeed, attempt);

                var result = TryGenerate(
                    worldSeed,
                    attempt,
                    effectiveSeed,
                    worldDiscDefinition,
                    sampler,
                    config
                );

                if (result.Success)
                {
                    Debug.Log($"[WorldLayoutGenerator] Radial layout generated successfully on attempt {attempt + 1}");
                    return result;
                }

                Debug.LogWarning($"[WorldLayoutGenerator] Attempt {attempt + 1} failed: {result.Error}");
            }

            return WorldLayoutResult.Failed($"Failed to generate valid radial layout after {config.MaxGenerationAttempts} attempts");
        }

        private static WorldLayoutResult TryGenerate(
            int worldSeed,
            int attempt,
            int effectiveSeed,
            WorldDiscDefinition worldDiscDefinition,
            ITerrainSampler sampler,
            WorldLayoutConfig config)
        {
            var random = new System.Random(effectiveSeed);

            // Phase 1: Determine Refuge position (near world center with bounded offset)
            var refugePosition = FindRefugePosition(random, sampler, config);

            // Create layout context
            var context = new WorldLayoutContext(
                worldSeed,
                attempt,
                effectiveSeed,
                worldDiscDefinition.WorldRadius,
                refugePosition,
                worldDiscDefinition.RingConfig
            );

            // Phase 2: Create Refuge node
            var refugeNode = CreateRefugeNode(effectiveSeed, refugePosition, sampler, config, worldDiscDefinition);
            context.AddNode(refugeNode);

            // Phase 3: Determine radial path count and distribute angles
            int pathCount = DeterminePathCount(random, config);
            var pathAngles = DistributePathAngles(pathCount, random, config);

            // Phase 4: Generate each radial path
            int nodeIndex = 1;
            int edgeIndex = 0;
            int pathIndex = 0;

            foreach (float startAngle in pathAngles)
            {
                var pathResult = GenerateRadialPath(
                    context,
                    random,
                    sampler,
                    config,
                    worldDiscDefinition,
                    effectiveSeed,
                    pathIndex,
                    startAngle,
                    refugeNode,
                    ref nodeIndex,
                    ref edgeIndex
                );

                if (!pathResult.success)
                {
                    return WorldLayoutResult.Failed(pathResult.error);
                }

                pathIndex++;
            }

            // Phase 5: Generate branches from path nodes
            GenerateBranches(context, random, sampler, config, worldDiscDefinition, effectiveSeed, ref nodeIndex, ref edgeIndex);

            // Phase 6: Create reservations
            GenerateReservations(context, random, config, worldDiscDefinition, sampler, effectiveSeed);

            // Phase 7: Structural validation
            var validationResult = WorldLayoutValidator.ValidateStructure(context);
            if (!validationResult.IsValid)
            {
                return WorldLayoutResult.Failed($"Structural validation failed: {validationResult.Error}");
            }

            // Phase 8: Primary path traversability validation
            var traversabilityResult = WorldLayoutValidator.ValidatePrimaryPathTraversability(context, config.PrimaryPathMaxSlope);
            if (!traversabilityResult.IsValid)
            {
                return WorldLayoutResult.Failed($"Traversability validation failed: {traversabilityResult.Error}");
            }

            return WorldLayoutResult.Succeeded(context);
        }

        #region Refuge Positioning

        private static Vector3 FindRefugePosition(
            System.Random random,
            ITerrainSampler sampler,
            WorldLayoutConfig config)
        {
            // Region centre in world coordinates (refuge sits here, then a
            // bounded random offset). Read from the sampler so the refuge is
            // placed relative to the region rather than a corner origin.
            Vector3 regionCenter = sampler.WorldCenter;
            float worldCenterX = regionCenter.x;
            float worldCenterZ = regionCenter.z;

            // Apply bounded random offset
            float maxOffset = config.RefugeMaxCenterOffset;
            float offsetAngle = (float)(random.NextDouble() * Mathf.PI * 2f);
            float offsetDist = (float)(random.NextDouble() * maxOffset);

            float refugeX = worldCenterX + Mathf.Cos(offsetAngle) * offsetDist;
            float refugeZ = worldCenterZ + Mathf.Sin(offsetAngle) * offsetDist;

            // Sample terrain height at refuge position
            float refugeHeight = sampler.SampleHeight(refugeX, refugeZ);

            return new Vector3(refugeX, refugeHeight, refugeZ);
        }

        private static WorldNode CreateRefugeNode(
            int seed,
            Vector3 position,
            ITerrainSampler sampler,
            WorldLayoutConfig config,
            WorldDiscDefinition worldDiscDefinition)
        {
            float height = sampler.SampleHeight(position.x, position.z);
            float slope = sampler.SampleSlope(position.x, position.z);

            // Refuge is at center, so distance = 0, normalized radius = 0, ring = Refuge
            return new WorldNode(
                WorldNode.GenerateId(seed, WorldNodeType.Refuge, 0),
                WorldNodeType.Refuge,
                new Vector3(position.x, height, position.z),
                config.RefugeRadius,
                distanceFromRefuge: 0f,
                normalizedWorldRadius: 0f,
                WorldRing.Refuge,
                radialPathId: null,  // Refuge doesn't belong to any single path
                pathStepIndex: -1,   // Refuge has no step index
                height,
                slope
            );
        }

        #endregion

        #region Path Distribution

        private static int DeterminePathCount(System.Random random, WorldLayoutConfig config)
        {
            int min = config.MinimumRadialPathCount;
            int max = config.MaximumRadialPathCount;
            return min + random.Next(max - min + 1);
        }

        private static List<float> DistributePathAngles(int pathCount, System.Random random, WorldLayoutConfig config)
        {
            var angles = new List<float>();

            if (pathCount <= 0) return angles;

            // Start with evenly distributed angles, then apply small variation
            float baseSpacing = 360f / pathCount;

            for (int i = 0; i < pathCount; i++)
            {
                // Base angle (evenly distributed)
                float baseAngle = i * baseSpacing;

                // Apply small random variation (max ±10% of spacing)
                float maxVariation = baseSpacing * 0.1f;
                float variation = ((float)random.NextDouble() - 0.5f) * 2f * maxVariation;

                float angle = baseAngle + variation;

                // Normalize to [0, 360)
                angle = ((angle % 360f) + 360f) % 360f;

                angles.Add(angle);
            }

            // Validate minimum angular separation
            angles.Sort();
            ValidateAngularSeparation(angles, config.MinimumAngularSeparation, baseSpacing);

            return angles;
        }

        private static void ValidateAngularSeparation(List<float> angles, float minSeparation, float baseSpacing)
        {
            // Simple validation - if any pair is too close, revert to even distribution
            for (int i = 0; i < angles.Count; i++)
            {
                int nextIndex = (i + 1) % angles.Count;
                float diff = angles[nextIndex] - angles[i];
                if (nextIndex == 0) diff += 360f; // Wrap around

                if (diff < minSeparation)
                {
                    // Revert to even distribution
                    for (int j = 0; j < angles.Count; j++)
                    {
                        angles[j] = j * baseSpacing;
                    }
                    return;
                }
            }
        }

        #endregion

        #region Radial Path Generation

        private static (bool success, string error) GenerateRadialPath(
            WorldLayoutContext context,
            System.Random random,
            ITerrainSampler sampler,
            WorldLayoutConfig config,
            WorldDiscDefinition worldDiscDefinition,
            int seed,
            int pathIndex,
            float startAngle,
            WorldNode refugeNode,
            ref int nodeIndex,
            ref int edgeIndex)
        {
            string pathId = RadialPath.GenerateId(seed, pathIndex);
            var radialPath = new RadialPath(pathId, startAngle);

            // First node after Refuge (doesn't add Refuge to path's node list)
            var currentNode = refugeNode;
            Vector2 currentDirection = AngleToDirection(startAngle);
            Vector3 refugePos = refugeNode.Position;

            for (int step = 0; step < config.NodesPerRadialPath; step++)
            {
                bool isLastNode = (step == config.NodesPerRadialPath - 1);
                var nodeType = isLastNode ? WorldNodeType.OuterDestination : WorldNodeType.Junction;

                // Find best candidate position
                var candidate = FindBestRadialCandidate(
                    currentNode.Position,
                    currentDirection,
                    refugePos,
                    context,
                    random,
                    sampler,
                    config,
                    worldDiscDefinition,
                    isPrimaryPath: true
                );

                if (candidate == null)
                {
                    return (false, $"Could not find valid candidate for path {pathIndex} step {step}");
                }

                // Calculate radial properties
                float distanceFromRefuge = Vector2.Distance(
                    new Vector2(candidate.Value.position.x, candidate.Value.position.z),
                    new Vector2(refugePos.x, refugePos.z)
                );
                float normalizedWorldRadius = distanceFromRefuge / worldDiscDefinition.WorldRadius;
                WorldRing ring = worldDiscDefinition.RingConfig.GetRingAt(normalizedWorldRadius);

                // Create node
                var newNode = new WorldNode(
                    WorldNode.GenerateId(seed, nodeType, nodeIndex++),
                    nodeType,
                    candidate.Value.position,
                    config.GetRadiusForType(nodeType),
                    distanceFromRefuge,
                    normalizedWorldRadius,
                    ring,
                    pathId,
                    pathStepIndex: step,
                    candidate.Value.height,
                    candidate.Value.slope
                );
                context.AddNode(newNode);
                radialPath.AddNode(newNode.NodeId);

                if (isLastNode)
                {
                    radialPath.SetOuterDestination(newNode.NodeId);
                }

                // Create edge with control points
                var edge = CreateEdge(
                    seed,
                    edgeIndex++,
                    currentNode,
                    newNode,
                    pathId,
                    isPrimaryPathEdge: true,
                    sampler,
                    config
                );

                if (!edge.IsTraversable)
                {
                    return (false, $"Path {pathIndex} edge from {currentNode.NodeId} to {newNode.NodeId} is not traversable");
                }

                context.AddEdge(edge);
                radialPath.AddEdge(edge.EdgeId);

                // Update direction for next step (emergent curvature)
                Vector2 newPos = new Vector2(newNode.Position.x, newNode.Position.z);
                Vector2 oldPos = new Vector2(currentNode.Position.x, currentNode.Position.z);
                Vector2 moveDir = (newPos - oldPos).normalized;

                // Blend current direction with actual move direction for smooth curves
                currentDirection = Vector2.Lerp(currentDirection, moveDir, 0.4f).normalized;

                currentNode = newNode;
            }

            context.AddRadialPath(radialPath);
            return (true, null);
        }

        private static Vector2 AngleToDirection(float angleDegrees)
        {
            float rad = angleDegrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        }

        #endregion

        #region Branch Generation

        private static void GenerateBranches(
            WorldLayoutContext context,
            System.Random random,
            ITerrainSampler sampler,
            WorldLayoutConfig config,
            WorldDiscDefinition worldDiscDefinition,
            int seed,
            ref int nodeIndex,
            ref int edgeIndex)
        {
            // Collect branch anchor candidates from all radial paths
            var branchCandidates = new List<WorldNode>();

            foreach (var path in context.RadialPaths)
            {
                // Skip first and last node of each path (near refuge and OuterDestination)
                for (int i = 1; i < path.NodeIds.Count - 1; i++)
                {
                    string nodeId = path.NodeIds[i];
                    if (context.NodesById.TryGetValue(nodeId, out var node))
                    {
                        branchCandidates.Add(node);
                    }
                }
            }

            // Shuffle candidates deterministically
            for (int i = branchCandidates.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (branchCandidates[i], branchCandidates[j]) = (branchCandidates[j], branchCandidates[i]);
            }

            int branchesCreated = 0;

            foreach (var anchor in branchCandidates)
            {
                if (branchesCreated >= config.BranchCount) break;

                // Check branch chance
                if (random.NextDouble() > config.BranchChance) continue;

                // Branch inherits RadialPathId from anchor
                string branchRadialPathId = anchor.RadialPathId;

                // Determine branch direction (roughly perpendicular to radial direction)
                Vector2 branchDir = GetBranchDirection(anchor, context, random);

                var currentBranchNode = anchor;
                int branchLength = random.Next(1, config.BranchMaxNodes + 1);

                for (int branchStep = 0; branchStep < branchLength; branchStep++)
                {
                    bool isTerminal = (branchStep == branchLength - 1);

                    var candidate = FindBestRadialCandidate(
                        currentBranchNode.Position,
                        branchDir,
                        context.RefugePosition,
                        context,
                        random,
                        sampler,
                        config,
                        worldDiscDefinition,
                        isPrimaryPath: false
                    );

                    if (candidate == null) break;

                    // Calculate radial properties for branch node
                    float distanceFromRefuge = Vector2.Distance(
                        new Vector2(candidate.Value.position.x, candidate.Value.position.z),
                        new Vector2(context.RefugePosition.x, context.RefugePosition.z)
                    );
                    float normalizedWorldRadius = distanceFromRefuge / worldDiscDefinition.WorldRadius;
                    WorldRing ring = worldDiscDefinition.RingConfig.GetRingAt(normalizedWorldRadius);

                    // Determine branch node type based on terrain
                    var branchNodeType = DetermineBranchNodeType(
                        candidate.Value,
                        isTerminal,
                        sampler,
                        config
                    );

                    var branchNode = new WorldNode(
                        WorldNode.GenerateId(seed, branchNodeType, nodeIndex++),
                        branchNodeType,
                        candidate.Value.position,
                        config.GetRadiusForType(branchNodeType),
                        distanceFromRefuge,
                        normalizedWorldRadius,
                        ring,
                        branchRadialPathId,  // Inherit from anchor
                        pathStepIndex: -1,   // Branches have no step index
                        candidate.Value.height,
                        candidate.Value.slope
                    );
                    context.AddNode(branchNode);

                    var edge = CreateEdge(
                        seed,
                        edgeIndex++,
                        currentBranchNode,
                        branchNode,
                        branchRadialPathId,
                        isPrimaryPathEdge: false,
                        sampler,
                        config
                    );
                    context.AddEdge(edge);

                    currentBranchNode = branchNode;

                    // Rotate branch direction slightly for natural curves
                    float rotAngle = ((float)random.NextDouble() - 0.5f) * 60f * Mathf.Deg2Rad;
                    float cos = Mathf.Cos(rotAngle);
                    float sin = Mathf.Sin(rotAngle);
                    branchDir = new Vector2(
                        branchDir.x * cos - branchDir.y * sin,
                        branchDir.x * sin + branchDir.y * cos
                    ).normalized;
                }

                branchesCreated++;
            }
        }

        private static Vector2 GetBranchDirection(WorldNode anchor, WorldLayoutContext context, System.Random random)
        {
            // Calculate radial direction from refuge to anchor
            Vector2 anchorPos = new Vector2(anchor.Position.x, anchor.Position.z);
            Vector2 refugePos = new Vector2(context.RefugePosition.x, context.RefugePosition.z);
            Vector2 radialDir = (anchorPos - refugePos).normalized;

            // Perpendicular direction (randomly left or right)
            Vector2 perp;
            if (random.NextDouble() > 0.5)
            {
                perp = new Vector2(-radialDir.y, radialDir.x);
            }
            else
            {
                perp = new Vector2(radialDir.y, -radialDir.x);
            }

            return perp;
        }

        #endregion

        #region Reservation Generation

        private static void GenerateReservations(
            WorldLayoutContext context,
            System.Random random,
            WorldLayoutConfig config,
            WorldDiscDefinition worldDiscDefinition,
            ITerrainSampler sampler,
            int seed)
        {
            // Collect eligible nodes for reservations
            var eligibleNodes = new List<WorldNode>();
            foreach (var node in context.NodesOrdered)
            {
                if (node.Type == WorldNodeType.Clearing ||
                    node.Type == WorldNodeType.Junction ||
                    node.Type == WorldNodeType.DeadEnd)
                {
                    eligibleNodes.Add(node);
                }
            }

            // Shuffle for random selection
            for (int i = eligibleNodes.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (eligibleNodes[i], eligibleNodes[j]) = (eligibleNodes[j], eligibleNodes[i]);
            }

            // Create encounter reservations
            int encounterIndex = 0;
            for (int i = 0; i < Mathf.Min(config.EncounterReservationCount, eligibleNodes.Count); i++)
            {
                var host = eligibleNodes[i];

                // Offset XZ slightly from node center, then sample the height
                // at the final XZ (the host height is only valid at the host XZ)
                float offsetAngle = (float)(random.NextDouble() * Mathf.PI * 2f);
                float offsetDist = host.Radius * 0.3f;
                float posX = host.Position.x + Mathf.Cos(offsetAngle) * offsetDist;
                float posZ = host.Position.z + Mathf.Sin(offsetAngle) * offsetDist;
                Vector3 pos = new Vector3(posX, sampler.SampleHeight(posX, posZ), posZ);

                var reservation = new EncounterReservation(
                    EncounterReservation.GenerateId(seed, encounterIndex++),
                    host.NodeId,
                    pos,
                    host.Radius * 0.5f,
                    host.DistanceFromRefuge,
                    host.NormalizedWorldRadius,
                    host.Ring,
                    host.RadialPathId
                );
                context.AddEncounterReservation(reservation);
            }

            // Create resource reservations (prefer dead ends and viewpoints)
            var resourceNodes = new List<WorldNode>();
            foreach (var node in context.NodesOrdered)
            {
                if (node.Type == WorldNodeType.DeadEnd || node.Type == WorldNodeType.Viewpoint)
                {
                    resourceNodes.Add(node);
                }
            }

            // Add remaining eligible nodes if needed
            foreach (var node in eligibleNodes)
            {
                if (!resourceNodes.Contains(node))
                {
                    resourceNodes.Add(node);
                }
            }

            int resourceIndex = 0;
            for (int i = 0; i < Mathf.Min(config.ResourceReservationCount, resourceNodes.Count); i++)
            {
                var host = resourceNodes[i];

                float offsetAngle = (float)(random.NextDouble() * Mathf.PI * 2f);
                float offsetDist = host.Radius * 0.4f;
                float posX = host.Position.x + Mathf.Cos(offsetAngle) * offsetDist;
                float posZ = host.Position.z + Mathf.Sin(offsetAngle) * offsetDist;
                Vector3 pos = new Vector3(posX, sampler.SampleHeight(posX, posZ), posZ);

                var reservation = new ResourceReservation(
                    ResourceReservation.GenerateId(seed, resourceIndex++),
                    host.NodeId,
                    pos,
                    host.Radius * 0.3f,
                    host.DistanceFromRefuge,
                    host.NormalizedWorldRadius,
                    host.Ring,
                    host.RadialPathId
                );
                context.AddResourceReservation(reservation);
            }

            // Landmarks are no longer reservations: since slice 0.9.10 they
            // are a first-class World system. The LandmarkPlanner derives them
            // directly from the layout's elevated / terminal nodes after the
            // layout is published (see ProceduralTerrainGenerator).
        }

        /// <summary>
        /// Re-sample every reservation height from the authoritative sampler.
        /// Must be called after any pass that modifies the heightmap after
        /// layout generation (corridor/clearing flattening) so stored
        /// reservation positions match the final published terrain.
        /// XZ positions are never changed.
        /// </summary>
        public static void ReprojectReservationHeights(WorldLayoutContext layout, ITerrainSampler sampler)
        {
            if (layout == null || sampler == null) return;

            foreach (var reservation in layout.EncounterReservations)
            {
                reservation.ReprojectHeight(sampler.SampleHeight(reservation.Position.x, reservation.Position.z));
            }

            foreach (var reservation in layout.ResourceReservations)
            {
                reservation.ReprojectHeight(sampler.SampleHeight(reservation.Position.x, reservation.Position.z));
            }
        }

        #endregion

        #region Candidate Evaluation

        private struct CandidatePosition
        {
            public Vector3 position;
            public float height;
            public float slope;
            public float score;
        }

        private static CandidatePosition? FindBestRadialCandidate(
            Vector3 fromPosition,
            Vector2 preferredDirection,
            Vector3 refugePosition,
            WorldLayoutContext context,
            System.Random random,
            ITerrainSampler sampler,
            WorldLayoutConfig config,
            WorldDiscDefinition worldDiscDefinition,
            bool isPrimaryPath)
        {
            float stepMin = isPrimaryPath ? config.RadialStepMin : config.RadialStepMin * 0.6f;
            float stepMax = isPrimaryPath ? config.RadialStepMax : config.RadialStepMax * 0.6f;

            var candidates = CollectRadialCandidates(
                fromPosition, preferredDirection, refugePosition, context, random,
                sampler, config, worldDiscDefinition, isPrimaryPath,
                stepMin, stepMax, angleSpreadDegrees: 90f);

            // Near the world margin the whole forward cone can be out of
            // bounds (flat terrain favours straight max-length steps, so paths
            // reach the edge). Retry once with shorter steps and a half-circle
            // spread so the path can curve along the edge instead of failing.
            // Primary paths only: extra random draws here never affect seeds
            // that used to succeed, because this branch used to abort the
            // whole attempt.
            if (candidates.Count == 0 && isPrimaryPath)
            {
                float fallbackMin = Mathf.Max(config.NodeMinSpacing * 1.2f, stepMin * 0.5f);
                float fallbackMax = Mathf.Max(fallbackMin + 1f, stepMin);
                candidates = CollectRadialCandidates(
                    fromPosition, preferredDirection, refugePosition, context, random,
                    sampler, config, worldDiscDefinition, isPrimaryPath,
                    fallbackMin, fallbackMax, angleSpreadDegrees: 180f);
            }

            if (candidates.Count == 0)
            {
                return null;
            }

            // Sort by score and return best
            candidates.Sort((a, b) => b.score.CompareTo(a.score));
            return candidates[0];
        }

        private static List<CandidatePosition> CollectRadialCandidates(
            Vector3 fromPosition,
            Vector2 preferredDirection,
            Vector3 refugePosition,
            WorldLayoutContext context,
            System.Random random,
            ITerrainSampler sampler,
            WorldLayoutConfig config,
            WorldDiscDefinition worldDiscDefinition,
            bool isPrimaryPath,
            float stepMin,
            float stepMax,
            float angleSpreadDegrees)
        {
            var candidates = new List<CandidatePosition>();
            float maxSlope = config.PrimaryPathMaxSlope;

            for (int i = 0; i < config.CandidatesPerStep; i++)
            {
                // Generate candidate in forward cone
                float angleSpread = angleSpreadDegrees * Mathf.Deg2Rad;
                float angle = Mathf.Atan2(preferredDirection.y, preferredDirection.x);
                angle += ((float)random.NextDouble() - 0.5f) * angleSpread;

                float distance = stepMin + (float)random.NextDouble() * (stepMax - stepMin);

                float candidateX = fromPosition.x + Mathf.Cos(angle) * distance;
                float candidateZ = fromPosition.z + Mathf.Sin(angle) * distance;

                // Check bounds - keep candidates a margin away from the region edges.
                float margin = config.NodeMinSpacing;
                float halfExtent = sampler.WorldSize * 0.5f;
                float minEdgeX = sampler.WorldCenter.x - halfExtent;
                float maxEdgeX = sampler.WorldCenter.x + halfExtent;
                float minEdgeZ = sampler.WorldCenter.z - halfExtent;
                float maxEdgeZ = sampler.WorldCenter.z + halfExtent;
                if (!sampler.IsWithinBounds(candidateX, candidateZ) ||
                    candidateX < minEdgeX + margin || candidateX > maxEdgeX - margin ||
                    candidateZ < minEdgeZ + margin || candidateZ > maxEdgeZ - margin)
                {
                    continue;
                }

                // Check spacing from existing nodes
                bool tooClose = false;
                foreach (var node in context.NodesOrdered)
                {
                    float dist = Vector2.Distance(
                        new Vector2(candidateX, candidateZ),
                        new Vector2(node.Position.x, node.Position.z)
                    );
                    if (dist < config.NodeMinSpacing)
                    {
                        tooClose = true;
                        break;
                    }
                }
                if (tooClose) continue;

                // Sample terrain
                float height = sampler.SampleHeight(candidateX, candidateZ);
                float slope = sampler.SampleSlope(candidateX, candidateZ);

                // Check slope at candidate position
                if (slope > maxSlope * 1.2f) continue;

                // Check edge traversability for primary paths
                if (isPrimaryPath)
                {
                    if (WorldLayoutValidator.WouldExceedCorrectionLimits(
                        sampler,
                        fromPosition,
                        new Vector3(candidateX, height, candidateZ),
                        maxSlope,
                        config.MaxCorrectionStrength,
                        config.EdgeSamplePoints))
                    {
                        continue;
                    }
                }

                // Score candidate using radial scoring
                Vector3 candidatePos = new Vector3(candidateX, height, candidateZ);
                float score = ScoreRadialCandidate(
                    fromPosition,
                    candidatePos,
                    preferredDirection,
                    refugePosition,
                    slope,
                    config,
                    worldDiscDefinition,
                    sampler,
                    isPrimaryPath
                );

                candidates.Add(new CandidatePosition
                {
                    position = candidatePos,
                    height = height,
                    slope = slope,
                    score = score
                });
            }

            return candidates;
        }

        private static float ScoreRadialCandidate(
            Vector3 from,
            Vector3 candidate,
            Vector2 preferredDirection,
            Vector3 refugePosition,
            float slope,
            WorldLayoutConfig config,
            WorldDiscDefinition worldDiscDefinition,
            ITerrainSampler sampler,
            bool isPrimaryPath)
        {
            float score = 0f;

            // === Outward Progression Score ===
            // Reward moving away from refuge (normalized)
            float fromDistanceToRefuge = Vector2.Distance(
                new Vector2(from.x, from.z),
                new Vector2(refugePosition.x, refugePosition.z)
            );
            float candidateDistanceToRefuge = Vector2.Distance(
                new Vector2(candidate.x, candidate.z),
                new Vector2(refugePosition.x, refugePosition.z)
            );
            float outwardProgress = (candidateDistanceToRefuge - fromDistanceToRefuge) / config.RadialStepMax;
            outwardProgress = Mathf.Clamp(outwardProgress, -1f, 1f);
            score += outwardProgress * config.OutwardProgressionWeight;

            // === Terrain Slope Score ===
            // Lower slope is better
            float slopeScore = 1f - Mathf.Clamp01(slope / config.PrimaryPathMaxSlope);
            score += slopeScore * config.TerrainSlopeWeight;

            // === Curvature Penalty Score ===
            // Penalize sharp turns from preferred direction
            Vector2 moveDir = new Vector2(candidate.x - from.x, candidate.z - from.z).normalized;
            float directionAlignment = Vector2.Dot(moveDir, preferredDirection);
            // directionAlignment is -1 to 1, convert to 0 to 1 where 1 is aligned
            float curvatureScore = (directionAlignment + 1f) * 0.5f;
            // Apply as penalty (lower curvature penalty weight = more curvature allowed)
            score += curvatureScore * config.CurvaturePenaltyWeight;

            // === Edge Penalties ===
            if (isPrimaryPath)
            {
                float margin = sampler.WorldSize * 0.1f;
                float halfExtent = sampler.WorldSize * 0.5f;
                float cX = sampler.WorldCenter.x;
                float cZ = sampler.WorldCenter.z;
                float edgeDist = Mathf.Min(
                    candidate.x - (cX - halfExtent), candidate.z - (cZ - halfExtent),
                    (cX + halfExtent) - candidate.x, (cZ + halfExtent) - candidate.z
                );
                if (edgeDist < margin)
                {
                    // Penalize being near terrain edge
                    score -= (1f - edgeDist / margin) * 20f;
                }
            }

            // === Small Deterministic Variation ===
            // Prevents overly predictable paths while remaining deterministic
            int posHash = (int)(candidate.x * 1000 + candidate.z);
            float variation = (float)(new System.Random(posHash).NextDouble()) * 3f;
            score += variation;

            return score;
        }

        #endregion

        #region Edge Creation

        private static WorldEdge CreateEdge(
            int seed,
            int edgeIndex,
            WorldNode nodeA,
            WorldNode nodeB,
            string radialPathId,
            bool isPrimaryPathEdge,
            ITerrainSampler sampler,
            WorldLayoutConfig config)
        {
            // Create control points along the edge
            var controlPoints = new List<Vector3>();
            int numPoints = config.EdgeSamplePoints;

            float totalSlope = 0f;
            float maxSlope = 0f;
            int slopeExceedCount = 0;

            for (int i = 0; i <= numPoints; i++)
            {
                float t = i / (float)numPoints;
                float x = Mathf.Lerp(nodeA.Position.x, nodeB.Position.x, t);
                float z = Mathf.Lerp(nodeA.Position.z, nodeB.Position.z, t);
                float height = sampler.SampleHeight(x, z);
                float slope = sampler.SampleSlope(x, z);

                controlPoints.Add(new Vector3(x, height, z));

                totalSlope += slope;
                if (slope > maxSlope) maxSlope = slope;

                // Count how many points exceed the slope limit
                if (slope > config.PrimaryPathMaxSlope)
                {
                    slopeExceedCount++;
                }
            }

            float avgSlope = totalSlope / (numPoints + 1);
            float length = Vector3.Distance(nodeA.Position, nodeB.Position);

            // Traversability determination
            bool isTraversable;
            if (isPrimaryPathEdge)
            {
                float exceedRatio = slopeExceedCount / (float)(numPoints + 1);
                // Traversable if average slope is acceptable AND fewer than 40% of points exceed limit
                // AND max slope doesn't exceed limit by more than 50%
                isTraversable = avgSlope <= config.PrimaryPathMaxSlope * 1.1f &&
                               exceedRatio < 0.4f &&
                               maxSlope < config.PrimaryPathMaxSlope * 1.5f;
            }
            else
            {
                // Branch edges are more lenient
                isTraversable = avgSlope <= config.PrimaryPathMaxSlope * 1.3f;
            }

            return new WorldEdge(
                WorldEdge.GenerateId(seed, GetNodeIndex(nodeA.NodeId), GetNodeIndex(nodeB.NodeId)),
                nodeA.NodeId,
                nodeB.NodeId,
                length,
                radialPathId,
                isPrimaryPathEdge,
                controlPoints,
                avgSlope,
                maxSlope,
                isTraversable
            );
        }

        private static int GetNodeIndex(string nodeId)
        {
            // Extract index from node ID format: "node_{seed}_{type}_{index}"
            var parts = nodeId.Split('_');
            if (parts.Length >= 4 && int.TryParse(parts[parts.Length - 1], out int index))
            {
                return index;
            }
            return 0;
        }

        #endregion

        #region Node Type Determination

        private static WorldNodeType DetermineBranchNodeType(
            CandidatePosition candidate,
            bool isTerminal,
            ITerrainSampler sampler,
            WorldLayoutConfig config)
        {
            // Check if elevated compared to surroundings (viewpoint)
            float centerHeight = candidate.height;
            float avgSurroundingHeight = 0f;
            int samples = 0;
            float checkRadius = 20f;

            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI * 0.25f;
                float checkX = candidate.position.x + Mathf.Cos(angle) * checkRadius;
                float checkZ = candidate.position.z + Mathf.Sin(angle) * checkRadius;

                if (sampler.IsWithinBounds(checkX, checkZ))
                {
                    avgSurroundingHeight += sampler.SampleHeight(checkX, checkZ);
                    samples++;
                }
            }

            if (samples > 0)
            {
                avgSurroundingHeight /= samples;

                // Significantly elevated = viewpoint
                if (centerHeight > avgSurroundingHeight + sampler.TerrainHeight * 0.05f)
                {
                    return WorldNodeType.Viewpoint;
                }
            }

            // Low slope = clearing
            if (candidate.slope < config.PrimaryPathMaxSlope * 0.4f)
            {
                return WorldNodeType.Clearing;
            }

            // Terminal node = dead end
            if (isTerminal)
            {
                return WorldNodeType.DeadEnd;
            }

            return WorldNodeType.Junction;
        }

        #endregion

        #region Utility

        /// <summary>
        /// Combine two values into a deterministic hash.
        /// </summary>
        private static int HashCombine(int a, int b)
        {
            unchecked
            {
                return (a * HASH_PRIME) ^ b;
            }
        }

        #endregion
    }
}

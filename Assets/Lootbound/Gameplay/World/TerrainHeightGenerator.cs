using UnityEngine;
using Lootbound.World.Coordinates;
using Lootbound.World.Layers.Fields;
using Lootbound.Gameplay.World.Providers;

namespace Lootbound.Gameplay.World
{
    /// <summary>
    /// Materializes a finite grid of the terrain by SAMPLING the World Engine's
    /// HeightField (since slice T2). The height is no longer produced here and no
    /// longer normalized world-wide: the World Field is the truth, this class is
    /// just one consumer that fills a grid for the current monolithic terrain.
    /// </summary>
    public static class TerrainHeightGenerator
    {
        /// <summary>
        /// Fill the context's height/macro/slope grids by sampling the world.
        /// </summary>
        public static void Generate(TerrainGenerationContext context, TerrainGenerationConfig config)
        {
            var offsets = new NoiseOffsets(context.Seed);
            var heightField = WorldFieldComposer.BuildHeightField(config, offsets);

            int resolution = context.Resolution;
            float worldSize = context.WorldSize;

            float[,] heightMap = new float[resolution, resolution];
            float[,] macroMap = new float[resolution, resolution];

            for (int x = 0; x < resolution; x++)
            {
                for (int z = 0; z < resolution; z++)
                {
                    // Convert to world coordinates (float, exactly as before).
                    float worldX = (x / (float)(resolution - 1)) * worldSize;
                    float worldZ = (z / (float)(resolution - 1)) * worldSize;

                    // The terrain now SAMPLES the World Engine's HeightField.
                    heightMap[x, z] = heightField.Evaluate(new WorldCoordinate(worldX, worldZ));
                    macroMap[x, z] = TerrainNoiseCore.EvaluateMacro(worldX, worldZ, offsets, config);
                }
            }

            context.SetHeightMap(heightMap);
            context.SetMacroMap(macroMap);

            // Global normalization is gone. The relief is defined ONLY by the
            // noise parameters, the HeightRemap curve and the generation curves -
            // no pass depends on the world's min/max any more. The normalized map
            // is simply the field's output.
            float[,] normalized = new float[resolution, resolution];
            System.Array.Copy(heightMap, normalized, heightMap.Length);
            context.SetNormalizedHeightMap(normalized);

            // Compute slope map
            ComputeSlopeMap(context);
        }

        /// <summary>
        /// Compute the slope map (degrees) from the current NormalizedHeightMap.
        /// Public so terrain-modifying passes outside this class (e.g. landmark
        /// seating) can refresh slopes after writing the heightmap.
        /// </summary>
        public static void ComputeSlopeMap(TerrainGenerationContext context)
        {
            int resolution = context.Resolution;
            float[,] slopeMap = new float[resolution, resolution];
            float[,] heightMap = context.NormalizedHeightMap;

            // Scale factor for gradient calculation
            float cellSize = context.WorldSize / (resolution - 1);
            float heightScale = context.TerrainHeight;

            for (int x = 0; x < resolution; x++)
            {
                for (int z = 0; z < resolution; z++)
                {
                    // Sample neighboring heights for gradient
                    int xm = Mathf.Max(0, x - 1);
                    int xp = Mathf.Min(resolution - 1, x + 1);
                    int zm = Mathf.Max(0, z - 1);
                    int zp = Mathf.Min(resolution - 1, z + 1);

                    float hL = heightMap[xm, z] * heightScale;
                    float hR = heightMap[xp, z] * heightScale;
                    float hD = heightMap[x, zm] * heightScale;
                    float hU = heightMap[x, zp] * heightScale;

                    // Compute gradient
                    float dx = (hR - hL) / (cellSize * (xp - xm));
                    float dz = (hU - hD) / (cellSize * (zp - zm));

                    // Slope angle in degrees
                    float gradient = Mathf.Sqrt(dx * dx + dz * dz);
                    float slopeAngle = Mathf.Atan(gradient) * Mathf.Rad2Deg;

                    slopeMap[x, z] = slopeAngle;
                }
            }

            context.SetSlopeMap(slopeMap);
        }

        /// <summary>
        /// Apply spawn zone flattening to the heightmap.
        /// </summary>
        public static void ApplySpawnFlattening(TerrainGenerationContext context, TerrainGenerationConfig config, Vector3 spawnWorldPos)
        {
            int resolution = context.Resolution;
            float[,] heightMap = context.NormalizedHeightMap;

            float safeRadius = config.SpawnSafeRadius;
            float blendRadius = config.SpawnBlendRadius;
            float targetHeight = config.SpawnTargetHeight;

            // Sample current height at spawn to use as local target
            var (spawnX, spawnZ) = context.WorldToHeightmap(spawnWorldPos);
            float localTargetHeight = heightMap[spawnX, spawnZ];

            // Blend between local height and config target for more natural result
            float blendedTarget = Mathf.Lerp(localTargetHeight, targetHeight, 0.6f);

            for (int x = 0; x < resolution; x++)
            {
                for (int z = 0; z < resolution; z++)
                {
                    float normX = x / (float)(resolution - 1);
                    float normZ = z / (float)(resolution - 1);

                    float worldX = normX * context.WorldSize;
                    float worldZ = normZ * context.WorldSize;

                    float distanceToSpawn = Vector2.Distance(
                        new Vector2(worldX, worldZ),
                        new Vector2(spawnWorldPos.x, spawnWorldPos.z)
                    );

                    if (distanceToSpawn < blendRadius)
                    {
                        float originalHeight = heightMap[x, z];
                        float flattenedHeight;

                        if (distanceToSpawn < safeRadius)
                        {
                            // Core safe zone - mostly flat with slight variation
                            float microVariation = (Mathf.PerlinNoise(x * 0.1f, z * 0.1f) - 0.5f) * 0.005f;
                            flattenedHeight = blendedTarget + microVariation;
                        }
                        else
                        {
                            // Blend zone - smooth transition
                            float t = (distanceToSpawn - safeRadius) / (blendRadius - safeRadius);
                            // Use smooth step for natural transition
                            t = t * t * (3f - 2f * t);
                            flattenedHeight = Mathf.Lerp(blendedTarget, originalHeight, t);
                        }

                        heightMap[x, z] = flattenedHeight;
                    }
                }
            }

            // Recompute slope map after flattening
            ComputeSlopeMap(context);
        }

        /// <summary>
        /// Apply layout-aware flattening for clearings and path corridors.
        /// </summary>
        public static void ApplyLayoutFlattening(
            TerrainGenerationContext context,
            TerrainGenerationConfig config,
            Layout.WorldLayoutContext layout)
        {
            if (layout == null) return;

            int resolution = context.Resolution;
            float[,] heightMap = context.NormalizedHeightMap;
            float worldSize = context.WorldSize;

            // Get correction limits from layout config
            var layoutConfig = config.LayoutConfig;
            if (layoutConfig == null) return;

            float corridorWidth = layoutConfig.CorridorWidth;
            float corridorBlend = layoutConfig.CorridorBlend;
            float maxCorrection = layoutConfig.MaxCorrectionStrength;
            float clearingRadius = layoutConfig.ClearingFlattenRadius;

            // Process each point in the heightmap
            for (int x = 0; x < resolution; x++)
            {
                for (int z = 0; z < resolution; z++)
                {
                    float normX = x / (float)(resolution - 1);
                    float normZ = z / (float)(resolution - 1);
                    float worldX = normX * worldSize;
                    float worldZ = normZ * worldSize;

                    float originalHeight = heightMap[x, z];
                    float correctionStrength = 0f;
                    float targetHeight = originalHeight;

                    // Check distance to primary path edges (corridors)
                    foreach (var edge in layout.EdgesOrdered)
                    {
                        if (!edge.IsPrimaryPathEdge) continue;

                        // Find closest point on edge polyline
                        float minDist = float.MaxValue;
                        float closestHeight = originalHeight;

                        var points = edge.ControlPoints;
                        for (int i = 0; i < points.Count - 1; i++)
                        {
                            var p1 = points[i];
                            var p2 = points[i + 1];
                            var closest = ClosestPointOnLineSegment(
                                new Vector2(worldX, worldZ),
                                new Vector2(p1.x, p1.z),
                                new Vector2(p2.x, p2.z)
                            );
                            float dist = Vector2.Distance(new Vector2(worldX, worldZ), closest);
                            if (dist < minDist)
                            {
                                minDist = dist;
                                // Interpolate height along segment
                                float t = Vector2.Distance(closest, new Vector2(p1.x, p1.z)) /
                                          Vector2.Distance(new Vector2(p1.x, p1.z), new Vector2(p2.x, p2.z));
                                t = Mathf.Clamp01(t);
                                closestHeight = Mathf.Lerp(
                                    p1.y / context.TerrainHeight,
                                    p2.y / context.TerrainHeight,
                                    t
                                );
                            }
                        }

                        if (minDist < corridorWidth + corridorBlend)
                        {
                            float strength;
                            if (minDist < corridorWidth)
                            {
                                strength = 1f;
                            }
                            else
                            {
                                float t = (minDist - corridorWidth) / corridorBlend;
                                strength = 1f - (t * t * (3f - 2f * t));
                            }

                            strength = Mathf.Min(strength, maxCorrection);
                            if (strength > correctionStrength)
                            {
                                correctionStrength = strength;
                                targetHeight = closestHeight;
                            }
                        }
                    }

                    // Check distance to clearing nodes
                    foreach (var node in layout.NodesOrdered)
                    {
                        if (node.Type != Layout.WorldNodeType.Clearing) continue;

                        float dist = Vector2.Distance(
                            new Vector2(worldX, worldZ),
                            new Vector2(node.Position.x, node.Position.z)
                        );

                        if (dist < clearingRadius + corridorBlend)
                        {
                            float strength;
                            if (dist < clearingRadius)
                            {
                                strength = 1f;
                            }
                            else
                            {
                                float t = (dist - clearingRadius) / corridorBlend;
                                strength = 1f - (t * t * (3f - 2f * t));
                            }

                            strength = Mathf.Min(strength, maxCorrection);
                            if (strength > correctionStrength)
                            {
                                correctionStrength = strength;
                                targetHeight = node.Position.y / context.TerrainHeight;
                            }
                        }
                    }

                    // Apply correction
                    if (correctionStrength > 0f)
                    {
                        heightMap[x, z] = Mathf.Lerp(originalHeight, targetHeight, correctionStrength);
                    }
                }
            }

            // Recompute slope map after layout flattening
            ComputeSlopeMap(context);
        }

        private static Vector2 ClosestPointOnLineSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float lengthSq = ab.sqrMagnitude;
            if (lengthSq < 0.0001f) return a;

            float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / lengthSq);
            return a + t * ab;
        }
    }
}

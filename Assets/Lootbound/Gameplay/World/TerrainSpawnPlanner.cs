using UnityEngine;

namespace Lootbound.Gameplay.World
{
    /// <summary>
    /// Finds a valid spawn location and prepares the spawn zone.
    /// </summary>
    public static class TerrainSpawnPlanner
    {
        /// <summary>
        /// Find and prepare a valid spawn location.
        /// </summary>
        public static void PlanSpawn(TerrainGenerationContext context, TerrainGenerationConfig config)
        {
            // Find valid spawn position
            Vector3 spawnPos = FindSpawnPosition(context, config);
            context.SpawnPosition = spawnPos;

            // Get slope at spawn
            var (spawnX, spawnZ) = context.WorldToHeightmap(spawnPos);
            context.SpawnSlope = context.SlopeMap[spawnX, spawnZ];

            // Apply flattening around spawn
            TerrainHeightGenerator.ApplySpawnFlattening(context, config, spawnPos);

            // Update spawn position height after flattening
            float newHeight = context.SampleHeightAtWorld(spawnPos.x, spawnPos.z);
            context.SpawnPosition = new Vector3(spawnPos.x, newHeight, spawnPos.z);

            // Update slope at spawn after flattening
            context.SpawnSlope = context.SlopeMap[spawnX, spawnZ];
        }

        /// <summary>
        /// Find a valid spawn position in the terrain.
        /// </summary>
        private static Vector3 FindSpawnPosition(TerrainGenerationContext context, TerrainGenerationConfig config)
        {
            int resolution = context.Resolution;
            float worldSize = context.WorldSize;
            float maxSlope = config.MaxSpawnSlope;

            // Search area - center region of the map
            float searchRadius = worldSize * 0.2f;
            Vector2 center = new Vector2(worldSize * 0.5f, worldSize * 0.5f);

            // Use seed to determine search pattern offset
            System.Random searchRandom = new System.Random(context.Seed + 12345);
            float angleOffset = (float)(searchRandom.NextDouble() * Mathf.PI * 2f);

            // Spiral search from center
            Vector3 bestPosition = new Vector3(center.x, 0, center.y);
            float bestScore = float.MinValue;
            bool foundValid = false;

            int searchSteps = 50;
            float maxSearchRadius = worldSize * 0.35f;

            for (int step = 0; step < searchSteps; step++)
            {
                // Spiral outward
                float t = step / (float)searchSteps;
                float radius = t * maxSearchRadius;
                float angle = angleOffset + t * Mathf.PI * 8f;

                float testX = center.x + Mathf.Cos(angle) * radius;
                float testZ = center.y + Mathf.Sin(angle) * radius;

                // Check bounds
                float margin = config.SpawnBlendRadius * 1.5f;
                if (testX < margin || testX > worldSize - margin ||
                    testZ < margin || testZ > worldSize - margin)
                {
                    continue;
                }

                // Get heightmap position
                var (hx, hz) = context.WorldToHeightmap(new Vector3(testX, 0, testZ));

                // Check slope
                float slope = context.SlopeMap[hx, hz];
                if (slope > maxSlope)
                {
                    continue;
                }

                // Check height - prefer mid-range heights
                float normalizedHeight = context.NormalizedHeightMap[hx, hz];
                if (normalizedHeight < 0.1f || normalizedHeight > 0.6f)
                {
                    continue;
                }

                // Check surrounding area for accessibility
                float areaScore = EvaluateSpawnArea(context, config, hx, hz);
                if (areaScore < 0)
                {
                    continue;
                }

                // Score based on:
                // - Lower slope is better
                // - Mid-range height is better
                // - More accessible area is better
                // - Closer to center is slightly better
                float slopeScore = 1f - (slope / maxSlope);
                float heightScore = 1f - Mathf.Abs(normalizedHeight - 0.3f) * 2f;
                float distanceFromCenter = Vector2.Distance(new Vector2(testX, testZ), center);
                float centerScore = 1f - (distanceFromCenter / maxSearchRadius) * 0.3f;

                float totalScore = slopeScore * 0.4f + heightScore * 0.2f + areaScore * 0.3f + centerScore * 0.1f;

                if (totalScore > bestScore)
                {
                    bestScore = totalScore;
                    float worldY = context.SampleHeightAtWorld(testX, testZ);
                    bestPosition = new Vector3(testX, worldY, testZ);
                    foundValid = true;
                }
            }

            // If no valid position found, use center with warning
            if (!foundValid)
            {
                Debug.LogWarning("[TerrainSpawnPlanner] Could not find ideal spawn position. Using center of map.");
                float centerY = context.SampleHeightAtWorld(center.x, center.y);
                bestPosition = new Vector3(center.x, centerY, center.y);
            }

            return bestPosition;
        }

        /// <summary>
        /// Evaluate the area around a potential spawn point.
        /// Returns a score from 0 to 1, or -1 if the area is unsuitable.
        /// </summary>
        private static float EvaluateSpawnArea(TerrainGenerationContext context, TerrainGenerationConfig config, int centerX, int centerZ)
        {
            int resolution = context.Resolution;
            float checkRadius = config.SpawnSafeRadius;
            float worldSize = context.WorldSize;

            // Convert radius to heightmap units
            int radiusInPixels = Mathf.CeilToInt((checkRadius / worldSize) * (resolution - 1));

            float totalSlope = 0f;
            float maxSlope = 0f;
            int validSamples = 0;
            int totalSamples = 0;

            for (int dx = -radiusInPixels; dx <= radiusInPixels; dx++)
            {
                for (int dz = -radiusInPixels; dz <= radiusInPixels; dz++)
                {
                    int x = centerX + dx;
                    int z = centerZ + dz;

                    if (x < 0 || x >= resolution || z < 0 || z >= resolution)
                    {
                        continue;
                    }

                    // Check if within circular radius
                    float dist = Mathf.Sqrt(dx * dx + dz * dz);
                    if (dist > radiusInPixels)
                    {
                        continue;
                    }

                    totalSamples++;
                    float slope = context.SlopeMap[x, z];
                    totalSlope += slope;

                    if (slope > maxSlope)
                    {
                        maxSlope = slope;
                    }

                    if (slope <= config.MaxSpawnSlope * 1.5f)
                    {
                        validSamples++;
                    }
                }
            }

            if (totalSamples == 0)
            {
                return -1f;
            }

            // Require at least 80% of samples to be valid
            float validRatio = validSamples / (float)totalSamples;
            if (validRatio < 0.8f)
            {
                return -1f;
            }

            // Score based on average slope and valid ratio
            float avgSlope = totalSlope / totalSamples;
            float slopeScore = 1f - Mathf.Clamp01(avgSlope / config.MaxSpawnSlope);

            return slopeScore * validRatio;
        }

        /// <summary>
        /// Get the player start position above the terrain.
        /// </summary>
        public static Vector3 GetPlayerStartPosition(TerrainGenerationContext context, float playerHeight)
        {
            Vector3 spawn = context.SpawnPosition;

            // Place player slightly above terrain to avoid clipping
            float safeHeight = spawn.y + playerHeight * 0.5f + 0.1f;

            return new Vector3(spawn.x, safeHeight, spawn.z);
        }
    }
}

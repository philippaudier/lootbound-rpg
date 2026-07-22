using UnityEngine;

namespace Lootbound.Gameplay.World
{
    /// <summary>
    /// Paints terrain texture layers based on height, slope, and other factors.
    /// </summary>
    public static class TerrainSurfacePainter
    {
        /// <summary>
        /// Layer indices for terrain painting.
        /// </summary>
        public enum TerrainLayer
        {
            Grass = 0,      // Low areas, gentle slopes
            DryGround = 1,  // Mid elevations
            Rock = 2,       // Steep slopes
            Highland = 3    // High elevations
        }

        /// <summary>
        /// Paint the terrain based on the generation context.
        /// </summary>
        public static float[,,] Paint(TerrainGenerationContext context, TerrainGenerationConfig config, int alphamapResolution)
        {
            int heightmapRes = context.Resolution;
            int numLayers = 4;

            float[,,] alphamap = new float[alphamapResolution, alphamapResolution, numLayers];

            // Use seed for noise variation in painting
            System.Random paintRandom = new System.Random(context.Seed + 54321);
            float noiseOffsetX = (float)(paintRandom.NextDouble() * 1000);
            float noiseOffsetZ = (float)(paintRandom.NextDouble() * 1000);

            for (int az = 0; az < alphamapResolution; az++)
            {
                for (int ax = 0; ax < alphamapResolution; ax++)
                {
                    // Convert alphamap coords to heightmap coords
                    float normX = ax / (float)(alphamapResolution - 1);
                    float normZ = az / (float)(alphamapResolution - 1);

                    int hx = Mathf.FloorToInt(normX * (heightmapRes - 1));
                    int hz = Mathf.FloorToInt(normZ * (heightmapRes - 1));
                    hx = Mathf.Clamp(hx, 0, heightmapRes - 1);
                    hz = Mathf.Clamp(hz, 0, heightmapRes - 1);

                    // Get terrain data at this point
                    float height = context.NormalizedHeightMap[hx, hz];
                    float slope = context.SlopeMap[hx, hz];
                    float macro = context.MacroMap[hx, hz];

                    // Add noise variation for more natural look
                    float noiseScale = 50f;
                    float noise = Mathf.PerlinNoise(
                        (ax + noiseOffsetX) / noiseScale,
                        (az + noiseOffsetZ) / noiseScale
                    );
                    float fineNoise = Mathf.PerlinNoise(
                        (ax + noiseOffsetX) / 15f,
                        (az + noiseOffsetZ) / 15f
                    );

                    // Calculate layer weights
                    float[] weights = CalculateLayerWeights(
                        height, slope, macro, noise, fineNoise, config
                    );

                    // Normalize weights
                    float sum = 0f;
                    for (int i = 0; i < numLayers; i++)
                    {
                        sum += weights[i];
                    }

                    if (sum > 0.001f)
                    {
                        for (int i = 0; i < numLayers; i++)
                        {
                            weights[i] /= sum;
                        }
                    }
                    else
                    {
                        // Default to grass if no weights
                        weights[0] = 1f;
                    }

                    // Apply weights to alphamap
                    // Note: Unity alphamap uses [z, x, layer] indexing
                    for (int i = 0; i < numLayers; i++)
                    {
                        alphamap[az, ax, i] = weights[i];
                    }
                }
            }

            return alphamap;
        }

        /// <summary>
        /// Calculate weights for each terrain layer at a single point. Public so
        /// the same classification can be reused per world coordinate (e.g. by the
        /// generator's splat sampler for streamed chunks), not just over the
        /// monolithic context grid. Weights are NOT normalized here.
        /// </summary>
        public static float[] CalculateLayerWeights(
            float height, float slope, float macro, float noise, float fineNoise,
            TerrainGenerationConfig config)
        {
            float[] weights = new float[4];

            // Layer thresholds
            float lowlandThreshold = config.LowlandThreshold;
            float highlandThreshold = config.HighlandThreshold;
            float steepThreshold = config.SteepSlopeThreshold;

            // Rock layer - steep slopes get rock regardless of height
            float rockWeight = 0f;
            if (slope > steepThreshold * 0.7f)
            {
                float slopeT = Mathf.InverseLerp(steepThreshold * 0.7f, steepThreshold, slope);
                rockWeight = Mathf.SmoothStep(0f, 1f, slopeT);
            }

            // Highland layer - high elevations
            float highlandWeight = 0f;
            if (height > highlandThreshold * 0.85f)
            {
                float heightT = Mathf.InverseLerp(highlandThreshold * 0.85f, highlandThreshold, height);
                highlandWeight = Mathf.SmoothStep(0f, 1f, heightT);

                // Add noise variation
                highlandWeight *= 0.7f + fineNoise * 0.3f;
            }

            // Grass layer - low areas and gentle slopes
            float grassWeight = 0f;
            if (height < lowlandThreshold * 1.3f && slope < steepThreshold * 0.6f)
            {
                float heightFactor = 1f - Mathf.InverseLerp(lowlandThreshold * 0.8f, lowlandThreshold * 1.3f, height);
                float slopeFactor = 1f - Mathf.InverseLerp(steepThreshold * 0.3f, steepThreshold * 0.6f, slope);

                grassWeight = Mathf.Min(heightFactor, slopeFactor);
                grassWeight = Mathf.SmoothStep(0f, 1f, grassWeight);

                // Noise variation
                grassWeight *= 0.8f + noise * 0.2f;
            }

            // Apply weights with priority
            weights[(int)TerrainLayer.Rock] = rockWeight;

            // Reduce other weights where rock is present
            float remainingWeight = 1f - rockWeight;

            weights[(int)TerrainLayer.Highland] = highlandWeight * remainingWeight * (1f - rockWeight * 0.5f);
            remainingWeight -= weights[(int)TerrainLayer.Highland];

            weights[(int)TerrainLayer.Grass] = grassWeight * Mathf.Max(0f, remainingWeight);
            remainingWeight -= weights[(int)TerrainLayer.Grass];

            weights[(int)TerrainLayer.DryGround] = Mathf.Max(0f, remainingWeight);

            // Add transition noise for softer edges
            float transitionNoise = (fineNoise - 0.5f) * 0.15f;

            // Apply subtle noise to grass/dry boundary
            if (weights[(int)TerrainLayer.Grass] > 0.1f && weights[(int)TerrainLayer.DryGround] > 0.1f)
            {
                float adjustment = transitionNoise * Mathf.Min(weights[(int)TerrainLayer.Grass], weights[(int)TerrainLayer.DryGround]);
                weights[(int)TerrainLayer.Grass] += adjustment;
                weights[(int)TerrainLayer.DryGround] -= adjustment;
            }

            return weights;
        }

        /// <summary>
        /// Get the recommended terrain layers order.
        /// </summary>
        public static string[] GetLayerNames()
        {
            return new string[]
            {
                "Grass",      // Index 0
                "DryGround",  // Index 1
                "Rock",       // Index 2
                "Highland"    // Index 3
            };
        }
    }
}

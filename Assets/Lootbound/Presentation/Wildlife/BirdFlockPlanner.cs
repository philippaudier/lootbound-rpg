using UnityEngine;

namespace Lootbound.Presentation.Wildlife
{
    /// <summary>
    /// Pure, deterministic flock planning. No camera, no player transform,
    /// no current time, no UnityEngine.Random, no mutable global state:
    /// (seed, library, anchor, minimum height) fully determine the plan.
    /// The presenter resolves the anchor, the ground height and the stable
    /// seed BEFORE calling in; camera avoidance is applied afterwards as a
    /// global translation only.
    /// </summary>
    public static class BirdFlockPlanner
    {
        // Defaults used when the library is absent (development fallback).
        private static readonly Vector2Int DefaultGroupSize = new Vector2Int(2, 6);
        private static readonly Vector2 DefaultDuration = new Vector2(6f, 12f);
        private const float DefaultRadius = 18f;
        private static readonly Vector2 DefaultHeight = new Vector2(4f, 14f);
        private const float DefaultBobAmplitude = 0.35f;
        private static readonly Vector2 DefaultScale = new Vector2(0.8f, 1.2f);
        private static readonly Vector2 DefaultFlapSpeed = new Vector2(2f, 4f);

        /// <summary>Stable FNV-1a seed derived from the ambient event's stable data.</summary>
        public static int DeriveSeed(string eventId, Vector3 position, float spawnTime)
        {
            unchecked
            {
                uint hash = 2166136261u;
                void Mix(int value) => hash = (hash ^ (uint)value) * 16777619u;
                if (!string.IsNullOrEmpty(eventId))
                {
                    foreach (char c in eventId) Mix(c);
                }
                Mix(Mathf.RoundToInt(position.x * 10f));
                Mix(Mathf.RoundToInt(position.y * 10f));
                Mix(Mathf.RoundToInt(position.z * 10f));
                Mix(Mathf.RoundToInt(spawnTime * 100f));
                return (int)hash;
            }
        }

        /// <summary>
        /// Builds a deterministic flock plan around <paramref name="anchor"/>.
        /// Returns false when no valid variant exists and the development
        /// fallback is not allowed. VariantIndex is -1 for a fallback plan.
        /// </summary>
        public static bool TryCreatePlan(
            int seed,
            BirdVisualLibrary library,
            Vector3 anchor,
            float minimumHeight,
            bool allowDevelopmentFallback,
            out BirdFlockPlan plan)
        {
            plan = null;
            var random = new System.Random(seed);

            int variantIndex = PickVariant(library, random);
            if (variantIndex < 0)
            {
                if (!allowDevelopmentFallback)
                {
                    return false;
                }

                variantIndex = -1;
            }

            // Group settings (library or fallback defaults)
            Vector2Int groupRange = library != null ? library.GroupSizeRange : DefaultGroupSize;
            Vector2 durationRange = library != null ? library.FlightDurationRange : DefaultDuration;
            float radius = library != null ? library.FlightRadius : DefaultRadius;
            Vector2 heightRange = library != null ? library.FlightHeightRange : DefaultHeight;
            float bobAmplitude = library != null ? library.BobAmplitude : DefaultBobAmplitude;

            Vector2 scaleRange = DefaultScale;
            Vector2 flapRange = DefaultFlapSpeed;
            if (variantIndex >= 0)
            {
                var variant = library.Variants[variantIndex];
                scaleRange = variant.ScaleRange;
                flapRange = variant.FlapSpeedRange;
            }

            int birdCount = groupRange.x + random.Next(groupRange.y - groupRange.x + 1);
            float duration = Mathf.Clamp(Lerp(durationRange, random), 2f, 60f);

            var trajectory = CreateTrajectory(random, anchor, minimumHeight, radius, heightRange);

            var specs = new BirdSpec[birdCount];
            for (int i = 0; i < birdCount; i++)
            {
                // Loose formation: irregular distances and heights, offset
                // starts and close-but-different speeds. Never a perfect V.
                var offset = new Vector3(
                    LerpRange(-2.6f, 2.6f, random),
                    LerpRange(-0.9f, 0.9f, random),
                    LerpRange(-2.6f, 2.6f, random));

                specs[i] = new BirdSpec(
                    formationOffset: offset,
                    speedFactor: Mathf.Clamp(LerpRange(0.9f, 1.1f, random), 0.5f, 1.5f),
                    startDelay: Mathf.Clamp(LerpRange(0f, 0.6f, random), 0f, 2f),
                    bobPhase: LerpRange(0f, Mathf.PI * 2f, random),
                    bobAmplitude: bobAmplitude * LerpRange(0.6f, 1.2f, random),
                    scale: Lerp(scaleRange, random),
                    flapSpeed: Lerp(flapRange, random));
            }

            plan = new BirdFlockPlan(variantIndex, trajectory, duration, specs);
            return true;
        }

        /// <summary>Weighted pick among valid variants (prefab set, weight &gt; 0); -1 when none.</summary>
        private static int PickVariant(BirdVisualLibrary library, System.Random random)
        {
            var variants = library != null ? library.Variants : null;
            if (variants == null || variants.Length == 0)
            {
                return -1;
            }

            float totalWeight = 0f;
            for (int i = 0; i < variants.Length; i++)
            {
                if (IsValid(variants[i]))
                {
                    totalWeight += variants[i].Weight;
                }
            }

            if (totalWeight <= 0f)
            {
                return -1;
            }

            float pick = (float)(random.NextDouble() * totalWeight);
            float accumulated = 0f;
            int lastValid = -1;
            for (int i = 0; i < variants.Length; i++)
            {
                if (!IsValid(variants[i]))
                {
                    continue;
                }

                lastValid = i;
                accumulated += variants[i].Weight;
                if (pick <= accumulated)
                {
                    return i;
                }
            }

            return lastValid;
        }

        private static bool IsValid(BirdVisualVariant variant)
        {
            return variant != null && variant.Prefab != null && variant.Weight > 0f &&
                   !float.IsNaN(variant.Weight) && !float.IsInfinity(variant.Weight);
        }

        private static BirdTrajectory CreateTrajectory(
            System.Random random, Vector3 anchor, float minimumHeight, float radius, Vector2 heightRange)
        {
            float baseY = Mathf.Max(minimumHeight, anchor.y);
            float angle = LerpRange(0f, Mathf.PI * 2f, random);
            var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            var perpendicular = new Vector3(-direction.z, 0f, direction.x);

            var type = (BirdTrajectoryType)random.Next(3);
            Vector3 start, control, end;

            switch (type)
            {
                case BirdTrajectoryType.Rising:
                {
                    // Starts low near the anchor, climbs and leaves.
                    start = anchor + perpendicular * LerpRange(-0.25f, 0.25f, random) * radius;
                    start.y = baseY + LerpRange(0.5f, 2f, random);

                    end = anchor + direction * radius * 1.3f;
                    end.y = baseY + heightRange.y + LerpRange(2f, 6f, random);

                    control = anchor + direction * radius * 0.4f + perpendicular * LerpRange(-0.2f, 0.2f, random) * radius;
                    control.y = (start.y + end.y) * 0.5f + LerpRange(0f, 2f, random);
                    break;
                }

                case BirdTrajectoryType.Circling:
                {
                    // A light arc around the anchor, then out.
                    float sweep = LerpRange(100f, 160f, random) * Mathf.Deg2Rad * (random.Next(2) == 0 ? 1f : -1f);
                    float endAngle = angle + sweep;
                    var endDirection = new Vector3(Mathf.Cos(endAngle), 0f, Mathf.Sin(endAngle));
                    float midAngle = angle + sweep * 0.5f;
                    var midDirection = new Vector3(Mathf.Cos(midAngle), 0f, Mathf.Sin(midAngle));

                    start = anchor + direction * radius * 0.85f;
                    start.y = baseY + Lerp(heightRange, random);

                    end = anchor + endDirection * radius * 1.5f;
                    end.y = baseY + Lerp(heightRange, random);

                    control = anchor + midDirection * radius * 1.5f;
                    control.y = Mathf.Max(start.y, end.y) + LerpRange(0f, 2f, random);
                    break;
                }

                default: // Crossing
                {
                    start = anchor - direction * radius + perpendicular * LerpRange(-0.3f, 0.3f, random) * radius;
                    start.y = baseY + Lerp(heightRange, random);

                    end = anchor + direction * radius + perpendicular * LerpRange(-0.3f, 0.3f, random) * radius;
                    end.y = baseY + Lerp(heightRange, random);

                    // A sideways control point keeps the path from being a
                    // perfectly straight line.
                    control = anchor + perpendicular * LerpRange(-0.5f, 0.5f, random) * radius;
                    control.y = Mathf.Max(start.y, end.y) + LerpRange(0.5f, 3f, random);
                    break;
                }
            }

            return new BirdTrajectory(type, start, control, end);
        }

        private static float Lerp(Vector2 range, System.Random random)
        {
            return Mathf.Lerp(range.x, range.y, (float)random.NextDouble());
        }

        private static float LerpRange(float min, float max, System.Random random)
        {
            return Mathf.Lerp(min, max, (float)random.NextDouble());
        }
    }
}

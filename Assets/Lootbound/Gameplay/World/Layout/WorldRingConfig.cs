using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lootbound.Gameplay.World.Layout
{
    /// <summary>
    /// Configuration for WorldRing thresholds.
    /// Thresholds define where each ring begins (normalized radius 0-1).
    ///
    /// Boundary rule: All rings use [min, max) except Void which uses [min, +∞].
    /// This ensures no overlap between rings.
    /// </summary>
    [CreateAssetMenu(fileName = "WorldRingConfig", menuName = "Lootbound/World Ring Config")]
    public class WorldRingConfig : ScriptableObject
    {
        [Serializable]
        public struct RingThreshold
        {
            [Tooltip("The ring this threshold defines")]
            public WorldRing ring;

            [Tooltip("Minimum normalized radius where this ring begins (inclusive)")]
            [Range(0f, 1f)]
            public float minimumNormalizedRadius;
        }

        [SerializeField]
        [Tooltip("Ring thresholds in ascending order. Each ring's max is the next ring's min.")]
        private RingThreshold[] ringThresholds;

        // Cached validation state
        private bool _isValidated;
        private bool _isValid;
        private string _validationError;

        /// <summary>
        /// Get the WorldRing at a given normalized world radius.
        /// Throws InvalidOperationException if config is invalid.
        /// </summary>
        public WorldRing GetRingAt(float normalizedWorldRadius)
        {
            EnsureValidated();

            if (!_isValid)
            {
                throw new InvalidOperationException($"WorldRingConfig is invalid: {_validationError}");
            }

            // Handle edge cases
            if (normalizedWorldRadius < 0f)
            {
                return WorldRing.Refuge;
            }

            // Find the ring by checking thresholds in reverse order
            // Each ring owns [min, nextMin) except Void which owns [min, +∞]
            for (int i = ringThresholds.Length - 1; i >= 0; i--)
            {
                if (normalizedWorldRadius >= ringThresholds[i].minimumNormalizedRadius)
                {
                    return ringThresholds[i].ring;
                }
            }

            // Should not reach here if config is valid (Refuge starts at 0)
            return WorldRing.Refuge;
        }

        /// <summary>
        /// Get the minimum normalized radius for a specific ring.
        /// Throws if config is invalid or ring not found.
        /// </summary>
        public float GetMinimumRadius(WorldRing ring)
        {
            EnsureValidated();

            if (!_isValid)
            {
                throw new InvalidOperationException($"WorldRingConfig is invalid: {_validationError}");
            }

            foreach (var threshold in ringThresholds)
            {
                if (threshold.ring == ring)
                {
                    return threshold.minimumNormalizedRadius;
                }
            }

            throw new ArgumentException($"Ring {ring} not found in configuration");
        }

        /// <summary>
        /// Get the maximum normalized radius for a specific ring.
        /// Returns float.PositiveInfinity for Void.
        /// Throws if config is invalid or ring not found.
        /// </summary>
        public float GetMaximumRadius(WorldRing ring)
        {
            EnsureValidated();

            if (!_isValid)
            {
                throw new InvalidOperationException($"WorldRingConfig is invalid: {_validationError}");
            }

            for (int i = 0; i < ringThresholds.Length; i++)
            {
                if (ringThresholds[i].ring == ring)
                {
                    // Last ring (Void) has no maximum
                    if (i == ringThresholds.Length - 1)
                    {
                        return float.PositiveInfinity;
                    }
                    // Max is the next ring's min
                    return ringThresholds[i + 1].minimumNormalizedRadius;
                }
            }

            throw new ArgumentException($"Ring {ring} not found in configuration");
        }

        /// <summary>
        /// Check if the configuration is valid.
        /// </summary>
        public bool IsValid
        {
            get
            {
                EnsureValidated();
                return _isValid;
            }
        }

        /// <summary>
        /// Get the validation error message if config is invalid.
        /// </summary>
        public string ValidationError
        {
            get
            {
                EnsureValidated();
                return _validationError;
            }
        }

        /// <summary>
        /// Validate the configuration and throw if invalid.
        /// Call this early to catch configuration errors.
        /// </summary>
        public void ValidateOrThrow()
        {
            EnsureValidated();

            if (!_isValid)
            {
                throw new InvalidOperationException($"WorldRingConfig validation failed: {_validationError}");
            }
        }

        private void EnsureValidated()
        {
            if (_isValidated) return;

            _validationError = ValidateConfiguration();
            _isValid = string.IsNullOrEmpty(_validationError);
            _isValidated = true;
        }

        private string ValidateConfiguration()
        {
            // Check array exists and has entries
            if (ringThresholds == null || ringThresholds.Length == 0)
            {
                return "Ring thresholds array is null or empty";
            }

            // Expected ring count (all WorldRing values)
            int expectedRingCount = Enum.GetValues(typeof(WorldRing)).Length;
            if (ringThresholds.Length != expectedRingCount)
            {
                return $"Expected {expectedRingCount} ring thresholds, found {ringThresholds.Length}";
            }

            // Track seen rings for duplicate detection
            var seenRings = new HashSet<WorldRing>();

            // First ring must be Refuge at 0
            if (ringThresholds[0].ring != WorldRing.Refuge)
            {
                return $"First ring must be Refuge, found {ringThresholds[0].ring}";
            }

            if (ringThresholds[0].minimumNormalizedRadius != 0f)
            {
                return $"Refuge must start at 0, found {ringThresholds[0].minimumNormalizedRadius}";
            }

            // Last ring must be Void
            if (ringThresholds[ringThresholds.Length - 1].ring != WorldRing.Void)
            {
                return $"Last ring must be Void, found {ringThresholds[ringThresholds.Length - 1].ring}";
            }

            float previousMin = -1f;

            for (int i = 0; i < ringThresholds.Length; i++)
            {
                var threshold = ringThresholds[i];

                // Check for duplicate rings
                if (!seenRings.Add(threshold.ring))
                {
                    return $"Duplicate ring found: {threshold.ring}";
                }

                // Check for negative values
                if (threshold.minimumNormalizedRadius < 0f)
                {
                    return $"Ring {threshold.ring} has negative minimum: {threshold.minimumNormalizedRadius}";
                }

                // Check values <= 1 for all rings except Void
                // (Void's minimum can be <= 1, it just extends to infinity)
                if (i < ringThresholds.Length - 1 && threshold.minimumNormalizedRadius > 1f)
                {
                    return $"Ring {threshold.ring} has minimum > 1: {threshold.minimumNormalizedRadius}";
                }

                // Check ascending order (strictly increasing after first)
                if (i > 0 && threshold.minimumNormalizedRadius <= previousMin)
                {
                    return $"Ring thresholds must be strictly ascending. {threshold.ring} min ({threshold.minimumNormalizedRadius}) <= previous ({previousMin})";
                }

                previousMin = threshold.minimumNormalizedRadius;
            }

            // Check all rings are present
            foreach (WorldRing ring in Enum.GetValues(typeof(WorldRing)))
            {
                if (!seenRings.Contains(ring))
                {
                    return $"Missing ring in configuration: {ring}";
                }
            }

            return null; // Valid
        }

        private void OnValidate()
        {
            // Reset validation cache when modified in editor
            _isValidated = false;
            _isValid = false;
            _validationError = null;

            // Validate and log errors in editor
            string error = ValidateConfiguration();
            if (!string.IsNullOrEmpty(error))
            {
                Debug.LogError($"[WorldRingConfig] {error}");
            }
        }

        /// <summary>
        /// Create a default valid configuration for testing.
        /// </summary>
        public static WorldRingConfig CreateDefault()
        {
            var config = CreateInstance<WorldRingConfig>();
            config.ringThresholds = new RingThreshold[]
            {
                new RingThreshold { ring = WorldRing.Refuge, minimumNormalizedRadius = 0f },
                new RingThreshold { ring = WorldRing.Nearlands, minimumNormalizedRadius = 0.05f },
                new RingThreshold { ring = WorldRing.Wildlands, minimumNormalizedRadius = 0.15f },
                new RingThreshold { ring = WorldRing.Farlands, minimumNormalizedRadius = 0.35f },
                new RingThreshold { ring = WorldRing.Outerlands, minimumNormalizedRadius = 0.55f },
                new RingThreshold { ring = WorldRing.Edgelands, minimumNormalizedRadius = 0.75f },
                new RingThreshold { ring = WorldRing.Void, minimumNormalizedRadius = 0.90f }
            };
            return config;
        }
    }
}

using System;

namespace Lootbound.Gameplay.Equipment
{
    /// <summary>
    /// Production implementation of IAttunementRandomSource using System.Random.
    /// Thread-safe with per-instance random state.
    /// </summary>
    public class SystemRandomSource : IAttunementRandomSource
    {
        private readonly Random random;

        /// <summary>
        /// Create a new random source with a random seed.
        /// </summary>
        public SystemRandomSource()
        {
            random = new Random();
        }

        /// <summary>
        /// Create a new random source with a specific seed for deterministic behavior.
        /// </summary>
        /// <param name="seed">Seed for the random number generator.</param>
        public SystemRandomSource(int seed)
        {
            random = new Random(seed);
        }

        /// <inheritdoc/>
        public float NextFloat()
        {
            return (float)random.NextDouble();
        }

        /// <inheritdoc/>
        public bool Roll(float successChance)
        {
            // Guaranteed success
            if (successChance >= 1f)
            {
                return true;
            }

            // Guaranteed failure
            if (successChance <= 0f)
            {
                return false;
            }

            return NextFloat() < successChance;
        }
    }

    /// <summary>
    /// Test implementation that always succeeds or fails based on configuration.
    /// For use in EditMode tests.
    /// </summary>
    public class DeterministicRandomSource : IAttunementRandomSource
    {
        private readonly float fixedValue;

        /// <summary>
        /// Create a deterministic source that returns a fixed value.
        /// </summary>
        /// <param name="fixedValue">Value to return from NextFloat().</param>
        public DeterministicRandomSource(float fixedValue)
        {
            this.fixedValue = fixedValue;
        }

        /// <summary>
        /// Create a source that always succeeds.
        /// </summary>
        public static DeterministicRandomSource AlwaysSucceed => new DeterministicRandomSource(0f);

        /// <summary>
        /// Create a source that always fails (unless guaranteed).
        /// </summary>
        public static DeterministicRandomSource AlwaysFail => new DeterministicRandomSource(0.999f);

        /// <inheritdoc/>
        public float NextFloat()
        {
            return fixedValue;
        }

        /// <inheritdoc/>
        public bool Roll(float successChance)
        {
            if (successChance >= 1f) return true;
            if (successChance <= 0f) return false;
            return fixedValue < successChance;
        }
    }

    /// <summary>
    /// Test implementation that returns a sequence of predetermined results.
    /// For use in EditMode tests requiring specific outcome sequences.
    /// </summary>
    public class SequenceRandomSource : IAttunementRandomSource
    {
        private readonly bool[] sequence;
        private int index;

        /// <summary>
        /// Create a source that returns results from a predetermined sequence.
        /// </summary>
        /// <param name="sequence">Sequence of success (true) or failure (false) results.</param>
        public SequenceRandomSource(params bool[] sequence)
        {
            this.sequence = sequence ?? new[] { true };
            index = 0;
        }

        /// <inheritdoc/>
        public float NextFloat()
        {
            // Return 0 for success, 0.999 for failure
            bool result = GetNextResult();
            return result ? 0f : 0.999f;
        }

        /// <inheritdoc/>
        public bool Roll(float successChance)
        {
            if (successChance >= 1f) return true;
            if (successChance <= 0f) return false;
            return GetNextResult();
        }

        private bool GetNextResult()
        {
            if (sequence.Length == 0) return true;

            bool result = sequence[index];
            index = (index + 1) % sequence.Length;
            return result;
        }

        /// <summary>
        /// Reset the sequence to the beginning.
        /// </summary>
        public void Reset()
        {
            index = 0;
        }
    }
}

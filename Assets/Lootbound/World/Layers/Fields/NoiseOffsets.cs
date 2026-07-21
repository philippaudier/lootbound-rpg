using System;

namespace Lootbound.World.Layers.Fields
{
    /// <summary>
    /// Per-seed deterministic offsets for noise evaluation, drawn once from the
    /// seed and reused for every evaluation. Pure C# (System.Random) - no Unity.
    /// The draw ORDER is part of the determinism contract and must never change,
    /// or existing worlds would shift.
    /// </summary>
    public readonly struct NoiseOffsets
    {
        public readonly float MacroOffsetX;
        public readonly float MacroOffsetZ;
        public readonly float RidgeOffsetX;
        public readonly float RidgeOffsetZ;
        public readonly float ValleyOffsetX;
        public readonly float ValleyOffsetZ;
        public readonly float DetailOffsetX;
        public readonly float DetailOffsetZ;
        public readonly float WarpOffsetX;
        public readonly float WarpOffsetZ;

        public NoiseOffsets(int seed)
        {
            var random = new Random(seed);
            MacroOffsetX = (float)(random.NextDouble() * 10000);
            MacroOffsetZ = (float)(random.NextDouble() * 10000);
            RidgeOffsetX = (float)(random.NextDouble() * 10000);
            RidgeOffsetZ = (float)(random.NextDouble() * 10000);
            ValleyOffsetX = (float)(random.NextDouble() * 10000);
            ValleyOffsetZ = (float)(random.NextDouble() * 10000);
            DetailOffsetX = (float)(random.NextDouble() * 10000);
            DetailOffsetZ = (float)(random.NextDouble() * 10000);
            WarpOffsetX = (float)(random.NextDouble() * 10000);
            WarpOffsetZ = (float)(random.NextDouble() * 10000);
        }
    }
}

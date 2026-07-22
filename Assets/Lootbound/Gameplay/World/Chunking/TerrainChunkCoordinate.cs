using System;

namespace Lootbound.Gameplay.World.Chunking
{
    /// <summary>
    /// A chunk's index on the terrain-streaming grid, independent of Unity. It is
    /// a pure integer coordinate with world conversions, deliberately NOT a
    /// Vector2Int so the streaming grid is a first-class, testable concept and its
    /// world math (floor, negatives) lives in one place.
    ///
    /// The world is Refuge-centred, so chunk indices are signed: the chunk holding
    /// world (0,0) is (0,0), the chunk just west of it is (-1, 0), and so on.
    /// </summary>
    public readonly struct TerrainChunkCoordinate : IEquatable<TerrainChunkCoordinate>
    {
        public readonly int X;
        public readonly int Z;

        public TerrainChunkCoordinate(int x, int z)
        {
            X = x;
            Z = z;
        }

        /// <summary>
        /// The chunk that contains a world coordinate. Uses a mathematical FLOOR
        /// (not a truncating cast), so negative coordinates map correctly:
        /// -0.1 and -chunkSize both fall in chunk index -1, -chunkSize-0.1 in -2.
        /// </summary>
        public static TerrainChunkCoordinate FromWorld(double worldX, double worldZ, float chunkSize)
        {
            int x = (int)Math.Floor(worldX / chunkSize);
            int z = (int)Math.Floor(worldZ / chunkSize);
            return new TerrainChunkCoordinate(x, z);
        }

        /// <summary>World X of this chunk's minimum (south-west) corner.</summary>
        public double OriginWorldX(float chunkSize) => X * (double)chunkSize;

        /// <summary>World Z of this chunk's minimum (south-west) corner.</summary>
        public double OriginWorldZ(float chunkSize) => Z * (double)chunkSize;

        public bool Equals(TerrainChunkCoordinate other) => X == other.X && Z == other.Z;

        public override bool Equals(object obj) => obj is TerrainChunkCoordinate other && Equals(other);

        public override int GetHashCode()
        {
            // Order-sensitive integer hash; stable and collision-light for the
            // small signed ranges a streaming grid uses.
            unchecked
            {
                return (X * 397) ^ Z;
            }
        }

        public static bool operator ==(TerrainChunkCoordinate a, TerrainChunkCoordinate b) => a.Equals(b);
        public static bool operator !=(TerrainChunkCoordinate a, TerrainChunkCoordinate b) => !a.Equals(b);

        public override string ToString() => $"Chunk({X},{Z})";
    }
}

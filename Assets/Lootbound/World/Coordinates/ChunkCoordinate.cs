using System;

namespace Lootbound.World.Coordinates
{
    /// <summary>
    /// A discrete chunk-grid index (X,Y map to world X,Z). This belongs to the
    /// future Presentation layer only - chunks are a technical subdivision, never
    /// the world itself. It lives in the World Engine so the coordinate contract
    /// is defined in one place, but the engine's truth is always WorldCoordinate.
    /// </summary>
    public readonly struct ChunkCoordinate : IEquatable<ChunkCoordinate>
    {
        public readonly int X;
        public readonly int Y;

        public ChunkCoordinate(int x, int y)
        {
            X = x;
            Y = y;
        }

        public bool Equals(ChunkCoordinate other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is ChunkCoordinate other && Equals(other);
        public override int GetHashCode() => (X, Y).GetHashCode();
        public override string ToString() => $"Chunk({X}, {Y})";
    }
}

using System;

namespace Lootbound.World.Coordinates
{
    /// <summary>
    /// A position in the world, in DOUBLE precision. The World Engine reasons in
    /// world space and never in Unity space - there is deliberately no
    /// Vector2/Vector3 here. Presentation converts a WorldCoordinate to a Unity
    /// float position through a floating origin at the World-Unity seam, so the
    /// engine never loses precision even at hundreds of kilometres, and maps,
    /// minimaps, saves and replay all reason in world space.
    /// </summary>
    public readonly struct WorldCoordinate : IEquatable<WorldCoordinate>
    {
        public readonly double X;
        public readonly double Z;

        public WorldCoordinate(double x, double z)
        {
            X = x;
            Z = z;
        }

        public bool Equals(WorldCoordinate other) => X == other.X && Z == other.Z;
        public override bool Equals(object obj) => obj is WorldCoordinate other && Equals(other);
        public override int GetHashCode() => (X, Z).GetHashCode();
        public override string ToString() => $"World({X:0.###}, {Z:0.###})";
    }
}

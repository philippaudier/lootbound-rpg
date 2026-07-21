using Lootbound.World.Coordinates;

namespace Lootbound.World.Processing
{
    /// <summary>
    /// A bounded region to analyze, at a chosen resolution. Domain Processing is
    /// domain-agnostic: the same analyzer runs over the whole world today, a
    /// region or a streaming tile tomorrow. Resolution lives HERE, never inside
    /// an algorithm - so swapping 129² for 257², or a full world for a tile,
    /// changes only the domain, never the code.
    /// </summary>
    public readonly struct WorldDomain
    {
        public readonly WorldCoordinate Min;
        public readonly WorldCoordinate Max;

        /// <summary>Number of grid samples per side (grid is Resolution x Resolution).</summary>
        public readonly int Resolution;

        public WorldDomain(WorldCoordinate min, WorldCoordinate max, int resolution)
        {
            Min = min;
            Max = max;
            Resolution = resolution < 2 ? 2 : resolution;
        }

        /// <summary>Square domain of the given side length, cornered at the origin.</summary>
        public static WorldDomain FromOrigin(double sideLength, int resolution)
            => new WorldDomain(new WorldCoordinate(0, 0), new WorldCoordinate(sideLength, sideLength), resolution);

        public double Width => Max.X - Min.X;
        public double Depth => Max.Z - Min.Z;

        /// <summary>World metres between two adjacent grid samples, along X.</summary>
        public double CellSizeX => Width / (Resolution - 1);

        /// <summary>World metres between two adjacent grid samples, along Z.</summary>
        public double CellSizeZ => Depth / (Resolution - 1);

        /// <summary>World coordinate of grid cell (ix, iz).</summary>
        public WorldCoordinate CellToWorld(int ix, int iz)
            => new WorldCoordinate(Min.X + ix * CellSizeX, Min.Z + iz * CellSizeZ);
    }
}

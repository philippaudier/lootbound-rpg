using System;
using Lootbound.World.Coordinates;

namespace Lootbound.World.Processing
{
    /// <summary>
    /// D8 flow direction: where water leaves a cell. North = +Z, East = +X
    /// (consistent with <see cref="ExposureField"/>). None = a sink (no lower
    /// neighbour).
    /// </summary>
    public enum FlowDirection : byte
    {
        None,
        North,
        NorthEast,
        East,
        SouthEast,
        South,
        SouthWest,
        West,
        NorthWest
    }

    /// <summary>
    /// Shared helpers for grid-backed domain fields: cell offsets and the
    /// coordinate-to-cell mapping (nearest cell, clamped to the domain).
    /// </summary>
    internal static class DomainGrid
    {
        public static readonly FlowDirection[] AllDirections =
        {
            FlowDirection.North, FlowDirection.NorthEast, FlowDirection.East, FlowDirection.SouthEast,
            FlowDirection.South, FlowDirection.SouthWest, FlowDirection.West, FlowDirection.NorthWest
        };

        public static (int dx, int dz) Offset(FlowDirection d)
        {
            switch (d)
            {
                case FlowDirection.North: return (0, 1);
                case FlowDirection.NorthEast: return (1, 1);
                case FlowDirection.East: return (1, 0);
                case FlowDirection.SouthEast: return (1, -1);
                case FlowDirection.South: return (0, -1);
                case FlowDirection.SouthWest: return (-1, -1);
                case FlowDirection.West: return (-1, 0);
                case FlowDirection.NorthWest: return (-1, 1);
                default: return (0, 0);
            }
        }

        public static (int ix, int iz) NearestCell(WorldDomain domain, WorldCoordinate c)
        {
            int n = domain.Resolution;
            int ix = (int)Math.Round((c.X - domain.Min.X) / domain.CellSizeX);
            int iz = (int)Math.Round((c.Z - domain.Min.Z) / domain.CellSizeZ);
            if (ix < 0) ix = 0; else if (ix > n - 1) ix = n - 1;
            if (iz < 0) iz = 0; else if (iz > n - 1) iz = n - 1;
            return (ix, iz);
        }
    }
}

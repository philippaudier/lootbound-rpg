using UnityEngine;

namespace Lootbound.Gameplay.World
{
    /// <summary>
    /// The rectangular world-space region currently materialized by the generator.
    ///
    /// This is NOT a statement that "the world is this big". The logical world is
    /// unbounded: <see cref="ProceduralTerrainGenerator"/> can evaluate a height at
    /// any coordinate, positive or negative. These bounds only describe the finite
    /// region for which a concrete heightmap - with its authored refuge, paths and
    /// landmarks - currently exists. Streaming will simply ask for other regions.
    ///
    /// It is also the single place the world origin lives. The legacy corner
    /// convention has <c>Min = (0,0)</c>; a refuge-centred world has a negative Min
    /// and <c>Center = (0,0)</c>. Every world-to-grid conversion goes through here,
    /// so no call site ever carries an artificial offset.
    /// </summary>
    public readonly struct WorldBounds
    {
        public readonly float MinX;
        public readonly float MinZ;
        public readonly float SizeX;
        public readonly float SizeZ;

        public WorldBounds(float minX, float minZ, float sizeX, float sizeZ)
        {
            MinX = minX;
            MinZ = minZ;
            SizeX = sizeX;
            SizeZ = sizeZ;
        }

        public float MaxX => MinX + SizeX;
        public float MaxZ => MinZ + SizeZ;
        public float CenterX => MinX + SizeX * 0.5f;
        public float CenterZ => MinZ + SizeZ * 0.5f;

        public Vector2 Min => new Vector2(MinX, MinZ);
        public Vector2 Max => new Vector2(MaxX, MaxZ);
        public Vector2 Center => new Vector2(CenterX, CenterZ);

        /// <summary>
        /// A square region anchored at a corner. With <paramref name="minX"/> and
        /// <paramref name="minZ"/> both 0 this is the legacy [0, size] convention.
        /// </summary>
        public static WorldBounds FromCorner(float minX, float minZ, float size)
            => new WorldBounds(minX, minZ, size, size);

        /// <summary>
        /// A square region centred on a point. With centre (0,0) this is the
        /// refuge-centred convention the world migrates to.
        /// </summary>
        public static WorldBounds FromCenter(float centerX, float centerZ, float size)
            => new WorldBounds(centerX - size * 0.5f, centerZ - size * 0.5f, size, size);

        /// <summary>True if the coordinate lies inside the materialized region.</summary>
        public bool Contains(float worldX, float worldZ)
            => worldX >= MinX && worldX <= MaxX && worldZ >= MinZ && worldZ <= MaxZ;

        /// <summary>Fraction of the X extent, 0 at Min, 1 at Max (unclamped).</summary>
        public float NormalizeX(float worldX) => (worldX - MinX) / SizeX;

        /// <summary>Fraction of the Z extent, 0 at Min, 1 at Max (unclamped).</summary>
        public float NormalizeZ(float worldZ) => (worldZ - MinZ) / SizeZ;
    }
}

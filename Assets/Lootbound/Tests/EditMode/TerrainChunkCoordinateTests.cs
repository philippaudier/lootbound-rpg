using System.Collections.Generic;
using NUnit.Framework;
using Lootbound.Gameplay.World.Chunking;

namespace Lootbound.Tests.EditMode
{
    public class TerrainChunkCoordinateTests
    {
        private const float ChunkSize = 128f;

        [Test]
        public void FromWorld_PositiveAndZero()
        {
            Assert.AreEqual(new TerrainChunkCoordinate(0, 0), TerrainChunkCoordinate.FromWorld(0.0, 0.0, ChunkSize));
            Assert.AreEqual(0, TerrainChunkCoordinate.FromWorld(127.9, 0.0, ChunkSize).X);
            Assert.AreEqual(1, TerrainChunkCoordinate.FromWorld(128.0, 0.0, ChunkSize).X);
            Assert.AreEqual(1, TerrainChunkCoordinate.FromWorld(255.9, 0.0, ChunkSize).X);
            Assert.AreEqual(2, TerrainChunkCoordinate.FromWorld(256.0, 0.0, ChunkSize).X);
        }

        [Test]
        public void FromWorld_Negative_UsesFloorNotTruncation()
        {
            // A truncating cast would wrongly give 0 for -0.1 and -1 for -128.1.
            Assert.AreEqual(-1, TerrainChunkCoordinate.FromWorld(-0.1, 0.0, ChunkSize).X);
            Assert.AreEqual(-1, TerrainChunkCoordinate.FromWorld(-127.9, 0.0, ChunkSize).X);
            Assert.AreEqual(-1, TerrainChunkCoordinate.FromWorld(-128.0, 0.0, ChunkSize).X); // min corner of chunk -1
            Assert.AreEqual(-2, TerrainChunkCoordinate.FromWorld(-128.1, 0.0, ChunkSize).X);
            Assert.AreEqual(-2, TerrainChunkCoordinate.FromWorld(-256.0, 0.0, ChunkSize).X);
            // Z axis independently
            Assert.AreEqual(-3, TerrainChunkCoordinate.FromWorld(0.0, -300.0, ChunkSize).Z);
        }

        [Test]
        public void OriginWorld_RoundTrips()
        {
            var c = new TerrainChunkCoordinate(3, -4);
            Assert.AreEqual(384.0, c.OriginWorldX(ChunkSize), 1e-9);
            Assert.AreEqual(-512.0, c.OriginWorldZ(ChunkSize), 1e-9);
            Assert.AreEqual(c, TerrainChunkCoordinate.FromWorld(
                c.OriginWorldX(ChunkSize), c.OriginWorldZ(ChunkSize), ChunkSize));
        }

        [Test]
        public void Equality_And_HashSet_Dedup()
        {
            Assert.AreEqual(new TerrainChunkCoordinate(2, 5), new TerrainChunkCoordinate(2, 5));
            Assert.AreNotEqual(new TerrainChunkCoordinate(2, 5), new TerrainChunkCoordinate(5, 2));
            Assert.IsTrue(new TerrainChunkCoordinate(2, 5) == new TerrainChunkCoordinate(2, 5));
            Assert.IsTrue(new TerrainChunkCoordinate(2, 5) != new TerrainChunkCoordinate(5, 2));

            var set = new HashSet<TerrainChunkCoordinate>
            {
                new TerrainChunkCoordinate(2, 5),
                new TerrainChunkCoordinate(2, 5),
                new TerrainChunkCoordinate(5, 2),
            };
            Assert.AreEqual(2, set.Count);
        }

        [Test]
        public void RequiredChunks_CountIsSquare_AndHasNoDuplicates()
        {
            var center = new TerrainChunkCoordinate(10, -7);
            for (int r = 0; r <= 3; r++)
            {
                IReadOnlyList<TerrainChunkCoordinate> required = TerrainChunkStreamer.RequiredChunks(center, r);
                int expected = (2 * r + 1) * (2 * r + 1);
                var unique = new HashSet<TerrainChunkCoordinate>(required);
                Assert.AreEqual(expected, required.Count, $"radius {r}: count");
                Assert.AreEqual(expected, unique.Count, $"radius {r}: duplicates");
                Assert.IsTrue(unique.Contains(center), $"radius {r}: contains centre");
            }
        }
    }
}

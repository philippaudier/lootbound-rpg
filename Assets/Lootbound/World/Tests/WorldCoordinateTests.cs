using NUnit.Framework;
using Lootbound.World.Coordinates;

namespace Lootbound.World.Tests
{
    public class WorldCoordinateTests
    {
        [Test]
        public void WorldCoordinate_PreservesDoublePrecision()
        {
            // A value a float32 could not represent distinctly from its neighbour.
            double big = 123456.789012345;
            var c = new WorldCoordinate(big, -big);
            Assert.AreEqual(big, c.X);
            Assert.AreEqual(-big, c.Z);
        }

        [Test]
        public void WorldCoordinate_EqualityIsByValue()
        {
            Assert.AreEqual(new WorldCoordinate(1.5, 2.5), new WorldCoordinate(1.5, 2.5));
            Assert.AreNotEqual(new WorldCoordinate(1.5, 2.5), new WorldCoordinate(1.5, 2.6));
        }

        [Test]
        public void ChunkCoordinate_EqualityIsByValue()
        {
            Assert.AreEqual(new ChunkCoordinate(3, -4), new ChunkCoordinate(3, -4));
            Assert.AreNotEqual(new ChunkCoordinate(3, -4), new ChunkCoordinate(3, 4));
        }
    }
}

using NUnit.Framework;
using Lootbound.Gameplay.World;
using Lootbound.Gameplay.World.Chunking;

namespace Lootbound.Tests.EditMode
{
    public class TerrainChunkBuilderTests
    {
        /// <summary>
        /// Deterministic, Unity-free height sampler: a smooth signed function
        /// mapped into [0, TerrainHeight]. Lets the builder be tested purely.
        /// </summary>
        private sealed class FuncSampler : IWorldHeightSampler
        {
            public float TerrainHeight => 200f;
            public bool IsReady => true;

            public float SampleHeight(double worldX, double worldZ)
            {
                double h = System.Math.Sin(worldX * 0.01) * System.Math.Cos(worldZ * 0.013);
                return (float)((h * 0.5 + 0.5) * TerrainHeight); // [0, 200]
            }
        }

        private const float ChunkSize = 128f;
        private const int Res = 33;

        [Test]
        public void Build_HasRequestedResolution()
        {
            var builder = new TerrainChunkBuilder(new FuncSampler());
            TerrainChunkData data = builder.Build(new TerrainChunkCoordinate(0, 0), Res, ChunkSize);

            Assert.AreEqual(Res, data.Resolution);
            Assert.AreEqual(Res, data.Heights.GetLength(0));
            Assert.AreEqual(Res, data.Heights.GetLength(1));
            Assert.AreEqual(ChunkSize, data.ChunkWorldSize);
        }

        [Test]
        public void Build_IsDeterministic()
        {
            var builder = new TerrainChunkBuilder(new FuncSampler());
            TerrainChunkData a = builder.Build(new TerrainChunkCoordinate(2, -3), Res, ChunkSize);
            TerrainChunkData b = builder.Build(new TerrainChunkCoordinate(2, -3), Res, ChunkSize);

            for (int z = 0; z < Res; z++)
            {
                for (int x = 0; x < Res; x++)
                {
                    Assert.AreEqual(a.Heights[z, x], b.Heights[z, x], 0f);
                }
            }
        }

        [Test]
        public void SharedEdge_Horizontal_MatchesExactly()
        {
            var builder = new TerrainChunkBuilder(new FuncSampler());
            TerrainChunkData left = builder.Build(new TerrainChunkCoordinate(0, 0), Res, ChunkSize);
            TerrainChunkData right = builder.Build(new TerrainChunkCoordinate(1, 0), Res, ChunkSize);

            // left's last column is the same world line as right's first column.
            for (int z = 0; z < Res; z++)
            {
                Assert.AreEqual(left.Heights[z, Res - 1], right.Heights[z, 0], 0f, $"row {z}");
            }
        }

        [Test]
        public void SharedEdge_Vertical_MatchesExactly()
        {
            var builder = new TerrainChunkBuilder(new FuncSampler());
            TerrainChunkData bottom = builder.Build(new TerrainChunkCoordinate(0, 0), Res, ChunkSize);
            TerrainChunkData top = builder.Build(new TerrainChunkCoordinate(0, 1), Res, ChunkSize);

            for (int x = 0; x < Res; x++)
            {
                Assert.AreEqual(bottom.Heights[Res - 1, x], top.Heights[0, x], 0f, $"col {x}");
            }
        }

        [Test]
        public void SharedEdge_AcrossNegativeBoundary_MatchesExactly()
        {
            var builder = new TerrainChunkBuilder(new FuncSampler());
            // shared edge sits exactly at world X = 0
            TerrainChunkData west = builder.Build(new TerrainChunkCoordinate(-1, 0), Res, ChunkSize);
            TerrainChunkData east = builder.Build(new TerrainChunkCoordinate(0, 0), Res, ChunkSize);

            for (int z = 0; z < Res; z++)
            {
                Assert.AreEqual(west.Heights[z, Res - 1], east.Heights[z, 0], 0f, $"row {z}");
            }
        }

        [Test]
        public void Heights_AreNormalized_AndDenormalizeToTheSampler()
        {
            var sampler = new FuncSampler();
            var builder = new TerrainChunkBuilder(sampler);
            TerrainChunkData data = builder.Build(new TerrainChunkCoordinate(0, 0), Res, ChunkSize);

            const int z = 10;
            const int x = 7;
            double worldX = (0 + x / (double)(Res - 1)) * ChunkSize;
            double worldZ = (0 + z / (double)(Res - 1)) * ChunkSize;
            float expected = sampler.SampleHeight(worldX, worldZ);

            Assert.AreEqual(expected, data.Heights[z, x] * sampler.TerrainHeight, 1e-2f);
            Assert.GreaterOrEqual(data.Heights[z, x], 0f);
            Assert.LessOrEqual(data.Heights[z, x], 1f);
        }
    }
}

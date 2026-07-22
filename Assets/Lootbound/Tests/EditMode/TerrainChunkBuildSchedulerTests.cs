using System.Collections.Generic;
using NUnit.Framework;
using Lootbound.Gameplay.World;
using Lootbound.Gameplay.World.Chunking;

namespace Lootbound.Tests.EditMode
{
    public class TerrainChunkBuildSchedulerTests
    {
        private sealed class FlatSampler : IWorldHeightSampler
        {
            public float TerrainHeight => 100f;
            public bool IsReady => true;
            public float SampleHeight(double worldX, double worldZ) => 10f;
        }

        private const int Res = 9;
        private const float Size = 128f;

        private static TerrainChunkBuildScheduler CreateScheduler(int maxQueued = 64)
        {
            return new TerrainChunkBuildScheduler(
                new TerrainChunkBuilder(new FlatSampler()), Res, Size, 0, maxQueued);
        }

        private static readonly TerrainChunkCoordinate Player = new TerrainChunkCoordinate(0, 0);

        [Test]
        public void Process_RespectsTheActivationBudget()
        {
            TerrainChunkBuildScheduler scheduler = CreateScheduler();
            for (int x = 0; x < 5; x++)
            {
                scheduler.Request(new TerrainChunkCoordinate(x, 0));
            }

            var results = new List<TerrainChunkData>();
            int finished = scheduler.Process(Player, 2, 1000.0, results);

            Assert.AreEqual(2, finished);
            Assert.AreEqual(2, results.Count);
            Assert.AreEqual(3, scheduler.QueuedCount, "the rest stays queued for later frames");
        }

        [Test]
        public void Process_BuildsNearestFirst()
        {
            TerrainChunkBuildScheduler scheduler = CreateScheduler();
            scheduler.Request(new TerrainChunkCoordinate(2, 2));  // distance 2
            scheduler.Request(new TerrainChunkCoordinate(0, 0));  // distance 0 (under the player)
            scheduler.Request(new TerrainChunkCoordinate(1, 0));  // distance 1

            var results = new List<TerrainChunkData>();
            scheduler.Process(Player, 1, 1000.0, results);
            scheduler.Process(Player, 1, 1000.0, results);
            scheduler.Process(Player, 1, 1000.0, results);

            Assert.AreEqual(new TerrainChunkCoordinate(0, 0), results[0].Coordinate, "player's chunk first");
            Assert.AreEqual(new TerrainChunkCoordinate(1, 0), results[1].Coordinate, "then the near ring");
            Assert.AreEqual(new TerrainChunkCoordinate(2, 2), results[2].Coordinate, "then the outer ring");
        }

        [Test]
        public void Process_BreaksEqualDistancesDeterministically()
        {
            // Four chunks all at Chebyshev distance 1; expected order is (Z, then X).
            var expected = new[]
            {
                new TerrainChunkCoordinate(0, -1),
                new TerrainChunkCoordinate(-1, 0),
                new TerrainChunkCoordinate(1, 0),
                new TerrainChunkCoordinate(0, 1),
            };

            // Request in a scrambled order; the outcome must not depend on it.
            TerrainChunkBuildScheduler scheduler = CreateScheduler();
            scheduler.Request(expected[2]);
            scheduler.Request(expected[0]);
            scheduler.Request(expected[3]);
            scheduler.Request(expected[1]);

            var results = new List<TerrainChunkData>();
            for (int i = 0; i < expected.Length; i++)
            {
                scheduler.Process(Player, 1, 1000.0, results);
            }

            for (int i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i], results[i].Coordinate, $"tie order at {i}");
            }
        }

        [Test]
        public void Request_IgnoresDuplicates()
        {
            TerrainChunkBuildScheduler scheduler = CreateScheduler();
            Assert.IsTrue(scheduler.Request(new TerrainChunkCoordinate(1, 1)));
            Assert.IsFalse(scheduler.Request(new TerrainChunkCoordinate(1, 1)));
            Assert.AreEqual(1, scheduler.QueuedCount);

            var results = new List<TerrainChunkData>();
            scheduler.Process(Player, 10, 1000.0, results);
            Assert.AreEqual(1, results.Count, "a duplicated request builds exactly once");
        }

        [Test]
        public void Cancel_RemovesAnObsoleteRequest()
        {
            TerrainChunkBuildScheduler scheduler = CreateScheduler();
            scheduler.Request(new TerrainChunkCoordinate(1, 0));
            scheduler.Request(new TerrainChunkCoordinate(9, 9));

            Assert.IsTrue(scheduler.Cancel(new TerrainChunkCoordinate(9, 9)));
            Assert.IsFalse(scheduler.Cancel(new TerrainChunkCoordinate(9, 9)), "already cancelled");

            var results = new List<TerrainChunkData>();
            scheduler.Process(Player, 10, 1000.0, results);

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(new TerrainChunkCoordinate(1, 0), results[0].Coordinate);
            Assert.AreEqual(1, scheduler.TotalCancelled);
        }

        [Test]
        public void Request_IsCappedAndOverflowIsIgnored()
        {
            TerrainChunkBuildScheduler scheduler = CreateScheduler(maxQueued: 2);
            Assert.IsTrue(scheduler.Request(new TerrainChunkCoordinate(1, 0)));
            Assert.IsTrue(scheduler.Request(new TerrainChunkCoordinate(2, 0)));
            Assert.IsFalse(scheduler.Request(new TerrainChunkCoordinate(3, 0)), "queue is full");
            Assert.AreEqual(2, scheduler.QueuedCount);
        }

        [Test]
        public void Process_OnAnEmptyQueue_DoesNothing()
        {
            TerrainChunkBuildScheduler scheduler = CreateScheduler();
            var results = new List<TerrainChunkData>();
            Assert.AreEqual(0, scheduler.Process(Player, 5, 1000.0, results));
            Assert.AreEqual(0, results.Count);
        }

        [Test]
        public void Process_ContinuesABuildAcrossMultipleCalls()
        {
            // Zero budget: each call still makes progress (at least one row) but
            // must yield - so one build deterministically spans several frames.
            TerrainChunkBuildScheduler scheduler = CreateScheduler();
            scheduler.Request(new TerrainChunkCoordinate(1, 1));

            var results = new List<TerrainChunkData>();
            int calls = 0;
            while (results.Count == 0 && calls < 200)
            {
                scheduler.Process(Player, 1, 0.0, results);
                calls++;
            }

            Assert.AreEqual(1, results.Count, "the sliced build eventually finishes");
            Assert.Greater(calls, 1, "a zero budget cannot finish a build in one call");
            Assert.IsFalse(scheduler.HasRunningBuild);
        }

        [Test]
        public void Cancel_DiscardsARunningBuild()
        {
            TerrainChunkBuildScheduler scheduler = CreateScheduler();
            var coord = new TerrainChunkCoordinate(5, 5);
            scheduler.Request(coord);

            var results = new List<TerrainChunkData>();
            scheduler.Process(Player, 1, 0.0, results); // starts the build, cannot finish it
            Assert.AreEqual(0, results.Count);
            Assert.IsTrue(scheduler.HasRunningBuild);
            Assert.IsTrue(scheduler.IsPending(coord));

            Assert.IsTrue(scheduler.Cancel(coord), "a running build can be cancelled");
            Assert.IsFalse(scheduler.HasRunningBuild);
            Assert.IsFalse(scheduler.IsPending(coord));
            Assert.AreEqual(1, scheduler.TotalCancelled);

            scheduler.Process(Player, 5, 1000.0, results);
            Assert.AreEqual(0, results.Count, "a cancelled build is never emitted");
        }

        [Test]
        public void Buffers_AreReusedAfterRelease_AndBurstsKeepDistinctArrays()
        {
            TerrainChunkBuildScheduler scheduler = CreateScheduler();
            var results = new List<TerrainChunkData>();

            scheduler.Request(new TerrainChunkCoordinate(0, 0));
            scheduler.Process(Player, 1, 1000.0, results);
            TerrainChunkData first = results[0];
            scheduler.ReleaseBuffers(first);

            results.Clear();
            scheduler.Request(new TerrainChunkCoordinate(1, 0));
            scheduler.Process(Player, 1, 1000.0, results);
            Assert.AreSame(first.Heights, results[0].Heights, "a released buffer set is reused, not reallocated");
            scheduler.ReleaseBuffers(results[0]);

            // A burst finishing several builds before any release must give each
            // result its own arrays - no silent corruption of earlier data.
            results.Clear();
            scheduler.Request(new TerrainChunkCoordinate(2, 0));
            scheduler.Request(new TerrainChunkCoordinate(3, 0));
            scheduler.Process(Player, 2, 1000.0, results);
            Assert.AreNotSame(results[0].Heights, results[1].Heights, "unreleased results keep distinct arrays");
        }
    }
}

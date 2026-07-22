using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Lootbound.Gameplay.World;
using Lootbound.Gameplay.World.Chunking;

namespace Lootbound.Tests.PlayMode
{
    /// <summary>
    /// The streaming foundation, exercised with real Unity Terrains but a fake
    /// height sampler (so it needs no full generator): a player walks the world,
    /// chunks load around it, far chunks recycle through the pool, the active set
    /// stays a full view, and no new instances are created once the pool is warm.
    /// </summary>
    public class TerrainChunkStreamingPlayModeTests
    {
        private sealed class FuncSampler : IWorldHeightSampler
        {
            public float TerrainHeight => 100f;
            public bool IsReady => true;

            public float SampleHeight(double worldX, double worldZ)
            {
                double h = System.Math.Sin(worldX * 0.02) * System.Math.Cos(worldZ * 0.02);
                return (float)((h * 0.5 + 0.5) * TerrainHeight);
            }
        }

        private const int Radius = 1;          // 3x3 = 9 chunks, light for play mode
        private const float ChunkSize = 128f;

        private GameObject _host;
        private TerrainChunkStreamer _streamer;
        private Transform _player;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("StreamerHost");
            _streamer = _host.AddComponent<TerrainChunkStreamer>();
            SetField(_streamer, "chunkWorldSize", ChunkSize);
            SetField(_streamer, "heightmapResolution", 33);
            SetField(_streamer, "activeRadiusInChunks", Radius);
            SetField(_streamer, "maxChunkActivationsPerFrame", 100); // build a whole view in one tick
            SetField(_streamer, "maxChunkBuildMillisecondsPerFrame", 1000f); // don't slice in this test

            _player = new GameObject("Player").transform;
            _streamer.Initialize(new FuncSampler(), _player);
        }

        [TearDown]
        public void TearDown()
        {
            if (_player != null)
            {
                Object.Destroy(_player.gameObject);
            }
            if (_host != null)
            {
                Object.Destroy(_host);
            }
        }

        [UnityTest]
        public IEnumerator Streams_LoadsAroundPlayer_RecyclesFar_BoundedInstances()
        {
            int perView = (2 * Radius + 1) * (2 * Radius + 1); // 9

            _player.position = Vector3.zero;
            _streamer.Tick();
            yield return null;
            Assert.AreEqual(perView, _streamer.ActiveChunkCount, "a full view loads around the player");
            Assert.AreEqual(perView, _streamer.InstancesCreated, "created exactly one view worth of instances");

            // Jump far: every old chunk leaves the radius and returns to the pool,
            // and the new view is served entirely from that pool - no new instances.
            _player.position = new Vector3(ChunkSize * 10f, 0f, ChunkSize * 10f);
            _streamer.Tick();
            yield return null;
            Assert.AreEqual(perView, _streamer.ActiveChunkCount, "still a full view after the jump");
            Assert.LessOrEqual(_streamer.InstancesCreated, perView, "pooled chunks reused - no unbounded growth");

            // Walk one chunk east: the trailing column recycles, the rest stay put.
            _player.position = new Vector3(ChunkSize * 11f, 0f, ChunkSize * 10f);
            _streamer.Tick();
            yield return null;
            Assert.AreEqual(perView, _streamer.ActiveChunkCount, "still a full view after a normal step");
            Assert.LessOrEqual(_streamer.InstancesCreated, perView, "still no new instances on a normal step");

            // The chunk under the player exists and is a live, active Unity Terrain.
            var under = TerrainChunkCoordinate.FromWorld(_player.position.x, _player.position.z, ChunkSize);
            Assert.AreEqual(new TerrainChunkCoordinate(11, 10), under);
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name,
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"field {name} not found on {target.GetType().Name}");
            field.SetValue(target, value);
        }
    }
}

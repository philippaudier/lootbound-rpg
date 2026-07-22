using Lootbound.Gameplay.World.Chunking;
using UnityEngine;

namespace Lootbound.Gameplay.World
{
    /// <summary>
    /// Places the player at the world's spawn (the Refuge) once the world is
    /// built. A client of the generator: the ground height comes from its
    /// <see cref="IWorldHeightSampler"/> sampling - no Terrain involved.
    ///
    /// When a <see cref="TerrainChunkStreamer"/> is assigned, the player is
    /// teleported to the spawn immediately but the CharacterController stays
    /// disabled until the chunk containing the Refuge is active (built, collider
    /// live) - so the spawn no longer depends on the Editor Terrain Preview's
    /// collider. Without a streamer, the player is released immediately and the
    /// preview collider remains the ground (documented legacy path).
    /// </summary>
    public sealed class WorldPlayerSpawner : MonoBehaviour
    {
        [SerializeField] private ProceduralTerrainGenerator generator;
        [SerializeField] private Transform player;
        [SerializeField] private float playerHeight = 1.8f;
        [Tooltip("Optional. When set, the player is held (controller disabled) until the Refuge chunk is streamed in, removing any dependency on the preview terrain's collider.")]
        [SerializeField] private TerrainChunkStreamer streamer;

        private CharacterController _controller;
        private Vector3 _pendingGround;
        private bool _waitingForGround;

        private void OnEnable()
        {
            if (generator == null)
            {
                return;
            }

            generator.OnGenerationComplete += Spawn;
            if (generator.IsGenerated && generator.Context != null)
            {
                Spawn(generator.Context);
            }
        }

        private void OnDisable()
        {
            if (generator != null)
            {
                generator.OnGenerationComplete -= Spawn;
            }
        }

        private void Update()
        {
            if (!_waitingForGround)
            {
                return;
            }

            if (streamer == null || streamer.HasActiveChunkAt(_pendingGround.x, _pendingGround.z))
            {
                _waitingForGround = false;
                if (_controller != null)
                {
                    _controller.enabled = true;
                }
                Debug.Log("[WorldPlayerSpawner] Refuge chunk ready - player released.");
            }
        }

        private void Spawn(TerrainGenerationContext context)
        {
            if (!Application.isPlaying || player == null || context == null || generator == null)
            {
                return;
            }

            Vector3 spawn = context.SpawnPosition;
            float ground = generator.SampleHeight(spawn.x, spawn.z);
            float safeY = ground + playerHeight * 0.5f + 0.5f;

            // Disable the CharacterController for a clean teleport.
            _controller = player.GetComponent<CharacterController>();
            if (_controller != null)
            {
                _controller.enabled = false;
            }

            player.position = new Vector3(spawn.x, safeY, spawn.z);

            // Face a random, seed-stable direction (same behaviour as pre-M4.1).
            var rotation = new System.Random(context.Seed + 99999);
            player.rotation = Quaternion.Euler(0f, (float)(rotation.NextDouble() * 360f), 0f);

            context.SpawnPosition = new Vector3(spawn.x, ground, spawn.z);
            _pendingGround = context.SpawnPosition;

            if (streamer != null && streamer.isActiveAndEnabled)
            {
                // Hold the player until the Refuge chunk (the streamer's highest
                // priority, right under the player) is active.
                _waitingForGround = true;
                Debug.Log($"[WorldPlayerSpawner] Player placed at ({spawn.x:F1}, {safeY:F1}, {spawn.z:F1}) - waiting for the Refuge chunk.");
            }
            else
            {
                if (_controller != null)
                {
                    _controller.enabled = true;
                }
                Debug.Log($"[WorldPlayerSpawner] Player spawned at ({spawn.x:F1}, {safeY:F1}, {spawn.z:F1}) - ground {ground:F1} m");
            }
        }
    }
}

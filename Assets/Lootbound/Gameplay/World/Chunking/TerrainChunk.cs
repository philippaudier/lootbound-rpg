using UnityEngine;

namespace Lootbound.Gameplay.World.Chunking
{
    /// <summary>
    /// Owns exactly one Unity Terrain (with its TerrainData and collider) and does
    /// exactly one thing: display the data it is handed via <see cref="Apply"/>.
    /// It never generates anything, never calls the generator, and never knows the
    /// player or the streamer. Feed it a <see cref="TerrainChunkData"/> from any
    /// source (generation today; save / network / import later) and it shows it.
    /// </summary>
    public sealed class TerrainChunk
    {
        private readonly Terrain _terrain;
        private readonly TerrainData _data;

        public TerrainChunkCoordinate Coordinate { get; private set; }
        public GameObject GameObject { get; }
        public bool IsActive => GameObject != null && GameObject.activeSelf;

        public TerrainChunk(Transform parent, TerrainLayer[] layers)
        {
            _data = new TerrainData();
            GameObject = Terrain.CreateTerrainGameObject(_data);
            _terrain = GameObject.GetComponent<Terrain>();
            if (parent != null)
            {
                GameObject.transform.SetParent(parent, false);
            }
            if (layers != null && layers.Length > 0)
            {
                _data.terrainLayers = layers;
            }
            GameObject.SetActive(false);
        }

        /// <summary>Show the given chunk data on this Unity Terrain.</summary>
        public void Apply(TerrainChunkData chunkData)
        {
            Coordinate = chunkData.Coordinate;

            // Resolution is set before heights; changing it clears the map, so it
            // is only touched on the first apply (all chunks share one resolution).
            if (_data.heightmapResolution != chunkData.Resolution)
            {
                _data.heightmapResolution = chunkData.Resolution;
            }
            _data.size = new Vector3(chunkData.ChunkWorldSize, chunkData.TerrainHeight, chunkData.ChunkWorldSize);
            _data.SetHeights(0, 0, chunkData.Heights);

            GameObject.transform.position = new Vector3(
                (float)chunkData.Coordinate.OriginWorldX(chunkData.ChunkWorldSize),
                0f,
                (float)chunkData.Coordinate.OriginWorldZ(chunkData.ChunkWorldSize));
            GameObject.name = $"TerrainChunk[{chunkData.Coordinate.X},{chunkData.Coordinate.Z}]";
            GameObject.SetActive(true);
        }

        /// <summary>Deactivate for reuse by the pool (no destroy).</summary>
        public void Recycle()
        {
            GameObject.SetActive(false);
        }

        /// <summary>Destroy the Unity object; used only on teardown, never during streaming.</summary>
        public void Dispose()
        {
            if (GameObject != null)
            {
                Object.Destroy(GameObject);
            }
        }
    }
}

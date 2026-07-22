using Unity.Profiling;
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
        private static readonly ProfilerMarker ApplyHeightsMarker = new ProfilerMarker("Chunk.ApplyHeights");
        private static readonly ProfilerMarker ApplyAlphamapsMarker = new ProfilerMarker("Chunk.ApplyAlphamaps");

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

            // No neighbour auto-connection in M3: chunks stitch by sharing exact
            // edge heights, and SetNeighbors / LOD is M4. Auto-connect would also
            // warn when it meets the higher-res Editor Terrain Preview.
            _terrain.allowAutoConnect = false;

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

            using (ApplyHeightsMarker.Auto())
            {
                // Resolution is set before heights; changing it clears the map, so
                // it is only touched on the first apply (all chunks share one).
                if (_data.heightmapResolution != chunkData.Resolution)
                {
                    _data.heightmapResolution = chunkData.Resolution;
                }
                _data.size = new Vector3(chunkData.ChunkWorldSize, chunkData.TerrainHeight, chunkData.ChunkWorldSize);
                _data.SetHeights(0, 0, chunkData.Heights);
            }

            // Paint, only if the data carries an alphamap AND its layer count
            // matches this terrain's layers (else Unity would throw).
            if (chunkData.Alphamaps != null && chunkData.AlphamapResolution > 0 &&
                chunkData.Alphamaps.GetLength(2) == _data.alphamapLayers && _data.alphamapLayers > 0)
            {
                using (ApplyAlphamapsMarker.Auto())
                {
                    if (_data.alphamapResolution != chunkData.AlphamapResolution)
                    {
                        _data.alphamapResolution = chunkData.AlphamapResolution;
                    }
                    _data.SetAlphamaps(0, 0, chunkData.Alphamaps);
                }
            }

            GameObject.transform.position = new Vector3(
                (float)chunkData.Coordinate.OriginWorldX(chunkData.ChunkWorldSize),
                0f,
                (float)chunkData.Coordinate.OriginWorldZ(chunkData.ChunkWorldSize));
            GameObject.name = $"TerrainChunk[{chunkData.Coordinate.X},{chunkData.Coordinate.Z}]";
            GameObject.SetActive(true);
        }

        /// <summary>
        /// Declare the adjacent chunks so Unity stitches edges across LOD (no
        /// cracks). Pass null where there is no active neighbour. Left/right are
        /// -X/+X, top/bottom are +Z/-Z, matching Unity's SetNeighbors order.
        /// </summary>
        public void SetNeighbors(TerrainChunk left, TerrainChunk top, TerrainChunk right, TerrainChunk bottom)
        {
            _terrain.SetNeighbors(left?._terrain, top?._terrain, right?._terrain, bottom?._terrain);
        }

        /// <summary>
        /// Deactivate for reuse by the pool (no destroy). Old neighbour links are
        /// cleared so a recycled terrain never keeps stitching to chunks it no
        /// longer sits beside; the collider deactivates with the GameObject. The
        /// chunk only reactivates inside <see cref="Apply"/>, AFTER its new data
        /// is fully written - stale relief is never displayed.
        /// </summary>
        public void Recycle()
        {
            _terrain.SetNeighbors(null, null, null, null);
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

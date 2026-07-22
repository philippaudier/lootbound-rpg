using UnityEngine;

namespace Lootbound.Gameplay.World
{
    /// <summary>
    /// Dev helper for the "Editor Terrain Preview": the single authored Terrain we
    /// keep for shaders, textures, stamps, gizmos and captures. In Play Mode it
    /// hides that Terrain's MESH so only the streamed chunks are visible, while
    /// keeping its collider so the player still spawns on the ground before chunks
    /// have streamed in. Unity reverts these runtime changes on exit, so the
    /// preview reappears intact in Edit Mode - no manual toggling.
    /// </summary>
    [RequireComponent(typeof(Terrain))]
    public sealed class TerrainPreviewHider : MonoBehaviour
    {
        [Tooltip("Keep the terrain collider active in Play, so the player spawns on the ground until chunks stream in.")]
        [SerializeField] private bool keepColliderForSpawn = true;

        private void Start()
        {
            var terrain = GetComponent<Terrain>();
            if (terrain != null)
            {
                terrain.drawHeightmap = false;
                terrain.drawTreesAndFoliage = false;
            }

            if (!keepColliderForSpawn)
            {
                var collider = GetComponent<TerrainCollider>();
                if (collider != null)
                {
                    collider.enabled = false;
                }
            }
        }
    }
}

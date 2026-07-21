#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace Lootbound.Gameplay.World.Landmarks
{
    /// <summary>
    /// Scene-view debug for landmark terrain seats. For every attached stamp it
    /// draws the foundation radius, the outer (transition) radius, the seat
    /// height and the centre - so it is immediately clear WHY a patch of terrain
    /// was seated, and where the influence fades back to natural relief.
    /// Attach to the ProceduralTerrainGenerator or any scene object.
    /// </summary>
    [ExecuteAlways]
    public class LandmarkTerrainStampGizmos : MonoBehaviour
    {
        [Header("Visualization")]
        [SerializeField] private bool showFoundation = true;
        [SerializeField] private bool showTransition = true;
        [SerializeField] private bool showSeatHeight = true;
        [SerializeField] private bool showLabels = true;

        [Header("Colors")]
        [SerializeField] private Color foundationColor = new Color(1f, 0.55f, 0.2f, 0.9f);
        [SerializeField] private Color transitionColor = new Color(1f, 0.8f, 0.4f, 0.35f);
        [SerializeField] private Color seatColor = new Color(0.4f, 0.9f, 1f, 0.9f);

        [Header("Sizes")]
        [SerializeField] private int circleSegments = 48;

        [Header("References")]
        [SerializeField] private ProceduralTerrainGenerator terrainGenerator;

        private void OnDrawGizmos()
        {
            if (terrainGenerator == null)
            {
                terrainGenerator = FindFirstObjectByType<ProceduralTerrainGenerator>();
            }

            var layout = terrainGenerator != null && terrainGenerator.Context != null
                ? terrainGenerator.Context.LayoutContext
                : null;
            if (layout == null)
            {
                return;
            }

            foreach (var stamp in layout.TerrainStamps)
            {
                if (stamp == null)
                {
                    continue;
                }

                Vector3 center = new Vector3(stamp.CenterX, stamp.SeatHeight, stamp.CenterZ);

                if (showFoundation && stamp.FoundationRadius > 0f)
                {
                    Gizmos.color = foundationColor;
                    DrawCircle(center, stamp.FoundationRadius);
                }

                if (showTransition && stamp.TransitionRadius > 0f)
                {
                    Gizmos.color = transitionColor;
                    DrawCircle(center, stamp.OuterRadius);
                }

                if (showSeatHeight)
                {
                    Gizmos.color = seatColor;
                    Gizmos.DrawSphere(center, 0.6f);
                    Gizmos.DrawLine(center, center + Vector3.up * 4f);
                }

                if (showLabels)
                {
                    Handles.color = seatColor;
                    Handles.Label(center + Vector3.up * 4.5f,
                        $"{stamp.LandmarkId}\n{stamp.Mode} • seat {stamp.SeatHeight:F1}m\ncut {stamp.MaxCutDepth:F1} / fill {stamp.MaxFillHeight:F1} • prio {stamp.Priority}");
                }
            }
        }

        private void DrawCircle(Vector3 center, float radius)
        {
            Vector3 prev = center + new Vector3(radius, 0f, 0f);
            for (int i = 1; i <= circleSegments; i++)
            {
                float angle = (i / (float)circleSegments) * Mathf.PI * 2f;
                Vector3 next = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }
    }
}
#endif

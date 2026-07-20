#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace Lootbound.Gameplay.World.Layout
{
    /// <summary>
    /// Debug visualization for radial world layout in the Scene view.
    /// Attach to the ProceduralTerrainGenerator or any object in the scene.
    /// </summary>
    [ExecuteAlways]
    public class WorldLayoutGizmos : MonoBehaviour
    {
        [Header("Visualization Settings")]
        [SerializeField] private bool showNodes = true;
        [SerializeField] private bool showEdges = true;
        [SerializeField] private bool showPrimaryPaths = true;
        [SerializeField] private bool showReservations = true;
        [SerializeField] private bool showRings = true;
        [SerializeField] private bool showLabels = true;

        [Header("Node Colors")]
        [SerializeField] private Color refugeColor = new Color(0f, 1f, 0.5f, 1f);
        [SerializeField] private Color outerDestinationColor = new Color(1f, 0.4f, 0f, 1f);
        [SerializeField] private Color junctionColor = new Color(0.5f, 0.5f, 1f, 1f);
        [SerializeField] private Color clearingColor = new Color(0.3f, 0.8f, 0.3f, 1f);
        [SerializeField] private Color viewpointColor = new Color(1f, 0.8f, 0f, 1f);
        [SerializeField] private Color deadEndColor = new Color(0.6f, 0.6f, 0.6f, 1f);
        [SerializeField] private Color landmarkColor = new Color(0.9f, 0.5f, 0.9f, 1f);

        [Header("Edge Colors")]
        [SerializeField] private Color primaryPathEdgeColor = new Color(0f, 1f, 0f, 0.8f);
        [SerializeField] private Color branchEdgeColor = new Color(1f, 1f, 0f, 0.5f);

        [Header("Reservation Colors")]
        [SerializeField] private Color encounterColor = new Color(1f, 0.2f, 0.2f, 0.6f);
        [SerializeField] private Color resourceColor = new Color(0.2f, 0.6f, 1f, 0.6f);

        [Header("Ring Colors")]
        [SerializeField] private Color refugeRingColor = new Color(0f, 1f, 0.5f, 0.3f);
        [SerializeField] private Color nearlandsRingColor = new Color(0.3f, 0.8f, 0.3f, 0.3f);
        [SerializeField] private Color wildlandsRingColor = new Color(0.6f, 0.9f, 0.3f, 0.3f);
        [SerializeField] private Color farlandsRingColor = new Color(1f, 0.9f, 0.3f, 0.3f);
        [SerializeField] private Color outerlandsRingColor = new Color(1f, 0.6f, 0.2f, 0.3f);
        [SerializeField] private Color edgelandsRingColor = new Color(1f, 0.3f, 0.2f, 0.3f);
        [SerializeField] private Color voidRingColor = new Color(0.5f, 0.1f, 0.5f, 0.3f);

        [Header("Sizes")]
        [SerializeField] private float nodeGizmoScale = 1f;
        [SerializeField] private float primaryPathWidth = 3f;
        [SerializeField] private float branchWidth = 1.5f;
        [SerializeField] private int ringSegments = 64;

        [Header("References")]
        [SerializeField] private ProceduralTerrainGenerator terrainGenerator;

        private void OnDrawGizmos()
        {
            if (terrainGenerator == null)
            {
                terrainGenerator = FindFirstObjectByType<ProceduralTerrainGenerator>();
            }

            if (terrainGenerator == null || terrainGenerator.Context == null)
            {
                return;
            }

            var layout = terrainGenerator.Context.LayoutContext;
            if (layout == null)
            {
                return;
            }

            // Draw rings first (background)
            if (showRings)
            {
                DrawRings(layout);
            }

            // Draw edges (below nodes)
            if (showEdges || showPrimaryPaths)
            {
                DrawEdges(layout);
            }

            // Draw nodes
            if (showNodes)
            {
                DrawNodes(layout);
            }

            // Draw reservations
            if (showReservations)
            {
                DrawReservations(layout);
            }
        }

        private void DrawRings(WorldLayoutContext layout)
        {
            if (layout.RingConfig == null || !layout.RingConfig.IsValid)
            {
                return;
            }

            Vector3 refugePos = layout.RefugePosition;
            float worldRadius = layout.WorldDiscRadius;

            // Draw each ring boundary as a circle
            DrawRingCircle(WorldRing.Refuge, refugePos, worldRadius, layout.RingConfig, refugeRingColor);
            DrawRingCircle(WorldRing.Nearlands, refugePos, worldRadius, layout.RingConfig, nearlandsRingColor);
            DrawRingCircle(WorldRing.Wildlands, refugePos, worldRadius, layout.RingConfig, wildlandsRingColor);
            DrawRingCircle(WorldRing.Farlands, refugePos, worldRadius, layout.RingConfig, farlandsRingColor);
            DrawRingCircle(WorldRing.Outerlands, refugePos, worldRadius, layout.RingConfig, outerlandsRingColor);
            DrawRingCircle(WorldRing.Edgelands, refugePos, worldRadius, layout.RingConfig, edgelandsRingColor);
            DrawRingCircle(WorldRing.Void, refugePos, worldRadius, layout.RingConfig, voidRingColor);

            // Draw world disc boundary
            Handles.color = new Color(1f, 1f, 1f, 0.5f);
            DrawCircle(refugePos, worldRadius, 2f);
        }

        private void DrawRingCircle(WorldRing ring, Vector3 center, float worldRadius, WorldRingConfig config, Color color)
        {
            float normalizedMin = config.GetMinimumRadius(ring);
            float actualRadius = normalizedMin * worldRadius;

            if (actualRadius > 0f)
            {
                Handles.color = color;
                DrawCircle(center, actualRadius, 1.5f);

                // Draw ring label
                if (showLabels)
                {
                    Vector3 labelPos = center + new Vector3(actualRadius, 5f, 0f);
                    Handles.Label(labelPos, ring.ToString(), GetSmallLabelStyle(color));
                }
            }
        }

        private void DrawCircle(Vector3 center, float radius, float thickness)
        {
            Vector3[] points = new Vector3[ringSegments + 1];
            for (int i = 0; i <= ringSegments; i++)
            {
                float angle = (i / (float)ringSegments) * Mathf.PI * 2f;
                points[i] = center + new Vector3(
                    Mathf.Cos(angle) * radius,
                    1f, // Slight elevation above terrain
                    Mathf.Sin(angle) * radius
                );
            }
            Handles.DrawAAPolyLine(thickness, points);
        }

        private void DrawNodes(WorldLayoutContext layout)
        {
            foreach (var node in layout.NodesOrdered)
            {
                Color color = GetNodeColor(node.Type);
                Gizmos.color = color;

                Vector3 pos = node.Position + Vector3.up * 2f;
                float radius = node.Radius * 0.5f * nodeGizmoScale;

                // Draw sphere
                Gizmos.DrawWireSphere(pos, radius);

                // Draw filled sphere for important nodes
                if (node.Type == WorldNodeType.Refuge || node.Type == WorldNodeType.OuterDestination)
                {
                    Gizmos.color = new Color(color.r, color.g, color.b, 0.3f);
                    Gizmos.DrawSphere(pos, radius);
                    Gizmos.color = color;
                }

                // Draw vertical line to terrain
                Gizmos.DrawLine(node.Position, pos);

                // Draw label
                if (showLabels)
                {
                    string label = $"{node.Type}\n{node.Ring}";
                    if (node.PathStepIndex >= 0)
                    {
                        label += $" [Step {node.PathStepIndex}]";
                    }
                    Handles.Label(pos + Vector3.up * (radius + 1f), label, GetLabelStyle(color));
                }
            }
        }

        private void DrawEdges(WorldLayoutContext layout)
        {
            foreach (var edge in layout.EdgesOrdered)
            {
                if (edge.IsPrimaryPathEdge && !showPrimaryPaths) continue;
                if (!edge.IsPrimaryPathEdge && !showEdges) continue;

                Color color = edge.IsPrimaryPathEdge ? primaryPathEdgeColor : branchEdgeColor;

                // Dim non-traversable edges
                if (!edge.IsTraversable)
                {
                    color = new Color(1f, 0f, 0f, color.a);
                }

                Gizmos.color = color;

                // Draw control points polyline
                var points = edge.ControlPoints;
                if (points.Count >= 2)
                {
                    for (int i = 0; i < points.Count - 1; i++)
                    {
                        Vector3 from = points[i] + Vector3.up * 1f;
                        Vector3 to = points[i + 1] + Vector3.up * 1f;

                        // Draw thick line for primary path
                        if (edge.IsPrimaryPathEdge)
                        {
                            Handles.color = color;
                            Handles.DrawAAPolyLine(primaryPathWidth, from, to);
                        }
                        else
                        {
                            Handles.color = color;
                            Handles.DrawAAPolyLine(branchWidth, from, to);
                        }
                    }
                }

                // Draw edge info at midpoint
                if (showLabels && points.Count > 0)
                {
                    int midIndex = points.Count / 2;
                    Vector3 midPoint = points[midIndex] + Vector3.up * 3f;
                    string label = $"Slope: {edge.AverageSlope:F1}°/{edge.MaxSlope:F1}°";
                    if (!edge.IsTraversable)
                    {
                        label += " [BLOCKED]";
                    }
                    Handles.Label(midPoint, label, GetSmallLabelStyle(color));
                }
            }
        }

        private void DrawReservations(WorldLayoutContext layout)
        {
            // Draw encounter reservations
            Gizmos.color = encounterColor;
            foreach (var reservation in layout.EncounterReservations)
            {
                Vector3 pos = reservation.Position + Vector3.up * 0.5f;
                Gizmos.DrawWireCube(pos, Vector3.one * reservation.Radius * 0.5f);

                if (showLabels)
                {
                    string label = $"E ({reservation.Ring})";
                    Handles.Label(pos + Vector3.up * reservation.Radius, label, GetSmallLabelStyle(encounterColor));
                }
            }

            // Draw resource reservations
            Gizmos.color = resourceColor;
            foreach (var reservation in layout.ResourceReservations)
            {
                Vector3 pos = reservation.Position + Vector3.up * 0.5f;
                Gizmos.DrawWireCube(pos, Vector3.one * reservation.Radius * 0.5f);

                if (showLabels)
                {
                    string label = $"R ({reservation.Ring})";
                    Handles.Label(pos + Vector3.up * reservation.Radius, label, GetSmallLabelStyle(resourceColor));
                }
            }

            // Landmarks are no longer reservations (slice 0.9.10): the
            // LandmarkDirector owns their runtime visualization.
        }

        private Color GetNodeColor(WorldNodeType type)
        {
            return type switch
            {
                WorldNodeType.Refuge => refugeColor,
                WorldNodeType.OuterDestination => outerDestinationColor,
                WorldNodeType.Junction => junctionColor,
                WorldNodeType.Clearing => clearingColor,
                WorldNodeType.Viewpoint => viewpointColor,
                WorldNodeType.Landmark => landmarkColor,
                WorldNodeType.DeadEnd => deadEndColor,
                _ => Color.white
            };
        }

        private static GUIStyle _labelStyle;
        private static GUIStyle GetLabelStyle(Color color)
        {
            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                    fontSize = 12
                };
            }
            _labelStyle.normal.textColor = color;
            return _labelStyle;
        }

        private static GUIStyle _smallLabelStyle;
        private static GUIStyle GetSmallLabelStyle(Color color)
        {
            if (_smallLabelStyle == null)
            {
                _smallLabelStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Normal,
                    fontSize = 10
                };
            }
            _smallLabelStyle.normal.textColor = color;
            return _smallLabelStyle;
        }
    }
}
#endif

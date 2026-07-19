using UnityEngine;
using UnityEngine.InputSystem;
using Lootbound.Gameplay.World;
using Lootbound.Gameplay.World.Progression;
using Lootbound.Gameplay.World.Spawning;

namespace Lootbound.Debugging
{
    /// <summary>
    /// World progression inspector. Toggle with F7.
    /// Shows the WorldRingContext at the player position and at the point the
    /// camera looks at (ring, depth, difficulty, loot tier, ambience), plus
    /// every registered definition's final weight - or its incompatibility
    /// reason - at the aimed position.
    /// </summary>
    public sealed class WorldProgressionDebugPanel : MonoBehaviour
    {
        [SerializeField] private ProceduralTerrainGenerator terrainGenerator;
        [SerializeField] private Transform player;
        [SerializeField] private EncounterRegistry encounterRegistry;
        [SerializeField] private ResourceSpawnRegistry resourceRegistry;
        [SerializeField] private LandmarkRegistry landmarkRegistry;
        [SerializeField] private Key toggleKey = Key.F7;

        [SerializeField]
        [Tooltip("Max distance of the camera aim raycast")]
        private float aimDistance = 400f;

        private bool isVisible;
        private Vector2 scrollPosition;

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current[toggleKey].wasPressedThisFrame)
            {
                isVisible = !isVisible;
            }
        }

        private void OnGUI()
        {
            if (!isVisible) return;

            const float width = 560f;
            const float height = 460f;
            GUILayout.BeginArea(new Rect(10f, 10f, width, height), GUI.skin.box);
            GUILayout.Label("<b>World Progression (F7)</b>", RichLabel());

            var progression = terrainGenerator != null && terrainGenerator.Context != null
                ? terrainGenerator.Context.LayoutContext?.Progression
                : null;

            if (progression == null)
            {
                GUILayout.Label("No progression available (no validated layout yet).");
                GUILayout.EndArea();
                return;
            }

            if (player != null)
            {
                DrawContext("Player", progression.GetContext(player.position));
            }

            scrollPosition = GUILayout.BeginScrollView(scrollPosition);

            if (TryGetAimedPoint(out Vector3 aimedPoint))
            {
                var aimed = progression.GetContext(aimedPoint);
                DrawContext("Aimed", aimed);
                DrawDefinitions(aimed);
            }
            else
            {
                GUILayout.Label("Aimed: (nothing hit)");
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private bool TryGetAimedPoint(out Vector3 point)
        {
            point = default;
            var camera = Camera.main;
            if (camera == null) return false;

            if (Physics.Raycast(camera.transform.position, camera.transform.forward,
                    out RaycastHit hit, aimDistance, ~0, QueryTriggerInteraction.Ignore))
            {
                point = hit.point;
                return true;
            }

            return false;
        }

        private void DrawContext(string label, in WorldRingContext context)
        {
            GUILayout.Label($"<b>{label}</b>  {context.Ring}   depth {context.Depth01:F2}   " +
                            $"{context.DistanceFromRefuge:F0}m{(context.IsInsideWorldDisc ? "" : "   OUTSIDE DISC")}",
                RichLabel());
            GUILayout.Label($"  difficulty {context.Difficulty01:F2}   loot tier T{context.ExpectedLootTier}   " +
                            $"fog {context.FogDensity01:F2}   light-att {context.LightAttenuation01:F2}   " +
                            $"sat {context.Saturation01:F2}   temp {context.Temperature01:F2}");
        }

        private void DrawDefinitions(in WorldRingContext context)
        {
            if (encounterRegistry != null)
            {
                GUILayout.Label("<b>Encounters here</b>", RichLabel());
                foreach (var definition in encounterRegistry.Definitions)
                {
                    if (definition == null) continue;
                    bool ok = WorldContentCompatibility.Evaluate(definition, context.Ring, context.Depth01,
                        out float weight, out string reason);
                    GUILayout.Label(ok
                        ? $"  {definition.EncounterId}: weight {weight:F2}  (diff {definition.DifficultyRating:F1}, loot {definition.LootValue:F1})"
                        : $"  {definition.EncounterId}: - {reason}");
                }
            }

            if (resourceRegistry != null)
            {
                GUILayout.Label("<b>Resources here</b>", RichLabel());
                foreach (var definition in resourceRegistry.Definitions)
                {
                    if (definition == null) continue;
                    bool ok = WorldContentCompatibility.Evaluate(definition, context.Ring, context.Depth01,
                        out float weight, out string reason);
                    GUILayout.Label(ok
                        ? $"  {definition.ResourceId}: weight {weight:F2}  (loot {definition.LootValue:F1})"
                        : $"  {definition.ResourceId}: - {reason}");
                }
            }

            if (landmarkRegistry != null)
            {
                GUILayout.Label("<b>Landmarks here</b>", RichLabel());
                foreach (var definition in landmarkRegistry.Definitions)
                {
                    if (definition == null) continue;
                    bool ok = WorldContentCompatibility.Evaluate(definition, context.Ring, context.Depth01,
                        out float weight, out string reason);
                    GUILayout.Label(ok
                        ? $"  {definition.LandmarkId}: weight {weight:F2}"
                        : $"  {definition.LandmarkId}: - {reason}");
                }
            }
        }

        private static GUIStyle RichLabel()
        {
            var style = new GUIStyle(GUI.skin.label) { richText = true };
            return style;
        }
    }
}

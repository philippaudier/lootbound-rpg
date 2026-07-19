using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using Lootbound.Gameplay.World;
using Lootbound.Gameplay.World.Ambience;
using Lootbound.Gameplay.World.Ambience.Events;
using Lootbound.Gameplay.World.Population;
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
        [SerializeField] private AmbientPopulationController ambientController;
        [SerializeField] private WorldAmbienceController ambienceController;
        [SerializeField] private AmbientEventDirector eventDirector;
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

            DrawAmbienceSection();
            DrawAmbientEventsSection();
            DrawAmbientSection();

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

        private void DrawAmbienceSection()
        {
            if (ambienceController == null)
            {
                return;
            }

            GUILayout.Label("<b>World Ambience</b>", RichLabel());
            GUILayout.Label($"  {(ambienceController.IsReady ? "active" : "inactive")}   applier: {ambienceController.ApplierStatus}");

            if (!ambienceController.IsReady)
            {
                return;
            }

            // Preview Depth Override (debug only): forces the visual context,
            // never touches WorldProgression or gameplay.
            bool previewActive = ambienceController.PreviewDepthOverride.HasValue;
            float actualDepth = ambienceController.ActualDepth01 ?? 0f;

            if (previewActive)
            {
                GUILayout.Label("  <b><color=#FFB020>PREVIEW ACTIVE</color></b>", RichLabel());
                GUILayout.Label($"  Actual Depth01: {actualDepth:F2}   Preview Depth01: {ambienceController.PreviewDepthOverride.Value:F2}");
            }
            else
            {
                GUILayout.Label($"  Actual Depth01: {actualDepth:F2}");
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label("  Preview:", GUILayout.Width(70f));
            float slider = GUILayout.HorizontalSlider(
                ambienceController.PreviewDepthOverride ?? actualDepth, 0f, 1f, GUILayout.Width(160f));
            if (previewActive || Mathf.Abs(slider - actualDepth) > 0.01f)
            {
                ambienceController.PreviewDepthOverride = slider;
            }
            foreach (float preset in new[] { 0f, 0.25f, 0.5f, 0.75f, 1f })
            {
                if (GUILayout.Button(preset.ToString("0.##"), GUILayout.Width(38f)))
                {
                    ambienceController.PreviewDepthOverride = preset;
                }
            }
            if (GUILayout.Button("Off", GUILayout.Width(38f)))
            {
                ambienceController.PreviewDepthOverride = null;
            }
            GUILayout.EndHorizontal();

            var context = ambienceController.LastContext;
            GUILayout.Label($"  Intent: fog {context.FogDensity01:F2}  light-att {context.LightAttenuation01:F2}  " +
                            $"sat {context.Saturation01:F2}  temp {context.Temperature01:F2}");

            var target = ambienceController.TargetState;
            var current = ambienceController.CurrentState;
            var baseline = ambienceController.Baseline;
            GUILayout.Label($"  Fog mfp: {current.MeanFreePath:F0}m -> {target.MeanFreePath:F0}m  (baseline {baseline.MeanFreePath:F0}m)");
            GUILayout.Label($"  Light x{current.DirectionalMultiplier:F2} -> x{target.DirectionalMultiplier:F2}   " +
                            $"Ambient x{current.AmbientMultiplier:F2} -> x{target.AmbientMultiplier:F2}");
            GUILayout.Label($"  Sat {current.SaturationOffset:F1} -> {target.SaturationOffset:F1}   " +
                            $"Temp {current.TemperatureOffset:F1} -> {target.TemperatureOffset:F1}   " +
                            $"Contrast {current.ContrastOffset:F1} -> {target.ContrastOffset:F1}");
            GUILayout.Label($"  Sky zenith #{ColorUtility.ToHtmlStringRGB(current.SkyZenithTint)} -> " +
                            $"#{ColorUtility.ToHtmlStringRGB(target.SkyZenithTint)}  " +
                            $"(baseline #{ColorUtility.ToHtmlStringRGB(baseline.SkyZenithTint)})");
            GUILayout.Label($"  Sky horizon #{ColorUtility.ToHtmlStringRGB(current.SkyHorizonTint)} -> " +
                            $"#{ColorUtility.ToHtmlStringRGB(target.SkyHorizonTint)}  " +
                            $"(baseline #{ColorUtility.ToHtmlStringRGB(baseline.SkyHorizonTint)})");
            string skyExposure = target.ControlSkyExposure
                ? $"Exposure {current.SkyExposure:F2} -> {target.SkyExposure:F2} EV (baseline {baseline.SkyExposure:F2})"
                : "Exposure: off (global profile)";
            GUILayout.Label($"  Sky sat {current.SkyColorSaturation:F2} -> {target.SkyColorSaturation:F2} " +
                            $"(baseline {baseline.SkyColorSaturation:F2})   {skyExposure}");
            GUILayout.Space(6f);
        }

        private readonly List<(string eventId, float remaining)> cooldownScratch = new List<(string, float)>();

        private void DrawAmbientEventsSection()
        {
            if (eventDirector == null)
            {
                return;
            }

            GUILayout.Label("<b>Ambient Events</b>", RichLabel());

            if (ambienceController != null && ambienceController.IsReady)
            {
                var current = ambienceController.CurrentState;
                var target = ambienceController.TargetState;
                GUILayout.Label($"  Birds {current.BirdActivity:F2} -> {target.BirdActivity:F2}   " +
                                $"Insects {current.InsectActivity:F2} -> {target.InsectActivity:F2}   " +
                                $"Wind {current.WindActivity:F2} -> {target.WindActivity:F2}   " +
                                $"Rare {current.RareEventActivity:F2} -> {target.RareEventActivity:F2}");
            }

            float now = Time.time;
            string lastSpawn = eventDirector.HasSpawned ? $"{now - eventDirector.LastSpawnTime:F1}s ago" : "never";
            GUILayout.Label($"  active {eventDirector.ActiveInstances.Count}   last spawn {lastSpawn}   " +
                            $"next evaluation in {Mathf.Max(0f, eventDirector.NextEvaluationAt - now):F1}s");

            foreach (var instance in eventDirector.ActiveInstances)
            {
                float distance = player != null
                    ? Vector3.Distance(player.position, instance.Position)
                    : 0f;
                GUILayout.Label($"    {instance.Profile?.EventId} ({instance.Profile?.Category})  " +
                                $"{distance:F0}m  {instance.RemainingSeconds(now):F1}s left");
            }

            eventDirector.CollectCooldowns(now, cooldownScratch);
            if (cooldownScratch.Count > 0)
            {
                GUILayout.Label("  <b>Cooldowns</b>", RichLabel());
                foreach (var (eventId, remaining) in cooldownScratch)
                {
                    GUILayout.Label($"    {eventId}: {remaining:F1}s");
                }
            }

            if (eventDirector.RecentRejections.Count > 0)
            {
                GUILayout.Label("  <b>Recent rejections</b>", RichLabel());
                foreach (var rejection in eventDirector.RecentRejections)
                {
                    GUILayout.Label($"    [{now - rejection.Time:F0}s ago] {rejection.Reason}");
                }
            }

            GUILayout.Space(6f);
        }

        private void DrawAmbientSection()
        {
            if (ambientController == null)
            {
                return;
            }

            GUILayout.Label("<b>Ambient Population</b>", RichLabel());

            if (!ambientController.IsActive)
            {
                GUILayout.Label("  inactive (waiting for terrain + navigation)");
                return;
            }

            var registry = ambientController.Registry;
            GUILayout.Label(
                $"  alive {registry.TotalAlive}/{ambientController.GlobalBudget}   cells planned {registry.PlannedCellCount}   " +
                $"spawned {registry.TotalSpawned}   despawned {registry.TotalDespawned}   deaths {registry.TotalDeaths}");

            if (ambientController.TryGetCurrentCellInfo(out var cell, out int cellSeed, out var cellContext))
            {
                GUILayout.Label($"  current cell ({cell.x},{cell.y})  seed {cellSeed}  " +
                                $"{cellContext.Ring}  depth {cellContext.Depth01:F2}");
            }

            if (ambientController.RecentRejections.Count > 0)
            {
                GUILayout.Label("  <b>Recent rejections</b>", RichLabel());
                foreach (var rejection in ambientController.RecentRejections)
                {
                    GUILayout.Label($"    {rejection.Reason} ({rejection.Kind}) at ({rejection.Position.x:F0},{rejection.Position.z:F0})");
                }
            }
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

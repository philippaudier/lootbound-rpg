using UnityEngine;
using UnityEngine.InputSystem;
using Lootbound.Gameplay.World.Navigation;
using Lootbound.Gameplay.World.Spawning;

namespace Lootbound.Debugging
{
    /// <summary>
    /// On-screen report of runtime navigation and the last world content
    /// spawning pass.
    /// Toggle with F6 (same pattern as DebugOverlay F3 / TerrainGenerationDebug F5).
    /// </summary>
    public sealed class WorldContentDebugPanel : MonoBehaviour
    {
        [SerializeField] private WorldContentSpawner spawner;
        [SerializeField] private RuntimeNavigationBuilder navigationBuilder;
        [SerializeField] private Key toggleKey = Key.F6;

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
            const float height = 480f;
            GUILayout.BeginArea(new Rect(10f, 10f, width, height), GUI.skin.box);

            DrawNavigationSection();

            GUILayout.Label("<b>World Content Spawning</b>", RichLabel());

            if (spawner == null)
            {
                GUILayout.Label("No WorldContentSpawner assigned.");
                GUILayout.EndArea();
                return;
            }

            var report = spawner.LastReport;
            if (report == null)
            {
                string waiting = spawner.IsWaitingForNavigation
                    ? "Waiting for navigation before spawning..."
                    : "No spawning pass has run yet.";
                GUILayout.Label(waiting);
                GUILayout.EndArea();
                return;
            }

            GUILayout.Label($"NavMesh sample failures: {report.TotalNavMeshMisses}");

            GUILayout.Label(
                $"Reservations: {report.ReservationsReceived}   " +
                $"Recipes: {report.RecipesPlanned}   " +
                $"Spawned: {report.SpawnsSucceeded}   " +
                $"Rejected: {report.SpawnsRejected}");

            scrollPosition = GUILayout.BeginScrollView(scrollPosition);

            GUILayout.Label("<b>Spawned content</b>", RichLabel());
            foreach (var outcome in report.Outcomes)
            {
                string status = outcome.Success
                    ? $"{outcome.SpawnedEntries}/{outcome.RequestedEntries}"
                    : "FAILED";
                string detail = string.IsNullOrEmpty(outcome.FailureDetail) ? "" : $"  [{outcome.FailureDetail}]";
                GUILayout.Label(
                    $"{outcome.Category}  {outcome.DefinitionId}  ({status})  " +
                    $"res={outcome.ReservationId}  node={outcome.HostNodeId}  " +
                    $"ring={outcome.Ring}  path={outcome.RadialPathId ?? "-"}{detail}");
            }

            GUILayout.Label("<b>Plan rejections</b>", RichLabel());
            if (report.Rejections.Count == 0)
            {
                GUILayout.Label("(none)");
            }
            else
            {
                foreach (var rejection in report.Rejections)
                {
                    GUILayout.Label(rejection.ToString());
                }
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawNavigationSection()
        {
            GUILayout.Label("<b>Runtime Navigation</b>", RichLabel());

            if (navigationBuilder == null)
            {
                GUILayout.Label("No RuntimeNavigationBuilder assigned.");
                return;
            }

            var result = navigationBuilder.LastResult;
            var stats = navigationBuilder.Stats;

            string identity = result != null
                ? $"gen={result.GenerationId}  seed={result.Seed}"
                : "gen=-  seed=-";
            GUILayout.Label($"State: {navigationBuilder.State}   {identity}");

            if (result != null && !result.Success)
            {
                GUILayout.Label($"Last failure: {result.FailureReason}");
            }

            if (stats.BuildCount > 0)
            {
                GUILayout.Label(
                    $"Builds: {stats.BuildCount}   last: {stats.LastDurationMs:F0}ms   " +
                    $"avg: {stats.AverageDurationMs:F0}ms   worst: {stats.WorstDurationMs:F0}ms");
                GUILayout.Label(
                    $"Triangles: {stats.LastTriangleCount}   " +
                    $"bounds: {stats.LastBounds.size.x:F0}x{stats.LastBounds.size.y:F0}x{stats.LastBounds.size.z:F0}");
            }
            else
            {
                GUILayout.Label("No build recorded yet.");
            }

            GUILayout.Space(6f);
        }

        private static GUIStyle RichLabel()
        {
            var style = new GUIStyle(GUI.skin.label) { richText = true };
            return style;
        }
    }
}

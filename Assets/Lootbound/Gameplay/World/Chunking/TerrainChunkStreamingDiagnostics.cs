using UnityEngine;
using UnityEngine.InputSystem;

namespace Lootbound.Gameplay.World.Chunking
{
    /// <summary>
    /// Read-only development overlay for chunk streaming, toggled with F7. It
    /// only OBSERVES the streamer's counters - never drives anything - and it
    /// never writes to the console, so it can stay in the scene permanently.
    /// </summary>
    public sealed class TerrainChunkStreamingDiagnostics : MonoBehaviour
    {
        [SerializeField] private TerrainChunkStreamer streamer;
        [SerializeField] private Key toggleKey = Key.F7;

        private bool _visible;

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard[toggleKey].wasPressedThisFrame)
            {
                _visible = !_visible;
            }
        }

        private void OnGUI()
        {
            if (!_visible)
            {
                return;
            }

            GUILayout.BeginArea(new Rect(10, 10, 320, 230), GUI.skin.box);
            GUILayout.Label($"Chunk Streaming  [{toggleKey}]");
            if (streamer == null)
            {
                GUILayout.Label("(no streamer assigned)");
            }
            else
            {
                GUILayout.Label($"Active chunks      : {streamer.ActiveChunkCount}");
                GUILayout.Label($"Queued builds      : {streamer.QueuedBuildCount}");
                GUILayout.Label($"Building now       : {(streamer.HasRunningBuild ? "yes" : "no")}");
                GUILayout.Label($"Pooled (free)      : {streamer.PooledChunkCount}");
                GUILayout.Label($"Instances created  : {streamer.InstancesCreated}");
                GUILayout.Label($"Built / cancelled  : {streamer.TotalChunksBuilt} / {streamer.TotalBuildsCancelled}");
                GUILayout.Label($"Activations (tick) : {streamer.LastTickActivations}");
                GUILayout.Label($"Streaming ms (tick): {streamer.LastTickMilliseconds:F2}  (peak {streamer.PeakTickMilliseconds:F2})");
                GUILayout.Label($"Avg build ms/chunk : {streamer.AverageBuildMilliseconds:F2}");
            }
            GUILayout.EndArea();
        }
    }
}

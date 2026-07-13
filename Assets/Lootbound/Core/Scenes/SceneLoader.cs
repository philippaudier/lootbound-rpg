using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Lootbound.Core.Logging;

namespace Lootbound.Core.Scenes
{
    public static class SceneLoader
    {
        private const string Category = "SceneLoader";

        private static bool isLoading;

        /// <summary>
        /// True if a scene load is currently in progress.
        /// </summary>
        public static bool IsLoading => isLoading;

        /// <summary>
        /// Checks if a scene can be loaded.
        /// </summary>
        /// <param name="sceneName">The scene name to check.</param>
        /// <returns>True if the scene exists and can be loaded.</returns>
        public static bool CanLoadScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                return false;
            }

            return Application.CanStreamedLevelBeLoaded(sceneName);
        }

        public static void LoadScene(string sceneName, Action onComplete = null)
        {
            if (isLoading)
            {
                LootboundLog.Warning(Category, $"Load already in progress, ignoring request for: {sceneName}");
                return;
            }

            if (!CanLoadScene(sceneName))
            {
                LootboundLog.Error(Category, $"Cannot load scene (not in Build Settings or invalid name): {sceneName}");
                return;
            }

            isLoading = true;
            LootboundLog.Info(Category, $"Loading scene: {sceneName}");

            var operation = SceneManager.LoadSceneAsync(sceneName);
            if (operation == null)
            {
                isLoading = false;
                LootboundLog.Error(Category, $"Failed to start loading scene: {sceneName}");
                return;
            }

            operation.completed += _ =>
            {
                isLoading = false;
                LootboundLog.Info(Category, $"Scene loaded: {sceneName}");
                onComplete?.Invoke();
            };
        }

        public static void LoadSceneAdditive(string sceneName, Action onComplete = null)
        {
            if (isLoading)
            {
                LootboundLog.Warning(Category, $"Load already in progress, ignoring additive request for: {sceneName}");
                return;
            }

            if (!CanLoadScene(sceneName))
            {
                LootboundLog.Error(Category, $"Cannot load scene additively (not in Build Settings or invalid name): {sceneName}");
                return;
            }

            isLoading = true;
            LootboundLog.Info(Category, $"Loading scene additively: {sceneName}");

            var operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            if (operation == null)
            {
                isLoading = false;
                LootboundLog.Error(Category, $"Failed to start loading scene additively: {sceneName}");
                return;
            }

            operation.completed += _ =>
            {
                isLoading = false;
                LootboundLog.Info(Category, $"Scene loaded additively: {sceneName}");
                onComplete?.Invoke();
            };
        }

        public static string GetActiveSceneName()
        {
            return SceneManager.GetActiveScene().name;
        }

        /// <summary>
        /// Resets the loading state. Use only for testing or error recovery.
        /// </summary>
        public static void ResetLoadingState()
        {
            isLoading = false;
        }
    }
}

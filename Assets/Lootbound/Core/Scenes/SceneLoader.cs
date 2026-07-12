using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Lootbound.Core.Logging;

namespace Lootbound.Core.Scenes
{
    public static class SceneLoader
    {
        private const string Category = "SceneLoader";

        public static void LoadScene(string sceneName, Action onComplete = null)
        {
            LootboundLog.Info(Category, $"Loading scene: {sceneName}");

            var operation = SceneManager.LoadSceneAsync(sceneName);
            if (operation == null)
            {
                LootboundLog.Error(Category, $"Failed to start loading scene: {sceneName}");
                return;
            }

            operation.completed += _ =>
            {
                LootboundLog.Info(Category, $"Scene loaded: {sceneName}");
                onComplete?.Invoke();
            };
        }

        public static void LoadSceneAdditive(string sceneName, Action onComplete = null)
        {
            LootboundLog.Info(Category, $"Loading scene additively: {sceneName}");

            var operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            if (operation == null)
            {
                LootboundLog.Error(Category, $"Failed to start loading scene additively: {sceneName}");
                return;
            }

            operation.completed += _ =>
            {
                LootboundLog.Info(Category, $"Scene loaded additively: {sceneName}");
                onComplete?.Invoke();
            };
        }

        public static string GetActiveSceneName()
        {
            return SceneManager.GetActiveScene().name;
        }
    }
}

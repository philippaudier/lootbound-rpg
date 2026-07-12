using UnityEngine;
using Lootbound.Core.Configuration;
using Lootbound.Core.Logging;
using Lootbound.Core.Scenes;

namespace Lootbound.Core.Bootstrap
{
    public class GameBootstrap : MonoBehaviour
    {
        private const string Category = "Bootstrap";

        [SerializeField] private LootboundGameConfig gameConfig;
        [SerializeField] private bool loadDefaultSceneOnStart = true;

        private static GameBootstrap instance;
        private static bool isBootstrapped;

        public static bool IsBootstrapped => isBootstrapped;
        public static LootboundGameConfig GameConfig => instance?.gameConfig;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                LootboundLog.Warning(Category, "Duplicate GameBootstrap detected, destroying...");
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeGame();
        }

        private void InitializeGame()
        {
            if (isBootstrapped)
            {
                LootboundLog.Warning(Category, "Game already bootstrapped, skipping initialization.");
                return;
            }

            if (gameConfig == null)
            {
                LootboundLog.Error(Category, "GameConfig is not assigned!");
                return;
            }

            LootboundLog.Initialize(gameConfig.LogLevel);
            LootboundLog.Info(Category, $"Initializing Lootbound v{gameConfig.GameVersion}");
            LootboundLog.Info(Category, $"Unity version: {Application.unityVersion}");
            LootboundLog.Info(Category, $"Debug tools: {(gameConfig.EnableDebugTools ? "Enabled" : "Disabled")}");

            isBootstrapped = true;

            if (loadDefaultSceneOnStart)
            {
                LoadDefaultScene();
            }
        }

        private void LoadDefaultScene()
        {
            string targetScene = gameConfig.DefaultGameplayScene;

            if (string.IsNullOrEmpty(targetScene))
            {
                LootboundLog.Error(Category, "Default gameplay scene is not configured!");
                return;
            }

            LootboundLog.Info(Category, $"Loading default scene: {targetScene}");
            SceneLoader.LoadScene(targetScene);
        }

        public static void LoadScene(string sceneName)
        {
            SceneLoader.LoadScene(sceneName);
        }
    }
}

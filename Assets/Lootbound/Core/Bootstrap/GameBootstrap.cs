using UnityEngine;
using Lootbound.Core.Configuration;
using Lootbound.Core.Logging;
using Lootbound.Core.Scenes;

namespace Lootbound.Core.Bootstrap
{
    public class GameBootstrap : MonoBehaviour
    {
        private const string Category = "Bootstrap";

        [Header("Configuration")]
        [SerializeField] private LootboundGameConfig gameConfig;

        [Header("Development Menu")]
        [Tooltip("Prefab with DevelopmentMenuController. If assigned, scene selection is shown at boot instead of auto-loading.")]
        [SerializeField] private GameObject developmentMenuPrefab;

        [Header("Legacy Behavior")]
        [Tooltip("If true and no development menu is configured, loads the default scene automatically.")]
        [SerializeField] private bool loadDefaultSceneOnStart = true;

        private static GameBootstrap instance;
        private static bool isBootstrapped;

        private GameObject menuInstance;

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

            // Prioritize development menu over legacy auto-load
            if (developmentMenuPrefab != null)
            {
                InitializeDevelopmentMenu();
            }
            else if (loadDefaultSceneOnStart)
            {
                LoadDefaultScene();
            }
        }

        private void InitializeDevelopmentMenu()
        {
            LootboundLog.Info(Category, "Initializing development menu...");

            // Instantiate the menu as a child of bootstrap (persists across scenes)
            menuInstance = Instantiate(developmentMenuPrefab, transform);

            // The DevelopmentMenuController will self-initialize via its Awake/Start
            // and open scene selection automatically when it detects it's in the boot scene

            LootboundLog.Info(Category, "Development menu instantiated.");
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

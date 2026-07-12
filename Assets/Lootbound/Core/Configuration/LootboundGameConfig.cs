using UnityEngine;

namespace Lootbound.Core.Configuration
{
    public enum LogLevel
    {
        Verbose = 0,
        Info = 1,
        Warning = 2,
        Error = 3,
        None = 4
    }

    [CreateAssetMenu(fileName = "LootboundGameConfig", menuName = "Lootbound/Game Config")]
    public class LootboundGameConfig : ScriptableObject
    {
        [Header("Scenes")]
        [SerializeField] private string defaultGameplayScene = "10_FoundationSandbox";

        [Header("Debug")]
        [SerializeField] private bool enableDebugTools = true;
        [SerializeField] private LogLevel logLevel = LogLevel.Info;

        [Header("Version")]
        [SerializeField] private string gameVersion = "0.1.0";

        public string DefaultGameplayScene => defaultGameplayScene;
        public bool EnableDebugTools => enableDebugTools;
        public LogLevel LogLevel => logLevel;
        public string GameVersion => gameVersion;
    }
}

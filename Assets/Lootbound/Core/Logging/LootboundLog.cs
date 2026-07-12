using UnityEngine;
using Lootbound.Core.Configuration;

namespace Lootbound.Core.Logging
{
    public static class LootboundLog
    {
        private static LogLevel currentLogLevel = LogLevel.Info;
        private static bool isInitialized;

        public static void Initialize(LogLevel level)
        {
            currentLogLevel = level;
            isInitialized = true;
        }

        public static void Verbose(string category, string message)
        {
            if (ShouldLog(LogLevel.Verbose))
            {
                Debug.Log(Format(category, message));
            }
        }

        public static void Info(string category, string message)
        {
            if (ShouldLog(LogLevel.Info))
            {
                Debug.Log(Format(category, message));
            }
        }

        public static void Warning(string category, string message)
        {
            if (ShouldLog(LogLevel.Warning))
            {
                Debug.LogWarning(Format(category, message));
            }
        }

        public static void Error(string category, string message)
        {
            if (ShouldLog(LogLevel.Error))
            {
                Debug.LogError(Format(category, message));
            }
        }

        private static bool ShouldLog(LogLevel messageLevel)
        {
            if (!isInitialized)
            {
                return true;
            }
            return messageLevel >= currentLogLevel;
        }

        private static string Format(string category, string message)
        {
            return $"[Lootbound][{category}] {message}";
        }
    }
}

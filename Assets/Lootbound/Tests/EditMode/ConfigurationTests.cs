using NUnit.Framework;
using UnityEngine;
using Lootbound.Core.Configuration;

namespace Lootbound.Tests.EditMode
{
    public class ConfigurationTests
    {
        [Test]
        public void LootboundGameConfig_CanBeCreated()
        {
            var config = ScriptableObject.CreateInstance<LootboundGameConfig>();

            Assert.That(config, Is.Not.Null);

            Object.DestroyImmediate(config);
        }

        [Test]
        public void LootboundGameConfig_HasDefaultValues()
        {
            var config = ScriptableObject.CreateInstance<LootboundGameConfig>();

            // Default values should be set
            Assert.That(config.DefaultGameplayScene, Is.EqualTo("10_FoundationSandbox"));
            Assert.That(config.EnableDebugTools, Is.True);
            Assert.That(config.LogLevel, Is.EqualTo(LogLevel.Info));
            Assert.That(config.GameVersion, Is.EqualTo("0.1.0"));

            Object.DestroyImmediate(config);
        }
    }
}

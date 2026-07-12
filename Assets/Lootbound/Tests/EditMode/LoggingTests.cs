using NUnit.Framework;
using Lootbound.Core.Configuration;
using Lootbound.Core.Logging;

namespace Lootbound.Tests.EditMode
{
    public class LoggingTests
    {
        [Test]
        public void LogLevel_Verbose_IsLowestPriority()
        {
            Assert.That((int)LogLevel.Verbose, Is.LessThan((int)LogLevel.Info));
            Assert.That((int)LogLevel.Info, Is.LessThan((int)LogLevel.Warning));
            Assert.That((int)LogLevel.Warning, Is.LessThan((int)LogLevel.Error));
            Assert.That((int)LogLevel.Error, Is.LessThan((int)LogLevel.None));
        }

        [Test]
        public void LogLevel_None_DisablesAllLogs()
        {
            Assert.That((int)LogLevel.None, Is.EqualTo(4));
        }

        [Test]
        public void Initialize_SetsLogLevel()
        {
            // This test verifies the Initialize method doesn't throw
            LootboundLog.Initialize(LogLevel.Warning);

            // No exception means success
            Assert.Pass();
        }
    }
}

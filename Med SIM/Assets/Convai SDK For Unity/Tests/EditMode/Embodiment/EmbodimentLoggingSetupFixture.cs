using System;
using Convai.Domain.Logging;
using Convai.Runtime;
using Convai.Runtime.Logging;
using NUnit.Framework;

namespace Convai.Tests.EditMode.Embodiment
{
    [SetUpFixture]
    public sealed class EmbodimentLoggingSetupFixture
    {
        private ConvaiSettings _settings;
        private LogLevel _originalGlobalLevel;
        private LogLevelOverride[] _originalCategoryOverrides;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _settings = ConvaiSettings.Instance;
            if (_settings == null)
                return;

            _originalGlobalLevel = _settings.GlobalLogLevel;
            _originalCategoryOverrides = CloneOverrides(_settings.CategoryOverrides);
            _settings.SetGlobalLogLevel(LogLevel.Trace);
            _settings.SetCategoryOverrides(Array.Empty<LogLevelOverride>());
            LoggingConfig.InvalidateCache();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            if (_settings == null)
                return;

            _settings.SetGlobalLogLevel(_originalGlobalLevel);
            _settings.SetCategoryOverrides(CloneOverrides(_originalCategoryOverrides));
            LoggingConfig.InvalidateCache();
        }

        private static LogLevelOverride[] CloneOverrides(LogLevelOverride[] source)
        {
            if (source == null || source.Length == 0)
                return Array.Empty<LogLevelOverride>();

            var copy = new LogLevelOverride[source.Length];
            Array.Copy(source, copy, source.Length);
            return copy;
        }
    }
}

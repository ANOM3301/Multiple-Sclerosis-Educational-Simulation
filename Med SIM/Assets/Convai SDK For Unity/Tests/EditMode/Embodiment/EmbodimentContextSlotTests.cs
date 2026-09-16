using System.Text.RegularExpressions;
using Convai.Domain.Logging;
using Convai.Runtime;
using Convai.Runtime.Embodiment;
using Convai.Runtime.Logging;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Convai.Tests.EditMode.Embodiment
{
    public sealed class EmbodimentContextSlotTests
    {
        private ConvaiSettings _settings;
        private LogLevel _originalGlobalLevel;
        private LogLevelOverride[] _originalCategoryOverrides;

        [SetUp]
        public void SetUp()
        {
            _settings = ConvaiSettings.Instance;
            if (_settings == null)
                return;

            _originalGlobalLevel = _settings.GlobalLogLevel;
            _originalCategoryOverrides = CloneOverrides(_settings.CategoryOverrides);
            _settings.SetGlobalLogLevel(LogLevel.Trace);
            _settings.SetCategoryOverrides(System.Array.Empty<LogLevelOverride>());
            LoggingConfig.InvalidateCache();
        }

        [TearDown]
        public void TearDown()
        {
            if (_settings == null)
                return;

            _settings.SetGlobalLogLevel(_originalGlobalLevel);
            _settings.SetCategoryOverrides(CloneOverrides(_originalCategoryOverrides));
            LoggingConfig.InvalidateCache();
        }

        private interface IFakeSource { }

        private sealed class FakeSourceA : IFakeSource { }

        private sealed class FakeSourceB : IFakeSource { }

        [Test]
        public void TryRegister_NullCandidate_ReturnsFalseAndStaysEmpty()
        {
            EmbodimentContextSlot<IFakeSource> slot = new("fake source");

            Assert.IsFalse(slot.TryRegister(null));
            Assert.IsNull(slot.Current);
        }

        [Test]
        public void TryRegister_FirstCandidate_RaisesChangedAndStoresCurrent()
        {
            EmbodimentContextSlot<IFakeSource> slot = new("fake source");
            IFakeSource captured = null;
            int eventCount = 0;
            slot.Changed += value => { captured = value; eventCount++; };

            FakeSourceA candidate = new();
            bool result = slot.TryRegister(candidate);

            Assert.IsTrue(result);
            Assert.AreSame(candidate, slot.Current);
            Assert.AreSame(candidate, captured);
            Assert.AreEqual(1, eventCount);
        }

        [Test]
        public void TryRegister_SameCandidateTwice_SecondCallNoOps()
        {
            EmbodimentContextSlot<IFakeSource> slot = new("fake source");
            int eventCount = 0;
            slot.Changed += _ => eventCount++;

            FakeSourceA candidate = new();
            slot.TryRegister(candidate);

            bool secondResult = slot.TryRegister(candidate);

            Assert.IsFalse(secondResult);
            Assert.AreSame(candidate, slot.Current);
            Assert.AreEqual(1, eventCount);
        }

        [Test]
        public void TryRegister_DifferentCandidate_RejectedAndOriginalKept()
        {
            EmbodimentContextSlot<IFakeSource> slot = new("fake source");
            FakeSourceA first = new();
            slot.TryRegister(first);

            FakeSourceB second = new();
            LogAssert.Expect(UnityEngine.LogType.Warning, new Regex("Duplicate fake source"));
            bool result = slot.TryRegister(second);

            Assert.IsFalse(result);
            Assert.AreSame(first, slot.Current);
        }

        [Test]
        public void Unregister_MatchingCandidate_ClearsAndRaisesNull()
        {
            EmbodimentContextSlot<IFakeSource> slot = new("fake source");
            FakeSourceA candidate = new();
            slot.TryRegister(candidate);

            IFakeSource captured = candidate;
            int eventCount = 0;
            slot.Changed += value => { captured = value; eventCount++; };

            bool result = slot.Unregister(candidate);

            Assert.IsTrue(result);
            Assert.IsNull(slot.Current);
            Assert.IsNull(captured);
            Assert.AreEqual(1, eventCount);
        }

        [Test]
        public void Unregister_NonMatchingCandidate_NoOps()
        {
            EmbodimentContextSlot<IFakeSource> slot = new("fake source");
            FakeSourceA registered = new();
            slot.TryRegister(registered);

            int eventCount = 0;
            slot.Changed += _ => eventCount++;

            FakeSourceB stranger = new();
            bool result = slot.Unregister(stranger);

            Assert.IsFalse(result);
            Assert.AreSame(registered, slot.Current);
            Assert.AreEqual(0, eventCount);
        }

        [Test]
        public void Unregister_EmptySlot_NoOps()
        {
            EmbodimentContextSlot<IFakeSource> slot = new("fake source");

            Assert.IsFalse(slot.Unregister(new FakeSourceA()));
            Assert.IsNull(slot.Current);
        }

        [Test]
        public void TryRegister_AfterUnregister_AcceptsNewSource()
        {
            EmbodimentContextSlot<IFakeSource> slot = new("fake source");
            FakeSourceA first = new();
            slot.TryRegister(first);
            slot.Unregister(first);

            FakeSourceB second = new();
            Assert.IsTrue(slot.TryRegister(second));
            Assert.AreSame(second, slot.Current);
        }

        [Test]
        public void Changed_HandlerThrows_ExceptionLoggedAndSlotStaysConsistent()
        {
            EmbodimentContextSlot<IFakeSource> slot = new("fake source");
            slot.Changed += _ => throw new System.InvalidOperationException("boom");

            LogAssert.Expect(UnityEngine.LogType.Error, new Regex("InvalidOperationException: boom"));
            FakeSourceA candidate = new();
            bool result = slot.TryRegister(candidate);

            Assert.IsTrue(result);
            Assert.AreSame(candidate, slot.Current);
        }

        private static LogLevelOverride[] CloneOverrides(LogLevelOverride[] source)
        {
            if (source == null || source.Length == 0)
                return System.Array.Empty<LogLevelOverride>();

            var copy = new LogLevelOverride[source.Length];
            System.Array.Copy(source, copy, source.Length);
            return copy;
        }
    }
}

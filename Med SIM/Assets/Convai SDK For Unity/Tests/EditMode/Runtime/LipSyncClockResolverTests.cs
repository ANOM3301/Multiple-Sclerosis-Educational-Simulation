using Convai.Modules.LipSync;
using NUnit.Framework;

namespace Convai.Tests.EditMode.Runtime
{
    [TestFixture]
    public class LipSyncClockResolverTests
    {
        [Test]
        public void Create_ReturnsNonNullClock()
        {
            IPlaybackClock clock = LipSyncClockResolver.Create();
            Assert.IsNotNull(clock);
        }

        [Test]
        public void Create_ReturnedClock_IsValid()
        {
            IPlaybackClock clock = LipSyncClockResolver.Create();
            Assert.IsTrue(clock.IsValid);
        }

        [Test]
        public void Create_InEditorOrNonWebGLContext_ReturnsDspTimePlaybackClock()
        {
            // The #if UNITY_WEBGL && !UNITY_EDITOR guard ensures DspTimePlaybackClock is
            // always produced in the editor and on native platforms.
            IPlaybackClock clock = LipSyncClockResolver.Create();
            Assert.IsInstanceOf<DspTimePlaybackClock>(clock);
        }

        [Test]
        public void Create_CalledTwice_ReturnsTwoIndependentInstances()
        {
            IPlaybackClock a = LipSyncClockResolver.Create();
            IPlaybackClock b = LipSyncClockResolver.Create();
            Assert.AreNotSame(a, b);
        }
    }
}

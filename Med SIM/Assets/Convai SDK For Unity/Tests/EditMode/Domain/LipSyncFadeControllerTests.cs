using Convai.Modules.LipSync;
using NUnit.Framework;

namespace Convai.Tests.EditMode.Domain
{
    [TestFixture]
    public class LipSyncFadeControllerTests
    {
        [Test]
        public void Begin_WithNullCurrentValues_StartsFadeWithoutThrowing()
        {
            // Arrange
            FadeController fadeController = new();

            // Act
            fadeController.Begin(null, 0.2f);

            // Assert
            Assert.IsTrue(fadeController.IsActive);
        }

        [Test]
        public void Tick_BeforeCompletion_ScalesOutputTowardZero()
        {
            // Arrange
            FadeController fadeController = new();
            float[] output = { 1f, 0.5f };
            fadeController.Begin(new[] { 1f, 0.5f }, 1f);

            // Act
            bool stillFading = fadeController.Tick(0.1f, output);

            // Assert
            Assert.IsTrue(stillFading);
            Assert.That(output[0], Is.LessThan(1f));
        }

        [Test]
        public void Tick_AfterFadeDuration_CompletesAndZerosOutput()
        {
            // Arrange
            FadeController fadeController = new();
            float[] output = { 1f, 0.5f };
            fadeController.Begin(new[] { 1f, 0.5f }, 0.1f);

            // Act
            bool stillFading = true;
            for (int i = 0; i < 4 && stillFading; i++) stillFading = fadeController.Tick(1f, output);

            // Assert
            Assert.IsFalse(stillFading);
            Assert.AreEqual(0f, output[0], 0.0001f);
            Assert.AreEqual(0f, output[1], 0.0001f);
        }

        [Test]
        public void Reset_AfterBegin_ClearsActiveStateAndProgress()
        {
            // Arrange
            FadeController fadeController = new();
            fadeController.Begin(new[] { 1f }, 0.2f);

            // Act
            fadeController.Reset();

            // Assert
            Assert.IsFalse(fadeController.IsActive);
            Assert.AreEqual(0f, fadeController.Progress, 0.0001f);
        }
    }
}

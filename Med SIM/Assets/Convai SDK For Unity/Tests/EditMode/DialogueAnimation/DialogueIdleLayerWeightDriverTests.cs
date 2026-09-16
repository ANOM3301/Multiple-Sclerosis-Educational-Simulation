using Convai.Modules.DialogueAnimation.Runtime.Driver;
using NUnit.Framework;

namespace Convai.Tests.EditMode.DialogueAnimation
{
    public sealed class DialogueIdleLayerWeightDriverTests
    {
        private const float Epsilon = 1e-5f;

        [Test]
        public void Initialize_EmptyIdleLibrary_ForcesIdleOverlayWeightZero()
        {
            DialogueIdleLayerWeightDriver driver = new();

            driver.Initialize(initialSpeaking: false, config: null, hasValidIdleLibrary: false);

            Assert.AreEqual(0f, driver.CurrentIdleOverlayWeight, Epsilon);
            Assert.AreEqual(1f, driver.CurrentBaseWeight, Epsilon);
        }

        [Test]
        public void Initialize_ValidIdleLibrary_UsesDefaultIdleOverlayWeight()
        {
            DialogueIdleLayerWeightDriver driver = new();

            driver.Initialize(initialSpeaking: false, config: null, hasValidIdleLibrary: true);

            Assert.AreEqual(1f, driver.CurrentIdleOverlayWeight, Epsilon);
        }

        [Test]
        public void Tick_WhenIdleLibraryBecomesEmpty_FadesIdleOverlayWeightOut()
        {
            DialogueIdleLayerWeightDriver driver = new();
            driver.Initialize(initialSpeaking: false, config: null, hasValidIdleLibrary: true);

            driver.Tick(
                deltaTime: 2f,
                speaking: false,
                config: null,
                writer: new DialogueConductorBinding(),
                owner: null,
                baseLayerIndex: 0,
                idleOverlayLayerIndex: 1,
                hasValidIdleLibrary: false);

            Assert.AreEqual(0f, driver.CurrentIdleOverlayWeight, Epsilon);
            Assert.AreEqual(1f, driver.CurrentBaseWeight, Epsilon);
        }
    }
}

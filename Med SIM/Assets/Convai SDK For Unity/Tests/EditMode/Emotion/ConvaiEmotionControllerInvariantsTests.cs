using Convai.Domain.Embodiment.Modules;
using Convai.Modules.Emotion.Components;
using Convai.Modules.Emotion.Profiles;
using Convai.Runtime.Animation;
using Convai.Tests.EditMode.Fixtures;
using NUnit.Framework;

namespace Convai.Tests.EditMode.Emotion
{
    [TestFixture]
    public sealed class ConvaiEmotionControllerInvariantsTests
        : EmbodimentReceiverTestsBase<ConvaiEmotionController, ConvaiEmotionProfile>
    {
        protected override string ExpectedModuleId => ModuleIds.Emotion;
        protected override EmbodimentTickPhase ExpectedPhase => EmbodimentTickPhase.Cognition;
        protected override ConvaiEmotionProfile CreateValidProfile() => ConvaiEmotionProfile.CreateDefault();
    }
}

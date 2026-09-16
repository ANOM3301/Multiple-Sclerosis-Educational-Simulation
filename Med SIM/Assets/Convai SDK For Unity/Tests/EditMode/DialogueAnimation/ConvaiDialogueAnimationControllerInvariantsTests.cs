using Convai.Domain.Embodiment.Modules;
using Convai.Modules.DialogueAnimation.Components;
using Convai.Modules.DialogueAnimation.Profiles;
using Convai.Runtime.Animation;
using Convai.Tests.EditMode.Fixtures;
using NUnit.Framework;

namespace Convai.Tests.EditMode.DialogueAnimation
{
    [TestFixture]
    public sealed class ConvaiDialogueAnimationControllerInvariantsTests
        : EmbodimentReceiverTestsBase<ConvaiDialogueAnimationController, ConvaiDialogueAnimationProfile>
    {
        protected override string ExpectedModuleId => ModuleIds.DialogueAnimation;
        protected override EmbodimentTickPhase ExpectedPhase => EmbodimentTickPhase.Expression;
        protected override ConvaiDialogueAnimationProfile CreateValidProfile() => ConvaiDialogueAnimationProfile.CreateDefault();
    }
}
